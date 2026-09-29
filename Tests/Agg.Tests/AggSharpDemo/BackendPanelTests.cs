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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// agg-gui's backend panel (demo-ui/src/backend_panel.rs) on the GUI demo page.
	// GuiDemoShell installs its theme as ThemeConfig.Current, which other tests set and read, and opens the
	// About window's MarkdownWidget.
	[NotInParallel(new[] { SharedStateKeys.ThemeConfigCurrent, SharedStateKeys.MarkdownWidget })]
	public class BackendPanelTests
	{
		private static DemoSpec Spec(string title) => GuiDemoSpecs.All.First(s => s.Title == title);

		private static GuiDemoShell LaidOutShell(IDemoStateStore store = null)
		{
			var page = new GuiWidget(1000, 700);
			var shell = new GuiDemoShell(new DemoTheme(), store);
			page.AddChild(shell);
			page.PerformLayout();
			return shell;
		}

		private static MenuItemModel BackendMenuItem(GuiDemoShell shell)
			=> shell.TopBar.Menus[1].SubMenuItems().Single(item => item.AutomationName == "view.backend");

		[Test]
		public async Task ViewBackendPanelShowsThePanelLeftOfTheCanvasAndIsRemembered()
		{
			var store = new MemoryStore();
			GuiDemoShell shell = LaidOutShell(store);
			await Assert.That(shell.BackendPanel.Visible).IsFalse();
			await Assert.That(shell.Canvas.Position.X).IsEqualTo(0);

			BackendMenuItem(shell).Action();
			shell.Parent.PerformLayout();
			await Assert.That(shell.BackendPanel.Visible).IsTrue();
			await Assert.That(shell.BackendPanel.Position.X).IsEqualTo(0);
			await Assert.That(shell.BackendPanel.Width).IsEqualTo(BackendPanel.PanelWidth);
			await Assert.That(shell.Canvas.Position.X).IsEqualTo(BackendPanel.PanelWidth);
			await Assert.That(shell.Canvas.Width).IsEqualTo(1000 - GuiDemoShell.SidebarWidth - BackendPanel.PanelWidth);

			// Saved open, it opens again on the next run; unchecked, it hides.
			shell.Persistence.SaveNow();
			GuiDemoShell next = LaidOutShell(store);
			await Assert.That(next.BackendPanel.Visible).IsTrue();

			BackendMenuItem(next).Action();
			await Assert.That(next.BackendPanel.Visible).IsFalse();
		}

		[Test]
		public async Task ContinuousRunModeAsksForTheNextFrameAndReactiveDoesNot()
		{
			GuiDemoShell shell = LaidOutShell();
			int invalidations = 0;
			shell.Invalidated += (s, e) => invalidations++;
			var image = new ImageBuffer(1000, 700);

			await Assert.That(shell.BackendPanel.RunMode).IsEqualTo(DemoRunMode.Reactive);
			shell.OnDraw(image.NewGraphics2D());
			await Assert.That(invalidations).IsEqualTo(0);
			await Assert.That(shell.BackendPanel.History.Count).IsEqualTo(1);

			// The Continuous segment switches the policy: every frame drawn asks for the next.
			shell.BackendPanel.RunModeControl.SelectedIndex = (int)DemoRunMode.Continuous;
			await Assert.That(shell.RedrawsContinuously).IsTrue();
			await Assert.That(shell.BackendPanel.FindDescendant("Backend FPS").Visible).IsTrue();
			invalidations = 0;
			shell.OnDraw(image.NewGraphics2D());
			await Assert.That(invalidations).IsGreaterThan(0);

			shell.BackendPanel.RunModeControl.SelectedIndex = (int)DemoRunMode.Reactive;
			invalidations = 0;
			shell.OnDraw(image.NewGraphics2D());
			await Assert.That(invalidations).IsEqualTo(0);
		}

		[Test]
		public async Task ResetAllStateForgetsTheSaveAndRestoresTheDefaults()
		{
			var store = new MemoryStore();
			GuiDemoShell shell = LaidOutShell(store);
			var defaultOrder = shell.Windows.ZOrder.ToList();
			DemoSpec closedDefault = defaultOrder.First();

			shell.Windows.SetOpen(Spec("Sliders"), true);
			shell.Windows.SetOpen(closedDefault, false);
			shell.DemoTheme.SetPreference(ThemePreference.Light);
			shell.DemoTheme.SetAccent(AccentColor.Green);
			shell.TopBar.SetSnapEnabled(false);
			shell.TopBar.SetBackendPanelOpen(true);
			shell.BackendPanel.SetRunMode(DemoRunMode.Continuous);
			shell.BackendPanel.SetSsaaFactor(4);
			shell.BackendPanel.SetInspectorEnabled(true);
			shell.Persistence.SaveNow();
			await Assert.That(store.Json).IsNotNull();

			shell.BackendPanel.FindDescendant("Backend Reset").InvokeClick();

			await Assert.That(store.Json).IsNull();
			await Assert.That(shell.Persistence.SavePending).IsFalse();
			await Assert.That(shell.Windows.ZOrder.Select(s => s.Title).ToList()).IsEquivalentTo(shell.Windows.DefaultStacking().Select(s => s.Title).ToList(), CollectionOrdering.Matching);
			await Assert.That(shell.Windows.IsOpen(Spec("Sliders"))).IsFalse();
			await Assert.That(shell.DemoTheme.Preference).IsEqualTo(ThemePreference.System);
			await Assert.That(shell.DemoTheme.Accent).IsEqualTo(AccentColor.Blue);
			await Assert.That(shell.TopBar.SnapEnabled).IsTrue();
			await Assert.That(shell.BackendPanel.RunMode).IsEqualTo(DemoRunMode.Reactive);
			await Assert.That(shell.BackendPanel.SsaaFactor).IsEqualTo(BackendPanel.DefaultSsaaFactor);
			await Assert.That(shell.BackendPanel.InspectorEnabled).IsFalse();
			await Assert.That(shell.BackendPanel.OpenWindowTitles).IsEquivalentTo(shell.Windows.DefaultStacking().Reverse().Select(s => s.Title).ToList(), CollectionOrdering.Matching);
		}

		[Test]
		public async Task FrameHistoryKeepsARollingWindowOfTheLatestFrames()
		{
			var history = new FrameHistory();
			await Assert.That(history.MeanMs).IsEqualTo(0);
			await Assert.That(history.Fps).IsEqualTo(0);

			for (int i = 1; i <= FrameHistory.Capacity + 10; i++)
			{
				history.Push(i);
			}

			// The oldest ten have rolled off; the rest are oldest first.
			await Assert.That(history.Count).IsEqualTo(FrameHistory.Capacity);
			await Assert.That(history.Samples.First()).IsEqualTo(11);
			await Assert.That(history.Samples.Last()).IsEqualTo(FrameHistory.Capacity + 10);
			await Assert.That(history.MeanMs).IsEqualTo((11 + FrameHistory.Capacity + 10) / 2.0);
			await Assert.That(history.Fps).IsEqualTo(1000 / history.MeanMs);
		}

		[Test]
		public async Task PickingAnSsaaFactorRaisesTheEvent()
		{
			GuiDemoShell shell = LaidOutShell();
			var raised = new List<int>();
			shell.BackendPanel.SsaaFactorChanged += (s, e) => raised.Add(shell.BackendPanel.SsaaFactor);

			shell.BackendPanel.SsaaControl.SelectedIndex = 2;
			shell.BackendPanel.SetSsaaFactor(3);
			shell.BackendPanel.SetSsaaFactor(5);

			await Assert.That(raised).IsEquivalentTo(new[] { 3 }, CollectionOrdering.Matching);
			await Assert.That(shell.BackendPanel.SsaaFactor).IsEqualTo(3);
		}

		[Test]
		public async Task InspectorPillRaisesTheEventAndLights()
		{
			GuiDemoShell shell = LaidOutShell();
			int raised = 0;
			shell.BackendPanel.InspectorToggled += (s, e) => raised++;

			await Assert.That(shell.BackendPanel.InspectorPill.IsOn).IsFalse();

			shell.BackendPanel.InspectorPill.InvokeClick();

			await Assert.That(raised).IsEqualTo(1);
			await Assert.That(shell.BackendPanel.InspectorEnabled).IsTrue();

			// Lit like a sidebar row: the accent, white lettering
			await Assert.That(shell.BackendPanel.InspectorPill.IsOn).IsTrue();
			await Assert.That(shell.BackendPanel.InspectorPill.BackgroundColor).IsEqualTo(DemoTheme.ColorOf(shell.DemoTheme.Accent));
			await Assert.That(shell.BackendPanel.InspectorPill.TextColor).IsEqualTo(Color.White);

			shell.BackendPanel.InspectorPill.InvokeClick();
			await Assert.That(shell.BackendPanel.InspectorEnabled).IsFalse();
			await Assert.That(shell.BackendPanel.InspectorPill.BackgroundColor).IsEqualTo(shell.DemoTheme.Palette.WidgetBackground);
		}

		[Test]
		public async Task SystemPillOpensTheSystemWindowAndFollowsIt()
		{
			GuiDemoShell shell = LaidOutShell();
			DemoSpec system = Spec("System");
			TogglePill pill = shell.BackendPanel.SystemPill;
			await Assert.That(pill.Name).IsEqualTo("Backend System");
			await Assert.That(pill.IconGlyph).IsEqualTo("\uF013");
			await Assert.That(shell.BackendPanel.InspectorPill.IconGlyph).IsEqualTo("\uF002");
			await Assert.That(pill.IsOn).IsFalse();

			pill.InvokeClick();
			await Assert.That(shell.Windows.IsOpen(system)).IsTrue();
			await Assert.That(pill.IsOn).IsTrue();

			// Closed some other way (its sidebar row), the pill goes dark with it
			shell.Sidebar.RowOf(system).InvokeClick();
			await Assert.That(shell.Windows.IsOpen(system)).IsFalse();
			await Assert.That(pill.IsOn).IsFalse();
		}

		[Test]
		public async Task RunningInsideNamesThePlatform()
		{
			GuiDemoShell shell = LaidOutShell();
			var line = (LiveText)shell.BackendPanel.FindDescendant("Backend Running Inside");
			await Assert.That(line.CurrentText).IsEqualTo("agg-sharp running inside none.");
		}

		[Test]
		public async Task TheAdapterIsTakenFromTheRenderStatusReport()
		{
			await Assert.That(BackendPanel.ParseRenderStatus("Metal Apple M5, presented 12")).IsEqualTo("Metal Apple M5");
			await Assert.That(BackendPanel.ParseRenderStatus("webgpu not initialized")).IsNull();
			await Assert.That(BackendPanel.ParseRenderStatus(null)).IsNull();

			// Off screen there is no platform window: the API alone.
			GuiDemoShell shell = LaidOutShell();
			await Assert.That(shell.BackendPanel.RendererDescription).IsEqualTo("WebGPU");
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
