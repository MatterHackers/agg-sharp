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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's TextOnBg (rendering_test.rs, egui's text_on_bg): the fox sentence in one colour on a snug
	/// rectangle of another, then "(r g b) on (r g b)" in the dim text colour.
	/// </summary>
	public class RenderingTestTextOnBackground : GuiWidget
	{
		private const string Sentence = "▣ The quick brown fox jumps over the lazy dog and runs away.";

		private const double RowHeight = 22;

		private readonly DemoTheme demoTheme;

		public RenderingTestTextOnBackground(DemoTheme demoTheme, Color foreground, Color background)
			: base(1, RowHeight * DeviceScale)
		{
			this.demoTheme = demoTheme;
			this.Foreground = foreground;
			this.Background = background;
			this.HAnchor = HAnchor.Stretch;
		}

		/// <summary>The sentence colour.</summary>
		public Color Foreground { get; }

		/// <summary>The rectangle colour behind it.</summary>
		public Color Background { get; }

		/// <summary>The value label, as egui prints it.</summary>
		public string ValueLabel => $"({this.Foreground.red} {this.Foreground.green} {this.Foreground.blue}) on ({this.Background.red} {this.Background.green} {this.Background.blue})";

		public override void OnDraw(Graphics2D graphics2D)
		{
			double s = DeviceScale;
			double pad = 4 * s;
			double h = this.Height;
			double size = RenderingTestWindow.TextSize(this.demoTheme, 12.5);
			double textWidth = new TypeFacePrinter(Sentence, size).LocalBounds.Width;
			double textHeight = 14 * s;
			double rectBottom = (h - textHeight - (pad * 2)) / 2;
			graphics2D.FillRectangle(0, rectBottom, textWidth + (pad * 2), rectBottom + textHeight + (pad * 2), this.Background);

			double baseline = (h * .32) + (3 * s);
			graphics2D.DrawString(Sentence, pad, baseline, size, color: this.Foreground);
			graphics2D.DrawString(this.ValueLabel, textWidth + (pad * 2) + (8 * s), baseline, RenderingTestWindow.TextSize(this.demoTheme, 11.5), color: this.demoTheme.Palette.TextDim);
			base.OnDraw(graphics2D);
		}
	}
}
