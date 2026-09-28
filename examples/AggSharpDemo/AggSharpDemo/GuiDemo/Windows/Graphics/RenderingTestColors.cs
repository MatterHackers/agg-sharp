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
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's ColorTest (rendering_test/color.rs, a subset of egui's): six labelled 256-step rows - solid
	/// orange, red to green and black to white interpolated in sRGB, green and white fading in over white and
	/// black, and blue fading in over red - above a note that all should change smoothly.
	/// </summary>
	public class RenderingTestColors : GuiWidget
	{
		/// <summary>Steps in each gradient.</summary>
		public const int Steps = 256;

		private const double RowHeight = 28;

		private const double BarHeight = 18;

		private const double LabelX = 276;

		private readonly DemoTheme demoTheme;

		public RenderingTestColors(DemoTheme demoTheme)
			: base(560 * DeviceScale, ((RowHeight * 7) + 8) * DeviceScale)
		{
			this.demoTheme = demoTheme;
		}

		/// <summary>color.rs's gamma_lerp: a straight per-channel blend of the sRGB values, alpha included.</summary>
		public static ColorF Lerp(ColorF left, ColorF right, double t)
		{
			return new ColorF(
				left.red + ((right.red - left.red) * t),
				left.green + ((right.green - left.green) * t),
				left.blue + ((right.blue - left.blue) * t),
				left.alpha + ((right.alpha - left.alpha) * t));
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double s = DeviceScale;
			DemoPalette palette = this.demoTheme.Palette;
			double textSize = RenderingTestWindow.TextSize(this.demoTheme, 11);
			double y = this.Height - ((BarHeight + 4) * s);

			void Row(Color background, ColorF left, ColorF right, string label)
			{
				graphics2D.FillRectangle(0, y, Steps * s, y + (BarHeight * s), background);
				for (int x = 0; x < Steps; x++)
				{
					Color color = Lerp(left, right, x / (double)(Steps - 1)).ToColor();
					graphics2D.FillRectangle(x * s, y, (x + 1) * s, y + (BarHeight * s), color);
				}

				graphics2D.DrawString(label, LabelX * s, y + (4 * s), textSize, color: palette.TextColor);
				y -= RowHeight * s;
			}

			var orange = new ColorF(1, 165 / 255.0, 0);
			Row(orange.ToColor(), orange, orange, "orange rgb(255, 165, 0)");
			Row(Color.White, new ColorF(1, 0, 0), new ColorF(0, 1, 0), "gamma interpolation: red -> green");
			Row(Color.Black, new ColorF(0, 0, 0), new ColorF(1, 1, 1), "gamma interpolation: black -> white");
			Row(Color.White, new ColorF(0, .75, 0, 0), new ColorF(0, .75, 0, 1), "alpha blend on white");
			Row(Color.Black, new ColorF(1, 1, 1, 0), new ColorF(1, 1, 1, 1), "alpha blend on black");
			Row(new Color(255, 0, 0), new ColorF(0, 0, 1, 0), new ColorF(0, 0, 1, 1), "add blue over red");

			graphics2D.DrawString("All rows should change smoothly without banding or dark seams.", 0, 8 * s, textSize, color: palette.TextDim);
			base.OnDraw(graphics2D);
		}
	}
}
