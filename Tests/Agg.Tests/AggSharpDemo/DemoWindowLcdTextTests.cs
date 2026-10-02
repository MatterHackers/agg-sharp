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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	/// <summary>
	/// The GUI demo's own windows, built the way the demo shell builds them (rounded, buffered, scrolled), show
	/// LCD subpixel text when the System window's LCD toggle is on - what a user actually looks at.
	/// </summary>
	// Keyless, so it runs alone: it writes GuiWidget.DeviceScale and LcdRenderSettings.Enabled, which every
	// layout and text raster in the process reads - no key names all their readers. That also covers
	// ThemeConfig.Current (new DemoTheme()) and the MarkdownWidget/UiThread the About window touches.
	[NotInParallel]
	public class DemoWindowLcdTextTests
	{
		[Test]
		[Arguments("Widget Gallery")]
		[Arguments("System")]
		public async Task LabelsInADemoWindowGetSubpixelColourWithLcdOn(string title)
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			double wasScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = 1;
				LcdRenderSettings.Enabled = false;
				var canvas = new GuiWidget(1400, 900);
				var host = new DemoWindowHost(canvas);
				DemoSpec spec = GuiDemoSpecs.All.First(s => s.Title == title);
				host.SetOpen(spec, true);
				host.Raise(spec);
				canvas.PerformLayout();
				WindowWidget window = host.GetWindow(spec);

				ImageBuffer grey = Draw(canvas);
				LcdRenderSettings.Enabled = true;
				ImageBuffer lcd = Draw(canvas);

				// Shown labels wholly inside the raised window's client area (so not scrolled out of view). No system
				// window hosts the canvas, so visibility is the Visible chain up to it rather than ActuallyVisibleOnScreen.
				RectangleDouble client = window.ClientArea.TransformToScreenSpace(window.ClientArea.LocalBounds);
				var labels = window.ClientArea.Descendants<TextWidget>()
					.Where(t => IsShown(t, canvas) && t.Text.Trim().Length > 1)
					.Select(t => t.TransformToScreenSpace(t.LocalBounds))
					.Where(r => r.Left >= client.Left && r.Bottom >= client.Bottom && r.Right <= client.Right && r.Top <= client.Top && r.Height > 4)
					.ToList();
				int fringed = labels.Count(r => GainedChroma(grey, lcd, r));

				await Assert.That(labels.Count).IsGreaterThan(3).Because("the window has to show some labels");
				await Assert.That(fringed * 2 > labels.Count).IsTrue()
					.Because($"most of the {title} window's labels have to be subpixel with LCD on, only {fringed} of {labels.Count} were");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
				GuiWidget.DeviceScale = wasScale;
			}
		}

		private static bool IsShown(GuiWidget widget, GuiWidget canvas)
		{
			for (GuiWidget current = widget; current != null && current != canvas; current = current.Parent)
			{
				if (!current.Visible)
				{
					return false;
				}
			}

			return true;
		}

		private static ImageBuffer Draw(GuiWidget canvas)
		{
			var image = new ImageBuffer((int)canvas.Width, (int)canvas.Height);
			Graphics2D graphics2D = image.NewGraphics2D();
			graphics2D.Clear(Color.White);
			canvas.OnDraw(graphics2D);
			return image;
		}

		// The colour LCD text adds: a pixel neutral in the greyscale frame and coloured in the LCD one, so a
		// label on a coloured background does not count as fringed.
		private static bool GainedChroma(ImageBuffer grey, ImageBuffer lcd, RectangleDouble area)
		{
			for (int y = (int)area.Bottom; y < (int)area.Top; y++)
			{
				for (int x = (int)area.Left; x < (int)area.Right; x++)
				{
					Color before = grey.GetPixel(x, y);
					Color after = lcd.GetPixel(x, y);
					if (Math.Abs(before.Red0To255 - before.Blue0To255) <= 8
						&& Math.Abs(after.Red0To255 - after.Blue0To255) > 24)
					{
						return true;
					}
				}
			}

			return false;
		}
	}
}
