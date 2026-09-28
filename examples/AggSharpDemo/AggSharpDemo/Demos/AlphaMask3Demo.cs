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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's alpha_mask3.cpp: an alpha mask used as a polygon clipper. Both shapes of a pair are drawn faintly,
	/// then the first is rendered into a gray8 mask (white on black for AND, black on white for SUB) and the second
	/// is drawn in translucent red through it, so the red is the second shape clipped to - or cut by - the first.
	/// The shapes are those of scanline_boolean2; dragging moves the spiral, the arrows or the first path.
	/// </summary>
	/// <remarks>
	/// C++ also prints how long making the mask and rendering through it took; that timing is left out (it
	/// differs every frame).
	/// <para>
	/// The GPU path renders the same gray8 mask and draws the second shape through it with
	/// <see cref="IAlphaMaskGraphics.DrawMasked"/>; the shape's edges are the GPU's anti-aliasing, not AGG's. A
	/// surface with neither a byte back buffer nor alpha masks draws only the faint shapes, without the red result.
	/// </para>
	/// </remarks>
	public class AlphaMask3Demo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private double x;

		private double y;

		public AlphaMask3Demo()
		{
			// alpha_mask3.cpp runs with flip_y = true and gives its controls !flip_y.
			this.PolygonsRbox = new RboxCtrl(5.0, 5.0, 5.0 + 205.0, 110.0, false);
			this.OperationRbox = new RboxCtrl(555.0, 5.0, 555.0 + 80.0, 55.0, false);

			this.OperationRbox.AddItem("AND");
			this.OperationRbox.AddItem("SUB");
			this.OperationRbox.CurrentItem = 0;

			foreach (string item in new[] { "Two Simple Paths", "Closed Stroke", "Great Britain and Arrows", "Great Britain and Spiral", "Spiral and Glyph" })
			{
				this.PolygonsRbox.AddItem(item);
			}

			this.PolygonsRbox.CurrentItem = 3;

			// In C++ on_draw's order.
			this.ctrls.Add(this.PolygonsRbox);
			this.ctrls.Add(this.OperationRbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			// C++ on_init: the middle of the window.
			this.x = this.Width / 2.0;
			this.y = this.Height / 2.0;
		}

		/// <summary>C++ <c>m_polygons</c>: which two shapes are drawn.</summary>
		public RboxCtrl PolygonsRbox { get; }

		/// <summary>C++ <c>m_operation</c>: AND (draw inside the first shape) or SUB (draw outside it).</summary>
		public RboxCtrl OperationRbox { get; }

		public override string Name => "alpha_mask3";

		public override string Category => "Vector Graphics";

		public override string Description => "An alpha mask used as a polygon clipper: the red shape is drawn only inside (AND) or only outside (SUB) the other one. Pick the shapes and the operation, and drag to move the spiral, arrows or path.";

		public override int Width => 640;

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

			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-screen CPU layer. The reference frame has no transform, so pixels are frame pixels.
			IImageByte destination = graphics.Rasterizer != null ? graphics.DestImage as IImageByte : null;
			IImageByte clipped = destination != null ? new ImageClippingProxy(destination) : null;

			// C++ draws everything through the platform's one rasterizer (no clip box) and a scanline_p8.
			var ras = new ScanlineRasterizer();
			void Render(Color color) => ScanlineBoolean2Demo.RenderScanlines(
				ras,
				new ScanlineCachePacked8(),
				clipped != null ? new SolidScanlineSink(clipped, color) : new RectangleScanlineSink(graphics, color));

			IVertexSource maskShape;
			IVertexSource drawnShape;
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

						// Not scanline_boolean2's order: alpha_mask3 turns this contour the other way.
						ps1.MoveTo(dx + 242, dy + 243);
						ps1.LineTo(dx + 268, dy + 309);
						ps1.LineTo(dx + 325, dy + 261);

						ps1.MoveTo(dx + 259, dy + 259);
						ps1.LineTo(dx + 273, dy + 288);
						ps1.LineTo(dx + 298, dy + 266);

						ras.reset();
						ras.add_path(ps1);
						Render(Rgba8.FromRgba(0, 0, 0, 0.1));

						ras.reset();
						ras.add_path(MakeOpenPath(false));
						Render(Rgba8.FromRgba(0, 0.6, 0, 0.1));

						maskShape = ps1;
						drawnShape = MakeOpenPath(false);
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
						ps1.LineTo(dx + 265 - 50, dy + 331);
						ps1.LineTo(dx + 363 - 50, dy + 249);
						ps1.ClosePolygon(FlagsAndCommand.FlagCCW);

						// A stroke 10 wide, where scanline_boolean2's is 15.
						var stroke = new Stroke(MakeOpenPath(true), 10.0);

						ras.reset();
						ras.add_path(ps1);
						Render(Rgba8.FromRgba(0, 0, 0, 0.1));

						ras.reset();
						ras.add_path(stroke);
						Render(Rgba8.FromRgba(0, 0.6, 0, 0.1));

						maskShape = ps1;
						drawnShape = stroke;
					}

					break;

				case 2:
					{
						Affine mtx1 = Affine.NewTranslation(-1150, -1150) * Affine.NewScaling(2.0);
						Affine mtx2 = mtx1 * Affine.NewTranslation(this.x - (this.Width / 2), this.y - (this.Height / 2));
						var gbPoly = new VertexSourceApplyTransform(GreatBritainPolygon.MakeGbPoly(), mtx1);
						var arrows = new VertexSourceApplyTransform(GreatBritainPolygon.MakeArrows(), mtx2);

						// C++ adds each path without a reset: add_path resets a rasterizer that has been swept.
						ras.add_path(gbPoly);
						Render(Rgba8.FromRgba(0.5, 0.5, 0, 0.1));

						ras.add_path(new Stroke(gbPoly, 0.1));
						Render(Rgba8.FromRgba(0, 0, 0));

						ras.add_path(arrows);
						Render(Rgba8.FromRgba(0.0, 0.5, 0.5, 0.1));

						maskShape = gbPoly;
						drawnShape = arrows;
					}

					break;

				case 3:
					{
						var gbPoly = new VertexSourceApplyTransform(GreatBritainPolygon.MakeGbPoly(), Affine.NewTranslation(-1150, -1150) * Affine.NewScaling(2.0));
						var spiral = new Stroke(ScanlineBoolean2Demo.MakeSpiral(this.x, this.y, 10, 150, 30, 0.0), 15.0);

						ras.add_path(gbPoly);
						Render(Rgba8.FromRgba(0.5, 0.5, 0, 0.1));

						ras.add_path(new Stroke(gbPoly, 0.1));
						Render(Rgba8.FromRgba(0, 0, 0));

						ras.add_path(spiral);
						Render(Rgba8.FromRgba(0.0, 0.5, 0.5, 0.1));

						maskShape = gbPoly;
						drawnShape = spiral;
					}

					break;

				default:
					{
						var spiral = new Stroke(ScanlineBoolean2Demo.MakeSpiral(this.x, this.y, 10, 150, 30, 0.0), 15.0);
						var glyph = new FlattenCurves(new VertexSourceApplyTransform(ScanlineBoolean2Demo.MakeGlyph(), Affine.NewScaling(4.0) * Affine.NewTranslation(220, 200)));

						ras.reset();
						ras.add_path(spiral);
						Render(Rgba8.FromRgba(0, 0, 0, 0.1));

						ras.reset();
						ras.add_path(glyph);
						Render(Rgba8.FromRgba(0, 0.6, 0, 0.1));

						maskShape = spiral;
						drawnShape = glyph;
					}

					break;
			}

			Color resultColor = Rgba8.FromRgba(0.5, 0.0, 0, 0.5);
			if (destination != null)
			{
				ImageBuffer mask = this.GenerateMask(ras, maskShape);
				ras.reset();
				ras.add_path(drawnShape);
				var masked = new ImageClippingProxy(new AlphaMaskAdaptor(destination, new AlphaMaskByteUnclipped(mask, 1, 0)));
				ScanlineBoolean2Demo.RenderScanlines(ras, new ScanlineCachePacked8(), new SolidScanlineSink(masked, resultColor));
				destination.MarkImageChanged();
			}
			else if (graphics is IAlphaMaskGraphics maskGraphics)
			{
				// The mask is in frame pixels, which DrawMasked places through the demo's transform.
				ImageBuffer mask = this.GenerateMask(ras, maskShape);
				maskGraphics.DrawMasked(mask, () => graphics.Render(drawnShape, resultColor));
			}

			this.ctrls.Render(graphics);
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

		/// <summary>The fixed path of "Two Simple Paths" and "Closed Stroke" (closed for the stroke).</summary>
		private static VertexStorage MakeOpenPath(bool closed)
		{
			var ps2 = new VertexStorage();
			ps2.MoveTo(100 + 32, 100 + 77);
			ps2.LineTo(100 + 473, 100 + 263);
			ps2.LineTo(100 + 351, 100 + 290);
			ps2.LineTo(100 + 354, 100 + 374);
			if (closed)
			{
				ps2.ClosePolygon();
			}

			return ps2;
		}

		/// <summary>
		/// alpha_mask3.cpp generate_alpha_mask: the shape rendered into a gray8 buffer the size of the demo - white
		/// on black for AND, black on white for SUB - through the same rasterizer and a scanline_p8.
		/// </summary>
		private ImageBuffer GenerateMask(ScanlineRasterizer ras, IVertexSource shape)
		{
			// C++ pixfmt_sgray8 blends with gray8::lerp, which BlenderGrayExact is.
			var mask = new ImageBuffer(this.Width, this.Height, 8, new BlenderGrayExact(1));
			bool and = this.OperationRbox.CurrentItem == 0;
			if (!and)
			{
				// A new buffer is already cleared to 0, AND's background.
				new ImageClippingProxy(mask).clear(Color.White);
			}

			ras.add_path(shape);
			ScanlineBoolean2Demo.RenderScanlines(ras, new ScanlineCachePacked8(), new SolidScanlineSink(new ImageClippingProxy(mask), and ? Color.White : Color.Black));
			return mask;
		}
	}
}
