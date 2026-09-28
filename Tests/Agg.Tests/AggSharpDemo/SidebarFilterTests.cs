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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo sidebar's search and collapse rules, apart from any widget (agg-gui's sidebar.rs).
	public class SidebarFilterTests
	{
		private static DemoSpec Spec(string title) => GuiDemoSpecs.All.First(s => s.Title == title);

		[Test]
		public async Task MatchesIsACaseInsensitiveSubstring()
		{
			await Assert.That(SidebarFilter.Matches("Widget Gallery", "")).IsTrue();
			await Assert.That(SidebarFilter.Matches("Widget Gallery", null)).IsTrue();
			await Assert.That(SidebarFilter.Matches("Widget Gallery", "gALLer")).IsTrue();
			await Assert.That(SidebarFilter.Matches("Widget Gallery", "gallery x")).IsFalse();
		}

		[Test]
		public async Task EntriesAreTheGroupsSpecsSortedCaseInsensitively()
		{
			var tools = SidebarFilter.EntriesOf("Tools");
			await Assert.That(tools.Select(s => s.Title)).IsEquivalentTo(new[] { "Inspector", "System" }).Because("app_builder.rs lists its Inspector tool entry with the Tools windows");

			var widgets = SidebarFilter.EntriesOf("Widgets").Select(s => s.Title).ToList();
			await Assert.That(widgets.Count).IsEqualTo(GuiDemoSpecs.All.Count(s => s.Group == "Widgets"));
			await Assert.That(widgets.First()).IsEqualTo("Code Editor");

			// "RichTextEdit" sorts before "Sliders" but after "Popups" regardless of case.
			await Assert.That(widgets.IndexOf("RichTextEdit")).IsLessThan(widgets.IndexOf("Sliders"));
			await Assert.That(widgets.IndexOf("Popups")).IsLessThan(widgets.IndexOf("RichTextEdit"));
		}

		[Test]
		public async Task SearchHidesUnmatchedRowsAndEmptyGroups()
		{
			var filter = new SidebarFilter();
			int changes = 0;
			filter.Changed += (s, e) => changes++;

			foreach (string group in GuiDemoSpecs.Groups)
			{
				await Assert.That(filter.IsGroupVisible(group)).IsTrue();
			}

			filter.SetQuery("LION");
			await Assert.That(changes).IsEqualTo(1);
			await Assert.That(filter.IsEntryVisible(Spec("Lion"))).IsTrue();
			await Assert.That(filter.IsEntryVisible(Spec("Painting"))).IsFalse();
			await Assert.That(GuiDemoSpecs.Groups.Where(filter.IsGroupVisible)).IsEquivalentTo(new[] { "Graphics" });

			// Setting the same text again is not a change.
			filter.SetQuery("LION");
			await Assert.That(changes).IsEqualTo(1);

			filter.SetQuery("");
			await Assert.That(GuiDemoSpecs.Groups.All(filter.IsGroupVisible)).IsTrue();
		}

		[Test]
		public async Task CollapsingHidesRowsButKeepsTheHeader()
		{
			var filter = new SidebarFilter();
			filter.SetCollapsed("Graphics", true);

			await Assert.That(filter.IsCollapsed("Graphics")).IsTrue();
			await Assert.That(filter.IsGroupVisible("Graphics")).IsTrue();
			await Assert.That(filter.IsEntryVisible(Spec("Lion"))).IsFalse();
			await Assert.That(filter.IsEntryVisible(Spec("Sliders"))).IsTrue();

			// Collapsed stays collapsed while searching, as agg-gui's CollapsingHeader keeps its own state.
			filter.SetQuery("lion");
			await Assert.That(filter.IsGroupVisible("Graphics")).IsTrue();
			await Assert.That(filter.IsEntryVisible(Spec("Lion"))).IsFalse();

			filter.SetCollapsed("Graphics", false);
			await Assert.That(filter.IsEntryVisible(Spec("Lion"))).IsTrue();
		}
	}
}
