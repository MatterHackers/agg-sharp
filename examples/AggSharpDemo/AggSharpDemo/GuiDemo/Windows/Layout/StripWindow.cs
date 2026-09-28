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
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout
{
	/// <summary>
	/// The "Strip" window, a port of agg-gui's strip_demo (demo-ui/src/windows/text_demos/strip_table.rs, egui's
	/// StripBuilder demo): five faded regions sized by exact, relative, remainder and at-least rules - blue across
	/// the top, red and yellow halves in the middle, gold and green fixed widths at the bottom - each describing
	/// its own rule. Regions are named "Strip Region 0" to "Strip Region 4" in that order.
	/// </summary>
	public class StripWindow : GuiWidget
	{
		/// <summary>agg-gui's body text size, which sets the footer strip's height.</summary>
		public const double BodyTextSize = 14;

		/// <summary>The smallest area the regions are laid out in; a smaller window clips them.</summary>
		public const double MinimumDesignWidth = 360;

		public const double MinimumDesignHeight = 260;

		/// <summary>Each region's text, in region order.</summary>
		public static readonly string[] Labels =
		{
			"width: 100%\nheight: 50px",
			"width: 50%\nheight: remaining",
			"width: 50%\nheight: 1/3 of the red region",
			"width: 120px\nheight: 60px",
			"width: 70px\n\nheight: 50%, but at least 60px.",
		};

		/// <summary>Each region's colour before fading, in region order.</summary>
		private static readonly ColorF[] Hues =
		{
			new ColorF(0, 0, 1), new ColorF(1, 0, 0), new ColorF(1, 1, 0), new ColorF(1, .84, 0), new ColorF(0, 1, 0),
		};

		private readonly DemoTheme demoTheme;
		private readonly GuiWidget[] regions = new GuiWidget[5];
		private readonly WrappedTextWidget[] texts = new WrappedTextWidget[5];

		public StripWindow(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			for (int i = 0; i < 5; i++)
			{
				this.regions[i] = new GuiWidget
				{
					Name = $"Strip Region {i}",
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Absolute,
					Padding = new BorderDouble(6),
					BackgroundColor = new ColorF(Hues[i].red, Hues[i].green, Hues[i].blue, .22).ToColor(),
				};
				this.texts[i] = new WrappedTextWidget(Labels[i], demoTheme.Theme.DefaultFontSize * 11 / 12, textColor: demoTheme.Palette.TextColor)
				{
					VAnchor = VAnchor.Top | VAnchor.Fit,
				};
				this.regions[i].AddChild(this.texts[i]);
				this.AddChild(this.regions[i]);
			}

			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		/// <summary>The region <paramref name="index"/> (0 to 4).</summary>
		public GuiWidget Region(int index) => this.regions[index];

		/// <summary>
		/// The five regions for a <paramref name="width"/> by <paramref name="height"/> area in design units, y up.
		/// egui's exact sizes and at-least limits hold even when the area is too small, so the area is first grown
		/// to fit them.
		/// </summary>
		public static RectangleDouble[] Regions(double width, double height, double bodyTextSize)
		{
			const double ExactWidths = 120 + 70;
			double footerMinimum = Math.Max(bodyTextSize, 12);
			double w = Math.Max(width, ExactWidths);
			double h = Math.Max(height, 50 + 60 + footerMinimum);
			double footerHeight = Math.Min(footerMinimum, h);
			double topHeight = Math.Min(50, Math.Max(0, h - footerHeight));
			double remaining = Math.Max(0, h - footerHeight - topHeight);
			double lowerHeight = Math.Min(Math.Max(remaining * .5, 60), remaining);
			double middleHeight = Math.Max(0, remaining - lowerHeight);
			double lowerY = footerHeight;
			double middleY = lowerY + lowerHeight;
			double topY = middleY + middleHeight;

			double half = w * .5;
			double yellowHeight = middleHeight / 3;
			double goldX = (w - ExactWidths) * .5;
			double greenX = Math.Max(0, w - 70);
			double goldY = lowerY + Math.Max(0, lowerHeight - 60) * .5;
			double greenHeight = Math.Min(Math.Max(lowerHeight * .5, 60), lowerHeight);
			double greenY = lowerY + (lowerHeight - greenHeight) * .5;

			return new[]
			{
				Rect(0, topY, w, topHeight),
				Rect(0, middleY, half, middleHeight),
				Rect(half, middleY + yellowHeight, half, yellowHeight),
				Rect(goldX, goldY, Math.Min(120, w), Math.Min(60, lowerHeight)),
				Rect(greenX, greenY, Math.Min(70, w), greenHeight),
			};
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			base.OnBoundsChanged(e);
			double scale = DeviceScale;
			RectangleDouble[] rects = this.CurrentRegions();
			for (int i = 0; i < 5; i++)
			{
				this.regions[i].Position = new Vector2(rects[i].Left * scale, rects[i].Bottom * scale);
				this.regions[i].Size = new Vector2(rects[i].Width * scale, rects[i].Height * scale);
			}
		}

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			// agg-gui strokes each region over its text, in the widget stroke colour.
			foreach (GuiWidget region in this.regions)
			{
				RectangleDouble r = region.BoundsRelativeToParent;
				graphics2D.Render(new Stroke(new RoundedRect(r.Left + .5, r.Bottom + .5, r.Right - .5, r.Top - .5, 0), 1), this.demoTheme.Palette.WidgetStroke);
			}
		}

		/// <summary>The regions for this window's size, laid out in at least 360 by 260.</summary>
		private RectangleDouble[] CurrentRegions()
		{
			double scale = DeviceScale;
			double width = Math.Max(MinimumDesignWidth, this.Width / scale);
			double height = Math.Max(MinimumDesignHeight, this.Height / scale);
			RectangleDouble[] rects = Regions(width, height, BodyTextSize);

			// agg-gui lays out from the bottom; a window shorter than the minimum keeps the top in view instead.
			double shift = this.Height / scale - height;
			for (int i = 0; i < rects.Length; i++)
			{
				rects[i].Offset(0, shift);
			}

			return rects;
		}

		private static RectangleDouble Rect(double x, double y, double width, double height) => new RectangleDouble(x, y, x + width, y + height);

		private void OnThemeChanged(object sender, EventArgs e)
		{
			this.Recolor();
			this.Invalidate();
		}

		/// <summary>Pushes the current theme into the widgets that copied their colours when they were built.</summary>
		private void Recolor()
		{
			this.BackgroundColor = this.demoTheme.Palette.WindowFill;
			foreach (WrappedTextWidget text in this.texts)
			{
				text.TextColor = this.demoTheme.Palette.TextColor;
			}
		}
	}
}
