/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.Collections.ObjectModel;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public enum Direction { Up, Down };

	public class Menu : Button
	{
		private double maxHeight;

		public bool AlignToRightEdge { get; set; }

		public Vector2 OpenOffset { get; set; }

		public bool IsOpen { get; private set; } = false;

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

		public override void OnClick(MouseEventArgs mouseEvent)
		{
			if (!mouseDownWhileOpen)
			{
				if (MenuItems.Count > 0)
				{
					UiThread.RunOnIdle(ShowMenu);
					IsOpen = true;
				}
			}

			base.OnClick(mouseEvent);
		}

		internal PopupMenu DropDownContainer = null;

		protected virtual void ShowMenu()
		{
			if (this.Parent == null)
			{
				throw new Exception("You cannot show the menu on a Menu unless it has a parent (has been added to a GuiWidget).");
			}

			var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Name = "_topToBottom",
			};

			foreach (MenuItem menu in MenuItems)
			{
				menu?.ClearRemovedFlag();
				topToBottom.AddChild(menu);
			}

			DropDownContainer = new PopupMenu(MenuItems, topToBottom, this, OpenOffset, MenuDirection, maxHeight, AlignToRightEdge, true)
			{
				BorderWidth = MenuItemsBorderWidth,
				BorderColor = MenuItemsBorderColor,
				BackgroundColor = MenuItemsBackgroundColor
			};

			DropDownContainer.Closed += DropListItems_Closed;
			DropDownContainer.Focus();
		}

		public MenuItem AddHorizontalLine()
		{
			var menuItem = new MenuItem(new GuiWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				Height = 1,
				BackgroundColor = RGBA_Bytes.LightGray,
				Margin = new BorderDouble(10, 1),
				VAnchor = VAnchor.ParentCenter,
			}, "HorizontalLine");
			MenuItems.Add(menuItem);

			return menuItem;
		}

		virtual protected void DropListItems_Closed(object sender, ClosedEventArgs e)
		{
			PopupMenu dropListItems = (PopupMenu)sender;
			dropListItems.Closed -= DropListItems_Closed;
			IsOpen = false;

			DropDownContainer = null;
		}
	}
}