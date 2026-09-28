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

using System.Globalization;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's BlendingTest (rendering_test/blending.rs, egui's paint_fine_lines_and_text): a 512 x 512 square,
	/// white on black above and black on white below. Each half has nested corner-sweeping curves stroked 0.05
	/// to 4 wide and a transparent-to-opaque bar on the left, and text at eight opacities plus a 6 to 14 size
	/// ramp on the right. The two halves should look symmetrical in their intensities.
	/// </summary>
	public class RenderingTestBlending : GuiWidget
	{
		private static readonly double[] Opacities = { 1, .5, .25, .1, .05, .02, .01, 0 };

		private static readonly double[] FontSizes = { 6, 7, 8, 9, 10, 12, 14 };

		private static readonly double[] LineWidths = { .05, .1, .25, .5, 1, 2, 4 };

		private readonly DemoTheme demoTheme;

		public RenderingTestBlending(DemoTheme demoTheme)
			: base(512 * DeviceScale, 512 * DeviceScale)
		{
			this.demoTheme = demoTheme;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double half = this.Height / 2;
			graphics2D.FillRectangle(0, half, this.Width, this.Height, Color.Black);
			this.DrawHalf(graphics2D, half, Color.White);
			graphics2D.FillRectangle(0, 0, this.Width, half, Color.White);
			this.DrawHalf(graphics2D, 0, Color.Black);
			base.OnDraw(graphics2D);
		}

		private void DrawHalf(Graphics2D graphics2D, double bottom, Color color)
		{
			double s = DeviceScale;
			double w = this.Width;
			double h = this.Height / 2;

			// Right: white, gray and black text at falling opacities, then the size ramp in the half's colour.
			double rightX = (w / 2) + (4 * s);
			double columnWidth = ((w / 2) - (8 * s)) / 3;
			double rowHeight = 20 * s;
			double textY = bottom + h - (rowHeight * .7);
			double small = RenderingTestWindow.TextSize(this.demoTheme, 11);
			foreach (double opacity in Opacities)
			{
				string percent = (100 * opacity).ToString("0", CultureInfo.InvariantCulture);
				DrawText(graphics2D, $"{percent}% white", rightX, textY, small, Color.White.WithAlpha(opacity));
				DrawText(graphics2D, $"{percent}% gray", rightX + columnWidth, textY, small, new Color(128, 128, 128).WithAlpha(opacity));
				DrawText(graphics2D, $"{percent}% black", rightX + (columnWidth * 2), textY, small, Color.Black.WithAlpha(opacity));
				textY -= rowHeight;
			}

			foreach (double size in FontSizes)
			{
				string text = $"{size.ToString(CultureInfo.InvariantCulture)}px - The quick brown fox jumps over the lazy dog and runs away.";
				DrawText(graphics2D, text, rightX, textY, RenderingTestWindow.TextSize(this.demoTheme, size), color);
				textY -= (size + 1) * s;
			}

			// Left: each curve sweeps from the rect's top left along its top to its bottom right; the rect then
			// loses 24 from its top and right, nesting the curves.
			double left = 16 * s;
			double right = (w / 2) - (16 * s);
			double top = bottom + h - (16 * s);
			double rectBottom = bottom + (16 * s);
			double labelSize = RenderingTestWindow.TextSize(this.demoTheme, 10);
			foreach (double lineWidth in LineWidths)
			{
				DrawText(graphics2D, lineWidth.ToString(CultureInfo.InvariantCulture), left, top, labelSize, color);
				var curve = new VertexStorage();
				curve.MoveTo(left + (16 * s), top);
				curve.Curve4(right, top, right, (top + rectBottom) / 2, right, rectBottom);
				graphics2D.Render(new Stroke(new FlattenCurves(curve), lineWidth * s), color);
				top -= 24 * s;
				right -= 24 * s;
			}

			double barY = bottom + (10 * s);
			DrawText(graphics2D, "transparent --> opaque", left, barY + (12 * s), RenderingTestWindow.TextSize(this.demoTheme, 9), color);
			const int Steps = 32;
			double stepWidth = ((w / 2) - (24 * s)) / Steps;
			for (int i = 0; i < Steps; i++)
			{
				double x = left + (i * stepWidth);
				graphics2D.FillRectangle(x, barY - (8 * s), x + stepWidth, barY, color.WithAlpha(i / (double)Steps));
			}
		}

		private static void DrawText(Graphics2D graphics2D, string text, double x, double y, double size, Color color)
		{
			graphics2D.DrawString(text, x, y, size, color: color);
		}
	}
}
