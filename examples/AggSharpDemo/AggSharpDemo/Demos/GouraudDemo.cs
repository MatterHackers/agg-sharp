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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's gouraud.cpp: a "cube" of six Gouraud-shaded triangles around a draggable triangle, with
	/// dilation, linear gamma and opacity sliders to tune away the seams between adjacent anti-aliased
	/// triangles. Drag a corner, or inside the triangle to move it all; arrow keys nudge the first two corners.
	/// </summary>
	public class GouraudDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly double[] vertexX = { 57, 369, 143 };

		private readonly double[] vertexY = { 60, 170, 310 };

		private double dragDx;

		private double dragDy;

		// 0-2 drags that corner, 3 drags the whole triangle, -1 is no drag.
		private int dragIndex = -1;

		public GouraudDemo()
		{
			// gouraud.cpp runs with flip_y = true and gives its sliders !flip_y.
			this.DilationSlider = new SliderCtrl(5, 5, 400 - 5, 11, false) { Label = "Dilation={0:F2}" };
			this.GammaSlider = new SliderCtrl(5, 5 + 15, 400 - 5, 11 + 15, false) { Label = "Linear gamma={0:F2}" };
			this.AlphaSlider = new SliderCtrl(5, 5 + 30, 400 - 5, 11 + 30, false) { Label = "Opacity={0:F2}" };
			this.DilationSlider.Value = 0.175;
			this.GammaSlider.Value = 0.809;
			this.AlphaSlider.Value = 1.0;

			this.ctrls.Add(this.DilationSlider);
			this.ctrls.Add(this.GammaSlider);
			this.ctrls.Add(this.AlphaSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_dilation</c>: how far each triangle is grown before it is rasterized.</summary>
		public SliderCtrl DilationSlider { get; }

		/// <summary>C++ <c>m_gamma</c>: the end of the rasterizer's <c>gamma_linear(0, end)</c>.</summary>
		public SliderCtrl GammaSlider { get; }

		/// <summary>C++ <c>m_alpha</c>: the opacity of every corner colour.</summary>
		public SliderCtrl AlphaSlider { get; }

		public override string Name => "gouraud";

		public override string Category => "Color & Gradients";

		public override string Description => "Six Gouraud-shaded triangles. Tune dilation and gamma to hide the seams; drag a corner or the whole shape.";

		public override int Width => 400;

		public override int Height => 320;

		/// <summary>Moves corner <paramref name="index"/> (0 to 2) to (<paramref name="x"/>, <paramref name="y"/>).</summary>
		public void SetVertex(int index, double x, double y)
		{
			this.vertexX[index] = x;
			this.vertexY[index] = y;
			this.Invalidate();
		}

		/// <summary>The current position of corner <paramref name="index"/> (0 to 2).</summary>
		public Vector2 GetVertex(int index) => new Vector2(this.vertexX[index], this.vertexY[index]);

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			double[] x = this.vertexX;
			double[] y = this.vertexY;
			double xc = (x[0] + x[1] + x[2]) / 3.0;
			double yc = (y[0] + y[1] + y[2]) / 3.0;

			double x1 = ((x[1] + x[0]) / 2) - (xc - ((x[1] + x[0]) / 2));
			double y1 = ((y[1] + y[0]) / 2) - (yc - ((y[1] + y[0]) / 2));
			double x2 = ((x[2] + x[1]) / 2) - (xc - ((x[2] + x[1]) / 2));
			double y2 = ((y[2] + y[1]) / 2) - (yc - ((y[2] + y[1]) / 2));
			double x3 = ((x[0] + x[2]) / 2) - (xc - ((x[0] + x[2]) / 2));
			double y3 = ((y[0] + y[2]) / 2) - (yc - ((y[0] + y[2]) / 2));

			double alpha = this.AlphaSlider.Value;
			Color red = Rgba8.FromRgba(1, 0, 0, alpha);
			Color green = Rgba8.FromRgba(0, 1, 0, alpha);
			Color blue = Rgba8.FromRgba(0, 0, 1, alpha);
			Color white = Rgba8.FromRgba(1, 1, 1, alpha);
			Color black = Rgba8.FromRgba(0, 0, 0, alpha);

			// Three triangles meeting at the centre in white, then three outside the edges in black.
			var triangles = new (Color C1, Color C2, Color C3, double X1, double Y1, double X2, double Y2, double X3, double Y3)[]
			{
				(red, green, white, x[0], y[0], x[1], y[1], xc, yc),
				(green, blue, white, x[1], y[1], x[2], y[2], xc, yc),
				(blue, red, white, x[2], y[2], x[0], y[0], xc, yc),
				(red, green, black, x[0], y[0], x[1], y[1], x1, y1),
				(green, blue, black, x[1], y[1], x[2], y[2], x2, y2),
				(blue, red, black, x[2], y[2], x[0], y[0], x3, y3),
			};

			// Three kinds of surface, told apart by what they carry. The rasterizer is asked first so a GPU surface
			// is never asked for DestImage, which would make it allocate a full-screen CPU layer.
			Affine transform = graphics.GetTransform();
			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			if (rasterizer == null && graphics is IGouraudGraphics gouraudGraphics)
			{
				// A GPU surface: span_gouraud_rgba's own pixels, dilated and through the same gamma. The triangles
				// are in the current transform like any path, so under a transform that scales the dilation scales
				// with it (the software reference dilates after transforming).
				var spans = new List<span_gouraud_rgba>();
				foreach (var t in triangles)
				{
					var span = new span_gouraud_rgba();
					span.colors(t.C1, t.C2, t.C3);
					span.triangle(t.X1, t.Y1, t.X2, t.Y2, t.X3, t.Y3, this.DilationSlider.Value);
					spans.Add(span);
				}

				gouraudGraphics.FillGouraud(spans, new gamma_linear(0.0, this.GammaSlider.Value));
			}
			else if (rasterizer == null)
			{
				// A surface with neither a rasterizer nor IGouraudGraphics - not Graphics2DGpu, which carries it, but
				// any other Graphics2D - gets the same six triangles as per-vertex-coloured primitives: the shading
				// without the dilation and gamma.
				var vertices = new PosColorVertex[triangles.Length * 3];
				for (int i = 0; i < triangles.Length; i++)
				{
					var t = triangles[i];
					vertices[(i * 3) + 0] = new PosColorVertex(Transformed(transform, t.X1, t.Y1), t.C1);
					vertices[(i * 3) + 1] = new PosColorVertex(Transformed(transform, t.X2, t.Y2), t.C2);
					vertices[(i * 3) + 2] = new PosColorVertex(Transformed(transform, t.X3, t.Y3), t.C3);
				}

				graphics.DrawColoredPrimitives(DrawTopology.TriangleList, vertices);
			}
			else if (graphics.DestImage is IImageByte destination)
			{
				// The software reference: straight into the back buffer, byte-identical to C++.
				this.RenderSpans(destination, rasterizer, transform, triangles);
			}
			else
			{
				// A software surface with a rasterizer but no byte back buffer (LcdBufferGraphics2D): the
				// triangles go into a transparent demo-sized layer that is then drawn as an image. Close to the
				// reference, not byte-identical - the edges blend twice.
				var layer = new ImageBuffer(this.Width, this.Height);
				var layerRasterizer = new ScanlineRasterizer();
				layerRasterizer.SetVectorClipBox(0, 0, this.Width, this.Height);
				this.RenderSpans(layer, layerRasterizer, Affine.NewIdentity(), triangles);
				graphics.Render(layer, 0, 0);
			}

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			// C++ times 100 redraws on a right press and shows it in a message box; the port has no timing.
			if (this.ctrls.OnMouseDown(x, y, button) || !button.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			for (int i = 0; i < 3; i++)
			{
				double dx = x - this.vertexX[i];
				double dy = y - this.vertexY[i];
				if (Math.Sqrt((dx * dx) + (dy * dy)) < 10.0)
				{
					this.dragDx = dx;
					this.dragDy = dy;
					this.dragIndex = i;
					return;
				}
			}

			if (agg_math.point_in_triangle(this.vertexX[0], this.vertexY[0], this.vertexX[1], this.vertexY[1], this.vertexX[2], this.vertexY[2], x, y))
			{
				this.dragDx = x - this.vertexX[0];
				this.dragDy = y - this.vertexY[0];
				this.dragIndex = 3;
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseMove(x, y, flags))
			{
				return;
			}

			if (!flags.HasFlag(AggInputFlags.MouseLeft))
			{
				this.dragIndex = -1;
				return;
			}

			if (this.dragIndex == 3)
			{
				double newX = x - this.dragDx;
				double newY = y - this.dragDy;
				for (int i = 1; i < 3; i++)
				{
					this.vertexX[i] -= this.vertexX[0] - newX;
					this.vertexY[i] -= this.vertexY[0] - newY;
				}

				this.SetVertex(0, newX, newY);
			}
			else if (this.dragIndex >= 0)
			{
				this.SetVertex(this.dragIndex, x - this.dragDx, y - this.dragDy);
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			this.dragIndex = -1;
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			if (this.ctrls.OnKeyDown(key))
			{
				return;
			}

			// C++ on_key nudges the first two corners by a tenth of a pixel (the third stays put).
			double dx = key == Keys.Left ? -0.1 : key == Keys.Right ? 0.1 : 0;
			double dy = key == Keys.Up ? 0.1 : key == Keys.Down ? -0.1 : 0;
			for (int i = 0; i < 2; i++)
			{
				this.vertexX[i] += dx;
				this.vertexY[i] += dy;
			}

			this.Invalidate();
		}

		private static Vector2 Transformed(Affine transform, double x, double y)
		{
			transform.Transform(ref x, ref y);
			return new Vector2(x, y);
		}

		/// <summary>
		/// C++ <c>render_gouraud</c>: each triangle through <see cref="span_gouraud_rgba"/>, dilated, on the
		/// given rasterizer with <c>gamma_linear(0, gamma)</c>, which is put back to none afterwards for the
		/// ctrls.
		/// </summary>
		/// <remarks>
		/// This is the one place the demo reaches past <see cref="Graphics2D"/>'s fills: a span generator has
		/// no <see cref="Graphics2D"/> call. The corners go through the graphics transform first, so the span
		/// generator interpolates in the same space the rasterizer covers.
		/// </remarks>
		private void RenderSpans(IImageByte destination, ScanlineRasterizer rasterizer, Affine transform, (Color C1, Color C2, Color C3, double X1, double Y1, double X2, double Y2, double X3, double Y3)[] triangles)
		{
			var scanline = new scanline_unpacked_8();
			var scanlineRenderer = new ScanlineRenderer();
			var spanAllocator = new span_allocator();
			var spanGenerator = new span_gouraud_rgba();

			// ScanlineRasterizer has no way to read its gamma back (C++ apply_gamma is commented out in the
			// port), so it is put back to none - what every other fill in agg-sharp assumes - even if a
			// triangle throws.
			rasterizer.gamma(new gamma_linear(0.0, this.GammaSlider.Value));
			try
			{
				double dilation = this.DilationSlider.Value;
				foreach (var t in triangles)
				{
					Vector2 p1 = Transformed(transform, t.X1, t.Y1);
					Vector2 p2 = Transformed(transform, t.X2, t.Y2);
					Vector2 p3 = Transformed(transform, t.X3, t.Y3);
					spanGenerator.colors(t.C1, t.C2, t.C3);
					spanGenerator.triangle(p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y, dilation);
					rasterizer.reset();
					rasterizer.add_path(spanGenerator);
					scanlineRenderer.GenerateAndRender(rasterizer, scanline, destination, spanAllocator, spanGenerator);
				}
			}
			finally
			{
				rasterizer.gamma(new gamma_none());
			}

			destination.MarkImageChanged();
		}
	}
}
