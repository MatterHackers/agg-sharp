using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.VectorMath;

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
                parent.SuspendLayout();

                foreach (GuiWidget child in parent.Children)
                {
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
                        ApplyHAnchorToChild(parent, child);
                        ApplyVAnchorToChild(parent, child);
                    }

                    DoLayoutChildren(layoutEventArgs);
                }

                parent.ResumeLayout();
            }
        }

        private void FixOriginXIfRightToLeft(GuiWidget parent)
        {
            if (parent.HAnchorIsSet(HAnchor.FitToChildren) && FlowDirection == UI.FlowDirection.RightToLeft)
            {
                RectangleDouble encloseChildrenRect = parent.GetMinimumBoundsToEncloseChildren();

                for (int childIndex = 0; childIndex < parent.Children.Count; childIndex++)
                {
                    GuiWidget child = parent.Children[childIndex];
                    if (child.Visible == false)
                    {
                        continue;
                    }

                    child.OriginRelativeParent = new Vector2(child.OriginRelativeParent.x - encloseChildrenRect.Left, child.OriginRelativeParent.y);
                }
            }
        }

        private void FixOriginYIfTopToBottom(GuiWidget parent)
        {
            if (parent.VAnchorIsSet(VAnchor.FitToChildren) && FlowDirection == UI.FlowDirection.TopToBottom)
            {
                RectangleDouble encloseChildrenRect = parent.GetMinimumBoundsToEncloseChildren();

                for (int childIndex = 0; childIndex < parent.Children.Count; childIndex++)
                {
                    GuiWidget child = parent.Children[childIndex];
                    if (child.Visible == false)
                    {
                        continue;
                    }

                    child.OriginRelativeParent = new Vector2(child.OriginRelativeParent.x, child.OriginRelativeParent.y - encloseChildrenRect.Bottom);
                }
            }
        }

        public override bool GetOriginAndHeightForChild(GuiWidget parent, GuiWidget child, out Vector2 newOriginRelParent, out double newHeight)
        {
            newOriginRelParent = Vector2.Zero;
            newHeight = 0;
            if (FlowDirection == UI.FlowDirection.LeftToRight || FlowDirection == UI.FlowDirection.RightToLeft)
            {
                return base.GetOriginAndHeightForChild(parent, child, out newOriginRelParent, out newHeight);
            }

            return false;
        }
    
        public override bool GetOriginAndWidthForChild(GuiWidget parent, GuiWidget child, out Vector2 newOriginRelParent, out double newWidth)
        {
            newOriginRelParent = Vector2.Zero;
            newWidth = 0;
            if (FlowDirection == UI.FlowDirection.BottomToTop || FlowDirection == UI.FlowDirection.TopToBottom)
            {
                return base.GetOriginAndWidthForChild(parent, child, out newOriginRelParent, out newWidth);
            }

            return false;
        }

        protected override void ApplyHAnchorToChild(GuiWidget parent, GuiWidget child)
        {
            if (FlowDirection == UI.FlowDirection.BottomToTop || FlowDirection == UI.FlowDirection.TopToBottom)
            {
                base.ApplyHAnchorToChild(parent, child);
            }
            else
            {
                if (child.HAnchor == HAnchor.ParentLeftRight || child.HAnchorIsSet(HAnchor.FitToChildren))
                {
                }
                else if (child.HAnchor != HAnchor.None)
                {
                    throw new Exception("HAnchor for a left right flow widget needs to be none or ParentLeftRight.");
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
                if (child.VAnchor == VAnchor.ParentBottomTop || child.VAnchorIsSet(VAnchor.FitToChildren))
                {
                }
                else if (child.VAnchor != VAnchor.None)
                {
                    throw new Exception("VAnchor for a top bottom flow widget needs to be none or ParentTopBottom.");
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
                GuiWidget child = parent.Children[childIndex];
                if (child.Visible == false)
                {
                    continue;
                }

                RectangleDouble childBoundsWithMargin = child.LocalBounds;
                childBoundsWithMargin.Inflate(child.Margin);
                totalWidthWithMargin += childBoundsWithMargin.Width;
                totalHeightWithMargin += childBoundsWithMargin.Height;
                boundsOfAllChildrenIncludingMargin.ExpandToInclude(childBoundsWithMargin);

                switch (FlowDirection)
                {
                    case UI.FlowDirection.LeftToRight:
                    case UI.FlowDirection.RightToLeft:
                        totalMinimumWidthOfAllItems += child.MinimumSize.x + child.Margin.Width;
                        totalMinimumHeightOfAllItems = Math.Max(totalMinimumHeightOfAllItems, child.MinimumSize.y + child.Margin.Height);

                        if (child.HAnchorIsSet(HAnchor.ParentLeftRight))
                        {
                            numItemsNeedingExpanding++;
                            totalWidthOfStaticItems += child.Margin.Width;
                        }
                        else if (child.HAnchor == HAnchor.None || child.HAnchorIsSet(HAnchor.FitToChildren))
                        {
                            totalWidthOfStaticItems += childBoundsWithMargin.Width;
                        }
                        else
                        {
                            throw new Exception("Only None or ParentLeftRight are valid HAnchor for a horizontal flowWidget.");
                        }
                        break;

                    case UI.FlowDirection.TopToBottom:
                    case UI.FlowDirection.BottomToTop:
                        totalMinimumWidthOfAllItems = Math.Max(totalMinimumWidthOfAllItems, child.MinimumSize.x + child.Margin.Width);
                        totalMinimumHeightOfAllItems += child.MinimumSize.y + child.Margin.Height;
                        if (child.VAnchorIsSet(VAnchor.ParentBottomTop))
                        {
                            numItemsNeedingExpanding++;
                            totalHeightOfStaticItems += child.Margin.Height;
                        }
                        else if (child.VAnchor == VAnchor.None || child.VAnchorIsSet(VAnchor.FitToChildren))
                        {
                            totalHeightOfStaticItems += childBoundsWithMargin.Height;
                        }
                        else
                        {
                            throw new Exception("Only None or ParentBottomTop are valid VAnchor for a vertial flowWidget.");
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
                        double curX = parent.Padding.Left;
                        foreach (GuiWidget child in parent.Children)
                        {
                            if (child.Visible == true)
                            {
                                double newX = curX - child.LocalBounds.Left + child.Margin.Left;
                                child.OriginRelativeParent = new Vector2(newX, child.OriginRelativeParent.y);
                                if (child.HAnchorIsSet(HAnchor.ParentLeftRight))
                                {
                                    RectangleDouble curChildBounds = child.LocalBounds;
                                    double newWidth = (parent.LocalBounds.Width - parent.Padding.Width - totalWidthOfStaticItems) / numItemsNeedingExpanding;
                                    child.LocalBounds = new RectangleDouble(curChildBounds.Left, curChildBounds.Bottom,
                                        newWidth, curChildBounds.Top);
                                }
                                curX += child.LocalBounds.Width + child.Margin.Width;
                            }
                        }
                    }
                    break;

                case UI.FlowDirection.RightToLeft:
                    {
                        double curX = parent.LocalBounds.Right - parent.Padding.Right;
                        foreach (GuiWidget child in parent.Children)
                        {
                            if (child.Visible == true)
                            {
                                if (child.HAnchorIsSet(HAnchor.ParentLeftRight))
                                {
                                    RectangleDouble curChildBounds = child.LocalBounds;
                                    double newWidth = (parent.LocalBounds.Width - parent.Padding.Width - totalWidthOfStaticItems) / numItemsNeedingExpanding;
                                    child.LocalBounds = new RectangleDouble(curChildBounds.Left, curChildBounds.Bottom,
                                        newWidth, curChildBounds.Top);
                                }

                                double newX = curX - child.LocalBounds.Left - (child.LocalBounds.Width + child.Margin.Right);
                                child.OriginRelativeParent = new Vector2(newX, child.OriginRelativeParent.y);
                                curX -= (child.LocalBounds.Width + child.Margin.Width);
                            }
                        }
                    }
                    break;

                case UI.FlowDirection.BottomToTop:
                    {
                        double curY = parent.Padding.Bottom;
                        foreach (GuiWidget child in parent.Children)
                        {
                            if (child.Visible == true)
                            {
                                double newY = curY - child.LocalBounds.Bottom + child.Margin.Bottom;
                                child.OriginRelativeParent = new Vector2(child.OriginRelativeParent.x, newY);
                                if (child.VAnchorIsSet(VAnchor.ParentBottomTop))
                                {
                                    RectangleDouble curChildBounds = child.LocalBounds;
                                    double newHeight = (parent.LocalBounds.Height - parent.Padding.Height - totalHeightOfStaticItems) / numItemsNeedingExpanding;
                                    child.LocalBounds = new RectangleDouble(curChildBounds.Left, curChildBounds.Bottom,
                                        curChildBounds.Right, newHeight);
                                }
                                curY += child.LocalBounds.Height + child.Margin.Height;
                            }
                        }
                    }
                    break;

                case UI.FlowDirection.TopToBottom:
                    {
                        double curY = parent.LocalBounds.Top - parent.Padding.Top;
                        foreach (GuiWidget child in parent.Children)
                        {
                            if (child.Visible == true)
                            {
                                if (child.VAnchorIsSet(VAnchor.ParentBottomTop))
                                {
                                    RectangleDouble curChildBounds = child.LocalBounds;
                                    double newHeight = (parent.LocalBounds.Height - parent.Padding.Height - totalHeightOfStaticItems) / numItemsNeedingExpanding;
                                    child.LocalBounds = new RectangleDouble(curChildBounds.Left, curChildBounds.Bottom,
                                        curChildBounds.Right, newHeight);
                                }

                                double newY = curY - child.LocalBounds.Bottom - (child.LocalBounds.Height + child.Margin.Top);
                                child.OriginRelativeParent = new Vector2(child.OriginRelativeParent.x, newY);
                                curY -= (child.LocalBounds.Height + child.Margin.Height);
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
