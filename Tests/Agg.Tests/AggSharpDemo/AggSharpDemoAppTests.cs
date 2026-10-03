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
using System.Reflection;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// GuiDemoShell installs its theme as ThemeConfig.Current, which other tests set and read.
	[NotInParallel(new[] { SharedStateKeys.ThemeConfigCurrent, SharedStateKeys.MarkdownWidget })] // the shell can build the About window's MarkdownWidget
	public class AggSharpDemoAppTests
	{
		[Test]
		public async Task TheTabStripAcrossTheTopHasBothTabsAndOpensOnTheAggDemosLion()
		{
			AggSharpDemoApp app = LaidOutApp(1000, 700);

			await Assert.That(app.Tabs.Tabs.Select(t => t.Text)).IsEquivalentTo(
				new[] { AggSharpDemoApp.AggDemosLabel, AggSharpDemoApp.AggSharpDemosLabel }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
			foreach (GuiWidget tab in app.Tabs.Tabs)
			{
				RectangleDouble bounds = BoundsIn(app, tab);
				await Assert.That(bounds.Top).IsEqualTo(app.LocalBounds.Top - TabView.TabTopPadding * GuiWidget.DeviceScale).Within(1)
					.Because($"'{tab.Text}' should sit along the top, under the band's top padding");
			}

			// Folder tabs sized to their labels, left-aligned after the inset.
			await Assert.That(BoundsIn(app, app.Tabs.Tabs[0]).Left).IsEqualTo(app.LocalBounds.Left + TabView.TabInset * GuiWidget.DeviceScale).Within(1);
			await Assert.That(BoundsIn(app, app.Tabs.Tabs[1]).Right).IsLessThan(app.LocalBounds.Right - TabView.TabInset * GuiWidget.DeviceScale);

			// The first visit opens on AGG drawing, as the site always has.
			await Assert.That(app.SelectedTab).IsEqualTo(AggSharpDemoApp.AggDemosTab);
			await Assert.That(app.AggDemosPage.SelectedDemo.Name).IsEqualTo(DemoRegistry.DefaultDemoName);
			await Assert.That(app.GuiDemoShell).IsNull().Because("only one mode is shown at a time");
		}

		[Test]
		public async Task SwitchingTabsSwapsTheBarAndTheContentAndTheBarStaysOnTheRight()
		{
			AggSharpDemoApp app = LaidOutApp(1000, 700);

			AggDemosPage aggPage = app.AggDemosPage;
			await AssertBarOnTheRight(app, aggPage.Sidebar, aggPage.Content);

			app.Tabs.Tabs[1].OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			app.PerformLayout();
			await Assert.That(app.SelectedTab).IsEqualTo(AggSharpDemoApp.AggSharpDemosTab);
			await Assert.That(app.AggDemosPage).IsNull().Because("the AGG page should be gone");
			await Assert.That(aggPage.HasBeenClosed).IsTrue();
			GuiDemoShell shell = app.GuiDemoShell;
			await Assert.That(shell).IsNotNull();
			await AssertBarOnTheRight(app, shell.Sidebar, shell.Canvas);

			app.SelectTab(AggSharpDemoApp.AggDemosTab);
			app.PerformLayout();
			await Assert.That(app.GuiDemoShell).IsNull();
			await Assert.That(shell.HasBeenClosed).IsTrue();
			await AssertBarOnTheRight(app, app.AggDemosPage.Sidebar, app.AggDemosPage.Content);
		}

		[Test]
		public async Task BothBarsAreTheSameComponent()
		{
			AggSharpDemoApp app = LaidOutApp(1000, 700);
			AggDemoSidebar aggBar = app.AggDemosPage.Sidebar;
			RectangleDouble aggBounds = BoundsIn(app, aggBar);
			double aggRowHeight = aggBar.Descendants<SidebarRow>().First().Height;
			Color aggFill = aggBar.BackgroundColor;
			await Assert.That(aggBar.Search).IsNotNull();

			app.SelectTab(AggSharpDemoApp.AggSharpDemosTab);
			app.PerformLayout();
			DemoSidebar guiBar = app.GuiDemoShell.Sidebar;
			RectangleDouble guiBounds = BoundsIn(app, guiBar);

			await Assert.That(aggBounds.Width).IsEqualTo(guiBounds.Width);
			await Assert.That(aggBounds.Right).IsEqualTo(guiBounds.Right);
			await Assert.That(aggRowHeight).IsEqualTo(guiBar.Descendants<SidebarRow>().First().Height);
			await Assert.That(aggFill).IsEqualTo(guiBar.BackgroundColor);
			await Assert.That(guiBar.Search).IsNotNull();
		}

		[Test]
		public async Task SelectingAnAggDemoInTheBarShowsItOnTheLeft()
		{
			AggSharpDemoApp app = LaidOutApp(1000, 700);
			AggDemosPage page = app.AggDemosPage;

			page.Sidebar.RowOf("gradients").InvokeClick();
			app.PerformLayout();

			AggDemoView view = page.Content.Descendants<AggDemoView>().Single();
			await Assert.That(view.Demo.Name).IsEqualTo("gradients");
			await Assert.That(page.SelectedDemo.Name).IsEqualTo("gradients");
			await Assert.That(BoundsIn(app, view).Right).IsLessThanOrEqualTo(BoundsIn(app, page.Sidebar).Left);
			await Assert.That(page.Sidebar.RowOf("gradients").IsOn).IsTrue();
			await Assert.That(page.Sidebar.RowOf("lion").IsOn).IsFalse();
		}

		[Test]
		public async Task TheAggDemosAreGroupedAndAlphabeticalInTheBarAndTheMenu()
		{
			AggSharpDemoApp app = LaidOutApp(1000, 700);
			AggDemosPage page = app.AggDemosPage;
			var allNames = DemoRegistry.CreateAggDemos().Select(d => d.Name).OrderBy(n => n).ToList();

			// Every demo is in one of the listed groups.
			await Assert.That(DemoRegistry.CreateAggDemos().All(d => DemoRegistry.Groups.Contains(d.Category))).IsTrue();

			// The bar: group headers in Groups order, each followed by its demos alphabetically.
			var barGroups = page.Sidebar.Descendants<SidebarGroupHeader>().Select(h => h.Name.Substring("AGG Sidebar Group ".Length)).ToList();
			await Assert.That(barGroups).IsEquivalentTo(DemoRegistry.Groups, TUnit.Assertions.Enums.CollectionOrdering.Matching);
			var barNames = page.Sidebar.Descendants<SidebarRow>().Select(r => r.Text).ToList();
			await Assert.That(barNames.OrderBy(n => n).ToList()).IsEquivalentTo(allNames, TUnit.Assertions.Enums.CollectionOrdering.Matching);

			// The menu: the same groups, the same order within each.
			var groups = page.TopBar.Menus[0].SubMenuItems();
			await Assert.That(groups.Select(g => g.Text)).IsEquivalentTo(DemoRegistry.Groups, TUnit.Assertions.Enums.CollectionOrdering.Matching);
			var menuNames = groups.SelectMany(g => g.SubMenuItems().Select(i => i.Text)).ToList();
			await Assert.That(menuNames).IsEquivalentTo(barNames, TUnit.Assertions.Enums.CollectionOrdering.Matching);
			foreach (var group in groups)
			{
				var names = group.SubMenuItems().Select(i => i.Text).ToList();
				var sorted = names.OrderBy(n => n.ToLowerInvariant(), StringComparer.Ordinal).ToList();
				await Assert.That(names).IsEquivalentTo(sorted, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because($"'{group.Text}' should be alphabetical");
			}
		}

		[Test]
		public async Task ANarrowWindowHidesTheAggBarAndTheDemosMenuPicksTheDemo()
		{
			AggSharpDemoApp app = LaidOutApp(600, 700);
			AggDemosPage page = app.AggDemosPage;

			await Assert.That(page.IsNarrow).IsTrue();
			await Assert.That(page.Sidebar.Visible).IsFalse().Because("a narrow window has no room for a permanent bar");
			await Assert.That(page.TopBar.SidebarDrawerButton.Visible).IsTrue();
			await Assert.That(BoundsIn(app, page.Content).Width).IsEqualTo(app.Width);

			var circles = page.TopBar.Menus[0].SubMenuItems()
				.Single(g => g.AutomationName == "demos.Shapes")
				.SubMenuItems()
				.Single(i => i.AutomationName == "agg.circles");
			circles.Action();

			await Assert.That(page.SelectedDemo.Name).IsEqualTo("circles");
			await Assert.That(page.Content.Descendants<AggDemoView>().Single().Demo.Name).IsEqualTo("circles");

			// The drawer still opens the bar, and picking from it closes it again so the demo shows.
			page.TopBar.SidebarDrawerButton.InvokeClick();
			await Assert.That(page.Sidebar.Visible).IsTrue();
			page.Sidebar.RowOf("lion").InvokeClick();
			await Assert.That(page.Sidebar.Visible).IsFalse();
			await Assert.That(page.SelectedDemo.Name).IsEqualTo("lion");
		}

		/// <summary>A phone-width page (360 design units) wraps the demo's description beneath its name and keeps the
		/// software toggle on screen; a long description once ran the header off the right edge, toggle and all.</summary>
		[Test]
		[Arguments(360.0)]
		[Arguments(1000.0)]
		public async Task TheAggDemoHeaderFitsTheWindowAndWrapsItsDescription(double width)
		{
			AggSharpDemoApp app = LaidOutApp(width, 700, initialDemo: "image_filters");
			AggDemosPage page = app.AggDemosPage;
			await Assert.That(page.SelectedDemo.Name).IsEqualTo("image_filters");

			GuiWidget name = page.Content.FindDescendant("AGG Demo Name");
			GuiWidget toggle = page.Content.FindDescendant("AGG Demo Software Toggle");
			var description = (WrappedTextWidget)page.Content.FindDescendant("AGG Demo Description");
			RectangleDouble window = app.LocalBounds;
			RectangleDouble toggleBounds = BoundsIn(app, toggle);
			RectangleDouble descriptionBounds = BoundsIn(app, description);
			RectangleDouble descriptionTextBounds = BoundsIn(app, description.TextWidget);

			await Assert.That(toggleBounds.Left).IsGreaterThanOrEqualTo(window.Left).Because("the toggle should be on screen");
			await Assert.That(toggleBounds.Right).IsLessThanOrEqualTo(window.Right).Because("the toggle should be on screen");
			await Assert.That(toggleBounds.Bottom).IsGreaterThanOrEqualTo(BoundsIn(app, name).Bottom - 1).Because("the toggle should share the name's row");
			await Assert.That(descriptionBounds.Right).IsLessThanOrEqualTo(window.Right).Because("the description should fit the window");
			await Assert.That(descriptionTextBounds.Right).IsLessThanOrEqualTo(window.Right).Because("the description's text should wrap inside the window");
			await Assert.That(descriptionBounds.Top).IsLessThanOrEqualTo(Math.Min(toggleBounds.Bottom, BoundsIn(app, name).Bottom)).Because("the description should sit beneath the title row");
			if (width < 400)
			{
				await Assert.That(description.TextWidget.Text).Contains("\n").Because("a phone-width page should wrap image_filters' description");
			}
		}

		[Test]
		public async Task ResizingAcrossTheBreakpointDocksAndHidesTheAggBar()
		{
			AggSharpDemoApp app = LaidOutApp(600, 700);
			GuiWidget window = app.Parent;
			AggDemosPage page = app.AggDemosPage;

			async Task AssertNarrow(string when)
			{
				await Assert.That(window.Width).IsEqualTo(600).Because($"{when}: the window should really be narrow");
				await Assert.That(page.Sidebar.Visible).IsFalse().Because($"{when}: the bar should be hidden");
				await Assert.That(page.TopBar.SidebarDrawerButton.Visible).IsTrue().Because($"{when}: the drawer button should show");
			}

			async Task AssertWide(string when)
			{
				await Assert.That(page.Sidebar.Visible).IsTrue().Because($"{when}: the bar should be back");
				await Assert.That(page.TopBar.SidebarDrawerButton.Visible).IsFalse().Because($"{when}: the drawer button should hide");
				await AssertBarOnTheRight(app, page.Sidebar, page.Content);
			}

			await AssertNarrow("narrow at start");

			window.Width = 1000;
			window.PerformLayout();
			await AssertWide("widened");

			window.Width = 600;
			window.PerformLayout();
			await AssertNarrow("narrowed again");

			// And the reverse: starting wide.
			app = LaidOutApp(1000, 700);
			window = app.Parent;

			// GuiWidget(width, height) makes its size the minimum; a window can shrink below where it opened.
			window.MinimumSize = MatterHackers.VectorMath.Vector2.Zero;
			page = app.AggDemosPage;
			await AssertWide("wide at start");

			window.Width = 600;
			window.PerformLayout();
			await AssertNarrow("narrowed");

			window.Width = 1000;
			window.PerformLayout();
			await AssertWide("widened again");
		}

		[Test]
		public async Task TheSelectedTabAndAggDemoSurviveARelaunch()
		{
			var store = new MemoryStore();
			AggSharpDemoApp first = LaidOutApp(1000, 700, store);
			first.AggDemosPage.Sidebar.RowOf("gradients").InvokeClick();
			first.SelectTab(AggSharpDemoApp.AggSharpDemosTab);

			// The GUI demo page saves its own state into the same store; that must not drop the app's choices.
			first.GuiDemoShell.Persistence.SaveNow();
			first.Close();

			DemoState saved = DemoState.Parse(store.Json);
			await Assert.That(saved.AppTab).IsEqualTo(AggSharpDemoApp.AggSharpDemosTab);
			await Assert.That(saved.AggDemo).IsEqualTo("gradients");

			AggSharpDemoApp second = LaidOutApp(1000, 700, store);
			await Assert.That(second.SelectedTab).IsEqualTo(AggSharpDemoApp.AggSharpDemosTab);
			await Assert.That(second.GuiDemoShell).IsNotNull();

			second.SelectTab(AggSharpDemoApp.AggDemosTab);
			await Assert.That(second.AggDemosPage.SelectedDemo.Name).IsEqualTo("gradients");
			second.Close();

			// A head asking for a demo by name still gets it, whatever was saved.
			AggSharpDemoApp third = LaidOutApp(1000, 700, store, "circles");
			await Assert.That(third.SelectedTab).IsEqualTo(AggSharpDemoApp.AggDemosTab);
			await Assert.That(third.AggDemosPage.SelectedDemo.Name).IsEqualTo("circles");
			third.Close();
		}

		[Test]
		public async Task SidebarScrollsSoTheLastDemoIsReachableInAShortWindow()
		{
			// Short enough that the AGG demo list overflows it, as it does in a normal window now.
			AggSharpDemoApp app = LaidOutApp(1000, 300);

			var scroll = (ScrollableWidget)app.AggDemosPage.Sidebar.FindDescendant("AGG Sidebar Scroll");
			await Assert.That(scroll).IsNotNull().Because("the sidebar should scroll when it overflows");

			var lastEntry = scroll.Descendants<SidebarRow>().Last();
			var before = lastEntry.TransformToParentSpace(scroll, lastEntry.LocalBounds);
			await Assert.That(before.Bottom).IsLessThan(scroll.LocalBounds.Bottom)
				.Because("the list should overflow a 300 px window, or this test proves nothing");

			// Scroll as far down as the list goes; the scroll area clamps it to the end.
			scroll.ScrollPosition = new MatterHackers.VectorMath.Vector2(0, 100000);
			var after = lastEntry.TransformToParentSpace(scroll, lastEntry.LocalBounds);
			await Assert.That(after.Bottom).IsGreaterThanOrEqualTo(scroll.LocalBounds.Bottom);
			await Assert.That(after.Top).IsLessThanOrEqualTo(scroll.LocalBounds.Top);
		}

		[Test]
		public async Task ChromeAndSidebarSearchFollowTheDemoTheme()
		{
			AggSharpDemoApp app = LaidOutApp(1000, 700);
			AggDemosPage page = app.AggDemosPage;

			var sidebar = page.Sidebar;
			var search = page.Sidebar.Search;
			var selectedEntry = page.Sidebar.RowOf("lion");
			var otherEntry = page.Sidebar.RowOf("gradients");

			foreach (ThemePreference preference in new[] { ThemePreference.Light, ThemePreference.Dark })
			{
				app.DemoTheme.SetPreference(preference);
				DemoPalette palette = preference == ThemePreference.Dark ? DemoPalette.Dark : DemoPalette.Light;

				// The selected tab joins the page's menu bar; the band behind the tabs is recessed a step darker.
				await Assert.That(app.Tabs.PageColor).IsEqualTo(palette.TopBarBackground).Because($"{preference} selected tab");
				await Assert.That((int)app.Tabs.BarColor.green).IsLessThan(palette.TopBarBackground.green).Because($"{preference} tab strip");
				await Assert.That(sidebar.BackgroundColor).IsEqualTo(palette.PanelFill).Because($"{preference} sidebar");
				await Assert.That(page.Content.BackgroundColor).IsEqualTo(palette.BackgroundColor).Because($"{preference} page area");
				await Assert.That(search.BackgroundColor).IsEqualTo(palette.WidgetBackground).Because($"{preference} search fill");
				await Assert.That(search.BorderColor).IsEqualTo(palette.WidgetStroke).Because($"{preference} search border");
				await Assert.That(search.ActualTextEditWidget.TextColor).IsEqualTo(palette.TextColor).Because($"{preference} search text");
				await Assert.That(search.NoContentFieldDescription.TextColor).IsEqualTo(palette.TextDim).Because($"{preference} search hint");

				// the open demo's row is lit with the accent; the others sit on the panel in the palette's text
				await Assert.That(selectedEntry.BackgroundColor).IsEqualTo(DemoTheme.ColorOf(app.DemoTheme.Accent)).Because($"{preference} selected row fill");
				await Assert.That(selectedEntry.TextColor).IsEqualTo(Color.White).Because($"{preference} selected row text");
				await Assert.That(otherEntry.BackgroundColor).IsEqualTo(Color.Transparent).Because($"{preference} row fill");
				await Assert.That(otherEntry.TextColor).IsEqualTo(palette.TextColor).Because($"{preference} row text");
				await Assert.That(otherEntry.HoverColor).IsEqualTo(app.DemoTheme.Theme.MinimalShade).Because($"{preference} row hover");
			}

			await Assert.That(DemoPalette.Dark.PanelFill).IsNotEqualTo(DemoPalette.Light.PanelFill)
				.Because("the palettes must differ, or this test proves nothing");

			// selecting another demo moves the accent to its row
			otherEntry.InvokeClick();
			await Assert.That(otherEntry.BackgroundColor).IsEqualTo(DemoTheme.ColorOf(app.DemoTheme.Accent));
			await Assert.That(selectedEntry.BackgroundColor).IsEqualTo(Color.Transparent);
		}

		[Test]
		public async Task SwitchingTabsDoesNotLeaveThemeSubscribersBehind()
		{
			AggSharpDemoApp app = LaidOutApp(1000, 700, initialDemo: AggSharpDemoApp.GuiDemoName);

			// one full round first, so the count is taken with a page up and every lazily built part in place
			ExerciseWindows(app);
			app.SelectTab(AggSharpDemoApp.AggDemosTab);
			app.SelectTab(AggSharpDemoApp.AggSharpDemosTab);
			int subscribers = ThemeChangedSubscriberCount(app.DemoTheme);

			for (int i = 0; i < 3; i++)
			{
				ExerciseWindows(app);
				app.SelectTab(AggSharpDemoApp.AggDemosTab);
				app.SelectTab(AggSharpDemoApp.AggSharpDemosTab);
			}

			await Assert.That(ThemeChangedSubscriberCount(app.DemoTheme)).IsEqualTo(subscribers);
		}

		[Test]
		[NotInParallel] // sets the process-wide GuiWidget.DeviceScale, which every layout reads
		public async Task FollowingANewDisplayScaleRebuildsOnTheSamePageWithTheSameWindows()
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				var store = new MemoryStore();
				var window = new SystemWindow(1800, 1000);
				var app = new AggSharpDemoApp(AggSharpDemoApp.GuiDemoName, store, followDisplayScale: true);
				window.AddChild(app);
				window.PerformLayout();

				// The app finds the window it follows when it loads, which is its first draw.
				window.OnDraw(new ImageBuffer(1800, 1000).NewGraphics2D());

				// Start from 1x whatever display the test process is on, so the step to 2x is a real rebuild.
				window.SetDisplayScale(1);
				UiThread.InvokePendingActions();
				await Assert.That(GuiWidget.DeviceScale).IsEqualTo(1.0);

				GuiDemoShell before = app.Descendants<GuiDemoShell>().Single();
				DemoSpec opened = GuiDemoSpecs.All.First(s => !s.OpenByDefault);
				DemoSpec closed = GuiDemoSpecs.All.First(s => s.OpenByDefault);
				before.Windows.SetOpen(opened, true);
				before.Windows.SetOpen(closed, false);

				window.SetDisplayScale(2);
				UiThread.InvokePendingActions();

				await Assert.That(GuiWidget.DeviceScale).IsEqualTo(2.0);

				GuiDemoShell after = app.Descendants<GuiDemoShell>().Single();
				await Assert.That(after).IsNotSameReferenceAs(before).Because("the page should be rebuilt at the new scale");
				await Assert.That(app.SelectedTab).IsEqualTo(AggSharpDemoApp.AggSharpDemosTab)
					.Because("the agg-sharp Demos tab should still be the selected one");
				await Assert.That(app.Tabs.SelectedIndex).IsEqualTo(1);

				await Assert.That(after.Windows.IsOpen(opened)).IsTrue().Because($"'{opened.Title}' was open before the rebuild");
				await Assert.That(after.Windows.IsOpen(closed)).IsFalse().Because($"'{closed.Title}' was closed before the rebuild");

				window.Close();
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
				UiThread.ResetForTests();
			}
		}

		private static AggSharpDemoApp LaidOutApp(double width, double height, IDemoStateStore store = null, string initialDemo = null)
		{
			var page = new GuiWidget(width, height);
			var app = new AggSharpDemoApp(initialDemo, store);
			page.AddChild(app);
			page.PerformLayout();
			return app;
		}

		private static RectangleDouble BoundsIn(GuiWidget ancestor, GuiWidget widget) => widget.TransformToParentSpace(ancestor, widget.LocalBounds);

		/// <summary>The selection bar is flush with the app's right edge, under the tab strip, and the content fills
		/// the width to its left.</summary>
		private static async Task AssertBarOnTheRight(AggSharpDemoApp app, GuiWidget bar, GuiWidget content)
		{
			RectangleDouble barBounds = BoundsIn(app, bar);
			RectangleDouble contentBounds = BoundsIn(app, content);
			await Assert.That(bar.Visible).IsTrue();
			await Assert.That(barBounds.Right).IsEqualTo(app.LocalBounds.Right).Because($"{bar.Name} should be docked on the right");
			await Assert.That(barBounds.Top).IsLessThan(app.LocalBounds.Top).Because($"{bar.Name} should sit below the tab strip");
			await Assert.That(contentBounds.Right).IsLessThanOrEqualTo(barBounds.Left).Because($"{content.Name} should be left of the bar");
			await Assert.That(contentBounds.Left).IsEqualTo(app.LocalBounds.Left).Because($"{content.Name} should fill the area to the bar's left");
		}

		/// <summary>Builds every demo window and leaves all but one of them kept by the host but not shown.</summary>
		private static void ExerciseWindows(AggSharpDemoApp app)
		{
			var shell = app.Descendants<GuiDemoShell>().Single();
			foreach (DemoSpec spec in GuiDemoSpecs.All)
			{
				shell.Windows.SetOpen(spec, true);
				shell.Windows.SetOpen(spec, false);
			}

			shell.Windows.SetOpen(GuiDemoSpecs.All.First(), true);
		}

		/// <summary>ThemeChanged is a field-like event, so its handlers are only readable through the compiler's
		/// backing field of the same name.</summary>
		private static int ThemeChangedSubscriberCount(DemoTheme demoTheme)
		{
			var field = typeof(DemoTheme).GetField(nameof(DemoTheme.ThemeChanged), BindingFlags.Instance | BindingFlags.NonPublic);
			return (field.GetValue(demoTheme) as Delegate)?.GetInvocationList().Length ?? 0;
		}

		private class MemoryStore : IDemoStateStore
		{
			public string Json { get; private set; }

			public string Load() => this.Json;

			public void Save(string json) => this.Json = json;

			public void Clear() => this.Json = null;
		}
	}
}
