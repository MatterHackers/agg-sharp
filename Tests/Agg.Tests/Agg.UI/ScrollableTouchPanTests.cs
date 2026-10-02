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

using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// A finger has no hover, so on touch the content itself is the scroll handle. As agg-gui's touch emulation does,
	// the SystemWindow holds a finger's press back: a tap (released within 8 px) is a click where it lifts, and a
	// drag past 8 px pans the nearest ScrollableWidget that can scroll that way, with no press reaching anything.
	// A mouse drag over the content does not pan.
	public class ScrollableTouchPanTests
	{
		private sealed class View
		{
			public SystemWindow Root;
			public ScrollableWidget Scroll;
			public GuiWidget Button;
			public int Clicks;
			public int Presses;
		}

		/// <summary>A 200 x 100 window over 500 of content, with a 100 x 40 button showing at (20..120, 20..60).</summary>
		private static View Make()
		{
			var view = new View { Root = new SystemWindow(200, 100) };
			view.Scroll = new ScrollableWidget(200, 100, autoScroll: true) { MinimumSize = Vector2.Zero };
			view.Root.AddChild(view.Scroll);
			var content = new GuiWidget(200, 500, SizeLimitsToSet.None);
			view.Scroll.AddChild(content);
			view.Button = AddButton(view, content, new Vector2(20, 420));
			return view;
		}

		private static GuiWidget AddButton(View view, GuiWidget parent, Vector2 origin)
		{
			var button = new GuiWidget(100, 40, SizeLimitsToSet.None) { OriginRelativeParent = origin };
			button.Click += (s, e) => view.Clicks++;
			button.MouseDown += (s, e) => view.Presses++;
			parent.AddChild(button);
			return button;
		}

		private static MouseEventArgs Touch(double x, double y)
			=> new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) { PointerType = PointerType.Touch };

		private static MouseEventArgs Mouse(double x, double y) => new MouseEventArgs(MouseButtons.Left, 1, x, y, 0);

		[Test]
		public async Task ATouchDragPansByTheFingersTravelAndNeverPresses()
		{
			View view = Make();
			await Assert.That(view.Scroll.ScrollOffsetFromTop()).IsEqualTo(0).Within(1e-9);

			view.Root.OnMouseDown(Touch(50, 40));

			// under the threshold it may still be a tap: nothing moves, and nothing is pressed yet
			view.Root.OnMouseMove(Touch(50, 45));
			await Assert.That(view.Scroll.ScrollOffsetFromTop()).IsEqualTo(0).Within(1e-9);
			await Assert.That(view.Presses).IsEqualTo(0);

			// past it, the content follows the finger from where it went down - up 30 shows 30 more below
			view.Root.OnMouseMove(Touch(50, 70));
			await Assert.That(view.Scroll.ScrollOffsetFromTop()).IsEqualTo(30).Within(1e-9);

			// released over the button, which has moved under the finger: a pan never presses or clicks
			view.Root.OnMouseUp(Touch(50, 70));
			await Assert.That(view.Presses).IsEqualTo(0);
			await Assert.That(view.Clicks).IsEqualTo(0);
			await Assert.That(view.Scroll.ScrollOffsetFromTop()).IsEqualTo(30).Within(1e-9);
		}

		[Test]
		public async Task ATouchTapClicksWhereTheFingerLifts()
		{
			// down just below the button, lifted on it, 7 px away: the click is at the lift, as in agg-gui
			View view = Make();
			view.Root.OnMouseDown(Touch(50, 14));
			view.Root.OnMouseMove(Touch(50, 21));
			await Assert.That(view.Presses).IsEqualTo(0);
			view.Root.OnMouseUp(Touch(50, 21));
			await Assert.That(view.Presses).IsEqualTo(1);
			await Assert.That(view.Clicks).IsEqualTo(1);
			await Assert.That(view.Scroll.ScrollOffsetFromTop()).IsEqualTo(0).Within(1e-9);
		}

		[Test]
		public async Task ACancelledTouchNeverClicks()
		{
			View view = Make();
			view.Root.OnMouseDown(Touch(50, 40));
			MouseEventArgs cancel = Touch(50, 40);
			cancel.Cancelled = true;
			view.Root.OnMouseUp(cancel);
			await Assert.That(view.Presses).IsEqualTo(0);
			await Assert.That(view.Clicks).IsEqualTo(0);
		}

		[Test]
		public async Task AMouseDragOverTheContentDoesNotPan()
		{
			View view = Make();
			view.Root.OnMouseDown(Mouse(50, 40));
			await Assert.That(view.Presses).IsEqualTo(1);
			view.Root.OnMouseMove(Mouse(50, 70));
			view.Root.OnMouseUp(Mouse(50, 70));
			await Assert.That(view.Scroll.ScrollOffsetFromTop()).IsEqualTo(0).Within(1e-9);

			// and a mouse click still clicks
			view.Root.OnMouseDown(Mouse(50, 40));
			view.Root.OnMouseUp(Mouse(50, 40));
			await Assert.That(view.Clicks).IsEqualTo(1);
		}

		[Test]
		public async Task AFloatingBarShowsWhileTouchPanningAndFadesAfter()
		{
			View view = Make();
			ScrollBar bar = view.Scroll.VerticalScrollBar;
			long now = 1_000_000;
			bar.Clock = () => now;
			bar.Floating = true;
			await Assert.That(bar.FadeTarget(now)).IsEqualTo(0);

			view.Root.OnMouseDown(Touch(50, 40));
			await Assert.That(bar.FadeTarget(now)).IsEqualTo(0);

			view.Root.OnMouseMove(Touch(50, 70));
			await Assert.That(bar.FadeTarget(now)).IsEqualTo(1);
			bar.StepFade(now + (long)ScrollBar.FadeMs);
			await Assert.That(bar.Opacity).IsEqualTo(1);

			view.Root.OnMouseUp(Touch(50, 70));
			await Assert.That(bar.FadeTarget(now)).IsEqualTo(0);
		}

		[Test]
		public async Task ASecondFingerStopsThePanAndTheGestureDoesNotClick()
		{
			// agg-gui hands a gesture to the multi-touch aggregate the moment a second finger lands
			View view = Make();
			view.Root.OnMouseDown(Touch(50, 40));
			view.Root.OnMouseMove(Touch(50, 60));
			await Assert.That(view.Scroll.ScrollOffsetFromTop()).IsEqualTo(20).Within(1e-9);

			var twoFingers = new MouseEventArgs(MouseButtons.Left, 0, new[] { new Vector2(50, 80), new Vector2(150, 80) }, 0, null)
			{
				PointerType = PointerType.Touch
			};
			view.Root.OnMouseMove(twoFingers);
			view.Root.OnMouseMove(Touch(50, 90));
			await Assert.That(view.Scroll.ScrollOffsetFromTop()).IsEqualTo(20).Within(1e-9);

			view.Root.OnMouseUp(Touch(50, 40));
			await Assert.That(view.Clicks).IsEqualTo(0);
		}

		[Test]
		public async Task AnInnerViewThatCannotScrollPassesThePanToItsParent()
		{
			// the inner view's content fits, so a vertical drag in it pans the outer view, as agg-gui's ScrollView
			// leaves a drag it cannot scroll to its ancestors
			View view = Make();
			var inner = new ScrollableWidget(100, 60, autoScroll: true) { MinimumSize = Vector2.Zero, OriginRelativeParent = new Vector2(20, 300) };
			inner.AddChild(new GuiWidget(100, 40, SizeLimitsToSet.None));
			view.Scroll.ScrollArea.Children[0].AddChild(inner);
			view.Scroll.SetScrollOffsetFromTop(150);
			double innerY = inner.TransformToScreenSpace(inner.LocalBounds).Center.Y;

			view.Root.OnMouseDown(Touch(50, innerY));
			view.Root.OnMouseMove(Touch(50, innerY + 20));
			view.Root.OnMouseUp(Touch(50, innerY + 20));
			await Assert.That(view.Scroll.ScrollOffsetFromTop()).IsEqualTo(170).Within(1e-9);
		}

		[Test]
		public async Task ADragTheViewCannotScrollIsTheWidgetsOwnDrag()
		{
			// nothing scrolls sideways here, so a sideways finger drag is the widget's drag (a slider, a 3D view):
			// pressed where the finger went down, released without a click
			View view = Make();
			view.Root.OnMouseDown(Touch(50, 40));
			view.Root.OnMouseMove(Touch(70, 40));
			await Assert.That(view.Presses).IsEqualTo(1);
			view.Root.OnMouseUp(Touch(70, 40));
			await Assert.That(view.Clicks).IsEqualTo(0);
			await Assert.That(view.Scroll.ScrollOffsetFromTop()).IsEqualTo(0).Within(1e-9);
		}

		private static MouseEventArgs TwoFingers(Vector2 a, Vector2 b)
			=> new MouseEventArgs(MouseButtons.Left, 0, new[] { a, b }, 0, null) { PointerType = PointerType.Touch };

		[Test]
		public async Task ASecondFingerBeforeTheGestureIsDecidedPressesAtTheStartAndNeverClicks()
		{
			View view = Make();
			int multiMoves = 0;
			view.Button.MouseMove += (s, e) => multiMoves += e.NumPositions > 1 ? 1 : 0;

			view.Root.OnMouseDown(Touch(50, 40));
			view.Root.OnMouseMove(TwoFingers(new Vector2(50, 40), new Vector2(90, 40)));
			await Assert.That(view.Presses).IsEqualTo(1);
			await Assert.That(multiMoves).IsEqualTo(1);

			view.Root.OnMouseUp(Touch(50, 40));
			await Assert.That(view.Clicks).IsEqualTo(0);
			await Assert.That(view.Root.ChildHasMouseCaptured).IsFalse();
		}

		[Test]
		public async Task TheWidgetsOwnDragReleasesItsCapture()
		{
			View view = Make();
			view.Root.OnMouseDown(Touch(50, 40));
			view.Root.OnMouseMove(Touch(70, 40));
			await Assert.That(view.Button.MouseCaptured).IsTrue();
			view.Root.OnMouseUp(Touch(70, 40));
			await Assert.That(view.Button.MouseCaptured).IsFalse();
			await Assert.That(view.Root.ChildHasMouseCaptured).IsFalse();
		}

		[Test]
		public async Task ALostReleaseIsLetGoWhenTheNextFingerLands()
		{
			// a host that dropped the pointerup must not leave the old press holding the capture
			View view = Make();
			view.Root.OnMouseDown(Touch(50, 40));
			view.Root.OnMouseMove(Touch(70, 40));
			await Assert.That(view.Button.MouseCaptured).IsTrue();

			view.Root.OnMouseDown(Touch(150, 80));
			await Assert.That(view.Button.MouseCaptured).IsFalse();
			await Assert.That(view.Clicks).IsEqualTo(0);
		}

		private sealed class PinchWidget : GuiWidget
		{
			private readonly MultiTouchGesture gesture = new MultiTouchGesture();

			public double Zoom = 1;

			public PinchWidget()
				: base(200, 100)
			{
			}

			public override void OnMouseMove(MouseEventArgs mouseEvent)
			{
				Zoom *= gesture.Update(mouseEvent)?.ZoomDelta ?? 1;
				base.OnMouseMove(mouseEvent);
			}
		}

		[Test]
		public async Task APinchReachesAWidgetThatReadsFingers()
		{
			var root = new SystemWindow(200, 100);
			var pinch = new PinchWidget();
			root.AddChild(pinch);

			root.OnMouseDown(Touch(80, 50));
			root.OnMouseMove(TwoFingers(new Vector2(80, 50), new Vector2(120, 50)));
			root.OnMouseMove(TwoFingers(new Vector2(60, 50), new Vector2(140, 50)));
			root.OnMouseUp(Touch(60, 50));

			await Assert.That(pinch.Zoom).IsEqualTo(2).Within(1e-9);
			await Assert.That(root.ChildHasMouseCaptured).IsFalse();
		}

		[Test]
		public async Task AFadedOutFloatingBarIsPannedOverButAVisibleBarStillDrags()
		{
			// a finger cannot see a faded-out bar, so a drag along the edge pans the content
			View view = Make();
			ScrollBar bar = view.Scroll.VerticalScrollBar;
			bar.Clock = () => 1_000_000;
			bar.Floating = true;
			view.Scroll.SetScrollOffsetFromTop(200);
			await Assert.That(bar.Opacity).IsEqualTo(0);
			double barX = bar.BoundsRelativeToParent.Center.X;

			view.Root.OnMouseDown(Touch(barX, 50));
			view.Root.OnMouseMove(Touch(barX, 80));
			view.Root.OnMouseUp(Touch(barX, 80));
			await Assert.That(view.Scroll.ScrollOffsetFromTop()).IsEqualTo(230).Within(1e-9);

			// a bar that is showing is the bar's drag, not a pan
			bar.Floating = false;
			barX = bar.BoundsRelativeToParent.Center.X;
			view.Scroll.SetScrollOffsetFromTop(200);
			view.Root.OnMouseDown(Touch(barX, 50));
			view.Root.OnMouseMove(Touch(barX, 80));
			view.Root.OnMouseUp(Touch(barX, 80));
			await Assert.That(view.Scroll.ScrollOffsetFromTop()).IsNotEqualTo(230);
		}
	}
}
