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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Tools;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's System window (agg-gui's demo-ui/src/windows/system.rs). Its controls write process-wide text
	// settings that every text test reads, so the tests that touch them are keyless [NotInParallel] (as
	// TypeFacePrinterSnapBaselineTests and GoldenTextTests are) and restore every setting in a finally block.
	public class SystemTypographyWindowTests
	{
		private static DemoSpec SystemSpec => GuiDemoSpecs.All.First(s => s.Title == "System");

		private static (GuiWidget Page, SystemTypographyWindow Window) Build()
		{
			var page = new GuiWidget(520 * GuiWidget.DeviceScale, 640 * GuiWidget.DeviceScale);
			GuiWidget content = GuiDemoSpecs.CreateContent(SystemSpec);
			page.AddChild(content);
			page.PerformLayout();
			return (page, (SystemTypographyWindow)content);
		}

		private static void Draw(GuiWidget page)
		{
			var image = new ImageBuffer((int)page.Width, (int)page.Height);
			page.OnDraw(image.NewGraphics2D());
		}

		[Test]
		[NotInParallel]
		public async Task BuildsBothTabsOnTheCurrentSettingsWithoutChangingThem()
		{
			bool wasLcd = LcdRenderSettings.Enabled;
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			double wasGamma = LcdRenderSettings.Gamma;
			double wasWeight = LcdRenderSettings.PrimaryWeight;
			TypeFace wasFont = AggContext.DefaultFont;
			long wasStyleEpoch = TextStyleSettings.Epoch;

			(GuiWidget page, SystemTypographyWindow window) = Build();
			Draw(page);

			await Assert.That(window.Name).IsEqualTo("System Content");
			foreach (string name in new[]
			{
				"System Tabs", SystemTypographyWindow.TabName("Font"), SystemTypographyWindow.TabName("Sample Text"),
				"System Font", "System LCD", "System Hinting", "System Gamma", "System Primary Weight", "System Sample Text",
				"System Point Size", "System Width", "System Interval", "System Faux Weight", "System Faux Italic",
			})
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull().Because(name);
			}

			await Assert.That(window.Tabs.SelectedIndex).IsEqualTo(0);
			await Assert.That(window.FontTab.Lcd.Checked).IsEqualTo(wasLcd);
			await Assert.That(window.FontTab.Hinting.Checked).IsEqualTo(wasSnapping);
			await Assert.That(window.FontTab.Gamma.Value).IsEqualTo(wasGamma).Within(1e-9);
			await Assert.That(window.FontTab.PrimaryWeight.Value).IsEqualTo(wasWeight).Within(.01);
			await Assert.That(window.FontTab.Font.SelectedIndex).IsEqualTo(SystemFontTab.CurrentFontIndex());
			await Assert.That(window.FontTab.PointSize.Value).IsEqualTo(TextStyleSettings.SizeScale * SystemFontTab.BasePointSize).Within(1e-9);
			await Assert.That(window.FontTab.GlyphWidth.Value).IsEqualTo(TextStyleSettings.Width).Within(1e-9);
			await Assert.That(window.FontTab.Interval.Value).IsEqualTo(TextStyleSettings.Interval).Within(1e-9);
			await Assert.That(window.FontTab.FauxWeight.Value).IsEqualTo(TextStyleSettings.FauxWeight).Within(1e-9);
			await Assert.That(window.FontTab.FauxItalic.Value).IsEqualTo(TextStyleSettings.FauxItalic).Within(1e-9);
			await Assert.That(TextStyleSettings.Epoch).IsEqualTo(wasStyleEpoch);

			// Building and drawing the window is not a change.
			await Assert.That(LcdRenderSettings.Enabled).IsEqualTo(wasLcd);
			await Assert.That(TypeFacePrinter.SnapBaselinesToWholePixels).IsEqualTo(wasSnapping);
			await Assert.That(LcdRenderSettings.Gamma).IsEqualTo(wasGamma);
			await Assert.That(LcdRenderSettings.PrimaryWeight).IsEqualTo(wasWeight);
			await Assert.That(AggContext.DefaultFont).IsSameReferenceAs(wasFont);
			await Assert.That(window.SampleText.FindDescendant("System Sample Text").Children.Last().Children.Count)
				.IsEqualTo(SystemSampleTextTab.Paragraphs.Length);
		}

		[Test]
		[NotInParallel]
		public async Task ControlsWriteTheProcessWideTextSettings()
		{
			bool wasLcd = LcdRenderSettings.Enabled;
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			double wasGamma = LcdRenderSettings.Gamma;
			double wasWeight = LcdRenderSettings.PrimaryWeight;
			TypeFace wasFont = AggContext.DefaultFont;
			try
			{
				(GuiWidget page, SystemTypographyWindow window) = Build();
				SystemFontTab tab = window.FontTab;

				tab.Lcd.Checked = !wasLcd;
				await Assert.That(LcdRenderSettings.Enabled).IsEqualTo(!wasLcd);
				tab.Hinting.Checked = !wasSnapping;
				await Assert.That(TypeFacePrinter.SnapBaselinesToWholePixels).IsEqualTo(!wasSnapping);
				tab.Gamma.Value = 1.8;
				await Assert.That(LcdRenderSettings.Gamma).IsEqualTo(1.8).Within(1e-9);
				tab.PrimaryWeight.Value = .5;
				await Assert.That(LcdRenderSettings.PrimaryWeight).IsEqualTo(.5).Within(1e-9);

				tab.Font.SelectedIndex = 1;
				await Assert.That(AggContext.DefaultFont).IsSameReferenceAs(LiberationSansBoldFont.Instance);

				// The picker names each font in its own face, the closed field included.
				await Assert.That(tab.Font.SelectedTypeFace).IsSameReferenceAs(LiberationSansBoldFont.Instance);
				await Assert.That(tab.Font.Descendants<TextWidget>().First().Printer.TypeFaceStyle.TypeFace)
					.IsSameReferenceAs(LiberationSansBoldFont.Instance);

				// The preview rebuilds its paragraphs in the new face.
				var paragraph = (WrappedTextWidget)window.SampleText.FindDescendant("System Sample Text").Children.Last().Children.First();
				TextWidget line = paragraph.Descendants<TextWidget>().First();
				await Assert.That(line.Printer.TypeFaceStyle.TypeFace).IsSameReferenceAs(LiberationSansBoldFont.Instance);
				Draw(page);
			}
			finally
			{
				LcdRenderSettings.Enabled = wasLcd;
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
				LcdRenderSettings.Gamma = wasGamma;
				LcdRenderSettings.PrimaryWeight = wasWeight;
				AggContext.DefaultFont = wasFont;
			}
		}

		/// <summary>agg-gui's point size, Width, Interval, Faux Weight and Faux Italic controls drive <see cref="TextStyleSettings"/>.</summary>
		[Test]
		[NotInParallel]
		public async Task StyleControlsWriteTheTextStyleSettings()
		{
			try
			{
				(GuiWidget page, SystemTypographyWindow window) = Build();
				SystemFontTab tab = window.FontTab;
				double regularEm = FirstPreviewLine(window).Printer.TypeFaceStyle.EmSizeInPoints;

				tab.GlyphWidth.Value = 1.2;
				await Assert.That(TextStyleSettings.Width).IsEqualTo(1.2).Within(1e-9);
				tab.Interval.Value = .1;
				await Assert.That(TextStyleSettings.Interval).IsEqualTo(.1).Within(1e-9);
				tab.FauxWeight.Value = .5;
				await Assert.That(TextStyleSettings.FauxWeight).IsEqualTo(.5).Within(1e-9);
				tab.FauxItalic.Value = -.5;
				await Assert.That(TextStyleSettings.FauxItalic).IsEqualTo(-.5).Within(1e-9);

				tab.PointSize.ActuallNumberEdit.Value = 21;
				tab.PointSize.ActuallNumberEdit.InternalTextEditWidget.OnEditComplete(EventArgs.Empty);
				await Assert.That(TextStyleSettings.SizeScale).IsEqualTo(1.5).Within(1e-9);

				// The preview rebuilds its paragraphs at the new size.
				await Assert.That(FirstPreviewLine(window).Printer.TypeFaceStyle.EmSizeInPoints).IsEqualTo(regularEm * 1.5).Within(1e-9);
				Draw(page);
			}
			finally
			{
				TextStyleSettings.Reset();
			}
		}

		/// <summary>The point size field copies its text colour when built, so a theme change has to recolour it.</summary>
		[Test]
		public async Task ThemeChangeRecoloursThePointSizeField()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (SystemTypographyWindow)GuiDemoSpecs.CreateContent(SystemSpec, demoTheme);
			demoTheme.SetPreference(ThemePreference.Dark);

			await Assert.That(window.FontTab.PointSize.ActuallNumberEdit.InternalTextEditWidget.TextColor)
				.IsEqualTo(demoTheme.Theme.EditFieldColors.Inactive.TextColor);
			await Assert.That(demoTheme.Theme.EditFieldColors.Inactive.TextColor).IsEqualTo(DemoPalette.Dark.TextColor);
		}

		private static TextWidget FirstPreviewLine(SystemTypographyWindow window)
		{
			var paragraph = (WrappedTextWidget)window.SampleText.FindDescendant("System Sample Text").Children.Last().Children.First();
			return paragraph.Descendants<TextWidget>().First();
		}
	}
}
