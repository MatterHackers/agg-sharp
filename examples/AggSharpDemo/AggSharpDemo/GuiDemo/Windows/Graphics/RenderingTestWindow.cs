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
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's "Rendering Test" window (rendering_test.rs, egui's ColorTest): a scrolling column of the pixel
	/// alignment tests (stripes drawn directly and through a bitmap), the colour test, the pixel squares and
	/// stroke rings, four text-on-background rows and the blending test, each with its instructions.
	/// </summary>
	public class RenderingTestWindow : GuiWidget
	{
		// egui's text_on_bg rows: (foreground, background) grays.
		private static readonly (byte Fore, byte Back)[] TextRows = { (200, 230), (140, 28), (39, 255), (220, 30) };

		public RenderingTestWindow(DemoTheme demoTheme)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.BackgroundColor = demoTheme.Palette.PanelFill;

			var kit = new MiscDemoKit(demoTheme);
			this.ScrollArea = new ScrollableWidget(autoScroll: true)
			{
				Name = "Rendering Test Scroll",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.ScrollArea.ScrollArea.HAnchor = HAnchor.Stretch;
			this.AddChild(this.ScrollArea);

			FlowLayoutWidget column = kit.Column();
			column.Padding = new BorderDouble(10);
			this.ScrollArea.AddChild(column);

			var separators = new List<GuiWidget>();
			void Add(GuiWidget widget)
			{
				widget.Margin = new BorderDouble(0, 2);
				column.AddChild(widget);
			}

			void Text(string text) => Add(kit.Wrapped(text, 13));
			void Heading(string text) => Add(kit.Label(text, 16));
			void Separator()
			{
				var separator = new GuiWidget(1, 1 * DeviceScale)
				{
					HAnchor = HAnchor.Stretch,
					BackgroundColor = demoTheme.Palette.Separator,
				};
				separators.Add(separator);
				Add(separator);
			}

			Text("This is made to test that the agg-gui rendering backend is set up correctly.");
			Separator();

			Heading("Pixel alignment test");
			Text("If anything is blurry, then everything will be blurry, including text.");
			Text("You might need a magnifying glass to check this test.");
			Text("The lines should be exactly one physical pixel wide, one physical pixel apart.");
			Text("They should be perfectly white and black.");
			this.PixelLines = new RenderingTestPixelLines(throughBitmap: false) { Name = "Rendering Test Pixel Lines" };
			Add(this.PixelLines);
			Text("The same two grids, drawn to a bitmap first then blit - must match pixel-for-pixel.");
			this.BitmapLines = new RenderingTestPixelLines(throughBitmap: true) { Name = "Rendering Test Bitmap Lines" };
			Add(this.BitmapLines);
			Separator();

			Heading("Color test");
			Text("If the rendering is done right, all groups of gradients will look uniform.");
			this.Colors = new RenderingTestColors(demoTheme) { Name = "Rendering Test Colors" };
			Add(this.Colors);
			Text("The first square should be exactly one physical pixel big.");
			Text("They should be exactly one physical pixel apart.");
			Text("Each subsequent square should be one physical pixel larger than the previous.");
			Text("They should be perfectly aligned to the physical pixel grid.");
			Add(new RenderingTestPixelSquares(demoTheme, outlined: false) { Name = "Rendering Test Squares" });
			Text("The strokes should align to the physical pixel grid.");
			Add(new RenderingTestPixelSquares(demoTheme, outlined: true) { Name = "Rendering Test Strokes" });
			Separator();

			Heading("Text rendering");
			foreach ((byte fore, byte back) in TextRows)
			{
				Add(new RenderingTestTextOnBackground(demoTheme, new Color(fore, fore, fore), new Color(back, back, back)));
			}

			Separator();
			Text("The left side shows how lines of different widths look.");
			Text("The right side tests text rendering at different opacities and sizes.");
			Text("The top and bottom images should look symmetrical in their intensities.");
			this.Blending = new RenderingTestBlending(demoTheme) { Name = "Rendering Test Blending" };
			Add(this.Blending);

			void Recolor(object sender, EventArgs e)
			{
				this.BackgroundColor = demoTheme.Palette.PanelFill;
				foreach (GuiWidget separator in separators)
				{
					separator.BackgroundColor = demoTheme.Palette.Separator;
				}

				kit.Recolor();
			}

			demoTheme.ThemeChanged += Recolor;
			this.Closed += (s, e) => demoTheme.ThemeChanged -= Recolor;
		}

		/// <summary>Scrolls the tests.</summary>
		public ScrollableWidget ScrollArea { get; }

		/// <summary>The stripes drawn directly.</summary>
		public RenderingTestPixelLines PixelLines { get; }

		/// <summary>The same stripes drawn through a bitmap.</summary>
		public RenderingTestPixelLines BitmapLines { get; }

		/// <summary>The gradient rows.</summary>
		public RenderingTestColors Colors { get; }

		/// <summary>The fine lines and faded text on black and white.</summary>
		public RenderingTestBlending Blending { get; }

		/// <summary>agg-gui's pixel em <paramref name="aggGuiSize"/> as a Graphics2D.DrawString size at this scale.</summary>
		public static double TextSize(DemoTheme demoTheme, double aggGuiSize) => DemoText.Points(aggGuiSize) * DeviceScale;
	}
}
