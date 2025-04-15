﻿/*
Copyright (c) 2020, Lars Brubaker
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

using System;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class LayoutEngineSimpleAlign : LayoutEngine
	{
		public override void InitLayout()
		{
			base.InitLayout();
		}

		public override void Layout(LayoutEventArgs layoutEventArgs)
		{
			GuiWidget parent = layoutEventArgs.ParentWidget;
			if (parent != null)
			{
				using (parent.LayoutLock())
				{
					// if we didn't specify a child than anchor all the children
					if (layoutEventArgs.ChildWidget == null)
					{
						foreach (GuiWidget child in parent.Children)
						{
							if (parent.HasBeenClosed)
							{
								return;
							}

							if (child.Visible == false)
							{
								continue;
							}

							ApplyHAnchorToChild(parent, child);
							ApplyVAnchorToChild(parent, child);
						}
					}
					else
					{
						ApplyHAnchorToChild(parent, layoutEventArgs.ChildWidget);
						ApplyVAnchorToChild(parent, layoutEventArgs.ChildWidget);
					}

					// make sure we fit to the children after anchoring
					bool parentChangedSize = false;
					DoFitToChildrenHorizontal(parent, ref parentChangedSize);
					DoFitToChildrenVertical(parent, ref parentChangedSize);

					// if we changed size again, than try one more time to anchor the children
					if (parentChangedSize)
					{
						foreach (GuiWidget child in parent.Children)
						{
							if (parent.HasBeenClosed)
							{
								return;
							}

							if (child.Visible == false)
							{
								continue;
							}

							ApplyHAnchorToChild(parent, child);
							ApplyVAnchorToChild(parent, child);
						}
					}
				}
			}
		}

		protected virtual void ApplyVAnchorToChild(GuiWidget parent, GuiWidget child)
		{
			if (child.Parent != parent)
			{
				throw new Exception("All children should have their parent set the parent they have.");
			}

			var (adjustOrigin, adjustHeight) = GetOriginAndHeightForChild(parent, child, out Vector2 newOriginRelParent, out double newHeight);
			if (adjustOrigin || adjustHeight)
			{
				child.OriginRelativeParent = newOriginRelParent;
				child.Height = newHeight;
			}
		}

		public void DoFitToChildrenVertical(GuiWidget widgetToAdjustBounds, ref bool sizeWasChanged)
		{
			if (widgetToAdjustBounds.VAnchorIsSet(VAnchor.Fit))
			{
				double heightToMatchParent = 0;
				if (widgetToAdjustBounds.Parent != null)
				{
					if (!GetOriginAndHeightForChild(widgetToAdjustBounds.Parent, widgetToAdjustBounds, out _, out heightToMatchParent).adjustHeight)
					{
						// we don't need to adjust anything for the parent so make sure this is not applied below.
						heightToMatchParent = 0;
					}
				}

				// get the bounds
				RectangleDouble adjustBounds = widgetToAdjustBounds.LocalBounds;
				// get the bounds to enclose its children
				RectangleDouble childrenEnclosingBounds = widgetToAdjustBounds.GetMinimumBoundsToEncloseChildren(true);
				// fix the v size to enclose the children
				adjustBounds.Bottom = childrenEnclosingBounds.Bottom;
				adjustBounds.Top = Math.Max(childrenEnclosingBounds.Bottom + heightToMatchParent, childrenEnclosingBounds.Top);
				if (widgetToAdjustBounds.LocalBounds != adjustBounds)
				{
					if (widgetToAdjustBounds.VAnchorIsSet(VAnchor.Stretch))
					{
						if (widgetToAdjustBounds.LocalBounds.Height < adjustBounds.Height)
						{
							widgetToAdjustBounds.LocalBounds = adjustBounds;
							sizeWasChanged = true;
						}
					}
					else
					{
						// push the new size in
						widgetToAdjustBounds.LocalBounds = adjustBounds;
						sizeWasChanged = true;
					}
				}
			}
		}

		public override (bool adjustOrigin, bool adjustHeight) GetOriginAndHeightForChild(GuiWidget parent, GuiWidget child, out Vector2 newOriginRelParent, out double newHeight)
		{
			bool needToAdjustHeight = false;
			bool needToAdjustOrigin = false;
			newOriginRelParent = child.OriginRelativeParent;
			newHeight = child.Height;

			if (!parent.Children.Contains(child))
			{
				throw new Exception("You need to call this on the parent that has this child.");
			}

			if ((child.VAnchor & VAnchor.Bottom) == VAnchor.Bottom)
			{
				// hold it to the Bottom
				newOriginRelParent = new Vector2(child.OriginRelativeParent.X, parent.LocalBounds.Bottom + child.DeviceMarginAndBorder.Bottom + parent.DevicePadding.Bottom - child.LocalBounds.Bottom);
				needToAdjustOrigin = true;

				if ((child.VAnchor & VAnchor.Center) == VAnchor.Center)
				{
					// widen the bounds to the center
					double parentUsableHeight = parent.LocalBounds.Height - (parent.DevicePadding.Top + parent.DevicePadding.Bottom);
					newHeight = parentUsableHeight / 2 - child.DeviceMarginAndBorder.Height;
					needToAdjustHeight = true;
				}
				else if ((child.VAnchor & VAnchor.Top) == VAnchor.Top)
				{
					// bounds need to be stretched
					double parentUsableHeight = parent.LocalBounds.Height - (parent.DevicePadding.Bottom + parent.DevicePadding.Top);
					newHeight = parentUsableHeight - (child.DeviceMarginAndBorder.Bottom + child.DeviceMarginAndBorder.Top);
					needToAdjustHeight = true;
				}
			}
			else if ((child.VAnchor & VAnchor.Center) == VAnchor.Center)
			{
				if ((child.VAnchor & VAnchor.Top) == VAnchor.Top)
				{
					// fix the offset
					newOriginRelParent = new VectorMath.Vector2(child.OriginRelativeParent.X,
						parent.DevicePadding.Bottom + child.DeviceMarginAndBorder.Bottom + (parent.Height - parent.DevicePadding.Bottom - parent.DevicePadding.Top) / 2);

					// bounds need to be stretched
					double parentUsableHeight = parent.LocalBounds.Height - (parent.DevicePadding.Top + parent.DevicePadding.Bottom);
					newHeight = parentUsableHeight / 2 - child.DeviceMarginAndBorder.Height;
					needToAdjustHeight = true;
				}
				else
				{
					// hold it centered
					double parentCenterY = parent.LocalBounds.Bottom + parent.DevicePadding.Bottom + (parent.Height - parent.DevicePadding.Bottom - parent.DevicePadding.Top) / 2;
					double originY = parentCenterY - child.LocalBounds.Bottom - (child.Height + child.DeviceMarginAndBorder.Bottom + child.DeviceMarginAndBorder.Top) / 2 + child.DeviceMarginAndBorder.Bottom;
					newOriginRelParent = new Vector2(child.OriginRelativeParent.X, originY);
					needToAdjustOrigin = true;
				}
			}
			else if ((child.VAnchor & VAnchor.Top) == VAnchor.Top)
			{
				// hold it to the Top
				newOriginRelParent = new Vector2(child.OriginRelativeParent.X, parent.LocalBounds.Top - child.DeviceMarginAndBorder.Top - parent.DevicePadding.Top - child.LocalBounds.Top);
				needToAdjustOrigin = true;
			}

			return (needToAdjustOrigin, needToAdjustHeight);
		}

		protected virtual void ApplyHAnchorToChild(GuiWidget parent, GuiWidget child)
		{
			if (child.Parent != parent)
			{
				throw new Exception("All children should have their parent set to the parent they have.");
			}

			var (adjustOrigin, adjustWidth) = GetOriginAndWidthForChild(parent, child, out Vector2 newOriginRelParent, out double newWidth);
			if (adjustOrigin || adjustWidth)
			{
				newWidth = Math.Max(newWidth, child.MinimumSize.X);
				if (child.OriginRelativeParent != newOriginRelParent
					&& child.Width != newWidth)
				{
					var origin = child.OriginRelativeParent;
					var width = child.Width;
					var parentLock = child.Parent?.LayoutLock();
					// only do one layout
					using (child.LayoutLock())
					{
						child.OriginRelativeParent = newOriginRelParent;
						child.Width = newWidth;
					}

					parentLock?.Dispose();
					if (child.OriginRelativeParent != origin
						|| child.Width != width)
					{
						child.PerformLayout();
					}
				}
				else // only one of them will actually change anything (so only one layout)
				{
					child.OriginRelativeParent = newOriginRelParent;
					child.Width = newWidth;
				}
			}
			else if (child.HAnchor == HAnchor.MinFitOrStretch)
			{
				child.OnLayout(new LayoutEventArgs(parent, child));
			}
		}

		public void DoFitToChildrenHorizontal(GuiWidget widgetToAdjust, ref bool sizeWasChanged)
		{
			if (widgetToAdjust.HAnchor.HasFlag(HAnchor.Fit)
				|| widgetToAdjust.HAnchor.HasFlag(HAnchor.MinFitOrStretch))
			{
				double widthToMatchParent = 0;
				// let's check if the parent would like to make this widget bigger
				if (widgetToAdjust.Parent != null)
				{
					if (!GetOriginAndWidthForChild(widgetToAdjust.Parent, widgetToAdjust, out _, out widthToMatchParent).adjustWidth)
					{
						// we don't need to adjust anything for the parent so make sure this is not applied below.
						widthToMatchParent = 0;
					}
				}

				// get the bounds
				RectangleDouble widgetToAdjustBounds = widgetToAdjust.LocalBounds;
				// get the bounds to enclose its children
				RectangleDouble childrenEnclosingBounds = widgetToAdjust.GetMinimumBoundsToEncloseChildren(true);
				// fix the h size to enclose the children
				widgetToAdjustBounds.Left = childrenEnclosingBounds.Left;
				if (widgetToAdjust.Parent != null
					&& widgetToAdjust.Parent.LayoutEngine != null)
				{
					if (widgetToAdjust.Parent.LayoutEngine as LayoutEngineFlow != null)
					{
						// The parent is a flow layout widget but it will only adjust our size if we are HAnchor leftright
						if (widgetToAdjust.HAnchorIsSet(HAnchor.Stretch))
						{
							// We make the assumption that the parent has set the size correctly assuming flow layout and this can only be made bigger if fit needs to.
							widgetToAdjustBounds.Right = Math.Max(widgetToAdjustBounds.Right, childrenEnclosingBounds.Right);
						}
						else // we need to just do the fit to children
						{
							widgetToAdjustBounds.Right = Math.Max(childrenEnclosingBounds.Left + widthToMatchParent, childrenEnclosingBounds.Right);
						}
					}
					else if ((widgetToAdjust.Parent.LayoutEngine as LayoutEngineSimpleAlign) != null)
					{
						if (widgetToAdjust.HAnchor.HasFlag(HAnchor.MinFitOrStretch))
						{
							widgetToAdjustBounds.Right = Math.Min(childrenEnclosingBounds.Right, childrenEnclosingBounds.Left + widgetToAdjust.Parent.Width);
						}
						else
						{
							widgetToAdjustBounds.Right = Math.Max(childrenEnclosingBounds.Left + widthToMatchParent, childrenEnclosingBounds.Right);
						}
					}
					else
					{
						throw new NotImplementedException();
					}
				}
				else
				{
					widgetToAdjustBounds.Right = Math.Max(childrenEnclosingBounds.Left + widthToMatchParent, childrenEnclosingBounds.Right);
				}

				if (widgetToAdjust.LocalBounds != widgetToAdjustBounds)
				{
					if (widgetToAdjust.HAnchorIsSet(HAnchor.Stretch))
					{
						if (widgetToAdjust.LocalBounds.Width < widgetToAdjustBounds.Width)
						{
							widgetToAdjust.LocalBounds = widgetToAdjustBounds;
							sizeWasChanged = true;
						}
					}
					else
					{
						// push the new size in
						widgetToAdjust.LocalBounds = widgetToAdjustBounds;
						sizeWasChanged = true;
					}
				}
			}
		}

		public override (bool adjustOrigin, bool adjustWidth) GetOriginAndWidthForChild(GuiWidget parent, GuiWidget child, out Vector2 newOriginRelParent, out double newWidth)
		{
			bool needToAdjustWidth = false;
			bool needToAdjustOrigin = false;
			newOriginRelParent = child.OriginRelativeParent;
			newWidth = child.Width;
			if (child.HAnchor.HasFlag(HAnchor.Left))
			{
				needToAdjustOrigin = true;
				// Hold it to the left
				newOriginRelParent = new Vector2(parent.LocalBounds.Left + child.DeviceMarginAndBorder.Left + parent.DevicePadding.Left - child.LocalBounds.Left, child.OriginRelativeParent.Y);

				if ((child.HAnchor & HAnchor.Center) == HAnchor.Center)
				{
					// widen the bounds to the center
					double parentUsableWidth = parent.LocalBounds.Width - (parent.DevicePadding.Left + parent.DevicePadding.Right);
					newWidth = parentUsableWidth / 2 - (child.DeviceMarginAndBorder.Left + child.DeviceMarginAndBorder.Right);
					needToAdjustWidth = true;
				}
				else if ((child.HAnchor & HAnchor.Right) == HAnchor.Right)
				{
					// widen the bounds to the right
					double parentUsableWidth = parent.LocalBounds.Width - (parent.DevicePadding.Left + parent.DevicePadding.Right);
					newWidth = parentUsableWidth - (child.DeviceMarginAndBorder.Left + child.DeviceMarginAndBorder.Right);
					needToAdjustWidth = true;
				}
			}
			else if ((child.HAnchor & HAnchor.Center) == HAnchor.Center)
			{
				if ((child.HAnchor & HAnchor.Right) == HAnchor.Right)
				{
					// fix the offset
					newOriginRelParent = new VectorMath.Vector2(
						parent.DevicePadding.Left + child.DeviceMarginAndBorder.Left + (parent.Width - parent.DevicePadding.Left - parent.DevicePadding.Right) / 2,
						child.OriginRelativeParent.Y);

					// widen the bounds to the right
					double parentUsableWidth = parent.LocalBounds.Width - (parent.DevicePadding.Left + parent.DevicePadding.Right);
					newWidth = parentUsableWidth / 2 - (child.DeviceMarginAndBorder.Left + child.DeviceMarginAndBorder.Right);
					needToAdjustWidth = true;
				}
				else
				{
					// hold it centered
					double parentCenterX = parent.LocalBounds.Left + parent.DevicePadding.Left + (parent.Width - (parent.DevicePadding.Left + parent.DevicePadding.Right)) / 2;
					double originX = parentCenterX - child.LocalBounds.Left - (child.Width + child.DeviceMarginAndBorder.Left + child.DeviceMarginAndBorder.Right) / 2 + child.DeviceMarginAndBorder.Left;
					newOriginRelParent = new Vector2(originX, child.OriginRelativeParent.Y);
					needToAdjustOrigin = true;
				}
			}
			else if ((child.HAnchor & HAnchor.Right) == HAnchor.Right)
			{
				// hold it to the right
				newOriginRelParent = new Vector2(parent.LocalBounds.Right - child.DeviceMarginAndBorder.Right - parent.DevicePadding.Right - child.LocalBounds.Right, child.OriginRelativeParent.Y);
				needToAdjustOrigin = true;
			}

			return (needToAdjustOrigin, needToAdjustWidth);
		}
	}
}