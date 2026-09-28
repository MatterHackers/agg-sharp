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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)] // new DemoTheme() writes ThemeConfig.Current
	public class DancingStringsWindowTests
	{
		private static DancingStringsWindow Build()
		{
			var spec = GuiDemoSpecs.All.First(s => s.Title == "Dancing Strings");
			var window = (DancingStringsWindow)GuiDemoSpecs.CreateContent(spec, new DemoTheme());
			var host = new GuiWidget(spec.DefaultWidth, spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();

			// Frozen, so a test draw asks for no idle frame.
			window.View.FixedTimeSeconds = 0.4;
			return window;
		}

		[Test]
		public async Task BuildsTheColoredCheckBoxAndView()
		{
			var window = Build();

			await Assert.That(window.FindDescendant("Dancing Strings Colored")).IsSameReferenceAs(window.ColoredCheckBox);
			await Assert.That(window.FindDescendant("Dancing Strings View")).IsSameReferenceAs(window.View);
			await Assert.That(window.View.Height).IsGreaterThan(100);

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			await Assert.That(window.View.FrameRequested).IsFalse();
		}

		[Test]
		public async Task ColoredTogglesTheGradientAndTimeMovesTheStrings()
		{
			var window = Build();
			var view = window.View;

			window.ColoredCheckBox.Checked = true;
			await Assert.That(view.Colored).IsTrue();
			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());

			// The ends are pinned to the middle line; the belly moves with time.
			await Assert.That(Math.Abs(view.PointAt(2, 0, 0.4).Y - (view.Height / 2))).IsLessThan(1e-9);
			await Assert.That(view.PointAt(2, DancingStringsView.Segments, 0.4).X).IsEqualTo(view.Width);
			await Assert.That(view.PointAt(2, 30, 0.4).Y).IsNotEqualTo(view.PointAt(2, 30, 0.9).Y);
		}
	}
}
