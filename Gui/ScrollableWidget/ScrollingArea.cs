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

using MatterHackers.VectorMath;
using System;

namespace MatterHackers.Agg.UI
{
	public class ScrollingArea : GuiWidget
	{
		private ScrollableWidget parentScrollableWidget;

		public ScrollingArea(ScrollableWidget parentScrollableWidget)
		{
			this.parentScrollableWidget = parentScrollableWidget;
		}

		private void CalculateChildrenBounds()
		{
			if (Children.Count > 0)
			{
				RectangleDouble boundsOfChildren = new RectangleDouble(double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
				foreach (GuiWidget widget in Children)
				{
					boundsOfChildren.ExpandToInclude(widget.BoundsRelativeToParent);
					boundsOfChildren.Left = Math.Min(boundsOfChildren.Left, widget.BoundsRelativeToParent.Left);
					boundsOfChildren.Bottom = Math.Min(boundsOfChildren.Bottom, widget.BoundsRelativeToParent.Bottom);
					boundsOfChildren.Right = Math.Max(boundsOfChildren.Right, widget.BoundsRelativeToParent.Right);
					boundsOfChildren.Top = Math.Max(boundsOfChildren.Top, widget.BoundsRelativeToParent.Top);
				}

				LocalBounds = boundsOfChildren;
			}
			else
			{
				// with nothing in it there is nothing to scroll to - keeping the bounds of content that has been
				// removed left the scroll bar showing over an empty area
				LocalBounds = new RectangleDouble(0, 0, 0, 0);
			}
		}

		public override void OnMarginChanged()
		{
			base.OnMarginChanged();
			ValidateScrollPosition();
		}

		private void RecalculateChildrenBounds(Object sender, EventArgs e)
		{
			Vector2 topLeftOffset = parentScrollableWidget.TopLeftOffset;
			bool wasFollowing = parentScrollableWidget.FollowingBottom;
			CalculateChildrenBounds();
			parentScrollableWidget.RestoreTopLeftOffset(topLeftOffset);
			parentScrollableWidget.KeepAtBottomIf(wasFollowing);
		}

		public override GuiWidget AddChild(GuiWidget child, int indexInChildrenList = -1)
		{
			child.BoundsChanged += RecalculateChildrenBounds;
			child.PositionChanged += RecalculateChildrenBounds;

			// remember the offset
			Vector2 topLeftOffset = parentScrollableWidget.TopLeftOffset;
			bool wasFollowing = parentScrollableWidget.FollowingBottom;

			base.AddChild(child, indexInChildrenList);
			CalculateChildrenBounds();

			// and restore it
			parentScrollableWidget.RestoreTopLeftOffset(topLeftOffset);
			parentScrollableWidget.KeepAtBottomIf(wasFollowing);

			return child;
		}

		public override void RemoveChild(GuiWidget childToRemove)
		{
			if (Children.Contains(childToRemove))
			{
				StopWatchingChild(childToRemove);

				// remember the offset, the way AddChild does
				Vector2 topLeftOffset = parentScrollableWidget.TopLeftOffset;
				bool wasFollowing = parentScrollableWidget.FollowingBottom;

				base.RemoveChild(childToRemove);
				CalculateChildrenBounds();

				// and restore it
				parentScrollableWidget.RestoreTopLeftOffset(topLeftOffset);
				parentScrollableWidget.KeepAtBottomIf(wasFollowing);
			}
			else
			{
				base.RemoveChild(childToRemove);
			}
		}

		public override GuiWidget RemoveChild(int index)
		{
			// remember the offset, the way AddChild does
			Vector2 topLeftOffset = parentScrollableWidget.TopLeftOffset;
			bool wasFollowing = parentScrollableWidget.FollowingBottom;

			GuiWidget removed = base.RemoveChild(index);

			if (removed != null)
			{
				StopWatchingChild(removed);
				CalculateChildrenBounds();

				// and restore it
				parentScrollableWidget.RestoreTopLeftOffset(topLeftOffset);
				parentScrollableWidget.KeepAtBottomIf(wasFollowing);
			}

			return removed;
		}

		private void StopWatchingChild(GuiWidget child)
		{
			child.BoundsChanged -= RecalculateChildrenBounds;
			child.PositionChanged -= RecalculateChildrenBounds;
		}

		private int debugRecursionCount = 0;

		/// <summary>
		/// Keeps the content from scrolling further than its margin past either edge of the view.
		/// </summary>
		/// <remarks>
		/// Every bound here is in device pixels, so the margin and padding are the device ones: the design-unit
		/// Margin and Padding put the stop margin x (scale - 1) away from the gap layout draws at 2x.
		/// </remarks>
		internal void ValidateScrollPosition()
		{
			var parent = this.Parent;
			if (parent == null)
			{
				return;
			}

			Vector2 newOrigin = OriginRelativeParent;

			Vector2 topLeftOffset = parentScrollableWidget.TopLeftOffset;

			RectangleDouble boundsWithMargin = LocalBounds;
			boundsWithMargin.Inflate(DeviceMargin);
			if (boundsWithMargin.Height < parentScrollableWidget.LocalBounds.Height)
			{
				debugRecursionCount++;
				if (debugRecursionCount < 20)
				{
					parentScrollableWidget.RestoreTopLeftOffset(new Vector2(parentScrollableWidget.TopLeftOffset.X, 0));
				}

				debugRecursionCount--;
				newOrigin.Y = OriginRelativeParent.Y;
			}
			else
			{
				if (newOrigin.Y + DeviceMargin.Top + DevicePadding.Top + LocalBounds.Top < parent.LocalBounds.Top)
				{
					newOrigin.Y = parent.LocalBounds.Top - DeviceMargin.Top - DevicePadding.Top - LocalBounds.Top;
				}
				else if (LocalBounds.Height + DeviceMargin.Height >= parent.LocalBounds.Height)
				{
					if (BoundsRelativeToParent.Bottom - DeviceMargin.Bottom > parent.LocalBounds.Bottom)
					{
						newOrigin.Y = parent.LocalBounds.Bottom - LocalBounds.Bottom + DeviceMargin.Bottom;
					}
				}
			}

			if (BoundsRelativeToParent.Left - DeviceMargin.Left > parent.LocalBounds.Left)
			{
				newOrigin.X = parent.LocalBounds.Left - LocalBounds.Left + DeviceMargin.Left;
			}
			else if (LocalBounds.Width + DeviceMargin.Width > parent.LocalBounds.Width)
			{
				if (BoundsRelativeToParent.Right + DeviceMargin.Right < parent.LocalBounds.Right)
				{
					newOrigin.X = parent.LocalBounds.Right - LocalBounds.Right - DeviceMargin.Right;
				}
			}
			else
			{
				// The content fits sideways, so there is nowhere to be scrolled to: flush left. Without this a view
				// scrolled right whose content then shrank kept it off the left edge.
				newOrigin.X = parent.LocalBounds.Left - LocalBounds.Left + DeviceMargin.Left;
			}

			if (newOrigin != OriginRelativeParent)
			{
				OriginRelativeParent = newOrigin;
			}
		}
	}
}