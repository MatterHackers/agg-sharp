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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's conv_contour.cpp: the glyph "a" grown or shrunk by conv_contour. Closing the polygons CW or CCW
	/// tells the contour which way is out; unmarked, it grows them as it finds them unless orientation
	/// autodetection is on.
	/// </summary>
	public class ConvContourDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		public ConvContourDemo()
		{
			// conv_contour.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.CloseRbox = new RboxCtrl(10.0, 10.0, 130.0, 80.0, false);
			this.CloseRbox.AddItem("Close");
			this.CloseRbox.AddItem("Close CW");
			this.CloseRbox.AddItem("Close CCW");
			this.CloseRbox.CurrentItem = 0;

			this.WidthSlider = new SliderCtrl(130 + 10.0, 10.0 + 4.0, 130 + 300.0, 10.0 + 8.0 + 4.0, false) { Label = "Width={0:F2}" };
			this.WidthSlider.SetRange(-100.0, 100.0);
			this.WidthSlider.Value = 0.0;

			this.AutoDetectCbox = new CboxCtrl(130 + 10.0, 10.0 + 4.0 + 16.0, "Autodetect orientation if not defined");

			this.ctrls.Add(this.CloseRbox);
			this.ctrls.Add(this.WidthSlider);
			this.ctrls.Add(this.AutoDetectCbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_close</c>: close the polygons unmarked, marked clockwise or marked counter-clockwise.</summary>
		public RboxCtrl CloseRbox { get; }

		/// <summary>C++ <c>m_width</c>: the contour width, negative to shrink.</summary>
		public SliderCtrl WidthSlider { get; }

		/// <summary>C++ <c>m_auto_detect</c>: conv_contour's auto_detect_orientation.</summary>
		public CboxCtrl AutoDetectCbox { get; }

		public override string Name => "conv_contour";

		public override string Category => "Paths & Strokes";

		public override string Description => "A glyph grown or shrunk by a contour. Change the width and how the polygons are closed.";

		public override int Width => 440;

		public override int Height => 330;

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			Affine transform = Affine.NewScaling(4.0) * Affine.NewTranslation(150, 100);

			// C++ conv_contour<conv_curve<conv_transform<path_storage>>>: the curves are flattened after the
			// transform, at its scale.
			var contour = new Contour(new FlattenCurves(new VertexSourceApplyTransform(this.ComposePath(), transform)))
			{
				Width = this.WidthSlider.Value,
				AutoDetectOrientation = this.AutoDetectCbox.Checked,
			};
			graphics.Render(contour, Rgba8.FromRgba(0, 0, 0));

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags) => this.ctrls.OnMouseDown(x, y, button);

		public override void OnMouseMove(int x, int y, AggInputFlags flags) => this.ctrls.OnMouseMove(x, y, flags);

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags) => this.ctrls.OnMouseUp(x, y, button);

		public override void OnKeyDown(Keys key, AggInputFlags flags) => this.ctrls.OnKeyDown(key);

		// C++ compose_path: the glyph "a" in font units, its two polygons closed with the rbox's orientation flag.
		private VertexStorage ComposePath()
		{
			FlagsAndCommand flag = this.CloseRbox.CurrentItem == 1 ? FlagsAndCommand.FlagCW
				: this.CloseRbox.CurrentItem == 2 ? FlagsAndCommand.FlagCCW
				: FlagsAndCommand.FlagNone;

			var path = new VertexStorage();
			path.MoveTo(28.47, 6.45);
			path.Curve3(21.58, 1.12, 19.82, 0.29);
			path.Curve3(17.19, -0.93, 14.21, -0.93);
			path.Curve3(9.57, -0.93, 6.57, 2.25);
			path.Curve3(3.56, 5.42, 3.56, 10.60);
			path.Curve3(3.56, 13.87, 5.03, 16.26);
			path.Curve3(7.03, 19.58, 11.99, 22.51);
			path.Curve3(16.94, 25.44, 28.47, 29.64);
			path.LineTo(28.47, 31.40);
			path.Curve3(28.47, 38.09, 26.34, 40.58);
			path.Curve3(24.22, 43.07, 20.17, 43.07);
			path.Curve3(17.09, 43.07, 15.28, 41.41);
			path.Curve3(13.43, 39.75, 13.43, 37.60);
			path.LineTo(13.53, 34.77);
			path.Curve3(13.53, 32.52, 12.38, 31.30);
			path.Curve3(11.23, 30.08, 9.38, 30.08);
			path.Curve3(7.57, 30.08, 6.42, 31.35);
			path.Curve3(5.27, 32.62, 5.27, 34.81);
			path.Curve3(5.27, 39.01, 9.57, 42.53);
			path.Curve3(13.87, 46.04, 21.63, 46.04);
			path.Curve3(27.59, 46.04, 31.40, 44.04);
			path.Curve3(34.28, 42.53, 35.64, 39.31);
			path.Curve3(36.52, 37.21, 36.52, 30.71);
			path.LineTo(36.52, 15.53);
			path.Curve3(36.52, 9.13, 36.77, 7.69);
			path.Curve3(37.01, 6.25, 37.57, 5.76);
			path.Curve3(38.13, 5.27, 38.87, 5.27);
			path.Curve3(39.65, 5.27, 40.23, 5.62);
			path.Curve3(41.26, 6.25, 44.19, 9.18);
			path.LineTo(44.19, 6.45);
			path.Curve3(38.72, -0.88, 33.74, -0.88);
			path.Curve3(31.35, -0.88, 29.93, 0.78);
			path.Curve3(28.52, 2.44, 28.47, 6.45);
			path.ClosePolygon(flag);

			path.MoveTo(28.47, 9.62);
			path.LineTo(28.47, 26.66);
			path.Curve3(21.09, 23.73, 18.95, 22.51);
			path.Curve3(15.09, 20.36, 13.43, 18.02);
			path.Curve3(11.77, 15.67, 11.77, 12.89);
			path.Curve3(11.77, 9.38, 13.87, 7.06);
			path.Curve3(15.97, 4.74, 18.70, 4.74);
			path.Curve3(22.41, 4.74, 28.47, 9.62);
			path.ClosePolygon(flag);
			return path;
		}
	}
}
