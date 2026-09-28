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
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Tools;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// app_builder.rs's Inspector window on the GUI demo page.
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class InspectorWindowTests
	{
		private static GuiDemoShell LaidOutShell(IDemoStateStore store = null)
		{
			var page = new GuiWidget(1000, 700);
			var shell = new GuiDemoShell(new DemoTheme(), store);
			page.AddChild(shell);
			page.PerformLayout();
			return shell;
		}

		[Test]
		public async Task BackendCheckboxAndSidebarRowShareTheInspectorWindow()
		{
			var store = new MemoryStore();
			GuiDemoShell shell = LaidOutShell(store);
			await Assert.That(shell.Windows.IsOpen(GuiDemoSpecs.Inspector)).IsFalse();

			shell.BackendPanel.InspectorPill.InvokeClick();
			await Assert.That(shell.Windows.IsOpen(GuiDemoSpecs.Inspector)).IsTrue();
			await Assert.That(shell.Windows.GetWindow(GuiDemoSpecs.Inspector).FindDescendant(GuiDemoSpecs.Inspector.ContentName)).IsTypeOf<InspectorWindow>();

			// The sidebar lists it in Tools; its row closes the window and unchecks the backend box.
			await Assert.That(SidebarFilter.EntriesOf("Tools")).Contains(GuiDemoSpecs.Inspector);
			shell.Sidebar.RowOf(GuiDemoSpecs.Inspector).InvokeClick();
			await Assert.That(shell.Windows.IsOpen(GuiDemoSpecs.Inspector)).IsFalse();
			await Assert.That(shell.BackendPanel.InspectorEnabled).IsFalse();

			// Open is remembered, and the next run's backend box agrees.
			shell.Windows.SetOpen(GuiDemoSpecs.Inspector, true);
			shell.Persistence.SaveNow();
			GuiDemoShell next = LaidOutShell(store);
			await Assert.That(next.Windows.IsOpen(GuiDemoSpecs.Inspector)).IsTrue();
			await Assert.That(next.BackendPanel.InspectorPill.IsOn).IsTrue();
		}

		[Test]
		public async Task TheTreeSelectionAndSplitAreRememberedAcrossRuns()
		{
			var store = new MemoryStore();
			GuiDemoShell shell = LaidOutShell(store);
			shell.Persistence.Start();
			shell.Windows.SetOpen(GuiDemoSpecs.Inspector, true);
			var panel = (InspectorPanel)shell.Windows.GetWindow(GuiDemoSpecs.Inspector).FindDescendant(GuiDemoSpecs.Inspector.ContentName);
			panel.InspectedRoot = shell;
			panel.RefreshNow();

			// Selecting a node deep in the tree and moving the split both ask for a save.
			InspectorNode chosen = panel.Model.Nodes[5];
			panel.Model.Select(chosen);
			await Assert.That(shell.Persistence.SavePending).IsTrue();
			panel.PropertiesHeight = 120;
			shell.Persistence.SaveNow();
			bool[] expanded = panel.Model.ExpansionByPosition().ToArray();

			// The next run's inspector opens with the same rows expanded, the same position selected and the split.
			GuiDemoShell next = LaidOutShell(store);
			var nextPanel = (InspectorPanel)next.Windows.GetWindow(GuiDemoSpecs.Inspector).FindDescendant(GuiDemoSpecs.Inspector.ContentName);
			await Assert.That(nextPanel.PropertiesHeight).IsEqualTo(120);
			nextPanel.InspectedRoot = next;
			nextPanel.RefreshNow();
			await Assert.That(nextPanel.Model.SelectedPosition).IsEqualTo(5);
			await Assert.That(nextPanel.Model.Selected.TypeName).IsEqualTo(chosen.TypeName);
			await Assert.That(nextPanel.Model.ExpansionByPosition().Take(expanded.Length).ToArray()).IsEquivalentTo(expanded, CollectionOrdering.Matching);
			shell.Persistence.Stop();
		}

		[Test]
		public async Task HoveringATreeRowHighlightsAndClickingSelectsOnScreen()
		{
			var shell = new GuiDemoShell(new DemoTheme());
			var window = new SystemWindow(1100, 700) { Name = "GuiDemo Test Window" };
			window.AddChild(shell);

			await AutomationRunner.ShowWindowAndExecuteTests(window, async testRunner =>
			{
				// View > Backend Panel, then its Inspector checkbox.
				testRunner.ClickByName("View Menu");
				testRunner.ClickByName("view.backend");
				testRunner.ClickByName("Backend Inspector");
				testRunner.WaitFor(() => shell.Windows.IsOpen(GuiDemoSpecs.Inspector));
				var panel = (InspectorPanel)shell.Windows.GetWindow(GuiDemoSpecs.Inspector).FindDescendant(GuiDemoSpecs.Inspector.ContentName);

				// The timer's first refresh fills the tree with the page, starting at the SystemWindow.
				testRunner.WaitFor(() => panel.Tree.Rows.Count > 1);
				await Assert.That(panel.Model.Root).IsEqualTo(window);

				RectangleDouble row = panel.Tree.RowBounds(1);
				var rowCenter = new Point2D((int)row.Center.X, (int)row.Center.Y);
				testRunner.MoveToByName("Inspector Tree", offset: rowCenter, origin: AutomationRunner.ClickOrigin.LowerLeft);
				// The runner glides the cursor there, so the rows between the Backend Inspector checkbox and this one
				// are each hovered on the way; where the host delivers the moves asynchronously (Windows), a wait for
				// "any row hovered" wakes on one of those passing rows. Wait for the row the cursor settles on.
				testRunner.WaitFor(() => panel.Model.Hovered?.Widget == panel.Tree.Rows[1].Widget);
				await Assert.That(panel.Model.Hovered.Widget).IsEqualTo(panel.Tree.Rows[1].Widget);

				testRunner.ClickByName("Inspector Tree", offset: rowCenter, origin: AutomationRunner.ClickOrigin.LowerLeft);
				testRunner.WaitFor(() => panel.Model.Selected != null);
				await Assert.That(panel.Model.Selected.Widget).IsEqualTo(panel.Tree.Rows[1].Widget);
				testRunner.MarkTestComplete();
			});
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
