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
using DualContouring;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// The <see cref="ISceneDrawContext"/> served by a <see cref="GL"/> facade and, where the facade's
	/// context offers one, the retained <see cref="INativeSceneRenderer"/> behind it.
	/// </summary>
	/// <remarks>
	/// Every member forwards to the helper that already implements it -
	/// <see cref="WorldViewExtensions"/>, <see cref="RenderHelper"/>, the scene renderer - so this file
	/// contains no drawing logic of its own and there stays exactly one copy of each helper body. That is
	/// the deliberate choice for this leg of the port: the helper bodies are what the tolerance-zero
	/// golden suite pins, and moving them and changing every call site in one step would make a pixel
	/// diff impossible to attribute. The helpers become internal to the renderer as the application's
	/// call sites move to this interface.
	/// </remarks>
	public sealed class SceneDrawContext : ISceneDrawContext
	{
		private readonly GL gl;

		/// <summary>Computed once at <see cref="BeginFrame"/>; see <see cref="ClippingFrustum"/>.</summary>
		private Frustum frameFrustum;

		/// <summary>The world to put back at <see cref="EndFrame"/>.</summary>
		private WorldView worldBeforeFrame;

		private bool scenePassOpen;

		/// <summary>Built on first use; see <see cref="RenderTransformedPath"/>.</summary>
		private Graphics2DGpu pathRenderer;

		/// <summary>
		/// Creates a context over a facade.
		/// </summary>
		/// <param name="gl">The facade to draw through. Its <c>GpuContext</c> supplies the retained scene
		/// renderer if it has one; if it does not, mesh draws fall back to the facade's own path.</param>
		/// <param name="world">The camera to answer <see cref="World"/> with before the first
		/// <see cref="BeginFrame"/>. Widgets that draw things outside a frame (a background layer, say)
		/// pass their stable world here.</param>
		/// <param name="deviceScale">The host's logical-to-physical pixel scaling.</param>
		public SceneDrawContext(GL gl, WorldView world = null, double deviceScale = 1)
		{
			this.gl = gl ?? throw new ArgumentNullException(nameof(gl));
			this.World = world;
			this.DeviceScale = deviceScale;
		}

		/// <summary>
		/// Creates a context for the surface a widget is drawing into, or null if that surface has no
		/// renderer behind it.
		/// </summary>
		/// <remarks>
		/// The one place the GPU <see cref="Graphics2D"/> is recognised. Widgets that draw 3D from inside a
		/// 2D draw pass used to make this downcast themselves, which is how a graphics API ended up in
		/// application signatures whose only real need was "give me the renderer".
		/// </remarks>
		/// <param name="graphics2D">The surface being drawn into.</param>
		/// <param name="world">The camera for draws made outside a frame; see the constructor.</param>
		/// <param name="deviceScale">The host's logical-to-physical pixel scaling.</param>
		public static SceneDrawContext TryCreate(Graphics2D graphics2D, WorldView world = null, double deviceScale = 1)
		{
			var gl = (graphics2D as Graphics2DGpu)?.gl;

			return gl == null ? null : new SceneDrawContext(gl, world, deviceScale);
		}

		/// <inheritdoc/>
		public WorldView World { get; private set; }

		/// <inheritdoc/>
		public double DeviceScale { get; }

		/// <inheritdoc/>
		public RectangleDouble Viewport { get; private set; }

		/// <inheritdoc/>
		public LightingData Lighting { get; private set; }

		/// <inheritdoc/>
		public bool IsFrameOpen { get; private set; }

		/// <inheritdoc/>
		public bool IsSceneRenderingActive => this.NativeRenderer?.IsSceneRenderingActive == true;

		/// <inheritdoc/>
		/// <remarks>
		/// Cached for the life of the frame. The camera does not move inside a frame, so this is the same
		/// value the per-helper recomputation produced - bit for bit, since it is the same arithmetic on
		/// the same matrices - and a scene full of wire boxes was rebuilding it per box.
		/// </remarks>
		public Frustum ClippingFrustum
		{
			get
			{
				if (this.frameFrustum != null)
				{
					return this.frameFrustum;
				}

				return this.World?.GetClippingFrustum();
			}
		}

		/// <summary>The retained scene path, when the facade's context provides one.</summary>
		private INativeSceneRenderer NativeRenderer => this.gl.GpuContext as INativeSceneRenderer;

		/// <inheritdoc/>
		public void BeginFrame(WorldView world, RectangleDouble viewport, LightingData lighting)
		{
			if (world == null)
			{
				throw new ArgumentNullException(nameof(world));
			}

			if (this.IsFrameOpen)
			{
				throw new InvalidOperationException("A frame is already open on this draw context.");
			}

			this.worldBeforeFrame = this.World;
			this.World = world;
			this.Viewport = viewport;
			this.Lighting = lighting;
			this.frameFrustum = world.GetClippingFrustum();
			this.IsFrameOpen = true;

			RenderHelper.SetGlContext(this.gl, world, viewport, lighting);

			var nativeRenderer = this.NativeRenderer;
			if (nativeRenderer != null)
			{
				nativeRenderer.BeginSceneRendering(new SceneRenderContext(world, viewport, lighting));
				this.scenePassOpen = true;
			}
		}

		/// <inheritdoc/>
		public void EndScenePass()
		{
			if (!this.scenePassOpen)
			{
				return;
			}

			this.scenePassOpen = false;
			this.NativeRenderer?.EndSceneRendering();
		}

		/// <inheritdoc/>
		public void EndFrame()
		{
			if (!this.IsFrameOpen)
			{
				return;
			}

			try
			{
				this.EndScenePass();
				RenderHelper.UnsetGlContext(this.gl);
			}
			finally
			{
				this.IsFrameOpen = false;
				this.frameFrustum = null;
				this.Lighting = null;
				this.Viewport = default;
				this.World = this.worldBeforeFrame;
				this.worldBeforeFrame = null;
			}
		}

		/// <inheritdoc/>
		public IDisposable SuppressDepthTest()
		{
			this.gl.Disable(EnableCap.DepthTest);

			// Restored to on rather than to whatever was there before: the compat layer's attribute stack
			// does not save the enable bits (only the viewport), and every caller of this is inside a frame
			// whose ambient state is depth tested.
			return new DisposableScope(() => this.gl.Enable(EnableCap.DepthTest));
		}

		/// <inheritdoc/>
		public void PreloadTexture(ImageBuffer image, bool useMipMaps = true, bool magFilterLinear = true, bool clamp = true)
		{
			if (image == null)
			{
				return;
			}

			ImageTexturePlugin.GetImageTexturePlugin(this.gl, image, useMipMaps, magFilterLinear, clamp);
		}

		/// <inheritdoc/>
		public void BeginFullFrameCapture(RectangleDouble viewport)
			=> this.NativeRenderer?.BeginFullFrameCapture(viewport);

		/// <inheritdoc/>
		public void EndFullFrameCaptureAndBlit()
		{
			var nativeRenderer = this.NativeRenderer;
			if (nativeRenderer == null)
			{
				return;
			}

			nativeRenderer.EndFullFrameCapture();
			nativeRenderer.DownsampleAndBlitFullFrame();
		}

		/// <inheritdoc/>
		public void DrawMesh(
			Mesh mesh,
			Color color,
			Matrix4X4 transform,
			RenderTypes renderType = RenderTypes.Shaded,
			Matrix4X4? meshToViewTransform = null,
			Color wireFrameColor = default,
			Action meshChanged = null,
			bool blendTexture = true,
			bool allowBspRendering = false,
			bool forceCullBackFaces = true,
			bool castsBedShadow = true,
			bool isSelected = false,
			bool overrideFaceColors = false,
			float alphaMultiplier = 1.0f)
		{
			RenderHelper.Render(
				this.gl,
				mesh,
				color,
				transform,
				renderType,
				meshToViewTransform,
				wireFrameColor,
				meshChanged,
				blendTexture,
				allowBspRendering,
				forceCullBackFaces,
				castsBedShadow,
				isSelected,
				overrideFaceColors,
				alphaMultiplier);
		}

		/// <inheritdoc/>
		public bool TryDrawBed(BedRenderCommand command) => this.NativeRenderer?.TryRender(command) == true;

		/// <inheritdoc/>
		public void QueueSelectionOutline(Mesh mesh, Color color, Matrix4X4 transform)
			=> this.NativeRenderer?.QueueSelectionOutline(mesh, color, transform);

		/// <inheritdoc/>
		public void RenderPlane(Plane plane, Color color, bool doDepthTest, double rectSize, double lineWidth)
			=> this.World.RenderPlane(this.gl, plane, color, doDepthTest, rectSize, lineWidth);

		/// <inheritdoc/>
		public void RenderPlane(Vector3 position, Vector3 normal, Color color, bool doDepthTest, double rectSize, double lineWidth)
			=> this.World.RenderPlane(this.gl, position, normal, color, doDepthTest, rectSize, lineWidth);

		/// <inheritdoc/>
		public void Render3DLine(Vector3 start, Vector3 end, Color color, bool doDepthTest = true, double width = 1, bool startArrow = false, bool endArrow = false)
			=> this.World.Render3DLine(this.gl, this.ClippingFrustum, start, end, color, doDepthTest, width, startArrow, endArrow);

		/// <inheritdoc/>
		public void Render3DLine(Frustum clippingFrustum, Vector3 start, Vector3 end, Color color, bool doDepthTest = true, double width = 1, bool startArrow = false, bool endArrow = false)
			=> this.World.Render3DLine(this.gl, clippingFrustum, start, end, color, doDepthTest, width, startArrow, endArrow);

		/// <inheritdoc/>
		public void Render3DLineNoPrep(Frustum clippingFrustum, Vector3 start, Vector3 end, Color color, double width = 1, bool startArrow = false, bool endArrow = false)
			=> this.World.Render3DLineNoPrep(this.gl, clippingFrustum, start, end, color, width, startArrow, endArrow);

		/// <inheritdoc/>
		public void Render3DLineNoPrep(Frustum clippingFrustum, Vector3Float start, Vector3Float end, Color color, double width = 1, bool startArrow = false, bool endArrow = false)
			=> this.World.Render3DLineNoPrep(this.gl, clippingFrustum, start, end, color, width, startArrow, endArrow);

		/// <inheritdoc/>
		public void PrepareFor3DLineRender(bool doDepthTest) => RenderHelper.PrepareFor3DLineRender(this.gl, doDepthTest);

		/// <inheritdoc/>
		public void RenderCylinderOutline(Matrix4X4 worldMatrix, Vector3 center, double diameter, double height, int sides, Color color, double lineWidth = 1, double extendLineLength = 0)
			=> this.World.RenderCylinderOutline(this.gl, worldMatrix, center, diameter, height, sides, color, lineWidth, extendLineLength);

		/// <inheritdoc/>
		public void RenderCylinderOutline(Matrix4X4 worldMatrix, Vector3 center, double diameter, double height, int sides, Color topBottomRingColor, Color sideLinesColor, double lineWidth = 1, double extendLineLength = 0, double phase = 0)
			=> this.World.RenderCylinderOutline(this.gl, worldMatrix, center, diameter, height, sides, topBottomRingColor, sideLinesColor, lineWidth, extendLineLength, phase);

		/// <inheritdoc/>
		public void RenderRing(Matrix4X4 worldMatrix, Vector3 center, double diameter, int sides, Color ringColor, double lineWidth = 1, double phase = 0, bool zBuffered = true)
			=> this.World.RenderRing(this.gl, worldMatrix, center, diameter, sides, ringColor, lineWidth, phase, zBuffered);

		/// <inheritdoc/>
		public void RenderPathOutline(Matrix4X4 worldMatrix, IVertexSource path, Color color, double lineWidth = 1)
			=> this.World.RenderPathOutline(this.gl, worldMatrix, path, color, lineWidth);

		/// <inheritdoc/>
		public void DrawOctree(OctreeNode rootNode, int colorIndex) => this.World.DrawOctree(this.gl, rootNode, colorIndex);

		/// <inheritdoc/>
		public void DrawOctreeNode(OctreeNode node, Color color) => this.World.DrawOctreeNode(this.gl, node, color);

		/// <inheritdoc/>
		public void RenderAabb(AxisAlignedBoundingBox bounds, Matrix4X4 matrix, Color color, double lineWidth = 1, double extendLineLength = 0)
			=> this.World.RenderAabb(this.gl, bounds, matrix, color, lineWidth, extendLineLength);

		/// <inheritdoc/>
		public void RenderAxis(Vector3 position, Matrix4X4 matrix, double size, double lineWidth)
			=> this.World.RenderAxis(this.gl, position, matrix, size, lineWidth);

		/// <inheritdoc/>
		public void RenderPath(IVertexSource vertexSource, Color color, bool doDepthTest)
			=> this.World.RenderPath(this.gl, vertexSource, color, doDepthTest);

		/// <inheritdoc/>
		public void RenderTransformedPath(Matrix4X4 transform, IVertexSource path, Color color, bool doDepthTest)
		{
			// Kept for the life of the context rather than built per call - it is only a handle onto the
			// facade, and the call sites this replaced built one per draw.
			this.pathRenderer ??= new Graphics2DGpu(this.gl, this.DeviceScale);

			this.pathRenderer.RenderTransformedPath(transform, path, color, doDepthTest);
		}

		/// <inheritdoc/>
		public void DrawPrimitives(DrawTopology topology, ReadOnlySpan<PosColorVertex> vertices, Matrix4X4 transform, bool depthTest)
			=> GlPrimitiveEmitter.Emit(this.gl, topology, vertices, transform, depthTest);

		private sealed class DisposableScope : IDisposable
		{
			private readonly Action onDispose;

			public DisposableScope(Action onDispose) => this.onDispose = onDispose;

			public void Dispose() => this.onDispose();
		}
	}
}
