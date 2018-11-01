/*
Copyright (c) 2015, Lars Brubaker
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
	public enum FlowDirection { LeftToRight, BottomToTop, RightToLeft, TopToBottom };

	public class LayoutEngineFlow : LayoutEngineSimpleAlign
	{
		public FlowDirection FlowDirection { get; set; }

		public LayoutEngineFlow(FlowDirection FlowDirection)
		{
			this.FlowDirection = FlowDirection;
		}

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
					for (int i = 0; i < parent.Children.Count; i++)
					{
						if (parent.HasBeenClosed)
						{
							return;
						}

						GuiWidget child = parent.Children[i];
						if (child.Visible == false)
						{
							continue;
						}
						ApplyHAnchorToChild(parent, child);
						ApplyVAnchorToChild(parent, child);
					}

					DoLayoutChildren(layoutEventArgs);

					FixOriginXIfRightToLeft(parent);
					FixOriginYIfTopToBottom(parent);

					bool parentChangedSize = false;
					DoFitToChildrenHorizontal(parent, ref parentChangedSize);
					DoFitToChildrenVertical(parent, ref parentChangedSize);
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

						DoLayoutChildren(layoutEventArgs);
					}
				}
			}
		}

		private void FixOriginXIfRightToLeft(GuiWidget parent)
		{
			if (parent.HAnchorIsSet(HAnchor.Fit) && FlowDirection == UI.FlowDirection.RightToLeft)
			{
				RectangleDouble encloseChildrenRect = parent.GetMinimumBoundsToEncloseChildren();

				for (int childIndex = 0; childIndex < parent.Children.Count; childIndex++)
				{
					if (parent.HasBeenClosed)
					{
						return;
					}

					GuiWidget child = parent.Children[childIndex];
					if (child.Visible == false)
					{
						continue;
					}

					child.OriginRelativeParent = new Vector2(child.OriginRelativeParent.X - encloseChildrenRect.Left, child.OriginRelativeParent.Y);
				}
			}
		}

		private void FixOriginYIfTopToBottom(GuiWidget parent)
		{
			if (parent.VAnchorIsSet(VAnchor.Fit) && FlowDirection == UI.FlowDirection.TopToBottom)
			{
				RectangleDouble encloseChildrenRect = parent.GetMinimumBoundsToEncloseChildren();

				for (int childIndex = 0; childIndex < parent.Children.Count; childIndex++)
				{
					if (parent.HasBeenClosed)
					{
						return;
					}

					GuiWidget child = parent.Children[childIndex];
					if (child.Visible == false)
					{
						continue;
					}

					child.OriginRelativeParent = new Vector2(child.OriginRelativeParent.X, child.OriginRelativeParent.Y - encloseChildrenRect.Bottom);
				}
			}
		}

		public override (bool adjustOrigin, bool adjustHeight) GetOriginAndHeightForChild(GuiWidget parent, GuiWidget child, out Vector2 newOriginRelParent, out double newHeight)
		{
			newOriginRelParent = Vector2.Zero;
			newHeight = 0;
			if (FlowDirection == UI.FlowDirection.LeftToRight || FlowDirection == UI.FlowDirection.RightToLeft)
			{
				return base.GetOriginAndHeightForChild(parent, child, out newOriginRelParent, out newHeight);
			}

			return (false, false);
		}

		public override (bool adjustOrigin, bool adjustWidth) GetOriginAndWidthForChild(GuiWidget parent, GuiWidget child, out Vector2 newOriginRelParent, out double newWidth)
		{
			newOriginRelParent = Vector2.Zero;
			newWidth = 0;
			if (FlowDirection == UI.FlowDirection.BottomToTop || FlowDirection == UI.FlowDirection.TopToBottom)
			{
				return base.GetOriginAndWidthForChild(parent, child, out newOriginRelParent, out newWidth);
			}

			return (false, false);
		}

		protected override void ApplyHAnchorToChild(GuiWidget parent, GuiWidget child)
		{
			if (FlowDirection == UI.FlowDirection.BottomToTop || FlowDirection == UI.FlowDirection.TopToBottom)
			{
				base.ApplyHAnchorToChild(parent, child);
			}
			else
			{
				if (child.HAnchor == HAnchor.Stretch || child.HAnchorIsSet(HAnchor.Fit))
				{
				}
				else if (child.HAnchor != HAnchor.Absolute)
				{
					throw new Exception("HAnchor for a left right flow widget needs to be Absolute or Stretch.");
				}
			}
		}

		protected override void ApplyVAnchorToChild(GuiWidget parent, GuiWidget child)
		{
			if (FlowDirection == UI.FlowDirection.LeftToRight || FlowDirection == UI.FlowDirection.RightToLeft)
			{
				base.ApplyVAnchorToChild(parent, child);
			}
			else
			{
				if (child.VAnchor == VAnchor.Stretch || child.VAnchorIsSet(VAnchor.Fit))
				{
				}
				else if (child.VAnchor != VAnchor.Absolute)
				{
					throw new Exception("VAnchor for a top bottom flow widget needs to be Absolute or Stretch.");
				}
			}
		}

		private void DoLayoutChildren(LayoutEventArgs layoutEventArgs)
		{
			GuiWidget parent = layoutEventArgs.ParentWidget;

			if (parent.CountVisibleChildren() == 0)
			{
				return;
			}

			RectangleDouble boundsOfAllChildrenIncludingMargin = RectangleDouble.ZeroIntersection;

			double totalWidthWithMargin = 0;
			double totalHeightWithMargin = 0;

			double totalWidthOfStaticItems = 0;
			double totalHeightOfStaticItems = 0;
			int numItemsNeedingExpanding = 0;

			double totalMinimumWidthOfAllItems = 0;
			double totalMinimumHeightOfAllItems = 0;

			for (int childIndex = 0; childIndex < parent.Children.Count; childIndex++)
			{
				if (parent.HasBeenClosed)
				{
					return;
				}

				GuiWidget child = parent.Children[childIndex];
				if (child.Visible == false)
				{
					continue;
				}

				RectangleDouble childBoundsWithMargin = child.LocalBounds;
				childBoundsWithMargin.Inflate(child.DeviceMarginAndBorder);
				totalWidthWithMargin += childBoundsWithMargin.Width;
				totalHeightWithMargin += childBoundsWithMargin.Height;
				boundsOfAllChildrenIncludingMargin.ExpandToInclude(childBoundsWithMargin);

				switch (FlowDirection)
				{
					case UI.FlowDirection.LeftToRight:
					case UI.FlowDirection.RightToLeft:
						totalMinimumWidthOfAllItems += child.MinimumSize.X + child.DeviceMarginAndBorder.Width;
						totalMinimumHeightOfAllItems = Math.Max(totalMinimumHeightOfAllItems, child.MinimumSize.Y + child.DeviceMarginAndBorder.Height);

						if (child.HAnchorIsSet(HAnchor.Stretch))
						{
							numItemsNeedingExpanding++;
							totalWidthOfStaticItems += child.DeviceMarginAndBorder.Width;
						}
						else if (child.HAnchor == HAnchor.Absolute || child.HAnchorIsSet(HAnchor.Fit))
						{
							totalWidthOfStaticItems += childBoundsWithMargin.Width;
						}
						else
						{
							throw new Exception("Only Absolute or Stretch are valid HAnchor for a horizontal flowWidget.");
						}
						break;

					case UI.FlowDirection.TopToBottom:
					case UI.FlowDirection.BottomToTop:
						totalMinimumWidthOfAllItems = Math.Max(totalMinimumWidthOfAllItems, child.MinimumSize.X + child.DeviceMarginAndBorder.Width);
						totalMinimumHeightOfAllItems += child.MinimumSize.Y + child.DeviceMarginAndBorder.Height;
						if (child.VAnchorIsSet(VAnchor.Stretch))
						{
							numItemsNeedingExpanding++;
							totalHeightOfStaticItems += child.DeviceMarginAndBorder.Height;
						}
						else if (child.VAnchor == VAnchor.Absolute || child.VAnchorIsSet(VAnchor.Fit))
						{
							totalHeightOfStaticItems += childBoundsWithMargin.Height;
						}
						else
						{
							throw new Exception("Only Absolute or Stretch are valid VAnchor for a vertical flowWidget.");
						}
						break;

					default:
						throw new NotImplementedException();
				}
			}

			switch (FlowDirection)
			{
				case UI.FlowDirection.LeftToRight:
					{
						double curX = parent.DevicePadding.Left;
						foreach (GuiWidget child in parent.Children)
						{
							if (parent.HasBeenClosed)
							{
								return;
							}

							if (child.Visible == true)
							{
								double newX = curX - child.LocalBounds.Left + child.DeviceMarginAndBorder.Left;
								child.OriginRelativeParent = new Vector2(newX, child.OriginRelativeParent.Y);
								if (child.HAnchorIsSet(HAnchor.Stretch))
								{
									RectangleDouble curChildBounds = child.LocalBounds;
									double newWidth = (parent.LocalBounds.Width - parent.DevicePadding.Width - totalWidthOfStaticItems) / numItemsNeedingExpanding;
									child.LocalBounds = new RectangleDouble(curChildBounds.Left, curChildBounds.Bottom,
										curChildBounds.Left + newWidth, curChildBounds.Top);
								}
								curX += child.LocalBounds.Width + child.DeviceMarginAndBorder.Width;
							}
						}
					}
					break;

				case UI.FlowDirection.RightToLeft:
					{
						double curX = parent.LocalBounds.Right - parent.DevicePadding.Right;
						foreach (GuiWidget child in parent.Children)
						{
							if (parent.HasBeenClosed)
							{
								return;
							}

							if (child.Visible == true)
							{
								if (child.HAnchorIsSet(HAnchor.Stretch))
								{
									RectangleDouble curChildBounds = child.LocalBounds;
									double newWidth = (parent.LocalBounds.Width - parent.DevicePadding.Width - totalWidthOfStaticItems) / numItemsNeedingExpanding;
									child.LocalBounds = new RectangleDouble(curChildBounds.Left, curChildBounds.Bottom,
										curChildBounds.Left + newWidth, curChildBounds.Top);
								}

								double newX = curX - child.LocalBounds.Left - (child.LocalBounds.Width + child.DeviceMarginAndBorder.Right);
								child.OriginRelativeParent = new Vector2(newX, child.OriginRelativeParent.Y);
								curX -= (child.LocalBounds.Width + child.DeviceMarginAndBorder.Width);
							}
						}
					}
					break;

				case UI.FlowDirection.BottomToTop:
					{
						double curY = parent.DevicePadding.Bottom;
						foreach (GuiWidget child in parent.Children)
						{
							if (parent.HasBeenClosed)
							{
								return;
							}

							if (child.Visible == true)
							{
								double newY = curY - child.LocalBounds.Bottom + child.DeviceMarginAndBorder.Bottom;
								child.OriginRelativeParent = new Vector2(child.OriginRelativeParent.X, newY);
								if (child.VAnchorIsSet(VAnchor.Stretch))
								{
									RectangleDouble curChildBounds = child.LocalBounds;
									double newHeight = (parent.LocalBounds.Height - parent.DevicePadding.Height - totalHeightOfStaticItems) / numItemsNeedingExpanding;
									child.LocalBounds = new RectangleDouble(curChildBounds.Left, curChildBounds.Bottom,
										curChildBounds.Right, curChildBounds.Bottom + newHeight);
								}
								curY += child.LocalBounds.Height + child.DeviceMarginAndBorder.Height;
							}
						}
					}
					break;

				case UI.FlowDirection.TopToBottom:
					{
						double curY = parent.LocalBounds.Top - parent.DevicePadding.Top;
						foreach (GuiWidget child in parent.Children)
						{
							if (parent.HasBeenClosed)
							{
								return;
							}

							if (child.Visible == true)
							{
								if (child.VAnchorIsSet(VAnchor.Stretch))
								{
									RectangleDouble curChildBounds = child.LocalBounds;
									double newHeight = (parent.LocalBounds.Height - parent.DevicePadding.Height - totalHeightOfStaticItems) / numItemsNeedingExpanding;// - child.DeviceMarginAndBorder.Height;
									child.LocalBounds = new RectangleDouble(curChildBounds.Left, curChildBounds.Bottom,
										curChildBounds.Right, curChildBounds.Bottom + newHeight);
								}

								double newY = curY - child.LocalBounds.Bottom - (child.LocalBounds.Height + child.DeviceMarginAndBorder.Top);
								child.OriginRelativeParent = new Vector2(child.OriginRelativeParent.X, newY);
								curY -= (child.LocalBounds.Height + child.DeviceMarginAndBorder.Height);
							}
						}
					}
					break;

				default:
					throw new NotImplementedException();
			}
		}
	}
}