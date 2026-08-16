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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// The drawing for the 2D golden suite, held apart from the tests because two suites render it: the
	/// classic D3D11 path that captured the goldens, and the wgpu path being held to them. One copy of the
	/// scene is the whole point - a scene that drifted between backends would compare two different pictures
	/// and call the difference a port bug.
	/// </summary>
	public static class Golden2DScenes
	{
		/// <summary>Anti-aliased strokes at every awkward angle, several widths, plus exact horizontal and
		/// vertical runs where half-pixel conventions show up first.</summary>
		public static void Lines(Graphics2DGpu graphics, GL gl)
		{
			for (int step = 0; step <= 12; step++)
			{
				double angle = step * Math.PI / 12;
				double width = 0.5 + (step * 0.35);
				var color = new Color(20 + (step * 18), 60, 200 - (step * 14));

				graphics.Line(
					256 - (Math.Cos(angle) * 150),
					300 - (Math.Sin(angle) * 70),
					256 + (Math.Cos(angle) * 150),
					300 + (Math.Sin(angle) * 70),
					color,
					width);
			}

			// Whole-pixel horizontal and vertical runs, and the same runs at a half-pixel offset.
			graphics.Line(20, 40, 492, 40, Color.Black, 1);
			graphics.Line(20, 60.5, 492, 60.5, Color.Black, 1);
			graphics.Line(40, 20, 40, 180, Color.Black, 1);
			graphics.Line(60.5, 20, 60.5, 180, Color.Black, 1);

			// A stroked open curve, which goes through the stroke generator - joins and caps - rather
			// than Line. Flattened explicitly: the tessellator does not subdivide curve commands, so an
			// unflattened path would silently stroke its control polygon instead.
			var path = new VertexStorage();
			path.MoveTo(120, 90);
			path.Curve4(200, 200, 300, 30, 400, 150);
			graphics.Render(new Stroke(new FlattenCurves(path), 3), Color.DarkGray);
		}

		/// <summary>Filled paths: a self-intersecting star (non-zero winding), a hole-in-a-square built
		/// from two contours, and an ellipse - the three fill shapes the tessellator treats differently.</summary>
		public static void FilledPaths(Graphics2DGpu graphics, GL gl)
		{
			var star = new VertexStorage();
			for (int point = 0; point < 10; point++)
			{
				double angle = point * Math.PI * 2 / 10;
				double radius = point == 0 || point % 2 == 0 ? 80 : 32;
				double x = 110 + (Math.Cos(angle) * radius);
				double y = 270 + (Math.Sin(angle) * radius);
				if (point == 0)
				{
					star.MoveTo(x, y);
				}
				else
				{
					star.LineTo(x, y);
				}
			}

			star.ClosePolygon();
			graphics.Render(star, new Color(220, 60, 40));

			var square = new VertexStorage();
			square.MoveTo(240, 210);
			square.LineTo(370, 210);
			square.LineTo(370, 340);
			square.LineTo(240, 340);
			square.ClosePolygon();
			square.MoveTo(270, 240);
			square.LineTo(270, 310);
			square.LineTo(340, 310);
			square.LineTo(340, 240);
			square.ClosePolygon();
			graphics.Render(square, new Color(40, 110, 200));

			graphics.Render(new Ellipse(430, 275, 60, 40), new Color(30, 160, 90, 160));

			// Sub-pixel placement: the same small circle stepped by a quarter pixel each time.
			for (int step = 0; step < 8; step++)
			{
				graphics.Render(new Ellipse(60 + (step * 30) + (step * 0.25), 90, 11, 11), new Color(80, 80, 80));
			}
		}

		/// <summary>Rounded rectangles - the widget chrome shape - at several radii, including the
		/// degenerate zero radius and a radius large enough to make the shape a stadium.</summary>
		public static void RoundedRects(Graphics2DGpu graphics, GL gl)
		{
			double[] radii = { 0, 2, 6, 14, 30 };
			for (int index = 0; index < radii.Length; index++)
			{
				double top = 350 - (index * 66);
				graphics.Render(
					new RoundedRect(30, top - 50, 230, top, radii[index]),
					new Color(60 + (index * 35), 90, 190 - (index * 25)));

				graphics.Render(
					new Stroke(new RoundedRect(270, top - 50, 480, top, radii[index]), 2),
					new Color(20, 20, 20));
			}
		}

		/// <summary>
		/// Per-vertex colour interpolation through immediate mode - the shape 2D widgets that draw their own
		/// gradients use (colour pickers, sliders). Nothing on <see cref="Graphics2D"/> expresses a gradient,
		/// so this deliberately drops to the <see cref="GL"/> facade the way those widgets do.
		/// </summary>
		public static void Gradients(Graphics2DGpu graphics, GL gl)
		{
			graphics.PushOrthoProjection();
			gl.Disable(EnableCap.Texture2D);
			gl.Disable(EnableCap.Lighting);
			gl.Disable(EnableCap.DepthTest);
			gl.Enable(EnableCap.Blend);
			gl.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

			// Opaque horizontal ramp.
			gl.Begin(BeginMode.TriangleStrip);
			gl.Color4((byte)255, (byte)0, (byte)0, (byte)255);
			gl.Vertex2(40, 220);
			gl.Color4((byte)255, (byte)0, (byte)0, (byte)255);
			gl.Vertex2(40, 350);
			gl.Color4((byte)0, (byte)80, (byte)255, (byte)255);
			gl.Vertex2(472, 220);
			gl.Color4((byte)0, (byte)80, (byte)255, (byte)255);
			gl.Vertex2(472, 350);
			gl.End();

			// Alpha ramp over the white background, so the blend is part of what is captured.
			gl.Begin(BeginMode.TriangleStrip);
			gl.Color4((byte)10, (byte)140, (byte)40, (byte)0);
			gl.Vertex2(40, 60);
			gl.Color4((byte)10, (byte)140, (byte)40, (byte)0);
			gl.Vertex2(40, 190);
			gl.Color4((byte)10, (byte)140, (byte)40, (byte)255);
			gl.Vertex2(472, 60);
			gl.Color4((byte)10, (byte)140, (byte)40, (byte)255);
			gl.Vertex2(472, 190);
			gl.End();

			// A three-colour fan, where the interpolation is not axis aligned.
			gl.Begin(BeginMode.Triangles);
			gl.Color4((byte)255, (byte)255, (byte)0, (byte)255);
			gl.Vertex2(256, 30);
			gl.Color4((byte)255, (byte)0, (byte)255, (byte)255);
			gl.Vertex2(150, 200);
			gl.Color4((byte)0, (byte)255, (byte)255, (byte)255);
			gl.Vertex2(362, 200);
			gl.End();

			graphics.PopOrthoProjection();
		}

		/// <summary>Textured blits: unscaled, magnified, minified and rotated, plus an alpha image over a
		/// filled background - the four sampling cases the port has to reproduce.</summary>
		public static void ImageBlits(Graphics2DGpu graphics, GL gl)
		{
			var source = BuildTestPattern(32, 32);

			graphics.FillRectangle(20, 210, 250, 360, new Color(230, 230, 190));

			graphics.Render(source, 30, 300);
			graphics.Render(source, 90, 300, 0, 3, 3);
			graphics.Render(source, 30, 230, 0, 0.5, 0.5);
			graphics.Render(source, 200, 300, Math.PI / 6, 2, 2);

			var translucent = BuildTestPattern(32, 32);
			var buffer = translucent.GetBuffer();
			for (int index = 3; index < buffer.Length; index += 4)
			{
				buffer[index] = 110;
			}

			translucent.MarkImageChanged();

			graphics.FillRectangle(300, 210, 480, 360, new Color(40, 40, 120));
			graphics.Render(translucent, 320, 300, 0, 2, 2);

			// Fractional destination placement, where the sampler's half-texel convention shows.
			graphics.Render(source, 40.5, 100, 0, 2, 2);
			graphics.Render(source, 160.25, 100, 0, 2, 2);
			graphics.Render(source, 280.75, 100, 0, 2, 2);
		}

		/// <summary>The <see cref="Graphics2D"/> transform stack: nested pushes with rotation, scale and
		/// translation applied to the same shape.</summary>
		public static void Transforms(Graphics2DGpu graphics, GL gl)
		{
			var arrow = new VertexStorage();
			arrow.MoveTo(0, -18);
			arrow.LineTo(70, -18);
			arrow.LineTo(70, -34);
			arrow.LineTo(104, 0);
			arrow.LineTo(70, 34);
			arrow.LineTo(70, 18);
			arrow.LineTo(0, 18);
			arrow.ClosePolygon();

			for (int step = 0; step < 8; step++)
			{
				graphics.PushTransform();
				var transform = Affine.NewIdentity();
				transform *= Affine.NewScaling(0.4 + (step * 0.09));
				transform *= Affine.NewRotation(step * Math.PI / 4);
				transform *= Affine.NewTranslation(256, 192);
				graphics.SetTransform(graphics.GetTransform() * transform);
				graphics.Render(arrow, new Color(30 + (step * 25), 140, 220 - (step * 22)));
				graphics.PopTransform();
			}

			// A nested push, to prove the stack restores rather than merely resets.
			graphics.PushTransform();
			graphics.SetTransform(graphics.GetTransform() * Affine.NewTranslation(60, 60));
			graphics.PushTransform();
			graphics.SetTransform(graphics.GetTransform() * Affine.NewScaling(0.5));
			graphics.Render(new RoundedRect(0, 0, 120, 60, 8), new Color(0, 0, 0, 120));
			graphics.PopTransform();
			graphics.Render(new Stroke(new RoundedRect(0, 0, 120, 60, 8), 2), Color.Black);
			graphics.PopTransform();
		}

		/// <summary>
		/// A deterministic, high-frequency pattern: every pixel is a pure function of its coordinates, so the
		/// texture upload and sampling are what is under test rather than the source image.
		/// </summary>
		private static ImageBuffer BuildTestPattern(int width, int height)
		{
			var image = new ImageBuffer(width, height, 32, new BlenderBGRA());
			var buffer = image.GetBuffer();

			for (int y = 0; y < height; y++)
			{
				int offset = image.GetBufferOffsetY(y);
				for (int x = 0; x < width; x++)
				{
					bool checker = ((x / 4) + (y / 4)) % 2 == 0;
					buffer[offset + (x * 4) + 0] = (byte)(checker ? 255 - (x * 8) : 30);
					buffer[offset + (x * 4) + 1] = (byte)(checker ? y * 8 : 200);
					buffer[offset + (x * 4) + 2] = (byte)(checker ? 60 : x * 8);
					buffer[offset + (x * 4) + 3] = 255;
				}
			}

			image.MarkImageChanged();
			return image;
		}
	}
}
