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

using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	public class TabViewTests
	{
		[Test]
		public async Task TabsShareTheStripAndPressingOneShowsItsPage()
		{
			var container = new GuiWidget(300, 200);
			var tabView = new TabView();
			container.AddChild(tabView);
			var first = new GuiWidget();
			var second = new GuiWidget();
			var third = new GuiWidget();
			tabView.AddTab("One", first);
			GuiWidget secondTab = tabView.AddTab("Two", second, "Tab Two");
			tabView.AddTab("Three", third);
			container.PerformLayout();

			// The first tab added is selected; the others' pages are hidden.
			await Assert.That(tabView.SelectedIndex).IsEqualTo(0);
			await Assert.That(first.Visible && !second.Visible && !third.Visible).IsTrue();

			// Three equal tabs across the full width, along the top.
			await Assert.That(secondTab.Width).IsEqualTo(100);
			await Assert.That(secondTab.TransformToParentSpace(tabView, secondTab.LocalBounds).Left).IsEqualTo(100);
			await Assert.That(container.FindDescendant("Tab Two")).IsEqualTo(secondTab);

			int changes = 0;
			tabView.SelectedIndexChanged += (s, e) => changes++;
			secondTab.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 50, 10, 0));
			await Assert.That(tabView.SelectedIndex).IsEqualTo(1);
			await Assert.That(tabView.SelectedPage).IsEqualTo(second);
			await Assert.That(!first.Visible && second.Visible && !third.Visible).IsTrue();
			await Assert.That(changes).IsEqualTo(1);

			// The page fills everything under the strip.
			RectangleDouble page = second.Parent.TransformToParentSpace(tabView, second.Parent.LocalBounds);
			await Assert.That(page.Top).IsEqualTo(200 - tabView.BarHeight * GuiWidget.DeviceScale);
			await Assert.That(page.Bottom).IsEqualTo(0);
		}
	}
}
