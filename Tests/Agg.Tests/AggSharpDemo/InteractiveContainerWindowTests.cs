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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Interactive Container window (agg-gui's misc/interactive_container.rs).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class InteractiveContainerWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Interactive Container");

		[Test]
		public async Task BuildsTheContainerAndRecolours()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (InteractiveContainerWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme);
			var host = new GuiWidget(Spec.DefaultWidth, Spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();

			InteractiveContainer container = window.Container;
			await Assert.That(window.Name).IsEqualTo("Interactive Container Content");
			await Assert.That(window.FindDescendant("Interactive Container")).IsSameReferenceAs(container);
			await Assert.That(window.FindDescendant("Interactive Container Reset")).IsSameReferenceAs(container.ResetButton);
			await Assert.That(window.FindDescendant("Interactive Container Plus 100")).IsSameReferenceAs(container.PlusButton);
			await Assert.That(container.CountText.Text).IsEqualTo("0");

			// The buttons share the bottom row, "+ 100" right of Reset, both under the number.
			RectangleDouble reset = container.ResetButton.TransformToParentSpace(container, container.ResetButton.LocalBounds);
			RectangleDouble plus = container.PlusButton.TransformToParentSpace(container, container.PlusButton.LocalBounds);
			await Assert.That(reset.Left).IsGreaterThan(0);
			await Assert.That(reset.Bottom).IsGreaterThan(0);
			await Assert.That(plus.Bottom).IsEqualTo(reset.Bottom);
			await Assert.That(plus.Left).IsGreaterThan(reset.Right);
			await Assert.That(container.CountText.BoundsRelativeToParent.Bottom).IsGreaterThan(reset.Top);
			await Assert.That(container.CountText.BoundsRelativeToParent.Top).IsLessThan(container.Height);

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(demoTheme.Palette.PanelFill);
			await Assert.That(container.CountText.TextColor).IsEqualTo(demoTheme.Theme.TextColor);
			await Assert.That(container.PlusButton.TextColor).IsEqualTo(demoTheme.Theme.TextColor);
			window.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task BackgroundClicksCountAndButtonClicksDoNot()
		{
			var window = (InteractiveContainerWindow)GuiDemoSpecs.CreateContent(Spec, new DemoTheme(ThemePreference.Light));
			InteractiveContainer container = window.Container;
			var systemWindow = new SystemWindow(Spec.DefaultWidth, Spec.DefaultHeight) { Name = "Interactive Container Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				// The count sits in the frame's background, so clicking it counts as agg-gui's painted label does.
				testRunner.ClickByName("Interactive Container Count");
				testRunner.WaitFor(() => container.Count == 1);
				await Assert.That(container.CountText.Text).IsEqualTo("1");

				// Hovering the frame lights it; moving onto a nested button does not.
				testRunner.MoveToByName("Interactive Container Count");
				testRunner.WaitFor(() => container.Hovered);
				testRunner.MoveToByName("Interactive Container Plus 100");
				testRunner.WaitFor(() => !container.Hovered);

				testRunner.ClickByName("Interactive Container Plus 100");
				testRunner.WaitFor(() => container.Count == 101);
				await Assert.That(container.Pressed).IsFalse();

				testRunner.ClickByName("Interactive Container Reset");
				testRunner.WaitFor(() => container.Count == 0);
				await Assert.That(container.CountText.Text).IsEqualTo("0");
				testRunner.MarkTestComplete();
			});
		}
	}
}
