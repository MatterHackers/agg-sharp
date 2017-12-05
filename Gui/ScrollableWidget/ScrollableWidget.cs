/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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

namespace MatterHackers.Agg.UI
{
	public class ScrollableWidget : GuiWidget
	{
		public event EventHandler ScrollPositionChanged;

		public bool AutoScroll { get; set; }

		public bool SuppressScroll { get; set; }

		public ScrollBar VerticalScrollBar { get; private set; }

		public Vector2 TopLeftOffset
		{
			get
			{
				Vector2 topLeftOffset = new Vector2(scrollArea.BoundsRelativeToParent.Left - LocalBounds.Left - ScrollArea.Margin.Left,
					scrollArea.BoundsRelativeToParent.Top - LocalBounds.Top + ScrollArea.Margin.Top);

				return topLeftOffset;
			}

			set
			{
				if (value != TopLeftOffset)
				{
					Vector2 deltaNeeded = TopLeftOffset - value;
					scrollArea.OriginRelativeParent = scrollArea.OriginRelativeParent - deltaNeeded;
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
					scrollArea.OriginRelativeParent = value;
					scrollArea.ValidateScrollPosition();

					OnScrollPositionChanged();
				}
			}
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			// make sure children controls get to try to handle this event first
			base.OnKeyDown(keyEvent);

			if (!keyEvent.Handled)
			{
				switch (keyEvent.KeyCode)
				{
					case Keys.Down:
						ScrollPosition += new Vector2(0, 16);
						keyEvent.Handled = true;
						break;

					case Keys.PageDown:
						keyEvent.Handled = true;
						ScrollPosition += new Vector2(0, Height - 20);
						break;

					case Keys.Up:
						ScrollPosition -= new Vector2(0, 16);
						keyEvent.Handled = true;
						break;

					case Keys.PageUp:
						ScrollPosition -= new Vector2(0, Height - 20);
						keyEvent.Handled = true;
						break;
				}
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

			base.AddChild(scrollArea);
			base.AddChild(VerticalScrollBar);
			VerticalScrollBar.HAnchor = UI.HAnchor.Right;
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			if (AutoScroll)
			{
				ScrollArea.ValidateScrollPosition();
			}
			base.OnBoundsChanged(e);
		}

		public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
		{
			ScrollArea.AddChild(child, indexInChildrenList);
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

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			// let children have at the data first. They may use up the scroll
			base.OnMouseWheel(mouseEvent);

			if (AutoScroll)
			{
				Vector2 oldScrollPosition = ScrollPosition;
				ScrollPosition += new Vector2(0, -mouseEvent.WheelDelta / 10);
				if (oldScrollPosition != ScrollPosition)
				{
					mouseEvent.WheelDelta = 0;
				}
				Invalidate();
			}
		}

		public Vector2 RatioOfViewToContents0To1()
		{
			Vector2 ratio = Vector2.Zero;
			RectangleDouble boundsOfScrollableContents = ScrollArea.LocalBounds;
			boundsOfScrollableContents.Inflate(ScrollArea.Margin); // expand it by margin as that is how much it is allowed to move

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
					if (Parent != null)
					{
						currentTopLeftOffset = TopLeftOffset;
					}

					base.LocalBounds = value;

					if (Parent != null)
					{
						TopLeftOffset = currentTopLeftOffset;
					}
				}
			}
		}

		public Vector2 ScrollRatioFromTop0To1
		{
			get
			{
				RectangleDouble boundsOfScrollableContents = ScrollArea.LocalBounds;
				boundsOfScrollableContents.Inflate(ScrollArea.Margin); // expand it by margin as that is how much it is allowed to move

				double maxYMovement = boundsOfScrollableContents.Height - Height;
				double maxXMovement = Math.Max(0, boundsOfScrollableContents.Width - Width);

				double x0To1 = 0;
				if (maxXMovement != 0)
				{
					x0To1 = 1 + (TopLeftOffset.X + ScrollArea.Margin.Left) / maxXMovement;
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
				boundsOfScrollableContents.Inflate(ScrollArea.Margin); // expand it by margin as that is how much it is allowed to move

				double maxYMovement = boundsOfScrollableContents.Height - Height;
				double maxXMovement = boundsOfScrollableContents.Width - Width;

				Vector2 scrollRatio0To1 = value;
				Vector2 newTopLeftOffset;
				newTopLeftOffset.X = scrollRatio0To1.X * maxXMovement + ScrollArea.Margin.Left;
				newTopLeftOffset.Y = -(scrollRatio0To1.Y - 1) * maxYMovement;

				TopLeftOffset = newTopLeftOffset;
			}
		}
	}
}