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
using System.Threading.Tasks;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderGl;
using MatterHackers.VectorMath;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// Goldens for the 3D path (<see cref="INativeSceneRenderer"/>) on the classic D3D11 backend: lit opaque
	/// meshes, the dual-depth-peeling transparency compositor, the edge and wireframe render types, selection
	/// outlines, and the 3x supersampled full-frame capture.
	/// </summary>
	/// <remarks>
	/// Every scene here is built from generated geometry rather than a loaded file, so the goldens depend on
	/// nothing but the renderer. The camera is set by hand (never <c>Fit</c>) for the same reason: a framing
	/// derived from a bounding box would move the moment the geometry helpers changed and every golden would
	/// go stale for a reason that has nothing to do with rendering.
	/// </remarks>
	[NotInParallel]
	public class GoldenSceneTests
	{
		private static async Task Check(string goldenName, bool supersample, Action<D3D11OffscreenCapture> drawScene)
		{
			using var capture = D3D11OffscreenCapture.Create();

			capture.ClearTo(Golden3DScenes.Background);

			var world = Golden3DScenes.CreateCamera(capture.Width, capture.Height);

			// A fresh LightingData per capture: RenderHelper.SetGlContext normalises LightDirection0 in
			// place, so a shared instance would be renormalising an already normalised vector.
			var lighting = new LightingData();

			capture.RenderScene(world, lighting, () => drawScene(capture), supersample);

			await GoldenImage.Check(capture.Capture(), goldenName);
		}

		[Test]
		public async Task OpaqueMeshes()
		{
			await Check("Scene.Opaque", supersample: false, capture =>
				Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Shaded, 255));
		}

		/// <summary>
		/// The transparency compositor. Every mesh is partly transparent and they overlap each other, so the
		/// image is entirely a product of the dual-depth-peeling passes - the hardest visual-parity item in
		/// the port, and the one whose blend formulation has to be reproduced exactly in WGSL.
		/// </summary>
		[Test]
		public async Task TransparentMeshes()
		{
			await Check("Scene.Transparent", supersample: false, capture =>
				Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Shaded, 120));
		}

		/// <summary>Opaque geometry in front of transparent geometry, which is the case that actually
		/// exercises the peel-against-opaque-depth interaction rather than peeling alone.</summary>
		[Test]
		public async Task MixedOpaqueAndTransparent()
		{
			await Check("Scene.Mixed", supersample: false, capture => Golden3DScenes.DrawMixedScene(capture.Gl));
		}

		/// <summary>The outline render type - shaded fill plus the mesh's edge overlay.</summary>
		[Test]
		public async Task OutlineRenderType()
		{
			await Check("Scene.Outlines", supersample: false, capture =>
				Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Outlines, 255));
		}

		/// <summary>Wireframe, where only the edge geometry is drawn - a different path again from the
		/// outline overlay.</summary>
		[Test]
		public async Task WireframeRenderType()
		{
			await Check("Scene.Wireframe", supersample: false, capture =>
				Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Wireframe, 255));
		}

		/// <summary>Selection outlines, queued on the renderer directly. Reachable without any app-level
		/// machinery, so the port's outline pass has a golden of its own.</summary>
		[Test]
		public async Task SelectionOutline()
		{
			await Check("Scene.SelectionOutline", supersample: false, capture =>
				Golden3DScenes.DrawSelectionOutlineScene(capture.Gl, capture.SceneRenderer));
		}

		/// <summary>
		/// The same opaque scene through <c>BeginFullFrameCapture</c> / <c>DownsampleAndBlitFullFrame</c> -
		/// the 3x supersample target plus the 9-tap box downsample the viewport and thumbnails use. The
		/// "swap the render target view" trick behind it has no WebGPU equivalent, so this golden is what
		/// says the replacement produces the same pixels.
		/// </summary>
		[Test]
		public async Task SupersampledFullFrame()
		{
			await Check("Scene.Supersampled", supersample: true, capture =>
				Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Shaded, 255));
		}

		/// <summary>Supersampled transparency: the capture target and the peel targets have to agree about
		/// resolution, which is exactly the kind of thing that breaks silently in a port.</summary>
		[Test]
		public async Task SupersampledTransparent()
		{
			await Check("Scene.SupersampledTransparent", supersample: true, capture =>
				Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Shaded, 120));
		}

		/// <summary>
		/// The printer bed: shadow mask, blur, composite and the analytic grid, queued through the
		/// renderer's bed entry point.
		/// </summary>
		[Test]
		public async Task BedWithShadow()
		{
			await Check("Scene.Bed", supersample: false, capture =>
				Golden3DScenes.DrawBedScene(capture.Gl, capture.SceneRenderer));
		}

		/// <summary>
		/// A textured mesh - the one scene that reaches the shaders' texture entry points.
		/// </summary>
		[Test]
		public async Task TexturedMesh()
		{
			await Check("Scene.TexturedMesh", supersample: false, capture =>
				Golden3DScenes.DrawTexturedMeshScene(capture.Gl));
		}

		/// <summary>
		/// The overhang render type.
		/// </summary>
		/// <remarks>
		/// This golden is captured through the classic backend's <i>fallback</i> path, not its native scene
		/// pipeline: <c>VorticeD3DGl.CanRender</c> refuses <see cref="RenderTypes.Overhang"/>, so
		/// <c>RenderHelper.Render</c> drops through to immediate-mode GL. That is exactly the hole Phase 3
		/// leg B closes on the WebGPU side, which is why the cross-backend twin of this test does not
		/// compare against this image - see <c>GoldenSceneOnWebGpuTests.OverhangRenderType</c>.
		/// </remarks>
		[Test]
		public async Task OverhangRenderType()
		{
			await Check("Scene.Overhang", supersample: false, capture =>
				Golden3DScenes.DrawOverhangScene(capture.Gl));
		}
	}
}
