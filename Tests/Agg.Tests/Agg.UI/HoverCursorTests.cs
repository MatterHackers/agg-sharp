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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Collections.Generic;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A widget whose <see cref="GuiWidget.Cursor"/> depends on where the mouse is inside it - a resize bar
	/// along one edge of a panel, a tab's close button - has to reach the window when that cursor changes,
	/// not only when the mouse first enters the widget. Before this was fixed the cursor applied on entry
	/// stuck until the mouse left and came back.
	/// </summary>
	public class HoverCursorTests
	{
		/// <summary>
		/// The root of the tree; it records each cursor pushed up to it, standing in for the platform window.
		/// </summary>
		private class CursorRecorder : GuiWidget
		{
			public CursorRecorder(double width, double height)
				: base(width, height)
			{
			}

			public List<Cursors> Applied { get; } = new List<Cursors>();

			protected override void SetCursor(Cursors cursorToSet)
			{
				Applied.Add(cursorToSet);
			}
		}

		/// <summary>
		/// Shows the split cursor over its leftmost 10 units, the shape of VerticalResizeContainer's bar.
		/// </summary>
		private class EdgeBarWidget : GuiWidget
		{
			private bool overBar;

			public EdgeBarWidget(double width, double height)
				: base(width, height)
			{
			}

			public override Cursors Cursor
			{
				get => overBar ? Cursors.VSplit : Cursors.Default;
				set => base.Cursor = value;
			}

			public override void OnMouseMove(MouseEventArgs mouseEvent)
			{
				overBar = mouseEvent.Position.X < 10;
				base.OnMouseMove(mouseEvent);
			}
		}

		/// <summary>
		/// Sets its own Cursor after base.OnMouseMove has run, the shape of a widget that decides its cursor
		/// from state its move handler computes last.
		/// </summary>
		private class SetAfterMoveWidget : GuiWidget
		{
			public SetAfterMoveWidget(double width, double height)
				: base(width, height)
			{
			}

			public override void OnMouseMove(MouseEventArgs mouseEvent)
			{
				base.OnMouseMove(mouseEvent);
				Cursor = mouseEvent.Position.X < 10 ? Cursors.VSplit : Cursors.Default;
			}
		}

		private static MouseEventArgs Move(double x, double y) => new MouseEventArgs(MouseButtons.None, 0, x, y, 0);

		private static MouseEventArgs Press(double x, double y) => new MouseEventArgs(MouseButtons.Left, 1, x, y, 0);

		[Test]
		public async Task CursorChangeWhileHoveringOneWidgetReachesTheWindow()
		{
			var root = new CursorRecorder(200, 200);
			var panel = new EdgeBarWidget(100, 100);
			root.AddChild(panel);

			// enter the panel away from its bar
			root.OnMouseMove(Move(50, 50));
			await Assert.That(root.Applied[^1]).IsEqualTo(Cursors.Default);
			var countAfterEnter = root.Applied.Count;

			// moving without a cursor change must not call the platform again
			root.OnMouseMove(Move(60, 50));
			await Assert.That(root.Applied.Count).IsEqualTo(countAfterEnter);

			// slide onto the bar without leaving the panel
			root.OnMouseMove(Move(5, 50));
			await Assert.That(root.Applied.Count).IsEqualTo(countAfterEnter + 1);
			await Assert.That(root.Applied[^1]).IsEqualTo(Cursors.VSplit);

			// and back off it
			root.OnMouseMove(Move(50, 50));
			await Assert.That(root.Applied[^1]).IsEqualTo(Cursors.Default);
		}

		[Test]
		public async Task CursorChangeDuringACapturedDragReachesTheWindow()
		{
			var root = new CursorRecorder(200, 200);
			var panel = new EdgeBarWidget(100, 100);
			root.AddChild(panel);

			root.OnMouseMove(Move(50, 50));
			root.OnMouseDown(Press(50, 50));
			var countAfterPress = root.Applied.Count;

			// the press captured the mouse on the panel; a move onto its bar is still a cursor change
			root.OnMouseMove(Move(5, 50));
			await Assert.That(root.Applied.Count).IsEqualTo(countAfterPress + 1);
			await Assert.That(root.Applied[^1]).IsEqualTo(Cursors.VSplit);
		}

		[Test]
		public async Task CursorSetByCodeWhileTheMouseRestsOverTheWidgetReachesTheWindow()
		{
			var root = new CursorRecorder(200, 200);
			var panel = new GuiWidget(100, 100);
			root.AddChild(panel);

			root.OnMouseMove(Move(50, 50));
			var countAfterEnter = root.Applied.Count;

			// a picking mode starts while the mouse sits still over the panel
			panel.Cursor = Cursors.Cross;
			await Assert.That(root.Applied.Count).IsEqualTo(countAfterEnter + 1);
			await Assert.That(root.Applied[^1]).IsEqualTo(Cursors.Cross);

			// setting the same cursor again does not call the platform
			panel.Cursor = Cursors.Cross;
			await Assert.That(root.Applied.Count).IsEqualTo(countAfterEnter + 1);
		}

		[Test]
		public async Task CursorSetAfterBaseMouseMoveReachesTheWindowOnThatMove()
		{
			var root = new CursorRecorder(200, 200);
			var panel = new SetAfterMoveWidget(100, 100);
			root.AddChild(panel);

			root.OnMouseMove(Move(50, 50));
			root.OnMouseMove(Move(5, 50));
			await Assert.That(root.Applied[^1]).IsEqualTo(Cursors.VSplit);

			root.OnMouseMove(Move(50, 50));
			await Assert.That(root.Applied[^1]).IsEqualTo(Cursors.Default);
		}

		[Test]
		public async Task CursorSetOnAWidgetTheMouseIsNotOverDoesNotReachTheWindow()
		{
			var root = new CursorRecorder(200, 200);
			var hovered = new GuiWidget(50, 50);
			var other = new GuiWidget(50, 50) { Position = new VectorMath.Vector2(100, 100) };
			root.AddChild(hovered);
			root.AddChild(other);

			root.OnMouseMove(Move(10, 10));
			var countAfterEnter = root.Applied.Count;

			other.Cursor = Cursors.Hand;
			await Assert.That(root.Applied.Count).IsEqualTo(countAfterEnter);
		}

		[Test]
		public async Task CursorSetByACapturedWidgetWhileTheMouseIsOutsideItReachesTheWindow()
		{
			var root = new CursorRecorder(200, 200);
			var dragged = new GuiWidget(50, 50);
			root.AddChild(dragged);

			root.OnMouseMove(Move(10, 10));
			root.OnMouseDown(Press(10, 10));

			// a drag out of the widget that holds the capture (the node editor refusing a dropped item)
			root.OnMouseMove(Move(150, 150));
			dragged.Cursor = Cursors.No;
			await Assert.That(root.Applied[^1]).IsEqualTo(Cursors.No);

			// a captured cursor that depends on the mouse position follows moves outside the widget too
			dragged.Cursor = Cursors.Default;
			var captured = new EdgeBarWidget(50, 50) { Position = new VectorMath.Vector2(0, 100) };
			root.AddChild(captured);
			root.OnMouseUp(Press(150, 150));
			root.OnMouseMove(Move(20, 120));
			root.OnMouseDown(Press(20, 120));
			root.OnMouseMove(Move(5, 190));
			await Assert.That(root.Applied[^1]).IsEqualTo(Cursors.VSplit);
		}

		[Test]
		public async Task ReleasingACapturedDragOverAnotherWidgetShowsThatWidgetsCursor()
		{
			var root = new CursorRecorder(200, 200);
			var dragged = new GuiWidget(50, 50);
			var sibling = new GuiWidget(50, 50) { Position = new VectorMath.Vector2(100, 100), Cursor = Cursors.Hand };
			root.AddChild(dragged);
			root.AddChild(sibling);

			// hover the sibling first, so its own record says Hand is already showing
			root.OnMouseMove(Move(120, 120));
			root.OnMouseMove(Move(10, 10));
			root.OnMouseDown(Press(10, 10));
			root.OnMouseMove(Move(120, 120));
			dragged.Cursor = Cursors.No;

			root.OnMouseUp(Press(120, 120));
			await Assert.That(root.Applied[^1]).IsEqualTo(Cursors.Hand);
		}
	}
}
