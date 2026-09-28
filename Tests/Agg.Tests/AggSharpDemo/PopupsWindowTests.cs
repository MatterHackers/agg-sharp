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
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Popups window (agg-gui's demo-ui/src/windows/popups_demo.rs).
	public class PopupsWindowTests
	{
		private static DemoSpec PopupsSpec => GuiDemoSpecs.All.First(s => s.Title == "Popups");

		private static PopupsWindow Build()
		{
			var page = new GuiWidget(500, 500);
			GuiWidget content = GuiDemoSpecs.CreateContent(PopupsSpec);
			page.AddChild(content);
			page.PerformLayout();
			return (PopupsWindow)content;
		}

		[Test]
		public async Task BuildsEveryControlNamedAtTheDefaults()
		{
			PopupsWindow window = Build();
			await Assert.That(window.Name).IsEqualTo("Popups Content");

			string[] names =
			{
				"Popups Config Scroll", "Popups Reset", "Popups Parent", "Popups Child", "Popups Preset", "Popups Gap",
				"Popups Close Behavior", "Popups Open", "Popups Trigger", "Popups Context Action",
			};
			foreach (string name in names)
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull();
			}

			await Assert.That(window.Trigger.ToolTipText).IsEqualTo(PopupsWindow.TriggerTip);
			await Assert.That(window.PresetCombo.SelectedLabel).IsEqualTo("BOTTOM_START");
			await Assert.That(window.ParentCombo.SelectedLabel).IsEqualTo("LEFT_BOTTOM");
			await Assert.That(window.ChildCombo.SelectedLabel).IsEqualTo("LEFT_TOP");
			await Assert.That(window.BehaviorCombo.SelectedLabel).IsEqualTo("CloseOnClick");
			await Assert.That(window.GapValue.Value).IsEqualTo(PopupsWindow.DefaultGap);
			await Assert.That(window.Popup.IsOpen).IsFalse();
		}

		[Test]
		public async Task ThePopupTitleStartsWithTheCommentIcon()
		{
			PopupsWindow window = Build();

			// agg-gui's "\u{F075}  Popup contents": the icon, then the title, on one row.
			var icon = (ImageWidget)window.PopupPanel.FindDescendant("Popups Popup Title Icon");
			await Assert.That(icon).IsNotNull();
			await Assert.That(icon.Image).IsNotNull();
			await Assert.That(icon.Parent.Children.IndexOf(icon)).IsEqualTo(0);
			await Assert.That(icon.Parent.Children[1].Text).IsEqualTo("Popup contents");

			// Drawn in the title's colour, so it reads in either theme.
			Color ink = icon.Image.GetPixel(icon.Image.Width / 2, icon.Image.Height / 2);
			await Assert.That(ink.alpha).IsGreaterThan((byte)0);
		}

		[Test]
		public async Task PresetAndAnchorCombosFollowEachOther()
		{
			PopupsWindow window = Build();

			// Picking a preset sets both anchor points.
			window.PresetCombo.SelectedIndex = RectAlign.Right.PresetIndex + 1;
			await Assert.That(window.ParentCombo.SelectedLabel).IsEqualTo("RIGHT_CENTER");
			await Assert.That(window.ChildCombo.SelectedLabel).IsEqualTo("LEFT_CENTER");
			await Assert.That(window.Popup.Align).IsEqualTo(RectAlign.Right);

			// A pair with no name shows the placeholder; Reset brings back the default.
			window.ChildCombo.SelectedIndex = Align2.Center.AllIndex;
			await Assert.That(window.PresetCombo.SelectedIndex).IsEqualTo(0);
			window.Reset();
			await Assert.That(window.Popup.Align).IsEqualTo(RectAlign.BottomStart);
			await Assert.That(window.PresetCombo.SelectedLabel).IsEqualTo("BOTTOM_START");
		}

		[Test]
		[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(ThemeConfig.Current) })]
		public async Task ClickingTheTriggerOpensThePopupAndAClickClosesIt()
		{
			var window = new SystemWindow(600, 600) { Name = "Popups Test Window" };
			var popups = (PopupsWindow)GuiDemoSpecs.CreateContent(PopupsSpec);
			window.AddChild(popups);

			await AutomationRunner.ShowWindowAndExecuteTests(window, async testRunner =>
			{
				// Left click opens the popup under the trigger, and the checkbox follows.
				testRunner.ClickByName("Popups Trigger");
				testRunner.WaitForName("Popups Popup");
				await Assert.That(popups.Popup.IsOpen).IsTrue();
				await Assert.That(popups.OpenCheckBox.Checked).IsTrue();
				RectangleDouble trigger = popups.Trigger.TransformToParentSpace(window, popups.Trigger.LocalBounds);
				await Assert.That(popups.PopupPanel.Position.Y + popups.PopupPanel.Height).IsEqualTo(trigger.Bottom - PopupsWindow.DefaultGap);

				// agg-gui's drop shadow: the panel's shape, 4 px right and down, painted just under it.
				await Assert.That(window.Children.IndexOf(popups.PopupShadow)).IsEqualTo(window.Children.IndexOf(popups.PopupPanel) - 1);
				await Assert.That(popups.PopupShadow.Size).IsEqualTo(popups.PopupPanel.Size);
				await Assert.That(popups.PopupShadow.Position).IsEqualTo(popups.PopupPanel.Position + new Vector2(4, -4) * GuiWidget.DeviceScale);

				// CloseOnClick: a click inside the popup closes it.
				testRunner.ClickByName("Popups Popup");
				testRunner.WaitFor(() => !popups.Popup.IsOpen);
				await Assert.That(popups.PopupPanel.Parent).IsNull();
				await Assert.That(popups.OpenCheckBox.Checked).IsFalse();
				await Assert.That(popups.PopupShadow.Parent).IsNull();

				// Escape closes even an IgnoreClicks popup, which a click outside does not.
				popups.BehaviorCombo.SelectedIndex = (int)PopupCloseBehavior.IgnoreClicks;
				testRunner.ClickByName("Popups Trigger");
				testRunner.WaitForName("Popups Popup");
				testRunner.ClickByName("Popups Gap");
				await Assert.That(popups.Popup.IsOpen).IsTrue();
				window.OnKeyDown(new KeyEventArgs(Keys.Escape));
				testRunner.WaitFor(() => !popups.Popup.IsOpen);
				await Assert.That(popups.PopupPanel.Parent).IsNull();

				// Right click opens the context menu; its action is reported under the trigger.
				testRunner.RightClickByName("Popups Trigger");
				testRunner.ClickByName("Popups Context copy");
				testRunner.WaitFor(() => popups.ContextActionText.Text == "Context action: copy");
				await Assert.That(popups.ContextActionText.Text).IsEqualTo("Context action: copy");
				testRunner.MarkTestComplete();
			});
		}
	}
}
