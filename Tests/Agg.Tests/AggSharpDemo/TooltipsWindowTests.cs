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
using System.Reflection;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Tooltips window (agg-gui's demo-ui/src/windows/basic.rs).
	public class TooltipsWindowTests
	{
		private static DemoSpec TooltipsSpec => GuiDemoSpecs.All.First(s => s.Title == "Tooltips");

		private static TooltipsWindow Build()
		{
			var page = new GuiWidget(400, 300);
			GuiWidget content = GuiDemoSpecs.CreateContent(TooltipsSpec);
			page.AddChild(content);
			page.PerformLayout();
			return (TooltipsWindow)content;
		}

		[Test]
		public async Task BuildsEveryControlNamedWithTooltips()
		{
			TooltipsWindow window = Build();
			await Assert.That(window.Name).IsEqualTo("Tooltips Content");

			string[] names =
			{
				"Tooltips Splitter", "Tooltips Source Link", "Tooltips Enabled", "Tooltips Sometimes Clickable",
				"Tooltips Scroll", "Tooltips Line 0", "Tooltips Line 999",
			};
			foreach (string name in names)
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull();
			}

			// agg-gui's 62/38 split, measured across the width.
			await Assert.That(window.Splitter.SplitterDistance).IsEqualTo(System.Math.Round(400 * 0.62));

			// Every line is hoverable (selectable) and carries its tooltip.
			GuiWidget line = window.FindDescendant("Tooltips Line 500");
			await Assert.That(line.Selectable).IsTrue();
			await Assert.That(line.ToolTipText).IsEqualTo("This tooltip is interactive, because the text in it is selectable.");
			await Assert.That(window.SometimesClickableButton.ToolTipText).IsEqualTo(TooltipsWindow.ButtonTip);
		}

		[Test]
		[NotInParallel]
		public async Task TheInteractiveTooltipHoldsALinkWithItsOwnTooltip()
		{
			var systemWindow = new SystemWindow(800, 600);
			try
			{
				GuiWidget content = GuiDemoSpecs.CreateContent(TooltipsSpec);
				systemWindow.AddChild(content);
				systemWindow.PerformLayout();
				var window = (TooltipsWindow)content;

				// The label is hoverable and its tooltip is a widget, not text.
				await Assert.That(window.InteractiveLabel.Selectable).IsTrue();
				Vector2 overLabel = window.InteractiveLabel.TransformToScreenSpace(window.InteractiveLabel.LocalBounds).Center;
				systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, overLabel.X, overLabel.Y, 0));
				var doShow = typeof(ToolTipManager).GetMethod("DoShowToolTip", BindingFlags.Instance | BindingFlags.NonPublic);
				await Assert.That((bool)doShow.Invoke(systemWindow.ToolTipManager, null)).IsTrue();

				GuiWidget tip = systemWindow.ToolTipManager.ContentToolTip;
				await Assert.That(tip).IsNotNull();
				var link = (Hyperlink)tip.FindDescendant("Tooltips Interactive Link");
				await Assert.That(link.Url).IsEqualTo(TooltipsWindow.LinkUrl);
				await Assert.That(link.ToolTipText).IsEqualTo(TooltipsWindow.NestedTip);
			}
			finally
			{
				systemWindow.ToolTipManager.Dispose();
			}
		}

		[Test]
		public async Task DisablingTheButtonSwapsItsTooltip()
		{
			TooltipsWindow window = Build();

			window.EnabledCheckBox.Checked = false;
			await Assert.That(window.SometimesClickableButton.Enabled).IsFalse();
			await Assert.That(window.SometimesClickableButton.ToolTipText).IsEqualTo(TooltipsWindow.DisabledButtonTip);

			window.EnabledCheckBox.Checked = true;
			await Assert.That(window.SometimesClickableButton.Enabled).IsTrue();
			await Assert.That(window.SometimesClickableButton.ToolTipText).IsEqualTo(TooltipsWindow.ButtonTip);
		}
	}
}
