using MatterHackers.Agg.UI;

using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
	public class MenuPage : TabPage
	{
		private TextWidget lastAction;

		public MenuPage()
			: base("Menu Controls")
		{
			//GuiWidget.DebugBoundsUnderMouse = true;
			BackgroundColor = Color.Green;
			lastAction = new TextWidget("Last Menu Action");
			lastAction.OriginRelativeParent = new Vector2(100, 250);
			AddChild(lastAction);

			Menu dropListMenu = new Menu(new TextWidget("gAction v"));
			dropListMenu.Name = "ListMenu Down";
			AddMenu(dropListMenu, "Walk");
			AddMenu(dropListMenu, "Jog");
			AddMenu(dropListMenu, "Run");
			dropListMenu.OriginRelativeParent = new Vector2(100, 200);
			AddChild(dropListMenu);

			Menu longMenue = new Menu(new TextWidget("very long"), maxHeight: 50);
			longMenue.Name = "ListMenu Down";
			for (int i = 0; i < 30; i++)
			{
				AddMenu(longMenue, "menu {0}".FormatWith(i));
			}
			longMenue.OriginRelativeParent = new Vector2(100, 300);
			AddChild(longMenue);

			Menu raiseListMenu = new Menu(new TextWidget("jAction ^"), Direction.Up);
			raiseListMenu.Name = "ListMenu Up";
			AddMenu(raiseListMenu, "Walk");
			AddMenu(raiseListMenu, "Jog");
			AddMenu(raiseListMenu, "Run");
			raiseListMenu.OriginRelativeParent = new Vector2(200, 200);
			AddChild(raiseListMenu);

			FlowLayoutWidget mainMenuBar = new FlowLayoutWidget();
			mainMenuBar.VAnchor = UI.VAnchor.Top;

			GuiWidget popupMenu = new TextWidget("Simple Menu");
			popupMenu.OriginRelativeParent = new Vector2(100, 100);
			//AddChild(popupMenu);

			var dropDownList = new DropDownList("- Select Something -", Color.Black)
			{
				HoverColor = Color.Gray
			};

			dropDownList.BackgroundColor = Color.Black;
			dropDownList.TextColor = Color.White;

			dropDownList.MenuItemsPadding = new BorderDouble(13);
			dropDownList.MenuItemsBorderWidth = 3;
			dropDownList.MenuItemsBorderColor = Color.Red;
			dropDownList.MenuItemsBackgroundColor = Color.Blue;
			dropDownList.MenuItemsTextColor = Color.White;
			dropDownList.MenuItemsTextHoverColor = Color.Yellow;

			dropDownList.Name = "Drop Down List";
			dropDownList.AddItem("Item 1", "value1");
			dropDownList.AddItem("Item 2 long name");
			dropDownList.AddItem("Item 3 realy long name");
			dropDownList.OriginRelativeParent = new Vector2(300, 200);
			AddChild(dropDownList);
		}

		private void AddMenu(Menu listMenuToAddTo, string name)
		{
			GuiWidget normal = new TextWidget(name);
			normal.BackgroundColor = Color.White;
			GuiWidget hover = new TextWidget(name);
			hover.BackgroundColor = Color.LightGray;
			MenuItem menuItem = new MenuItem(new MenuItemStatesView(normal, hover));
			menuItem.Selected += (sender, e) => { lastAction.Text = name; };
			listMenuToAddTo.MenuItems.Add(menuItem);
		}
	}
}