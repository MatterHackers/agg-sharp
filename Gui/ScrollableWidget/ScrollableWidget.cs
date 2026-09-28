/*
Copyright (c) 2026, Lars Brubaker, John Lewin
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

using MatterHackers.VectorMath;
using System;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	public class ScrollableWidget : GuiWidget
	{
		public event EventHandler ScrollPositionChanged;

		public bool AutoScroll { get; set; }

		public bool SuppressScroll { get; set; }

		/// <summary>
		/// Opt-in (off by default): while the view shows the end of its content, content growth and resizes keep it
		/// at the end - a log or chat that follows new rows. Scrolling up detaches it; scrolling back to the bottom
		/// re-attaches it. Content that fits counts as at the bottom, so it attaches the moment it starts to overflow.
		/// </summary>
		public bool StickToBottom { get; set; }

		/// <summary>True when <see cref="StickToBottom"/> is on and the view is at the end of the content (within half
		/// a pixel), so whatever is about to change the content or the view should leave it at the end.</summary>
		internal bool FollowingBottom => StickToBottom && this.ScrollOffsetFromTop() >= this.MaxScrollFromTop() - .5;

		/// <summary>Puts the view back at the end of the content if <paramref name="wasFollowing"/> (a
		/// <see cref="FollowingBottom"/> read before the change).</summary>
		internal void KeepAtBottomIf(bool wasFollowing)
		{
			if (wasFollowing)
			{
				this.SetScrollOffsetFromTop(this.MaxScrollFromTop());
			}
		}

		public ScrollBar VerticalScrollBar { get; private set; }

		private ScrollEdgeFade edgeFade;

		/// <summary>
		/// The fade that dissolves the content towards edges with more beyond them. Created on first read (none by
		/// default) with <see cref="ScrollEdgeFade.Strength"/> 0; set the strength above 0 to see it.
		/// </summary>
		public ScrollEdgeFade EdgeFade
		{
			get
			{
				if (edgeFade == null)
				{
					edgeFade = new ScrollEdgeFade(this);
					// above the content, below the bars
					base.AddChild(edgeFade, Children.IndexOf(scrollArea) + 1);
				}

				return edgeFade;
			}
		}

		/// <summary>The bar along the bottom, or null until <see cref="HorizontalScroll"/> is turned on.</summary>
		public ScrollBar HorizontalScrollBar { get; private set; }

		/// <summary>
		/// Opt-in (off by default): a horizontal scroll bar along the bottom, shown when the content is wider than the
		/// view, and shift+wheel scrolling sideways. A trackpad's sideways swipe scrolls sideways either way.
		/// </summary>
		/// <remarks>The bar is only created when this is first turned on, so a view that never asks has the same
		/// children it always had.</remarks>
		public bool HorizontalScroll
		{
			get => HorizontalScrollBar?.Show == ScrollBar.ShowState.WhenRequired || HorizontalScrollBar?.Show == ScrollBar.ShowState.Always;
			set
			{
				if (value == HorizontalScroll)
				{
					return;
				}

				if (HorizontalScrollBar == null)
				{
					HorizontalScrollBar = new ScrollBar(this, Orientation.Horizontal) { Show = ScrollBar.ShowState.Never };
					HorizontalScrollBar.VisibleChanged += (s, e) => SetScrollAreaMargin();
					HorizontalScrollBar.SizeChanged += (s, e) => SetScrollAreaMargin();
					HorizontalScrollBar.HAnchor = UI.HAnchor.Left;
					HorizontalScrollBar.VAnchor = UI.VAnchor.Bottom;
					base.AddChild(HorizontalScrollBar);
				}

				HorizontalScrollBar.Show = value ? ScrollBar.ShowState.WhenRequired : ScrollBar.ShowState.Never;
				SetScrollAreaMargin();
			}
		}

		public Vector2 TopLeftOffset
		{
			get
			{
				Vector2 topLeftOffset = new Vector2(scrollArea.BoundsRelativeToParent.Left - LocalBounds.Left - ScrollArea.DeviceMargin.Left,
					scrollArea.BoundsRelativeToParent.Top - LocalBounds.Top + ScrollArea.DeviceMargin.Top);

				return topLeftOffset;
			}

			set
			{
				if (value != TopLeftOffset)
				{
					NoteRequestedScroll(TopLeftOffset, value);
					Vector2 deltaNeeded = TopLeftOffset - value;
					scrollArea.OriginRelativeParent -= deltaNeeded;
					scrollArea.ValidateScrollPosition();

					OnScrollPositionChanged();
				}
			}
		}

		public Vector2 ScrollPosition
		{
			get
			{
				return scrollArea.OriginRelativeParent;
			}

			set
			{
				if (value != scrollArea.OriginRelativeParent)
				{
					NoteRequestedScroll(scrollArea.OriginRelativeParent, value);
					scrollArea.OriginRelativeParent = value;
					scrollArea.ValidateScrollPosition();

					OnScrollPositionChanged();
				}
			}
		}

		public Vector2 ScrollPositionFromTop
		{
			get
			{
				return scrollArea.Position + new Vector2(0, ScrollArea.Height - Height);
			}

			set
			{
				scrollArea.Position = value + new Vector2(0, Height - ScrollArea.Height);
			}
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			// make sure children controls get to try to handle this event first
			base.OnKeyDown(keyEvent);

			// check for arrow keys (but only if no modifiers are pressed)
			if (!keyEvent.Handled
				&& !keyEvent.Control
				&& !keyEvent.Alt
				&& !keyEvent.Shift)
			{
				var startingScrollPosition = ScrollPosition;
				switch (keyEvent.KeyCode)
				{
					case Keys.Down:
						ScrollPosition += new Vector2(0, 16 * GuiWidget.DeviceScale);
						break;

					case Keys.PageDown:
						ScrollPosition += new Vector2(0, Height - 20);
						break;

					case Keys.Up:
						ScrollPosition -= new Vector2(0, 16 * GuiWidget.DeviceScale);
						break;

					case Keys.PageUp:
						ScrollPosition -= new Vector2(0, Height - 20);
						break;
				}

				// we only handled the key if it resulted in the area scrolling
				if(startingScrollPosition != ScrollPosition)
				{
					keyEvent.Handled = true;
				}
			}
		}

		/// <summary>Count scrolls asked for from outside the internal keep-the-offset restores, per axis; a resize
		/// compares them before and after to tell whether something scrolled that axis on purpose while the bounds
		/// were changing. Per axis because MatterCAD's ThemedHorizontalScrollBar resets only X from BoundsChanged
		/// (ScrollPosition = (left, ScrollPosition.Y)) and that must not cost the view its vertical position.</summary>
		private int requestedScrollsX;

		private int requestedScrollsY;

		private bool restoringOffset;

		/// <summary>Puts the offset back after the content or view changed under it - bookkeeping, not a scroll
		/// request, so it does not count as one for <see cref="LocalBounds"/>.</summary>
		internal void RestoreTopLeftOffset(Vector2 topLeftOffset)
		{
			bool wasRestoring = restoringOffset;
			restoringOffset = true;
			try
			{
				TopLeftOffset = topLeftOffset;
			}
			finally
			{
				restoringOffset = wasRestoring;
			}
		}

		/// <summary>Counts a scroll from <paramref name="from"/> to <paramref name="to"/> (either both
		/// TopLeftOffsets or both ScrollPositions - only which axes differ matters) against the axes it moves.</summary>
		private void NoteRequestedScroll(Vector2 from, Vector2 to)
		{
			if (restoringOffset)
			{
				return;
			}

			if (from.X != to.X)
			{
				requestedScrollsX++;
			}

			if (from.Y != to.Y)
			{
				requestedScrollsY++;
			}
		}

		private void OnScrollPositionChanged()
		{
			ScrollPositionChanged?.Invoke(this, null);
		}

		public ScrollingArea ScrollArea
		{
			get { return scrollArea; }
		}

		public void AddChildToBackground(GuiWidget widgetToAdd, int indexToAddAt = 0)
		{
			base.AddChild(widgetToAdd, indexToAddAt);
		}

		private ScrollingArea scrollArea;

		public ScrollableWidget(bool autoScroll = false)
			: this(0, 0, autoScroll)
		{
		}

		public ScrollableWidget(double width, double height, bool autoScroll = false)
			: base(width, height)
		{
			scrollArea = new ScrollingArea(this);
			scrollArea.HAnchor = UI.HAnchor.Fit;
			AutoScroll = autoScroll;
			VerticalScrollBar = new ScrollBar(this);

			VerticalScrollBar.VisibleChanged += (s, e) =>
			{
				SetScrollAreaMargin();
			};

			VerticalScrollBar.SizeChanged += (s, e) =>
			{
				SetScrollAreaMargin();
			};

			// through the same helper the VisibleChanged and SizeChanged handlers use - Margin is in design units
			// and layout multiplies it by DeviceScale, so assigning the bar's already scaled Width here made the
			// gap twice as wide as the bar on any display with a scale above 1
			SetScrollAreaMargin();

			base.AddChild(scrollArea);
			base.AddChild(VerticalScrollBar);
			VerticalScrollBar.HAnchor = UI.HAnchor.Right;
		}

		/// <summary>Steps the scroll area in from each bar that takes room beside it (a floating bar takes none).</summary>
		internal void SetScrollAreaMargin()
		{
			if (VerticalScrollBar.ReservesSpace)
			{
				scrollArea.Margin = scrollArea.Margin.Clone(right: VerticalScrollBar.Width / DeviceScale);
			}
			else
			{
				scrollArea.Margin = scrollArea.Margin.Clone(right: 0);
			}

			if (HorizontalScrollBar != null)
			{
				scrollArea.Margin = scrollArea.Margin.Clone(bottom: HorizontalScrollBar.ReservesSpace ? HorizontalScrollBar.Height / DeviceScale : 0);
			}
		}

		/// <summary>
		/// What HAnchor/VAnchor Fit sizes us to: the content we scroll, measured from our own origin.
		/// </summary>
		/// <remarks>
		/// Our children are not a fair measure of us. The scrolling area is deliberately displaced by the scroll
		/// position, so a view showing the top of taller content encloses its rows below its own origin and the
		/// fitted rect came out with a negative bottom (B:-300 T:200 for 500 of content in a 200 tall view). A
		/// later Height assignment only moves Top, so that bottom stayed negative for good, and the scroll bar -
		/// which is laid out at the local origin - was drawn a whole scroll offset clear of the view it scrolls.
		/// The area's margin counts as content because that is how far the content is allowed to move. It is taken
		/// from DeviceMarginAndBorder (device pixels) because bounds are in device pixels, the same reason
		/// <see cref="ScrollingArea.ValidateScrollPosition"/> and <see cref="RatioOfViewToContents0To1"/> use
		/// DeviceMargin.
		/// Background children (AddChildToBackground) are deliberately excluded from Fit sizing - they decorate the
		/// view, they are not content to be enclosed.
		/// </remarks>
		public override RectangleDouble GetMinimumBoundsToEncloseChildren(bool considerChildAnchor = false)
		{
			RectangleDouble contentBounds = ScrollArea.LocalBounds;
			contentBounds.Inflate(ScrollArea.DeviceMarginAndBorder);

			return new RectangleDouble(0,
				0,
				contentBounds.Width + DevicePadding.Width,
				contentBounds.Height + DevicePadding.Height);
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			if (AutoScroll)
			{
				ScrollArea.ValidateScrollPosition();
			}
			base.OnBoundsChanged(e);
		}

		public override GuiWidget AddChild(GuiWidget child, int indexInChildrenList = -1)
		{
			return ScrollArea.AddChild(child, indexInChildrenList);
		}

		private bool mouseDownOnScrollArea = false;
		private double mouseDownY = 0;
		private double scrollOnDownY = 0;

		private static bool ScrollWithMouse(GuiWidget widgetToCheck)
		{
			if (widgetToCheck as TextEditWidget != null)
			{
				return false;
			}

			if (widgetToCheck.UnderMouseState == UI.UnderMouseState.UnderMouseNotFirst)
			{
				// If we are not the first widget clicked on let's see if there is a child that is a scroll widget.
				// If there is let it have this move and not us.
				foreach (GuiWidget child in widgetToCheck.Children)
				{
					if (child.UnderMouseState != UI.UnderMouseState.NotUnderMouse)
					{
						ScrollableWidget childScroll = child as ScrollableWidget;
						if (childScroll != null)
						{
							return false;
						}
						else
						{
							return ScrollWithMouse(child);
						}
					}
				}
			}

			return true;
		}

		bool mouseEventIsTouchScrolling = false;
		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			mouseEventIsTouchScrolling = false;
			mouseDownY = mouseEvent.Y;
			mouseDownOnScrollArea = true;
			scrollOnDownY = ScrollPosition.Y;
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if(SuppressScroll)
			{
				return;
			}

			if (mouseDownOnScrollArea
				&& GuiWidget.TouchScreenMode
				&& ScrollWithMouse(this))
			{
				ScrollPosition = new Vector2(ScrollPosition.X, scrollOnDownY - (mouseDownY - mouseEvent.Y));
			}

			if (ScrollPosition.Y < scrollOnDownY - 10
				|| ScrollPosition.Y > scrollOnDownY + 10)
			{
				// If touch is enabled and we've scrolled more than 10 pixels, update to suppress child clicks
				mouseEventIsTouchScrolling = true;
			}

			// FIXME: BUG: This is a hack to fix the scroll bar position being wrong sometimes.
			// I think the problem may be that the layout manager is not trying to hold the scroll bar to the right on a
			// visibility change. The scroll bar is becoming visible and not doing a layout. There is evidence this is the
			// problem, but not proof.
			if (VerticalScrollBar.Visible
				&& VerticalScrollBar.BoundsRelativeToParent.Left < this.Width / 2)
            {
				// Make a layout event happen to fix the scroll bar
				var width = this.Width;
				this.Width = this.Width + 1;
				this.Width = width;
            }

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			mouseDownOnScrollArea = false;
			if (mouseEventIsTouchScrolling 
				&& PositionWithinLocalBounds(mouseEvent.Position.X, mouseEvent.Position.Y))
			{
				// Suppress child clicks by sending MouseUp coordinates that are outside our bounds
				base.OnMouseUp(new MouseEventArgs(mouseEvent, double.MinValue, double.MinValue));
			}
			else
			{
				base.OnMouseUp(mouseEvent);
			}
		}

		/// <summary>
		/// True when the content is wider than the view, so there is something off the sides to scroll to. A
		/// panel with nothing hidden sideways must leave <see cref="MouseEventArgs.WheelDeltaX"/> alone.
		/// </summary>
		/// <remarks>
		/// Measured exactly the way <see cref="ScrollingArea.ValidateScrollPosition"/> decides whether to clamp,
		/// so "we can scroll" and "the scroll will be allowed to stand" can never disagree.
		/// </remarks>
		/// <summary>
		/// How much more than the view the content has to be before it counts as overflowing. Content sized to
		/// exactly fit - a window fitted to it - lands a rounding error either side of the view (1e-13 on the
		/// GUI demo's Mobile Keyboard), which is not something to show a scroll bar for.
		/// </summary>
		private const double OverflowTolerance = 1e-6;

		private bool HasHorizontalOverflow => ScrollArea.LocalBounds.Width + ScrollArea.DeviceMargin.Width > LocalBounds.Width + OverflowTolerance;

		/// <summary>
		/// True when the content is taller than the view, so a vertical scroll bar has somewhere to scroll to.
		/// </summary>
		/// <remarks>
		/// Measured the way <see cref="HasHorizontalOverflow"/> is - the margin is part of how far the content is
		/// allowed to move, so it counts as content the same way <see cref="RatioOfViewToContents0To1"/> counts it.
		/// </remarks>
		internal bool HasVerticalOverflow => ScrollArea.LocalBounds.Height + ScrollArea.DeviceMargin.Height > LocalBounds.Height + OverflowTolerance;

		/// <summary>
		/// What one pixel of <c>WheelDelta / 5</c> is worth, for both axes of <paramref name="mouseEvent"/>.
		/// </summary>
		/// <remarks>
		/// DPI has exactly one owner per kind of scroll, and this is where that is decided.
		/// <list type="bullet">
		/// <item>A <see cref="MouseEventArgs.WheelDeltaIsPreciseScroll"/> delta (a trackpad) is a physical
		/// distance the platform already converted into device pixels using the scale of the display the
		/// window is on. It is passed through untouched: scaling it again is scaling DPI twice, which is what
		/// made a Retina trackpad scroll roughly twice the finger travel.</item>
		/// <item>A wheel detent carries no distance - Win32's 120 means "one click" - so the size comes from
		/// here. <see cref="GuiWidget.DeviceScale"/> is how much bigger than its design size this UI is being
		/// drawn, so a click stays worth the same number of the lines it is scrolling past.</item>
		/// </list>
		/// Both axes get the same answer, so a diagonal gesture keeps its angle.
		/// </remarks>
		private static double WheelScale(MouseEventArgs mouseEvent)
		{
			return mouseEvent.WheelDeltaIsPreciseScroll ? 1 : GuiWidget.DeviceScale;
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			// let children have at the data first. They may use up the scroll
			base.OnMouseWheel(mouseEvent);

			if (AutoScroll)
			{
				double scrollScale = WheelScale(mouseEvent);

				// Shift turns a wheel sideways, where the view has opted in to horizontal scrolling and there is
				// something off the sides (otherwise the wheel is left to scroll as it always did). The wheel
				// turning down (a negative delta) moves the view right, as it moves the view down.
				if (HorizontalScroll
					&& mouseEvent.WheelDeltaX == 0
					&& HasHorizontalOverflow
					&& Keyboard.IsKeyDown(Keys.Shift))
				{
					mouseEvent.WheelDeltaX = mouseEvent.WheelDelta;
					mouseEvent.WheelDelta = 0;
				}

				Vector2 oldScrollPosition = ScrollPosition;
				ScrollPosition += new Vector2(0, -mouseEvent.WheelDelta / 5 * scrollScale);
				if (oldScrollPosition != ScrollPosition)
				{
					mouseEvent.WheelDelta = 0;
				}

				// A trackpad's sideways component scrolls the container the pointer is over, the way it does
				// natively. ScrollPosition is where the content sits, so adding moves the content right - which is
				// what a positive WheelDeltaX (fingers moving right) asks for, and it is why this does not negate
				// the way the wheel above does: agg's Y grows upwards but its X grows the same way the gesture does.
				if (mouseEvent.WheelDeltaX != 0
					&& HasHorizontalOverflow)
				{
					oldScrollPosition = ScrollPosition;
					ScrollPosition += new Vector2(mouseEvent.WheelDeltaX / 5 * scrollScale, 0);

					// only taken if it actually moved - at either end of the travel the gesture is left for an
					// ancestor that may still have somewhere to go
					if (oldScrollPosition != ScrollPosition)
					{
						mouseEvent.WheelDeltaX = 0;
					}
				}

				Invalidate();
			}
		}

		public Vector2 RatioOfViewToContents0To1()
		{
			Vector2 ratio = Vector2.Zero;
			RectangleDouble boundsOfScrollableContents = ScrollArea.LocalBounds;
			boundsOfScrollableContents.Inflate(ScrollArea.DeviceMargin); // expand it by margin as that is how much it is allowed to move

			if (boundsOfScrollableContents.Width > 0)
			{
				ratio.X = Math.Max(0, Math.Min(1, Width / boundsOfScrollableContents.Width));
			}
			if (boundsOfScrollableContents.Height > 0)
			{
				ratio.Y = Math.Max(0, Math.Min(1, Height / boundsOfScrollableContents.Height));
			}

			return ratio;
		}

		public override RectangleDouble LocalBounds
		{
			set
			{
				if (value != LocalBounds)
				{
					Vector2 currentTopLeftOffset = new Vector2();
					bool wasFollowing = false;
					if (Parent != null)
					{
						currentTopLeftOffset = TopLeftOffset;
						wasFollowing = FollowingBottom;
					}

					int xScrollsBefore = requestedScrollsX;
					int yScrollsBefore = requestedScrollsY;
					base.LocalBounds = value;

					// Keep the offset the view had - except on an axis something (a BoundsChanged handler, say)
					// scrolled on purpose while the bounds changed; restoring over that silently undid it.
					if (Parent != null)
					{
						bool keepX = requestedScrollsX == xScrollsBefore;
						bool keepY = requestedScrollsY == yScrollsBefore;
						Vector2 now = TopLeftOffset;
						RestoreTopLeftOffset(new Vector2(keepX ? currentTopLeftOffset.X : now.X, keepY ? currentTopLeftOffset.Y : now.Y));
						KeepAtBottomIf(wasFollowing && keepY);
					}
				}
			}
		}

		public Vector2 ScrollRatioFromTop0To1
		{
			get
			{
				RectangleDouble boundsOfScrollableContents = ScrollArea.LocalBounds;
				boundsOfScrollableContents.Inflate(ScrollArea.DeviceMargin); // expand it by margin as that is how much it is allowed to move

				double maxYMovement = boundsOfScrollableContents.Height - Height;
				double maxXMovement = Math.Max(0, boundsOfScrollableContents.Width - Width);

				double x0To1 = 0;
				if (maxXMovement != 0)
				{
					x0To1 = 1 + (TopLeftOffset.X + ScrollArea.DeviceMargin.Left) / maxXMovement;
				}

				double y0To1 = 0;
				if (maxYMovement != 0)
				{
					y0To1 = 1 - TopLeftOffset.Y / maxYMovement;
				}

				Vector2 scrollRatio0To1 = new Vector2(Math.Min(1, Math.Max(0, x0To1)), Math.Min(1, Math.Max(0, y0To1)));

				return scrollRatio0To1;
			}

			set
			{
				RectangleDouble boundsOfScrollableContents = ScrollArea.LocalBounds;
				boundsOfScrollableContents.Inflate(ScrollArea.DeviceMargin); // expand it by margin as that is how much it is allowed to move

				double maxYMovement = boundsOfScrollableContents.Height - Height;
				double maxXMovement = boundsOfScrollableContents.Width - Width;

				Vector2 scrollRatio0To1 = value;
				Vector2 newTopLeftOffset;
				newTopLeftOffset.X = scrollRatio0To1.X * maxXMovement + ScrollArea.DeviceMargin.Left;
				newTopLeftOffset.Y = -(scrollRatio0To1.Y - 1) * maxYMovement;

				TopLeftOffset = newTopLeftOffset;
			}
		}

		public enum ScrollAmount
		{
			Minimum,
			Center,
		}

		public void ScrollIntoView(GuiWidget widget, ScrollAmount scrollAmount = ScrollAmount.Minimum)
		{
			if (this.Descendants().Contains(widget))
			{
				var clippedBounds = widget.ClippedOnScreenBounds();
				var screenBounds = widget.TransformToScreenSpace(widget.LocalBounds);

				if (clippedBounds.Height != screenBounds.Height)
				{
					if (scrollAmount == ScrollAmount.Center)
					{
						// Move the content by the distance from the widget's centre to this view's centre. Setting the
						// scroll to minus the centre only worked out for a widget already near the view's bottom.
						var scrollSpace = widget.TransformToParentSpace(this, widget.LocalBounds);
						this.ScrollPosition = new Vector2(0, this.ScrollPosition.Y + this.LocalBounds.Center.Y - scrollSpace.Center.Y);
					}
					else
					{
						// do the minimum scroll that will put the widget on screen
						var bounds = this.LocalBounds;
						var scrollSpace = widget.TransformToParentSpace(this, widget.LocalBounds);
						// are we above or below
						if (scrollSpace.Top >= bounds.Top)
						{
							// the widget is clipped on the top
							// lower it
							this.ScrollPosition = new Vector2(0, this.ScrollPosition.Y + bounds.Top - scrollSpace.Top);
						}
						else if (scrollSpace.Bottom <= bounds.Bottom)
						{
							// the widget is clipped on the top
							// lower it
							this.ScrollPosition = new Vector2(0, this.ScrollPosition.Y + bounds.Bottom - scrollSpace.Bottom);
						}
					}
				}
			}
		}
	}
}