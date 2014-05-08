using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

using MatterHackers.Agg.Font;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
    public class ListBoxTextItem : TextWidget
    {
        public string ItemValue { get; set; }

        public ListBoxTextItem(string displayName, string itemValue)
            : base(displayName)
        {
            Padding = new BorderDouble(3);
            ItemValue = itemValue;
            MinimumSize = new Vector2(Width, Height);
        }
    }

    public class ListBox : ScrollableWidget
    {
        public delegate void SelectedValueChangedEventHandler(object sender, EventArgs e);
        public event SelectedValueChangedEventHandler SelectedValueChanged;
        public delegate void HoverValueChangedEventHandler(object sender, EventArgs e);
        public event HoverValueChangedEventHandler HoverValueChanged;

        protected FlowLayoutWidget topToBottomItemList;

        RGBA_Bytes hoverColor = new RGBA_Bytes(205, 205, 255, 255);
        RGBA_Bytes selectedColor = new RGBA_Bytes(105, 105, 255, 255);

        int selectedIndex = -1;
        int hoverIndex = -1;
        int dragIndex = -1;

        public int Count
        {
            get
            {
                return topToBottomItemList.Children.Count;
            }
        }

        public int SelectedIndex
        {
            get
            { 
                return selectedIndex; 
            }
            set 
            {
                if (value < -1 || value >= topToBottomItemList.Children.Count)
                {
                    throw new ArgumentOutOfRangeException();
                }

                if (value != selectedIndex)
                {
                    selectedIndex = value;
                    OnSelectedIndexChanged();

                    for (int index = 0; index < topToBottomItemList.Children.Count; index++)
                    {
                        GuiWidget child = topToBottomItemList.Children[index];
                        if (index == selectedIndex)
                        {
                            child.BackgroundColor = selectedColor;
                            SelectedIndex = index;
                        }
                        else
                        {
                            child.BackgroundColor = new RGBA_Bytes();
                        }
                        child.Invalidate();
                    }

                    Invalidate();
                }
            }
        }

        public int DragIndex
        {
            get
            {
                return dragIndex;
            }
            set
            {
                if (value < -1 || value >= topToBottomItemList.Children.Count)
                {
                    throw new ArgumentOutOfRangeException();
                }

                if (value != dragIndex)
                {
                    dragIndex = value;
                }
            }
        }

        public int HoverIndex
        {
            get
            {
                return hoverIndex;
            }
            set
            {
                if (value < -1 || value >= topToBottomItemList.Children.Count)
                {
                    throw new ArgumentOutOfRangeException();
                }

                if (value != hoverIndex)
                {
                    hoverIndex = value;
                    OnHoverIndexChanged();

                    RGBA_Bytes noneColor = new RGBA_Bytes();
                    for (int index = 0; index < topToBottomItemList.Children.Count; index++)
                    {
                        if (index != SelectedIndex)
                        {
                            GuiWidget child = topToBottomItemList.Children[index];
                            if (index == HoverIndex)
                            {
                                HoverIndex = index;
                                child.BackgroundColor = hoverColor;
                            }
                            else if (child.BackgroundColor != noneColor)
                            {
                                child.BackgroundColor = noneColor;
                            }
                            child.Invalidate();
                        }
                    }

                    Invalidate();
                }
            }
        }

        public ListBox(RectangleDouble bounds)
        {
            AutoScroll = true;
            LocalBounds = new RectangleDouble(0, 0, bounds.Width, bounds.Height);
            topToBottomItemList = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottomItemList.HAnchor = UI.HAnchor.ParentLeft | UI.HAnchor.FitToChildren;
            base.AddChild(topToBottomItemList);
        }

        public ListBox()
            : this(new RectangleDouble())
        {
        }

        public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
        {
            FlowLayoutWidget itemHolder = new FlowLayoutWidget();
            itemHolder.Name = "LB item holder";
            itemHolder.Margin = new BorderDouble(3, 0, 0, 0);
            itemHolder.HAnchor = UI.HAnchor.ParentLeftRight | UI.HAnchor.FitToChildren;
            itemHolder.AddChild(child);
            //itemHolder.FitToChildren();
            topToBottomItemList.AddChild(itemHolder, indexInChildrenList);

            itemHolder.MouseEnterBounds += new EventHandler(itemToAdd_MouseEnterBounds);
            itemHolder.MouseLeaveBounds += new EventHandler(itemToAdd_MouseLeaveBounds);
            itemHolder.MouseDownInBounds += new MouseEventHandler(itemHolder_MouseDownInBounds);
            itemHolder.ParentChanged += new EventHandler(itemHolder_ParentChanged);
        }

        bool settingLocalBounds = false;
        public override RectangleDouble LocalBounds
        {
            set
            {
                if (!settingLocalBounds)
                {
                    Vector2 currentTopLeftOffset = new Vector2();
                    if (Parent != null)
                    {
                        currentTopLeftOffset = TopLeftOffset;
                    }
                    settingLocalBounds = true;
                    if (topToBottomItemList != null)
                    {
                        if (VerticalScrollBar.Visible)
                        {
                            topToBottomItemList.Width = Math.Max(0, value.Width - ScrollArea.Padding.Width - topToBottomItemList.Margin.Width - VerticalScrollBar.Width);
                        }
                        else
                        {
                            topToBottomItemList.Width = Math.Max(0, value.Width - ScrollArea.Padding.Width - topToBottomItemList.Margin.Width);
                        }
                    }

                    base.LocalBounds = value;
                    if (Parent != null)
                    {
                        TopLeftOffset = currentTopLeftOffset;
                    }
                    settingLocalBounds = false;
                }
            }
        }

        public override void RemoveChild(int index)
        {
            topToBottomItemList.RemoveChild(index);
        }

        public override void RemoveChild(GuiWidget childToRemove)
        {
            for (int i = topToBottomItemList.Children.Count - 1; i >= 0; i--)
            {
                GuiWidget itemHolder = topToBottomItemList.Children[i];
                if (itemHolder == childToRemove || itemHolder.Children[0] == childToRemove)
                {
                    topToBottomItemList.RemoveChild(itemHolder);
                }
            }
        }

        void itemHolder_ParentChanged(object sender, EventArgs e)
        {
            FlowLayoutWidget itemHolder = (FlowLayoutWidget)sender;
            itemHolder.MouseEnterBounds -= new EventHandler(itemToAdd_MouseEnterBounds);
            itemHolder.MouseLeaveBounds -= new EventHandler(itemToAdd_MouseLeaveBounds);
            itemHolder.MouseDownInBounds -= new MouseEventHandler(itemHolder_MouseDownInBounds);
            itemHolder.ParentChanged -= new EventHandler(itemHolder_ParentChanged);
        }

        void itemHolder_MouseDownInBounds(object sender, MouseEventArgs mouseEvent)
        {
            GuiWidget widgetClicked = ((GuiWidget)sender);
            for (int index = 0; index < topToBottomItemList.Children.Count; index++)
            {
                GuiWidget child = topToBottomItemList.Children[index];
                if (child == widgetClicked)
                {
                    SelectedIndex = index;
                }
            }
        }

        void itemToAdd_MouseLeaveBounds(object sender, EventArgs e)
        {
            GuiWidget widgetLeft = ((GuiWidget)sender);
            if (SelectedIndex >= 0)
            {
                if (widgetLeft != topToBottomItemList.Children[SelectedIndex])
                {
                    widgetLeft.BackgroundColor = new RGBA_Bytes();
                    widgetLeft.Invalidate();
                    Invalidate();
                }
            }
        }

        void itemToAdd_MouseEnterBounds(object sender, EventArgs e)
        {
            GuiWidget widgetEntered = ((GuiWidget)sender);
            for(int index = 0; index < topToBottomItemList.Children.Count; index++)
            {
                if (index != SelectedIndex)
                {
                    GuiWidget child = topToBottomItemList.Children[index];
                    if (child == widgetEntered)
                    {
                        HoverIndex = index;
                    }
                }
            }
        }

        public void OnSelectedIndexChanged()
        {
            Invalidate();
            if (SelectedValueChanged != null)
            {
                SelectedValueChanged(this, null);
            }
        }

        public void OnHoverIndexChanged()
        {
            Invalidate();
            if (HoverValueChanged != null)
            {
                HoverValueChanged(this, null);
            }
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            //activeView.OnDraw(graphics2D);

            base.OnDraw(graphics2D);
        }

        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            base.OnMouseDown(mouseEvent);
        }

        public override void OnMouseUp(MouseEventArgs mouseEvent)
        {
            base.OnMouseUp(mouseEvent);
        }

        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            base.OnMouseMove(mouseEvent);
        }

        public void ClearSelected()
        {
            if (selectedIndex != -1)
            {
                selectedIndex = -1;
                OnSelectedIndexChanged();
            }
        }

        public GuiWidget SelectedItem 
        {
            get
            {
                if (SelectedIndex != -1)
                {
                    return Children[SelectedIndex];
                }

                return null;
            }

            set
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    if (Children[SelectedIndex] == value)
                    {
                        SelectedIndex = i;
                    }
                }
            }
        }
    }
}
