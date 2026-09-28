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
	/// C++ AGG's line_thickness.cpp: a row of lines growing thicker and a wheel of lines all one width, stroked
	/// anti-aliased and then smoothed by slight_blur, in monochrome or colour, dark on light or inverted.
	/// </summary>
	/// <remarks>
	/// On the GPU the frame is blurred by <see cref="IBlurGraphics.BlurBox"/>'s slight blur. The "Blur: %3.2f ms"
	/// timer text is left out, as in the other ports.
	/// </remarks>
	public class LineThicknessDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		public LineThicknessDemo()
		{
			// line_thickness.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.ThicknessSlider = new SliderCtrl(10, 10, 640 - 10, 19, false) { Label = "Line thickness={0:F2}" };
			this.ThicknessSlider.SetRange(0.0, 5.0);
			this.ThicknessSlider.Value = 1.0;

			this.BlurSlider = new SliderCtrl(10, 10 + 20, 640 - 10, 19 + 20, false) { Label = "Blur radius={0:F2}" };
			this.BlurSlider.SetRange(0.0, 2.0);
			this.BlurSlider.Value = 1.5;

			this.MonochromeCbox = new CboxCtrl(10, 10 + 40, "Monochrome") { Checked = true };
			this.InvertCbox = new CboxCtrl(10, 10 + 60, "Invert");

			this.ctrls.Add(this.ThicknessSlider);
			this.ctrls.Add(this.BlurSlider);
			this.ctrls.Add(this.MonochromeCbox);
			this.ctrls.Add(this.InvertCbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_slider1</c>: the wheel's line width, and the row's width step (times 0.3), 0 to 5.</summary>
		public SliderCtrl ThicknessSlider { get; }

		/// <summary>C++ <c>m_slider2</c>: the slight_blur radius, 0 to 2; 0 leaves the lines sharp.</summary>
		public SliderCtrl BlurSlider { get; }

		/// <summary>C++ <c>m_cbox1</c>: black and white when checked, green and magenta when not.</summary>
		public CboxCtrl MonochromeCbox { get; }

		/// <summary>C++ <c>m_cbox2</c>: swaps the foreground and background colours.</summary>
		public CboxCtrl InvertCbox { get; }

		public override string Name => "line_thickness";

		public override string Category => "Lines";

		public override string Description => "Anti-aliased lines from hairline to thick, smoothed by a slight Gaussian blur. Change the thickness and blur, or switch the colours.";

		public override int Width => 640;

		public override int Height => 480;

		public override void Draw(Graphics2D graphics)
		{
			Color light = this.MonochromeCbox.Checked ? Rgba8.FromRgba(1.0, 1.0, 1.0) : Rgba8.FromRgba(1.0, 0.0, 1.0);
			Color dark = this.MonochromeCbox.Checked ? Rgba8.FromRgba(0.0, 0.0, 0.0) : Rgba8.FromRgba(0.0, 1.0, 0.0);
			Color foreground = this.InvertCbox.Checked ? light : dark;
			Color background = this.InvertCbox.Checked ? dark : light;

			// C++ SetCtrlClr: the check boxes are drawn in the foreground colour, the sliders keep theirs.
			foreach (CboxCtrl cbox in new[] { this.MonochromeCbox, this.InvertCbox })
			{
				cbox.TextColor = foreground;
				cbox.InactiveColor = foreground;
				cbox.ActiveColor = foreground;
			}

			graphics.FillRectangle(0, 0, this.Width, this.Height, background);

			double thickness = this.ThicknessSlider.Value;
			var path = new VertexStorage();

			// The row of straight lines, each 0.3 of the thickness wider than the last.
			for (int i = 0; i < 20; i++)
			{
				path.Clear();
				path.MoveTo(20 + (30 * i), 310);
				path.LineTo(40 + (30 * i), 460);
				graphics.Render(new Stroke(path, thickness * 0.3 * (i + 1)), foreground);
			}

			// The wheel of lines.
			for (int i = 0; i < 40; i++)
			{
				double angle = i * Math.PI / 20;
				path.Clear();
				path.MoveTo(320 + (20 * Math.Sin(angle)), 180 + (20 * Math.Cos(angle)));
				path.LineTo(320 + (100 * Math.Sin(angle)), 180 + (100 * Math.Cos(angle)));
				graphics.Render(new Stroke(path, thickness), foreground);
			}

			if (graphics is ImageGraphics2D && graphics.DestImage is IImageByte destination && this.BlurSlider.Value > 0)
			{
				// C++ apply_slight_blur over renderer_base's clip box: the whole window. The graphics transform only
				// offsets the demo inside a larger frame.
				int offsetX = (int)Math.Round(graphics.GetTransform().tx);
				int offsetY = (int)Math.Round(graphics.GetTransform().ty);
				new SlightBlur(this.BlurSlider.Value).Blur(destination, new RectangleInt(offsetX, offsetY, offsetX + this.Width - 1, offsetY + this.Height - 1));
			}
			else if (graphics is IBlurGraphics blurGraphics && !(graphics is ImageGraphics2D) && this.BlurSlider.Value > 0)
			{
				blurGraphics.BlurBox(new RoundedRect(0, 0, this.Width, this.Height, 0), this.BlurSlider.Value, BlurKind.Slight);
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
	}
}
