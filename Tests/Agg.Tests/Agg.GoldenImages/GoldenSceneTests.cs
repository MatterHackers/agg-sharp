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
		/// <summary>Background for the scene captures - a mid grey, so both dark and light geometry show
		/// their silhouette anti-aliasing against it.</summary>
		private static readonly ColorF Background = new ColorF(0.31f, 0.33f, 0.36f, 1);

		private static async Task Check(string goldenName, bool supersample, Action<D3D11OffscreenCapture> drawScene)
		{
			using var capture = D3D11OffscreenCapture.Create();

			capture.ClearTo(Background);

			var world = CreateCamera(capture.Width, capture.Height);

			// A fresh LightingData per capture: RenderHelper.SetGlContext normalises LightDirection0 in
			// place, so a shared instance would be renormalising an already normalised vector.
			var lighting = new LightingData();

			capture.RenderScene(world, lighting, () => drawScene(capture), supersample);

			await GoldenImage.Check(capture.Capture(), goldenName);
		}

		/// <summary>
		/// A fixed three-quarter view, framed by scale rather than by distance.
		/// </summary>
		/// <remarks>
		/// <see cref="WorldView"/>'s default frustum is near 0.1 / far 100 with the camera parked 7 units
		/// back, so millimetre-sized geometry has to be scaled into that range rather than pushed away from
		/// the camera - a translate large enough to frame a 60mm box puts the whole scene behind the far
		/// plane and renders an empty frame. This is the same arrangement <c>D3D11ThumbnailRenderer</c> uses.
		/// </remarks>
		private static WorldView CreateCamera(int width, int height)
		{
			var world = new WorldView(width, height);
			world.Reset();
			world.Scale = 0.035;
			world.Rotate(Quaternion.FromEulerAngles(new Vector3(0, 0, -MathHelper.Tau / 16)));
			world.Rotate(Quaternion.FromEulerAngles(new Vector3(MathHelper.Tau * 0.19, 0, 0)));

			return world;
		}

		/// <summary>
		/// A UV sphere, generated here so the scenes have curvature (and therefore a dense, lighting
		/// sensitive normal field) without depending on a mesh generator that may be tuned later.
		/// </summary>
		private static Mesh CreateSphere(double radius, int segments, int rings)
		{
			var mesh = new Mesh();

			for (int ring = 0; ring < rings; ring++)
			{
				double phi0 = Math.PI * ring / rings;
				double phi1 = Math.PI * (ring + 1) / rings;

				for (int segment = 0; segment < segments; segment++)
				{
					double theta0 = Math.PI * 2 * segment / segments;
					double theta1 = Math.PI * 2 * (segment + 1) / segments;

					Vector3 a = OnSphere(radius, phi0, theta0);
					Vector3 b = OnSphere(radius, phi1, theta0);
					Vector3 c = OnSphere(radius, phi1, theta1);
					Vector3 d = OnSphere(radius, phi0, theta1);

					if (ring != 0)
					{
						mesh.CreateFace(a, b, d);
					}

					if (ring != rings - 1)
					{
						mesh.CreateFace(b, c, d);
					}
				}
			}

			mesh.CalculateNormals();
			return mesh;
		}

		private static Vector3 OnSphere(double radius, double phi, double theta)
			=> new Vector3(
				radius * Math.Sin(phi) * Math.Cos(theta),
				radius * Math.Sin(phi) * Math.Sin(theta),
				radius * Math.Cos(phi));

		/// <summary>Three boxes and a sphere, arranged so they overlap in screen space and therefore depend
		/// on the depth buffer as well as on lighting.</summary>
		private static void DrawStandardScene(D3D11OffscreenCapture capture, RenderTypes renderType, int alpha)
		{
			var box = PlatonicSolids.CreateCube(60, 60, 60);
			var slab = PlatonicSolids.CreateCube(140, 40, 14);
			var sphere = CreateSphere(38, 24, 16);

			RenderHelper.Render(
				capture.Gl,
				slab,
				new Color(200, 200, 205, alpha),
				Matrix4X4.CreateTranslation(0, 0, -34),
				renderType,
				wireFrameColor: new Color(20, 20, 20));

			RenderHelper.Render(
				capture.Gl,
				box,
				new Color(210, 70, 50, alpha),
				Matrix4X4.CreateRotationZ(MathHelper.Tau * 0.06) * Matrix4X4.CreateTranslation(-52, -18, 8),
				renderType,
				wireFrameColor: new Color(20, 20, 20));

			RenderHelper.Render(
				capture.Gl,
				box,
				new Color(60, 130, 220, alpha),
				Matrix4X4.CreateRotationX(MathHelper.Tau * 0.11) * Matrix4X4.CreateTranslation(46, 12, 20),
				renderType,
				wireFrameColor: new Color(20, 20, 20));

			RenderHelper.Render(
				capture.Gl,
				sphere,
				new Color(90, 200, 120, alpha),
				Matrix4X4.CreateTranslation(0, 34, 34),
				renderType,
				wireFrameColor: new Color(20, 20, 20));
		}

		[Test]
		public async Task OpaqueMeshes()
		{
			await Check("Scene.Opaque", supersample: false, capture =>
				DrawStandardScene(capture, RenderTypes.Shaded, 255));
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
				DrawStandardScene(capture, RenderTypes.Shaded, 120));
		}

		/// <summary>Opaque geometry in front of transparent geometry, which is the case that actually
		/// exercises the peel-against-opaque-depth interaction rather than peeling alone.</summary>
		[Test]
		public async Task MixedOpaqueAndTransparent()
		{
			await Check("Scene.Mixed", supersample: false, capture =>
			{
				var box = PlatonicSolids.CreateCube(60, 60, 60);
				var sphere = CreateSphere(46, 24, 16);

				RenderHelper.Render(
					capture.Gl,
					box,
					new Color(210, 70, 50),
					Matrix4X4.CreateTranslation(-40, 0, 0),
					RenderTypes.Shaded);

				RenderHelper.Render(
					capture.Gl,
					sphere,
					new Color(90, 200, 220, 110),
					Matrix4X4.CreateTranslation(0, 0, 10),
					RenderTypes.Shaded);

				RenderHelper.Render(
					capture.Gl,
					box,
					new Color(240, 210, 60, 150),
					Matrix4X4.CreateRotationZ(MathHelper.Tau * 0.12) * Matrix4X4.CreateTranslation(46, 20, 26),
					RenderTypes.Shaded);
			});
		}

		/// <summary>The outline render type - shaded fill plus the mesh's edge overlay.</summary>
		[Test]
		public async Task OutlineRenderType()
		{
			await Check("Scene.Outlines", supersample: false, capture =>
				DrawStandardScene(capture, RenderTypes.Outlines, 255));
		}

		/// <summary>Wireframe, where only the edge geometry is drawn - a different path again from the
		/// outline overlay.</summary>
		[Test]
		public async Task WireframeRenderType()
		{
			await Check("Scene.Wireframe", supersample: false, capture =>
				DrawStandardScene(capture, RenderTypes.Wireframe, 255));
		}

		/// <summary>Selection outlines, queued on the renderer directly. Reachable without any app-level
		/// machinery, so the port's outline pass has a golden of its own.</summary>
		[Test]
		public async Task SelectionOutline()
		{
			await Check("Scene.SelectionOutline", supersample: false, capture =>
			{
				var box = PlatonicSolids.CreateCube(70, 70, 70);
				var sphere = CreateSphere(40, 24, 16);
				var boxTransform = Matrix4X4.CreateRotationZ(MathHelper.Tau * 0.05) * Matrix4X4.CreateTranslation(-46, 0, 0);
				var sphereTransform = Matrix4X4.CreateTranslation(48, 10, 8);

				RenderHelper.Render(capture.Gl, box, new Color(210, 70, 50), boxTransform, RenderTypes.Shaded, isSelected: true);
				RenderHelper.Render(capture.Gl, sphere, new Color(60, 130, 220), sphereTransform, RenderTypes.Shaded);

				capture.SceneRenderer.QueueSelectionOutline(box, new Color(255, 255, 255), boxTransform);
			});
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
				DrawStandardScene(capture, RenderTypes.Shaded, 255));
		}

		/// <summary>Supersampled transparency: the capture target and the peel targets have to agree about
		/// resolution, which is exactly the kind of thing that breaks silently in a port.</summary>
		[Test]
		public async Task SupersampledTransparent()
		{
			await Check("Scene.SupersampledTransparent", supersample: true, capture =>
				DrawStandardScene(capture, RenderTypes.Shaded, 120));
		}
	}
}
