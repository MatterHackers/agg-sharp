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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's window list must stay agg-gui's (demo-ui/src/specs.rs), and every window must build.
	// Builds every demo window, About included: new DemoTheme() writes ThemeConfig.Current and the About
	// window's MarkdownWidget writes MarkdownWidget.Theme.
	[NotInParallel(new[] { SharedStateKeys.ThemeConfigCurrent, SharedStateKeys.MarkdownWidget })]
	public class GuiDemoSpecsTests
	{
		// specs.rs DEMOS then TESTS, copied by hand: "group|title|width|height|open".
		private static readonly string[] SpecsRs =
		{
			"Widgets|Widget Gallery|360|290|True",
			"Widgets|Sliders|360|290|False",
			"Widgets|TextEdit|360|290|False",
			"Widgets|RichTextEdit|640|520|False",
			"Widgets|Mobile Keyboard|420|540|False",
			"Widgets|Tooltips|360|290|False",
			"Widgets|Popups|360|290|False",
			"Widgets|Menus|520|320|False",
			"Widgets|Modals|360|290|False",
			"Widgets|Misc Demos|360|290|False",
			"Widgets|Code Editor|360|290|False",
			"Widgets|Code Example|360|290|True",
			"Widgets|Font Book|360|290|False",
			"Layout|Frame|360|290|False",
			"Layout|Panels|360|290|False",
			"Layout|Strip|360|290|False",
			"Layout|Table|720|560|False",
			"Layout|Scrolling|680|540|False",
			"Layout|Window Options|360|290|False",
			"Layout|Text Layout|360|290|False",
			"Layout|Interactive Container|360|290|False",
			"Graphics|Bézier Curve|360|290|False",
			"Graphics|Dancing Strings|360|290|False",
			"Graphics|Painting|360|290|False",
			"Graphics|Rendering Test|360|290|False",
			"Graphics|Lion|520|620|True",
			"Graphics|Screenshot|360|290|False",
			"Graphics|Screen Share|380|420|False",
			"Graphics|3D Animation|300|260|False",
			"Tools|System|520|640|False",
			"Interaction|Drag and Drop|360|290|False",
			"Interaction|Multi Touch|360|290|False",
			"Interaction|Undo Redo|360|290|False",
			"Interaction|Scene|360|290|False",
			"Tests|Clipboard Test|360|290|False",
			"Tests|Cursor Test|296|560|False",
			"Tests|Input Event History|360|290|False",
			"Tests|Input Test|360|290|False",
			"Tests|Flex Layout Test|360|290|False",
			"Tests|Manual Layout Test|360|290|False",
			"Tests|SVG Test|960|620|False",
			"Window Resize Test|↔ auto-sized|360|240|False",
			"Window Resize Test|↔ resizable + scroll|300|290|False",
			"Window Resize Test|↔ resizable + embedded scroll|300|290|False",
			"Window Resize Test|↔ resizable without scroll|300|290|False",
			"Window Resize Test|↔ resizable with TextEdit|300|290|False",
			"Window Resize Test|↔ freely resized|250|150|False",
		};

		[Test]
		public async Task MatchesAggGuiSpecsInOrder()
		{
			List<string> actual = GuiDemoSpecs.All
				.Select(s => $"{s.Group}|{s.Title}|{s.DefaultWidth}|{s.DefaultHeight}|{s.OpenByDefault}")
				.ToList();

			await Assert.That(actual).IsEquivalentTo(SpecsRs, TUnit.Assertions.Enums.CollectionOrdering.Matching);
			await Assert.That(GuiDemoSpecs.Groups)
				.IsEquivalentTo(new[] { "Widgets", "Layout", "Graphics", "Interaction", "Tests", "Window Resize Test", "Tools" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

			// Every spec is filed under a listed group, and titles are unique (they key the factories).
			foreach (DemoSpec spec in GuiDemoSpecs.All)
			{
				await Assert.That(GuiDemoSpecs.Groups.Contains(spec.Group)).IsTrue();
			}

			await Assert.That(GuiDemoSpecs.All.Select(s => s.Title).Distinct().Count()).IsEqualTo(GuiDemoSpecs.All.Count);
		}

		[Test]
		public async Task EveryWindowBuildsNamedContent()
		{
			foreach (DemoSpec spec in GuiDemoSpecs.All)
			{
				GuiWidget content = GuiDemoSpecs.CreateContent(spec);
				await Assert.That(content).IsNotNull();
				await Assert.That(content.Name).IsEqualTo(spec.Title + " Content");

				// A second build gets the same name, so automation can find a window's content every time.
				await Assert.That(GuiDemoSpecs.CreateContent(spec).Name).IsEqualTo(content.Name);
			}
		}

		[Test]
		public async Task EveryWindowClassIsSomeSpecsContent()
		{
			// Guards merges: a cherry-pick that brings back a window's ComingSoon line leaves its built window
			// class unreachable, and this names it. (A duplicate factory key throws when GuiDemoSpecs loads.)
			var reached = new HashSet<System.Type>();
			foreach (DemoSpec spec in GuiDemoSpecs.All.Append(GuiDemoSpecs.About).Append(GuiDemoSpecs.Inspector))
			{
				reached.Add(GuiDemoSpecs.CreateContent(spec).GetType());
			}

			string windowsNamespace = typeof(GuiDemoSpecs).Namespace + ".Windows";
			List<string> unreachable = typeof(GuiDemoSpecs).Assembly.GetTypes()
				.Where(t => t.Namespace != null && (t.Namespace == windowsNamespace || t.Namespace.StartsWith(windowsNamespace + ".")))
				.Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Window") && typeof(GuiWidget).IsAssignableFrom(t))
				.Where(t => !reached.Contains(t))
				.Select(t => t.FullName)
				.ToList();

			await Assert.That(unreachable).IsEmpty();
		}
	}
}
