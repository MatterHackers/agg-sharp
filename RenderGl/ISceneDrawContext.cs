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
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// Everything scene, editor and control code needs in order to draw, with no graphics API in the
	/// signature.
	/// </summary>
	/// <remarks>
	/// This is the seam the wgpu port's Phase 5 puts between the application and the renderer. Today the
	/// application passes a <c>GL</c> facade around and downcasts its <c>GpuContext</c> to reach the
	/// retained scene path; every one of those calls is expressed here instead, so a draw method can take
	/// an <see cref="ISceneDrawContext"/> and know nothing about how it is served.
	/// <para>
	/// The vocabulary is deliberately the one the application already speaks - the
	/// <c>WorldViewExtensions</c> helpers, <c>RenderHelper.Render</c>, the frame setup pair and the two
	/// hand-rolled immediate-mode shapes - rather than a smaller, more principled set. A narrower
	/// interface would have to be met by more code at each call site, which is how a seam leaks.
	/// </para>
	/// <para>
	/// A context is bound to one output. It carries the frame's camera, so helpers that used to be
	/// extension methods on <see cref="WorldView"/> take no world argument here.
	/// </para>
	/// </remarks>
	public interface ISceneDrawContext
	{
		/// <summary>The camera the frame is drawn with.</summary>
		WorldView World { get; }

		/// <summary>
		/// The scaling the host applies between logical and physical pixels, for the draws that are sized
		/// in screen units.
		/// </summary>
		double DeviceScale { get; }

		/// <summary>The screen-space rectangle being drawn into. Empty outside a frame.</summary>
		RectangleDouble Viewport { get; }

		/// <summary>The frame's lights. Null outside a frame.</summary>
		LightingData Lighting { get; }

		/// <summary>
		/// The view frustum of <see cref="World"/>, computed once per frame.
		/// </summary>
		/// <remarks>
		/// Line drawing clips against this per segment, and the helpers that draw many segments used to
		/// each rebuild it. It is exposed because callers that draw their own loops of
		/// <see cref="Render3DLineNoPrep(Frustum, Vector3, Vector3, Color, double, bool, bool)"/> still
		/// want to hoist it.
		/// </remarks>
		Frustum ClippingFrustum { get; }

		/// <summary>Whether <see cref="BeginFrame"/> has been called without its <see cref="EndFrame"/>.</summary>
		bool IsFrameOpen { get; }

		/// <summary>
		/// Whether the retained scene pass is open, i.e. queued meshes are being collected rather than
		/// drawn immediately.
		/// </summary>
		bool IsSceneRenderingActive { get; }

		/// <summary>
		/// Installs the 3D frame state and opens the scene pass.
		/// </summary>
		/// <remarks>
		/// Replaces the <c>SetGlContext</c>/<c>BeginSceneRendering</c> pair, which were two separate
		/// global-state calls every 3D widget had to make in the right order. The context owns which pass
		/// is open and what uniform state the frame is in, so getting that wrong is no longer possible
		/// from the outside.
		/// </remarks>
		/// <param name="world">The camera for the frame. Becomes <see cref="World"/> until
		/// <see cref="EndFrame"/>, which restores whatever world was current before.</param>
		/// <param name="viewport">The screen-space rectangle to draw into.</param>
		/// <param name="lighting">The frame's lights. Mutated in place (its light direction is
		/// normalised), so callers should not share one instance across contexts.</param>
		void BeginFrame(WorldView world, RectangleDouble viewport, LightingData lighting);

		/// <summary>
		/// Closes the retained scene pass early, flushing its queued geometry, while leaving the frame's
		/// matrices and lighting installed.
		/// </summary>
		/// <remarks>
		/// The one thing frame begin/end cannot hide: some overlays (path outlines) are drawn with the
		/// immediate-mode escape hatch and must land after the scene has composited, but still inside the
		/// frame's camera. Calling this twice, or not at all, is safe - <see cref="EndFrame"/> closes the
		/// pass if it is still open.
		/// </remarks>
		void EndScenePass();

		/// <summary>Closes the scene pass if still open and tears down the frame state.</summary>
		void EndFrame();

		/// <summary>
		/// Draws everything inside the scope as an always-visible overlay instead of as scene geometry.
		/// </summary>
		/// <remarks>
		/// The 3D controls' "ghost" pass: the handles are drawn once with the depth test off so they show
		/// through the part, then again depth tested so they sort among themselves. It is a scope rather
		/// than a pair of calls because the depth test is the one piece of ambient render state the
		/// renderer still reads back from the caller - a queued mesh lands in the overlay queue or the
		/// scene queue depending on it - so leaving it off by accident would silently move whole objects
		/// in front of the part.
		/// </remarks>
		/// <returns>Restores the depth test when disposed.</returns>
		IDisposable SuppressDepthTest();

		/// <summary>
		/// Ensures an image is resident as a texture, sampled the way the caller asks.
		/// </summary>
		/// <remarks>
		/// For the caller that puts a texture onto meshes it does not draw itself. Drawing would upload it
		/// on demand, but with the mesh path's own (clamped, mip-mapped) sampling, and the texture cache is
		/// keyed by image and context only - so whoever uploads first decides the sampling for everyone.
		/// Pre-loading is how the hole-marking stripe pattern stays tiling rather than clamped.
		/// </remarks>
		void PreloadTexture(ImageBuffer image, bool useMipMaps = true, bool magFilterLinear = true, bool clamp = true);

		/// <summary>
		/// Redirects the frame to an off-screen supersample target. Call before <see cref="BeginFrame"/>.
		/// </summary>
		/// <param name="viewport">The logical viewport being captured.</param>
		void BeginFullFrameCapture(RectangleDouble viewport);

		/// <summary>
		/// Ends the supersample capture and box-downsamples it onto the real target. Call after
		/// <see cref="EndFrame"/>.
		/// </summary>
		void EndFullFrameCaptureAndBlit();

		/// <summary>
		/// Draws a mesh - the single entry point for scene geometry.
		/// </summary>
		/// <remarks>
		/// Inside a frame this queues a <see cref="MeshRenderCommand"/> that the scene renderer sorts into
		/// passes (see <see cref="NativeSceneRenderPlanner"/>) rather than drawing anything immediately.
		/// </remarks>
		/// <param name="mesh">The geometry. Null draws nothing.</param>
		/// <param name="color">The part colour. Alpha below 1 routes the mesh to the transparency passes.</param>
		/// <param name="transform">Mesh to world.</param>
		/// <param name="renderType">Shaded, outlined, wireframe and so on.</param>
		/// <param name="meshToViewTransform">Mesh to view, for the render types that sort by depth.</param>
		/// <param name="wireFrameColor">Edge colour for the wire overlay render types.</param>
		/// <param name="meshChanged">Invoked when a lazily built render cache for this mesh completes.</param>
		/// <param name="blendTexture">False replaces rather than modulates the mesh's texture.</param>
		/// <param name="allowBspRendering">Permits the BSP visibility sort for transparent geometry.</param>
		/// <param name="forceCullBackFaces">False draws back faces, which also forces the transparent pass.</param>
		/// <param name="castsBedShadow">Whether this mesh contributes to the bed's cast shadow.</param>
		/// <param name="isSelected">Whether this mesh is part of the current selection.</param>
		/// <param name="overrideFaceColors">Ignores per-face colours in favour of <paramref name="color"/>.</param>
		/// <param name="alphaMultiplier">Scales the final alpha of every colour, per-face ones included.</param>
		void DrawMesh(
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
			float alphaMultiplier = 1.0f);

		/// <summary>
		/// Queues the printer bed, with its cast shadow and analytic grid.
		/// </summary>
		/// <param name="command">The bed, its texture and its grid styling.</param>
		/// <returns>False if this context cannot draw a bed as a bed, in which case the caller draws its
		/// mesh with <see cref="DrawMesh"/> instead.</returns>
		bool TryDrawBed(BedRenderCommand command);

		/// <summary>Queues a selection outline around a mesh, drawn after the scene composites.</summary>
		void QueueSelectionOutline(Mesh mesh, Color color, Matrix4X4 transform);

		/// <summary>Draws the outline of a square patch of a plane.</summary>
		void RenderPlane(Plane plane, Color color, bool doDepthTest, double rectSize, double lineWidth);

		/// <summary>Draws the outline of a square patch of the plane through a point with a normal.</summary>
		void RenderPlane(Vector3 position, Vector3 normal, Color color, bool doDepthTest, double rectSize, double lineWidth);

		/// <summary>
		/// Draws a line in 3D that stays a constant width in screen pixels, setting up the line render
		/// state around it.
		/// </summary>
		void Render3DLine(Vector3 start, Vector3 end, Color color, bool doDepthTest = true, double width = 1, bool startArrow = false, bool endArrow = false);

		/// <summary>As <see cref="Render3DLine(Vector3, Vector3, Color, bool, double, bool, bool)"/>, with
		/// a caller-hoisted frustum for loops of many lines.</summary>
		void Render3DLine(Frustum clippingFrustum, Vector3 start, Vector3 end, Color color, bool doDepthTest = true, double width = 1, bool startArrow = false, bool endArrow = false);

		/// <summary>
		/// Draws one screen-width line without touching the line render state, for callers that have
		/// already called <see cref="PrepareFor3DLineRender"/> and are drawing many.
		/// </summary>
		void Render3DLineNoPrep(Frustum clippingFrustum, Vector3 start, Vector3 end, Color color, double width = 1, bool startArrow = false, bool endArrow = false);

		/// <inheritdoc cref="Render3DLineNoPrep(Frustum, Vector3, Vector3, Color, double, bool, bool)"/>
		void Render3DLineNoPrep(Frustum clippingFrustum, Vector3Float start, Vector3Float end, Color color, double width = 1, bool startArrow = false, bool endArrow = false);

		/// <summary>
		/// Installs the state the *NoPrep* line and primitive draws expect: blending on, lighting and
		/// texturing off, depth testing as asked.
		/// </summary>
		void PrepareFor3DLineRender(bool doDepthTest);

		/// <summary>Draws a cylinder as its two end rings and its side lines.</summary>
		void RenderCylinderOutline(Matrix4X4 worldMatrix, Vector3 center, double diameter, double height, int sides, Color color, double lineWidth = 1, double extendLineLength = 0);

		/// <summary>As above, with the rings and the side lines coloured separately; either may be
		/// <see cref="Color.Transparent"/> to be skipped entirely.</summary>
		void RenderCylinderOutline(Matrix4X4 worldMatrix, Vector3 center, double diameter, double height, int sides, Color topBottomRingColor, Color sideLinesColor, double lineWidth = 1, double extendLineLength = 0, double phase = 0);

		/// <summary>Draws a polygonal ring - the rotate control's dial.</summary>
		void RenderRing(Matrix4X4 worldMatrix, Vector3 center, double diameter, int sides, Color ringColor, double lineWidth = 1, double phase = 0, bool zBuffered = true);

		/// <summary>Draws a 2D path, flattened and transformed into the scene, as screen-width lines.</summary>
		void RenderPathOutline(Matrix4X4 worldMatrix, IVertexSource path, Color color, double lineWidth = 1);

		/// <summary>Draws every populated node of an octree, colour-coded by depth.</summary>
		void DrawOctree(OctreeNode rootNode, int colorIndex);

		/// <summary>Draws one octree node as a wire box.</summary>
		void DrawOctreeNode(OctreeNode node, Color color);

		/// <summary>Draws a transformed bounding box as twelve screen-width lines.</summary>
		void RenderAabb(AxisAlignedBoundingBox bounds, Matrix4X4 matrix, Color color, double lineWidth = 1, double extendLineLength = 0);

		/// <summary>Draws the three coloured axis lines through a point.</summary>
		void RenderAxis(Vector3 position, Matrix4X4 matrix, double size, double lineWidth);

		/// <summary>Draws a filled, anti-aliased 2D path in the scene's coordinate space.</summary>
		void RenderPath(IVertexSource vertexSource, Color color, bool doDepthTest);

		/// <summary>
		/// Draws a filled, anti-aliased 2D path placed into the scene by a transform.
		/// </summary>
		/// <remarks>
		/// Unlike <see cref="RenderPath"/> this tessellates the path into a mesh the retained scene path can
		/// take, which is what the rotate control's dial and angle wedge are made of. Sized in world units
		/// by <paramref name="transform"/>, not in screen pixels.
		/// </remarks>
		void RenderTransformedPath(Matrix4X4 transform, IVertexSource path, Color color, bool doDepthTest);

		/// <summary>
		/// The escape hatch: draws vertices the caller assembled itself, each with its own colour.
		/// </summary>
		/// <remarks>
		/// For geometry that is neither a mesh nor a line - a per-vertex gradient, or a bulk line list a
		/// caller batches itself because drawing it as thousands of screen-width lines would be too slow.
		/// Everything that can be said with the members above should be; this exists so that the handful
		/// of sites that cannot do not need a graphics API in their signature either.
		/// </remarks>
		/// <param name="topology">How the vertices assemble into primitives.</param>
		/// <param name="vertices">The vertices, in the space <paramref name="transform"/> maps to world.</param>
		/// <param name="transform">Applied on top of the frame's camera.</param>
		/// <param name="depthTest">Whether the primitives are occluded by scene geometry.</param>
		void DrawPrimitives(DrawTopology topology, ReadOnlySpan<PosColorVertex> vertices, Matrix4X4 transform, bool depthTest);
	}
}
