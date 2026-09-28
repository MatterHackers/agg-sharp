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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Strip window (agg-gui's strip_demo in demo-ui/src/windows/text_demos/strip_table.rs).
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)] // new DemoTheme() writes ThemeConfig.Current
	public class StripWindowTests
	{
		private static DemoSpec StripSpec => GuiDemoSpecs.All.First(s => s.Title == "Strip");

		private static async Task AssertRect(RectangleDouble actual, double x, double y, double width, double height)
		{
			await Assert.That(actual.Left).IsEqualTo(x).Within(.001);
			await Assert.That(actual.Bottom).IsEqualTo(y).Within(.001);
			await Assert.That(actual.Width).IsEqualTo(width).Within(.001);
			await Assert.That(actual.Height).IsEqualTo(height).Within(.001);
		}

		[Test]
		public async Task RegionsMatchAggGuisReferenceShapeAndLimits()
		{
			// agg-gui's strip_regions_match_reference_shape.
			RectangleDouble[] regions = StripWindow.Regions(400, 300, 14);
			await AssertRect(regions[0], 0, 250, 400, 50);
			await AssertRect(regions[1], 0, 132, 200, 118);
			await AssertRect(regions[2], 200, 171.333, 200, 39.333);
			await AssertRect(regions[3], 105, 43, 120, 60);
			await AssertRect(regions[4], 330, 43, 70, 60);

			// strip_regions_respect_exact_and_at_least_lower_limits: 120 + 70 stay side by side.
			regions = StripWindow.Regions(120, 80, 14);
			await AssertRect(regions[3], 0, 14, 120, 60);
			await AssertRect(regions[4], 120, 14, 70, 60);
			await Assert.That(regions[0].Height).IsGreaterThanOrEqualTo(50);
		}

		[Test]
		public async Task BuildsNamedRegionsThatFollowTheWindowSize()
		{
			var page = new GuiWidget(400 * GuiWidget.DeviceScale, 300 * GuiWidget.DeviceScale);
			var window = (StripWindow)GuiDemoSpecs.CreateContent(StripSpec);
			page.AddChild(window);
			page.PerformLayout();
			await Assert.That(window.Name).IsEqualTo("Strip Content");

			double s = GuiWidget.DeviceScale;
			for (int i = 0; i < 5; i++)
			{
				await Assert.That(window.FindDescendant($"Strip Region {i}")).IsNotNull();
			}

			await AssertRect(window.Region(0).BoundsRelativeToParent, 0, 250 * s, 400 * s, 50 * s);

			// Widening the window stretches the full-width and half-width regions and moves the green one right.
			page.Width = 500 * s;
			page.PerformLayout();
			await AssertRect(window.Region(0).BoundsRelativeToParent, 0, 250 * s, 500 * s, 50 * s);
			await Assert.That(window.Region(1).Width).IsEqualTo(250 * s).Within(.001);
			await Assert.That(window.Region(4).BoundsRelativeToParent.Left).IsEqualTo(430 * s).Within(.001);
		}
	}
}
