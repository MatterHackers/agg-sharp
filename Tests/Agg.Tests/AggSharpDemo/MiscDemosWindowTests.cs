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
using MatterHackers.GuiAutomation;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Misc Demos window (agg-gui's demo-ui/src/windows/misc/).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(ThemeConfig.Current) })]
	public class MiscDemosWindowTests
	{
		private static DemoSpec MiscSpec => GuiDemoSpecs.All.First(s => s.Title == "Misc Demos");

		private static MiscDemosWindow Build()
		{
			var page = new GuiWidget(500, 400);
			GuiWidget content = GuiDemoSpecs.CreateContent(MiscSpec);
			page.AddChild(content);
			page.PerformLayout();
			return (MiscDemosWindow)content;
		}

		[Test]
		public async Task BuildsEverySectionAndControlNamed()
		{
			MiscDemosWindow window = Build();
			await Assert.That(window.Name).IsEqualTo("Misc Demos Content");

			foreach (string title in MiscDemosWindow.SectionTitles)
			{
				var section = (CollapsingHeader)window.FindDescendant($"Misc Section {title}");
				await Assert.That(section).IsNotNull();
				await Assert.That(section.Expanded).IsEqualTo(title == "Label").Because("only Label starts open, as in egui");
			}

			string[] names =
			{
				"Misc Scroll", "Misc Label Colors", "Misc Angle", "Misc Angle Turns", "Misc Password", "Misc Password Reveal",
				"Misc Color Picker", "Misc Swatch Pink", "Misc Tree", "Misc Tree Node root", "Misc Tree Add root",
				"Misc Tree Delete 1.2", "Misc Tree Add 1.2.1", "Misc Checkbox 0", "Misc Checkbox 63", "Misc Checkbox Labelled",
				"Misc Radio 0", "Misc Radio 64", "Misc Check All", "Misc Item 3", "Misc Columns Slider", "Misc Column 1",
				"Misc Columns Delete", "Misc Box width", "Misc Box number of boxes", "Misc Box Painter",
				"Misc Custom Header Toggle", "Misc Custom Header Body", "Misc Painted Icon", "Misc Resize", "Resize Grip",
				"Misc Many Circles",
			};
			foreach (string name in names)
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull().Because(name);
			}

			await Assert.That(window.FindDescendant("Misc Angle Turns").Text).IsEqualTo("≈ 0.333τ");

			// Item 1 alone is checked, so "check all" starts indeterminate.
			var all = (CheckBox)window.FindDescendant("Misc Check All");
			await Assert.That(all.Checked).IsFalse();
			await Assert.That(all.Indeterminate).IsTrue();
		}

		[Test]
		public async Task PasswordRevealSwapsTheEyeForTheEyeSlash()
		{
			MiscDemosWindow window = Build();
			var password = (ThemedPasswordTextEditWidget)window.FindDescendant("Misc Password");
			var reveal = (ThemedIconButton)window.FindDescendant("Misc Password Reveal");
			await Assert.That(password.Hidden).IsTrue();

			// The button draws Font Awesome glyphs, as agg-gui's does, not words.
			byte[] eye = reveal.IconImage.GetBuffer().ToArray();
			await Assert.That(eye.Any(b => b != 0)).IsTrue().Because("the eye glyph drew");

			reveal.InvokeClick();
			await Assert.That(password.Hidden).IsFalse();
			byte[] eyeSlash = reveal.IconImage.GetBuffer().ToArray();
			await Assert.That(eyeSlash.SequenceEqual(eye)).IsFalse().Because("revealed, it shows the eye-slash");

			reveal.InvokeClick();
			await Assert.That(password.Hidden).IsTrue();
			await Assert.That(reveal.IconImage.GetBuffer().SequenceEqual(eye)).IsTrue();
		}

		[Test]
		public async Task TreeModelAddsAndDeletesByPath()
		{
			var root = MiscTreeNode.Demo();
			await Assert.That(root.Children.Count).IsEqualTo(2);
			await Assert.That(root.Children[0].Children.Count).IsEqualTo(4);
			await Assert.That(root.Children[1].Children[2].Children.Count).IsEqualTo(2);

			root.AddChild(new int[0]);
			root.AddChild(new[] { 0 });
			await Assert.That(root.Children.Count).IsEqualTo(3);
			await Assert.That(root.Children[0].Children.Count).IsEqualTo(5);

			root.Delete(new[] { 0, 2 });
			await Assert.That(root.Children[0].Children.Count).IsEqualTo(4);

			root.Delete(new int[0]);
			await Assert.That(root.Children.Count).IsEqualTo(3).Because("the root cannot delete itself");
		}

		[Test]
		public async Task CheckAllAndTreeAndColumnsRespondToClicks()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (MiscDemosWindow)GuiDemoSpecs.CreateContent(MiscSpec, demoTheme);
			var systemWindow = new SystemWindow(560, 900) { Name = "Misc Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				// Close Label so the next sections sit near the top; open Checkboxes and tick "check all".
				testRunner.ClickByName("Label Header");
				testRunner.ClickByName("Checkboxes Header");
				testRunner.ClickByName("Misc Check All");
				var items = Enumerable.Range(1, 3).Select(i => (CheckBox)window.FindDescendant($"Misc Item {i}")).ToList();
				testRunner.WaitFor(() => items.All(c => c.Checked));
				var all = (CheckBox)window.FindDescendant("Misc Check All");
				await Assert.That(all.Indeterminate).IsFalse();

				// Unticking one item leaves "check all" indeterminate.
				testRunner.ClickByName("Misc Item 2");
				testRunner.WaitFor(() => all.Indeterminate);
				await Assert.That(all.Checked).IsFalse();
				testRunner.ClickByName("Checkboxes Header");

				// The tree's root "+" adds a third child.
				var tree = (MiscTreeSection)window.FindDescendant("Misc Tree");
				testRunner.ClickByName("Tree Header");
				testRunner.ClickByName("Misc Tree Add root");
				testRunner.WaitForName("Misc Tree Node 2");
				await Assert.That(tree.Root.Children.Count).IsEqualTo(3);
				testRunner.ClickByName("Tree Header");

				// "Delete this" drops a column.
				testRunner.ClickByName("Columns Header");
				testRunner.ClickByName("Misc Columns Delete");
				testRunner.WaitFor(() => window.ColumnCountSlider.Value == 1);
				testRunner.WaitFor(() => window.FindDescendant("Misc Column 1") == null);
				testRunner.MarkTestComplete();
			});
		}
	}
}
