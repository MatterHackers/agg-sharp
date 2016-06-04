using MatterHackers.VectorMath;
using System;
using System.Collections.ObjectModel;

namespace MatterHackers.Agg.UI
{
	public enum Direction { Up, Down };

	public class Menu : Button
	{
		private double maxHeight;

		public bool AlignToRightEdge { get; set; }

		public Vector2 OpenOffset { get; set; }

		public bool IsOpen { get; private set; } = false;

		public virtual BorderDouble MenuItemsPadding { get; set; }

		public Direction MenuDirection { get; private set; }

		public RGBA_Bytes MenuItemsBorderColor { get; set; }

		public RGBA_Bytes MenuItemsBackgroundColor { get; set; } = RGBA_Bytes.White;

		public RGBA_Bytes MenuItemsBackgroundHoverColor { get; set; } = RGBA_Bytes.Gray;

		public RGBA_Bytes MenuItemsTextColor { get; set; } = RGBA_Bytes.Black;

		public RGBA_Bytes MenuItemsTextHoverColor { get; set; } = RGBA_Bytes.Black;

		public int MenuItemsBorderWidth { get; set; } = 1;

		public ObservableCollection<MenuItem> MenuItems = new ObservableCollection<MenuItem>();

		// If max height is > 0 it will limit the height of the menu
		public Menu(Direction direction = Direction.Down, double maxHeight = 0)
		{
			this.maxHeight = maxHeight;
			this.MenuDirection = direction;
			this.VAnchor = VAnchor.FitToChildren;
			this.HAnchor = HAnchor.FitToChildren;
			this.Click += (s, e) =>
			{
				if (!mouseDownWhileOpen)
				{
					if (MenuItems.Count > 0)
					{
						UiThread.RunOnIdle(ShowMenu);
						IsOpen = true;
					}
				}
			};
		}

		// If max height is > 0 it will limit the height of the menu
		public Menu(GuiWidget view, Direction direction = Direction.Down, double maxHeight = 0)
			: this(direction, maxHeight)
		{
			AddChild(view);
		}

		private bool mouseDownWhileOpen = false;

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

		internal OpenMenuContents DropDownContainer = null;

		protected virtual void ShowMenu()
		{
			if (this.Parent == null)
			{
				throw new Exception("You cannot show the menu on a Menu unless it has a parent (has been added to a GuiWidget).");
			}

			DropDownContainer = new OpenMenuContents(MenuItems, this, OpenOffset, MenuDirection, MenuItemsBackgroundColor, MenuItemsBorderColor, MenuItemsBorderWidth, maxHeight, AlignToRightEdge);
			DropDownContainer.Closed += new EventHandler(DropListItems_Closed);
			DropDownContainer.Focus();
		}

		public void AddHorizontalLine()
		{
			MenuItem menuItem = new MenuItem(new GuiWidget(HAnchor.ParentLeftRight, VAnchor.AbsolutePosition)
			{
				Height = 2,
				BackgroundColor = RGBA_Bytes.Gray,
				Margin = new BorderDouble(3, 1),
				VAnchor = VAnchor.ParentCenter,
			}, "HorizontalLine");
			MenuItems.Add(menuItem);
		}

		virtual protected void DropListItems_Closed(object sender, EventArgs e)
		{
			OpenMenuContents dropListItems = (OpenMenuContents)sender;
			dropListItems.Closed -= DropListItems_Closed;
			IsOpen = false;

			DropDownContainer = null;
		}
	}
}