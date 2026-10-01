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

using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>What <see cref="BrowserTouchPoints.Update"/> tells the dispatcher to do with a pointer event.</summary>
	public enum BrowserTouchAction
	{
		/// <summary>Hand it to <see cref="BrowserSystemWindow.EnqueuePointerEvent"/> as a mouse event, as before.</summary>
		Forward,

		/// <summary>Drop it: a finger other than the first, with nothing new to say.</summary>
		Swallow,

		/// <summary>Queue one mouse move carrying every finger (<see cref="BrowserTouchPoints.Positions"/>).</summary>
		MoveWithAllTouches,
	}

	/// <summary>
	/// Folds the browser's one-event-per-finger touch pointers into agg's one mouse whose moves carry every
	/// finger in <see cref="MouseEventArgs.NumPositions"/> - what ScenePanZoom, the Lion view and
	/// <see cref="MultiTouchGesture"/> read.
	/// </summary>
	/// <remarks>
	/// The first finger down is the mouse: its down and up are forwarded as the button press, so agg's single
	/// capture never sees a second press it has no button for. The other fingers only ever show up as extra
	/// positions on a move, primary first. Mouse and pen pointers pass straight through. Pure - no JS - so the
	/// desktop suite runs it.
	/// </remarks>
	public class BrowserTouchPoints
	{
		// Insertion order is landing order, which keeps the primary finger at index 0.
		private readonly List<(int Id, Vector2 Position)> touches = new List<(int Id, Vector2 Position)>();

		/// <summary>How many touch pointers are down.</summary>
		public int Count => this.touches.Count;

		/// <summary>Every finger down, the primary first.</summary>
		public Vector2[] Positions => this.touches.Select(t => t.Position).ToArray();

		/// <summary>
		/// Queues on <paramref name="window"/> the <see cref="BrowserTouchAction.MoveWithAllTouches"/> move: the
		/// left button held (the primary finger's press) and every finger's position.
		/// </summary>
		public void EnqueueMove(BrowserSystemWindow window)
		{
			if (this.touches.Count == 0 || !window.ShouldAcceptInput())
			{
				return;
			}

			window.Enqueue(BrowserInputEvent.Mouse(
				BrowserInputEventKind.MouseMove,
				new MouseEventArgs(MouseButtons.Left, 0, this.Positions, 0, null) { PointerType = PointerType.Touch },
				window.modifierState.DownStateKeys));
		}

		/// <summary>Tracks one pointer event and says what agg should see of it.</summary>
		/// <param name="type">The DOM event type.</param>
		/// <param name="pointerType">"touch", "mouse" or "pen"; only touch is tracked.</param>
		/// <param name="aggPosition">Where the pointer is, already in agg's space (<see cref="BrowserPointer.ToAggPosition"/>).</param>
		public BrowserTouchAction Update(string type, int pointerId, string pointerType, Vector2 aggPosition)
		{
			if (pointerType != "touch")
			{
				return BrowserTouchAction.Forward;
			}

			int index = this.touches.FindIndex(t => t.Id == pointerId);
			switch (type)
			{
				case "pointerdown":
					if (index < 0)
					{
						this.touches.Add((pointerId, aggPosition));
						index = this.touches.Count - 1;
					}

					return index == 0 ? BrowserTouchAction.Forward : BrowserTouchAction.MoveWithAllTouches;

				case "pointermove":
					if (index < 0)
					{
						// A finger left behind when the primary lifted.
						return BrowserTouchAction.Swallow;
					}

					this.touches[index] = (pointerId, aggPosition);
					if (this.touches.Count >= 2)
					{
						return BrowserTouchAction.MoveWithAllTouches;
					}

					return index == 0 ? BrowserTouchAction.Forward : BrowserTouchAction.Swallow;

				case "pointerup":
				case "pointercancel":
					if (index < 0)
					{
						return BrowserTouchAction.Swallow;
					}

					this.touches.RemoveAt(index);
					if (index == 0)
					{
						// The mouse's own finger lifting is the button up. Any finger left behind is ignored
						// until it lifts too: agg has no button held for it to drag.
						this.touches.Clear();
						return BrowserTouchAction.Forward;
					}

					// Another finger lifting drops the gesture to fewer positions - agg hears of it on a move.
					return BrowserTouchAction.MoveWithAllTouches;

				default:
					return BrowserTouchAction.Forward;
			}
		}
	}
}
