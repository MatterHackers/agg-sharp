using MatterHackers.VectorMath;
using System;

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
				parent.SuspendLayout();

				// if we didn't specify a child than anchor all the children
				if (layoutEventArgs.ChildWidget == null)
				{
					foreach (GuiWidget child in parent.Children)
					{
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
						if (child.Visible == false)
						{
							continue;
						}
						ApplyHAnchorToChild(parent, child);
						ApplyVAnchorToChild(parent, child);
					}
				}

				parent.ResumeLayout();
			}
		}

		protected virtual void ApplyVAnchorToChild(GuiWidget parent, GuiWidget child)
		{
			if (child.Parent != parent)
			{
				throw new Exception("All children should have their parent set the parent they have.");
			}

			Vector2 newOriginRelParent;
			double newHeight;
			if (GetOriginAndHeightForChild(parent, child, out newOriginRelParent, out newHeight))
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
					Vector2 newOriginRelParent;
					if (!GetOriginAndHeightForChild(widgetToAdjustBounds.Parent, widgetToAdjustBounds, out newOriginRelParent, out heightToMatchParent))
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

		public override bool GetOriginAndHeightForChild(GuiWidget parent, GuiWidget child, out Vector2 newOriginRelParent, out double newHeight)
		{
			bool needToAdjustAnything = false;
			newOriginRelParent = child.OriginRelativeParent;
			newHeight = child.Height;

			if (!parent.Children.Contains(child))
			{
				throw new Exception("You need to call this on the parent that has this child.");
			}

			if ((child.VAnchor & VAnchor.Bottom) == VAnchor.Bottom)
			{
				// hold it to the Bottom
				newOriginRelParent = new Vector2(child.OriginRelativeParent.x, parent.LocalBounds.Bottom + child.DeviceMargin.Bottom + parent.DevicePadding.Bottom - child.LocalBounds.Bottom);

				if ((child.VAnchor & VAnchor.Center) == VAnchor.Center)
				{
					// widen the bounds to the center
					double parentUsableHeight = parent.LocalBounds.Height - (parent.DevicePadding.Top + parent.DevicePadding.Bottom);
					newHeight = parentUsableHeight / 2 - child.DeviceMargin.Height;
				}
				else if ((child.VAnchor & VAnchor.Top) == VAnchor.Top)
				{
					// bounds need to be stretched
					double parentUsableHeight = parent.LocalBounds.Height - (parent.DevicePadding.Bottom + parent.DevicePadding.Top);
					newHeight = parentUsableHeight - (child.DeviceMargin.Bottom + child.DeviceMargin.Top);
				}
				needToAdjustAnything = true;
			}
			else if ((child.VAnchor & VAnchor.Center) == VAnchor.Center)
			{
				if ((child.VAnchor & VAnchor.Top) == VAnchor.Top)
				{
					// fix the offset
					newOriginRelParent = new VectorMath.Vector2(child.OriginRelativeParent.x,
						parent.DevicePadding.Bottom + child.DeviceMargin.Bottom + (parent.Height - parent.DevicePadding.Bottom - parent.DevicePadding.Top) / 2);

					// bounds need to be stretched
					double parentUsableHeight = parent.LocalBounds.Height - (parent.DevicePadding.Top + parent.DevicePadding.Bottom);
					newHeight = parentUsableHeight / 2 - child.DeviceMargin.Height;
				}
				else
				{
					// hold it centered
					double parentCenterY = parent.LocalBounds.Bottom + parent.DevicePadding.Bottom + (parent.Height - parent.DevicePadding.Bottom - parent.DevicePadding.Top) / 2;
					double originY = parentCenterY - child.LocalBounds.Bottom - (child.Height + child.DeviceMargin.Bottom + child.DeviceMargin.Top) / 2 + child.DeviceMargin.Bottom;
					newOriginRelParent = new Vector2(child.OriginRelativeParent.x, originY);
				}
				needToAdjustAnything = true;
			}
			else if ((child.VAnchor & VAnchor.Top) == VAnchor.Top)
			{
				// hold it to the Top
				newOriginRelParent = new Vector2(child.OriginRelativeParent.x, parent.LocalBounds.Top - child.DeviceMargin.Top - parent.DevicePadding.Top - child.LocalBounds.Top);
				needToAdjustAnything = true;
			}

			return needToAdjustAnything;
		}

		protected virtual void ApplyHAnchorToChild(GuiWidget parent, GuiWidget child)
		{
			if (child.Parent != parent)
			{
				throw new Exception("All children should have their parent set to the parent they have.");
			}

			Vector2 newOriginRelParent;
			double newWidth;
			if (GetOriginAndWidthForChild(parent, child, out newOriginRelParent, out newWidth))
			{
				child.OriginRelativeParent = newOriginRelParent;
				child.Width = newWidth;
			}
		}

		public void DoFitToChildrenHorizontal(GuiWidget widgetToAdjust, ref bool sizeWasChanged)
		{
			if (widgetToAdjust.HAnchorIsSet(HAnchor.Fit))
			{
				double widthToMatchParent = 0;
				// let's check if the parent would like to make this widget bigger
				if (widgetToAdjust.Parent != null)
				{
					Vector2 newOriginRelParent;
					if (!GetOriginAndWidthForChild(widgetToAdjust.Parent, widgetToAdjust, out newOriginRelParent, out widthToMatchParent))
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
					if(widgetToAdjust.Parent.LayoutEngine as LayoutEngineFlow != null)
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
						widgetToAdjustBounds.Right = Math.Max(childrenEnclosingBounds.Left + widthToMatchParent, childrenEnclosingBounds.Right);
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

		public override bool GetOriginAndWidthForChild(GuiWidget parent, GuiWidget child, out Vector2 newOriginRelParent, out double newWidth)
		{
			bool needToAdjustAnything = false;
			newOriginRelParent = child.OriginRelativeParent;
			newWidth = child.Width;
			if ((child.HAnchor & HAnchor.Left) == HAnchor.Left)
			{
				// Hold it to the left
				newOriginRelParent = new Vector2(parent.LocalBounds.Left + child.DeviceMargin.Left + parent.DevicePadding.Left - child.LocalBounds.Left, child.OriginRelativeParent.y);

				if ((child.HAnchor & HAnchor.Center) == HAnchor.Center)
				{
					// widen the bounds to the center
					double parentUsableWidth = parent.LocalBounds.Width - (parent.DevicePadding.Left + parent.DevicePadding.Right);
					newWidth = parentUsableWidth / 2 - (child.DeviceMargin.Left + child.DeviceMargin.Right);
				}
				else if ((child.HAnchor & HAnchor.Right) == HAnchor.Right)
				{
					// widen the bounds to the right
					double parentUsableWidth = parent.LocalBounds.Width - (parent.DevicePadding.Left + parent.DevicePadding.Right);
					newWidth = parentUsableWidth - (child.DeviceMargin.Left + child.DeviceMargin.Right);
				}
				needToAdjustAnything = true;
			}
			else if ((child.HAnchor & HAnchor.Center) == HAnchor.Center)
			{
				if ((child.HAnchor & HAnchor.Right) == HAnchor.Right)
				{
					// fix the offset
					newOriginRelParent = new VectorMath.Vector2(
						parent.DevicePadding.Left + child.DeviceMargin.Left + (parent.Width - parent.DevicePadding.Left - parent.DevicePadding.Right) / 2,
						child.OriginRelativeParent.y);

					// widen the bounds to the right
					double parentUsableWidth = parent.LocalBounds.Width - (parent.DevicePadding.Left + parent.DevicePadding.Right);
					newWidth = parentUsableWidth / 2 - (child.DeviceMargin.Left + child.DeviceMargin.Right);
				}
				else
				{
					// hold it centered
					double parentCenterX = parent.LocalBounds.Left + parent.DevicePadding.Left + (parent.Width - (parent.DevicePadding.Left + parent.DevicePadding.Right)) / 2;
					double originX = parentCenterX - child.LocalBounds.Left - (child.Width + child.DeviceMargin.Left + child.DeviceMargin.Right) / 2 + child.DeviceMargin.Left;
					newOriginRelParent = new Vector2(originX, child.OriginRelativeParent.y);
				}
				needToAdjustAnything = true;
			}
			else if ((child.HAnchor & HAnchor.Right) == HAnchor.Right)
			{
				// hold it to the right
				newOriginRelParent = new Vector2(parent.LocalBounds.Right - child.DeviceMargin.Right - parent.DevicePadding.Right - child.LocalBounds.Right, child.OriginRelativeParent.y);
				needToAdjustAnything = true;
			}

			return needToAdjustAnything;
		}
	}
}