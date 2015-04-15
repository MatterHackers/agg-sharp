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

				// if we didn't specify a chiled than achor all the children
				if (layoutEventArgs.ChildWidget == null)
				{
					foreach (GuiWidget child in parent.Children)
					{
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
				throw new Exception("All children should have their parent set the the parent they have.");
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
			if (widgetToAdjustBounds.VAnchorIsSet(VAnchor.FitToChildren))
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
				// get the bounds to enclose its childern
				RectangleDouble childrenEnclosingBounds = widgetToAdjustBounds.GetMinimumBoundsToEncloseChildren(true);
				// fix the v size to enclose the children
				adjustBounds.Bottom = childrenEnclosingBounds.Bottom;
				adjustBounds.Top = Math.Max(childrenEnclosingBounds.Bottom + heightToMatchParent, childrenEnclosingBounds.Top);
				if (widgetToAdjustBounds.LocalBounds != adjustBounds)
				{
					if (widgetToAdjustBounds.VAnchorIsSet(VAnchor.ParentBottomTop))
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

			if ((child.VAnchor & VAnchor.ParentBottom) == VAnchor.ParentBottom)
			{
				// hold it to the Bottom
				newOriginRelParent = new Vector2(child.OriginRelativeParent.x, parent.LocalBounds.Bottom + child.Margin.Bottom + parent.Padding.Bottom - child.LocalBounds.Bottom);

				if ((child.VAnchor & VAnchor.ParentCenter) == VAnchor.ParentCenter)
				{
					// widen the bounds to the center
					double parentUsableHeight = parent.LocalBounds.Height - (parent.Padding.Top + parent.Padding.Bottom);
					newHeight = parentUsableHeight / 2 - child.Margin.Height;
				}
				else if ((child.VAnchor & VAnchor.ParentTop) == VAnchor.ParentTop)
				{
					// bounds need to be stretched
					double parentUsableHeight = parent.LocalBounds.Height - (parent.Padding.Bottom + parent.Padding.Top);
					newHeight = parentUsableHeight - (child.Margin.Bottom + child.Margin.Top);
				}
				needToAdjustAnything = true;
			}
			else if ((child.VAnchor & VAnchor.ParentCenter) == VAnchor.ParentCenter)
			{
				if ((child.VAnchor & VAnchor.ParentTop) == VAnchor.ParentTop)
				{
					// fix the offset
					newOriginRelParent = new VectorMath.Vector2(child.OriginRelativeParent.x,
						parent.Padding.Bottom + child.Margin.Bottom + (parent.Height - parent.Padding.Bottom - parent.Padding.Top) / 2);

					// bounds need to be stretched
					double parentUsableHeight = parent.LocalBounds.Height - (parent.Padding.Top + parent.Padding.Bottom);
					newHeight = parentUsableHeight / 2 - child.Margin.Height;
				}
				else
				{
					// hold it centered
					double parentCenterY = parent.LocalBounds.Bottom + parent.Padding.Bottom + (parent.Height - parent.Padding.Bottom - parent.Padding.Top) / 2;
					double originY = parentCenterY - child.LocalBounds.Bottom - (child.Height + child.Margin.Bottom + child.Margin.Top) / 2 + child.Margin.Bottom;
					newOriginRelParent = new Vector2(child.OriginRelativeParent.x, originY);
				}
				needToAdjustAnything = true;
			}
			else if ((child.VAnchor & VAnchor.ParentTop) == VAnchor.ParentTop)
			{
				// hold it to the Top
				newOriginRelParent = new Vector2(child.OriginRelativeParent.x, parent.LocalBounds.Top - child.Margin.Top - parent.Padding.Top - child.LocalBounds.Top);
				needToAdjustAnything = true;
			}

			return needToAdjustAnything;
		}

		protected virtual void ApplyHAnchorToChild(GuiWidget parent, GuiWidget child)
		{
			if (child.Parent != parent)
			{
				throw new Exception("All children should have their parent set the the parent they have.");
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
			if (widgetToAdjust.HAnchorIsSet(HAnchor.FitToChildren))
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
				// get the bounds to enclose its childern
				RectangleDouble childrenEnclosingBounds = widgetToAdjust.GetMinimumBoundsToEncloseChildren(true);
				// fix the h size to enclose the children
				widgetToAdjustBounds.Left = childrenEnclosingBounds.Left;
				widgetToAdjustBounds.Right = Math.Max(childrenEnclosingBounds.Left + widthToMatchParent, childrenEnclosingBounds.Right);
				if (widgetToAdjust.LocalBounds != widgetToAdjustBounds)
				{
					if (widgetToAdjust.HAnchorIsSet(HAnchor.ParentLeftRight))
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
			if ((child.HAnchor & HAnchor.ParentLeft) == HAnchor.ParentLeft)
			{
				// Hold it to the left
				newOriginRelParent = new Vector2(parent.LocalBounds.Left + child.Margin.Left + parent.Padding.Left - child.LocalBounds.Left, child.OriginRelativeParent.y);

				if ((child.HAnchor & HAnchor.ParentCenter) == HAnchor.ParentCenter)
				{
					// widen the bounds to the center
					double parentUsableWidth = parent.LocalBounds.Width - (parent.Padding.Left + parent.Padding.Right);
					newWidth = parentUsableWidth / 2 - (child.Margin.Left + child.Margin.Right);
				}
				else if ((child.HAnchor & HAnchor.ParentRight) == HAnchor.ParentRight)
				{
					// widen the bounds to the right
					double parentUsableWidth = parent.LocalBounds.Width - (parent.Padding.Left + parent.Padding.Right);
					newWidth = parentUsableWidth - (child.Margin.Left + child.Margin.Right);
				}
				needToAdjustAnything = true;
			}
			else if ((child.HAnchor & HAnchor.ParentCenter) == HAnchor.ParentCenter)
			{
				if ((child.HAnchor & HAnchor.ParentRight) == HAnchor.ParentRight)
				{
					// fix the offset
					newOriginRelParent = new VectorMath.Vector2(
						parent.Padding.Left + child.Margin.Left + (parent.Width - parent.Padding.Left - parent.Padding.Right) / 2,
						child.OriginRelativeParent.y);

					// widen the bounds to the right
					double parentUsableWidth = parent.LocalBounds.Width - (parent.Padding.Left + parent.Padding.Right);
					newWidth = parentUsableWidth / 2 - (child.Margin.Left + child.Margin.Right);
				}
				else
				{
					// hold it centered
					double parentCenterX = parent.LocalBounds.Left + parent.Padding.Left + (parent.Width - (parent.Padding.Left + parent.Padding.Right)) / 2;
					double originX = parentCenterX - child.LocalBounds.Left - (child.Width + child.Margin.Left + child.Margin.Right) / 2 + child.Margin.Left;
					newOriginRelParent = new Vector2(originX, child.OriginRelativeParent.y);
				}
				needToAdjustAnything = true;
			}
			else if ((child.HAnchor & HAnchor.ParentRight) == HAnchor.ParentRight)
			{
				// hold it to the right
				newOriginRelParent = new Vector2(parent.LocalBounds.Right - child.Margin.Right - parent.Padding.Right - child.LocalBounds.Right, child.OriginRelativeParent.y);
				needToAdjustAnything = true;
			}

			return needToAdjustAnything;
		}
	}
}