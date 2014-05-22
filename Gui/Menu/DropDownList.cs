using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.VectorMath;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
    public class DropDownList : Menu
    {
        public event EventHandler SelectionChanged;
        protected TextWidget mainControlText;

        public override RectangleDouble LocalBounds
        {
            get
            {
                return base.LocalBounds;
            }
            set
            {
                base.LocalBounds = value;
            }
        }

        RGBA_Bytes normalColor;
        public RGBA_Bytes NormalColor
        {
            get { return normalColor; }
            set { normalColor = value; }
        }

        int borderWidth = 0;
        public int BorderWidth
        {
            get { return borderWidth; }
            set { borderWidth = value; }
        }

        RGBA_Bytes borderColor;
        public RGBA_Bytes BorderColor
        {
            get { return borderColor; }
            set { borderColor = value; }
        }

        RGBA_Bytes hoverColor;
        public RGBA_Bytes HoverColor
        {
            get { return hoverColor; }
            set { hoverColor = value; }
        }

        RGBA_Bytes textColor = RGBA_Bytes.Black;
        public RGBA_Bytes TextColor
        {
            get { return textColor; }
            set 
            {
                textColor = value;
                mainControlText.TextColor = TextColor;
            }
        }

        int selectedIndex = -1;
        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (SelectedIndex != value)
                {
                    selectedIndex = value;
                    mainControlText.Text = MenuItems[SelectedIndex].Text;
                    OnSelectionChanged(null);
                    Invalidate();
                }
            }
        }

        public String SelectedLabel
        {
            get 
            {
                if (SelectedIndex < 0 || SelectedIndex >= MenuItems.Count)
                {
                    return "";
                }
                return GetValue(SelectedIndex); 
            }
            set
            {
                if (SelectedIndex == -1 || SelectedLabel != value)
                {
                    int index = 0;
                    foreach (MenuItem item in MenuItems)
                    {
                        if (item.Text == value)
                        {
                            SelectedIndex = index;
                            return;
                        }
                        index++;
                    }
                    throw new Exception("The label you specified '{0}' is not in the drop down list.".FormatWith(value));
                }
            }
        }

        public String SelectedValue
        {
            get
            {
                if (SelectedIndex < 0 || SelectedIndex >= MenuItems.Count)
                {
                    return "";
                }
                return GetValue(SelectedIndex);
            }
            set
            {
                if (SelectedIndex == -1 || SelectedValue != value)
                {
                    int index = 0;
                    foreach (MenuItem item in MenuItems)
                    {
                        if (item.Value == value)
                        {
                            SelectedIndex = index;
                            return;
                        }
                        index++;
                    }
                    throw new Exception("The value you specified '{0}' is not in the drop down list.".FormatWith(value));
                }
            }
        }



        public string GetValue(int itemIndex)
        {
            return MenuItems[itemIndex].Value;
        }

        public DropDownList(string noSelectionString, RGBA_Bytes normalColor, RGBA_Bytes hoverColor, Direction direction = Direction.Down, double maxHeight = 0)
            : base(direction, maxHeight)
        {
            MenuItems.CollectionChanged += new NotifyCollectionChangedEventHandler(MenuItems_CollectionChanged);
            mainControlText = new TextWidget(noSelectionString);
            mainControlText.AutoExpandBoundsToText = true;
            mainControlText.VAnchor = UI.VAnchor.ParentBottom | UI.VAnchor.FitToChildren;
            mainControlText.HAnchor = UI.HAnchor.ParentLeft | UI.HAnchor.FitToChildren;
            mainControlText.Margin = new BorderDouble(right: 30);
            AddChild(mainControlText);

            MouseEnter += new EventHandler(DropDownList_MouseEnter);
            MouseLeave += new EventHandler(DropDownList_MouseLeave);

            NormalColor = normalColor;
            HoverColor = hoverColor;
            BackgroundColor = normalColor;
            BorderColor = RGBA_Bytes.White;
        }

        void DropDownList_MouseLeave(object sender, EventArgs e)
        {
            BackgroundColor = NormalColor;
        }

        void DropDownList_MouseEnter(object sender, EventArgs e)
        {
            BackgroundColor = HoverColor;
        }

        void OnSelectionChanged(EventArgs e)
        {
            if (SelectionChanged != null)
            {
                SelectionChanged(this, e);
            }
        }

        void MenuItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Vector2 minSize = new Vector2(LocalBounds.Width, LocalBounds.Height);

            string startText = mainControlText.Text;
            foreach (MenuItem item in MenuItems)
            {
                mainControlText.Text = item.Text;

                minSize.x = Math.Max(minSize.x, LocalBounds.Width);
                minSize.y = Math.Max(minSize.y, LocalBounds.Height);
            }
            mainControlText.Text = startText;

            this.MinimumSize = minSize;

            foreach (MenuItem item in e.NewItems)
            {
                item.MinimumSize = minSize;
                item.Selected -= new EventHandler(item_Selected);
                item.Selected += new EventHandler(item_Selected);
            }
        }

        void SetMenuItemsToNewMinSIze()
        {
            Vector2 minSize = new Vector2(LocalBounds.Width, LocalBounds.Height);

            foreach (MenuItem item in MenuItems)
            {
                item.MinimumSize = new Vector2(LocalBounds.Width, LocalBounds.Height);
            }
        }

        public override void OnBoundsChanged(EventArgs e)
        {
            SetMenuItemsToNewMinSIze();
            base.OnBoundsChanged(e);
        }

        void item_Selected(object sender, EventArgs e)
        {
            int newSelectedIndex = 0;
            foreach (MenuItem item in MenuItems)
            {
                if (item == sender)
                {
                    break;
                }
                newSelectedIndex++;
            }

            SelectedIndex = newSelectedIndex;
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            base.OnDraw(graphics2D);
            this.DrawBorder(graphics2D);
            this.DrawDirectionalArrow(graphics2D);
        }

        private void DrawDirectionalArrow(Graphics2D graphics2D)
        {
            PathStorage littleArrow = new PathStorage();
            if (this.MenuDirection == Direction.Down)
            {
                littleArrow.MoveTo(-4, 0);
                littleArrow.LineTo(4, 0);
                littleArrow.LineTo(0, -5);
            }
            else if (this.MenuDirection == Direction.Up)
            {
                littleArrow.MoveTo(-4, -5);
                littleArrow.LineTo(4, -5);
                littleArrow.LineTo(0, 0);
            }
            else
            {
                throw new NotImplementedException("Pulldown direction has not been implemented");
            }
            graphics2D.Render(littleArrow, LocalBounds.Right - 8, LocalBounds.Top - 4, borderColor);
        }

        private void DrawBorder(Graphics2D graphics2D)
        {
            RectangleDouble Bounds = LocalBounds;
            RoundedRect borderRect = new RoundedRect(this.LocalBounds, 0);
            Stroke strokeRect = new Stroke(borderRect, borderWidth);
            graphics2D.Render(strokeRect, borderColor);
        }

        public MenuItem AddItem(string name, string value = null)
        {
            if (value == null)
            {
                value = name;
            }
            
            mainControlText.Margin = MenuItemsPadding;

            GuiWidget normalTextWithMargin = new GuiWidget();
            normalTextWithMargin.HAnchor = UI.HAnchor.ParentLeftRight | UI.HAnchor.FitToChildren;
            normalTextWithMargin.VAnchor = UI.VAnchor.FitToChildren;
            normalTextWithMargin.BackgroundColor = MenuItemsBackgroundColor;
            TextWidget normal = new TextWidget(name);
            normal.Margin = MenuItemsPadding;
            normal.TextColor = MenuItemsTextColor;
            normalTextWithMargin.AddChild(normal);

            GuiWidget hoverTextWithMargin = new GuiWidget();
            hoverTextWithMargin.HAnchor = UI.HAnchor.ParentLeftRight | UI.HAnchor.FitToChildren;
            hoverTextWithMargin.VAnchor = UI.VAnchor.FitToChildren;
            hoverTextWithMargin.BackgroundColor = MenuItemsBackgroundHoverColor;
            TextWidget hover = new TextWidget(name);
            hover.Margin = MenuItemsPadding;
            hover.TextColor = MenuItemsTextHoverColor;
            hoverTextWithMargin.AddChild(hover);

            MenuItem menuItem = new MenuItem(new MenuItemStatesView(normalTextWithMargin, hoverTextWithMargin), value);
            menuItem.Text = name;
            MenuItems.Add(menuItem);

            return menuItem;
        }
    }
}

