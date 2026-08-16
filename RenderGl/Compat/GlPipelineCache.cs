/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using MatterHackers.RenderCore;

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// Keyed caches for the immutable objects a WebGPU draw needs: shader modules by source key,
	/// pipelines by their descriptor, bind groups by theirs.
	/// <para>
	/// This is the direct heir of the classic path's <c>blendStateCache</c>. The difference is only that
	/// WebGPU bakes far more into one object, so the key is the whole
	/// <see cref="RenderPipelineDescriptor"/> - which is why that struct implements
	/// <see cref="IEquatable{T}"/>. Same state in means the same instance out, and that identity is what
	/// tests assert on.
	/// </para>
	/// </summary>
	public class GlPipelineCache : IDisposable
	{
		private readonly IRenderDevice device;
		private readonly Dictionary<string, IShaderModule> modules = new Dictionary<string, IShaderModule>(StringComparer.Ordinal);
		private readonly Dictionary<RenderPipelineDescriptor, IRenderPipeline> pipelines
			= new Dictionary<RenderPipelineDescriptor, IRenderPipeline>();

		private readonly Dictionary<BindGroupDescriptor, IBindGroup> bindGroups
			= new Dictionary<BindGroupDescriptor, IBindGroup>();

		/// <summary>Creates a cache over a device.</summary>
		/// <param name="device">The device that creates the cached objects.</param>
		public GlPipelineCache(IRenderDevice device)
		{
			this.device = device ?? throw new ArgumentNullException(nameof(device));
		}

		/// <summary>How many distinct pipelines have been created. A cache-hit assertion reads this.</summary>
		public int PipelineCount => this.pipelines.Count;

		/// <summary>How many distinct bind groups have been created.</summary>
		public int BindGroupCount => this.bindGroups.Count;

		/// <summary>How many shader modules have been compiled.</summary>
		public int ShaderModuleCount => this.modules.Count;

		/// <summary>Compiles a module the first time a key is seen, and returns the same one after.</summary>
		/// <param name="sourceKey">One of <see cref="GlShaderKeys.AllModuleKeys"/>.</param>
		public IShaderModule GetShaderModule(string sourceKey)
		{
			if (!this.modules.TryGetValue(sourceKey, out var module))
			{
				module = this.device.CreateShaderModule(sourceKey);
				this.modules[sourceKey] = module;
			}

			return module;
		}

		/// <summary>Returns the pipeline for a descriptor, creating it on first use.</summary>
		/// <param name="descriptor">The full pipeline state, which is also the key.</param>
		public IRenderPipeline GetPipeline(in RenderPipelineDescriptor descriptor)
		{
			if (!this.pipelines.TryGetValue(descriptor, out var pipeline))
			{
				pipeline = this.device.CreateRenderPipeline(descriptor);
				this.pipelines[descriptor] = pipeline;
			}

			return pipeline;
		}

		/// <summary>Returns the bind group for a descriptor, creating it on first use.</summary>
		/// <param name="descriptor">The bound resources, which are also the key.</param>
		public IBindGroup GetBindGroup(in BindGroupDescriptor descriptor)
		{
			if (!this.bindGroups.TryGetValue(descriptor, out var bindGroup))
			{
				bindGroup = this.device.CreateBindGroup(descriptor);
				this.bindGroups[descriptor] = bindGroup;
			}

			return bindGroup;
		}

		/// <summary>
		/// Builds the pipeline descriptor a draw needs from the current GL state. Everything GL treats
		/// as dynamic - blend equation, color write mask, cull, depth test, topology - is baked in here,
		/// so a state change simply lands on a different cache entry.
		/// </summary>
		/// <param name="state">The shadowed GL state.</param>
		/// <param name="target">Format of the color attachment being drawn into.</param>
		/// <param name="depthFormat">Format of the depth attachment, or Undefined when the pass has none.</param>
		/// <param name="topology">The primitive topology of the draw.</param>
		/// <param name="textured">Whether the draw samples a texture.</param>
		/// <param name="lit">Whether the draw uses the lit shader variant.</param>
		public RenderPipelineDescriptor BuildPipelineDescriptor(
			GlStateShadow state,
			TextureFormat target,
			TextureFormat depthFormat,
			PrimitiveTopology topology,
			bool textured,
			bool lit)
		{
			if (state == null)
			{
				throw new ArgumentNullException(nameof(state));
			}

			var module = this.GetShaderModule(GlShaderKeys.ModuleKey(textured, lit));

			// Both halves of the equation take the same factors and always add, which is exactly what
			// the classic path's GetOrCreateBlendState builds - GL's fixed function pipeline has no
			// separate alpha equation to express.
			bool blendEnabled = state.BlendEnabled;
			var blend = blendEnabled
				? new BlendComponent(
					BlendOperation.Add,
					GlStateShadow.MapBlendFactor(state.BlendSourceFactor),
					GlStateShadow.MapBlendFactor(state.BlendDestinationFactor))
				: default;

			var colorTarget = new ColorTargetState(target, blendEnabled, blend, blend, state.ColorWriteMask);

			// glPolygonOffset(factor, units) maps onto webgpu's depthBiasSlopeScale/depthBias, which is
			// the same conversion the D3D11 backend does onto RasterizerDescription: the factor is
			// already a slope multiplier in both APIs, and units - GL's "smallest resolvable depth
			// difference" step - is exactly what D3D11 and webgpu count depthBias in, so it only has to
			// be truncated to the integer the field holds. depthBiasClamp has no GL source and stays 0.
			// The bias only applies while GL_POLYGON_OFFSET_FILL is enabled, so it zeroes out otherwise
			// rather than fragmenting the pipeline cache with dormant offsets.
			int depthBias = state.PolygonOffsetEnabled ? (int)state.PolygonOffsetUnits : 0;
			float depthBiasSlopeScale = state.PolygonOffsetEnabled ? state.PolygonOffsetFactor : 0;

			// A disabled depth test becomes an Always comparison rather than a missing depth attachment,
			// because the pass still has one and the pipeline must agree with the pass.
			// Depth writes need both the test and the mask: D3D11 ignores DepthWriteMask entirely when
			// DepthEnable is false, so the classic path's GetOrCreateDepthStencilState(enabled, ...) writes
			// no depth with the test off however the mask is left. WebGPU has no such coupling - a pipeline
			// with depthWriteEnabled and Always would stamp depth on every fragment - so the AND has to be
			// made explicit here or overlays drawn with the test off would clobber the depth buffer.
			var depth = depthFormat == TextureFormat.Undefined
				? DepthStencilState.None
				: new DepthStencilState(
					depthFormat,
					state.DepthTestEnabled && state.DepthMask,
					state.DepthTestEnabled ? state.DepthCompare : CompareFunction.Always,
					depthBias,
					depthBiasSlopeScale);

			return new RenderPipelineDescriptor(
				module,
				GlShaderKeys.VertexEntryPoint,
				module,
				GlShaderKeys.FragmentEntryPoint(state.FlatShading),
				new[] { GlShaderKeys.VertexLayout(textured, lit) },
				new[] { colorTarget },
				GlShaderKeys.BindGroupLayout(textured),
				depth,
				topology,
				state.CullingEnabled ? state.CullFaceMode : CullMode.None,
				state.FrontFaceCcw ? FrontFace.Ccw : FrontFace.Cw,
				1,
				GlShaderKeys.ModuleKey(textured, lit));
		}

		/// <summary>
		/// Releases every cached object. Note that pipelines and modules outlive individual frames by
		/// design - this is only called when the context itself goes away.
		/// </summary>
		public void Dispose()
		{
			foreach (var bindGroup in this.bindGroups.Values)
			{
				bindGroup.Dispose();
			}

			foreach (var pipeline in this.pipelines.Values)
			{
				pipeline.Dispose();
			}

			foreach (var module in this.modules.Values)
			{
				module.Dispose();
			}

			this.bindGroups.Clear();
			this.pipelines.Clear();
			this.modules.Clear();
		}
	}
}
