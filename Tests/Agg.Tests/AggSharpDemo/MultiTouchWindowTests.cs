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

using System;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Interaction;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Multi Touch window (agg-gui's text_demos/multi_touch.rs).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class MultiTouchWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Multi Touch");

		private static (MultiTouchWindow Window, GuiWidget Host) Build(DemoTheme demoTheme = null)
		{
			var window = (MultiTouchWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme ?? new DemoTheme());
			var host = new GuiWidget(Spec.DefaultWidth, Spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();
			return (window, host);
		}

		/// <summary>A mouse event at view-local <paramref name="viewPositions"/>, in <paramref name="host"/>'s space.</summary>
		private static MouseEventArgs At(GuiWidget host, MultiTouchView view, params Vector2[] viewPositions)
			=> new MouseEventArgs(MouseButtons.Left, 0, viewPositions.Select(p => view.TransformToParentSpace(host, p)).ToArray(), 0, null);

		[Test]
		public async Task BuildsTheControlsAndDrawsInBothThemes()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			(MultiTouchWindow window, _) = Build(demoTheme);

			await Assert.That(window.Name).IsEqualTo("Multi Touch Content");
			await Assert.That(window.FindDescendant("Multi Touch View")).IsSameReferenceAs(window.View);
			await Assert.That(window.FindDescendant("Multi Touch Status")).IsSameReferenceAs(window.StatusLabel);
			await Assert.That(window.FindDescendant("Multi Touch Source Link")).IsNotNull();
			await Assert.That(window.StatusLabel.Text).IsEqualTo("Input source: none");
			await Assert.That(window.View.Height).IsGreaterThan(100);

			// At home the arrow runs bottom-left to top-right across the middle.
			window.View.Clock = () => 0;
			(Vector2 tail, Vector2 tip) = window.View.ArrowEnds();
			await Assert.That(tail.X).IsLessThan(tip.X);
			await Assert.That(tail.Y).IsLessThan(tip.Y);
			await Assert.That(((tail + tip) / 2 - new Vector2(window.View.Width / 2, window.View.Height / 2)).Length).IsLessThan(1e-9);

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(demoTheme.Palette.PanelFill);
			window.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task TwoFingerMovesRoutedToTheViewPinchTurnAndSlideTheArrow()
		{
			(MultiTouchWindow window, GuiWidget host) = Build();
			MultiTouchView view = window.View;
			view.Clock = () => 0;
			var c = new Vector2(view.Width / 2, view.Height / 2);

			host.OnMouseDown(At(host, view, c + new Vector2(-40, 0)));
			host.OnMouseMove(At(host, view, c + new Vector2(-40, 0), c + new Vector2(40, 0)));
			await Assert.That(window.StatusLabel.Text).IsEqualTo("Input source: 2-finger touch");

			// Spread to twice the distance, turned a quarter counter-clockwise, centre slid right by 10.
			host.OnMouseMove(At(host, view, c + new Vector2(10, -80), c + new Vector2(10, 80)));
			await Assert.That(Math.Abs(view.Zoom - 2)).IsLessThan(1e-9);
			await Assert.That(Math.Abs(view.Rotation - (Math.PI / 2))).IsLessThan(1e-9);
			await Assert.That((view.Translation - new Vector2(10 / view.UnitScale, 0)).Length).IsLessThan(1e-9);
			await Assert.That(view.NumTouches).IsEqualTo(2);

			host.OnMouseUp(At(host, view, c));
			await Assert.That(window.StatusLabel.Text).IsEqualTo("Input source: none");
		}

		[Test]
		public async Task ATrackpadPinchIsAMultiTouchZoomNotAScroll()
		{
			(MultiTouchWindow window, GuiWidget host) = Build();
			MultiTouchView view = window.View;
			Vector2 centre = new Vector2(view.Width / 2, view.Height / 2);
			Vector2 pointer = view.TransformToParentSpace(host, centre);
			host.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, pointer.X, pointer.Y, 120) { FromTrackpadPinch = true });
			await Assert.That(view.Translation).IsEqualTo(Vector2.Zero);

			var fingers = new TrackpadPinchFingers(50);
			foreach (Vector2[] frame in fingers.Magnify(pointer, .5, TrackpadGesturePhase.Began))
			{
				host.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, frame, 0, null));
			}

			await Assert.That(Math.Abs(view.Zoom - 1.5)).IsLessThan(1e-9);
			await Assert.That(view.Translation.Length).IsLessThan(1e-9);
		}

		[Test]
		public async Task ALiftedArrowHoldsThenDriftsHome()
		{
			MultiTouchView view = Build().Window.View;
			double now = 0;
			view.Clock = () => now;
			view.Integrate(new MultiTouchInfo(2, 1.5, 0.3, new Vector2(20, 0), 0.4, Vector2.Zero));
			await Assert.That(view.SlowlyReset(0.25, 1.0 / 60)).IsTrue();
			await Assert.That(view.Zoom).IsEqualTo(1.5);

			// Past the hold it eases toward home, and a second later it is there.
			view.SlowlyReset(0.6, 1.0 / 60);
			await Assert.That(view.Zoom).IsLessThan(1.5);
			await Assert.That(view.Zoom).IsGreaterThan(1.0);
			await Assert.That(view.SlowlyReset(1.0, 1.0 / 60)).IsFalse();
			await Assert.That(view.Zoom).IsEqualTo(1.0);
			await Assert.That(view.Rotation).IsEqualTo(0.0);
			await Assert.That(view.Translation).IsEqualTo(Vector2.Zero);
		}

		[Test]
		public async Task APinchThroughTheSystemWindowReachesTheView()
		{
			var window = (MultiTouchWindow)GuiDemoSpecs.CreateContent(Spec, new DemoTheme(ThemePreference.Light));
			var systemWindow = new SystemWindow(600, 400) { Name = "Multi Touch Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				MultiTouchView view = window.View;
				testRunner.WaitForName("Multi Touch View");
				var c = new Vector2(view.Width / 2, view.Height / 2);
				MouseEventArgs Touches(params Vector2[] positions) =>
					new MouseEventArgs(MouseButtons.Left, 0, positions.Select(p => view.TransformToParentSpace(systemWindow, c + p)).ToArray(), 0, null);

				UiThread.RunOnIdle(() =>
				{
					systemWindow.OnMouseDown(Touches(new Vector2(-30, 0)));
					systemWindow.OnMouseMove(Touches(new Vector2(-30, 0), new Vector2(30, 0)));
					systemWindow.OnMouseMove(Touches(new Vector2(-60, 0), new Vector2(60, 0)));
				});
				testRunner.WaitFor(() => view.Zoom > 1.5);

				await Assert.That(window.StatusLabel.Text).IsEqualTo("Input source: 2-finger touch");
				await Assert.That(Math.Abs(view.Zoom - 2)).IsLessThan(1e-9);
				UiThread.RunOnIdle(() => systemWindow.OnMouseUp(Touches(Vector2.Zero)));
				testRunner.WaitFor(() => window.StatusLabel.Text == "Input source: none");
				testRunner.MarkTestComplete();
			});
		}
	}
}
