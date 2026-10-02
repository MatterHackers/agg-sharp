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
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)] // new DemoTheme() writes ThemeConfig.Current
	public class ThreeDAnimationWindowTests
	{
		[Test]
		public async Task TheSsaaSegmentsSetTheBarGridsFactor()
		{
			var spec = GuiDemoSpecs.All.First(s => s.Title == "3D Animation");
			var demoTheme = new DemoTheme();
			var window = (ThreeDAnimationWindow)GuiDemoSpecs.CreateContent(spec, demoTheme);

			await Assert.That(window.Name).IsEqualTo(spec.ContentName);
			await Assert.That(window.FindDescendant("3D Animation Bar Grid")).IsSameReferenceAs(window.BarGrid);
			await Assert.That(window.SsaaButtons.Select(b => b.Text)).IsEquivalentTo(ThreeDAnimationWindow.SsaaLabels, TUnit.Assertions.Enums.CollectionOrdering.Matching);

			// Starts Off (linear 1), as agg-gui's cube does; each button is its linear factor.
			Color accent = DemoTheme.ColorOf(demoTheme.Accent);
			await Assert.That(window.SsaaButtons.Single(b => b.BackgroundColor == accent).Text).IsEqualTo("Off");
			await Assert.That(window.BarGrid.SsaaFactor).IsEqualTo(1);
			await Assert.That(window.Status.Text).StartsWith("Off");
			window.SsaaButtons[3].InvokeClick();
			await Assert.That(window.BarGrid.SsaaFactor).IsEqualTo(4);
			window.SsaaButtons[0].InvokeClick();
			await Assert.That(window.BarGrid.SsaaFactor).IsEqualTo(1);
		}

		[Test]
		public async Task TheStatusLineReadsLikeAggGuis()
		{
			await Assert.That(ThreeDAnimationWindow.StatusCaption(1, 300, 182)).IsEqualTo("Off  (300 × 182 = 0.4 MB)");
			await Assert.That(ThreeDAnimationWindow.StatusCaption(2, 300, 182)).IsEqualTo("2× linear · 4× memory  (300 × 182 = 1.7 MB)");
			await Assert.That(ThreeDAnimationWindow.StatusCaption(3, 0, 0)).IsEqualTo("3× linear · 9× memory");

			var spec = GuiDemoSpecs.All.First(s => s.Title == "3D Animation");
			var window = (ThreeDAnimationWindow)GuiDemoSpecs.CreateContent(spec, new DemoTheme());
			var page = new MatterHackers.Agg.UI.GuiWidget(300, 260);
			page.AddChild(window);
			page.PerformLayout();
			window.SsaaFactor = 1;

			// The line follows the bar grid's laid out size.
			await Assert.That(window.BarGrid.Width).IsGreaterThan(0);
			await Assert.That(window.Status.Text).IsEqualTo(ThreeDAnimationWindow.StatusCaption(1, (int)System.Math.Round(window.BarGrid.Width), (int)System.Math.Round(window.BarGrid.Height)));
			await Assert.That(window.Status.Text).StartsWith("Off  (300 × ");
		}
	}
}
