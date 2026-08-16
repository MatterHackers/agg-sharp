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
using MatterHackers.Agg.Image;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// The 3D golden scenes, defined once and rendered by both backends: <see cref="GoldenSceneTests"/>
	/// captures them on the classic D3D11 path and <see cref="GoldenSceneOnWebGpuTests"/> compares the
	/// WebGPU output against those same PNGs. Extracted for exactly that reason - two copies of a scene
	/// is two chances for the comparison to be measuring the scene rather than the renderer.
	/// </summary>
	/// <remarks>
	/// Every scene is built from generated geometry rather than a loaded file, so the goldens depend on
	/// nothing but the renderer. The camera is set by hand (never <c>Fit</c>) for the same reason: a
	/// framing derived from a bounding box would move the moment the geometry helpers changed and every
	/// golden would go stale for a reason that has nothing to do with rendering.
	/// </remarks>
	public static class Golden3DScenes
	{
		/// <summary>Background for the scene captures - a mid grey, so both dark and light geometry show
		/// their silhouette anti-aliasing against it.</summary>
		public static ColorF Background => new ColorF(0.31f, 0.33f, 0.36f, 1);

		/// <summary>
		/// A fixed three-quarter view, framed by scale rather than by distance.
		/// </summary>
		/// <remarks>
		/// <see cref="WorldView"/>'s default frustum is near 0.1 / far 100 with the camera parked 7 units
		/// back, so millimetre-sized geometry has to be scaled into that range rather than pushed away from
		/// the camera - a translate large enough to frame a 60mm box puts the whole scene behind the far
		/// plane and renders an empty frame. This is the same arrangement <c>D3D11ThumbnailRenderer</c> uses.
		/// </remarks>
		/// <param name="width">Viewport width in pixels.</param>
		/// <param name="height">Viewport height in pixels.</param>
		public static WorldView CreateCamera(int width, int height)
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
		/// <param name="radius">Sphere radius.</param>
		/// <param name="segments">Divisions around the equator.</param>
		/// <param name="rings">Divisions from pole to pole.</param>
		public static Mesh CreateSphere(double radius, int segments, int rings)
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

		/// <summary>Three boxes and a sphere, arranged so they overlap in screen space and therefore depend
		/// on the depth buffer as well as on lighting.</summary>
		/// <param name="gl">The facade to draw through.</param>
		/// <param name="renderType">Shaded, outlines, wireframe, ...</param>
		/// <param name="alpha">Alpha applied to every mesh colour; 255 is the opaque scene.</param>
		public static void DrawStandardScene(GL gl, RenderTypes renderType, int alpha)
		{
			var box = PlatonicSolids.CreateCube(60, 60, 60);
			var slab = PlatonicSolids.CreateCube(140, 40, 14);
			var sphere = CreateSphere(38, 24, 16);

			RenderHelper.Render(
				gl,
				slab,
				new Color(200, 200, 205, alpha),
				Matrix4X4.CreateTranslation(0, 0, -34),
				renderType,
				wireFrameColor: new Color(20, 20, 20));

			RenderHelper.Render(
				gl,
				box,
				new Color(210, 70, 50, alpha),
				Matrix4X4.CreateRotationZ(MathHelper.Tau * 0.06) * Matrix4X4.CreateTranslation(-52, -18, 8),
				renderType,
				wireFrameColor: new Color(20, 20, 20));

			RenderHelper.Render(
				gl,
				box,
				new Color(60, 130, 220, alpha),
				Matrix4X4.CreateRotationX(MathHelper.Tau * 0.11) * Matrix4X4.CreateTranslation(46, 12, 20),
				renderType,
				wireFrameColor: new Color(20, 20, 20));

			RenderHelper.Render(
				gl,
				sphere,
				new Color(90, 200, 120, alpha),
				Matrix4X4.CreateTranslation(0, 34, 34),
				renderType,
				wireFrameColor: new Color(20, 20, 20));
		}

		/// <summary>Opaque geometry in front of transparent geometry.</summary>
		/// <param name="gl">The facade to draw through.</param>
		public static void DrawMixedScene(GL gl)
		{
			var box = PlatonicSolids.CreateCube(60, 60, 60);
			var sphere = CreateSphere(46, 24, 16);

			RenderHelper.Render(
				gl,
				box,
				new Color(210, 70, 50),
				Matrix4X4.CreateTranslation(-40, 0, 0),
				RenderTypes.Shaded);

			RenderHelper.Render(
				gl,
				sphere,
				new Color(90, 200, 220, 110),
				Matrix4X4.CreateTranslation(0, 0, 10),
				RenderTypes.Shaded);

			RenderHelper.Render(
				gl,
				box,
				new Color(240, 210, 60, 150),
				Matrix4X4.CreateRotationZ(MathHelper.Tau * 0.12) * Matrix4X4.CreateTranslation(46, 20, 26),
				RenderTypes.Shaded);
		}

		/// <summary>
		/// The overhang render type: a sphere and a tilted slab, whose per-face colour runs from blue on
		/// upward-facing triangles to red on the steepest downward-facing ones.
		/// </summary>
		/// <remarks>
		/// The colours come from <c>OverhangRender</c>, which re-colours the mesh's triangle plugin by face
		/// normal. Curvature matters here: a sphere covers the whole normal range in one mesh, so the
		/// golden fails visibly if the colour ramp is applied at the wrong stage or the vertex colour
		/// channel is not read at all.
		/// </remarks>
		/// <param name="gl">The facade to draw through.</param>
		public static void DrawOverhangScene(GL gl)
		{
			var sphere = CreateSphere(44, 24, 16);
			var slab = PlatonicSolids.CreateCube(120, 50, 12);

			RenderHelper.Render(
				gl,
				sphere,
				new Color(200, 200, 205),
				Matrix4X4.CreateTranslation(-34, 0, 16),
				RenderTypes.Overhang);

			RenderHelper.Render(
				gl,
				slab,
				new Color(200, 200, 205),
				Matrix4X4.CreateRotationY(MathHelper.Tau * 0.09) * Matrix4X4.CreateTranslation(48, 10, 22),
				RenderTypes.Overhang);
		}

		/// <summary>
		/// A textured mesh: a box with a generated checker image on every face.
		/// </summary>
		/// <remarks>
		/// The scene shaders take a different fragment entry point the moment a submesh carries a texture,
		/// and nothing in the earlier goldens reaches it - so this is the only case that says the sampler,
		/// the texture upload path and the uv channel all agree between the two backends.
		/// </remarks>
		/// <param name="gl">The facade to draw through.</param>
		public static void DrawTexturedMeshScene(GL gl)
		{
			var box = PlatonicSolids.CreateCube(80, 80, 80);
			box.PlaceTexture(CreateCheckerTexture(64, 8), Matrix4X4.Identity);

			RenderHelper.Render(
				gl,
				box,
				new Color(255, 255, 255),
				Matrix4X4.CreateRotationZ(MathHelper.Tau * 0.05) * Matrix4X4.CreateTranslation(0, 0, 10),
				RenderTypes.Shaded);
		}

		/// <summary>
		/// The printer bed, queued through <see cref="INativeSceneRenderer.TryRender(BedRenderCommand)"/>
		/// with two objects standing on it.
		/// </summary>
		/// <remarks>
		/// This is the whole bed feature in one image: the objects' blurred shadow cast straight down onto
		/// the bed, that shadow composited under the bed's own translucent texture, the analytic grid and
		/// axis lines drawn on top of it, and the bed peeled as transparent geometry along with anything
		/// else transparent in the frame.
		/// <para>
		/// Assembled here rather than through <c>FloorDrawable</c>, which lives in MatterCADLib and is not
		/// reachable from these tests - the command it builds is, and it is agg's own type.
		/// </para>
		/// </remarks>
		/// <param name="gl">The facade to draw through.</param>
		/// <param name="sceneRenderer">The renderer the bed is queued on.</param>
		public static void DrawBedScene(GL gl, INativeSceneRenderer sceneRenderer)
		{
			var bedBounds = new RectangleDouble(-120, -120, 120, 120);
			var bedMesh = MeshHelper.CreatePlane(bedBounds.Width, bedBounds.Height);
			bedMesh.PlaceTextureOnFaces(0, CreateBedTexture(256));

			var box = PlatonicSolids.CreateCube(50, 50, 50);
			var sphere = CreateSphere(30, 24, 16);

			RenderHelper.Render(gl, box, new Color(210, 70, 50), Matrix4X4.CreateTranslation(-46, -10, 25), RenderTypes.Shaded);
			RenderHelper.Render(gl, sphere, new Color(60, 130, 220), Matrix4X4.CreateTranslation(42, 16, 30), RenderTypes.Shaded);

			sceneRenderer.TryRender(new BedRenderCommand
			{
				Mesh = bedMesh,
				Color = Color.White,
				ShadowColor = new Color(20, 15, 10),
				Transform = Matrix4X4.CreateTranslation(0, 0, -.05),
				TopBaseTexture = CreateBedTexture(256),
				BedBounds = bedBounds,
				GridSpacing = 50,
				GridLineColor = new Color(120, 120, 130),
				AxisXColor = new Color(200, 90, 90),
				AxisYColor = new Color(90, 170, 90),
				AxisZColor = new Color(90, 110, 200),
				AxisHeight = 20,
			});
		}

		/// <summary>
		/// The bed's own image: a translucent fill with a border, generated so the golden depends on no
		/// file. Translucent on purpose - the bed is, and a premultiplied-to-straight alpha conversion
		/// between the image and the GPU is one of the things this golden is checking.
		/// </summary>
		/// <param name="size">Edge length in pixels.</param>
		public static ImageBuffer CreateBedTexture(int size)
		{
			var image = new ImageBuffer(size, size, 32, new BlenderBGRA());
			var graphics = image.NewGraphics2D();
			graphics.Clear(new Color(0, 0, 0, 0));
			graphics.FillRectangle(0, 0, size, size, new Color(220, 220, 225, 80));
			graphics.Rectangle(1, 1, size - 1, size - 1, new Color(150, 150, 160, 200), 2);
			image.MarkImageChanged();
			return image;
		}

		/// <summary>
		/// A checkerboard, generated rather than loaded so the goldens depend on no file on disk.
		/// </summary>
		/// <param name="size">Edge length in pixels.</param>
		/// <param name="cells">Checker cells along each edge.</param>
		public static ImageBuffer CreateCheckerTexture(int size, int cells)
		{
			var image = new ImageBuffer(size, size, 32, new BlenderBGRA());
			int cellSize = Math.Max(1, size / cells);
			var graphics = image.NewGraphics2D();
			graphics.Clear(new Color(230, 230, 235));

			for (int y = 0; y < size; y += cellSize)
			{
				for (int x = 0; x < size; x += cellSize)
				{
					if (((x / cellSize) + (y / cellSize)) % 2 == 0)
					{
						graphics.FillRectangle(x, y, x + cellSize, y + cellSize, new Color(40, 90, 160));
					}
				}
			}

			image.MarkImageChanged();
			return image;
		}

		/// <summary>A selected box beside an unselected sphere, with the box's outline queued directly on
		/// the renderer - reachable without any app-level machinery.</summary>
		/// <param name="gl">The facade to draw through.</param>
		/// <param name="sceneRenderer">The renderer the outline is queued on.</param>
		public static void DrawSelectionOutlineScene(GL gl, INativeSceneRenderer sceneRenderer)
		{
			var box = PlatonicSolids.CreateCube(70, 70, 70);
			var sphere = CreateSphere(40, 24, 16);
			var boxTransform = Matrix4X4.CreateRotationZ(MathHelper.Tau * 0.05) * Matrix4X4.CreateTranslation(-46, 0, 0);
			var sphereTransform = Matrix4X4.CreateTranslation(48, 10, 8);

			RenderHelper.Render(gl, box, new Color(210, 70, 50), boxTransform, RenderTypes.Shaded, isSelected: true);
			RenderHelper.Render(gl, sphere, new Color(60, 130, 220), sphereTransform, RenderTypes.Shaded);

			sceneRenderer.QueueSelectionOutline(box, new Color(255, 255, 255), boxTransform);
		}

		private static Vector3 OnSphere(double radius, double phi, double theta)
			=> new Vector3(
				radius * Math.Sin(phi) * Math.Cos(theta),
				radius * Math.Sin(phi) * Math.Sin(theta),
				radius * Math.Cos(phi));
	}
}
