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
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's image_fltr_graph.cpp: the shapes of the 16 image reconstruction filters. Each checked filter draws
	/// its weight function (dark red), the sum of its weights at every whole-pixel offset (dark green, 1.0 on the
	/// base line plus 256) and the normalized integer lookup table the span filters sample (dark blue).
	/// </summary>
	public class ImageFltrGraphDemo : AggDemo
	{
		private static readonly string[] FilterNames =
		{
			"bilinear", "bicubic ", "spline16", "spline36", "hanning ", "hamming ", "hermite ", "kaiser  ",
			"quadric ", "catrom  ", "gaussian", "bessel  ", "mitchell", "sinc    ", "lanczos ", "blackman",
		};

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		public ImageFltrGraphDemo()
		{
			// image_fltr_graph.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.RadiusSlider = new SliderCtrl(5.0, 5.0, 780 - 5, 10.0, false) { Label = "Radius={0:F3}" };
			this.FilterCboxes = new CboxCtrl[FilterNames.Length];
			for (int i = 0; i < FilterNames.Length; i++)
			{
				this.FilterCboxes[i] = new CboxCtrl(8.0, 30.0 + (15 * i), FilterNames[i], false);
				this.ctrls.Add(this.FilterCboxes[i]);
			}

			this.RadiusSlider.SetRange(2.0, 8.0);
			this.RadiusSlider.Value = 4.0;

			// add_ctrl order: the radius slider last. It takes clicks even while it is hidden, as in C++.
			this.ctrls.Add(this.RadiusSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_radius</c>: the radius of sinc, lanczos and blackman, shown only while one is checked.</summary>
		public SliderCtrl RadiusSlider { get; }

		/// <summary>C++ <c>m_filters</c>: one checkbox per filter, bilinear through blackman. None starts checked.</summary>
		public CboxCtrl[] FilterCboxes { get; }

		public override string Name => "image_fltr_graph";

		public override string Category => "Images";

		public override string Description => "The shapes of 16 image filters: each one's weight curve, summed weights and lookup table. Check filters to compare them.";

		// agg_main: init(780, 300, window_resize). C++ draws through trans_affine_resizing(), identity at this size.
		public override int Width => 780;

		public override int Height => 300;

		public override void Draw(Graphics2D graphics)
		{
			// Every part of the frame is a stroked path or a ctrl, so the software and GPU paths draw it the same way.
			graphics.Clear(Color.White);

			double xStart = 125.0;
			double xEnd = this.Width - 15.0;
			double yStart = 10.0;
			double yEnd = this.Height - 10.0;
			double xCenter = (xStart + xEnd) / 2;

			// C++ srgba8(0, 0, 0, a) drawn into the rgba8 window: black is black in sRGB too, and alpha is not converted.
			for (int i = 0; i <= 16; i++)
			{
				double x = xStart + ((xEnd - xStart) * i / 16.0);
				var gridLine = new VertexStorage();
				gridLine.MoveTo(x + 0.5, yStart);
				gridLine.LineTo(x + 0.5, yEnd);
				graphics.Render(new Stroke(gridLine), new Color(0, 0, 0, i == 8 ? 255 : 100));
			}

			double ys = yStart + ((yEnd - yStart) / 6.0);
			var baseLine = new VertexStorage();
			baseLine.MoveTo(xStart, ys);
			baseLine.LineTo(xEnd, ys);
			graphics.Render(new Stroke(baseLine), new Color(0, 0, 0));

			for (int i = 0; i < this.FilterCboxes.Length; i++)
			{
				if (this.FilterCboxes[i].Checked)
				{
					DrawFilter(graphics, NewFilter(i, this.RadiusSlider.Value), xStart, xEnd, yEnd, xCenter, ys);
				}
			}

			foreach (CboxCtrl cbox in this.FilterCboxes)
			{
				cbox.Render(graphics);
			}

			if (this.FilterCboxes[13].Checked || this.FilterCboxes[14].Checked || this.FilterCboxes[15].Checked)
			{
				this.RadiusSlider.Render(graphics);
			}
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags) => this.ctrls.OnMouseDown(x, y, button);

		public override void OnMouseMove(int x, int y, AggInputFlags flags) => this.ctrls.OnMouseMove(x, y, flags);

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags) => this.ctrls.OnMouseUp(x, y, button);

		public override void OnKeyDown(Keys key, AggInputFlags flags) => this.ctrls.OnKeyDown(key);

		// C++ m_filter_func[i] after set_radius(m_radius.value()): only sinc, lanczos and blackman take the radius.
		private static IImageFilterFunction NewFilter(int index, double radius)
		{
			return index switch
			{
				0 => new image_filter_bilinear(),
				1 => new image_filter_bicubic(),
				2 => new image_filter_spline16(),
				3 => new image_filter_spline36(),
				4 => new image_filter_hanning(),
				5 => new image_filter_hamming(),
				6 => new image_filter_hermite(),
				7 => new image_filter_kaiser(),
				8 => new image_filter_quadric(),
				9 => new image_filter_catrom(),
				10 => new image_filter_gaussian(),
				11 => new image_filter_bessel(),
				12 => new image_filter_mitchell(),
				13 => new image_filter_sinc(radius),
				14 => new image_filter_lanczos(radius),
				_ => new image_filter_blackman(radius),
			};
		}

		// The three curves of one checked filter, stroked 1.5 wide.
		private static void DrawFilter(Graphics2D graphics, IImageFilterFunction filter, double xStart, double xEnd, double yEnd, double xCenter, double ys)
		{
			// The C++ adaptors pass |x|: the filter functions are written for x >= 0.
			double Weight(double x) => filter.calc_weight(Math.Abs(x));

			double radius = filter.radius();
			int n = (int)(radius * 256 * 2);
			double dy = yEnd - ys;
			double xs = ((xEnd + xStart) / 2.0) - (radius * (xEnd - xStart) / 16.0);
			double dx = (xEnd - xStart) * radius / 8.0;

			var weightCurve = new VertexStorage();
			weightCurve.MoveTo(xs + 0.5, ys + (dy * Weight(-radius)));
			for (int j = 1; j < n; j++)
			{
				weightCurve.LineTo(xs + (dx * j / n) + 0.5, ys + (dy * Weight((j / 256.0) - radius)));
			}

			graphics.Render(new Stroke(weightCurve, 1.5), Rgba8.FromRgba(0.5, 0, 0));

			// The weights summed at every whole-pixel offset from each of 256 subpixel positions: 1.0 (256 above the
			// base line) for a filter that keeps brightness. C++ tests xf >= -radius || xf <= radius, which always
			// holds, so every offset in [-ceil(radius), ceil(radius)) is summed - the span the lookup table covers.
			var sumCurve = new VertexStorage();
			int ir = (int)(Math.Ceiling(radius) + 0.1);
			for (int xint = 0; xint < 256; xint++)
			{
				double sum = 0;
				for (int xfract = -ir; xfract < ir; xfract++)
				{
					sum += Weight((xint / 256.0) + xfract);
				}

				double x = xCenter + ((-128.0 + xint) / 128.0 * radius * (xEnd - xStart) / 16.0);
				double y = ys + (sum * 256) - 256;
				if (xint == 0)
				{
					sumCurve.MoveTo(x, y);
				}
				else
				{
					sumCurve.LineTo(x, y);
				}
			}

			graphics.Render(new Stroke(sumCurve, 1.5), Rgba8.FromRgba(0, 0.5, 0));

			// image_filter_lut(filter): normalized, as the span filters use it. Each entry steps dx / n, the weight
			// curve's scale, so the table lies over the red curve.
			var lookUpTable = new ImageFilterLookUpTable(filter, true);
			int[] weights = lookUpTable.weight_array();
			double filterScale = (int)ImageFilterLookUpTable.image_filter_scale_e.image_filter_scale;
			xs = ((xEnd + xStart) / 2.0) - (lookUpTable.diameter() * (xEnd - xStart) / 32.0);
			int nn = lookUpTable.diameter() * 256;
			var tableCurve = new VertexStorage();
			tableCurve.MoveTo(xs + 0.5, ys + (dy * weights[0] / filterScale));
			for (int j = 1; j < nn; j++)
			{
				tableCurve.LineTo(xs + (dx * j / n) + 0.5, ys + (dy * weights[j] / filterScale));
			}

			graphics.Render(new Stroke(tableCurve, 1.5), Rgba8.FromRgba(0, 0, 0.5));
		}
	}
}
