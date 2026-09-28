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
using System.Runtime.CompilerServices;
using MatterHackers.Agg.Image;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// <see cref="Graphics2DGpu"/>'s <see cref="Agg.ICompOpGraphics"/>: every SVG compositing operator, as a layer
	/// composite. The draws go source-over into an offscreen premultiplied layer the size of the target, then
	/// <c>CompOpComposite.wgsl</c> composites the layer onto a copy of the destination with
	/// <see cref="BlenderCompOpBGRA"/>'s formulas, writing each destination pixel once.
	/// </summary>
	/// <remarks>
	/// <para>
	/// One composite per call, not one per draw: a shape drawn as overlapping pieces (compositing2's rings) meets
	/// the destination once, as a software span would, instead of its anti-aliased seams going through the
	/// operator twice.
	/// </para>
	/// <para>
	/// <b>Cover.</b> The layer holds the cover-scaled source, which is what BlenderCompOpBGRA feeds most operators.
	/// Clear, src, src-in, dst-in, src-out and dst-atop instead lerp the destination to the full-cover result by the
	/// cover, which cannot be told apart from the source's own alpha in the layer - so for those the draws run a
	/// second time into a coverage layer, in white (<see cref="Graphics2DGpu.CoverageOnly"/>). Only vector and pattern
	/// fills take the white; an image drawn through those operators has its alpha taken as its cover.
	/// </para>
	/// <para>
	/// Reading the destination needs its texture to allow copies (<see cref="TextureUsage.CopySrc"/>): retained
	/// layers and offscreen targets do, and a window's swapchain does where the platform grants it. Source-over
	/// and dst need no read, so they run directly (dst draws nothing, as the software blender leaves the pixel).
	/// </para>
	/// </remarks>
	internal static class GpuCompOp
	{
		/// <summary>The WGSL module of the composite (the backend's <c>CompOpComposite.wgsl</c>).</summary>
		internal const string ShaderModuleKey = "CompOpComposite";

		private const int UniformSize = 32;

		private static readonly ConditionalWeakTable<GlCompatContext, Layers> LayersByContext = new ConditionalWeakTable<GlCompatContext, Layers>();

		public static bool Supports(GL gl, CompOp op)
		{
			if (!(gl?.GpuContext is GlCompatContext context))
			{
				return false;
			}

			if (op == CompOp.SrcOver || op == CompOp.Dst)
			{
				return true;
			}

			var target = context.Passes.ColorTarget;
			return target != null && (target.Descriptor.Usage & TextureUsage.CopySrc) != 0;
		}

		public static void Draw(Graphics2DGpu graphics, CompOp op, Action draw)
		{
			if (draw == null)
			{
				throw new ArgumentNullException(nameof(draw));
			}

			var gl = graphics.gl;
			if (!Supports(gl, op))
			{
				throw new NotSupportedException($"The GPU cannot draw with the {op} compositing operator here; check SupportsCompOp first.");
			}

			if (op == CompOp.SrcOver)
			{
				draw();
				return;
			}

			if (op == CompOp.Dst)
			{
				return;
			}

			var context = (GlCompatContext)gl.GpuContext;
			var layers = LayersByContext.GetValue(context, c => c.Own(new Layers()));
			if (layers.InUse)
			{
				throw new NotSupportedException("DrawWithCompOp cannot be nested inside another DrawWithCompOp.");
			}

			bool needsCoverage = NeedsCoverage(op);
			var target = context.Passes.ColorTarget;
			var descriptor = target.Descriptor;
			int scale = context.CoordinateScale;
			var state = context.State;
			GlViewportRect? viewport = state.ViewportSet ? state.Viewport : (GlViewportRect?)null;

			layers.InUse = true;
			var redirect = GpuTargetRedirect.SaveCurrent(gl, context);
			bool drawn = false;
			try
			{
				layers.Ensure(context, descriptor, needsCoverage);
				RedirectInto(gl, redirect, layers.Color, scale, descriptor, viewport);
				draw();

				if (needsCoverage)
				{
					// Spend the colour layer's clear there before the coverage layer takes the target.
					context.Passes.EnsurePassOpen();
					RedirectInto(gl, redirect, layers.Coverage, scale, descriptor, viewport);
					graphics.CoverageOnly = true;
					try
					{
						draw();
					}
					finally
					{
						graphics.CoverageOnly = false;
					}
				}

				drawn = true;
			}
			finally
			{
				redirect.Restore();
				layers.InUse = false;
			}

			if (drawn)
			{
				Composite(context, target, layers, op, needsCoverage);
			}
		}

		/// <summary>The operators BlenderCompOpBGRA lerps by the cover rather than running on the cover-scaled source.</summary>
		private static bool NeedsCoverage(CompOp op)
			=> op == CompOp.Clear || op == CompOp.Src || op == CompOp.SrcIn || op == CompOp.DstIn || op == CompOp.SrcOut || op == CompOp.DstAtop;

		private static void RedirectInto(GL gl, GpuTargetRedirect redirect, IGpuTexture layer, int scale, TextureDescriptor descriptor, GlViewportRect? viewport)
		{
			redirect.RedirectTo(layer, null, scale, (int)descriptor.Width / scale, (int)descriptor.Height / scale);

			// The layer lines up with the target texel for texel, so the draws keep the target's viewport.
			if (viewport.HasValue)
			{
				gl.Viewport(viewport.Value.X, viewport.Value.Y, viewport.Value.Width, viewport.Value.Height);
			}
		}

		private static void Composite(GlCompatContext context, IGpuTexture target, Layers layers, CompOp op, bool needsCoverage)
		{
			var descriptor = target.Descriptor;
			(int X, int Y, int Width, int Height) scissor = (0, 0, (int)descriptor.Width, (int)descriptor.Height);
			if (context.State.ScissorEnabled)
			{
				// In the target's device pixels, top-down and clamped to it - the layout the copy wants too.
				scissor = context.ToDeviceRect(context.State.Scissor);
				if (scissor.Width <= 0 || scissor.Height <= 0)
				{
					return;
				}
			}

			// Spend any clear queued on the target and end the compat pass: the copy and this pass cannot run inside it.
			context.Passes.EnsurePassOpen();
			context.FlushPass();

			var device = context.Device;
			device.CopyTextureToTexture(target, layers.Destination, scissor.X, scissor.Y, scissor.Width, scissor.Height);

			GlUniformBlock.WriteVector4(layers.UniformScratch.AsSpan(), 0, (int)op, needsCoverage ? 1 : 0, 0, 1);
			GlUniformBlock.WriteVector4(layers.UniformScratch.AsSpan(), 16, 0, 0, 0, 0);
			RunComposite(context, target, layers, layers.Color, needsCoverage ? layers.Coverage : layers.Color, layers.Destination, scissor, false);
		}

		/// <summary>
		/// Draws a linear-light retained layer (rgba16float, linear premultiplied) source-over onto the current target
		/// with its texel (0, 0) on the target's device texel (<paramref name="deviceX"/>, <paramref name="deviceY"/>),
		/// top-down, texel for texel. Onto sRGB bytes it mixes in linear light and encodes the result, as C++ AGG's
		/// rgba32 window is shown through srgba8; where the target cannot be read back it encodes the layer alone and
		/// lets the pipeline blend it, mixing in sRGB.
		/// </summary>
		public static void CompositeLinearLayer(GL gl, IGpuTexture layer, int deviceX, int deviceY, double opacity)
			=> CompositeLinearLayer(gl, layer, deviceX, deviceY, opacity, true);

		/// <param name="readDestination">False to take the path a target without CopySrc takes (mode 2) even where
		/// the target could be read - how a test reaches that fallback on a readable capture.</param>
		internal static void CompositeLinearLayer(GL gl, IGpuTexture layer, int deviceX, int deviceY, double opacity, bool readDestination)
		{
			var context = (GlCompatContext)gl.GpuContext;
			var target = context.Passes.ColorTarget;
			var descriptor = target.Descriptor;
			var layerSize = layer.Descriptor;
			int left = Math.Max(0, deviceX);
			int top = Math.Max(0, deviceY);
			int right = Math.Min((int)descriptor.Width, deviceX + (int)layerSize.Width);
			int bottom = Math.Min((int)descriptor.Height, deviceY + (int)layerSize.Height);
			if (context.State.ScissorEnabled)
			{
				var clip = context.ToDeviceRect(context.State.Scissor);
				left = Math.Max(left, clip.X);
				top = Math.Max(top, clip.Y);
				right = Math.Min(right, clip.X + clip.Width);
				bottom = Math.Min(bottom, clip.Y + clip.Height);
			}

			if (right <= left || bottom <= top)
			{
				return;
			}

			var layers = LayersByContext.GetValue(context, c => c.Own(new Layers()));
			bool canRead = readDestination && (descriptor.Usage & TextureUsage.CopySrc) != 0;
			int mode = !canRead ? 2 : context.Passes.LinearLight ? 3 : 1;
			var scissor = (left, top, right - left, bottom - top);

			context.Passes.EnsurePassOpen();
			context.FlushPass();
			if (canRead)
			{
				layers.Destination = Layers.Fit(context, layers.Destination, descriptor, TextureUsage.CopyDst | TextureUsage.TextureBinding, "CompOpDestination");
				context.Device.CopyTextureToTexture(target, layers.Destination, left, top, right - left, bottom - top);
			}

			GlUniformBlock.WriteVector4(layers.UniformScratch.AsSpan(), 0, (int)CompOp.SrcOver, 0, mode, (float)Math.Clamp(opacity, 0, 1));
			GlUniformBlock.WriteVector4(layers.UniformScratch.AsSpan(), 16, deviceX, deviceY, 0, 0);
			// Without a destination read the layer fills the destination slot, which mode 2 never reads.
			RunComposite(context, target, layers, layer, layer, canRead ? layers.Destination : layer, scissor, !canRead);
		}

		private static void RunComposite(
			GlCompatContext context,
			IGpuTexture target,
			Layers layers,
			IGpuTexture layer,
			IGpuTexture coverage,
			IGpuTexture destination,
			(int X, int Y, int Width, int Height) scissor,
			bool blendOver)
		{
			var device = context.Device;
			var descriptor = target.Descriptor;
			layers.Uniform ??= device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, UniformSize);
			device.WriteBuffer(layers.Uniform, 0, layers.UniformScratch);

			var sourceOver = new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha);
			var cache = context.Pipelines;
			var module = cache.GetShaderModule(ShaderModuleKey);
			var pipeline = cache.GetPipeline(new RenderPipelineDescriptor(
				module,
				"vertexMain",
				module,
				"fragmentMain",
				Array.Empty<VertexBufferLayout>(),
				new[]
				{
					// No blending - the shader's result replaces the pixel - except for a layer encoded alone.
					blendOver
						? new ColorTargetState(descriptor.Format, true, sourceOver, sourceOver)
						: new ColorTargetState(descriptor.Format, false, default, default),
				},
				new[]
				{
					new BindGroupLayoutEntry(0, 0, ShaderStage.Fragment, BindingType.Texture),
					new BindGroupLayoutEntry(0, 1, ShaderStage.Fragment, BindingType.Texture),
					new BindGroupLayoutEntry(0, 2, ShaderStage.Fragment, BindingType.Texture),
					new BindGroupLayoutEntry(0, 3, ShaderStage.Fragment, BindingType.UniformBuffer),
				},
				DepthStencilState.None,
				PrimitiveTopology.TriangleList,
				CullMode.None,
				FrontFace.Ccw,
				1,
				"CompOpComposite"));

			var bindGroup = cache.GetBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[]
				{
					BindGroupEntry.ForTexture(0, layer),
					// Without a coverage layer the shader never reads binding 1; the colour layer fills the slot.
					BindGroupEntry.ForTexture(1, coverage),
					BindGroupEntry.ForTexture(2, destination),
					BindGroupEntry.ForBuffer(3, layers.Uniform),
				},
				"CompOpComposite"));

			using (var encoder = device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(target, LoadOp.Load) },
				DepthAttachment.None,
				"CompOpComposite")))
			{
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.SetScissor(scissor.X, scissor.Y, scissor.Width, scissor.Height);
				encoder.Draw(3);
			}

			// The one uniform buffer and the layers are rewritten by the next composite; submitting now keeps
			// those writes from overtaking this draw in the queue.
			context.Submit();
		}

		/// <summary>One context's layer textures and uniform, reused from call to call.</summary>
		private sealed class Layers : IDisposable
		{
			public readonly byte[] UniformScratch = new byte[UniformSize];

			public bool InUse;

			public IGpuTexture Color;

			public IGpuTexture Coverage;

			public IGpuTexture Destination;

			public IGpuBuffer Uniform;

			public void Ensure(GlCompatContext context, TextureDescriptor target, bool needsCoverage)
			{
				this.Color = Fit(context, this.Color, target, TextureUsage.RenderAttachment | TextureUsage.TextureBinding, "CompOpLayer");
				this.Destination = Fit(context, this.Destination, target, TextureUsage.CopyDst | TextureUsage.TextureBinding, "CompOpDestination");
				if (needsCoverage)
				{
					this.Coverage = Fit(context, this.Coverage, target, TextureUsage.RenderAttachment | TextureUsage.TextureBinding, "CompOpCoverage");
				}
			}

			/// <summary>Releases the layers and uniform with their context (<see cref="GlCompatContext.Own"/>).</summary>
			public void Dispose()
			{
				this.Color?.Dispose();
				this.Coverage?.Dispose();
				this.Destination?.Dispose();
				this.Uniform?.Dispose();
			}

			public static IGpuTexture Fit(GlCompatContext context, IGpuTexture texture, TextureDescriptor target, TextureUsage usage, string label)
			{
				var current = texture?.Descriptor;
				if (current != null
					&& current.Value.Width == target.Width
					&& current.Value.Height == target.Height
					&& current.Value.Format == target.Format)
				{
					return texture;
				}

				if (texture != null)
				{
					// Bind groups are cached on the texture object; without this every resize strands one.
					context.Pipelines.InvalidateBindGroupsUsing(texture);
					texture.Dispose();
				}

				return context.Device.CreateTexture(new TextureDescriptor(target.Width, target.Height, target.Format, usage, 1, 1, label));
			}
		}
	}
}
