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
		public async Task SidebarSectionHeadersSitInsideThePaddingAndMargin()
		{
			var page = new GuiWidget(1000, 700);
			var app = new AggSharpDemoApp(AggSharpDemoApp.GuiDemoName);
			page.AddChild(app);
			page.PerformLayout();

			var sidebarList = ((ScrollableWidget)app.Children.First()).ScrollArea.Children.First();
			var headers = sidebarList.Children.OfType<TextWidget>().ToList();
			await Assert.That(headers.Count).IsEqualTo(2);
			foreach (TextWidget header in headers)
			{
				// The sidebar list's 8 px padding plus the header's 2 px left margin, from the list's drawn left edge
				// (a padded flow's left edge sits one padding left of its origin).
				await Assert.That(header.Position.X + header.LocalBounds.Left - sidebarList.LocalBounds.Left).IsEqualTo(10)
					.Because($"'{header.Text}' should sit 10 px in");
			}
		}

		[Test]
		public async Task SidebarScrollsSoTheLastDemoIsReachableInAShortWindow()
		{
			// Short enough that the AGG demo list overflows it, as it does in a normal window now.
			var page = new GuiWidget(1000, 300);
			var app = new AggSharpDemoApp(AggSharpDemoApp.GuiDemoName);
			page.AddChild(app);
			page.PerformLayout();

			var scroll = app.Children.First() as ScrollableWidget;
			await Assert.That(scroll).IsNotNull().Because("the sidebar should scroll when it overflows");

			var lastEntry = scroll.Descendants<ThemedTextButton>().Last();
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
			var page = new GuiWidget(1000, 700);
			var app = new AggSharpDemoApp(AggSharpDemoApp.GuiDemoName);
			page.AddChild(app);
			page.PerformLayout();

			var sidebar = app.Children.First();
			var pageArea = app.Children.Last();
			var search = (ThemedTextEditWidget)app.FindDescendant("Sidebar Search");
			var selectedEntry = (ThemedTextButton)app.FindDescendant(AggSharpDemoApp.EntryName(AggSharpDemoApp.GuiDemoName));
			var otherEntry = (ThemedTextButton)app.FindDescendant(AggSharpDemoApp.EntryName("lion"));

			foreach (ThemePreference preference in new[] { ThemePreference.Light, ThemePreference.Dark })
			{
				app.DemoTheme.SetPreference(preference);
				DemoPalette palette = preference == ThemePreference.Dark ? DemoPalette.Dark : DemoPalette.Light;

				await Assert.That(sidebar.BackgroundColor).IsEqualTo(palette.PanelFill).Because($"{preference} sidebar");
				await Assert.That(pageArea.BackgroundColor).IsEqualTo(palette.BackgroundColor).Because($"{preference} page area");
				await Assert.That(search.BackgroundColor).IsEqualTo(palette.WidgetBackground).Because($"{preference} search fill");
				await Assert.That(search.BorderColor).IsEqualTo(palette.WidgetStroke).Because($"{preference} search border");
				await Assert.That(search.ActualTextEditWidget.TextColor).IsEqualTo(palette.TextColor).Because($"{preference} search text");
				await Assert.That(search.NoContentFieldDescription.TextColor).IsEqualTo(palette.TextDim).Because($"{preference} search hint");

				// the open page's row is lit with the accent; the others sit on the panel in the palette's text
				await Assert.That(selectedEntry.BackgroundColor).IsEqualTo(DemoTheme.ColorOf(app.DemoTheme.Accent)).Because($"{preference} selected row fill");
				await Assert.That(selectedEntry.TextColor).IsEqualTo(Color.White).Because($"{preference} selected row text");
				await Assert.That(otherEntry.BackgroundColor).IsEqualTo(Color.Transparent).Because($"{preference} row fill");
				await Assert.That(otherEntry.TextColor).IsEqualTo(palette.TextColor).Because($"{preference} row text");
				await Assert.That(otherEntry.HoverColor).IsEqualTo(app.DemoTheme.Theme.MinimalShade).Because($"{preference} row hover");
			}

			await Assert.That(DemoPalette.Dark.PanelFill).IsNotEqualTo(DemoPalette.Light.PanelFill)
				.Because("the palettes must differ, or this test proves nothing");

			// selecting another page moves the accent to its row
			otherEntry.InvokeClick();
			await Assert.That(otherEntry.BackgroundColor).IsEqualTo(DemoTheme.ColorOf(app.DemoTheme.Accent));
			await Assert.That(selectedEntry.BackgroundColor).IsEqualTo(Color.Transparent);
		}

		[Test]
		public async Task RebuildingTheGuiDemoPageDoesNotLeaveThemeSubscribersBehind()
		{
			var page = new GuiWidget(1000, 700);
			var app = new AggSharpDemoApp(AggSharpDemoApp.GuiDemoName);
			page.AddChild(app);
			page.PerformLayout();
			var guiDemoEntry = app.FindDescendant(AggSharpDemoApp.EntryName(AggSharpDemoApp.GuiDemoName));
			var lionEntry = app.FindDescendant(AggSharpDemoApp.EntryName("lion"));

			// one full round first, so the count is taken with a page up and every lazily built part in place
			ExerciseWindows(app);
			lionEntry.InvokeClick();
			guiDemoEntry.InvokeClick();
			int subscribers = ThemeChangedSubscriberCount(app.DemoTheme);

			for (int i = 0; i < 3; i++)
			{
				ExerciseWindows(app);
				lionEntry.InvokeClick();
				guiDemoEntry.InvokeClick();
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

				var guiDemoEntry = (ThemedTextButton)app.FindDescendant(AggSharpDemoApp.EntryName(AggSharpDemoApp.GuiDemoName));
				await Assert.That(guiDemoEntry.BackgroundColor).IsEqualTo(DemoTheme.ColorOf(app.DemoTheme.Accent))
					.Because("the GUI demo should still be the selected page");

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
			private string json;

			public string Load() => this.json;

			public void Save(string json) => this.json = json;

			public void Clear() => this.json = null;
		}
	}
}
