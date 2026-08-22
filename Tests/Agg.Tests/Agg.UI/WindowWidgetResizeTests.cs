/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A drag of one of a <see cref="WindowWidget"/>'s eight grab handles has to track the mouse exactly:
	/// each screen pixel of mouse travel is one pixel of size (and, for the left and bottom handles, one
	/// pixel of movement), and only while the button that started the drag is still down.
	/// </summary>
	public class WindowWidgetResizeTests
	{
		private const int StepCount = 10;
		private const int StepSize = 5;

		[Test]
		public async Task BottomRightGrabTracksMouseInScreenSpace()
		{
			// Down and to the right: the right edge follows the mouse out, the bottom edge follows it down,
			// so both dimensions grow and the window's Position drops with the bottom edge.
			await AssertDragTracksMouse(
				HAnchor.Right,
				VAnchor.Bottom,
				mouseStep: new Vector2(StepSize, -StepSize),
				expectedSizeStep: new Vector2(StepSize, StepSize),
				expectedPositionStep: new Vector2(0, -StepSize));
		}

		[Test]
		public async Task TopLeftGrabTracksMouseInScreenSpace()
		{
			// Up and to the left: the left edge follows the mouse out (so Position moves with it) and the
			// top edge follows it up.
			await AssertDragTracksMouse(
				HAnchor.Left,
				VAnchor.Top,
				mouseStep: new Vector2(-StepSize, StepSize),
				expectedSizeStep: new Vector2(StepSize, StepSize),
				expectedPositionStep: new Vector2(-StepSize, 0));
		}

		[Test]
		public async Task BottomLeftGrabTracksMouseInScreenSpace()
		{
			await AssertDragTracksMouse(
				HAnchor.Left,
				VAnchor.Bottom,
				mouseStep: new Vector2(-StepSize, -StepSize),
				expectedSizeStep: new Vector2(StepSize, StepSize),
				expectedPositionStep: new Vector2(-StepSize, -StepSize));
		}

		[Test]
		public async Task TopRightGrabTracksMouseInScreenSpace()
		{
			await AssertDragTracksMouse(
				HAnchor.Right,
				VAnchor.Top,
				mouseStep: new Vector2(StepSize, StepSize),
				expectedSizeStep: new Vector2(StepSize, StepSize),
				expectedPositionStep: new Vector2(0, 0));
		}

		[Test]
		public async Task LeftGrabTracksMouseInScreenSpace()
		{
			await AssertDragTracksMouse(
				HAnchor.Left,
				VAnchor.Stretch,
				mouseStep: new Vector2(-StepSize, 0),
				expectedSizeStep: new Vector2(StepSize, 0),
				expectedPositionStep: new Vector2(-StepSize, 0));
		}

		[Test]
		public async Task BottomGrabTracksMouseInScreenSpace()
		{
			await AssertDragTracksMouse(
				HAnchor.Stretch,
				VAnchor.Bottom,
				mouseStep: new Vector2(0, -StepSize),
				expectedSizeStep: new Vector2(0, StepSize),
				expectedPositionStep: new Vector2(0, -StepSize));
		}

		[Test]
		public async Task RightGrabTracksMouseInScreenSpace()
		{
			await AssertDragTracksMouse(
				HAnchor.Right,
				VAnchor.Stretch,
				mouseStep: new Vector2(StepSize, 0),
				expectedSizeStep: new Vector2(StepSize, 0),
				expectedPositionStep: new Vector2(0, 0));
		}

		[Test]
		public async Task TopGrabTracksMouseInScreenSpace()
		{
			await AssertDragTracksMouse(
				HAnchor.Stretch,
				VAnchor.Top,
				mouseStep: new Vector2(0, StepSize),
				expectedSizeStep: new Vector2(0, StepSize),
				expectedPositionStep: new Vector2(0, 0));
		}

		// Regression: the window jumped to a sliver and then chased the pointer around. Both platform sinks
		// (WinformsEventSink, MacSystemWindow) report "the pointer is nowhere near me" as a mouse MOVE with no
		// button at (-10, -10), and the grab handle took that for a drag of the whole window's width - it
		// resized to its minimum, then snapped back the moment a real move arrived.
		[Test]
		public async Task PointerLeavingTheWindowDoesNotResizeIt()
		{
			var (systemWindow, window, mousePosition) = StartDrag(HAnchor.Right, VAnchor.Bottom);

			mousePosition += new Vector2(StepSize, -StepSize);
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, mousePosition.X, mousePosition.Y, 0));

			var draggedSize = window.Size;
			var draggedPosition = window.Position;

			// what a platform sink sends when the pointer leaves the window
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, -10, -10, 0));

			await Assert.That(window.Size.X).IsEqualTo(draggedSize.X).Within(0.001)
				.Because($"the pointer leaving the window must not resize it (width went from {draggedSize.X} to {window.Size.X})");
			await Assert.That(window.Size.Y).IsEqualTo(draggedSize.Y).Within(0.001)
				.Because($"the pointer leaving the window must not resize it (height went from {draggedSize.Y} to {window.Size.Y})");
			await Assert.That(window.Position.Y).IsEqualTo(draggedPosition.Y).Within(0.001)
				.Because("the pointer leaving the window must not move it");
		}

		// Regression: a mouse up that lands outside the platform window can be dropped before it ever reaches
		// the widget tree (MacSystemWindow discards events outside its view), which left the handle believing
		// it was still being dragged. Every later pointer move - no button held - dragged the window with it.
		[Test]
		public async Task HoveringAfterALostMouseUpDoesNotResizeTheWindow()
		{
			var (systemWindow, window, mousePosition) = StartDrag(HAnchor.Right, VAnchor.Bottom);

			mousePosition += new Vector2(StepSize, -StepSize);
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, mousePosition.X, mousePosition.Y, 0));

			var draggedSize = window.Size;
			var draggedPosition = window.Position;

			// the button was let go where we could not see it, so all that arrives is a plain hover
			mousePosition += new Vector2(40, 40);
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, mousePosition.X, mousePosition.Y, 0));

			await Assert.That(window.Size.X).IsEqualTo(draggedSize.X).Within(0.001)
				.Because($"a hover with no button held must not resize the window (width went from {draggedSize.X} to {window.Size.X})");
			await Assert.That(window.Size.Y).IsEqualTo(draggedSize.Y).Within(0.001)
				.Because($"a hover with no button held must not resize the window (height went from {draggedSize.Y} to {window.Size.Y})");
			await Assert.That(window.Position.Y).IsEqualTo(draggedPosition.Y).Within(0.001)
				.Because("a hover with no button held must not move the window");
		}

		// The minimum size clamp has to stop the edge being dragged, not slide the window: the left handle
		// derives the window's new left from the width it actually got, so dragging it right past the minimum
		// leaves the right edge exactly where it was.
		[Test]
		public async Task LeftGrabDraggedPastTheMinimumWidthLeavesTheRightEdgeAlone()
		{
			var (systemWindow, window, mousePosition) = StartDrag(HAnchor.Left, VAnchor.Stretch);

			var startRight = window.Position.X + window.Size.X;

			// far further right than the window is wide
			mousePosition += new Vector2(window.Size.X * 2, 0);
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, mousePosition.X, mousePosition.Y, 0));

			await Assert.That(window.Size.X).IsEqualTo(window.MinimumSize.X).Within(0.001)
				.Because("the window should stop at its minimum width");
			await Assert.That(window.Position.X + window.Size.X).IsEqualTo(startRight).Within(0.001)
				.Because($"the edge that is not being dragged must not move (right went from {startRight} to {window.Position.X + window.Size.X})");
		}

		/// <summary>
		/// Builds a window on a system window and presses the middle of the grab handle with the given
		/// anchors, returning the screen space position the mouse was pressed at.
		/// </summary>
		private static (SystemWindow systemWindow, WindowWidget window, Vector2 mousePosition) StartDrag(HAnchor hAnchor, VAnchor vAnchor)
		{
			var systemWindow = new SystemWindow(800, 600);

			var window = new WindowWidget(new ThemeConfig(), new RectangleDouble(100, 100, 400, 400));
			systemWindow.AddChild(window);
			systemWindow.PerformLayout();

			var grab = window.Children.OfType<GrabControl>()
				.First(control => control.HAnchor == hAnchor && control.VAnchor == vAnchor);

			var mousePosition = grab.TransformToScreenSpace(new Vector2(grab.Width / 2, grab.Height / 2));
			systemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, mousePosition.X, mousePosition.Y, 0));

			return (systemWindow, window, mousePosition);
		}

		/// <summary>
		/// Presses the grab handle with the given anchors and walks the mouse <see cref="StepCount"/> steps
		/// of <paramref name="mouseStep"/> screen pixels, asserting after every step that the window's size
		/// and position have moved exactly one step further - never backwards, never twice as far.
		/// </summary>
		private static async Task AssertDragTracksMouse(HAnchor hAnchor,
			VAnchor vAnchor,
			Vector2 mouseStep,
			Vector2 expectedSizeStep,
			Vector2 expectedPositionStep)
		{
			var (systemWindow, window, mousePosition) = StartDrag(hAnchor, vAnchor);

			var startSize = window.Size;
			var startPosition = window.Position;

			for (int step = 1; step <= StepCount; step++)
			{
				mousePosition += mouseStep;
				systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, mousePosition.X, mousePosition.Y, 0));

				var expectedSize = startSize + expectedSizeStep * step;
				var expectedPosition = startPosition + expectedPositionStep * step;

				await Assert.That(window.Size.X).IsEqualTo(expectedSize.X).Within(1)
					.Because($"after {step} steps of {mouseStep} the width should be {expectedSize.X} but was {window.Size.X}");
				await Assert.That(window.Size.Y).IsEqualTo(expectedSize.Y).Within(1)
					.Because($"after {step} steps of {mouseStep} the height should be {expectedSize.Y} but was {window.Size.Y}");
				await Assert.That(window.Position.X).IsEqualTo(expectedPosition.X).Within(1)
					.Because($"after {step} steps of {mouseStep} the left should be {expectedPosition.X} but was {window.Position.X}");
				await Assert.That(window.Position.Y).IsEqualTo(expectedPosition.Y).Within(1)
					.Because($"after {step} steps of {mouseStep} the bottom should be {expectedPosition.Y} but was {window.Position.Y}");
			}

			systemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, mousePosition.X, mousePosition.Y, 0));
		}
	}
}
