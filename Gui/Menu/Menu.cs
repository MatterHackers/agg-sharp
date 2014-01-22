using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
    public enum Direction { Up, Down };

    public class Menu : Button
    {
        public Vector2 OpenOffset { get; set; }
        bool menuIsOpen = false;
        public bool IsOpen { get { return menuIsOpen; } }
        Direction menuDirection;
        public ObservableCollection<MenuItem> MenuItems = new ObservableCollection<MenuItem>();

        public BorderDouble MenuItemsPadding { get; set; }

        RGBA_Bytes menuItemsBackgroundColor = RGBA_Bytes.White;
        public RGBA_Bytes MenuItemsBackgroundColor
        {
            get { return menuItemsBackgroundColor; }
            set { menuItemsBackgroundColor = value; }
        }

        public Direction MenuDirection { get {return menuDirection;}}

        RGBA_Bytes menuItemsHoverColor = RGBA_Bytes.Gray;
        public RGBA_Bytes MenuItemsBackgroundHoverColor
        {
            get { return menuItemsHoverColor; }
            set { menuItemsHoverColor = value; }
        }

        RGBA_Bytes menuItemsTextColor = RGBA_Bytes.Black;
        public RGBA_Bytes MenuItemsTextColor
        {
            get { return menuItemsTextColor; }
            set { menuItemsTextColor = value; }
        }

        RGBA_Bytes menuItemsTextHoverColor = RGBA_Bytes.Black;
        public RGBA_Bytes MenuItemsTextHoverColor
        {
            get { return menuItemsTextHoverColor; }
            set { menuItemsTextHoverColor = value; }
        }

        RGBA_Bytes menuItemsBorderColor;
        public RGBA_Bytes MenuItemsBorderColor
        {
            get { return menuItemsBorderColor; }
            set { menuItemsBorderColor = value; }
        }

        int borderWidth = 1;
        public int MenuItemsBorderWidth { get { return borderWidth; } set { borderWidth = value; } }

        public Menu(Direction direction = Direction.Down)
        {
            this.menuDirection = direction;
            Click += new ButtonEventHandler(ListMenu_Click);
            VAnchor = UI.VAnchor.FitToChildren;
            HAnchor = UI.HAnchor.FitToChildren;
        }

        public Menu(GuiWidget view, Direction direction = Direction.Down)
            : this(direction)
        {
            AddChild(view);
        }

        bool mouseDownWhileOpen = false;
        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            if (IsOpen)
            {
                mouseDownWhileOpen = true;
            }
            else
            {
                mouseDownWhileOpen = false;
            }
            base.OnMouseDown(mouseEvent);
        }

        void ListMenu_Click(object sender, MouseEventArgs mouseEvent)
        {
            if (mouseDownWhileOpen)
            {
            }
            else
            {
                UiThread.RunOnIdle(ShowMenu);
                menuIsOpen = true;
            }
        }

        void ShowMenu(object state)
        {
            if (this.Parent == null)
            {
                throw new Exception("You cannot show the menu on a Menu unless it has a parent (has been added to a GuiWidegt).");
            }

            OpenMenuContents dropListItems = new OpenMenuContents(MenuItems, this, OpenOffset, menuDirection, MenuItemsBackgroundColor, MenuItemsBorderColor, MenuItemsBorderWidth);
            dropListItems.Closed += new EventHandler(dropListItems_Closed);

            dropListItems.Focus();
        }

        void dropListItems_Closed(object sender, EventArgs e)
        {
            OpenMenuContents dropListItems = (OpenMenuContents)sender;
            dropListItems.Closed -= new EventHandler(dropListItems_Closed);
            menuIsOpen = false;
        }
    }
}
