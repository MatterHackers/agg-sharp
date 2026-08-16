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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl.Scene
{
	/// <summary>
	/// <see cref="INativeSceneRenderer"/> on the retained seam: the 3D scene compositor the WebGPU
	/// backend renders through. Ported from the scene half of <c>VorticeD3DGl</c> (which stays untouched
	/// on disk as the parity oracle) and from <c>NodeDesignerScene.hlsl</c>, whose WGSL twin lives beside
	/// the backend in <c>WebGpuRender/Shaders</c>.
	/// <para>
	/// <b>Why this file is in RenderGl and not in WebGpuRender.</b> The interface it implements speaks
	/// <see cref="Mesh"/>, <see cref="Color"/>, <see cref="WorldView"/> and <see cref="Matrix4X4"/>, none
	/// of which the backend project may reference - it deliberately depends on RenderCore and the wgpu
	/// binding only, so backend code cannot reach back into the layers above it. This class is therefore
	/// placed exactly where <see cref="GlCompatContext"/> is, for exactly the same reason, and talks only
	/// to <see cref="IRenderDevice"/>; the WGSL it drives travels with the backend that can compile it.
	/// </para>
	/// <para>
	/// <b>Scope.</b> The whole classic scene frame: opaque meshes with lighting, the wireframe/outline
	/// render types, the always-visible overlay pass, selection outlines, the 3x supersampled full-frame
	/// capture, the printer bed with its cast shadow and analytic grid, and dual depth peeled
	/// transparency. The one thing deliberately left out is the classic path's <i>other</i> transparency
	/// mode, the sorted alpha-blend approximation it falls back to when depth peeling is switched off -
	/// see <see cref="DepthPeelingLayers"/>.
	/// </para>
	/// </summary>
	public sealed class WebGpuSceneRenderer : INativeSceneRenderer, IDisposable
	{
		/// <summary>Floats per vertex in the scene interleaved format: position, normal, uv, edge hints, color.</summary>
		private const int SceneVertexFloatStride = SceneEdgeShaderDataPlugin.TotalVertexFloatStride;

		private const int SceneVertexStride = SceneVertexFloatStride * sizeof(float);

		private const int TransformUniformSize = 128;

		private const int LightUniformSize = 112;

		/// <summary>
		/// 12 float4s: the five the classic SceneEffectBuffer starts with plus the seven of the analytic
		/// bed grid block. Written in full on every draw (zeroed where the bed block does not apply), as
		/// the classic path writes its whole constant buffer.
		/// </summary>
		private const int EffectUniformSize = 192;

		private const int BedShadowUniformSize = 32;

		/// <summary>Both bed intermediates are capped here, as the classic path caps them: the bed texture
		/// can be larger than anything the shadow needs.</summary>
		private const int BedTextureSizeLimit = 2048;

		/// <summary>How opaque an object's shadow on the bed gets. The classic BedShadowStrength.</summary>
		private const float BedShadowStrength = .70f;

		/// <summary>How far above the bed the orthographic shadow camera sits, in mm.</summary>
		private const double BedShadowViewDistance = 1000;

		private const int OutlineUniformSize = 32;

		private const int DownsampleUniformSize = 16;

		/// <summary>
		/// Linear supersample factor for <see cref="BeginFullFrameCapture"/>: the capture target is this
		/// many times the caller's target in each dimension, so every output pixel averages a 3x3 block.
		/// Matches the classic path's <c>VorticeD3DGl.SupersampleScale</c>; the goldens are captured at it.
		/// </summary>
		public const int SupersampleScale = 3;

		/// <summary>The intermediate targets' format. The classic path's are R8G8B8A8_UNorm.</summary>
		private const TextureFormat SceneColorFormat = TextureFormat.Rgba8Unorm;

		private const TextureFormat SceneDepthFormat = TextureFormat.Depth32Float;

		/// <summary>
		/// The <see cref="meshBufferSlots"/> key the selection mask's position buffers live under. Negative
		/// so it can never collide with a <see cref="RenderTypes"/> value.
		/// </summary>
		private const int SelectionBufferSlot = -1;

		/// <summary>
		/// The dual-peel accumulation targets' format, matching the classic path's
		/// R16G16B16A16_Float exactly - the front accumulator multiplies transmittances layer after layer
		/// and would band badly in 8 bits, and any other float width would round differently from the
		/// oracle and show up in the goldens.
		/// </summary>
		private const TextureFormat TransparencyAccumFormat = TextureFormat.Rgba16Float;

		/// <summary>Front accumulation: color += dstAlpha * src, alpha *= (1 - srcAlpha). The classic
		/// path's dualDepthPeelBlendState render target 1.</summary>
		private static readonly ColorTargetState FrontAccumTargetState = new ColorTargetState(
			TransparencyAccumFormat,
			true,
			new BlendComponent(BlendOperation.Add, BlendFactor.DstAlpha, BlendFactor.One),
			new BlendComponent(BlendOperation.Add, BlendFactor.Zero, BlendFactor.OneMinusSrcAlpha));

		/// <summary>Back accumulation: plain source-over, front to back. Render target 2 of the same state.</summary>
		private static readonly ColorTargetState BackAccumTargetState = new ColorTargetState(
			TransparencyAccumFormat,
			true,
			BlendComponent.AlphaBlend,
			new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha));

		private readonly GlCompatContext compat;
		private readonly IRenderDevice device;
		private readonly GlPipelineCache cache;

		private readonly List<MeshRenderCommand> queuedSceneCommands = new List<MeshRenderCommand>();
		private readonly List<MeshRenderCommand> queuedOverlayCommands = new List<MeshRenderCommand>();
		private readonly List<SelectionOutlineCommand> queuedSelectionOutlines = new List<SelectionOutlineCommand>();
		private readonly NativeSceneRenderPlanner renderPlanner = new NativeSceneRenderPlanner();

		// One uniform buffer per draw, indexed by a counter that resets each frame and reused frame after
		// frame. Queue writes are ordered against submits, not against the draws recorded into a pass, so
		// a buffer may only be rewritten once the submit that consumed its previous contents has happened
		// - which is exactly the frame boundary this renderer submits on.
		private readonly List<IGpuBuffer> transformUniforms = new List<IGpuBuffer>();
		private readonly List<IGpuBuffer> effectUniforms = new List<IGpuBuffer>();

		// Vertex buffers built from a mesh's render-data plugins, keyed the way those plugins are keyed:
		// per mesh, then per slot (one per RenderTypes for the scene data, one for the selection
		// positions). A plugin is replaced wholesale when mesh.ChangedCount moves, and the buffers it
		// cached go unreachable with it - so a flat retention list would keep one dead buffer per submesh
		// per mesh edit. Each slot remembers which plugin instance minted its buffers, and the next
		// generation retires the previous one's.
		// The key table is weak, because buffers must not be what keeps a mesh alive; the strong list of
		// slots is what Dispose walks.
		private readonly ConditionalWeakTable<Mesh, Dictionary<int, MeshBufferSlot>> meshBufferSlots
			= new ConditionalWeakTable<Mesh, Dictionary<int, MeshBufferSlot>>();

		private readonly List<MeshBufferSlot> retainedMeshBuffers = new List<MeshBufferSlot>();

		// Buffers a retired plugin generation left behind. Not disposed where they are retired: a mesh
		// edited between two passes of one frame retires buffers that this frame's already-recorded draws
		// still reference, so they are released only after the submit that consumes those draws.
		private readonly List<IGpuBuffer> retiredMeshBuffers = new List<IGpuBuffer>();

		private readonly byte[] transformScratch = new byte[TransformUniformSize];
		private readonly byte[] effectScratch = new byte[EffectUniformSize];
		private readonly byte[] lightScratch = new byte[LightUniformSize];
		private readonly byte[] outlineScratch = new byte[OutlineUniformSize];
		private readonly byte[] downsampleScratch = new byte[DownsampleUniformSize];
		private readonly byte[] bedShadowScratch = new byte[BedShadowUniformSize];

		private SceneRenderContext activeSceneRenderContext;
		private int drawSlot;

		/// <summary>Device pixels per logical pixel: 1 normally, <see cref="SupersampleScale"/> while a
		/// full-frame capture is in progress. Applied to the scene's target sizes and to every width the
		/// shaders measure in pixels, exactly as the classic path applies its supersampleScale.</summary>
		private int supersampleScale = 1;

		private IGpuTexture capturedColorTarget;
		private IGpuTexture capturedDepthTarget;
		private IGpuTexture sampleFrameColor;
		private IGpuTexture sampleFrameDepth;

		private IGpuBuffer lightUniform;
		private IGpuBuffer outlineUniform;
		private IGpuBuffer downsampleUniform;
		private ISampler linearSampler;
		private ISampler pointSampler;
		private IGpuTexture whiteTexture;

		private SceneTarget sceneColorTarget;
		private SceneTarget sceneDepthTarget;
		private SceneTarget selectionTarget;
		private IGpuTexture resolvedSceneTarget;
		private IGpuTexture transparentOverlayTarget;
		private IGpuTexture frontAccumTarget;
		private IGpuTexture backAccumTarget;
		private PeelDepthRange peelRangeA;
		private PeelDepthRange peelRangeB;
		private int targetWidth;
		private int targetHeight;

		private int depthPeelingLayers = 6;

		private BedRenderCommand queuedBedCommand;
		private readonly List<IGpuBuffer> bedShadowUniforms = new List<IGpuBuffer>();
		private IGpuTexture bedBaseTexture;
		private MatterHackers.Agg.Image.ImageBuffer bedBaseSource;
		private int bedBaseSourceChangedCount = -1;
		private IGpuTexture bedShadowMaskTarget;
		private IGpuTexture bedShadowBlurTargetA;
		private IGpuTexture bedShadowBlurTargetB;
		private IGpuTexture bedCompositeTarget;
		private int lastBedShadowSignature;

		/// <summary>Creates a scene renderer over a compat context.</summary>
		/// <param name="compat">
		/// The context whose device the scene records on and whose render target the finished frame is
		/// composited into. The compat layer owns pass lifetime, so the scene flushes its pass before
		/// opening its own and leaves it closed afterwards - the next widget draw re-opens it with
		/// <see cref="LoadOp.Load"/> and finds the scene already there.
		/// </param>
		public WebGpuSceneRenderer(GlCompatContext compat)
		{
			this.compat = compat ?? throw new ArgumentNullException(nameof(compat));
			this.device = compat.Device;
			this.cache = compat.Pipelines;
		}

		/// <inheritdoc/>
		public bool IsSceneRenderingActive => this.activeSceneRenderContext != null;

		/// <summary>
		/// How many transparent layers the peel resolves, as the user setting spells it. Two peeled layers
		/// per iteration (one from the front, one from the back), so this is twice the pass count; three
		/// or fewer means "do not peel at all" and is normalized to zero, exactly as the classic path's
		/// property of the same name does.
		/// </summary>
		public int DepthPeelingLayers
		{
			get => this.depthPeelingLayers;
			set => this.depthPeelingLayers = SceneTransparencyModeUtilities.NormalizeDepthPeelingLayers(value);
		}

		/// <summary>
		/// The facade the mesh render-data plugins are keyed on. Set by the host right after it creates
		/// the <see cref="GL"/> over the compat context, exactly as the classic path sets
		/// <c>VorticeD3DGl.OwnerGl</c>: the caches must be per context, or one device would bind another
		/// device's buffers.
		/// </summary>
		public GL OwnerGl { get; set; }

		/// <inheritdoc/>
		public void BeginSceneRendering(SceneRenderContext context)
		{
			this.activeSceneRenderContext = context;
			this.ClearQueuedSceneEffects();
		}

		/// <summary>
		/// Whether this renderer draws the command itself, or the caller has to fall back to the legacy
		/// immediate-mode GL mesh path.
		/// </summary>
		/// <remarks>
		/// Every render type is answered here, which is the point: the fallback in
		/// <c>RenderHelper.Render</c> is dead code for scene geometry only while this never says no, and
		/// the compat layer deliberately does not implement the client-array draws that fallback needs.
		/// <see cref="RenderTypes.Overhang"/> is drawn natively by baking its per-face colours into the
		/// vertex colour channel (see <see cref="TryRender"/>); <see cref="RenderTypes.Hidden"/> draws
		/// nothing at all, which is a thing this renderer can do perfectly.
		/// </remarks>
		/// <param name="command">The queued mesh draw.</param>
		public bool CanRender(MeshRenderCommand command)
		{
			return this.activeSceneRenderContext != null
				&& command?.Mesh != null
				&& (command.RenderType == RenderTypes.Shaded
					|| command.RenderType == RenderTypes.Outlines
					|| command.RenderType == RenderTypes.NonManifold
					|| command.RenderType == RenderTypes.Wireframe
					|| command.RenderType == RenderTypes.Polygons
					|| command.RenderType == RenderTypes.Overhang
					|| command.RenderType == RenderTypes.Hidden);
		}

		/// <inheritdoc/>
		public bool TryRender(MeshRenderCommand command)
		{
			if (!this.CanRender(command))
			{
				return false;
			}

			if (command.RenderType == RenderTypes.Hidden)
			{
				// Accepted and drawn as nothing. Queuing it would only make every pass filter it out again
				// through RequiresSceneMeshPass, and refusing it would send the caller to the fallback to
				// render nothing there instead.
				return true;
			}

			if (command.RenderType == RenderTypes.Overhang)
			{
				// Re-colours the mesh's triangle plugin by face normal, which is where the overhang colours
				// come from - the shader has no notion of "overhang", it just reads the vertex colour
				// channel. This has to happen before SceneEdgeShaderDataPlugin interleaves that channel,
				// which it does lazily at draw time, so queuing time is the last safe moment. It is also
				// where RenderHelper's fallback calls it, so the two agree about ordering.
				OverhangRender.EnsureUpdated(this.OwnerGl, command.Mesh, command.Transform);
			}

			// A caller that turned the depth test off wants this drawn as an always-visible overlay (the
			// 3D control ghost pass), not as scene geometry.
			if (this.compat.State.DepthTestEnabled)
			{
				this.queuedSceneCommands.Add(command);
			}
			else
			{
				this.queuedOverlayCommands.Add(command);
			}

			return true;
		}

		/// <inheritdoc/>
		public bool TryRender(BedRenderCommand command)
		{
			// The same three conditions the classic path checks. A bed with no texture has nothing to
			// composite the shadow under, so the caller is better off drawing its mesh the ordinary way.
			if (this.activeSceneRenderContext == null
				|| command?.Mesh == null
				|| command.TopBaseTexture == null)
			{
				return false;
			}

			this.queuedBedCommand = command;
			return true;
		}

		/// <inheritdoc/>
		public void QueueSelectionOutline(Mesh mesh, Color color, Matrix4X4 transform)
		{
			if (!this.IsSceneRenderingActive || mesh == null)
			{
				return;
			}

			this.queuedSelectionOutlines.Add(new SelectionOutlineCommand
			{
				Mesh = mesh,
				Color = color,
				Transform = transform,
			});
		}

		/// <inheritdoc/>
		public void EndSceneRendering()
		{
			try
			{
				this.RenderQueuedSceneEffects();
			}
			finally
			{
				this.ClearQueuedSceneEffects();
				this.activeSceneRenderContext = null;
			}
		}

		/// <summary>
		/// Points every subsequent draw - this renderer's and the compat layer's GL immediate mode alike -
		/// at an off-screen target <see cref="SupersampleScale"/> times the caller's in each dimension.
		/// </summary>
		/// <remarks>
		/// The classic path does this by swapping its renderTargetView/depthStencilView fields, which have
		/// no equivalent here; the compat layer's render target is the same global, so it is what gets
		/// swapped. The two things that have to move with it are the coordinate scale (GL viewports and
		/// scissors are in logical pixels and would otherwise clip to a ninth of the frame) and the scene
		/// pipeline's own intermediate targets, which are sized from the viewport in
		/// <see cref="EnsureFrameResources"/>.
		/// </remarks>
		/// <param name="viewport">The logical viewport, accepted for interface parity; the capture target
		/// is sized from the current render target, exactly as the classic path sizes it from the
		/// backbuffer.</param>
		public void BeginFullFrameCapture(RectangleDouble viewport)
		{
			if (this.capturedColorTarget != null)
			{
				throw new InvalidOperationException("A full-frame capture is already in progress.");
			}

			var destination = this.compat.Passes.ColorTarget;
			if (destination == null)
			{
				throw new InvalidOperationException(
					"No render target is set on the compat context, so there is nothing to capture on behalf of.");
			}

			// A clear queued against the caller's target must land on the caller's target. WebGPU clears
			// through a pass load op, so a clear left pending here would be consumed by the first pass on
			// the capture target instead - opening and immediately ending a pass spends it now, which is
			// the order the classic path's immediate glClear gives for free.
			this.compat.Passes.EnsurePassOpen();
			this.compat.FlushPass();

			int width = (int)destination.Descriptor.Width * SupersampleScale;
			int height = (int)destination.Descriptor.Height * SupersampleScale;
			this.EnsureSampleFrameTargets(width, height, destination.Descriptor.Format);

			this.capturedColorTarget = destination;
			this.capturedDepthTarget = this.compat.Passes.DepthTarget;

			this.compat.SetRenderTarget(this.sampleFrameColor, this.sampleFrameDepth);
			this.compat.CoordinateScale = SupersampleScale;
			this.supersampleScale = SupersampleScale;

			// Cleared to transparent so only the region the 3D frame actually covers contributes when the
			// downsampled result is alpha-blended back over the caller's target.
			using (this.device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(this.sampleFrameColor, LoadOp.Clear, ClearColor.Transparent) },
				new DepthAttachment(this.sampleFrameDepth, LoadOp.Clear, DepthAttachment.FarClear),
				"SupersampleClear")))
			{
			}
		}

		/// <summary>Points drawing back at the target <see cref="BeginFullFrameCapture"/> took over.</summary>
		public void EndFullFrameCapture()
		{
			if (this.capturedColorTarget == null)
			{
				return;
			}

			this.compat.SetRenderTarget(this.capturedColorTarget, this.capturedDepthTarget);
			this.compat.CoordinateScale = 1;
			this.supersampleScale = 1;
			this.capturedColorTarget = null;
			this.capturedDepthTarget = null;
		}

		/// <summary>
		/// Box-downsamples the capture target onto the caller's target with the 9-tap filter, completing
		/// the frame. Call after <see cref="EndFullFrameCapture"/>.
		/// </summary>
		public void DownsampleAndBlitFullFrame()
		{
			if (this.sampleFrameColor == null)
			{
				return;
			}

			var destination = this.compat.Passes.ColorTarget;
			if (destination == null)
			{
				return;
			}

			// Same reason as in BeginFullFrameCapture: spend any queued clear before compositing over it.
			this.compat.Passes.EnsurePassOpen();
			this.compat.FlushPass();

			this.EnsureSharedResources();
			this.WriteDownsampleUniform();

			var layout = new[]
			{
				new BindGroupLayoutEntry(0, 0, ShaderStage.Fragment, BindingType.Sampler),
				new BindGroupLayoutEntry(0, 1, ShaderStage.Fragment, BindingType.Texture),
				new BindGroupLayoutEntry(0, 8, ShaderStage.Fragment, BindingType.UniformBuffer),
			};

			// Premultiplied source-over, not straight: what landed in the capture target was produced by
			// SrcAlpha blending, so its colour is already multiplied by its alpha. Blending it back with
			// SrcAlpha again would double-premultiply and darken everything translucent.
			var colorTarget = new ColorTargetState(
				destination.Descriptor.Format,
				true,
				new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha),
				new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha));

			var pipeline = this.GetFullscreenPipeline(
				SceneShaderKeys.Downsample3x3EntryPoint,
				colorTarget,
				layout,
				"SupersampleDownsample");

			var bindGroup = this.cache.GetBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[]
				{
					BindGroupEntry.ForSampler(0, this.pointSampler),
					BindGroupEntry.ForTexture(1, this.sampleFrameColor),
					BindGroupEntry.ForBuffer(8, this.downsampleUniform),
				},
				"SupersampleDownsample"));

			// No SetViewport: the whole capture target maps to the whole destination, which is a pass's
			// default viewport. The classic path says the same thing by setting the full backbuffer.
			using (var encoder = this.device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(destination, LoadOp.Load) },
				DepthAttachment.None,
				"SupersampleDownsample")))
			{
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.Draw(3);
			}

			this.compat.Submit();
		}

		/// <summary>Releases every device object this renderer owns, including the retained mesh buffers.</summary>
		public void Dispose()
		{
			foreach (var buffer in this.transformUniforms)
			{
				buffer.Dispose();
			}

			foreach (var buffer in this.effectUniforms)
			{
				buffer.Dispose();
			}

			foreach (var slot in this.retainedMeshBuffers)
			{
				slot.RetireInto(this.retiredMeshBuffers);
			}

			this.DisposeRetiredMeshBuffers();

			this.transformUniforms.Clear();
			this.effectUniforms.Clear();
			this.retainedMeshBuffers.Clear();

			this.lightUniform?.Dispose();
			this.outlineUniform?.Dispose();
			this.downsampleUniform?.Dispose();
			foreach (var buffer in this.bedShadowUniforms)
			{
				buffer.Dispose();
			}

			this.bedShadowUniforms.Clear();
			this.linearSampler?.Dispose();
			this.pointSampler?.Dispose();

			// The cache belongs to the compat context and can outlive this renderer, so the groups holding
			// these two go with them exactly as the frame targets' groups do.
			this.cache.InvalidateBindGroupsUsing(this.whiteTexture, this.bedBaseTexture);
			this.whiteTexture?.Dispose();
			this.bedBaseTexture?.Dispose();
			this.DisposeTargets();
			this.DisposeSampleFrameTargets();
			this.DisposeBedTargets();

			this.lightUniform = null;
			this.outlineUniform = null;
			this.downsampleUniform = null;
			this.linearSampler = null;
			this.pointSampler = null;
			this.whiteTexture = null;
			this.bedBaseTexture = null;
			this.bedBaseSource = null;
			this.bedBaseSourceChangedCount = -1;
		}

		// ---- Frame ------------------------------------------------------------------------------------

		/// <summary>
		/// Draws everything queued this frame, in the classic path's order: the bed shadow texture, opaque
		/// colour, the depth prepass the outline composite reads, the peeled transparency layers, the
		/// overlay, the resolve, the blit to the caller's target, then the selection outlines.
		/// </summary>
		private void RenderQueuedSceneEffects()
		{
			if (this.activeSceneRenderContext == null
				|| (this.queuedSceneCommands.Count == 0
					&& this.queuedOverlayCommands.Count == 0
					&& this.queuedSelectionOutlines.Count == 0
					&& this.queuedBedCommand == null))
			{
				return;
			}

			var destination = this.compat.Passes.ColorTarget;
			if (destination == null)
			{
				throw new InvalidOperationException(
					"No render target is set on the compat context, so the scene has nowhere to composite into.");
			}

			// Two things at once. The compat layer may be holding a pass open over the same target, and
			// passes do not nest - so it has to be ended. But it may also be holding a *queued* clear
			// (WebGPU clears through a pass's load op, so the compat layer defers one until its next pass
			// opens), and the scene composites with LoadOp.Load: a clear left queued would land on top of
			// the finished scene at the next widget draw, or never at all. Opening and immediately ending
			// the compat pass consumes the clear first, which is the order the classic path's immediate
			// glClear gives for free.
			this.compat.Passes.EnsurePassOpen();
			this.compat.FlushPass();

			// During a full-frame capture the scene pipeline renders at supersampleScale times the logical
			// viewport, so its output matches the resolution of the target it composites into.
			int width = Math.Max(1, (int)Math.Ceiling(this.activeSceneRenderContext.Viewport.Width)) * this.supersampleScale;
			int height = Math.Max(1, (int)Math.Ceiling(this.activeSceneRenderContext.Viewport.Height)) * this.supersampleScale;
			this.EnsureFrameResources(width, height);

			this.drawSlot = 0;
			this.WriteLightUniform();

			// Before the plan is built, because the shadow mask rasterizes the queued scene commands from
			// above the bed and has nothing to do with the frame's opaque/transparent split.
			this.RenderBedShadowTexture(this.queuedBedCommand);

			var renderPlan = this.renderPlanner.Build(this.queuedSceneCommands);

			this.RenderOpaqueCommands(renderPlan.OpaqueCommands);
			this.RenderSceneDepth(renderPlan);
			this.RenderTransparentLayers(renderPlan.TransparentCommands);
			this.RenderTransparentOverlays();
			this.CompositeSceneTargets();
			this.BlitResolvedSceneToTarget(destination);
			this.RenderSelectionOutlines(destination);

			// Everything the frame recorded reaches the queue here, which is what makes the per-draw
			// uniform buffers safe to rewrite on the next frame.
			this.compat.Submit();

			// And what makes the buffers this frame retired safe to release: no unsubmitted draw can still
			// be referencing them.
			this.DisposeRetiredMeshBuffers();
		}

		private void RenderOpaqueCommands(IReadOnlyList<MeshRenderCommand> commands)
		{
			using (var encoder = this.device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(this.sceneColorTarget.Color, LoadOp.Clear, ClearColor.Transparent) },
				new DepthAttachment(this.sceneColorTarget.Depth, LoadOp.Clear, DepthAttachment.FarClear),
				"SceneOpaque")))
			{
				foreach (var command in commands)
				{
					if (!SceneRenderModeUtilities.RequiresSceneMeshPass(command.RenderType))
					{
						continue;
					}

					this.DrawMeshCommand(
						encoder,
						command,
						new MeshDrawState
						{
							ColorFormat = SceneColorFormat,
							DepthFormat = SceneDepthFormat,
							EnableWireframe = SceneRenderModeUtilities.ShouldDrawWireframeOverlay(command.RenderType),
							WireframeOnly = SceneRenderModeUtilities.IsWireframeOnly(command.RenderType),
						});
				}
			}
		}

		/// <summary>
		/// The depth-only prepass. The classic path binds no colour target and disables colour writes;
		/// WebGPU says the same thing by opening a pass with a depth attachment alone and a pipeline with
		/// no colour targets.
		/// </summary>
		private void RenderSceneDepth(NativeSceneRenderPlan renderPlan)
		{
			using (var encoder = this.device.BeginRenderPass(new RenderPassDescriptor(
				Array.Empty<ColorAttachment>(),
				new DepthAttachment(this.sceneDepthTarget.Depth, LoadOp.Clear, DepthAttachment.FarClear),
				"SceneDepth")))
			{
				foreach (var command in renderPlan.OpaqueCommands)
				{
					this.DrawMeshCommand(encoder, command, new MeshDrawState { DepthOnly = true, DepthFormat = SceneDepthFormat });
				}

				foreach (var command in renderPlan.TransparentCommands)
				{
					this.DrawMeshCommand(encoder, command, new MeshDrawState { DepthOnly = true, DepthFormat = SceneDepthFormat });
				}

				if (this.IsBedDrawable)
				{
					this.DrawMeshCommand(
						encoder,
						this.queuedBedCommand.CreateSceneCommand(),
						new MeshDrawState
						{
							DepthOnly = true,
							DepthFormat = SceneDepthFormat,
							ForcedTexture = this.bedCompositeTarget,
						});
				}
			}
		}

		/// <summary>True when a bed is queued and its composited texture exists to draw it with.</summary>
		private bool IsBedDrawable => this.queuedBedCommand != null && this.bedCompositeTarget != null;

		/// <summary>
		/// Clears the transparency accumulation targets to the values that make the resolve an identity:
		/// front alpha 1 (nothing absorbed yet), back zero. A frame with no transparent geometry runs the
		/// same resolve the classic path always runs rather than a shortcut whose output would have to be
		/// argued about.
		/// </summary>
		private void ClearTransparencyTargets()
		{
			this.ClearColorTarget(this.frontAccumTarget, new ClearColor(0, 0, 0, 1), "ClearFrontAccum");
			this.ClearColorTarget(this.backAccumTarget, ClearColor.Transparent, "ClearBackAccum");
		}

		/// <summary>
		/// Dual depth peeling: seeds the depth range from every transparent fragment, then peels the
		/// nearest and farthest remaining layer per iteration into the two accumulation targets.
		/// </summary>
		/// <remarks>
		/// The classic path's loop, one for one (<c>VorticeD3DGl.RenderTransparentLayers</c>), except that
		/// each of its iterations is three passes here: the depth range it kept in a MAX-blended Rg32Float
		/// target is kept in two hardware depth buffers instead, and a depth attachment cannot be written
		/// by the same pass that writes colour. See the peel section of NodeDesignerScene.wgsl for why the
		/// two formulations compute the same numbers.
		/// </remarks>
		/// <param name="transparentCommands">The plan's transparent half, in queue order - the peel is
		/// order independent, which is the entire reason it exists.</param>
		private void RenderTransparentLayers(IReadOnlyList<MeshRenderCommand> transparentCommands)
		{
			this.ClearTransparencyTargets();

			if (transparentCommands.Count == 0 && !this.IsBedDrawable)
			{
				return;
			}

			// Below the early-out on purpose: a frame with nothing transparent in it renders identically
			// in either transparency mode (the cleared accumulators resolve to an identity), so failing an
			// opaque frame over a mode it never reaches would take the whole app down for nothing. Once
			// there is transparency to draw the mode matters, and then this is as loud as it was.
			if (SceneTransparencyModeUtilities.GetSceneTransparencyMode(this.DepthPeelingLayers)
				!= SceneTransparencyMode.DualDepthPeeling)
			{
				throw new NotSupportedException(
					"WebGpuSceneRenderer implements the dual depth peeling transparency mode only; the classic "
					+ "path's sorted alpha-blend approximation (DepthPeelingLayers <= 2) is not ported.");
			}

			this.InitializeDualDepthPeel(transparentCommands);

			var source = this.peelRangeA;
			var destination = this.peelRangeB;
			int iterationCount = DualDepthPeelingMath.GetIterationCount(this.DepthPeelingLayers);
			for (int iteration = 0; iteration < iterationCount; iteration++)
			{
				this.PeelDepthRangePass(transparentCommands, source, destination.Near, CompareFunction.Less, DepthAttachment.FarClear, "PeelNear");
				this.PeelDepthRangePass(transparentCommands, source, destination.Far, CompareFunction.Greater, 0, "PeelFar");
				this.PeelColorPass(transparentCommands, source);

				(source, destination) = (destination, source);
			}
		}

		/// <summary>
		/// The classic <c>InitializeDualDepthPeel</c>: the widest depth range, seeded from every
		/// transparent fragment that is not behind opaque geometry.
		/// </summary>
		/// <param name="transparentCommands">The transparent commands.</param>
		private void InitializeDualDepthPeel(IReadOnlyList<MeshRenderCommand> transparentCommands)
		{
			this.PeelGeometryPass(
				transparentCommands,
				peelSource: null,
				new DepthAttachment(this.peelRangeA.Near, LoadOp.Clear, DepthAttachment.FarClear),
				Array.Empty<ColorAttachment>(),
				new MeshDrawState { Peel = PeelStage.Init, DepthFormat = SceneDepthFormat, DepthCompare = CompareFunction.Less },
				"PeelInitNear");

			this.PeelGeometryPass(
				transparentCommands,
				peelSource: null,
				new DepthAttachment(this.peelRangeA.Far, LoadOp.Clear, 0),
				Array.Empty<ColorAttachment>(),
				new MeshDrawState { Peel = PeelStage.Init, DepthFormat = SceneDepthFormat, DepthCompare = CompareFunction.Greater },
				"PeelInitFar");
		}

		/// <summary>One half of an iteration's depth range: the min (or max) depth of the fragments still
		/// strictly inside the range peeled so far.</summary>
		/// <param name="transparentCommands">The transparent commands.</param>
		/// <param name="source">The previous iteration's range, which the shader reads.</param>
		/// <param name="target">The depth texture this pass narrows.</param>
		/// <param name="compare">Less for the near half, Greater for the far half.</param>
		/// <param name="clearValue">1 for the near half, 0 for the far half - the empty range.</param>
		/// <param name="label">Pass label.</param>
		private void PeelDepthRangePass(
			IReadOnlyList<MeshRenderCommand> transparentCommands,
			PeelDepthRange source,
			IGpuTexture target,
			CompareFunction compare,
			float clearValue,
			string label)
		{
			this.PeelGeometryPass(
				transparentCommands,
				source,
				new DepthAttachment(target, LoadOp.Clear, clearValue),
				Array.Empty<ColorAttachment>(),
				new MeshDrawState { Peel = PeelStage.Depth, DepthFormat = SceneDepthFormat, DepthCompare = compare },
				label);
		}

		/// <summary>Accumulates the two layers this iteration peels into the front and back targets.</summary>
		/// <param name="transparentCommands">The transparent commands.</param>
		/// <param name="source">The range peeled so far, whose two boundaries are this iteration's layers.</param>
		private void PeelColorPass(IReadOnlyList<MeshRenderCommand> transparentCommands, PeelDepthRange source)
		{
			this.PeelGeometryPass(
				transparentCommands,
				source,
				DepthAttachment.None,
				new[]
				{
					new ColorAttachment(this.frontAccumTarget, LoadOp.Load),
					new ColorAttachment(this.backAccumTarget, LoadOp.Load),
				},
				new MeshDrawState { Peel = PeelStage.Color, DepthFormat = TextureFormat.Undefined },
				"PeelColor");
		}

		/// <summary>Draws every transparent command once, in one pass, with the given peel state.</summary>
		/// <param name="transparentCommands">The transparent commands.</param>
		/// <param name="peelSource">The depth range the shaders read, or null for the init passes.</param>
		/// <param name="depth">The pass's depth attachment.</param>
		/// <param name="colorAttachments">The pass's colour attachments.</param>
		/// <param name="drawState">Peel stage, depth format and compare.</param>
		/// <param name="label">Pass label.</param>
		private void PeelGeometryPass(
			IReadOnlyList<MeshRenderCommand> transparentCommands,
			PeelDepthRange peelSource,
			DepthAttachment depth,
			ColorAttachment[] colorAttachments,
			MeshDrawState drawState,
			string label)
		{
			drawState.PeelSource = peelSource;

			using (var encoder = this.device.BeginRenderPass(new RenderPassDescriptor(colorAttachments, depth, label)))
			{
				foreach (var command in transparentCommands)
				{
					if (!SceneRenderModeUtilities.RequiresSceneMeshPass(command.RenderType)
						|| !SceneRenderModeUtilities.ShouldRenderTransparentFill(command.RenderType))
					{
						continue;
					}

					var commandState = drawState;
					commandState.EnableWireframe = SceneRenderModeUtilities.ShouldDrawWireframeOverlay(command.RenderType);
					commandState.WireframeOnly = SceneRenderModeUtilities.IsWireframeOnly(command.RenderType);

					this.DrawMeshCommand(encoder, command, commandState);
				}

				// The bed peels with the transparent objects and always last, so a shadowed grid line is
				// composited over whatever the objects above it contributed. Its grid is analytic, so the
				// draw carries the bed styling; the init pass does not, because DualDepthInitPS only ever
				// looks at the texture's alpha and the grid cannot change that.
				if (this.IsBedDrawable)
				{
					var bedState = drawState;
					bedState.ForcedTexture = this.bedCompositeTarget;
					bedState.Unlit = true;
					bedState.BedGrid = drawState.Peel == PeelStage.Init ? null : this.queuedBedCommand;

					this.DrawMeshCommand(encoder, this.queuedBedCommand.CreateSceneCommand(), bedState);
				}
			}
		}

		private void RenderTransparentOverlays()
		{
			using (var encoder = this.device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(this.transparentOverlayTarget, LoadOp.Clear, ClearColor.Transparent) },
				DepthAttachment.None,
				"SceneOverlay")))
			{
				foreach (var command in this.queuedOverlayCommands)
				{
					if (!SceneRenderModeUtilities.RequiresSceneMeshPass(command.RenderType))
					{
						continue;
					}

					this.DrawMeshCommand(
						encoder,
						command,
						new MeshDrawState
						{
							ColorFormat = SceneColorFormat,
							DepthFormat = TextureFormat.Undefined,
							Blend = BlendComponent.AlphaBlend,
							BlendEnabled = true,
						});
				}
			}
		}

		private void CompositeSceneTargets()
		{
			var layout = new[]
			{
				new BindGroupLayoutEntry(0, 0, ShaderStage.Fragment, BindingType.Sampler),
				new BindGroupLayoutEntry(0, 1, ShaderStage.Fragment, BindingType.Texture),
				new BindGroupLayoutEntry(0, 2, ShaderStage.Fragment, BindingType.Texture),
				new BindGroupLayoutEntry(0, 3, ShaderStage.Fragment, BindingType.Texture),
				new BindGroupLayoutEntry(0, 4, ShaderStage.Fragment, BindingType.Texture),
			};

			var pipeline = this.GetFullscreenPipeline(
				SceneShaderKeys.ResolveDualPeelEntryPoint,
				new ColorTargetState(SceneColorFormat),
				layout,
				"SceneResolve");

			var bindGroup = this.cache.GetBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[]
				{
					BindGroupEntry.ForSampler(0, this.pointSampler),
					BindGroupEntry.ForTexture(1, this.sceneColorTarget.Color),
					BindGroupEntry.ForTexture(2, this.frontAccumTarget),
					BindGroupEntry.ForTexture(3, this.backAccumTarget),
					BindGroupEntry.ForTexture(4, this.transparentOverlayTarget),
				},
				"SceneResolve"));

			using (var encoder = this.device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(this.resolvedSceneTarget, LoadOp.Clear, ClearColor.Transparent) },
				DepthAttachment.None,
				"SceneResolve")))
			{
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.Draw(3);
			}
		}

		/// <summary>
		/// Blits the resolved scene into the caller's target, over whatever was already there. The blend
		/// is the classic path's resolvedSceneBlitBlendState - straight source-over, because the resolve
		/// hands back straight (un-premultiplied) alpha.
		/// </summary>
		private void BlitResolvedSceneToTarget(IGpuTexture destination)
		{
			var layout = new[]
			{
				new BindGroupLayoutEntry(0, 0, ShaderStage.Fragment, BindingType.Sampler),
				new BindGroupLayoutEntry(0, 1, ShaderStage.Fragment, BindingType.Texture),
			};

			var colorTarget = new ColorTargetState(
				destination.Descriptor.Format,
				true,
				BlendComponent.AlphaBlend,
				new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha));

			var pipeline = this.GetFullscreenPipeline(
				SceneShaderKeys.CopyTextureEntryPoint,
				colorTarget,
				layout,
				"SceneBlit");

			var bindGroup = this.cache.GetBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[]
				{
					BindGroupEntry.ForSampler(0, this.pointSampler),
					BindGroupEntry.ForTexture(1, this.resolvedSceneTarget),
				},
				"SceneBlit"));

			using (var encoder = this.device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(destination, LoadOp.Load) },
				DepthAttachment.None,
				"SceneBlit")))
			{
				this.ApplySceneViewport(encoder, destination);
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.Draw(3);
			}
		}

		private void RenderSelectionOutlines(IGpuTexture destination)
		{
			if (this.queuedSelectionOutlines.Count == 0)
			{
				return;
			}

			using (var encoder = this.device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(this.selectionTarget.Color, LoadOp.Clear, ClearColor.Transparent) },
				new DepthAttachment(this.selectionTarget.Depth, LoadOp.Clear, DepthAttachment.FarClear),
				"SelectionMask")))
			{
				foreach (var outline in this.queuedSelectionOutlines)
				{
					this.DrawSelectionMask(encoder, outline);
				}
			}

			this.CompositeSelectionOutlines(destination);
		}

		private void CompositeSelectionOutlines(IGpuTexture destination)
		{
			var layout = new[]
			{
				new BindGroupLayoutEntry(0, 0, ShaderStage.Fragment, BindingType.Sampler),
				new BindGroupLayoutEntry(0, 1, ShaderStage.Fragment, BindingType.Texture),
				new BindGroupLayoutEntry(0, 5, ShaderStage.Fragment, BindingType.UniformBuffer),
				new BindGroupLayoutEntry(0, 6, ShaderStage.Fragment, BindingType.DepthTexture),
				new BindGroupLayoutEntry(0, 7, ShaderStage.Fragment, BindingType.DepthTexture),
			};

			// Both halves take the same factors, which is what the classic path's GetOrCreateBlendState
			// builds for every GL-shaped blend - unlike the resolved-scene blit above, whose alpha half is
			// One/InvSrcAlpha because it is a purpose-built blend state. Getting this wrong is invisible in
			// the colour channels and shows up only as a wrong destination alpha.
			var colorTarget = new ColorTargetState(
				destination.Descriptor.Format,
				true,
				BlendComponent.AlphaBlend,
				BlendComponent.AlphaBlend);

			var pipeline = this.GetFullscreenPipeline(
				SceneShaderKeys.OutlineCompositeEntryPoint,
				colorTarget,
				layout,
				"SelectionOutline");

			this.WriteOutlineUniform();

			var bindGroup = this.cache.GetBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[]
				{
					BindGroupEntry.ForSampler(0, this.pointSampler),
					BindGroupEntry.ForTexture(1, this.selectionTarget.Color),
					BindGroupEntry.ForBuffer(5, this.outlineUniform),
					BindGroupEntry.ForTexture(6, this.selectionTarget.Depth),
					BindGroupEntry.ForTexture(7, this.sceneDepthTarget.Depth),
				},
				"SelectionOutline"));

			using (var encoder = this.device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(destination, LoadOp.Load) },
				DepthAttachment.None,
				"SelectionOutline")))
			{
				this.ApplySceneViewport(encoder, destination);
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.Draw(3);
			}
		}

		// ---- Bed --------------------------------------------------------------------------------------

		/// <summary>
		/// Produces the texture the bed is drawn with: the scene's objects rasterized from above the bed,
		/// blurred, and composited under the bed's own image.
		/// </summary>
		/// <remarks>
		/// Cached on a signature of everything the mask depends on (the bed, and every shadow-casting
		/// mesh's identity, change count and transform), exactly as the classic path caches it - the
		/// shadow does not move when the camera does, so a camera-only frame does no work here.
		/// </remarks>
		/// <param name="bedCommand">The queued bed, or null.</param>
		private void RenderBedShadowTexture(BedRenderCommand bedCommand)
		{
			if (bedCommand?.TopBaseTexture == null)
			{
				return;
			}

			this.EnsureBedTargets(bedCommand.TopBaseTexture.Width, bedCommand.TopBaseTexture.Height);
			this.EnsureBedBaseTexture(bedCommand.TopBaseTexture);

			int signature = this.ComputeBedShadowSignature(bedCommand);
			if (signature == this.lastBedShadowSignature)
			{
				return;
			}

			this.lastBedShadowSignature = signature;

			this.RenderBedShadowMask(bedCommand);
			this.RenderBedBlurPass(0, this.bedShadowMaskTarget, this.bedShadowBlurTargetA, 1.0f / this.bedShadowMaskTarget.Descriptor.Width, 0);
			this.RenderBedBlurPass(1, this.bedShadowBlurTargetA, this.bedShadowBlurTargetB, 0, 1.0f / this.bedShadowMaskTarget.Descriptor.Height);
			this.RenderBedCompositePass(bedCommand);
		}

		/// <summary>Everything the cached shadow depends on, hashed the way the classic path hashes it.</summary>
		/// <param name="bedCommand">The queued bed.</param>
		private int ComputeBedShadowSignature(BedRenderCommand bedCommand)
		{
			var hash = default(HashCode);
			hash.Add(bedCommand.ObjectsBelowBed);
			hash.Add(bedCommand.BedBounds.Left);
			hash.Add(bedCommand.BedBounds.Right);
			hash.Add(bedCommand.BedBounds.Bottom);
			hash.Add(bedCommand.BedBounds.Top);
			hash.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(bedCommand.TopBaseTexture));

			foreach (var command in this.queuedSceneCommands)
			{
				if (!RenderHelper.ShouldRenderInBedShadow(command, bedCommand.BedBounds))
				{
					continue;
				}

				hash.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(command.Mesh));
				hash.Add(command.Mesh.ChangedCount);
				hash.Add(command.Transform);
			}

			return hash.ToHashCode();
		}

		/// <summary>Rasterizes the shadow casters as flat black silhouettes seen from straight above.</summary>
		/// <param name="bedCommand">The queued bed, whose bounds are the orthographic frustum.</param>
		private void RenderBedShadowMask(BedRenderCommand bedCommand)
		{
			var bedCenter = new Vector3(
				(bedCommand.BedBounds.Left + bedCommand.BedBounds.Right) * .5,
				(bedCommand.BedBounds.Bottom + bedCommand.BedBounds.Top) * .5,
				0);

			var shadowView = Matrix4X4.LookAt(
				bedCenter + new Vector3(0, 0, BedShadowViewDistance),
				bedCenter,
				Vector3.UnitY);

			var shadowProjection = Matrix4X4.CreateOrthographicOffCenter(
				bedCommand.BedBounds.Left,
				bedCommand.BedBounds.Right,
				bedCommand.BedBounds.Bottom,
				bedCommand.BedBounds.Top,
				1,
				BedShadowViewDistance * 2);

			using (var encoder = this.device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(this.bedShadowMaskTarget, LoadOp.Clear, ClearColor.Transparent) },
				DepthAttachment.None,
				"BedShadowMask")))
			{
				foreach (var command in this.queuedSceneCommands)
				{
					if (!RenderHelper.ShouldRenderInBedShadow(command, bedCommand.BedBounds))
					{
						continue;
					}

					// Glyph meshes can have mixed winding on their caps and sides, so the mask renders
					// without culling; CreateBedShadowCommand is what says so.
					var shadowCommand = RenderHelper.CreateBedShadowCommand(command);
					this.DrawFlatMask(
						encoder,
						shadowCommand.Mesh,
						shadowCommand.Transform * shadowView,
						shadowProjection,
						Color.Black,
						DepthStencilState.None,
						"BedShadowMask");
				}
			}
		}

		/// <summary>One axis of the separable blur over the shadow mask.</summary>
		/// <param name="source">The texture to blur.</param>
		/// <param name="destination">Where the blurred result lands.</param>
		/// <param name="directionX">Horizontal step in uv, or 0 for the vertical pass.</param>
		/// <param name="directionY">Vertical step in uv, or 0 for the horizontal pass.</param>
		private void RenderBedBlurPass(int passIndex, IGpuTexture source, IGpuTexture destination, float directionX, float directionY)
		{
			var settings = this.WriteBedShadowUniform(passIndex, directionX, directionY, Color.Transparent);

			var pipeline = this.GetFullscreenPipeline(
				SceneShaderKeys.BedShadowBlurEntryPoint,
				new ColorTargetState(SceneColorFormat),
				BedShadowBindGroupLayout,
				"BedShadowBlur");

			var bindGroup = this.cache.GetBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[]
				{
					BindGroupEntry.ForTexture(1, source),
					BindGroupEntry.ForTexture(2, this.whiteTexture),
					BindGroupEntry.ForSampler(9, this.linearSampler),
					BindGroupEntry.ForBuffer(10, settings),
				},
				"BedShadowBlur"));

			using (var encoder = this.device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(destination, LoadOp.Clear, ClearColor.Transparent) },
				DepthAttachment.None,
				"BedShadowBlur")))
			{
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.Draw(3);
			}
		}

		/// <summary>Tints the bed's own texture by the blurred shadow, producing the texture it is drawn with.</summary>
		/// <param name="bedCommand">The queued bed, for its shadow colour.</param>
		private void RenderBedCompositePass(BedRenderCommand bedCommand)
		{
			var settings = this.WriteBedShadowUniform(2, 0, 0, bedCommand.ShadowColor);

			var pipeline = this.GetFullscreenPipeline(
				SceneShaderKeys.BedShadowCompositeEntryPoint,
				new ColorTargetState(SceneColorFormat),
				BedShadowBindGroupLayout,
				"BedShadowComposite");

			var bindGroup = this.cache.GetBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[]
				{
					BindGroupEntry.ForTexture(1, this.bedBaseTexture),
					BindGroupEntry.ForTexture(2, this.bedShadowBlurTargetB),
					BindGroupEntry.ForSampler(9, this.linearSampler),
					BindGroupEntry.ForBuffer(10, settings),
				},
				"BedShadowComposite"));

			using (var encoder = this.device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(this.bedCompositeTarget, LoadOp.Clear, ClearColor.Transparent) },
				DepthAttachment.None,
				"BedShadowComposite")))
			{
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.Draw(3);
			}
		}

		/// <summary>
		/// Publishes one bed post-process pass's settings and returns the buffer holding them.
		/// </summary>
		/// <remarks>
		/// A buffer per pass, not one reused buffer. Queue writes are ordered against submits, not against
		/// passes, so three passes sharing one uniform would all read the last write - which showed up
		/// exactly as it would be expected to: both blur passes ran with the composite's zero direction and
		/// produced an unblurred copy, and the bed shadow came out with a hard edge.
		/// </remarks>
		/// <param name="passIndex">Which of this frame's bed passes is being set up.</param>
		/// <param name="directionX">Horizontal blur step in uv.</param>
		/// <param name="directionY">Vertical blur step in uv.</param>
		/// <param name="shadowColor">The composite's tint colour; unused by the blur.</param>
		private IGpuBuffer WriteBedShadowUniform(int passIndex, float directionX, float directionY, Color shadowColor)
		{
			var span = this.bedShadowScratch.AsSpan();
			GlUniformBlock.WriteVector4(span, 0, directionX, directionY, BedShadowStrength, 0);
			WriteColor(span, 16, shadowColor);

			while (this.bedShadowUniforms.Count <= passIndex)
			{
				this.bedShadowUniforms.Add(
					this.device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, BedShadowUniformSize));
			}

			var buffer = this.bedShadowUniforms[passIndex];
			this.device.WriteBuffer(buffer, 0, this.bedShadowScratch);
			return buffer;
		}

		/// <summary>Creates the bed intermediates, sized from the bed texture and capped.</summary>
		/// <param name="width">The bed texture's width.</param>
		/// <param name="height">The bed texture's height.</param>
		private void EnsureBedTargets(int width, int height)
		{
			int shadowWidth = Math.Min(width, BedTextureSizeLimit);
			int shadowHeight = Math.Min(height, BedTextureSizeLimit);

			if (this.bedShadowMaskTarget != null
				&& this.bedShadowMaskTarget.Descriptor.Width == (uint)shadowWidth
				&& this.bedShadowMaskTarget.Descriptor.Height == (uint)shadowHeight)
			{
				return;
			}

			this.DisposeBedTargets();

			this.bedShadowMaskTarget = this.CreateColorTarget(shadowWidth, shadowHeight, "bedShadowMask");
			this.bedShadowBlurTargetA = this.CreateColorTarget(shadowWidth, shadowHeight, "bedShadowBlurA");
			this.bedShadowBlurTargetB = this.CreateColorTarget(shadowWidth, shadowHeight, "bedShadowBlurB");
			this.bedCompositeTarget = this.CreateColorTarget(shadowWidth, shadowHeight, "bedComposite");

			// New targets hold nothing, so whatever the last signature said about their contents is void.
			this.lastBedShadowSignature = 0;
		}

		/// <summary>
		/// Uploads the bed's own image. Agg generates it with premultiplied colour channels; the mesh
		/// pipeline multiplies by alpha itself, so it is converted back to straight alpha here or a
		/// translucent white bed would render grey.
		/// </summary>
		/// <param name="source">The bed's base image.</param>
		private void EnsureBedBaseTexture(MatterHackers.Agg.Image.ImageBuffer source)
		{
			if (this.bedBaseTexture != null
				&& ReferenceEquals(this.bedBaseSource, source)
				&& this.bedBaseSourceChangedCount == source.ChangedCount)
			{
				return;
			}

			this.cache.InvalidateBindGroupsUsing(this.bedBaseTexture);
			this.bedBaseTexture?.Dispose();
			this.bedBaseTexture = this.device.CreateTexture(new TextureDescriptor(
				(uint)source.Width,
				(uint)source.Height,
				TextureFormat.Bgra8Unorm,
				TextureUsage.TextureBinding | TextureUsage.CopyDst,
				1,
				1,
				"bedBase"));

			this.device.WriteTexture(
				this.bedBaseTexture,
				ImageAlphaConverter.ConvertPremultipliedBgraToStraightAlpha(source.GetBuffer()),
				(uint)(source.Width * 4));

			this.bedBaseSource = source;
			this.bedBaseSourceChangedCount = source.ChangedCount;
			this.lastBedShadowSignature = 0;
		}

		private void DisposeBedTargets()
		{
			// The blur chain samples each of these and the bed draws with the composite, so all four are
			// bound into cached groups that have to go with them.
			this.cache.InvalidateBindGroupsUsing(
				this.bedShadowMaskTarget,
				this.bedShadowBlurTargetA,
				this.bedShadowBlurTargetB,
				this.bedCompositeTarget);

			this.bedShadowMaskTarget?.Dispose();
			this.bedShadowBlurTargetA?.Dispose();
			this.bedShadowBlurTargetB?.Dispose();
			this.bedCompositeTarget?.Dispose();

			this.bedShadowMaskTarget = null;
			this.bedShadowBlurTargetA = null;
			this.bedShadowBlurTargetB = null;
			this.bedCompositeTarget = null;
			this.lastBedShadowSignature = 0;
		}

		// ---- Mesh drawing -----------------------------------------------------------------------------

		/// <summary>
		/// Records one mesh command, submesh by submesh. The interleaved vertex data and its GPU buffer
		/// are retained on <see cref="SceneEdgeShaderDataPlugin"/>, which is itself keyed by mesh, context
		/// and render type and rebuilt when the mesh's change count moves - so the buffer is minted once
		/// per mesh edit rather than once per frame, exactly as the classic path does it.
		/// </summary>
		private void DrawMeshCommand(IRenderEncoder encoder, MeshRenderCommand command, MeshDrawState drawState)
		{
			var world = this.activeSceneRenderContext.WorldView;
			this.WriteTransformUniform(command.Transform * world.ModelviewMatrix, world.ProjectionMatrix);

			var meshPlugin = MeshTrianglePlugin.Get(this.OwnerGl, command.Mesh);
			var sceneShaderData = SceneEdgeShaderDataPlugin.Get(this.OwnerGl, command.Mesh, command.RenderType);

			// Asked of the interleaved data rather than of Mesh.FaceColors, which is what the classic path
			// asks. The two agree for face-coloured meshes, and only this form also catches the colours
			// RenderTypes.Overhang bakes into the same channel from a normal-driven colour function - there
			// is no FaceColors array behind those.
			bool useVertexColor = !command.OverrideFaceColors && HasVertexColors(sceneShaderData);

			this.WriteEffectUniform(
				command.Color,
				command.WireFrameColor,
				drawState.EnableWireframe,
				drawState.WireframeOnly,
				command.Unlit || drawState.Unlit,
				useVertexColor,
				command.AlphaMultiplier,
				drawState.BedGrid);

			var transformBuffer = this.transformUniforms[this.drawSlot];
			var effectBuffer = this.effectUniforms[this.drawSlot];
			this.drawSlot++;

			var cullMode = command.ForceCullBackFaces ? CullMode.Back : CullMode.None;

			for (int subMeshIndex = 0; subMeshIndex < meshPlugin.subMeshs.Count; subMeshIndex++)
			{
				var subMesh = meshPlugin.subMeshs[subMeshIndex];
				var sceneSubMesh = sceneShaderData.SubMeshes[subMeshIndex];
				if (sceneSubMesh.InterleavedData == null || sceneSubMesh.InterleavedData.Length == 0)
				{
					continue;
				}

				// The bed's texture is not the mesh's: it is the shadow composite this frame produced, and
				// it overrides whatever the submesh carries (the classic path's forcedTextureView).
				var texture = drawState.ForcedTexture ?? this.ResolveTexture(subMesh.texture);
				bool useTexture = texture != null;

				var pipeline = this.GetMeshPipeline(drawState, cullMode, useTexture);
				var bindGroup = this.cache.GetBindGroup(new BindGroupDescriptor(
					pipeline,
					0,
					this.BuildMeshBindings(drawState, transformBuffer, effectBuffer, texture ?? this.whiteTexture),
					"SceneMesh"));

				var vertexBuffer = this.EnsureMeshBuffer(command.Mesh, command.RenderType, sceneShaderData, sceneSubMesh);

				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.SetVertexBuffer(0, vertexBuffer);
				encoder.Draw(sceneSubMesh.InterleavedData.Length / SceneVertexFloatStride);
			}
		}

		private void DrawSelectionMask(IRenderEncoder encoder, SelectionOutlineCommand outline)
		{
			var world = this.activeSceneRenderContext.WorldView;

			// The classic path builds the selection command with ForceCullBackFaces false, so the mask is
			// a silhouette even for a mesh with mixed winding.
			//
			// KNOWN DIVERGENCE FROM THE ORACLE (measured, Phase 3 leg A). The classic path *asks* for this
			// same depth-testing, depth-writing state (RenderFlatMask(enableDepthTest: true)), but it asks
			// through ShouldBindDepthStencilState, whose cache was last updated by the depth prepass and
			// was NOT updated by the composite and blit passes that ran in between - those call
			// OMSetDepthStencilState directly. So the request matches the cache, nothing is rebound, and
			// the mask is actually drawn with the blit's depth-off/write-off state: the selection depth
			// target keeps its 1.0 clear, and the outline composite's occlusion test degenerates into "is
			// there any geometry under this pixel", which dims the outline over the selected object's own
			// body. Reproducing that here made Scene.SelectionOutline match the golden channel for channel,
			// which is the evidence; it is not reproduced on purpose, because the bug is frame-shape
			// dependent (an overlay command in the frame changes the cached state and the mask then does
			// test depth). This renderer does what the classic path's code says instead of what its state
			// cache does, and the cross-backend golden carries the difference as a documented allowance.
			this.DrawFlatMask(
				encoder,
				outline.Mesh,
				outline.Transform * world.ModelviewMatrix,
				world.ProjectionMatrix,
				outline.Color,
				new DepthStencilState(SceneDepthFormat, true, CompareFunction.LessEqual),
				"SelectionMask");
		}

		/// <summary>
		/// The classic <c>RenderFlatMask</c>: a mesh's silhouette in one flat colour, position only. Used
		/// for both the selection mask and the bed shadow mask, which differ only in camera and depth
		/// state.
		/// </summary>
		/// <param name="encoder">The open pass.</param>
		/// <param name="mesh">The mesh to fill.</param>
		/// <param name="modelView">Model to eye.</param>
		/// <param name="projection">Eye to clip.</param>
		/// <param name="color">The flat fill colour.</param>
		/// <param name="depth">The depth state, or <see cref="DepthStencilState.None"/> for no depth buffer.</param>
		/// <param name="label">Pipeline and pass label.</param>
		private void DrawFlatMask(
			IRenderEncoder encoder,
			Mesh mesh,
			Matrix4X4 modelView,
			Matrix4X4 projection,
			Color color,
			DepthStencilState depth,
			string label)
		{
			this.WriteTransformUniform(modelView, projection);
			this.WriteEffectUniform(color, Color.Transparent, false, false, false, false, 1.0f);

			var transformBuffer = this.transformUniforms[this.drawSlot];
			var effectBuffer = this.effectUniforms[this.drawSlot];
			this.drawSlot++;

			var pipeline = this.cache.GetPipeline(new RenderPipelineDescriptor(
				this.cache.GetShaderModule(SceneShaderKeys.SceneModule),
				SceneShaderKeys.SelectionVertexEntryPoint,
				this.cache.GetShaderModule(SceneShaderKeys.SceneModule),
				SceneShaderKeys.SelectionMaskEntryPoint,
				new[] { SelectionVertexLayout },
				new[] { new ColorTargetState(SceneColorFormat) },
				SceneBindGroupLayout,
				depth,
				PrimitiveTopology.TriangleList,
				CullMode.None,
				FrontFace.Ccw,
				1,
				label));

			var bindGroup = this.cache.GetBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[]
				{
					BindGroupEntry.ForBuffer(0, transformBuffer),
					BindGroupEntry.ForBuffer(1, this.lightUniform),
					BindGroupEntry.ForBuffer(2, effectBuffer),
					BindGroupEntry.ForSampler(3, this.linearSampler),
					BindGroupEntry.ForTexture(4, this.whiteTexture),
				},
				label));

			var meshPlugin = MeshTrianglePlugin.Get(this.OwnerGl, mesh);
			foreach (var subMesh in meshPlugin.subMeshs)
			{
				if (subMesh.positionData.Count == 0)
				{
					continue;
				}

				var buffer = this.EnsureSelectionBuffer(mesh, meshPlugin, subMesh);
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.SetVertexBuffer(0, buffer);
				encoder.Draw(subMesh.positionData.Count);
			}
		}

		/// <summary>True when any submesh carries a per-vertex colour channel worth reading.</summary>
		/// <param name="sceneShaderData">The mesh's interleaved scene data.</param>
		private static bool HasVertexColors(SceneEdgeShaderDataPlugin sceneShaderData)
		{
			for (int index = 0; index < sceneShaderData.SubMeshes.Count; index++)
			{
				if (sceneShaderData.SubMeshes[index].HasVertexColors)
				{
					return true;
				}
			}

			return false;
		}

		private IGpuTexture ResolveTexture(MatterHackers.Agg.Image.ImageBuffer image)
		{
			if (image == null || this.OwnerGl == null)
			{
				return null;
			}

			// The mesh's face texture reaches the device through the same upload path the 2D stack uses:
			// the plugin owns the GL texture name and the compat layer's store owns the device texture.
			var texturePlugin = ImageTexturePlugin.GetImageTexturePlugin(this.OwnerGl, image, true);
			var entry = texturePlugin == null ? null : this.compat.Textures.Find(texturePlugin.GLTextureHandle);
			return entry?.Texture;
		}

		/// <summary>
		/// The vertex buffer for one submesh of a mesh's scene render data, minted on first use and
		/// retained on the submesh itself afterwards.
		/// </summary>
		/// <param name="mesh">The mesh being drawn, which is what retention is keyed on.</param>
		/// <param name="renderType">The render type whose plugin generation owns this buffer.</param>
		/// <param name="owner">The plugin instance the submesh came from. A different instance than the
		/// slot last saw means the mesh was edited, and the previous generation's buffers are retired.</param>
		/// <param name="sceneSubMesh">The submesh whose interleaved data the buffer holds.</param>
		private IGpuBuffer EnsureMeshBuffer(
			Mesh mesh,
			RenderTypes renderType,
			SceneEdgeShaderDataPlugin owner,
			SceneEdgeShaderSubMeshData sceneSubMesh)
		{
			var slot = this.GetMeshBufferSlot(mesh, (int)renderType, owner);
			if (sceneSubMesh.CachedGpuBuffer is IGpuBuffer cached)
			{
				return cached;
			}

			var bytes = new byte[sceneSubMesh.InterleavedData.Length * sizeof(float)];
			Buffer.BlockCopy(sceneSubMesh.InterleavedData, 0, bytes, 0, bytes.Length);
			var buffer = this.device.CreateBuffer(BufferUsage.Vertex, (ulong)bytes.Length, bytes);
			sceneSubMesh.CachedGpuBuffer = buffer;
			slot.Buffers.Add(buffer);
			return buffer;
		}

		/// <summary>
		/// The position-only vertex buffer the selection mask draws, minted on first use and retained on
		/// the submesh afterwards.
		/// </summary>
		/// <param name="mesh">The mesh being masked.</param>
		/// <param name="owner">The triangle plugin the submesh came from; a new instance retires the old
		/// generation's buffers exactly as it does for the scene data.</param>
		/// <param name="subMesh">The submesh whose positions the buffer holds.</param>
		private IGpuBuffer EnsureSelectionBuffer(Mesh mesh, MeshTrianglePlugin owner, SubTriangleMesh subMesh)
		{
			var slot = this.GetMeshBufferSlot(mesh, SelectionBufferSlot, owner);
			if (subMesh.CachedSelectionGpuBuffer is IGpuBuffer cached)
			{
				return cached;
			}

			int count = subMesh.positionData.Count;
			var bytes = new byte[count * 3 * sizeof(float)];
			var span = bytes.AsSpan();
			for (int index = 0; index < count; index++)
			{
				var position = subMesh.positionData.Array[index];
				WriteFloat(span, (index * 3 * sizeof(float)) + 0, position.positionX);
				WriteFloat(span, (index * 3 * sizeof(float)) + 4, position.positionY);
				WriteFloat(span, (index * 3 * sizeof(float)) + 8, position.positionZ);
			}

			var buffer = this.device.CreateBuffer(BufferUsage.Vertex, (ulong)bytes.Length, bytes);
			subMesh.CachedSelectionGpuBuffer = buffer;
			slot.Buffers.Add(buffer);
			return buffer;
		}

		/// <summary>
		/// The retention slot for one mesh and slot key, retiring the buffers of any earlier plugin
		/// generation the moment a new one draws through it.
		/// </summary>
		/// <param name="mesh">The mesh being drawn.</param>
		/// <param name="slotKey">Which family of buffers: a <see cref="RenderTypes"/> value for scene data,
		/// or <see cref="SelectionBufferSlot"/> for the selection positions.</param>
		/// <param name="owner">The plugin instance that minted (or is about to mint) the buffers.</param>
		private MeshBufferSlot GetMeshBufferSlot(Mesh mesh, int slotKey, object owner)
		{
			var slotsForMesh = this.meshBufferSlots.GetValue(mesh, _ => new Dictionary<int, MeshBufferSlot>());
			if (!slotsForMesh.TryGetValue(slotKey, out var slot))
			{
				slot = new MeshBufferSlot { Owner = owner };
				slotsForMesh[slotKey] = slot;
				this.retainedMeshBuffers.Add(slot);
			}
			else if (!ReferenceEquals(slot.Owner, owner))
			{
				slot.RetireInto(this.retiredMeshBuffers);
				slot.Owner = owner;
			}

			return slot;
		}

		/// <summary>Releases the buffers retired since the last submit. Called after one.</summary>
		private void DisposeRetiredMeshBuffers()
		{
			foreach (var buffer in this.retiredMeshBuffers)
			{
				buffer.Dispose();
			}

			this.retiredMeshBuffers.Clear();
		}

		// ---- Pipelines --------------------------------------------------------------------------------

		private static readonly VertexBufferLayout SceneVertexLayout = new VertexBufferLayout(
			SceneVertexStride,
			new[]
			{
				new VertexAttribute(0, VertexFormat.Float32x3, 0),
				new VertexAttribute(1, VertexFormat.Float32x3, 12),
				new VertexAttribute(2, VertexFormat.Float32x2, 24),
				new VertexAttribute(3, VertexFormat.Float32x3, 32),
				new VertexAttribute(4, VertexFormat.Float32x4, 44),
			});

		private static readonly VertexBufferLayout SelectionVertexLayout = new VertexBufferLayout(
			3 * sizeof(float),
			new[] { new VertexAttribute(0, VertexFormat.Float32x3, 0) });

		private static readonly BindGroupLayoutEntry[] SceneBindGroupLayout =
		{
			new BindGroupLayoutEntry(0, 0, ShaderStage.Vertex, BindingType.UniformBuffer),
			new BindGroupLayoutEntry(0, 1, ShaderStage.Fragment, BindingType.UniformBuffer),
			new BindGroupLayoutEntry(0, 2, ShaderStage.Fragment, BindingType.UniformBuffer),
			new BindGroupLayoutEntry(0, 3, ShaderStage.Fragment, BindingType.Sampler),
			new BindGroupLayoutEntry(0, 4, ShaderStage.Fragment, BindingType.Texture),
		};

		/// <summary>
		/// The peel bind group layout: the mesh layout plus the opaque depth every peel stage tests
		/// against. The init passes stop there - they write the very range the other two bindings would
		/// name, and WebGPU (rightly) refuses a pass that samples its own depth attachment.
		/// </summary>
		private static readonly BindGroupLayoutEntry[] PeelInitBindGroupLayout = AppendEntries(
			SceneBindGroupLayout,
			new BindGroupLayoutEntry(0, 5, ShaderStage.Fragment, BindingType.DepthTexture));

		/// <summary>
		/// The peel layout for the iteration passes, which also read the previous range - and the blurred
		/// bed shadow, because the textured peel shaders run the analytic bed grid (switched off for every
		/// draw that is not the bed, as the classic path switches it off with the same flag).
		/// </summary>
		private static readonly BindGroupLayoutEntry[] PeelBindGroupLayout = AppendEntries(
			PeelInitBindGroupLayout,
			new BindGroupLayoutEntry(0, 6, ShaderStage.Fragment, BindingType.DepthTexture),
			new BindGroupLayoutEntry(0, 7, ShaderStage.Fragment, BindingType.DepthTexture),
			new BindGroupLayoutEntry(0, 8, ShaderStage.Fragment, BindingType.Texture));

		/// <summary>
		/// What the two bed shadow post-process passes bind: the source texture (the module's texture0),
		/// the second texture the composite pass reads, a linear sampler and the blur/composite settings.
		/// The blur only uses the first, but a layout may be a superset of what an entry point touches and
		/// one layout keeps the two passes' bind groups the same shape.
		/// </summary>
		private static readonly BindGroupLayoutEntry[] BedShadowBindGroupLayout =
		{
			new BindGroupLayoutEntry(0, 1, ShaderStage.Fragment, BindingType.Texture),
			new BindGroupLayoutEntry(0, 2, ShaderStage.Fragment, BindingType.Texture),
			new BindGroupLayoutEntry(0, 9, ShaderStage.Fragment, BindingType.Sampler),
			new BindGroupLayoutEntry(0, 10, ShaderStage.Fragment, BindingType.UniformBuffer),
		};

		private static BindGroupLayoutEntry[] AppendEntries(BindGroupLayoutEntry[] layout, params BindGroupLayoutEntry[] extra)
		{
			var combined = new BindGroupLayoutEntry[layout.Length + extra.Length];
			Array.Copy(layout, combined, layout.Length);
			Array.Copy(extra, 0, combined, layout.Length, extra.Length);
			return combined;
		}

		/// <summary>The bindings one mesh draw needs, which grow by the depth textures on a peel pass.</summary>
		/// <param name="drawState">Which pass this draw is in.</param>
		/// <param name="transformBuffer">This draw's transform uniform.</param>
		/// <param name="effectBuffer">This draw's effect uniform.</param>
		/// <param name="texture">The submesh's texture, or the white 1x1 stand-in.</param>
		private BindGroupEntry[] BuildMeshBindings(
			in MeshDrawState drawState,
			IGpuBuffer transformBuffer,
			IGpuBuffer effectBuffer,
			IGpuTexture texture)
		{
			var bindings = new List<BindGroupEntry>(8)
			{
				BindGroupEntry.ForBuffer(0, transformBuffer),
				BindGroupEntry.ForBuffer(1, this.lightUniform),
				BindGroupEntry.ForBuffer(2, effectBuffer),
				BindGroupEntry.ForSampler(3, this.linearSampler),
				BindGroupEntry.ForTexture(4, texture),
			};

			if (drawState.Peel != PeelStage.None)
			{
				bindings.Add(BindGroupEntry.ForTexture(5, this.sceneColorTarget.Depth));
			}

			if (drawState.PeelSource != null)
			{
				bindings.Add(BindGroupEntry.ForTexture(6, drawState.PeelSource.Near));
				bindings.Add(BindGroupEntry.ForTexture(7, drawState.PeelSource.Far));
				bindings.Add(BindGroupEntry.ForTexture(8, this.bedShadowBlurTargetB ?? this.whiteTexture));
			}

			return bindings.ToArray();
		}

		private IRenderPipeline GetMeshPipeline(in MeshDrawState drawState, CullMode cullMode, bool useTexture)
		{
			var module = this.cache.GetShaderModule(SceneShaderKeys.SceneModule);

			string fragmentEntry;
			ColorTargetState[] colorTargets;
			BindGroupLayoutEntry[] bindGroupLayout = SceneBindGroupLayout;

			switch (drawState.Peel)
			{
				case PeelStage.Init:
					fragmentEntry = SceneShaderKeys.PeelInitEntryPoint;
					colorTargets = Array.Empty<ColorTargetState>();
					bindGroupLayout = PeelInitBindGroupLayout;
					break;

				case PeelStage.Depth:
					fragmentEntry = useTexture
						? SceneShaderKeys.PeelDepthTextureEntryPoint
						: SceneShaderKeys.PeelDepthColorEntryPoint;
					colorTargets = Array.Empty<ColorTargetState>();
					bindGroupLayout = PeelBindGroupLayout;
					break;

				case PeelStage.Color:
					fragmentEntry = useTexture
						? SceneShaderKeys.PeelTextureEntryPoint
						: SceneShaderKeys.PeelColorEntryPoint;
					colorTargets = new[] { FrontAccumTargetState, BackAccumTargetState };
					bindGroupLayout = PeelBindGroupLayout;
					break;

				default:
					fragmentEntry = drawState.DepthOnly
						? SceneShaderKeys.SceneDepthOnlyEntryPoint
						: useTexture ? SceneShaderKeys.SceneTextureEntryPoint : SceneShaderKeys.SceneColorEntryPoint;
					colorTargets = drawState.DepthOnly
						? Array.Empty<ColorTargetState>()
						: new[]
						{
							new ColorTargetState(
								drawState.ColorFormat,
								drawState.BlendEnabled,
								drawState.Blend,
								drawState.BlendEnabled
									? new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha)
									: default),
						};
					break;
			}

			// The overlay and peel-colour passes have no depth attachment at all; every other pass tests
			// with drawState's compare (LessEqual for scene geometry, Less/Greater for the two halves of a
			// peeled depth range) and writes.
			var depth = drawState.DepthFormat == TextureFormat.Undefined
				? DepthStencilState.None
				: new DepthStencilState(drawState.DepthFormat, true, drawState.EffectiveDepthCompare);

			return this.cache.GetPipeline(new RenderPipelineDescriptor(
				module,
				SceneShaderKeys.SceneVertexEntryPoint,
				module,
				fragmentEntry,
				new[] { SceneVertexLayout },
				colorTargets,
				bindGroupLayout,
				depth,
				PrimitiveTopology.TriangleList,
				cullMode,
				FrontFace.Ccw,
				1,
				"SceneMesh"));
		}

		private IRenderPipeline GetFullscreenPipeline(
			string fragmentEntryPoint,
			in ColorTargetState colorTarget,
			BindGroupLayoutEntry[] layout,
			string label)
		{
			var module = this.cache.GetShaderModule(SceneShaderKeys.PostProcessModule);
			return this.cache.GetPipeline(new RenderPipelineDescriptor(
				module,
				SceneShaderKeys.FullscreenVertexEntryPoint,
				module,
				fragmentEntryPoint,
				Array.Empty<VertexBufferLayout>(),
				new[] { colorTarget },
				layout,
				DepthStencilState.None,
				PrimitiveTopology.TriangleList,
				CullMode.None,
				FrontFace.Ccw,
				1,
				label));
		}

		// ---- Uniforms ---------------------------------------------------------------------------------

		private void WriteTransformUniform(Matrix4X4 modelView, Matrix4X4 projection)
		{
			var span = this.transformScratch.AsSpan();
			GlUniformBlock.WriteMatrix(span, 0, modelView);

			// The same 0..w clip depth remap the classic path's UpdateTransformBuffer applies; the WGSL
			// therefore has no z fixup of its own.
			GlUniformBlock.WriteMatrix(span, 64, GlUniformBlock.ToClipSpaceProjection(projection));

			var buffer = this.EnsureUniformSlot(this.transformUniforms, TransformUniformSize);
			this.device.WriteBuffer(buffer, 0, this.transformScratch);
		}

		private void WriteEffectUniform(
			Color meshColor,
			Color wireframeColor,
			bool enableWireframe,
			bool wireframeOnly,
			bool unlit,
			bool useVertexColor,
			float alphaMultiplier,
			BedRenderCommand bedGrid = null)
		{
			// The classic path's default when a command carries no wireframe color.
			var effectiveWireframeColor = wireframeColor.Alpha0To1 > 0 ? wireframeColor : new Color(25, 25, 25);

			var span = this.effectScratch.AsSpan();
			WriteColor(span, 0, meshColor);
			WriteColor(span, 16, effectiveWireframeColor);

			// EffectFlags: z and w are the depth peeling flags, always off on the opaque path.
			GlUniformBlock.WriteVector4(span, 32, enableWireframe ? 1 : 0, wireframeOnly ? 1 : 0, 0, 0);

			// targetWidth/Height are already device pixels (EnsureFrameResources scaled them), but the
			// wireframe width is a constant in device pixels, so it has to be scaled here to keep the
			// same on-screen thickness through a supersampled capture.
			GlUniformBlock.WriteVector4(
				span,
				48,
				this.targetWidth,
				this.targetHeight,
				SceneRenderModeUtilities.DefaultWireframeWidth * this.supersampleScale,
				unlit ? 1 : 0);

			GlUniformBlock.WriteVector4(
				span,
				64,
				useVertexColor ? 1 : 0,
				alphaMultiplier,
				bedGrid != null ? 1 : 0,
				bedGrid != null ? BedShadowStrength : 0);

			this.WriteBedGridConstants(span, bedGrid);

			var buffer = this.EnsureUniformSlot(this.effectUniforms, EffectUniformSize);
			this.device.WriteBuffer(buffer, 0, this.effectScratch);
		}

		/// <summary>
		/// Fills the bed grid half of the effect uniform (bytes 80..192), or zeroes it. Line widths are
		/// halved here because the shader measures distance from a line's centre, and scaled by the
		/// supersample factor so the on-screen thickness survives a full-frame capture the way the
		/// wireframe width does.
		/// </summary>
		/// <param name="span">The effect uniform scratch.</param>
		/// <param name="bedGrid">The queued bed, or null on every other draw.</param>
		private void WriteBedGridConstants(Span<byte> span, BedRenderCommand bedGrid)
		{
			if (bedGrid == null)
			{
				span.Slice(80, EffectUniformSize - 80).Clear();
				return;
			}

			var bounds = bedGrid.BedBounds;
			GlUniformBlock.WriteVector4(span, 80, (float)bounds.Left, (float)bounds.Bottom, (float)bounds.Width, (float)bounds.Height);

			WriteColor(span, 96, bedGrid.GridLineColor);
			WriteColor(span, 112, bedGrid.AxisXColor);
			WriteColor(span, 128, bedGrid.AxisYColor);
			WriteColor(span, 144, bedGrid.AxisZColor);

			GlUniformBlock.WriteVector4(
				span,
				160,
				(float)bedGrid.GridSpacing,
				bedGrid.GridLineWidthPixels * 0.5f * this.supersampleScale,
				bedGrid.AxisLineWidthPixels * 0.5f * this.supersampleScale,
				(float)bedGrid.AxisHeight);

			// The same shadow colour the composite pass tints the fill with, so an analytic line darkens
			// identically where it crosses an object's shadow.
			WriteColor(span, 176, bedGrid.ShadowColor);
		}

		/// <summary>
		/// Publishes the frame's lighting. The classic path sets the lights through the GL emulation with
		/// an identity modelview - GL transforms a light position by the current modelview, and
		/// RenderHelper.SetGlContext sets them before loading the camera - so the directions are already
		/// in eye space and are written straight through here.
		/// </summary>
		private void WriteLightUniform()
		{
			var lighting = this.activeSceneRenderContext.Lighting ?? new LightingData();
			var span = this.lightScratch.AsSpan();

			GlUniformBlock.WriteVector4(span, 0, lighting.LightDirection0);
			GlUniformBlock.WriteVector4(span, 16, lighting.AmbientLight);
			GlUniformBlock.WriteVector4(span, 32, lighting.DiffuseLight0);

			GlUniformBlock.WriteVector4(span, 48, lighting.LightDirection1);

			// Light 1 has no ambient term: the classic path never sets one, so its LightData keeps the
			// GL default of black.
			GlUniformBlock.WriteVector4(span, 64, 0, 0, 0, 1);
			GlUniformBlock.WriteVector4(span, 80, lighting.DiffuseLight1);

			// Both lights are on for every scene draw, as UpdateLightBuffer(true, true) says.
			GlUniformBlock.WriteVector4(span, 96, 1, 1, 0, 0);

			this.device.WriteBuffer(this.lightUniform, 0, this.lightScratch);
		}

		private void WriteOutlineUniform()
		{
			var span = this.outlineScratch.AsSpan();

			// The outline width is in target pixels, so it scales with the capture the same way the
			// wireframe width does - it has to downsample back to the same ~2 screen pixels.
			GlUniformBlock.WriteVector4(
				span,
				0,
				2.0f * this.supersampleScale,
				0.35f,
				this.targetWidth,
				this.targetHeight);
			GlUniformBlock.WriteVector4(span, 16, 0, 0, 0, 0);
			this.device.WriteBuffer(this.outlineUniform, 0, this.outlineScratch);
		}

		private void WriteDownsampleUniform()
		{
			var span = this.downsampleScratch.AsSpan();
			GlUniformBlock.WriteVector4(
				span,
				0,
				1.0f / this.sampleFrameColor.Descriptor.Width,
				1.0f / this.sampleFrameColor.Descriptor.Height,
				0,
				0);
			this.device.WriteBuffer(this.downsampleUniform, 0, this.downsampleScratch);
		}

		/// <summary>
		/// Returns this draw's slot in a uniform pool, growing the pool on demand. The slot index is the
		/// draw counter, so the same draw ordinal gets the same buffer every frame - which is what makes
		/// the bind groups built around them hit the cache instead of being minted per draw per frame.
		/// </summary>
		/// <param name="pool">The pool to index.</param>
		/// <param name="sizeInBytes">Size of a buffer in this pool.</param>
		private IGpuBuffer EnsureUniformSlot(List<IGpuBuffer> pool, int sizeInBytes)
		{
			while (pool.Count <= this.drawSlot)
			{
				pool.Add(this.device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, (ulong)sizeInBytes));
			}

			return pool[this.drawSlot];
		}

		private static void WriteColor(Span<byte> destination, int offset, Color color)
			=> GlUniformBlock.WriteVector4(
				destination,
				offset,
				color.Red0To1,
				color.Green0To1,
				color.Blue0To1,
				color.Alpha0To1);

		private static void WriteFloat(Span<byte> destination, int offset, float value)
			=> System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(offset, 4), value);

		// ---- Targets ----------------------------------------------------------------------------------

		/// <summary>
		/// Creates the objects that outlive any one frame and do not depend on the frame's size. Split out
		/// of <see cref="EnsureFrameResources"/> because the supersample capture needs the sampler and the
		/// downsample uniform even in a frame that queued no scene geometry at all.
		/// </summary>
		private void EnsureSharedResources()
		{
			this.linearSampler = this.linearSampler ?? this.device.CreateSampler(SamplerDescriptor.LinearClamp);
			this.pointSampler = this.pointSampler ?? this.device.CreateSampler(SamplerDescriptor.NearestClamp);
			this.lightUniform = this.lightUniform
				?? this.device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, LightUniformSize);
			this.outlineUniform = this.outlineUniform
				?? this.device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, OutlineUniformSize);
			this.downsampleUniform = this.downsampleUniform
				?? this.device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, DownsampleUniformSize);

			if (this.whiteTexture == null)
			{
				this.whiteTexture = this.device.CreateTexture(new TextureDescriptor(
					1,
					1,
					TextureFormat.Rgba8Unorm,
					TextureUsage.TextureBinding | TextureUsage.CopyDst,
					1,
					1,
					"sceneWhite"));
				this.device.WriteTexture(this.whiteTexture, new byte[] { 255, 255, 255, 255 }, 4);
			}
		}

		/// <summary>Creates or resizes the supersample capture target and its depth buffer.</summary>
		/// <param name="width">Capture width in device pixels.</param>
		/// <param name="height">Capture height in device pixels.</param>
		/// <param name="format">The caller's target format, matched so the round trip cannot convert.</param>
		private void EnsureSampleFrameTargets(int width, int height, TextureFormat format)
		{
			if (this.sampleFrameColor != null
				&& this.sampleFrameColor.Descriptor.Width == (uint)width
				&& this.sampleFrameColor.Descriptor.Height == (uint)height
				&& this.sampleFrameColor.Descriptor.Format == format)
			{
				return;
			}

			this.DisposeSampleFrameTargets();

			this.sampleFrameColor = this.device.CreateTexture(new TextureDescriptor(
				(uint)width,
				(uint)height,
				format,
				TextureUsage.RenderAttachment | TextureUsage.TextureBinding,
				1,
				1,
				"sampleFrameColor"));

			this.sampleFrameDepth = this.device.CreateTexture(new TextureDescriptor(
				(uint)width,
				(uint)height,
				SceneDepthFormat,
				TextureUsage.RenderAttachment | TextureUsage.TextureBinding,
				1,
				1,
				"sampleFrameDepth"));
		}

		private void DisposeSampleFrameTargets()
		{
			// The downsample pass samples sampleFrameColor, so its bind group dies with the capture target.
			this.cache.InvalidateBindGroupsUsing(this.sampleFrameColor, this.sampleFrameDepth);

			this.sampleFrameColor?.Dispose();
			this.sampleFrameDepth?.Dispose();
			this.sampleFrameColor = null;
			this.sampleFrameDepth = null;
		}

		private void EnsureFrameResources(int width, int height)
		{
			this.EnsureSharedResources();

			if (this.targetWidth == width && this.targetHeight == height && this.sceneColorTarget != null)
			{
				return;
			}

			this.DisposeTargets();
			this.targetWidth = width;
			this.targetHeight = height;

			this.sceneColorTarget = this.CreateSceneTarget(width, height, true, "sceneColor");
			this.sceneDepthTarget = this.CreateSceneTarget(width, height, false, "sceneDepth");
			this.selectionTarget = this.CreateSceneTarget(width, height, true, "sceneSelection");
			this.resolvedSceneTarget = this.CreateColorTarget(width, height, "sceneResolved");
			this.transparentOverlayTarget = this.CreateColorTarget(width, height, "sceneOverlay");
			this.frontAccumTarget = this.CreateColorTarget(width, height, "sceneFrontAccum", TransparencyAccumFormat);
			this.backAccumTarget = this.CreateColorTarget(width, height, "sceneBackAccum", TransparencyAccumFormat);
			this.peelRangeA = this.CreatePeelRange(width, height, "peelRangeA");
			this.peelRangeB = this.CreatePeelRange(width, height, "peelRangeB");
		}

		private PeelDepthRange CreatePeelRange(int width, int height, string label)
			=> new PeelDepthRange
			{
				Near = this.CreateDepthTarget(width, height, label + "Near"),
				Far = this.CreateDepthTarget(width, height, label + "Far"),
			};

		private IGpuTexture CreateDepthTarget(int width, int height, string label)
			=> this.device.CreateTexture(new TextureDescriptor(
				(uint)width,
				(uint)height,
				SceneDepthFormat,
				TextureUsage.RenderAttachment | TextureUsage.TextureBinding,
				1,
				1,
				label));

		private SceneTarget CreateSceneTarget(int width, int height, bool withColor, string label)
		{
			return new SceneTarget
			{
				Color = withColor ? this.CreateColorTarget(width, height, label) : null,
				Depth = this.CreateDepthTarget(width, height, label + "Depth"),
			};
		}

		private IGpuTexture CreateColorTarget(int width, int height, string label, TextureFormat format = SceneColorFormat)
			=> this.device.CreateTexture(new TextureDescriptor(
				(uint)width,
				(uint)height,
				format,
				TextureUsage.RenderAttachment | TextureUsage.TextureBinding,
				1,
				1,
				label));

		private void ClearColorTarget(IGpuTexture target, ClearColor clearColor, string label)
		{
			using (this.device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(target, LoadOp.Clear, clearColor) },
				DepthAttachment.None,
				label)))
			{
			}
		}

		/// <summary>
		/// Sets the viewport for a pass that draws into the caller's target. GL measures viewports from
		/// the bottom left and WebGPU (like D3D) from the top left, which is the same conversion
		/// <c>SceneViewportUtilities</c> does for the classic path.
		/// </summary>
		private void ApplySceneViewport(IRenderEncoder encoder, IGpuTexture destination)
		{
			var viewport = this.activeSceneRenderContext.Viewport;
			int scale = this.supersampleScale;
			int x = (int)viewport.Left * scale;
			int y = (int)viewport.Bottom * scale;
			int width = Math.Max(1, (int)Math.Ceiling(viewport.Width)) * scale;
			int height = Math.Max(1, (int)Math.Ceiling(viewport.Height)) * scale;
			int topDownY = (int)destination.Descriptor.Height - y - height;

			encoder.SetViewport(x, topDownY, width, height);
		}

		private void DisposeTargets()
		{
			// Every one of these is sampled by some pass (the composite reads the scene colour and the two
			// accumulators, the peel reads its own depth ranges), so the bind groups built over them are
			// cached against textures that are about to stop existing. A resize would otherwise strand one
			// whole generation of groups per resize in a cache that never evicts.
			this.cache.InvalidateBindGroupsUsing(
				this.sceneColorTarget?.Color,
				this.sceneColorTarget?.Depth,
				this.sceneDepthTarget?.Color,
				this.sceneDepthTarget?.Depth,
				this.selectionTarget?.Color,
				this.selectionTarget?.Depth,
				this.resolvedSceneTarget,
				this.transparentOverlayTarget,
				this.frontAccumTarget,
				this.backAccumTarget,
				this.peelRangeA?.Near,
				this.peelRangeA?.Far,
				this.peelRangeB?.Near,
				this.peelRangeB?.Far);

			this.sceneColorTarget?.Dispose();
			this.sceneDepthTarget?.Dispose();
			this.selectionTarget?.Dispose();
			this.resolvedSceneTarget?.Dispose();
			this.transparentOverlayTarget?.Dispose();
			this.frontAccumTarget?.Dispose();
			this.backAccumTarget?.Dispose();
			this.peelRangeA?.Dispose();
			this.peelRangeB?.Dispose();

			this.peelRangeA = null;
			this.peelRangeB = null;
			this.sceneColorTarget = null;
			this.sceneDepthTarget = null;
			this.selectionTarget = null;
			this.resolvedSceneTarget = null;
			this.transparentOverlayTarget = null;
			this.frontAccumTarget = null;
			this.backAccumTarget = null;
			this.targetWidth = 0;
			this.targetHeight = 0;
		}

		private void ClearQueuedSceneEffects()
		{
			this.queuedSceneCommands.Clear();
			this.queuedOverlayCommands.Clear();
			this.queuedSelectionOutlines.Clear();
			this.queuedBedCommand = null;

			// The plan holds the same commands, and with them the meshes; a renderer that outlives the
			// frame would keep the last frame's geometry rooted until some later frame rebuilt the plan.
			this.renderPlanner.ReleasePlan();
		}

		private sealed class SelectionOutlineCommand
		{
			public Color Color;

			public Mesh Mesh;

			public Matrix4X4 Transform;
		}

		/// <summary>
		/// The vertex buffers one mesh render-data plugin generation minted, and which generation that
		/// was. See <see cref="meshBufferSlots"/>.
		/// </summary>
		private sealed class MeshBufferSlot
		{
			/// <summary>The plugin instance these buffers belong to. Compared by reference only.</summary>
			public object Owner;

			/// <summary>The buffers minted for that instance, one per submesh drawn so far.</summary>
			public List<IGpuBuffer> Buffers { get; } = new List<IGpuBuffer>();

			/// <summary>Hands the buffers to the caller's retirement list and empties the slot.</summary>
			/// <param name="retired">The list that owns them until the next submit has happened.</param>
			public void RetireInto(List<IGpuBuffer> retired)
			{
				retired.AddRange(this.Buffers);
				this.Buffers.Clear();
			}
		}

		private sealed class SceneTarget : IDisposable
		{
			public IGpuTexture Color;

			public IGpuTexture Depth;

			public void Dispose()
			{
				this.Color?.Dispose();
				this.Depth?.Dispose();
			}
		}

		/// <summary>Which of the three peel passes a draw is in, or none for ordinary scene geometry.</summary>
		private enum PeelStage
		{
			/// <summary>Not a peel pass.</summary>
			None = 0,

			/// <summary>Seeds the first depth range (<c>DualDepthInitPS</c>).</summary>
			Init,

			/// <summary>Narrows one half of the depth range for the next iteration.</summary>
			Depth,

			/// <summary>Accumulates the two layers this iteration peels.</summary>
			Color,
		}

		/// <summary>What one mesh draw needs that the command itself does not carry: which pass it is in.</summary>
		private struct MeshDrawState
		{
			public TextureFormat ColorFormat;

			public TextureFormat DepthFormat;

			public bool DepthOnly;

			public bool EnableWireframe;

			public bool WireframeOnly;

			public bool BlendEnabled;

			public BlendComponent Blend;

			public PeelStage Peel;

			/// <summary>The depth test, defaulting to the scene's LessEqual so zero-init stays the common case.</summary>
			public CompareFunction DepthCompare;

			/// <summary>The previous iteration's peeled range, sampled by the peel shaders; null in the
			/// init passes, which are writing that range rather than reading it.</summary>
			public PeelDepthRange PeelSource;

			/// <summary>Overrides every submesh's own texture - the bed's composited shadow texture.</summary>
			public IGpuTexture ForcedTexture;

			/// <summary>Forces the draw unlit whatever the command says, as the bed's peel draw is.</summary>
			public bool Unlit;

			/// <summary>Switches on the analytic grid and publishes its styling; null for every other draw.</summary>
			public BedRenderCommand BedGrid;

			/// <summary>The depth test to build the pipeline with; <see cref="CompareFunction.Never"/> is
			/// not a value any pass here wants, so it doubles as "unset".</summary>
			public CompareFunction EffectiveDepthCompare
				=> this.DepthCompare == CompareFunction.Never ? CompareFunction.LessEqual : this.DepthCompare;
		}

		/// <summary>
		/// One peeled depth range: the nearest and farthest depth still to be resolved, per pixel. Two of
		/// these ping-pong through the peel loop, standing in for the classic path's pair of MAX-blended
		/// Rg32Float targets.
		/// </summary>
		private sealed class PeelDepthRange : IDisposable
		{
			/// <summary>min(z) of the remaining layers - the classic target's negated red channel.</summary>
			public IGpuTexture Near;

			/// <summary>max(z) of the remaining layers - the classic target's green channel.</summary>
			public IGpuTexture Far;

			public void Dispose()
			{
				this.Near?.Dispose();
				this.Far?.Dispose();
			}
		}
	}
}
