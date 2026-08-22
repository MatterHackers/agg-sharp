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
	/// backend renders through. Ported from the scene half of the classic D3D11 backend
	/// (<c>VorticeD3DGl</c> and <c>NodeDesignerScene.hlsl</c>, both deleted in Phase 4.5 once the goldens
	/// re-baselined onto this renderer); the WGSL shaders live beside the backend in
	/// <c>WebGpuRender/Shaders</c>.
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
	/// capture, the printer bed with its cast shadow and analytic grid, and both transparency modes - dual
	/// depth peeling and the sorted alpha-blend approximation the user setting falls back to when peeling
	/// is switched off (see <see cref="DepthPeelingLayers"/>).
	/// </para>
	/// </summary>
	public sealed class WebGpuSceneRenderer : INativeSceneRenderer, IDisposable
	{
		/// <summary>Floats per vertex in the scene interleaved format: position, normal, uv, edge hints, color.</summary>
		private const int SceneVertexFloatStride = SceneEdgeShaderDataPlugin.TotalVertexFloatStride;

		private const int SceneVertexStride = SceneVertexFloatStride * sizeof(float);

		/// <summary>Bytes per vertex of the position-only selection/shadow mask format.</summary>
		private const int SelectionVertexStride = 3 * sizeof(float);

		/// <summary>
		/// Vertices per primitive. Every mesh buffer here is an unindexed triangle list, so this is the
		/// granularity a vertex buffer may be split on - a chunk boundary anywhere else would drop the
		/// triangle that straddles it.
		/// </summary>
		private const int VerticesPerPrimitive = 3;

		private const int TransformUniformSize = 128;

		private const int LightUniformSize = 112;

		/// <summary>
		/// 12 float4s: the five the classic SceneEffectBuffer starts with plus the seven of the analytic
		/// bed grid block. Written in full on every draw (zeroed where the bed block does not apply), as
		/// the classic path writes its whole constant buffer.
		/// </summary>
		private const int EffectUniformSize = 192;

		/// <summary>
		/// Bytes between one draw's uniform slot and the next. A bound range's offset must be a multiple of
		/// WebGPU's guaranteed minUniformBufferOffsetAlignment (256), so a slot is two of those: the
		/// transform block at the slot's start and the effect block 256 bytes in.
		/// </summary>
		private const int UniformSlotStride = 512;

		/// <summary>Byte offset of the effect block within a draw's slot.</summary>
		private const int EffectBlockOffset = 256;

		/// <summary>
		/// Compile time cross-checks, not values anything reads: growing either block past the room its
		/// slot leaves would make a draw overwrite the block after it - the effect block for the transform
		/// block, the next draw's slot for the effect block. Unsigned is what turns the resulting negative
		/// constant into a build error instead of a rendering mystery.
		/// </summary>
		private const uint TransformBlockHeadroom = EffectBlockOffset - TransformUniformSize;

		private const uint EffectBlockHeadroom = UniformSlotStride - (EffectBlockOffset + EffectUniformSize);

		/// <summary>Draw slots per uniform buffer; 1024 covers a depth-peeled frame outright.</summary>
		private const int UniformSlotsPerBuffer = 1024;

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
		/// The linear supersample factor <see cref="BeginFullFrameCapture"/> uses when the device will size a
		/// target that large: the capture target is this many times the caller's target in each dimension, so
		/// every output pixel averages a 3x3 block. Matches the classic path's
		/// <c>VorticeD3DGl.SupersampleScale</c>; the goldens are captured at it.
		/// </summary>
		public const int MaxSupersampleScale = 3;

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

		// One uniform slot per draw, indexed by a counter that resets each frame, staged in a CPU array and
		// pushed to the GPU in one write per submit rather than the two per draw it used to take - a
		// depth-peeled frame has ~740 draw slots, and at ~13 us a queue write that was ~19 ms of a frame.
		// See StagedUniformBuffers for why one slot per draw is still required.
		private readonly StagedUniformBuffers uniforms;

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

		// This frame's mesh draw setup, memoized. A depth-peeled frame draws every command a dozen times -
		// the depth prepass, the two peel inits, and a depth plus a colour pass per iteration - and each of
		// those draws wants the same transform and effect bytes, so a slot per draw meant ~940 slots and
		// ~940 plugin lookups where ~100 of each would do. Emptied with the frame's queued commands, which
		// is both when the slots it names are reused and the only time it stops rooting their meshes.
		private readonly Dictionary<MeshDrawSetupKey, MeshDrawSetup> meshDrawSetups
			= new Dictionary<MeshDrawSetupKey, MeshDrawSetup>();

		private readonly byte[] lightScratch = new byte[LightUniformSize];
		private readonly byte[] outlineScratch = new byte[OutlineUniformSize];
		private readonly byte[] downsampleScratch = new byte[DownsampleUniformSize];
		private readonly byte[] bedShadowScratch = new byte[BedShadowUniformSize];

		private SceneRenderContext activeSceneRenderContext;
		private int drawSlot;

		/// <summary>
		/// How many mesh vertex buffers this renderer has minted since it was created. The same number the
		/// <c>Scene.VertexChunkCreate</c> frame counter reports, readable without the profiler switched on:
		/// a scene that redraws unchanged geometry must not move it, and a test can say so.
		/// </summary>
		public int VertexBufferCreateCount { get; private set; }

		/// <summary>Device pixels per logical pixel: 1 normally, the frame's capture scale (at most
		/// <see cref="MaxSupersampleScale"/>) while a full-frame capture is in progress. Applied to the
		/// scene's target sizes and to every width the shaders measure in pixels, exactly as the classic path
		/// applies its supersampleScale.</summary>
		private int supersampleScale = 1;

		/// <summary>
		/// The scale the capture target that currently exists was built at. Unlike
		/// <see cref="supersampleScale"/> this survives <see cref="EndFullFrameCapture"/>, because
		/// <see cref="DownsampleAndBlitFullFrame"/> runs after it and has to filter over the block size the
		/// target was actually rendered at.
		/// </summary>
		private int captureSupersampleScale = MaxSupersampleScale;

		private IGpuTexture capturedColorTarget;
		private IGpuTexture capturedDepthTarget;

		/// <summary>
		/// Where the capture that is currently open was started from, in DEBUG builds only; null in
		/// release. Carried purely so the "already in progress" throw can name the frame that left the
		/// capture open - the reports of it are top level paints, so telling a genuinely nested paint
		/// apart from a capture stranded by an earlier frame is otherwise guesswork.
		/// </summary>
		private string captureOpenedAt;

		/// <summary>
		/// True between a capture that opened successfully and the downsample that spends it. Callers pair
		/// Begin and End in a finally, so a Begin that threw is still followed by an End *and a blit* -
		/// and the capture target from an earlier frame is still lying there for that blit to composite.
		/// This is what keeps the failed frame from being painted over with the previous one's 3D content.
		/// </summary>
		private bool blitPending;

#if DEBUG
		/// <summary>
		/// Whether <see cref="BeginFullFrameCapture"/> records where it was called from, for the
		/// "already in progress" message. Off unless <c>AGG_CAPTURE_TRACE</c> is set, because capturing it
		/// is a full stack walk and captures run several times a frame; DEBUG only, and settable so a test
		/// can exercise the diagnostic without the environment.
		/// </summary>
		internal static bool CaptureTraceEnabled { get; set; }
			= Environment.GetEnvironmentVariable("AGG_CAPTURE_TRACE") != null;
#endif
		private IGpuTexture sampleFrameColor;
		private IGpuTexture sampleFrameDepth;

		private IGpuBuffer lightUniform;
		private IGpuBuffer outlineUniform;
		private IGpuBuffer downsampleUniform;
		private ISampler linearSampler;
		private ISampler meshTextureSampler;
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

		/// <summary>
		/// The frame targets, kept one set per viewport size rather than one set full stop.
		/// <para>
		/// One renderer serves every 3D view in the window, and MatterCAD draws more than one per frame -
		/// a full size part view and, say, a 300x300 preview. Sizing a single set from "the current
		/// viewport" made every view rebuild all thirteen textures the previous view had just built: at a
		/// 3213x1395 main view that is a quarter of a gigabyte of render targets created and destroyed
		/// twice per frame, and - because the peel bindings sample the scene depth target - it also
		/// invalidated and re-created every one of the ~800 cached mesh bind groups each frame. Measured
		/// at 82 ms/frame with it, 8 ms without.
		/// </para>
		/// </summary>
		private readonly Dictionary<(int Width, int Height), SceneFrameTargets> frameTargetSets
			= new Dictionary<(int Width, int Height), SceneFrameTargets>();

		/// <summary>
		/// How many sizes' worth of frame targets to keep. Two covers the main view plus one preview,
		/// which is the shape that hurts; the third is slack for a window mid-resize.
		/// </summary>
		private const int MaxFrameTargetSets = 3;

		/// <summary>Monotonic use stamp, so the least recently drawn size is the one evicted.</summary>
		private int frameTargetUseStamp;

		/// <summary>
		/// <c>AGG_FRAME_MESH_LOG=1</c>: names every mesh whose GPU buffers are minted, which is how a mesh
		/// that is rebuilt every frame is found. Separate from the frame profile because it is loud.
		/// </summary>
		private static readonly bool LogMeshBufferCreation
			= Environment.GetEnvironmentVariable("AGG_FRAME_MESH_LOG") == "1";

		private int depthPeelingLayers = 6;

		private BedRenderCommand queuedBedCommand;

		// The bed's mesh command, minted once per frame. BedRenderCommand.CreateSceneCommand builds a fresh
		// object every call, and the bed is drawn from four sites (the depth prepass, both transparency
		// modes, and each peel pass) - so calling it per site handed the draw-setup cache a new key every
		// time and the bed alone burned a slot and a pair of plugin lookups per pass.
		private MeshRenderCommand bedSceneCommand;
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
			this.uniforms = new StagedUniformBuffers(
				this.device,
				UniformSlotStride,
				UniformSlotsPerBuffer,
				"Scene.UniformSlotCreate");

			// Every submit - this renderer's own end-of-frame one, the mid-frame ones its bed shadow and
			// downsample passes make, and any the 2D layer makes while the scene has draws staged - has to
			// carry the staged uniforms with it.
			this.compat.BeforeSubmit += this.FlushUniformWrites;
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
		/// Caps the size of the vertex buffers mesh geometry is uploaded in, in bytes. Null - the default -
		/// uses the device's own <see cref="DeviceLimits.MaxBufferSize"/>.
		/// </summary>
		/// <remarks>
		/// A test hook, and deliberately narrower than the device limit it stands in for. Only mesh vertex
		/// data is chunked, so lowering the device's own limit far enough to split the couple of kilobytes a
		/// golden scene's cube occupies would also refuse the compat layer's uniform and immediate-mode
		/// buffers, which have no chunking and are never within orders of magnitude of the real limit.
		/// Splitting a scene the goldens already pin, at a limit only this path sees, is what proves the
		/// chunking is invisible.
		/// </remarks>
		public ulong? MaxMeshVertexBufferBytes { get; set; }

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
			this.bedSceneCommand = null;
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
				using (FrameProfiler.Time("SceneRenderTotal"))
				{
					this.RenderQueuedSceneEffects();
				}
			}
			finally
			{
				this.ClearQueuedSceneEffects();
				this.activeSceneRenderContext = null;
			}
		}

		/// <summary>
		/// The largest supersample factor a <paramref name="width"/> x <paramref name="height"/> target can be
		/// captured at without asking the device for a texture larger than it will make: the biggest scale in
		/// <see cref="MaxSupersampleScale"/>..1 whose product fits <paramref name="maxTextureDimension"/> in
		/// both axes.
		/// <para>
		/// Never returns less than 1. At 1 supersampling is effectively off and the frame is softer, which is
		/// the price of the alternative: an over-limit texture is not refused by wgpu-native but handed back
		/// as a non-null error texture, and the invalid view it yields fails validation at the next queue
		/// submit inside Rust, where the panic cannot unwind across the FFI boundary and aborts the process.
		/// A fullscreen retina window - 3024x1898 device pixels - is already past 8192/3.
		/// </para>
		/// </summary>
		/// <param name="width">Target width in device pixels. Non-positive sizes are answered, not thrown at:
		/// a collapsed widget reaches here and the caller clamps the target size afterwards.</param>
		/// <param name="height">Target height in device pixels.</param>
		/// <param name="maxTextureDimension">The device's <c>maxTextureDimension2D</c>.</param>
		internal static int SupersampleScaleFor(int width, int height, uint maxTextureDimension)
		{
			long longestEdge = Math.Max(width, height);
			for (int scale = MaxSupersampleScale; scale > 1; scale--)
			{
				if (longestEdge * scale <= maxTextureDimension)
				{
					return scale;
				}
			}

			return 1;
		}

		/// <summary>
		/// Points every subsequent draw - this renderer's and the compat layer's GL immediate mode alike -
		/// at an off-screen target up to <see cref="MaxSupersampleScale"/> times the caller's in each
		/// dimension.
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
			// Before anything else, including the checks that return or throw: a pending blit belongs to
			// the frame that armed it, and every path out of here leaves this frame with nothing to
			// composite unless it gets all the way through.
			this.blitPending = false;

			if (this.capturedColorTarget != null)
			{
				throw new InvalidOperationException(
					"A full-frame capture is already in progress."
					+ (this.captureOpenedAt == null ? string.Empty : "\nOpened at:\n" + this.captureOpenedAt));
			}

			var destination = this.compat.Passes.ColorTarget;
			if (destination == null)
			{
				if (this.compat.Passes.TargetReleased)
				{
					// The frame's target went away mid-paint (see GlRenderPassScope.TargetReleased). Skipping
					// the capture leaves capturedColorTarget null, so EndFullFrameCapture and the downsample
					// blit no-op too and the 3D content this frame is simply dropped.
					return;
				}

				throw new InvalidOperationException(
					"No render target is set on the compat context, so there is nothing to capture on behalf of.");
			}

			// A clear queued against the caller's target must land on the caller's target. WebGPU clears
			// through a pass load op, so a clear left pending here would be consumed by the first pass on
			// the capture target instead - opening and immediately ending a pass spends it now, which is
			// the order the classic path's immediate glClear gives for free.
			this.compat.Passes.EnsurePassOpen();
			this.compat.FlushPass();

			// Re-evaluated every capture, not once: the window the frame is going to is resized and made
			// fullscreen under the renderer, and the scale that fit the old size would ask for an
			// over-limit texture at the new one. Everything that has to agree on it for this frame - the
			// capture target here, the scene pipeline's own targets in EnsureFrameResources, the compat
			// layer's viewport and scissor scaling, and every pixel width the shaders are handed - reads it
			// back off supersampleScale rather than recomputing it.
			int scale = SupersampleScaleFor(
				(int)destination.Descriptor.Width,
				(int)destination.Descriptor.Height,
				this.device.Limits.MaxTextureDimension2D);

			int width = (int)destination.Descriptor.Width * scale;
			int height = (int)destination.Descriptor.Height * scale;
			this.EnsureSampleFrameTargets(width, height, destination.Descriptor.Format);

			var previousDepth = this.compat.Passes.DepthTarget;
			int previousCoordinateScale = this.compat.CoordinateScale;
			int previousCaptureScale = this.captureSupersampleScale;

			// All or nothing from here: the moment capturedColorTarget is non-null the capture counts as
			// open, and every call site calls Begin outside its try. A throw part way through - a target
			// released under the frame, a pass that will not open - would otherwise leave the capture
			// flagged open forever and turn one bad frame into an exception on every frame after it.
			try
			{
				this.capturedColorTarget = destination;
				this.capturedDepthTarget = previousDepth;
#if DEBUG
				this.captureOpenedAt = CaptureTraceEnabled ? Environment.StackTrace : null;
#endif

				this.compat.SetRenderTarget(this.sampleFrameColor, this.sampleFrameDepth);
				this.compat.CoordinateScale = scale;
				this.supersampleScale = scale;
				this.captureSupersampleScale = scale;

				// Cleared to transparent so only the region the 3D frame actually covers contributes when the
				// downsampled result is alpha-blended back over the caller's target.
				using (this.device.BeginRenderPass(new RenderPassDescriptor(
					new[] { new ColorAttachment(this.sampleFrameColor, LoadOp.Clear, ClearColor.Transparent) },
					new DepthAttachment(this.sampleFrameDepth, LoadOp.Clear, DepthAttachment.FarClear),
					"SupersampleClear")))
				{
				}

				this.blitPending = true;
			}
			catch
			{
				this.blitPending = false;
				this.capturedColorTarget = null;
				this.capturedDepthTarget = null;
				this.captureOpenedAt = null;
				this.supersampleScale = 1;
				this.captureSupersampleScale = previousCaptureScale;
				this.compat.CoordinateScale = previousCoordinateScale;

				try
				{
					this.compat.SetRenderTarget(destination, previousDepth);
				}
				catch (Exception)
				{
					// Pointing the compat layer back is best effort; the original failure is the one worth
					// reporting, and the state above is already unwound either way.
				}

				throw;
			}
		}

		/// <summary>Points drawing back at the target <see cref="BeginFullFrameCapture"/> took over.</summary>
		public void EndFullFrameCapture()
		{
			if (this.capturedColorTarget == null)
			{
				return;
			}

			var restoreColor = this.capturedColorTarget;
			var restoreDepth = this.capturedDepthTarget;

			// Cleared in a finally, not after the restore: SetRenderTarget ends the open pass, and if that
			// throws (a target released mid frame) the capture would otherwise stay flagged open and every
			// later frame would throw out of BeginFullFrameCapture instead.
			try
			{
				this.compat.SetRenderTarget(restoreColor, restoreDepth);
			}
			finally
			{
				this.compat.CoordinateScale = 1;
				this.supersampleScale = 1;
				this.capturedColorTarget = null;
				this.capturedDepthTarget = null;
				this.captureOpenedAt = null;

				// SetTargets ends the open pass before it reassigns, so a throw from the pass end above left
				// the compat layer still pointing at the capture target - the rest of this frame would draw
				// into a texture nothing presents. The retry cannot fail the same way (the pass has been
				// forgotten either way), and if it fails for some other reason the frame is lost regardless.
				if (!ReferenceEquals(this.compat.Passes.ColorTarget, restoreColor))
				{
					try
					{
						this.compat.SetRenderTarget(restoreColor, restoreDepth);
					}
					catch (Exception)
					{
						// Best effort: the original failure is the one worth reporting.
					}
				}
			}
		}

		/// <summary>
		/// Box-downsamples the capture target onto the caller's target with the 9-tap filter, completing
		/// the frame. Call after <see cref="EndFullFrameCapture"/>.
		/// </summary>
		public void DownsampleAndBlitFullFrame()
		{
			// Not just "is there a capture target": the targets are kept between frames, so without this a
			// frame whose BeginFullFrameCapture threw would composite the previous frame's 3D content over
			// itself from the finally that pairs with the failed Begin.
			if (!this.blitPending)
			{
				return;
			}

			this.blitPending = false;

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
			this.compat.BeforeSubmit -= this.FlushUniformWrites;
			this.uniforms.Dispose();

			foreach (var slot in this.retainedMeshBuffers)
			{
				slot.RetireInto(this.retiredMeshBuffers);

				// The slot table is keyed weakly, so a live mesh's slot outlives this renderer. Clearing the
				// retained flag (as the sweep and release paths do) is what lets that slot be retained again
				// by another renderer - leave it set and its next buffers are never tracked, so never freed.
				slot.Owner = null;
				slot.IsRetained = false;
			}

			this.DisposeRetiredMeshBuffers();

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
			this.meshTextureSampler?.Dispose();
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
			this.meshTextureSampler = null;
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
				if (this.compat.Passes.TargetReleased)
				{
					// Same as in BeginFullFrameCapture: the destination disappeared while the frame was being
					// built, so this frame's scene is dropped rather than thrown over. EndSceneRendering
					// clears the queue either way.
					return;
				}

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

			// Safe together: the previous frame ended in a submit, which flushed every slot it staged.
			this.drawSlot = 0;
			this.uniforms.Reset();
			this.WriteLightUniform();

			// Before the plan is built, because the shadow mask rasterizes the queued scene commands from
			// above the bed and has nothing to do with the frame's opaque/transparent split.
			using (FrameProfiler.Time("Scene.BedShadow"))
			{
				this.RenderBedShadowTexture(this.queuedBedCommand);
			}

			var renderPlan = this.renderPlanner.Build(this.queuedSceneCommands);
			FrameProfiler.Count("Scene.OpaqueCmds", renderPlan.OpaqueCommands.Count);
			FrameProfiler.Count("Scene.TransparentCmds", renderPlan.TransparentCommands.Count);

			using (FrameProfiler.Time("Scene.Opaque"))
			{
				this.RenderOpaqueCommands(renderPlan.OpaqueCommands);
			}

			using (FrameProfiler.Time("Scene.Depth"))
			{
				this.RenderSceneDepth(renderPlan);
			}

			using (FrameProfiler.Time("Scene.Transparent"))
			{
				this.RenderTransparentLayers(renderPlan.TransparentCommands);
			}

			using (FrameProfiler.Time("Scene.Overlays"))
			{
				this.RenderTransparentOverlays();
			}

			using (FrameProfiler.Time("Scene.Composite"))
			{
				this.CompositeSceneTargets();
				this.BlitResolvedSceneToTarget(destination);
			}

			using (FrameProfiler.Time("Scene.SelectionOutlines"))
			{
				this.RenderSelectionOutlines(destination);
			}

			FrameProfiler.Count("Scene.DrawSlots", this.drawSlot);
			FrameProfiler.Count("Scene.UniformPoolSize", this.uniforms.SlotCapacity);

			// Meshes the session has finished with give their buffers back here, one frame boundary after
			// the collector took them.
			this.SweepCollectedMeshBuffers();

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
						this.BedSceneCommand,
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
		/// The queued bed as a mesh command, minted on first use and reused for the rest of the frame so
		/// every pass that draws the bed presents the draw-setup cache the same command instance.
		/// </summary>
		private MeshRenderCommand BedSceneCommand
			=> this.bedSceneCommand ??= this.queuedBedCommand.CreateSceneCommand();

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
		/// Draws the frame's transparency in whichever of the two modes the user setting asks for: dual
		/// depth peeling, or the sorted alpha-blend approximation.
		/// </summary>
		/// <remarks>
		/// The peel is the classic path's loop one for one (<c>VorticeD3DGl.RenderTransparentLayers</c>),
		/// except that each of its iterations is three passes here: the depth range it kept in a MAX-blended
		/// Rg32Float target is kept in two hardware depth buffers instead, and a depth attachment cannot be
		/// written by the same pass that writes colour. See the peel section of NodeDesignerScene.wgsl for
		/// why the two formulations compute the same numbers.
		/// </remarks>
		/// <param name="transparentCommands">The plan's transparent half, in queue order - the peel is
		/// order independent, which is the entire reason it exists; the alpha-blend mode sorts it.</param>
		private void RenderTransparentLayers(IReadOnlyList<MeshRenderCommand> transparentCommands)
		{
			if (SceneTransparencyModeUtilities.GetSceneTransparencyMode(this.DepthPeelingLayers)
				!= SceneTransparencyMode.DualDepthPeeling)
			{
				this.RenderTransparentAlphaBlend(transparentCommands);
				return;
			}

			this.ClearTransparencyTargets();

			if (transparentCommands.Count == 0 && !this.IsBedDrawable)
			{
				return;
			}

			using (FrameProfiler.Time("Peel.Init"))
			{
				this.InitializeDualDepthPeel(transparentCommands);
			}

			var source = this.peelRangeA;
			var destination = this.peelRangeB;
			int iterationCount = DualDepthPeelingMath.GetIterationCount(this.DepthPeelingLayers);
			FrameProfiler.Count("Scene.PeelIterations", iterationCount);
			for (int iteration = 0; iteration < iterationCount; iteration++)
			{
				using (FrameProfiler.Time("Peel.Depth"))
				{
					this.PeelDepthRangePass(transparentCommands, source, destination.Near, CompareFunction.Less, DepthAttachment.FarClear, "PeelNear");
					this.PeelDepthRangePass(transparentCommands, source, destination.Far, CompareFunction.Greater, 0, "PeelFar");
				}

				using (FrameProfiler.Time("Peel.Color"))
				{
					this.PeelColorPass(transparentCommands, source);
				}

				(source, destination) = (destination, source);
			}
		}

		/// <summary>
		/// The other transparency mode: the classic path's sorted alpha-blend approximation
		/// (<c>VorticeD3DGl.RenderTransparentAlphaBlend</c>), used when the user turns depth peeling off.
		/// </summary>
		/// <remarks>
		/// No peel, no accumulation targets: the transparent commands are sorted back to front by view-space
		/// centre and blended straight into the scene colour target, each one twice - back faces first (cull
		/// front), then front faces - so a single hollow object still looks solid. Depth is tested against
		/// the opaque scene but never written, which is what lets the sorted draws blend with each other.
		/// It is an approximation and it is meant to be: it is cheap, and per-object sorting gets the
		/// ordering wrong wherever two transparent objects interpenetrate.
		/// <para>
		/// The classic path also clears the peel accumulation targets here. They are not read in this mode
		/// (its resolve is <see cref="CompositeSceneTargetsAlphaBlend"/>, which never samples them), so the
		/// two clear passes are simply not run.
		/// </para>
		/// </remarks>
		/// <param name="transparentCommands">The plan's transparent half, unsorted.</param>
		private void RenderTransparentAlphaBlend(IReadOnlyList<MeshRenderCommand> transparentCommands)
		{
			var sorted = SceneTransparencyModeUtilities.SortTransparentCommandsBackToFront(
				transparentCommands,
				this.activeSceneRenderContext.WorldView.ModelviewMatrix);

			bool bedAfterObjects = this.IsBedDrawable
				&& SceneTransparencyModeUtilities.ShouldRenderBedAfterTransparentObjects(
					this.BedSceneCommand.Transform,
					this.activeSceneRenderContext.WorldView.EyePosition);

			// LoadOp.Load on both attachments: the opaque pass already filled this target and its depth, and
			// the transparent draws blend over the one and test against the other.
			using (var encoder = this.device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(this.sceneColorTarget.Color, LoadOp.Load) },
				new DepthAttachment(this.sceneColorTarget.Depth, LoadOp.Load),
				"SceneAlphaBlend")))
			{
				if (this.IsBedDrawable && !bedAfterObjects)
				{
					this.DrawAlphaBlendBed(encoder);
				}

				foreach (var command in sorted)
				{
					if (!SceneRenderModeUtilities.RequiresSceneMeshPass(command.RenderType)
						|| !SceneRenderModeUtilities.ShouldRenderTransparentFill(command.RenderType))
					{
						continue;
					}

					var state = this.AlphaBlendDrawState();
					state.CullOverride = CullMode.Front;
					this.DrawMeshCommand(encoder, command, state);

					state.CullOverride = CullMode.Back;
					state.EnableWireframe = SceneRenderModeUtilities.ShouldDrawWireframeOverlay(command.RenderType);
					this.DrawMeshCommand(encoder, command, state);
				}

				if (bedAfterObjects)
				{
					this.DrawAlphaBlendBed(encoder);
				}
			}
		}

		/// <summary>The alpha-blend mode's shared per-draw state: source-over blending into the scene colour
		/// target, depth tested but not written.</summary>
		private MeshDrawState AlphaBlendDrawState()
		{
			return new MeshDrawState
			{
				ColorFormat = SceneColorFormat,
				DepthFormat = SceneDepthFormat,
				NoDepthWrite = true,
				BlendEnabled = true,
				Blend = BlendComponent.AlphaBlend,
				AlphaBlendShading = true,
			};
		}

		/// <summary>Draws the bed into the alpha-blend transparency pass, culled as the bed command asks
		/// (the classic path passes no cull override for it) and with its analytic grid switched on.</summary>
		/// <param name="encoder">The open alpha-blend pass.</param>
		private void DrawAlphaBlendBed(IRenderEncoder encoder)
		{
			var state = this.AlphaBlendDrawState();
			state.ForcedTexture = this.bedCompositeTarget;
			state.Unlit = true;
			state.BedGrid = this.queuedBedCommand;

			this.DrawMeshCommand(encoder, this.BedSceneCommand, state);
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

					this.DrawMeshCommand(encoder, this.BedSceneCommand, bedState);
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
			if (!SceneTransparencyModeUtilities.ShouldUseDualDepthPeelResolve(this.DepthPeelingLayers))
			{
				this.CompositeSceneTargetsAlphaBlend();
				return;
			}

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
		/// The alpha-blend transparency mode's resolve (the classic <c>CompositeSceneTargetsAlphaBlend</c>):
		/// no peel accumulators to unmix, so the scene colour target - which already has the sorted
		/// transparent draws blended into it - is copied over and the overlay laid on top.
		/// </summary>
		private void CompositeSceneTargetsAlphaBlend()
		{
			var layout = new[]
			{
				new BindGroupLayoutEntry(0, 0, ShaderStage.Fragment, BindingType.Sampler),
				new BindGroupLayoutEntry(0, 1, ShaderStage.Fragment, BindingType.Texture),
			};

			var copyPipeline = this.GetFullscreenPipeline(
				SceneShaderKeys.CopyTextureEntryPoint,
				new ColorTargetState(SceneColorFormat),
				layout,
				"SceneResolveCopy");

			var overlayPipeline = this.GetFullscreenPipeline(
				SceneShaderKeys.CopyTextureEntryPoint,
				new ColorTargetState(
					SceneColorFormat,
					true,
					BlendComponent.AlphaBlend,
					new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha)),
				layout,
				"SceneResolveOverlay");

			var sceneBindGroup = this.cache.GetBindGroup(new BindGroupDescriptor(
				copyPipeline,
				0,
				new[]
				{
					BindGroupEntry.ForSampler(0, this.pointSampler),
					BindGroupEntry.ForTexture(1, this.sceneColorTarget.Color),
				},
				"SceneResolveCopy"));

			var overlayBindGroup = this.cache.GetBindGroup(new BindGroupDescriptor(
				overlayPipeline,
				0,
				new[]
				{
					BindGroupEntry.ForSampler(0, this.pointSampler),
					BindGroupEntry.ForTexture(1, this.transparentOverlayTarget),
				},
				"SceneResolveOverlay"));

			using (var encoder = this.device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(this.resolvedSceneTarget, LoadOp.Clear, ClearColor.Transparent) },
				DepthAttachment.None,
				"SceneResolveAlphaBlend")))
			{
				encoder.SetPipeline(copyPipeline);
				encoder.SetBindGroup(0, sceneBindGroup);
				encoder.Draw(3);

				encoder.SetPipeline(overlayPipeline);
				encoder.SetBindGroup(0, overlayBindGroup);
				encoder.Draw(3);
			}
		}

		/// <summary>
		/// Blits the resolved scene into the caller's target, over whatever was already there.
		/// </summary>
		/// <remarks>
		/// The source factor follows the transparency mode, as the classic path's two blit blend states do:
		/// the peel resolve hands back straight (un-premultiplied) alpha and blends source-over, while the
		/// alpha-blend resolve's target already holds premultiplied colour (everything drawn into it was
		/// blended with SrcAlpha) and must not be multiplied by alpha a second time.
		/// </remarks>
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
				SceneTransparencyModeUtilities.ShouldUseDualDepthPeelResolve(this.DepthPeelingLayers)
					? BlendComponent.AlphaBlend
					: new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha),
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

			this.EnsureBedTargets(bedCommand.ShadowMapSize);
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

		/// <summary>
		/// Creates the square bed intermediates at the requested shadow resolution, capped.
		/// </summary>
		/// <remarks>
		/// Sized from <see cref="BedRenderCommand.ShadowMapSize"/> rather than from the base texture:
		/// the base texture carries only the bed's flat fill colour, so it is typically a few texels
		/// and would starve the shadow if it drove this.
		/// </remarks>
		/// <param name="shadowMapSize">Requested edge length in pixels.</param>
		private void EnsureBedTargets(int shadowMapSize)
		{
			int shadowSize = Math.Clamp(shadowMapSize, 1, BedTextureSizeLimit);

			if (this.bedShadowMaskTarget != null
				&& this.bedShadowMaskTarget.Descriptor.Width == (uint)shadowSize
				&& this.bedShadowMaskTarget.Descriptor.Height == (uint)shadowSize)
			{
				return;
			}

			this.DisposeBedTargets();

			this.bedShadowMaskTarget = this.CreateColorTarget(shadowSize, shadowSize, "bedShadowMask");
			this.bedShadowBlurTargetA = this.CreateColorTarget(shadowSize, shadowSize, "bedShadowBlurA");
			this.bedShadowBlurTargetB = this.CreateColorTarget(shadowSize, shadowSize, "bedShadowBlurB");
			this.bedCompositeTarget = this.CreateColorTarget(shadowSize, shadowSize, "bedComposite");

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
			var setupKey = new MeshDrawSetupKey(command, drawState);
			if (!this.meshDrawSetups.TryGetValue(setupKey, out var setup))
			{
				using (FrameProfiler.Time("Scene.DrawSetup"))
				{
					var world = this.activeSceneRenderContext.WorldView;
					this.WriteTransformUniform(command.Transform * world.ModelviewMatrix, world.ProjectionMatrix);

					var resolvedMeshPlugin = MeshTrianglePlugin.Get(this.OwnerGl, command.Mesh);
					var resolvedShaderData = SceneEdgeShaderDataPlugin.Get(this.OwnerGl, command.Mesh, command.RenderType);

					// Asked of the interleaved data rather than of Mesh.FaceColors, which is what the classic path
					// asks. The two agree for face-coloured meshes, and only this form also catches the colours
					// RenderTypes.Overhang bakes into the same channel from a normal-driven colour function - there
					// is no FaceColors array behind those.
					bool useVertexColor = !command.OverrideFaceColors && HasVertexColors(resolvedShaderData);

					this.WriteEffectUniform(
						command.Color,
						command.WireFrameColor,
						drawState.EnableWireframe,
						drawState.WireframeOnly,
						command.Unlit || drawState.Unlit,
						useVertexColor,
						command.AlphaMultiplier,
						drawState.BedGrid);

					// StageSlot writes into the slot drawSlot currently names, so the counter only moves once
					// both blocks are staged - and only on a miss, which is what keeps the peel's repeat draws
					// pointing at the one slot this setup owns.
					setup = new MeshDrawSetup(this.drawSlot++, resolvedMeshPlugin, resolvedShaderData);
					this.meshDrawSetups.Add(setupKey, setup);
				}
			}

			// Held for the whole frame, including across the mesh plugins' own rebuild-on-change check. That
			// is the assumption this renderer already makes everywhere: the queued commands are a snapshot,
			// and a mesh edited between the depth prepass and the peel passes that draw it would desync the
			// two regardless of what is cached here.
			var meshPlugin = setup.MeshPlugin;
			var sceneShaderData = setup.SceneShaderData;
			int uniformSlot = setup.UniformSlot;

			var cullMode = drawState.CullOverride ?? (command.ForceCullBackFaces ? CullMode.Back : CullMode.None);

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
				IBindGroup bindGroup;
				using (FrameProfiler.Time("Scene.BindGroupGet"))
				{
					bindGroup = this.cache.GetBindGroup(new BindGroupDescriptor(
						pipeline,
						0,
						this.BuildMeshBindings(drawState, uniformSlot, texture ?? this.whiteTexture),
						"SceneMesh"));
				}

				var vertexBuffers = this.EnsureMeshBuffers(command.Mesh, command.RenderType, sceneShaderData, sceneSubMesh);

				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);

				// One draw per chunk. A submesh whose interleaved data is larger than the device will create
				// in one buffer lives in several, split on triangle boundaries - so drawing all of them in
				// order is the same triangle list, in the same order, as the single draw it used to be.
				foreach (var vertexBuffer in vertexBuffers)
				{
					encoder.SetVertexBuffer(0, vertexBuffer);
					encoder.Draw((int)(vertexBuffer.SizeInBytes / SceneVertexStride));
				}
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

			int uniformSlot = this.drawSlot++;
			var slotBuffer = this.uniforms.BufferFor(uniformSlot);
			ulong slotOffset = this.uniforms.OffsetFor(uniformSlot);

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
					BindGroupEntry.ForBuffer(0, slotBuffer, slotOffset, TransformUniformSize),
					BindGroupEntry.ForBuffer(1, this.lightUniform),
					BindGroupEntry.ForBuffer(2, slotBuffer, slotOffset + EffectBlockOffset, EffectUniformSize),
					BindGroupEntry.ForSampler(3, this.meshTextureSampler),
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

				var buffers = this.EnsureSelectionBuffers(mesh, meshPlugin, subMesh);
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);

				// One draw per chunk, for the reason DrawMeshCommand gives.
				foreach (var buffer in buffers)
				{
					encoder.SetVertexBuffer(0, buffer);
					encoder.Draw((int)(buffer.SizeInBytes / SelectionVertexStride));
				}
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
		/// The vertex buffers for one submesh of a mesh's scene render data, minted on first use and
		/// retained on the submesh itself afterwards. Normally one; more when the submesh is larger than
		/// the device will create in a single buffer.
		/// </summary>
		/// <param name="mesh">The mesh being drawn, which is what retention is keyed on.</param>
		/// <param name="renderType">The render type whose plugin generation owns these buffers.</param>
		/// <param name="owner">The plugin instance the submesh came from. A different instance than the
		/// slot last saw means the mesh was edited, and the previous generation's buffers are retired.</param>
		/// <param name="sceneSubMesh">The submesh whose interleaved data the buffers hold.</param>
		private IReadOnlyList<IGpuBuffer> EnsureMeshBuffers(
			Mesh mesh,
			RenderTypes renderType,
			SceneEdgeShaderDataPlugin owner,
			SceneEdgeShaderSubMeshData sceneSubMesh)
		{
			var slot = this.GetMeshBufferSlot(mesh, (int)renderType, owner);
			if (sceneSubMesh.CachedGpuBufferChunks is IReadOnlyList<IGpuBuffer> cached)
			{
				return cached;
			}

			var interleavedData = sceneSubMesh.InterleavedData;
			if (LogMeshBufferCreation)
			{
				// A mesh whose buffers are minted every frame is a mesh something is rebuilding every
				// frame; the identity and face count are what make it findable. Behind its own switch
				// because in a scene that does it, this prints a hundred lines a frame.
				Console.WriteLine(
					$"[mesh] buffers for mesh#{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(mesh)}"
					+ $" faces {mesh.Faces.Count} changed {mesh.ChangedCount} type {renderType}"
					+ $" (frame {FrameProfiler.FrameCount})");
			}

			var chunks = this.CreateVertexChunks(
				interleavedData.Length / SceneVertexFloatStride,
				SceneVertexStride,
				(destination, firstVertex, byteCount) =>
					Buffer.BlockCopy(interleavedData, firstVertex * SceneVertexStride, destination, 0, byteCount));

			sceneSubMesh.CachedGpuBufferChunks = chunks;
			slot.Add(chunks, () => sceneSubMesh.CachedGpuBufferChunks = null);
			return chunks;
		}

		/// <summary>
		/// The position-only vertex buffers the selection mask draws, minted on first use and retained on
		/// the submesh afterwards. Chunked on the same rule as the scene data.
		/// </summary>
		/// <param name="mesh">The mesh being masked.</param>
		/// <param name="owner">The triangle plugin the submesh came from; a new instance retires the old
		/// generation's buffers exactly as it does for the scene data.</param>
		/// <param name="subMesh">The submesh whose positions the buffers hold.</param>
		private IReadOnlyList<IGpuBuffer> EnsureSelectionBuffers(Mesh mesh, MeshTrianglePlugin owner, SubTriangleMesh subMesh)
		{
			var slot = this.GetMeshBufferSlot(mesh, SelectionBufferSlot, owner);
			if (subMesh.CachedSelectionGpuBuffer is IReadOnlyList<IGpuBuffer> cached)
			{
				return cached;
			}

			var positions = subMesh.positionData;
			var chunks = this.CreateVertexChunks(
				positions.Count,
				SelectionVertexStride,
				(destination, firstVertex, byteCount) =>
				{
					var span = destination.AsSpan();
					int chunkVertexCount = byteCount / SelectionVertexStride;
					for (int index = 0; index < chunkVertexCount; index++)
					{
						var position = positions.Array[firstVertex + index];
						int offset = index * SelectionVertexStride;
						WriteFloat(span, offset + 0, position.positionX);
						WriteFloat(span, offset + 4, position.positionY);
						WriteFloat(span, offset + 8, position.positionZ);
					}
				});

			subMesh.CachedSelectionGpuBuffer = chunks;
			slot.Add(chunks, () => subMesh.CachedSelectionGpuBuffer = null);
			return chunks;
		}

		/// <summary>
		/// Uploads vertex data as one buffer per chunk, each within the device's
		/// <see cref="DeviceLimits.MaxBufferSize"/> and each holding whole triangles.
		/// </summary>
		/// <remarks>
		/// The scratch buffer is one chunk rather than the whole submesh: a mesh big enough to need
		/// splitting is exactly the mesh whose full byte copy the process cannot afford twice.
		/// </remarks>
		/// <param name="vertexCount">Total vertices to upload.</param>
		/// <param name="vertexStride">Bytes per vertex.</param>
		/// <param name="fillChunk">Fills one chunk: the destination, the chunk's first vertex, and how many
		/// bytes of the destination it covers.</param>
		private IReadOnlyList<IGpuBuffer> CreateVertexChunks(
			int vertexCount,
			int vertexStride,
			Action<byte[], int, int> fillChunk)
		{
			int verticesPerChunk = this.MaxVerticesPerChunk(vertexStride);
			var chunks = new List<IGpuBuffer>();
			byte[] scratch = null;

			for (int firstVertex = 0; firstVertex < vertexCount; firstVertex += verticesPerChunk)
			{
				int chunkVertexCount = Math.Min(verticesPerChunk, vertexCount - firstVertex);
				int chunkByteCount = chunkVertexCount * vertexStride;
				if (scratch == null || scratch.Length != chunkByteCount)
				{
					scratch = new byte[chunkByteCount];
				}

				fillChunk(scratch, firstVertex, chunkByteCount);
				FrameProfiler.Count("Scene.VertexChunkCreate");
				this.VertexBufferCreateCount++;
				chunks.Add(this.device.CreateBuffer(BufferUsage.Vertex, (ulong)chunkByteCount, scratch));
			}

			return chunks;
		}

		/// <summary>
		/// How many vertices of the given stride go in one buffer: as many as the device's buffer limit
		/// allows, rounded down to whole triangles.
		/// </summary>
		/// <param name="vertexStride">Bytes per vertex.</param>
		/// <exception cref="InvalidOperationException">The limit cannot hold even one triangle.</exception>
		private int MaxVerticesPerChunk(int vertexStride)
		{
			ulong limit = this.MaxMeshVertexBufferBytes ?? this.device.Limits.MaxBufferSize;
			ulong primitiveByteCount = (ulong)(vertexStride * VerticesPerPrimitive);
			ulong primitivesPerChunk = limit / primitiveByteCount;
			if (primitivesPerChunk == 0)
			{
				throw new InvalidOperationException(
					$"This device's maxBufferSize of {limit:N0} bytes cannot hold a single {primitiveByteCount}"
					+ " byte triangle, so mesh geometry cannot be uploaded at all.");
			}

			return (int)Math.Min(primitivesPerChunk * VerticesPerPrimitive, int.MaxValue);
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
				slot = new MeshBufferSlot(mesh) { Owner = owner };
				slotsForMesh[slotKey] = slot;
				this.Retain(slot);
			}
			else
			{
				if (!ReferenceEquals(slot.Owner, owner))
				{
					slot.RetireInto(this.retiredMeshBuffers);
					slot.Owner = owner;
				}

				// A release (or, for a mesh that came back from the dead between the sweep and now, a sweep)
				// took the slot off the retention list. It is about to mint buffers again, so it goes back.
				if (!slot.IsRetained)
				{
					this.Retain(slot);
				}
			}

			return slot;
		}

		/// <summary>Puts a slot on the strong list that owns its buffers.</summary>
		private void Retain(MeshBufferSlot slot)
		{
			this.retainedMeshBuffers.Add(slot);
			slot.IsRetained = true;
		}

		/// <summary>
		/// Retires the buffers of every slot whose mesh has been collected. Called just before the frame's
		/// submit, so the retirement list is drained by the same submit that makes it safe to.
		/// </summary>
		/// <remarks>
		/// The slot table is keyed weakly, but the strong list is not: without this sweep a session's every
		/// mesh keeps its GPU buffers and - through the slot's Owner - its plugin's interleaved vertex data
		/// alive for the life of the renderer, which for the viewport is the life of the process.
		/// </remarks>
		private void SweepCollectedMeshBuffers()
		{
			for (int index = this.retainedMeshBuffers.Count - 1; index >= 0; index--)
			{
				var slot = this.retainedMeshBuffers[index];
				if (slot.Mesh.TryGetTarget(out _))
				{
					continue;
				}

				slot.RetireInto(this.retiredMeshBuffers);
				slot.Owner = null;
				slot.IsRetained = false;
				this.retainedMeshBuffers.RemoveAt(index);
			}
		}

		/// <summary>
		/// Releases every mesh vertex buffer this renderer is holding, along with the plugin generations
		/// they came from. For one-shot rendering - a thumbnail draws a mesh once and will never draw it
		/// again, so caching its buffers only pins the mesh's render data until something else evicts it.
		/// </summary>
		/// <remarks>
		/// Submits first: a buffer may only be released once no unsubmitted draw can still reference it,
		/// which is the same retire-then-drain order the per-frame path uses.
		/// </remarks>
		public void ReleaseAllMeshBuffers()
		{
			foreach (var slot in this.retainedMeshBuffers)
			{
				slot.RetireInto(this.retiredMeshBuffers);
				slot.Owner = null;
				slot.IsRetained = false;
			}

			this.retainedMeshBuffers.Clear();

			this.compat.Submit();
			this.DisposeRetiredMeshBuffers();
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

		/// <summary>
		/// The mesh layout plus the blurred bed shadow, for the sorted alpha-blend transparency mode: its
		/// textured entry point runs the analytic grid, which samples that texture before it checks whether
		/// the grid is switched on at all (the sample has to stay in uniform control flow for fwidth).
		/// </summary>
		private static readonly BindGroupLayoutEntry[] SceneBedBindGroupLayout = AppendEntries(
			SceneBindGroupLayout,
			new BindGroupLayoutEntry(0, 8, ShaderStage.Fragment, BindingType.Texture));

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
			int uniformSlot,
			IGpuTexture texture)
		{
			var slotBuffer = this.uniforms.BufferFor(uniformSlot);
			ulong slotOffset = this.uniforms.OffsetFor(uniformSlot);

			var bindings = new List<BindGroupEntry>(8)
			{
				BindGroupEntry.ForBuffer(0, slotBuffer, slotOffset, TransformUniformSize),
				BindGroupEntry.ForBuffer(1, this.lightUniform),
				BindGroupEntry.ForBuffer(2, slotBuffer, slotOffset + EffectBlockOffset, EffectUniformSize),
				BindGroupEntry.ForSampler(3, this.meshTextureSampler),
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
			else if (drawState.AlphaBlendShading)
			{
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
						: useTexture
							? drawState.AlphaBlendShading
								? SceneShaderKeys.SceneBedTextureEntryPoint
								: SceneShaderKeys.SceneTextureEntryPoint
							: SceneShaderKeys.SceneColorEntryPoint;

					// The untextured alpha-blend entry point is sceneColorMain unchanged (the classic
					// SceneColorAlphaBlendPS differs from SceneColorPS only by dropping ApplyDepthPeeling,
					// which is a no-op with peeling off), but it still gets the wider layout so every draw in
					// the pass shares one bind group shape.
					if (drawState.AlphaBlendShading)
					{
						bindGroupLayout = SceneBedBindGroupLayout;
					}

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
				: new DepthStencilState(drawState.DepthFormat, !drawState.NoDepthWrite, drawState.EffectiveDepthCompare);

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
			var span = this.StageSlot(0, TransformUniformSize);
			GlUniformBlock.WriteMatrix(span, 0, modelView);

			// The same 0..w clip depth remap the classic path's UpdateTransformBuffer applies; the WGSL
			// therefore has no z fixup of its own.
			GlUniformBlock.WriteMatrix(span, 64, GlUniformBlock.ToClipSpaceProjection(projection));
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

			var span = this.StageSlot(EffectBlockOffset, EffectUniformSize);
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

		/// <summary>
		/// The 9-tap downsample's tap spacing, in capture-target texture coordinates.
		/// </summary>
		/// <remarks>
		/// One texel apart at the 3x the goldens are captured at, which puts the nine taps on the nine texels
		/// of the source block exactly - the value this used to write unconditionally. At a scale the device
		/// limit forced down it has to shrink with the block: half a texel at 2x (the four corner taps land on
		/// the 2x2 block's texel centres) and zero at 1x, where all nine taps collapse onto the one source
		/// texel and the pass becomes the plain blit it should be. Leaving it at one texel would blur a frame
		/// that was never supersampled in the first place.
		/// </remarks>
		private void WriteDownsampleUniform()
		{
			float tapSpacingInTexels = (this.captureSupersampleScale - 1) * 0.5f;
			var span = this.downsampleScratch.AsSpan();
			GlUniformBlock.WriteVector4(
				span,
				0,
				tapSpacingInTexels / this.sampleFrameColor.Descriptor.Width,
				tapSpacingInTexels / this.sampleFrameColor.Descriptor.Height,
				0,
				0);
			this.device.WriteBuffer(this.downsampleUniform, 0, this.downsampleScratch);
		}

		/// <summary>
		/// The staging bytes of one block of the current draw's slot. Nothing reaches the GPU here:
		/// <see cref="FlushUniformWrites"/> pushes the whole staged range before the submit that consumes
		/// these draws.
		/// <para>
		/// The slot index is the draw counter, so the same draw ordinal gets the same buffer range every
		/// frame - which is what makes the bind groups built around it hit the cache instead of being
		/// minted per draw per frame.
		/// </para>
		/// <para>
		/// The span must be consumed before the next <see cref="StageSlot"/> call: growing the pool
		/// resizes the staging array, and a span handed out earlier would then point into the orphaned
		/// copy and swallow the writes made through it.
		/// </para>
		/// </summary>
		/// <param name="blockOffset">Byte offset of the block within the slot.</param>
		/// <param name="sizeInBytes">Size of the block.</param>
		private Span<byte> StageSlot(int blockOffset, int sizeInBytes)
			=> this.uniforms.Stage(this.drawSlot, blockOffset, sizeInBytes);

		/// <summary>
		/// Pushes every slot staged since the last flush to the GPU. Called from the compat context
		/// immediately before each device submit, which is the only ordering that matters: queue writes are
		/// ordered against the submit, not against the draws recorded before it, so this is exactly
		/// equivalent to the per-draw writes it replaces.
		/// </summary>
		private void FlushUniformWrites() => this.uniforms.Flush(this.drawSlot);

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

			// Mesh face textures get their own sampler, matching the classic path's `defaultSampler` exactly:
			// trilinear and wrapping. Both halves of that mattered, and neither showed until a mipmapped
			// texture was minified in a golden - filtering within one level only made minified faces snap
			// between mip levels, and clamping instead of wrapping changed the pixels along every uv seam.
			// The full-screen bed passes keep the clamped sampler above, which is what the classic path binds
			// for them (`linearClampSampler`).
			this.meshTextureSampler = this.meshTextureSampler
				?? this.device.CreateSampler(new SamplerDescriptor(
					AddressMode.Repeat,
					AddressMode.Repeat,
					FilterMode.Linear,
					FilterMode.Linear,
					FilterMode.Linear,
					"meshTexture"));
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

			var key = (Width: width, Height: height);
			if (!this.frameTargetSets.TryGetValue(key, out var targets))
			{
				FrameProfiler.Count("Scene.TargetSetCreate");

				// Room is made before the new set exists so the peak is one set, not two.
				this.EvictFrameTargetSets(MaxFrameTargetSets - 1);
				targets = this.CreateFrameTargetSet(width, height);
				this.frameTargetSets[key] = targets;
			}

			targets.LastUsedStamp = ++this.frameTargetUseStamp;
			this.ActivateFrameTargets(targets);
		}

		/// <summary>Creates one size's worth of frame targets.</summary>
		private SceneFrameTargets CreateFrameTargetSet(int width, int height)
			=> new SceneFrameTargets
			{
				Width = width,
				Height = height,
				SceneColor = this.CreateSceneTarget(width, height, true, "sceneColor"),
				SceneDepth = this.CreateSceneTarget(width, height, false, "sceneDepth"),
				Selection = this.CreateSceneTarget(width, height, true, "sceneSelection"),
				Resolved = this.CreateColorTarget(width, height, "sceneResolved"),
				TransparentOverlay = this.CreateColorTarget(width, height, "sceneOverlay"),
				FrontAccum = this.CreateColorTarget(width, height, "sceneFrontAccum", TransparencyAccumFormat),
				BackAccum = this.CreateColorTarget(width, height, "sceneBackAccum", TransparencyAccumFormat),
				PeelRangeA = this.CreatePeelRange(width, height, "peelRangeA"),
				PeelRangeB = this.CreatePeelRange(width, height, "peelRangeB"),
			};

		/// <summary>Points the frame's drawing at one size's targets.</summary>
		private void ActivateFrameTargets(SceneFrameTargets targets)
		{
			this.targetWidth = targets.Width;
			this.targetHeight = targets.Height;
			this.sceneColorTarget = targets.SceneColor;
			this.sceneDepthTarget = targets.SceneDepth;
			this.selectionTarget = targets.Selection;
			this.resolvedSceneTarget = targets.Resolved;
			this.transparentOverlayTarget = targets.TransparentOverlay;
			this.frontAccumTarget = targets.FrontAccum;
			this.backAccumTarget = targets.BackAccum;
			this.peelRangeA = targets.PeelRangeA;
			this.peelRangeB = targets.PeelRangeB;
		}

		/// <summary>
		/// Drops least recently used target sets until at most <paramref name="keep"/> remain. A set being
		/// evicted takes its cached bind groups with it, exactly as a resize does.
		/// </summary>
		/// <param name="keep">How many sets may remain.</param>
		private void EvictFrameTargetSets(int keep)
		{
			while (this.frameTargetSets.Count > keep)
			{
				var oldest = default((int Width, int Height));
				int oldestStamp = int.MaxValue;
				foreach (var entry in this.frameTargetSets)
				{
					if (entry.Value.LastUsedStamp < oldestStamp)
					{
						oldestStamp = entry.Value.LastUsedStamp;
						oldest = entry.Key;
					}
				}

				var doomed = this.frameTargetSets[oldest];
				this.frameTargetSets.Remove(oldest);
				this.DisposeFrameTargetSet(doomed);
			}
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

		/// <summary>Releases one size's targets and every bind group that sampled them.</summary>
		private void DisposeFrameTargetSet(SceneFrameTargets targets)
		{
			// Every one of these is sampled by some pass (the composite reads the scene colour and the two
			// accumulators, the peel reads its own depth ranges), so the bind groups built over them are
			// cached against textures that are about to stop existing. A resize would otherwise strand one
			// whole generation of groups per resize in a cache that never evicts.
			this.cache.InvalidateBindGroupsUsing(
				targets.SceneColor?.Color,
				targets.SceneColor?.Depth,
				targets.SceneDepth?.Color,
				targets.SceneDepth?.Depth,
				targets.Selection?.Color,
				targets.Selection?.Depth,
				targets.Resolved,
				targets.TransparentOverlay,
				targets.FrontAccum,
				targets.BackAccum,
				targets.PeelRangeA?.Near,
				targets.PeelRangeA?.Far,
				targets.PeelRangeB?.Near,
				targets.PeelRangeB?.Far);

			targets.SceneColor?.Dispose();
			targets.SceneDepth?.Dispose();
			targets.Selection?.Dispose();
			targets.Resolved?.Dispose();
			targets.TransparentOverlay?.Dispose();
			targets.FrontAccum?.Dispose();
			targets.BackAccum?.Dispose();
			targets.PeelRangeA?.Dispose();
			targets.PeelRangeB?.Dispose();
		}

		/// <summary>Releases every size's frame targets and forgets the active ones.</summary>
		private void DisposeTargets()
		{
			foreach (var targets in this.frameTargetSets.Values)
			{
				this.DisposeFrameTargetSet(targets);
			}

			this.frameTargetSets.Clear();

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

		/// <summary>One viewport size's worth of scene render targets.</summary>
		private sealed class SceneFrameTargets
		{
			public int Width;
			public int Height;
			public SceneTarget SceneColor;
			public SceneTarget SceneDepth;
			public SceneTarget Selection;
			public IGpuTexture Resolved;
			public IGpuTexture TransparentOverlay;
			public IGpuTexture FrontAccum;
			public IGpuTexture BackAccum;
			public PeelDepthRange PeelRangeA;
			public PeelDepthRange PeelRangeB;

			/// <summary>When this set was last drawn into; the smallest stamp is evicted first.</summary>
			public int LastUsedStamp;
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

			// And the draw setup cache is keyed on those commands and holds their render-data plugins, so it
			// roots the same geometry and has to be dropped with them rather than at the next frame's reset.
			this.meshDrawSetups.Clear();

			// Minted from the bed command this frame queued, and keyed on by the cache above, so it belongs
			// to the frame exactly as the entries do.
			this.bedSceneCommand = null;
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

			/// <summary>
			/// Whether <see cref="retainedMeshBuffers"/> is currently holding this slot. A swept or released
			/// slot comes off that list but stays reachable from the weak key table, so the next draw through
			/// it has to put it back before it mints buffers nothing would own.
			/// </summary>
			public bool IsRetained;

			/// <param name="mesh">The mesh this slot's buffers were built from.</param>
			public MeshBufferSlot(Mesh mesh)
			{
				this.Mesh = new WeakReference<Mesh>(mesh);
			}

			/// <summary>
			/// The mesh the slot is keyed on, weakly - the slot is what keeps the plugin generation (and its
			/// multi-megabyte interleaved vertex data) alive, so it must never be what keeps the mesh alive.
			/// A dead target is how the sweep recognises a slot nothing can ever draw through again.
			/// </summary>
			public WeakReference<Mesh> Mesh { get; }

			/// <summary>
			/// The buffers minted for that instance: one per chunk, of every submesh drawn so far.
			/// </summary>
			public List<IGpuBuffer> Buffers { get; } = new List<IGpuBuffer>();

			/// <summary>
			/// Nulls the per-submesh caches that point at <see cref="Buffers"/>. One per submesh rather than
			/// one per buffer - a submesh caches its whole chunk list on one field - because the two kinds of
			/// submesh cache it on unrelated fields of unrelated types.
			/// </summary>
			private List<Action> CacheClears { get; } = new List<Action>();

			/// <summary>Records the buffers of one submesh this slot owns.</summary>
			/// <param name="buffers">The chunks just minted for that submesh.</param>
			/// <param name="clearSubMeshCache">Nulls the submesh field that now caches them.</param>
			public void Add(IReadOnlyList<IGpuBuffer> buffers, Action clearSubMeshCache)
			{
				this.Buffers.AddRange(buffers);
				this.CacheClears.Add(clearSubMeshCache);
			}

			/// <summary>Hands the buffers to the caller's retirement list and empties the slot.</summary>
			/// <remarks>
			/// The submesh caches are cleared here as well. On the mesh-edit path that is redundant (the
			/// edit replaced the submeshes wholesale), but on the release path the submeshes outlive their
			/// buffers, and a cache still pointing at a disposed buffer would be handed to the next draw.
			/// </remarks>
			/// <param name="retired">The list that owns them until the next submit has happened.</param>
			public void RetireInto(List<IGpuBuffer> retired)
			{
				retired.AddRange(this.Buffers);
				this.Buffers.Clear();

				foreach (var clearSubMeshCache in this.CacheClears)
				{
					clearSubMeshCache();
				}

				this.CacheClears.Clear();
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

			/// <summary>
			/// Shades through the alpha-blend transparency mode's entry points: the textured one runs the
			/// analytic bed grid, so every draw in that pass binds the blurred shadow texture.
			/// </summary>
			public bool AlphaBlendShading;

			/// <summary>Tests depth but leaves it alone - the classic path's noDepthWriteState, which is what
			/// lets sorted transparent draws blend with each other.</summary>
			public bool NoDepthWrite;

			/// <summary>Overrides the culling the command asks for - the alpha-blend mode's two-pass
			/// back-faces-then-front-faces draw (the classic <c>cullModeOverride</c>).</summary>
			public CullMode? CullOverride;

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
		/// What identifies one command's uniform blocks within a frame.
		/// <para>
		/// The transform block reads only the command's transform and the frame's camera, which no
		/// <see cref="DrawMeshCommand"/> call varies - the one draw that uses another camera is the bed
		/// shadow, and it goes through <see cref="DrawFlatMask"/>, which is deliberately not memoized. The
		/// effect block reads the command plus exactly the four pass-level flags below: the wireframe pair
		/// (the alpha-blend mode draws one command back faces then front faces), unlit (the bed's peel
		/// draw), and whether the analytic bed grid is on (it is off in the bed's peel init pass and on in
		/// its others). Everything else <see cref="MeshDrawState"/> carries picks a pipeline or a binding,
		/// not a uniform byte.
		/// </para>
		/// <para>
		/// The bed grid is keyed by presence and not by its styling, which is sufficient only because a
		/// frame holds exactly one <see cref="queuedBedCommand"/>: two differently styled
		/// <see cref="BedRenderCommand"/>s in one frame would share a slot and the second would draw with
		/// the first one's grid colours.
		/// </para>
		/// </summary>
		private readonly struct MeshDrawSetupKey : IEquatable<MeshDrawSetupKey>
		{
			private readonly MeshRenderCommand command;
			private readonly bool enableWireframe;
			private readonly bool wireframeOnly;
			private readonly bool unlit;
			private readonly bool bedGrid;

			public MeshDrawSetupKey(MeshRenderCommand command, in MeshDrawState drawState)
			{
				this.command = command;
				this.enableWireframe = drawState.EnableWireframe;
				this.wireframeOnly = drawState.WireframeOnly;
				this.unlit = drawState.Unlit;
				this.bedGrid = drawState.BedGrid != null;
			}

			/// <inheritdoc/>
			public bool Equals(MeshDrawSetupKey other)
				// Reference equality, because a frame's render plan holds the very command objects the
				// passes re-draw; two commands that merely compare equal would still want their own slot.
				=> ReferenceEquals(this.command, other.command)
				&& this.enableWireframe == other.enableWireframe
				&& this.wireframeOnly == other.wireframeOnly
				&& this.unlit == other.unlit
				&& this.bedGrid == other.bedGrid;

			/// <inheritdoc/>
			public override bool Equals(object obj) => obj is MeshDrawSetupKey other && this.Equals(other);

			/// <inheritdoc/>
			public override int GetHashCode()
				=> HashCode.Combine(
					RuntimeHelpers.GetHashCode(this.command),
					this.enableWireframe,
					this.wireframeOnly,
					this.unlit,
					this.bedGrid);
		}

		/// <summary>The per-frame draw setup a <see cref="MeshDrawSetupKey"/> buys back: the uniform slot the
		/// blocks were staged into, and the mesh's two resolved render-data plugins.</summary>
		private readonly struct MeshDrawSetup
		{
			public MeshDrawSetup(int uniformSlot, MeshTrianglePlugin meshPlugin, SceneEdgeShaderDataPlugin sceneShaderData)
			{
				this.UniformSlot = uniformSlot;
				this.MeshPlugin = meshPlugin;
				this.SceneShaderData = sceneShaderData;
			}

			public int UniformSlot { get; }

			public MeshTrianglePlugin MeshPlugin { get; }

			public SceneEdgeShaderDataPlugin SceneShaderData { get; }
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
