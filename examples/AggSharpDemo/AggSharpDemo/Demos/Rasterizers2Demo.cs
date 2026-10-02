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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's rasterizers2.cpp: one spiral drawn five ways - aliased Bresenham lines at whole-pixel and at subpixel
	/// accuracy (renderer_primitives), Maxim's anti-aliased outline (rasterizer_outline_aa), conv_stroke through the
	/// scanline rasterizer, and an image pattern (a chain) laid along the line (renderer_outline_image).
	/// </summary>
	/// <remarks>
	/// The software mode is the C++ render, byte for byte: everything but the controls is drawn into a premultiplied
	/// BGR canvas, as C++ draws through pixfmt_bgr24_pre. The outline renderers write pixels themselves; any other
	/// surface - the GPU - takes those same pixels as rectangles through a <see cref="Graphics2DSpanImage"/>. The "Test Performance" timing is left out, as the
	/// other ports leave their timers out.
	/// </remarks>
	public class Rasterizers2Demo : AggDemo
	{
		/// <summary>
		/// C++ <c>pixmap_chain</c>: 16 x 7 ARGB, a chain link. Its bytes are sRGB, which C++ converts to linear
		/// (<c>rgba(srgba8)</c>) before premultiplying.
		/// </summary>
		private static readonly uint[] PixmapChain =
		{
			0x00ffffff, 0x00ffffff, 0x00ffffff, 0x00ffffff, 0xb4c29999, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xb4c29999, 0x00ffffff, 0x00ffffff, 0x00ffffff, 0x00ffffff,
			0x00ffffff, 0x00ffffff, 0x0cfbf9f9, 0xff9a5757, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xb4c29999, 0x00ffffff, 0x00ffffff, 0x00ffffff,
			0x00ffffff, 0x5ae0cccc, 0xffa46767, 0xff660000, 0xff975252, 0x7ed4b8b8, 0x5ae0cccc, 0x5ae0cccc, 0x5ae0cccc, 0x5ae0cccc, 0xa8c6a0a0, 0xff7f2929, 0xff670202, 0x9ecaa6a6, 0x5ae0cccc, 0x00ffffff,
			0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xa4c7a2a2, 0x3affff00, 0x3affff00, 0xff975151, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000,
			0x00ffffff, 0x5ae0cccc, 0xffa46767, 0xff660000, 0xff954f4f, 0x7ed4b8b8, 0x5ae0cccc, 0x5ae0cccc, 0x5ae0cccc, 0x5ae0cccc, 0xa8c6a0a0, 0xff7f2929, 0xff670202, 0x9ecaa6a6, 0x5ae0cccc, 0x00ffffff,
			0x00ffffff, 0x00ffffff, 0x0cfbf9f9, 0xff9a5757, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xb4c29999, 0x00ffffff, 0x00ffffff, 0x00ffffff,
			0x00ffffff, 0x00ffffff, 0x00ffffff, 0x00ffffff, 0xb4c29999, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xb4c29999, 0x00ffffff, 0x00ffffff, 0x00ffffff, 0x00ffffff,
		};

		private static readonly Color LineColor = Rgba8.FromRgba(0.4, 0.3, 0.1);

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private ImageBuffer canvas;

		public Rasterizers2Demo()
		{
			// rasterizers2.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.StepSlider = new SliderCtrl(10.0, 10.0 + 4.0, 150.0, 10.0 + 8.0 + 4.0, false) { Label = "Step={0:F2}" };
			this.StepSlider.SetRange(0.0, 2.0);
			this.StepSlider.Value = 0.1;

			this.WidthSlider = new SliderCtrl(150.0 + 10.0, 10.0 + 4.0, 400 - 10.0, 10.0 + 8.0 + 4.0, false) { Label = "Width={0:F2}" };
			this.WidthSlider.SetRange(0.0, 14.0);
			this.WidthSlider.Value = 3.0;

			this.TestCbox = new CboxCtrl(10.0, 10.0 + 4.0 + 16.0, "Test Performance");
			this.RotateCbox = new CboxCtrl(130 + 10.0, 10.0 + 4.0 + 16.0, "Rotate");
			this.AccurateJoinsCbox = new CboxCtrl(200 + 10.0, 10.0 + 4.0 + 16.0, "Accurate Joins");
			this.ScalePatternCbox = new CboxCtrl(310 + 10.0, 10.0 + 4.0 + 16.0, "Scale Pattern") { Checked = true };

			this.ctrls.Add(this.StepSlider);
			this.ctrls.Add(this.WidthSlider);
			foreach (CboxCtrl cbox in new[] { this.TestCbox, this.RotateCbox, this.AccurateJoinsCbox, this.ScalePatternCbox })
			{
				cbox.SetTextSize(9.0, 7.0);
				this.ctrls.Add(cbox);
			}

			this.ctrls.Changed += (s, e) =>
			{
				// C++ on_ctrl_change: Rotate animates.
				this.WaitMode = !this.RotateCbox.Checked;
				this.Invalidate();
			};
		}

		/// <summary>C++ <c>m_step</c>: how many degrees the spirals turn each frame while Rotate is on, 0 to 2.</summary>
		public SliderCtrl StepSlider { get; }

		/// <summary>C++ <c>m_width</c>: the line width of the anti-aliased, scanline and pattern lanes, 0 to 14.</summary>
		public SliderCtrl WidthSlider { get; }

		/// <summary>C++ <c>m_test</c>: times each lane in C++; shown but not acted on here.</summary>
		public CboxCtrl TestCbox { get; }

		/// <summary>C++ <c>m_rotate</c>: turns the spirals continuously.</summary>
		public CboxCtrl RotateCbox { get; }

		/// <summary>C++ <c>m_accurate_joins</c>: miter-accurate joins for the anti-aliased outline instead of round.</summary>
		public CboxCtrl AccurateJoinsCbox { get; }

		/// <summary>C++ <c>m_scale_pattern</c>: scales the chain to the line width instead of drawing it 7 pixels tall.</summary>
		public CboxCtrl ScalePatternCbox { get; }

		/// <summary>C++ <c>m_start_angle</c>, in degrees: where the spirals start.</summary>
		public double StartAngle { get; set; }

		public override string Name => "rasterizers2";

		public override string Category => "Rendering";

		public override string Description => "One spiral drawn five ways: aliased Bresenham lines, anti-aliased outlines, the scanline rasterizer and an image pattern laid along the line. Change the width, or turn on Rotate.";

		public override int Width => 500;

		public override int Height => 450;

		public override void OnIdle()
		{
			// C++ on_idle.
			this.StartAngle += this.StepSlider.Value;
			if (this.StartAngle > 360.0)
			{
				this.StartAngle -= 360.0;
			}

			this.Invalidate();
		}

		public override void Draw(Graphics2D graphics)
		{
			if (graphics is ImageGraphics2D && graphics.DestImage is IImageByte)
			{
				this.DrawSoftware(graphics);
			}
			else
			{
				this.DrawSpans(graphics);
			}

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseDown(x, y, button);
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			this.ctrls.OnMouseMove(x, y, flags);
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		/// <summary>C++ <c>spiral</c>: from radius 5 to 70, 8 degrees and 8/45 of a pixel further out each vertex.</summary>
		private VertexStorage Spiral(double x, double y, bool roundOff)
		{
			var path = new VertexStorage();
			double angle = this.StartAngle * Math.PI / 180.0;
			double da = 8.0 * Math.PI / 180.0;
			double dr = 8 / 45.0;
			for (double r = 5; r <= 70; r += dr, angle += da)
			{
				double vx = x + (Math.Cos(angle) * r);
				double vy = y + (Math.Sin(angle) * r);
				if (roundOff)
				{
					// C++ roundoff, the conv_transform the pixel-accuracy lane goes through.
					vx = Math.Floor(vx);
					vy = Math.Floor(vy);
				}

				if (path.Count == 0)
				{
					path.MoveTo(vx, vy);
				}
				else
				{
					path.LineTo(vx, vy);
				}
			}

			return path;
		}

		private void DrawSoftware(Graphics2D graphics)
		{
			if (this.canvas == null)
			{
				this.canvas = new ImageBuffer(this.Width, this.Height, 24, new BlenderPreMultBGR());
			}

			Graphics2D canvasGraphics = this.canvas.NewGraphics2D();
			canvasGraphics.Rasterizer.reset_clipping();
			canvasGraphics.Clear(Rgba8.FromRgba(1.0, 1.0, 0.95));
			var destination = new ImageClippingProxy(this.canvas);
			this.DrawLanes(canvasGraphics, destination, destination);

			// The canvas is opaque and whole pixels, so this is a straight copy; the controls then draw "plain", as
			// C++ draws them through the non-premultiplied pixfmt.
			graphics.Render(this.canvas, 0, 0);
		}

		/// <summary>
		/// Any other surface - the GPU - takes the pixel renderers' pixels as rectangles through a
		/// <see cref="Graphics2DSpanImage"/>, which blends straight: the chain's premultiplied colors are converted on the
		/// way, and the lanes' opaque color blends the same either way.
		/// </summary>
		private void DrawSpans(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Rgba8.FromRgba(1.0, 1.0, 0.95));
			var spans = new Graphics2DSpanImage(graphics, this.Width, this.Height);
			var destination = new ImageClippingProxy(spans);
			this.DrawLanes(graphics, destination, new PremultipliedColorSpans(destination));
			spans.Flush();
		}

		/// <summary>
		/// C++ on_draw's five lanes and labels: the pixel renderers into <paramref name="destination"/> (the image pattern
		/// into <paramref name="patternDestination"/>), the scanline lane and labels as vectors on <paramref name="vectors"/>.
		/// </summary>
		private void DrawLanes(Graphics2D vectors, ImageClippingProxy destination, IImageByte patternDestination)
		{
			double w = this.Width;
			double h = this.Height;
			double width = this.WidthSlider.Value;

			// The aliased lanes: renderer_primitives through rasterizer_outline.
			var primitives = new RendererPrimitives(destination) { LineColor = LineColor };
			var aliased = new RasterizerOutline(primitives);
			aliased.AddPath(this.Spiral(w / 5, (h / 4) + 50, true));
			aliased.AddPath(this.Spiral(w / 2, (h / 4) + 50, false));

			// The anti-aliased outline.
			var outlineRenderer = new OutlineRenderer(destination, new LineProfileAnitAlias(width, new gamma_none()));
			outlineRenderer.color(LineColor);
			var outline = new rasterizer_outline_aa(outlineRenderer);
			outline.line_join(this.AccurateJoinsCbox.Checked ? rasterizer_outline_aa.outline_aa_join_e.outline_miter_accurate_join : rasterizer_outline_aa.outline_aa_join_e.outline_round_join);
			outline.round_cap(true);
			outline.add_path(this.Spiral(w / 5, h - (h / 4) + 20, false));

			// The scanline rasterizer.
			var stroke = new Stroke(this.Spiral(w / 2, h - (h / 4) + 20, false), width) { LineCap = LineCap.Round };
			vectors.Render(stroke, LineColor);

			// The image pattern.
			var chain = new PixmapChainSource();
			var pattern = new line_image_pattern_pow2(new pattern_filter_bilinear_RGBA_Bytes());
			pattern.create(this.ScalePatternCbox.Checked ? new LineImageScale(chain, width) : chain);
			var imageRenderer = new ImageLineRenderer(patternDestination, pattern);
			if (this.ScalePatternCbox.Checked)
			{
				imageRenderer.scale_x(width / chain.Height);
			}

			new rasterizer_outline_aa(imageRenderer).add_path(this.Spiral(w - (w / 5), h - (h / 4) + 20, false));

			DrawLabel(vectors, 50, 80, "Bresenham lines,\n\nregular accuracy");
			DrawLabel(vectors, (w / 2) - 50, 80, "Bresenham lines,\n\nsubpixel accuracy");
			DrawLabel(vectors, 50, (h / 2) + 50, "Anti-aliased lines");
			DrawLabel(vectors, (w / 2) - 50, (h / 2) + 50, "Scanline rasterizer");
			DrawLabel(vectors, w - (w / 5) - 50, (h / 2) + 50, "Arbitrary Image Pattern");
		}

		/// <summary>C++ <c>text</c>: gsv_text 8 high, stroked 0.7 wide in black.</summary>
		private static void DrawLabel(Graphics2D graphics, double x, double y, string label)
		{
			var outline = new VertexStorage();
#pragma warning disable CS0618 // gsv_text is obsolete for UI text; rasterizers2.cpp labels its lanes with exactly this font.
			var text = new gsv_text();
#pragma warning restore CS0618
			text.size(8, 0);
			text.text(label);
			text.start_point(x, y);
			foreach (VertexData vertex in text.Vertices())
			{
				if (vertex.IsStop)
				{
					break;
				}

				outline.Add(vertex.Position.X, vertex.Position.Y, vertex.Command);
			}

			graphics.Render(new Stroke(outline, 0.7), Color.Black);
		}

		/// <summary>C++ <c>pattern_pixmap_argb32</c>: the chain as linear, premultiplied colors.</summary>
		private class PixmapChainSource : ILineImageSource
		{
			public double Width => 16;

			public double Height => 7;

			public LineImageColor Pixel(int x, int y)
			{
				// The chain is authored in sRGB and C++ linearises it (rgba(srgba8)), so it draws darker than authored.
				uint p = PixmapChain[(y * 16) + x];
				return new LineImageColor(
					SrgbLut.LinearFromSrgbFloat((int)((p >> 16) & 0xFF)),
					SrgbLut.LinearFromSrgbFloat((int)((p >> 8) & 0xFF)),
					SrgbLut.LinearFromSrgbFloat((int)(p & 0xFF)),
					(float)((p >> 24) * (1 / 255.0))).Premultiply();
			}
		}
	}
}
