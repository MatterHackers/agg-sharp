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

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's SVG Test window (agg-gui's tests/svg.rs); the SvgCompare cases are compare.rs's own tests.
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class SvgTestWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "SVG Test");

		[Test]
		public async Task CompareRequiresExactOpaqueColour()
		{
			SvgCompareResult result = SvgCompare.Compare(new byte[] { 11, 20, 30, 255 }, new byte[] { 10, 20, 30, 255 });
			await Assert.That(result.Pass).IsFalse();
			await Assert.That(result.MismatchedPixels).IsEqualTo(1);
		}

		[Test]
		public async Task CompareAllowsOneLevelOfAlphaButNotSix()
		{
			await Assert.That(SvgCompare.Compare(new byte[] { 20, 40, 60, 128 }, new byte[] { 20, 40, 60, 129 }).Pass).IsTrue();
			await Assert.That(SvgCompare.Compare(new byte[] { 20, 40, 60, 128 }, new byte[] { 20, 40, 60, 134 }).Pass).IsFalse();
		}

		[Test]
		public async Task CompareIgnoresColourUnderZeroAlpha()
		{
			await Assert.That(SvgCompare.Compare(new byte[] { 255, 0, 0, 0 }, new byte[] { 0, 255, 0, 0 }).Pass).IsTrue();
		}

		[Test]
		public async Task ComparePassesUpToATenthOfAPercentOfPixels()
		{
			var reference = new byte[1000 * 4];
			var rendered = new byte[1000 * 4];
			rendered[3] = 255;
			await Assert.That(SvgCompare.Compare(rendered, reference).Pass).IsTrue();
			rendered[7] = 255;
			SvgCompareResult twoOff = SvgCompare.Compare(rendered, reference);
			await Assert.That(twoOff.Pass).IsFalse();
			await Assert.That(twoOff.Ratio).IsEqualTo(.002);
		}

		[Test]
		public async Task ToRgbaPutsTheTopRowFirst()
		{
			var image = new ImageBuffer(1, 2);
			image.SetPixel(0, 1, new Color(10, 20, 30, 255)); // the top row: ImageBuffer rows run bottom-up
			byte[] rgba = SvgCompare.ToRgba(image);
			await Assert.That(rgba.Take(4).ToArray()).IsEquivalentTo(new byte[] { 10, 20, 30, 255 }, CollectionOrdering.Matching);
		}

		[Test]
		public async Task EveryEmbeddedSampleHasItsSvgAndReference()
		{
			var samples = SvgTestSample.LoadAll();
			await Assert.That(samples.Count).IsEqualTo(58);
			foreach (SvgTestSample sample in samples)
			{
				await Assert.That(sample.SvgText).Contains("<svg");
				await Assert.That(sample.Reference.Width).IsGreaterThan(0);
				await Assert.That(sample.Reference.Height).IsGreaterThan(0);
			}

			// The reference is decoded top row first: the rect sample's frame is black at its top-left corner.
			ImageBuffer rect = samples[0].Reference;
			await Assert.That(samples[0].Name).IsEqualTo("shapes/rect/simple-case");
			await Assert.That(rect.GetPixel(rect.Width / 2, rect.Height / 2).green).IsGreaterThan(rect.GetPixel(rect.Width / 2, rect.Height / 2).red);
		}

		// The samples agg/Svg renders within SvgCompare's tolerance. The other basic-shape rows (circle, curves,
		// thin strokes) are off only along antialiased edges - resvg's coverage differs from agg's by more than
		// one alpha level on more than 0.1% of pixels; agg-gui's own AGG renderer misses them by the same margins.
		// Gradient rows are off by one level across the ramp (agg-gui's are too: opaque colour must match exactly).
		// Pattern rows draw their tiles exactly where a tile maps pixel for pixel (simple-case misses only on its
		// rounded corners' antialiasing); elsewhere resvg samples the tile bicubically and agg-sharp bilinearly, so
		// tile edges differ. Image rows are off the same way: resvg scales rasters bicubically, agg-sharp bilinearly,
		// so upscaled pixels differ by a level or more across the whole image (embedded-svg is off on antialiased
		// edges). Text rows are drawn in the references' Noto Sans (the window's font resolver), kerned as resvg
		// kerns them, and are off only along the glyphs' antialiased edges (agg-gui lists all four as incomplete too).
		private static readonly string[] PassingSamples =
		{
			"shapes/rect/simple-case",
			"shapes/path/M-L-L-Z",
			"painting/fill/named-color",
			"painting/fill/currentColor",
			"painting/fill/rgb-color",
			"painting/fill/hsl-with-alpha",
			"painting/fill-rule/nonzero",
			"painting/fill-rule/evenodd",
			"painting/opacity/50percent",
			"painting/opacity/mixed-group-opacity",
			"paint-servers/linearGradient/single-stop-with-opacity-used-by-stroke",
		};

		[Test]
		public async Task TheSupportedSamplesMatchTheirReferences()
		{
			foreach (SvgTestSample sample in SvgTestSample.LoadAll().Where(s => PassingSamples.Contains(s.Name)))
			{
				await Assert.That(sample.RenderError).IsNull();
				await Assert.That(sample.Passes).IsTrue().Because($"{sample.Name}: {sample.Score.Ratio:P2} of pixels differ");
			}
		}

		[Test]
		public async Task BuildsAndDrawsOffscreenAndZoomResizesTheCanvas()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (SvgTestWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme);
			var host = new GuiWidget(960, 620);
			host.AddChild(window);
			host.PerformLayout();

			await Assert.That(window.Name).IsEqualTo("SVG Test Content");
			await Assert.That(window.Summary.Text).IsEqualTo($"{window.PassCount} of 58 samples match their reference");

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());

			double halfWidth = window.Canvas.Width;
			window.Canvas.Zoom = 1;
			await Assert.That(window.Canvas.Width).IsGreaterThan(halfWidth * 1.5);
			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(demoTheme.Palette.PanelFill);
		}

		[Test]
		public async Task ZoomButtonsAndHoldingARenderShowItsDiff()
		{
			var window = (SvgTestWindow)GuiDemoSpecs.CreateContent(Spec, new DemoTheme(ThemePreference.Light));
			var systemWindow = new SystemWindow(960, 620) { Name = "SVG Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				SvgTestCanvas canvas = window.Canvas;
				testRunner.ClickByName("SVG Test Zoom 100");
				testRunner.WaitFor(() => canvas.Zoom == 1);
				testRunner.ClickByName("SVG Test Zoom 50");
				testRunner.WaitFor(() => canvas.Zoom == .5);

				Vector2 center = canvas.ImageBounds(0, 1).Center;
				var offset = new Point2D((int)(center.X - canvas.LocalBounds.Left), (int)(center.Y - canvas.LocalBounds.Bottom));
				testRunner.DragByName("SVG Test Canvas", offset: offset, origin: AutomationRunner.ClickOrigin.LowerLeft);
				testRunner.WaitFor(() => canvas.HeldRow == 0);
				await Assert.That(canvas.Samples[0].Diff.Width).IsEqualTo(canvas.Samples[0].Reference.Width);
				testRunner.DropByName("SVG Test Canvas", offset: offset, origin: AutomationRunner.ClickOrigin.LowerLeft);
				testRunner.WaitFor(() => canvas.HeldRow == -1);
				await Assert.That(canvas.HeldRow).IsEqualTo(-1);
				testRunner.MarkTestComplete();
			});
		}
	}
}
