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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Tests.GoldenImages;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	/// <summary>
	/// The Widget Gallery's empty TextEdit draws its half-transparent "Write something here" hint as the
	/// browser shows it: the whole shell on the GPU, with LCD text on and off, at 1x and 2x device scale, and
	/// after the theme has changed from the one the shell was built in (the browser builds dark, then picks up
	/// the system's light preference). The CPU half is
	/// <see cref="WidgetGalleryWindowTests.EmptyTextEditDrawsItsPlaceholder"/>.
	/// </summary>
	[NotInParallel] // LcdRenderSettings.Enabled, GuiWidget.DeviceScale and ThemeConfig.Current are process-wide
	public class WidgetGalleryPlaceholderGpuTests
	{
		private static ThemePreference Opposite(ThemePreference preference)
			=> preference == ThemePreference.Light ? ThemePreference.Dark : ThemePreference.Light;

		[Test]
		[Arguments(ThemePreference.Light, false, 1)]
		[Arguments(ThemePreference.Dark, false, 1)]
		[Arguments(ThemePreference.Light, true, 1)]
		[Arguments(ThemePreference.Dark, true, 1)]
		[Arguments(ThemePreference.Light, false, 2)]
		[Arguments(ThemePreference.Dark, false, 2)]
		[Arguments(ThemePreference.Light, true, 2)]
		[Arguments(ThemePreference.Dark, true, 2)]
		public async Task EmptyTextEditDrawsItsPlaceholderOnTheGpu(ThemePreference preference, bool lcd, double deviceScale)
		{
			bool wasLcd = LcdRenderSettings.Enabled;
			double wasScale = GuiWidget.DeviceScale;
			LcdRenderSettings.Enabled = lcd;
			GuiWidget.DeviceScale = deviceScale;
			try
			{
				// The whole shell, as the browser shows it: the gallery sits in a window (rounded, so composited
				// through its own backbuffer) over the shell, not bare on the page.
				var page = new GuiWidget(1200 * deviceScale, 800 * deviceScale);
				var shell = new GuiDemoShell(new DemoTheme(Opposite(preference)));
				shell.DemoTheme.SetPreference(preference);
				page.AddChild(shell);
				page.PerformLayout();
				var gallery = (WidgetGalleryWindow)shell.FindDescendant("Widget Gallery Content");

				ThemedTextEditWidget field = gallery.TextField;
				TextWidget hint = field.NoContentFieldDescription;
				await Assert.That(hint.Visible).IsTrue();

				// The whole page, so the field is reached through every parent (and backbuffer) the demo has.
				RectangleDouble fieldBounds = field.TransformToScreenSpace(field.LocalBounds);
				RectangleDouble hintBounds = hint.TransformToScreenSpace(hint.LocalBounds);
				int width = (int)Math.Ceiling(page.Width);
				int height = (int)Math.Ceiling(page.Height);

				using var capture = WebGpuOffscreenCapture.Create(width, height);
				Color fill = field.BackgroundColor;
				var frame = capture.BeginWidgetFrame(new ColorF(fill.Red0To1, fill.Green0To1, fill.Blue0To1, 1));
				page.OnDraw(frame);
				ImageBuffer image = await capture.CaptureAsync();
				await Assert.That(capture.Device.LastUncapturedError).IsNull();

				int inked = 0;
				for (int y = (int)Math.Max(hintBounds.Bottom, fieldBounds.Bottom); y < (int)Math.Min(hintBounds.Top, fieldBounds.Top); y++)
				{
					for (int x = (int)Math.Max(hintBounds.Left, fieldBounds.Left); x < (int)Math.Min(hintBounds.Right, fieldBounds.Right); x++)
					{
						Color pixel = image.GetPixel(x, y);
						if (Math.Abs(pixel.red - fill.red) + Math.Abs(pixel.green - fill.green) + Math.Abs(pixel.blue - fill.blue) > 90)
						{
							inked++;
						}
					}
				}

				await Assert.That(inked).IsGreaterThan(20)
					.Because($"{preference}, lcd {lcd}, scale {deviceScale}: the hint at {hintBounds} in the field at {fieldBounds} should draw ink");
				page.Close();
			}
			finally
			{
				LcdRenderSettings.Enabled = wasLcd;
				GuiWidget.DeviceScale = wasScale;
			}
		}
	}
}
