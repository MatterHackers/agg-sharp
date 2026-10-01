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
using System.Collections.Generic;
using System.Linq;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Turns a finger's press, moves and release (mouse events whose <see cref="MouseEventArgs.PointerType"/> is
	/// <see cref="PointerType.Touch"/>) into what the widget tree should see, the way agg-gui's touch_emulation.rs
	/// does. A <see cref="SystemWindow"/> owns one, so every touch host gets the same gestures.
	/// </summary>
	/// <remarks>
	/// <para>The press is held back, because until the finger moves or lifts nobody knows what it is:</para>
	/// <list type="bullet">
	/// <item>A tap - lifted within <see cref="Threshold"/> of where it went down - is a press and release where it
	/// lifts, so it clicks what is under the finger then.</item>
	/// <item>A drag past the threshold pans the nearest <see cref="ScrollableWidget"/> under the press that can scroll
	/// along the drag (agg-gui's middle-drag pan, which an inner view with nothing to scroll leaves to its parent).
	/// No press is delivered, so a pan never presses or clicks anything.</item>
	/// <item>A drag nothing can scroll that way is the widget's own drag - a slider, a 3D view - pressed where the
	/// finger went down, as a touch press always used to be. agg-gui leaves such drags to widgets that take its
	/// middle button; agg's widgets drag on the left one.</item>
	/// <item>A second finger stops a pan for the rest of the gesture, and hands an undecided press to whatever reads
	/// multi-position moves, pressed where the first finger went down.</item>
	/// </list>
	/// <para>A press that was delivered is released as <see cref="MouseEventArgs.Cancelled"/>: a gesture that got past
	/// being a tap never clicks, as in agg-gui. A cancel (the platform taking the pointer) never clicks either.</para>
	/// </remarks>
	internal class TouchPressDeferral
	{
		/// <summary>How far, in device pixels, a finger travels before it stops being a possible tap - agg-gui's
		/// TOUCH_SCROLL_THRESHOLD, also in physical pixels.</summary>
		internal const double Threshold = 8;

		private enum Phase
		{
			None,
			Undecided,
			Panning,
			Pressed,
			Ignored
		}

		private readonly GuiWidget root;
		private readonly Action<MouseEventArgs> deliverDown;
		private readonly Action<MouseEventArgs> deliverMove;
		private readonly Action<MouseEventArgs> deliverUp;

		private Phase phase;
		private MouseEventArgs press;

		/// <summary>The latest touch event seen, where a held press is let go if its own release never comes.</summary>
		private MouseEventArgs latest;
		private ScrollableWidget panned;

		/// <param name="root">The window the events arrive at; the pan target is found below it.</param>
		/// <param name="deliverDown">Hands a press to the widget tree, bypassing this.</param>
		/// <param name="deliverMove">Hands a move to the widget tree, bypassing this.</param>
		/// <param name="deliverUp">Hands a release to the widget tree, bypassing this.</param>
		public TouchPressDeferral(GuiWidget root, Action<MouseEventArgs> deliverDown, Action<MouseEventArgs> deliverMove, Action<MouseEventArgs> deliverUp)
		{
			this.root = root;
			this.deliverDown = deliverDown;
			this.deliverMove = deliverMove;
			this.deliverUp = deliverUp;
		}

		public void Down(MouseEventArgs mouseEvent)
		{
			if (phase == Phase.Pressed)
			{
				// the last gesture's release was lost (a host dropped it): let its press go, so the capture it
				// holds does not swallow this one
				deliverUp(new MouseEventArgs(latest, latest.X, latest.Y) { Cancelled = true });
			}

			latest = mouseEvent;
			EndPan();
			phase = Phase.Undecided;
			press = mouseEvent;

			// the finger only moves the hover for now, so the widget it lands on can show it
			deliverMove(Hover(mouseEvent));
		}

		public void Move(MouseEventArgs mouseEvent)
		{
			latest = mouseEvent;
			switch (phase)
			{
				case Phase.Undecided:
					if (mouseEvent.NumPositions > 1)
					{
						Press();
						deliverMove(mouseEvent);
						return;
					}

					Vector2 travel = mouseEvent.Position - press.Position;
					if (travel.Length < Threshold)
					{
						deliverMove(Hover(mouseEvent));
						return;
					}

					panned = PanTarget(press.Position, travel);
					if (panned == null)
					{
						Press();
						deliverMove(mouseEvent);
						return;
					}

					phase = Phase.Panning;
					panned.BeginTouchPan();
					panned.TouchPan(travel);
					return;

				case Phase.Panning:
					if (mouseEvent.NumPositions > 1)
					{
						EndPan();
						phase = Phase.Ignored;
						return;
					}

					panned.TouchPan(mouseEvent.Position - press.Position);
					return;

				case Phase.Pressed:
					deliverMove(mouseEvent);
					return;

				case Phase.None:
					// a move with no press seen: nothing is held back, so it is plain hover
					deliverMove(mouseEvent);
					return;
			}
		}

		public void Up(MouseEventArgs mouseEvent)
		{
			Phase ending = phase;
			phase = Phase.None;
			EndPan();

			switch (ending)
			{
				case Phase.Undecided:
					if (!mouseEvent.Cancelled)
					{
						// a tap: pressed and released where the finger lifted
						deliverDown(new MouseEventArgs(mouseEvent, mouseEvent.X, mouseEvent.Y) { Cancelled = false });
						deliverUp(mouseEvent);
					}

					break;

				case Phase.Pressed:
					deliverUp(new MouseEventArgs(mouseEvent, mouseEvent.X, mouseEvent.Y) { Cancelled = true });
					break;

				case Phase.None:
					// a release with no press seen still has to reach whatever holds the capture
					deliverUp(mouseEvent);
					return;
			}

			// the finger has gone, so nothing is hovered any more - the sentinel the hosts use for "nowhere"
			deliverMove(new MouseEventArgs(MouseButtons.None, 0, -10, -10, 0));
			press = null;
		}

		/// <summary>Delivers the held-back press where the finger went down.</summary>
		private void Press()
		{
			phase = Phase.Pressed;
			deliverDown(press);
		}

		private void EndPan()
		{
			panned?.EndTouchPan();
			panned = null;
		}

		private static MouseEventArgs Hover(MouseEventArgs mouseEvent)
		{
			return new MouseEventArgs(MouseButtons.None, 0, mouseEvent.X, mouseEvent.Y, 0) { PointerType = PointerType.Touch };
		}

		/// <summary>
		/// The deepest <see cref="ScrollableWidget"/> under <paramref name="position"/> that can scroll along
		/// <paramref name="travel"/>, found down the same path a press there would be routed. Null when none can, or
		/// when the press is on a scroll bar - that is the bar's drag. A floating bar faded out is passed over: the
		/// finger cannot see it, so a drag along the view's edge pans rather than scrubbing an invisible thumb.
		/// </summary>
		private ScrollableWidget PanTarget(Vector2 position, Vector2 travel)
		{
			var scrollables = new List<ScrollableWidget>();
			GuiWidget widget = root;
			double x = position.X;
			double y = position.Y;
			while (widget != null)
			{
				if (widget is ScrollBar)
				{
					return null;
				}

				if (widget is ScrollableWidget scrollable)
				{
					scrollables.Add(scrollable);
				}

				GuiWidget hit = null;
				foreach (GuiWidget child in widget.Children.Reverse())
				{
					double childX = x;
					double childY = y;
					child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
					if (child.Visible && child.Enabled && child.CanSelect && child.PositionWithinLocalBounds(childX, childY)
						&& !(child is ScrollBar bar && bar.Opacity <= 0))
					{
						hit = child;
						x = childX;
						y = childY;
						break;
					}
				}

				widget = hit;
			}

			for (int i = scrollables.Count - 1; i >= 0; i--)
			{
				if (!scrollables[i].SuppressScroll && scrollables[i].CanTouchPan(travel))
				{
					return scrollables[i];
				}
			}

			return null;
		}
	}
}
