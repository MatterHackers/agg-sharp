/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A drag of a <see cref="WindowWidget"/>'s title bar moves the window and nothing else: one screen pixel
	/// of mouse travel is one pixel of movement, and only while the button that started the drag is held.
	/// </summary>
	public class WindowWidgetTitleBarDragTests
	{
		[Test]
		public async Task TitleBarDragMovesTheWindowByExactlyTheMouseTravel()
		{
			var (systemWindow, window, mousePosition) = StartTitleBarDrag();

			var startSize = window.Size;
			var startPosition = window.Position;

			var step = new Vector2(7, -5);

			for (int stepIndex = 1; stepIndex <= 10; stepIndex++)
			{
				mousePosition += step;
				systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, mousePosition.X, mousePosition.Y, 0));

				var expectedPosition = startPosition + (step * stepIndex);

				await Assert.That(window.Position.X).IsEqualTo(expectedPosition.X).Within(0.001)
					.Because($"after {stepIndex} steps of {step} the left should be {expectedPosition.X} but was {window.Position.X}");
				await Assert.That(window.Position.Y).IsEqualTo(expectedPosition.Y).Within(0.001)
					.Because($"after {stepIndex} steps of {step} the bottom should be {expectedPosition.Y} but was {window.Position.Y}");
			}

			await Assert.That(window.Size.X).IsEqualTo(startSize.X).Within(0.001)
				.Because("dragging the title bar must not resize the window");
			await Assert.That(window.Size.Y).IsEqualTo(startSize.Y).Within(0.001)
				.Because("dragging the title bar must not resize the window");

			systemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, mousePosition.X, mousePosition.Y, 0));
		}

		// Regression: both platform sinks (WinformsEventSink, MacSystemWindow) report the pointer leaving the
		// window as a MOVE with no button at (-10, -10). The title bar took it for a drag and threw the window
		// at the corner of the screen - the same mistake the grab handles used to make, which is why the window
		// "sometimes disappeared".
		[Test]
		public async Task PointerLeavingTheWindowDoesNotMoveIt()
		{
			var (systemWindow, window, mousePosition) = StartTitleBarDrag();

			mousePosition += new Vector2(20, 20);
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, mousePosition.X, mousePosition.Y, 0));

			var draggedPosition = window.Position;

			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, -10, -10, 0));

			await Assert.That(window.Position.X).IsEqualTo(draggedPosition.X).Within(0.001)
				.Because($"the pointer leaving the window must not move it (left went from {draggedPosition.X} to {window.Position.X})");
			await Assert.That(window.Position.Y).IsEqualTo(draggedPosition.Y).Within(0.001)
				.Because($"the pointer leaving the window must not move it (bottom went from {draggedPosition.Y} to {window.Position.Y})");
		}

		// Regression: a mouse up that lands outside the platform window can be dropped before it reaches the
		// widget tree, which left the title bar believing it was still being dragged - every later hover then
		// carried the window around with the pointer, with nothing held down.
		[Test]
		public async Task HoveringAfterALostMouseUpDoesNotMoveTheWindow()
		{
			var (systemWindow, window, mousePosition) = StartTitleBarDrag();

			mousePosition += new Vector2(20, 20);
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, mousePosition.X, mousePosition.Y, 0));

			var draggedPosition = window.Position;

			// the button was let go where we could not see it, so all that arrives is a plain hover
			mousePosition += new Vector2(40, 40);
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, mousePosition.X, mousePosition.Y, 0));

			await Assert.That(window.Position.X).IsEqualTo(draggedPosition.X).Within(0.001)
				.Because($"a hover with no button held must not move the window (left went from {draggedPosition.X} to {window.Position.X})");
			await Assert.That(window.Position.Y).IsEqualTo(draggedPosition.Y).Within(0.001)
				.Because($"a hover with no button held must not move the window (bottom went from {draggedPosition.Y} to {window.Position.Y})");
		}

		[Test]
		public async Task AMoveAfterTheButtonIsReleasedDoesNotMoveTheWindow()
		{
			var (systemWindow, window, mousePosition) = StartTitleBarDrag();

			mousePosition += new Vector2(20, 20);
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, mousePosition.X, mousePosition.Y, 0));
			systemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, mousePosition.X, mousePosition.Y, 0));

			var droppedPosition = window.Position;

			// a drag of something else entirely passing over the bar
			mousePosition += new Vector2(30, 30);
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, mousePosition.X, mousePosition.Y, 0));

			await Assert.That(window.Position.X).IsEqualTo(droppedPosition.X).Within(0.001)
				.Because("the drag ended with the button, a later move is not part of it");
			await Assert.That(window.Position.Y).IsEqualTo(droppedPosition.Y).Within(0.001)
				.Because("the drag ended with the button, a later move is not part of it");
		}

		/// <summary>
		/// Builds a window on a system window and presses the middle of its title bar, returning the screen
		/// space position the mouse was pressed at.
		/// </summary>
		private static (SystemWindow systemWindow, WindowWidget window, Vector2 mousePosition) StartTitleBarDrag()
		{
			var systemWindow = new SystemWindow(800, 600);

			var window = new WindowWidget(new ThemeConfig(), new RectangleDouble(100, 100, 400, 400));
			systemWindow.AddChild(window);
			systemWindow.PerformLayout();

			var titleBar = window.TitleBar;
			var mousePosition = titleBar.TransformToScreenSpace(new Vector2(titleBar.Width / 2, titleBar.Height / 2));
			systemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, mousePosition.X, mousePosition.Y, 0));

			return (systemWindow, window, mousePosition);
		}
	}
}
