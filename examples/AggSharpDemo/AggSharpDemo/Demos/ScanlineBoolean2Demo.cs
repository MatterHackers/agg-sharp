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
using System.Globalization;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's scanline_boolean2.cpp: boolean operations on complex shapes - Great Britain with a spiral or
	/// arrows, two simple paths, a closed stroke, or a spiral and a glyph. Each shape is stored as scanlines
	/// (<see cref="ScanlineStorageAa8"/> or <see cref="ScanlineStorageBin"/>), the two stores are combined
	/// (<see cref="ScanlineBooleanAlgebra"/>) into a third, and that one is drawn in translucent red. Dragging
	/// moves the spiral, the arrows or the first path.
	/// </summary>
	/// <remarks>
	/// C++ also prints how long the combine and the render took; that timing is left out (it differs every
	/// frame), and only its num_spans line is kept, where C++ puts it.
	/// <para>
	/// On the GPU the same rasterizers, stores and boolean run on the CPU and their scanlines' pixels are
	/// drawn through a <see cref="Graphics2DSpanImage"/>, so both frames hold the same pixels.
	/// </para>
	/// </remarks>
	public class ScanlineBoolean2Demo : AggDemo
	{
		private static readonly SboolOp[] Operations = { SboolOp.Or, SboolOp.And, SboolOp.Xor, SboolOp.XorSaddle, SboolOp.AMinusB, SboolOp.BMinusA };

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private double x;

		private double y;

		public ScanlineBoolean2Demo()
		{
			// scanline_boolean2.cpp runs with flip_y = true and gives its controls !flip_y.
			this.PolygonsRbox = new RboxCtrl(5.0, 5.0, 5.0 + 205.0, 110.0, false);
			this.FillRuleRbox = new RboxCtrl(200, 5.0, 200 + 105.0, 50.0, false);
			this.ScanlineTypeRbox = new RboxCtrl(300, 5.0, 300 + 115.0, 70.0, false);
			this.OperationRbox = new RboxCtrl(535.0, 5.0, 535.0 + 115.0, 145.0, false);

			foreach (string item in new[] { "None", "OR", "AND", "XOR Linear", "XOR Saddle", "A-B", "B-A" })
			{
				this.OperationRbox.AddItem(item);
			}

			this.OperationRbox.CurrentItem = 2;

			this.FillRuleRbox.AddItem("Even-Odd");
			this.FillRuleRbox.AddItem("Non Zero");
			this.FillRuleRbox.CurrentItem = 1;

			this.ScanlineTypeRbox.AddItem("scanline_p");
			this.ScanlineTypeRbox.AddItem("scanline_u");
			this.ScanlineTypeRbox.AddItem("scanline_bin");
			this.ScanlineTypeRbox.CurrentItem = 1;

			foreach (string item in new[] { "Two Simple Paths", "Closed Stroke", "Great Britain and Arrows", "Great Britain and Spiral", "Spiral and Glyph" })
			{
				this.PolygonsRbox.AddItem(item);
			}

			this.PolygonsRbox.CurrentItem = 3;

			// In C++ on_draw's order: the polygons and fill rule boxes overlap, so the order shows.
			this.ctrls.Add(this.PolygonsRbox);
			this.ctrls.Add(this.FillRuleRbox);
			this.ctrls.Add(this.ScanlineTypeRbox);
			this.ctrls.Add(this.OperationRbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			// C++ on_init: the middle of the window.
			this.x = this.Width / 2.0;
			this.y = this.Height / 2.0;
		}

		/// <summary>C++ <c>m_polygons</c>: which two shapes are combined.</summary>
		public RboxCtrl PolygonsRbox { get; }

		/// <summary>C++ <c>m_fill_rule</c>: even-odd or non-zero, for both shapes.</summary>
		public RboxCtrl FillRuleRbox { get; }

		/// <summary>C++ <c>m_scanline_type</c>: scanline_p8 or scanline_u8 (anti-aliased stores) or scanline_bin.</summary>
		public RboxCtrl ScanlineTypeRbox { get; }

		/// <summary>C++ <c>m_operation</c>: None, or the operation in <see cref="Operations"/> order.</summary>
		public RboxCtrl OperationRbox { get; }

		public override string Name => "scanline_boolean2";

		public override string Category => "Masks & Clipping";

		public override string Description => "Boolean operations on complex shapes - Great Britain, arrows, a spiral, a glyph - stored as scanlines and combined. Pick the shapes, the operation, the fill rule and the scanline type, and drag to move the spiral, arrows or path.";

		public override int Width => 655;

		public override int Height => 520;

		/// <summary>Where the moving shape is (C++ <c>m_x</c>, <c>m_y</c>); dragging sets it.</summary>
		public void MoveTo(double x, double y)
		{
			this.x = x;
			this.y = y;
			this.Invalidate();
		}

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			// C++ draws the controls first; the shapes go over them.
			this.ctrls.Render(graphics);

			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-screen CPU layer. The reference frame has no transform, so pixels are frame pixels.
			// Any other surface takes the same scanlines' exact pixels through a Graphics2DSpanImage.
			IImageByte destination = graphics.Rasterizer != null ? graphics.DestImage as IImageByte : null;
			Graphics2DSpanImage spans = destination == null ? new Graphics2DSpanImage(graphics, this.Width, this.Height) : null;
			IImageByte target = destination ?? new ImageClippingProxy(spans);
			Func<Color, IScanlineSink> solid = color => new SolidScanlineSink(target, color);
			Func<Color, IScanlineSink> binSolid = color => new BinSolidScanlineSink(target, color);

			var ras1 = new ScanlineRasterizer();
			var ras2 = new ScanlineRasterizer();
			var fillingRule = this.FillRuleRbox.CurrentItem != 0 ? Util.filling_rule_e.fill_non_zero : Util.filling_rule_e.fill_even_odd;
			ras1.filling_rule(fillingRule);
			ras2.filling_rule(fillingRule);

			// Every shape is drawn through a scanline_p8, as C++ render_sbool does.
			void Render(ScanlineRasterizer ras, Color color) => RenderScanlines(ras, new ScanlineCachePacked8(), solid(color));

			switch (this.PolygonsRbox.CurrentItem)
			{
				case 0:
					{
						double dx = this.x - (this.Width / 2) + 100;
						double dy = this.y - (this.Height / 2) + 100;
						var ps1 = new VertexStorage();
						ps1.MoveTo(dx + 140, dy + 145);
						ps1.LineTo(dx + 225, dy + 44);
						ps1.LineTo(dx + 296, dy + 219);
						ps1.ClosePolygon();

						ps1.LineTo(dx + 226, dy + 289);
						ps1.LineTo(dx + 82, dy + 292);

						ps1.MoveTo(dx + 220, dy + 222);
						ps1.LineTo(dx + 363, dy + 249);
						ps1.LineTo(dx + 265, dy + 331);

						ps1.MoveTo(dx + 242, dy + 243);
						ps1.LineTo(dx + 325, dy + 261);
						ps1.LineTo(dx + 268, dy + 309);

						ps1.MoveTo(dx + 259, dy + 259);
						ps1.LineTo(dx + 273, dy + 288);
						ps1.LineTo(dx + 298, dy + 266);

						var ps2 = new VertexStorage();
						ps2.MoveTo(100 + 32, 100 + 77);
						ps2.LineTo(100 + 473, 100 + 263);
						ps2.LineTo(100 + 351, 100 + 290);
						ps2.LineTo(100 + 354, 100 + 374);

						ras1.reset();
						ras1.add_path(ps1);
						Render(ras1, Rgba8.FromRgba(0, 0, 0, 0.1));

						ras2.reset();
						ras2.add_path(ps2);
						Render(ras2, Rgba8.FromRgba(0, 0.6, 0, 0.1));
					}

					break;

				case 1:
					{
						double dx = this.x - (this.Width / 2) + 100;
						double dy = this.y - (this.Height / 2) + 100;
						var ps1 = new VertexStorage();
						ps1.MoveTo(dx + 140, dy + 145);
						ps1.LineTo(dx + 225, dy + 44);
						ps1.LineTo(dx + 296, dy + 219);
						ps1.ClosePolygon();

						ps1.LineTo(dx + 226, dy + 289);
						ps1.LineTo(dx + 82, dy + 292);

						ps1.MoveTo(dx + 220 - 50, dy + 222);
						ps1.LineTo(dx + 363 - 50, dy + 249);
						ps1.LineTo(dx + 265 - 50, dy + 331);
						ps1.ClosePolygon();

						var ps2 = new VertexStorage();
						ps2.MoveTo(100 + 32, 100 + 77);
						ps2.LineTo(100 + 473, 100 + 263);
						ps2.LineTo(100 + 351, 100 + 290);
						ps2.LineTo(100 + 354, 100 + 374);
						ps2.ClosePolygon();

						ras1.reset();
						ras1.add_path(ps1);
						Render(ras1, Rgba8.FromRgba(0, 0, 0, 0.1));

						ras2.reset();
						ras2.add_path(new Stroke(ps2, 15.0));
						Render(ras2, Rgba8.FromRgba(0, 0.6, 0, 0.1));
					}

					break;

				case 2:
					{
						Affine mtx1 = Affine.NewTranslation(-1150, -1150) * Affine.NewScaling(2.0);
						Affine mtx2 = mtx1 * Affine.NewTranslation(this.x - (this.Width / 2), this.y - (this.Height / 2));
						var gbPoly = new VertexSourceApplyTransform(GreatBritainPolygon.MakeGbPoly(), mtx1);
						var arrows = new VertexSourceApplyTransform(GreatBritainPolygon.MakeArrows(), mtx2);

						ras2.add_path(gbPoly);
						Render(ras2, Rgba8.FromRgba(0.5, 0.5, 0, 0.1));

						ras1.add_path(new Stroke(gbPoly, 0.1));
						Render(ras1, Rgba8.FromRgba(0, 0, 0));

						ras2.add_path(arrows);
						Render(ras2, Rgba8.FromRgba(0.0, 0.5, 0.5, 0.1));

						ras1.reset();
						ras1.add_path(gbPoly);
					}

					break;

				case 3:
					{
						// C++ also multiplies in trans_affine_resizing(), which is identity until the window is resized.
						var gbPoly = new VertexSourceApplyTransform(GreatBritainPolygon.MakeGbPoly(), Affine.NewTranslation(-1150, -1150) * Affine.NewScaling(2.0));

						ras1.add_path(gbPoly);
						Render(ras1, Rgba8.FromRgba(0.5, 0.5, 0, 0.1));

						ras1.reset();
						ras1.add_path(new Stroke(gbPoly, 0.1));
						Render(ras1, Rgba8.FromRgba(0, 0, 0));

						ras2.reset();
						ras2.add_path(new Stroke(MakeSpiral(this.x, this.y, 10, 150, 30, 0.0), 15.0));
						Render(ras2, Rgba8.FromRgba(0.0, 0.5, 0.5, 0.1));

						ras1.reset();
						ras1.add_path(gbPoly);
					}

					break;

				case 4:
					{
						ras1.reset();
						ras1.add_path(new Stroke(MakeSpiral(this.x, this.y, 10, 150, 30, 0.0), 15.0));
						Render(ras1, Rgba8.FromRgba(0, 0, 0, 0.1));

						ras2.reset();
						ras2.add_path(new FlattenCurves(new VertexSourceApplyTransform(MakeGlyph(), Affine.NewScaling(4.0) * Affine.NewTranslation(220, 200))));
						Render(ras2, Rgba8.FromRgba(0, 0.6, 0, 0.1));
					}

					break;
			}

			if (this.OperationRbox.CurrentItem > 0)
			{
				this.RenderScanlineBoolean(ras1, ras2, solid, binSolid);
			}

			destination?.MarkImageChanged();
			spans?.Flush();
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button))
			{
				return;
			}

			if (button.HasFlag(AggInputFlags.MouseLeft))
			{
				this.MoveTo(x, y);
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseMove(x, y, flags))
			{
				return;
			}

			if (flags.HasFlag(AggInputFlags.MouseLeft))
			{
				this.MoveTo(x, y);
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		/// <summary>C++ render_scanlines: every scanline the generator makes, through the scanline, into the sink.</summary>
		internal static void RenderScanlines(IScanlineGenerator generator, IScanlineCache scanline, IScanlineSink sink)
		{
			if (generator.rewind_scanlines())
			{
				scanline.reset(generator.min_x(), generator.max_x());
				sink.prepare();
				while (generator.sweep_scanline(scanline))
				{
					sink.render(scanline);
				}
			}
		}

		/// <summary>C++ count_spans: the spans in every scanline the generator makes.</summary>
		private static int CountSpans(IScanlineGenerator generator, IScanlineCache scanline)
		{
			int count = 0;
			if (generator.rewind_scanlines())
			{
				scanline.reset(generator.min_x(), generator.max_x());
				while (generator.sweep_scanline(scanline))
				{
					count += scanline.num_spans();
				}
			}

			return count;
		}

		/// <summary>
		/// C++ spiral: from radius r1 out to r2, turning 4 degrees and growing step / 90 per vertex, as one open path.
		/// </summary>
		internal static VertexStorage MakeSpiral(double x, double y, double r1, double r2, double step, double startAngle)
		{
			double da = 4.0 * Math.PI / 180.0;
			double dr = step / 90.0;
			var spiral = new VertexStorage();
			double angle = startAngle;
			bool start = true;
			for (double r = r1; r <= r2; r += dr, angle += da)
			{
				if (start)
				{
					spiral.MoveTo(x + (Math.Cos(angle) * r), y + (Math.Sin(angle) * r));
					start = false;
				}
				else
				{
					spiral.LineTo(x + (Math.Cos(angle) * r), y + (Math.Sin(angle) * r));
				}
			}

			return spiral;
		}

		/// <summary>scanline_boolean2.cpp's glyph (an "a"), in font units; alpha_mask3.cpp draws the same one.</summary>
		internal static VertexStorage MakeGlyph()
		{
			var glyph = new VertexStorage();
			glyph.MoveTo(28.47, 6.45);
			glyph.Curve3(21.58, 1.12, 19.82, 0.29);
			glyph.Curve3(17.19, -0.93, 14.21, -0.93);
			glyph.Curve3(9.57, -0.93, 6.57, 2.25);
			glyph.Curve3(3.56, 5.42, 3.56, 10.60);
			glyph.Curve3(3.56, 13.87, 5.03, 16.26);
			glyph.Curve3(7.03, 19.58, 11.99, 22.51);
			glyph.Curve3(16.94, 25.44, 28.47, 29.64);
			glyph.LineTo(28.47, 31.40);
			glyph.Curve3(28.47, 38.09, 26.34, 40.58);
			glyph.Curve3(24.22, 43.07, 20.17, 43.07);
			glyph.Curve3(17.09, 43.07, 15.28, 41.41);
			glyph.Curve3(13.43, 39.75, 13.43, 37.60);
			glyph.LineTo(13.53, 34.77);
			glyph.Curve3(13.53, 32.52, 12.38, 31.30);
			glyph.Curve3(11.23, 30.08, 9.38, 30.08);
			glyph.Curve3(7.57, 30.08, 6.42, 31.35);
			glyph.Curve3(5.27, 32.62, 5.27, 34.81);
			glyph.Curve3(5.27, 39.01, 9.57, 42.53);
			glyph.Curve3(13.87, 46.04, 21.63, 46.04);
			glyph.Curve3(27.59, 46.04, 31.40, 44.04);
			glyph.Curve3(34.28, 42.53, 35.64, 39.31);
			glyph.Curve3(36.52, 37.21, 36.52, 30.71);
			glyph.LineTo(36.52, 15.53);
			glyph.Curve3(36.52, 9.13, 36.77, 7.69);
			glyph.Curve3(37.01, 6.25, 37.57, 5.76);
			glyph.Curve3(38.13, 5.27, 38.87, 5.27);
			glyph.Curve3(39.65, 5.27, 40.23, 5.62);
			glyph.Curve3(41.26, 6.25, 44.19, 9.18);
			glyph.LineTo(44.19, 6.45);
			glyph.Curve3(38.72, -0.88, 33.74, -0.88);
			glyph.Curve3(31.35, -0.88, 29.93, 0.78);
			glyph.Curve3(28.52, 2.44, 28.47, 6.45);
			glyph.ClosePolygon();

			glyph.MoveTo(28.47, 9.62);
			glyph.LineTo(28.47, 26.66);
			glyph.Curve3(21.09, 23.73, 18.95, 22.51);
			glyph.Curve3(15.09, 20.36, 13.43, 18.02);
			glyph.Curve3(11.77, 15.67, 11.77, 12.89);
			glyph.Curve3(11.77, 9.38, 13.87, 7.06);
			glyph.Curve3(15.97, 4.74, 18.70, 4.74);
			glyph.Curve3(22.41, 4.74, 28.47, 9.62);
			glyph.ClosePolygon();
			return glyph;
		}

		/// <summary>
		/// C++ render_scanline_boolean: both shapes into scanline stores, the operation on the stores into a third,
		/// that drawn in translucent red, and the span count.
		/// </summary>
		private void RenderScanlineBoolean(ScanlineRasterizer ras1, ScanlineRasterizer ras2, Func<Color, IScanlineSink> solid, Func<Color, IScanlineSink> binSolid)
		{
			SboolOp op = Operations[this.OperationRbox.CurrentItem - 1];
			Color resultColor = Rgba8.FromRgba(0.5, 0.0, 0, 0.5);
			int numSpans;
			if (this.ScanlineTypeRbox.CurrentItem == 2)
			{
				var storage = new ScanlineStorageBin();
				var storage1 = new ScanlineStorageBin();
				var storage2 = new ScanlineStorageBin();
				var sl = new scanline_bin();
				RenderScanlines(ras1, sl, storage1);
				RenderScanlines(ras2, sl, storage2);
				ScanlineBooleanAlgebra.CombineShapesBin(op, storage1, storage2, new scanline_bin(), new scanline_bin(), sl, storage);
				RenderScanlines(storage, sl, binSolid(resultColor));
				numSpans = CountSpans(storage, sl);
			}
			else
			{
				// scanline_p8 or scanline_u8, one type for every scanline, as C++ instantiates it.
				Func<IScanlineCache> newScanline = this.ScanlineTypeRbox.CurrentItem == 0
					? () => new ScanlineCachePacked8()
					: () => new scanline_unpacked_8();
				var storage = new ScanlineStorageAa8();
				var storage1 = new ScanlineStorageAa8();
				var storage2 = new ScanlineStorageAa8();
				IScanlineCache sl = newScanline();
				RenderScanlines(ras1, sl, storage1);
				RenderScanlines(ras2, sl, storage2);
				ScanlineBooleanAlgebra.CombineShapesAa(op, storage1, storage2, newScanline(), newScanline(), sl, storage);
				RenderScanlines(storage, sl, solid(resultColor));
				numSpans = CountSpans(storage, sl);
			}

			// C++ prints "Combine=%.3fms\n\nRender=%.3fms\n\nnum_spans=%d" 8 high from (420, 40), stroked 1 wide with
			// round caps through ras1 (so with its fill rule). The timings are left out; their lines stay blank so
			// num_spans sits where C++ puts it, at the window's bottom edge.
			var outline = new VertexStorage();
#pragma warning disable CS0618 // gsv_text is obsolete for UI text; C++ draws with exactly this font.
			var text = new gsv_text();
#pragma warning restore CS0618
			text.size(8.0, 0.0);
			text.start_point(420, 40);
			text.text("\n\n\n\nnum_spans=" + numSpans.ToString(CultureInfo.InvariantCulture));
			foreach (VertexData vertex in text.Vertices())
			{
				if (vertex.IsStop)
				{
					break;
				}

				outline.Add(vertex.Position.X, vertex.Position.Y, vertex.Command);
			}

			ras1.add_path(new Stroke(outline, 1.0) { LineCap = LineCap.Round });
			RenderScanlines(ras1, new ScanlineCachePacked8(), solid(Rgba8.FromRgba(0.0, 0.0, 0.0)));
		}
	}
}
