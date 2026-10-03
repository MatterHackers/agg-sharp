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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	public class TabViewTests
	{
		private static (GuiWidget Container, TabView TabView) Build(double width, params string[] labels)
		{
			var container = new GuiWidget(width, 200);
			var tabView = new TabView();
			container.AddChild(tabView);
			foreach (string label in labels)
			{
				tabView.AddTab(label, new GuiWidget());
			}

			container.PerformLayout();
			return (container, tabView);
		}

		private static RectangleDouble BoundsIn(TabView tabView, GuiWidget tab) => tab.TransformToParentSpace(tabView, tab.LocalBounds);

		private static double LabelledWidth(TabView tabView, string label) =>
			new TypeFacePrinter(label, tabView.PointSize * GuiWidget.DeviceScale).GetSize().X + 2 * TabView.TabLabelPadding * GuiWidget.DeviceScale;

		[Test]
		public async Task FolderTabsAreSizedToTheirLabelsAndPressingOneShowsItsPage()
		{
			var container = new GuiWidget(300, 200);
			var tabView = new TabView();
			container.AddChild(tabView);
			var first = new GuiWidget();
			var second = new GuiWidget();
			var third = new GuiWidget();
			GuiWidget firstTab = tabView.AddTab("One", first);
			GuiWidget secondTab = tabView.AddTab("Two", second, "Tab Two");
			GuiWidget thirdTab = tabView.AddTab("Three", third);
			container.PerformLayout();
			double scale = GuiWidget.DeviceScale;

			// The first tab added is selected; the others' pages are hidden.
			await Assert.That(tabView.SelectedIndex).IsEqualTo(0);
			await Assert.That(first.Visible && !second.Visible && !third.Visible).IsTrue();

			// Each tab is its label plus padding, left-aligned after the inset with a small gap between, and the band
			// shows above them. Edges are whole pixels, so allow a pixel of rounding.
			await Assert.That(BoundsIn(tabView, firstTab).Left).IsEqualTo(TabView.TabInset * scale).Within(1);
			await Assert.That(secondTab.Width).IsEqualTo(LabelledWidth(tabView, "Two")).Within(1);
			await Assert.That(thirdTab.Width).IsEqualTo(LabelledWidth(tabView, "Three")).Within(1);
			await Assert.That(thirdTab.Width).IsGreaterThan(secondTab.Width).Because("a longer label gets a wider tab");
			await Assert.That(BoundsIn(tabView, secondTab).Left - BoundsIn(tabView, firstTab).Right).IsEqualTo(TabView.TabGap * scale).Within(1);
			await Assert.That(BoundsIn(tabView, thirdTab).Right).IsLessThan(300 - TabView.TabInset * scale).Because("the tabs fit, so they don't stretch");
			await Assert.That(BoundsIn(tabView, secondTab).Top).IsEqualTo(200 - TabView.TabTopPadding * scale).Within(1);
			await Assert.That(BoundsIn(tabView, secondTab).Bottom).IsEqualTo(200 - tabView.BarHeight * scale);
			await Assert.That(container.FindDescendant("Tab Two")).IsEqualTo(secondTab);

			int changes = 0;
			tabView.SelectedIndexChanged += (s, e) => changes++;
			secondTab.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			await Assert.That(tabView.SelectedIndex).IsEqualTo(1);
			await Assert.That(tabView.SelectedPage).IsEqualTo(second);
			await Assert.That(!first.Visible && second.Visible && !third.Visible).IsTrue();
			await Assert.That(changes).IsEqualTo(1);

			// The page fills everything under the strip.
			RectangleDouble page = second.Parent.TransformToParentSpace(tabView, second.Parent.LocalBounds);
			await Assert.That(page.Top).IsEqualTo(200 - tabView.BarHeight * GuiWidget.DeviceScale);
			await Assert.That(page.Bottom).IsEqualTo(0);
		}

		[Test]
		public async Task TabsThatDoNotFitShareTheWidthInsideTheInsetsEqually()
		{
			(GuiWidget _, TabView tabView) = Build(160, "Alpha Beta", "Gamma Delta", "Epsilon Zeta");
			double scale = GuiWidget.DeviceScale;
			await Assert.That(LabelledWidth(tabView, "Alpha Beta") * 3).IsGreaterThan(160).Because("the case needs labels that can't fit");

			double expected = (160 - 2 * TabView.TabInset * scale - 2 * TabView.TabGap * scale) / 3;
			foreach (GuiWidget tab in tabView.Tabs)
			{
				await Assert.That(tab.Width).IsEqualTo(expected).Within(1).Because($"'{tab.Text}' should get an equal share");
			}

			await Assert.That(BoundsIn(tabView, tabView.Tabs[0]).Left).IsEqualTo(TabView.TabInset * scale).Within(1);
			await Assert.That(BoundsIn(tabView, tabView.Tabs[2]).Right).IsEqualTo(160 - TabView.TabInset * scale).Within(1);
		}

		/// <summary>The selected tab covers the strip's separator line in the page colour, so it opens into the page;
		/// an idle tab leaves the line showing.</summary>
		[Test]
		public async Task TheSelectedTabIsPageColouredOverTheSeparatorLine()
		{
			(GuiWidget container, TabView tabView) = Build(300, "One", "Two");
			var bar = new Color(200, 40, 40);
			var page = new Color(40, 200, 40);
			var separator = new Color(40, 40, 200);
			tabView.BarColor = bar;
			tabView.PageColor = page;
			tabView.SeparatorColor = separator;

			var image = new ImageBuffer((int)container.Width, (int)container.Height);
			container.OnDraw(image.NewGraphics2D());

			RectangleDouble selected = BoundsIn(tabView, tabView.Tabs[0]);
			RectangleDouble idle = BoundsIn(tabView, tabView.Tabs[1]);
			int lineRow = (int)(selected.Bottom + 0.5 * GuiWidget.DeviceScale);

			// Off-centre so the label can't land on the samples.
			int selectedX = (int)(selected.Left + 6);
			int idleX = (int)(idle.Left + 6);
			await Assert.That(image.GetPixel(selectedX, lineRow)).IsEqualTo(page).Because("the selected tab should hide the separator under it");
			await Assert.That(image.GetPixel(idleX, lineRow)).IsEqualTo(separator).Because("an idle tab leaves the separator showing");
			await Assert.That(image.GetPixel((int)selected.Center.X, (int)selected.Top - 1)).IsEqualTo(separator).Because("the selected tab is outlined along its top");
			await Assert.That(image.GetPixel((int)selected.Left, (int)(selected.Bottom + 4))).IsEqualTo(separator).Because("the selected tab is outlined down its left");
			await Assert.That(image.GetPixel(idleX, (int)(idle.Bottom + 4))).IsEqualTo(bar).Because("an idle tab has no fill");
			await Assert.That(image.GetPixel(2, (int)(selected.Bottom + 4))).IsEqualTo(bar).Because("the band shows left of the first tab");
		}

		/// <summary>The separator and the selected tab's outline are single whole device-pixel rows and columns at any
		/// DeviceScale, even when the strip sits at a fractional pixel position (36 units at 1.1 is 39.6 px).</summary>
		[Test]
		[NotInParallel] // DeviceScale is process wide
		[Arguments(2.0)]
		[Arguments(1.25)]
		[Arguments(1.1)]
		public async Task OutlineAndSeparatorAreCrispPixelRowsAtAnyScale(double scale)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = scale;
				var bar = new Color(200, 40, 40);
				var page = new Color(40, 200, 40);
				var separator = new Color(40, 40, 200);

				// A fractional offset as well, so the strip's origin is off the pixel grid at every scale.
				var container = new GuiWidget(300, 200);
				var holder = new GuiWidget(299, 199)
				{
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Absolute,
					OriginRelativeParent = new VectorMath.Vector2(.3, .4),
				};
				container.AddChild(holder);
				var tabView = new TabView { BarColor = bar, PageColor = page, SeparatorColor = separator };
				holder.AddChild(tabView);
				tabView.AddTab("One", new GuiWidget());
				tabView.AddTab("Two", new GuiWidget());
				container.PerformLayout();

				var image = new ImageBuffer((int)container.Width, (int)container.Height);
				container.OnDraw(image.NewGraphics2D());

				RectangleDouble selected = tabView.Tabs[0].TransformToParentSpace(container, tabView.Tabs[0].LocalBounds);
				RectangleDouble idle = tabView.Tabs[1].TransformToParentSpace(container, tabView.Tabs[1].LocalBounds);

				// Down an idle tab: band, then exactly one separator row, then no more separator.
				int idleX = (int)(idle.Left + 6);
				int lineRow = FirstRowNotColoured(image, idleX, (int)idle.Top + 3, bar);
				await Assert.That(image.GetPixel(idleX, lineRow)).IsEqualTo(separator).Because($"the separator row should be solid at {scale}");
				await Assert.That(image.GetPixel(idleX, lineRow - 1)).IsNotEqualTo(separator).Because($"the separator should be one row at {scale}");

				// Down the selected tab: band, one solid outline row, then page - and page where the separator runs.
				int selectedX = (int)selected.Center.X;
				int topRow = FirstRowNotColoured(image, selectedX, (int)selected.Top + 3, bar);
				await Assert.That(image.GetPixel(selectedX, topRow)).IsEqualTo(separator).Because($"the top outline should be solid at {scale}");
				await Assert.That(image.GetPixel(selectedX, topRow - 1)).IsEqualTo(page).Because($"the top outline should be one row at {scale}");
				await Assert.That(image.GetPixel(selectedX, lineRow)).IsEqualTo(page).Because($"the selected tab should cover the separator at {scale}");

				// Across the selected tab's lower half: band, one solid outline column, then page.
				int row = lineRow + 3;
				int leftColumn = 2; // past the part-pixel left edge of the holder
				while (image.GetPixel(leftColumn, row) == bar)
				{
					leftColumn++;
				}

				await Assert.That(image.GetPixel(leftColumn, row)).IsEqualTo(separator).Because($"the left outline should be solid at {scale}");
				await Assert.That(image.GetPixel(leftColumn + 1, row)).IsEqualTo(page).Because($"the left outline should be one column at {scale}");
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		/// <summary>Scanning down column <paramref name="x"/> from <paramref name="startRow"/> (inside the band, below its part-pixel top edge), the first row that isn't
		/// <paramref name="color"/>.</summary>
		private static int FirstRowNotColoured(ImageBuffer image, int x, int startRow, Color color)
		{
			int y = startRow;
			while (y > 0 && image.GetPixel(x, y) == color)
			{
				y--;
			}

			return y;
		}
	}
}
