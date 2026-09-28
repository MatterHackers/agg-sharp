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
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Input Test window (agg-gui's tests/basic/input_probe); the classifier cases are
	// classifier.rs's own tests.
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class InputTestWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Input Test");

		private static Vector2 P(double x, double y) => new Vector2(x, y);

		private static Interaction Click(MouseButtons button, int count) => new Interaction(InteractionType.Click, button, count);

		[Test]
		public async Task ClicksCountUpWithinTheWindowAndSpot()
		{
			var c = new InteractionClassifier(ProbeKind.Click);
			await Assert.That(c.OnDown(MouseButtons.Left, P(10, 10), 0).Count).IsEqualTo(0);
			await Assert.That(c.OnUp(MouseButtons.Left, P(10, 10), 10).Single()).IsEqualTo(Click(MouseButtons.Left, 1));
			c.OnDown(MouseButtons.Left, P(11, 11), 100);
			await Assert.That(c.OnUp(MouseButtons.Left, P(11, 11), 110).Single()).IsEqualTo(Click(MouseButtons.Left, 2));
			c.OnDown(MouseButtons.Left, P(10, 12), 200);
			await Assert.That(c.OnUp(MouseButtons.Left, P(10, 12), 210).Single()).IsEqualTo(Click(MouseButtons.Left, 3));

			// Past the 400 ms window the sequence restarts.
			c.OnDown(MouseButtons.Left, P(10, 12), 700);
			await Assert.That(c.OnUp(MouseButtons.Left, P(10, 12), 710).Single()).IsEqualTo(Click(MouseButtons.Left, 1));

			// Too far from the previous click, even in time: restarts.
			c.OnDown(MouseButtons.Left, P(100, 100), 750);
			await Assert.That(c.OnUp(MouseButtons.Left, P(100, 100), 760).Single()).IsEqualTo(Click(MouseButtons.Left, 1));

			// Another button is its own sequence.
			c.OnDown(MouseButtons.Right, P(100, 100), 800);
			await Assert.That(c.OnUp(MouseButtons.Right, P(100, 100), 810).Single()).IsEqualTo(Click(MouseButtons.Right, 1));
		}

		[Test]
		public async Task ClickProbeIgnoresAMovedRelease()
		{
			var c = new InteractionClassifier(ProbeKind.Click);
			c.OnDown(MouseButtons.Left, P(10, 10), 0);
			await Assert.That(c.OnMove(P(40, 40), true, 20).Count).IsEqualTo(0);
			await Assert.That(c.OnUp(MouseButtons.Left, P(40, 40), 30).Count).IsEqualTo(0);
		}

		[Test]
		public async Task DragProbeReportsStartDeltasAndStop()
		{
			var c = new InteractionClassifier(ProbeKind.Drag);
			c.OnDown(MouseButtons.Left, P(0, 0), 0);
			await Assert.That(c.OnMove(P(3, 0), true, 5).Count).IsEqualTo(0);
			await Assert.That(c.OnMove(P(10, 0), true, 10).ToArray()).IsEquivalentTo(new[]
			{
				new Interaction(InteractionType.DragStarted, MouseButtons.Left),
				new Interaction(InteractionType.Dragged, MouseButtons.Left, Dx: 7, Dy: 0),
			});
			await Assert.That(c.OnMove(P(15, 4), true, 15).Single()).IsEqualTo(new Interaction(InteractionType.Dragged, MouseButtons.Left, Dx: 5, Dy: 4));
			await Assert.That(c.OnUp(MouseButtons.Left, P(15, 4), 20).Single()).IsEqualTo(new Interaction(InteractionType.DragStopped, MouseButtons.Left));

			// A drag that never crosses the threshold is neither a drag nor a click.
			c.OnDown(MouseButtons.Left, P(0, 0), 100);
			await Assert.That(c.OnMove(P(2, 2), true, 105).Count).IsEqualTo(0);
			await Assert.That(c.OnUp(MouseButtons.Left, P(2, 2), 110).Count).IsEqualTo(0);
		}

		[Test]
		public async Task ClickAndDragTellsAStillGestureFromAMovedOne()
		{
			var c = new InteractionClassifier(ProbeKind.ClickAndDrag);
			c.OnDown(MouseButtons.Left, P(0, 0), 0);
			await Assert.That(c.OnUp(MouseButtons.Left, P(1, 1), 10).Single()).IsEqualTo(Click(MouseButtons.Left, 1));

			c = new InteractionClassifier(ProbeKind.ClickAndDrag);
			c.OnDown(MouseButtons.Left, P(0, 0), 0);
			await Assert.That(c.OnMove(P(20, 0), true, 5)).Contains(new Interaction(InteractionType.DragStarted, MouseButtons.Left));
			await Assert.That(c.OnUp(MouseButtons.Left, P(20, 0), 10).Single()).IsEqualTo(new Interaction(InteractionType.DragStopped, MouseButtons.Left));
		}

		[Test]
		public async Task HoverProbeOnlyEntersAndLeaves()
		{
			var c = new InteractionClassifier(ProbeKind.Hover);
			await Assert.That(c.OnMove(P(5, 5), true, 0).Single()).IsEqualTo(Interaction.HoverEnter);
			await Assert.That(c.OnMove(P(6, 6), true, 5).Count).IsEqualTo(0);
			await Assert.That(c.OnMove(P(-1, -1), false, 10).Single()).IsEqualTo(Interaction.HoverLeave);

			// It never records a press, so it never clicks or drags however the pointer moves while held.
			c.OnDown(MouseButtons.Left, P(5, 5), 20);
			await Assert.That(c.OnMove(P(40, 40), true, 25).All(i => i.IsHover)).IsTrue();
			await Assert.That(c.OnUp(MouseButtons.Left, P(40, 40), 30).Count).IsEqualTo(0);
		}

		[Test]
		public async Task DescribeUsesEguisWording()
		{
			await Assert.That(InteractionClassifier.Describe(Click(MouseButtons.Left, 2)).Summary).IsEqualTo("Double-clicked");
			await Assert.That(InteractionClassifier.Describe(Click(MouseButtons.Right, 5)).Summary).IsEqualTo("Triple-clicked by Right button");
			(string summary, string full) = InteractionClassifier.Describe(new Interaction(InteractionType.Dragged, MouseButtons.Middle, Dx: 7, Dy: -3));
			await Assert.That(summary).IsEqualTo("Dragged by Middle button");
			await Assert.That(full).IsEqualTo("Dragged by Middle button (Δ7, -3)");
		}

		[Test]
		public async Task BuildsFourProbesAndDrawsInBothThemes()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (InputTestWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme);
			var host = new GuiWidget(640, 400);
			host.AddChild(window);
			host.PerformLayout();

			await Assert.That(window.Name).IsEqualTo("Input Test Content");
			await Assert.That(window.Probes.Select(p => p.Kind).ToArray()).IsEquivalentTo(new[] { ProbeKind.Hover, ProbeKind.Click, ProbeKind.Drag, ProbeKind.ClickAndDrag });
			await Assert.That(window.FindDescendant("Input Test Probe Click + Drag")).IsSameReferenceAs(window.Probes[3]);

			// The probes share the row's width equally and fill the height left under the controls.
			await Assert.That(window.Probes[1].Width).IsEqualTo(window.Probes[0].Width).Within(1);
			await Assert.That(window.Probes[0].Width).IsGreaterThan(100);
			await Assert.That(window.Probes[0].Height).IsGreaterThan(150);

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());

			// The probe's header text is drawn: its title band holds text-coloured pixels.
			InputProbe probe = window.Probes[0];
			RectangleDouble probeBounds = probe.TransformToParentSpace(window, probe.LocalBounds);
			int darkPixels = 0;
			for (int y = (int)(probeBounds.Top - 64 * GuiWidget.DeviceScale); y < (int)probeBounds.Top - 2; y++)
			{
				for (int x = (int)probeBounds.Left + 2; x < (int)probeBounds.Right - 2; x++)
				{
					if (image.GetPixel(x, y).Red0To255 < 128)
					{
						darkPixels++;
					}
				}
			}

			await Assert.That(darkPixels).IsGreaterThan(20);
			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(demoTheme.Palette.PanelFill);
			window.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task ProbesClassifyRealMouseInput()
		{
			var window = (InputTestWindow)GuiDemoSpecs.CreateContent(Spec, new DemoTheme(ThemePreference.Light));
			var systemWindow = new SystemWindow(760, 420) { Name = "Input Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				InputProbe hover = window.Probes[0];
				InputProbe click = window.Probes[1];
				InputProbe drag = window.Probes[2];

				// Hover events stay out of the history until the checkbox lets them in.
				testRunner.MoveToByName("Input Test Probe Hover");
				testRunner.WaitFor(() => hover.ContainsPointer);
				await Assert.That(hover.History.Entries.Count).IsEqualTo(0);
				testRunner.ClickByName("Input Test Include Hover");
				testRunner.WaitFor(() => hover.IncludeHover);
				testRunner.MoveToByName("Input Test Probe Hover");
				testRunner.WaitFor(() => hover.History.Entries.Any(e => e.Summary == "Hover enter"));

				testRunner.ClickByName("Input Test Probe Click");
				testRunner.WaitFor(() => click.History.Entries.Count > 0);
				await Assert.That(click.History.Entries[0].Summary).IsEqualTo("Clicked");

				testRunner.DragByName("Input Test Probe Drag");
				testRunner.DropByName("Input Test Probe Drag", offset: new Point2D(30, 20));
				testRunner.WaitFor(() => drag.History.Entries.Any(e => e.Summary == "Drag stopped"));
				await Assert.That(drag.History.Entries.Any(e => e.Summary == "Drag started")).IsTrue();

				testRunner.ClickByName("Input Test Clear");
				testRunner.WaitFor(() => drag.History.Entries.Count == 0);
				await Assert.That(click.History.Entries.Count).IsEqualTo(0);
				testRunner.MarkTestComplete();
			});
		}
	}
}
