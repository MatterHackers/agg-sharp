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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's sidebar (agg-gui's sidebar.rs) bound to its window host, and the top bar's Demos menu
	// driving the same host through the shell.
	// GuiDemoShell installs its theme as ThemeConfig.Current, which other tests set and read.
	[NotInParallel(new[] { SharedStateKeys.ThemeConfigCurrent, SharedStateKeys.MarkdownWidget })] // the shell can build the About window's MarkdownWidget
	public class DemoSidebarTests
	{
		private static DemoSpec Spec(string title) => GuiDemoSpecs.All.First(s => s.Title == title);

		private static GuiDemoShell CreateShell(DemoTheme demoTheme = null)
		{
			var page = new GuiWidget(1000, 700);
			var shell = new GuiDemoShell(demoTheme);
			page.AddChild(shell);
			page.PerformLayout();
			return shell;
		}

		[Test]
		public async Task EachWindowsIconShowsInItsRowTitleAndDemosMenuItem()
		{
			GuiDemoShell shell = CreateShell(new DemoTheme(ThemePreference.Dark));
			DemoSpec lion = Spec("Lion");

			// The sidebar row: the glyph before the text, lettered like it (white once lit)
			GuiWidget row = shell.Sidebar.RowOf(lion);
			await Assert.That(shell.Sidebar.RowIconOf(lion)).IsEqualTo(lion.Icon);
			RectangleDouble iconBounds = shell.Sidebar.RowIconBoundsOf(lion);
			TextWidget rowText = row.Descendants<TextWidget>().Single();
			await Assert.That(rowText.BoundsRelativeToParent.Left).IsGreaterThanOrEqualTo(iconBounds.Right);

			var image = new Image.ImageBuffer((int)row.LocalBounds.Width, (int)row.LocalBounds.Height);
			var graphics = image.NewGraphics2D();
			graphics.SetTransform(Transform.Affine.NewTranslation(-row.LocalBounds.Left, -row.LocalBounds.Bottom));
			row.OnDraw(graphics);
			bool whiteInIcon = false;
			for (int y = (int)(iconBounds.Bottom - row.LocalBounds.Bottom); y < (int)(iconBounds.Top - row.LocalBounds.Bottom); y++)
			{
				for (int x = (int)(iconBounds.Left - row.LocalBounds.Left); x < (int)(iconBounds.Right - row.LocalBounds.Left); x++)
				{
					Color pixel = image.GetPixel(x, y);
					whiteInIcon |= pixel.red > 230 && pixel.green > 230 && pixel.blue > 230;
				}
			}

			await Assert.That(whiteInIcon).IsTrue();
			await Assert.That(shell.Sidebar.RowIconOf(GuiDemoSpecs.All.First(s => s.Icon == null))).IsNull();

			// The window title: the glyph sits before the title text, coloured like it
			var titleIcon = shell.Windows.GetWindow(lion).TitleBar.Descendants<IconGlyphWidget>().Single();
			await Assert.That(titleIcon.Glyph).IsEqualTo(lion.Icon);
			await Assert.That(titleIcon.Color).IsEqualTo(DemoPalette.Dark.TextColor);
			var siblings = titleIcon.Parent.Children;
			await Assert.That(siblings[siblings.IndexOf(titleIcon) + 1] is TextWidget text && text.Text == lion.Title).IsTrue();

			// The Demos menu item, and none for the Window Resize Test entries, which have no icon
			var demos = shell.TopBar.Menus[0].SubMenuItems();
			var lionItem = demos.Single(g => g.Text == "Graphics").SubMenuItems().Single(i => i.Text == lion.Title);
			await Assert.That(lionItem.IconGlyph).IsEqualTo(lion.Icon);
			await Assert.That(lionItem.IconTypeFace).IsEqualTo(IconFont.TypeFace);
			await Assert.That(demos.Single(g => g.Text == "Window Resize Test").SubMenuItems().All(i => i.IconGlyph == null)).IsTrue();
		}

		[Test]
		public async Task ClickingARowTogglesItsWindowAndTheHostRelightsTheRow()
		{
			GuiDemoShell shell = CreateShell();
			DemoSpec sliders = Spec("Sliders");
			GuiWidget row = shell.Sidebar.RowOf(sliders);
			await Assert.That(row.Name).IsEqualTo("Sidebar Sliders");
			await Assert.That(shell.Sidebar.IsRowOn(sliders)).IsFalse();

			row.InvokeClick();
			await Assert.That(shell.Windows.IsOpen(sliders)).IsTrue();
			await Assert.That(shell.Sidebar.IsRowOn(sliders)).IsTrue();

			row.InvokeClick();
			await Assert.That(shell.Windows.IsOpen(sliders)).IsFalse();
			await Assert.That(shell.Sidebar.IsRowOn(sliders)).IsFalse();

			// The other way: the window's own close button (through the host) turns the row off.
			DemoSpec lion = Spec("Lion");
			await Assert.That(shell.Sidebar.IsRowOn(lion)).IsTrue();
			shell.Windows.GetWindow(lion).TitleBar.Descendants().First(w => w.ToolTipText == "Close").InvokeClick();
			await Assert.That(shell.Sidebar.IsRowOn(lion)).IsFalse();
		}

		[Test]
		public async Task TheAboutRowOpensTheAboutWindow()
		{
			GuiDemoShell shell = CreateShell();
			await Assert.That(shell.Sidebar.AboutRow.Name).IsEqualTo("Sidebar About");
			// Open on a first run, as agg-gui's is; the row closes and reopens it.
			await Assert.That(shell.Windows.IsOpen(GuiDemoSpecs.About)).IsTrue();
			shell.Sidebar.AboutRow.InvokeClick();
			await Assert.That(shell.Windows.IsOpen(GuiDemoSpecs.About)).IsFalse();
			await Assert.That(shell.Sidebar.IsRowOn(GuiDemoSpecs.About)).IsFalse();

			shell.Sidebar.AboutRow.InvokeClick();

			WindowWidget about = shell.Windows.GetWindow(GuiDemoSpecs.About);
			await Assert.That(shell.Windows.IsOpen(GuiDemoSpecs.About)).IsTrue();
			await Assert.That(about.Descendants().Any(w => w.Name == GuiDemoSpecs.About.ContentName)).IsTrue();
			await Assert.That(shell.Sidebar.IsRowOn(GuiDemoSpecs.About)).IsTrue();
		}

		[Test]
		public async Task TheDemosMenuTogglesTheWindow()
		{
			GuiDemoShell shell = CreateShell();
			DemoSpec painting = Spec("Painting");
			MenuItemModel item = shell.TopBar.Menus[0].SubMenuItems()
				.Single(g => g.AutomationName == "demos.Graphics").SubMenuItems()
				.Single(i => i.AutomationName == "demo.Painting");

			item.Action();
			await Assert.That(shell.Windows.IsOpen(painting)).IsTrue();
			await Assert.That(shell.Windows.ZOrder.Last()).IsEqualTo(painting);
			await Assert.That(shell.Sidebar.IsRowOn(painting)).IsTrue();

			item.Action();
			await Assert.That(shell.Windows.IsOpen(painting)).IsFalse();
		}

		[Test]
		public async Task SearchAndCollapseHideRowsAndEmptyGroups()
		{
			GuiDemoShell shell = CreateShell();
			DemoSidebar sidebar = shell.Sidebar;

			sidebar.Search.Text = "lion";
			await Assert.That(sidebar.Filter.Query).IsEqualTo("lion");
			await Assert.That(sidebar.RowOf(Spec("Lion")).Visible).IsTrue();
			await Assert.That(sidebar.RowOf(Spec("Painting")).Visible).IsFalse();
			await Assert.That(sidebar.HeaderOf("Graphics").Visible).IsTrue();
			await Assert.That(sidebar.HeaderOf("Widgets").Visible).IsFalse();

			sidebar.Search.Text = "";
			sidebar.HeaderOf("Widgets").InvokeClick();
			await Assert.That(sidebar.Filter.IsCollapsed("Widgets")).IsTrue();
			await Assert.That(sidebar.HeaderOf("Widgets").Visible).IsTrue();
			await Assert.That(sidebar.RowOf(Spec("Sliders")).Visible).IsFalse();
			await Assert.That(sidebar.RowOf(Spec("Lion")).Visible).IsTrue();
		}

		[Test]
		public async Task GroupHeaderDrawsAVectorTriangleThatFollowsTheOpenState()
		{
			GuiDemoShell shell = CreateShell(new DemoTheme(ThemePreference.Dark));
			SidebarGroupHeader header = shell.Sidebar.HeaderOf("Widgets");

			// The name alone is lettered; the ▼/► glyphs the default font lacks are gone.
			TextWidget label = header.Descendants<TextWidget>().Single();
			await Assert.That(label.Text).IsEqualTo("Widgets");
			await Assert.That(label.Position.X).IsEqualTo(SidebarGroupHeader.LabelX * GuiWidget.DeviceScale);

			// Open: a flat top edge with the apex below it (y-up), pointing down.
			await Assert.That(header.IsOpen).IsTrue();
			var open = Points(header.TrianglePath());
			await Assert.That(open.Count).IsEqualTo(3);
			await Assert.That(open[0].Y).IsEqualTo(open[1].Y);
			await Assert.That(open[2].Y).IsLessThan(open[0].Y);
			await Assert.That(open[2].X).IsEqualTo((open[0].X + open[1].X) / 2);

			// Collapsed: a vertical left edge with the apex to its right, pointing right.
			header.InvokeClick();
			await Assert.That(header.IsOpen).IsFalse();
			var closed = Points(header.TrianglePath());
			await Assert.That(closed[0].X).IsEqualTo(closed[1].X);
			await Assert.That(closed[2].X).IsGreaterThan(closed[0].X);
			await Assert.That(closed[2].Y).IsEqualTo((closed[0].Y + closed[1].Y) / 2);

			// Drawn: the band tints the whole row, the top line is the separator, and the triangle is text_dim.
			var image = new Image.ImageBuffer((int)header.Width, (int)header.Height);
			header.OnDraw(image.NewGraphics2D());
			DemoPalette palette = new DemoTheme(ThemePreference.Dark).Palette;
			Color band = image.GetPixel((int)header.Width - 2, (int)header.Height / 2);
			Color top = image.GetPixel((int)header.Width - 2, (int)header.Height - 1);
			await Assert.That(band.alpha).IsEqualTo((byte)Math.Round(255 * SidebarGroupHeader.BandAlpha));
			await Assert.That(top.alpha).IsEqualTo(palette.Separator.alpha);
			Color triangle = image.GetPixel((int)(closed[0].X + 1), (int)closed[2].Y);
			await Assert.That(triangle.alpha).IsGreaterThan((byte)100);
		}

		[Test]
		public async Task SearchPlaceholderLeadsWithAMagnifierThatHidesOnceTyping()
		{
			GuiDemoShell shell = CreateShell();
			DemoSidebar sidebar = shell.Sidebar;

			await Assert.That(sidebar.SearchIcon.Visible).IsTrue();
			await Assert.That(sidebar.Search.NoContentFieldDescription.Position.X).IsGreaterThanOrEqualTo(
				sidebar.SearchIcon.Position.X + sidebar.SearchIcon.Width);

			sidebar.Search.Text = "lion";
			await Assert.That(sidebar.SearchIcon.Visible).IsFalse();
			sidebar.Search.Text = "";
			await Assert.That(sidebar.SearchIcon.Visible).IsTrue();
		}

		private static List<VectorMath.Vector2> Points(VertexSource.IVertexSource path)
		{
			return path.Vertices().Where(v => v.IsMoveTo || v.IsLineTo).Select(v => v.Position).ToList();
		}

		[Test]
		public async Task OrganizeTilesMovedWindowsBack()
		{
			GuiDemoShell shell = CreateShell();
			WindowWidget lion = shell.Windows.GetWindow(Spec("Lion"));
			var tiled = lion.Position;
			lion.Position = new VectorMath.Vector2(3, 4);

			shell.Sidebar.Descendants().Single(w => w.Name == "Sidebar Organize").InvokeClick();

			await Assert.That(lion.Position).IsEqualTo(tiled);
		}

		[Test]
		public async Task HeadingSitsTwelvePixelsIn()
		{
			GuiDemoShell shell = CreateShell();
			GuiWidget heading = shell.Sidebar.Descendants().Single(w => w.Name == "Sidebar Heading");

			// agg-gui's heading margin; HAnchor.Left makes the flow honour it.
			double left = heading.Position.X + shell.Sidebar.Position.X;
			await Assert.That(left).IsEqualTo(780 + 12 * GuiWidget.DeviceScale);
		}
	}
}
