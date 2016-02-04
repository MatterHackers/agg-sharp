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

using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Collections.Specialized;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	public class DropDownList : Menu
	{
		public event EventHandler SelectionChanged;

		protected TextWidget mainControlText;

		public RGBA_Bytes NormalColor { get; set; }

		public int BorderWidth { get; set; }

		public RGBA_Bytes BorderColor { get; set; }

		public RGBA_Bytes HoverColor { get; set; }

		private RGBA_Bytes textColor = RGBA_Bytes.Black;

		public RGBA_Bytes TextColor
		{
			get { return textColor; }
			set
			{
				textColor = value;
				mainControlText.TextColor = TextColor;
			}
		}

		private int selectedIndex = -1;

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

		protected override void ShowMenu()
		{
			base.ShowMenu();

			if (selectedIndex >= 0)
			{
				var selectedMenuItem = MenuItems[selectedIndex];

				// Scroll the selected item into view
				DropDownContainer.ScrollIntoView(selectedMenuItem);

				// Highlight the selected item
				var statesView = selectedMenuItem.Children<MenuItemStatesView>().FirstOrDefault();
				if (statesView != null)
				{
					statesView.Highlighted = true;
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

				// Return the text property of the selected MenuItem
				return MenuItems[SelectedIndex].Text;
			}
			set
			{
				if (SelectedIndex == -1 || SelectedLabel != value)
				{
					int index = 0;
					foreach (MenuItem item in MenuItems)
					{
						// If the items .Text property matches the passed in value, change the current SelectedIndex
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

				// Return the Value property of the selected MenuItem
				return MenuItems[SelectedIndex].Value;
			}
			set
			{
				if (SelectedIndex == -1 || SelectedValue != value)
				{
					int index = 0;
					foreach (MenuItem item in MenuItems)
					{
						// If the items .Value property matches the passed in value, change the current SelectedIndex
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

		private void DropDownList_MouseLeave(object sender, EventArgs e)
		{
			BackgroundColor = NormalColor;
		}

		private void DropDownList_MouseEnter(object sender, EventArgs e)
		{
			BackgroundColor = HoverColor;
		}

		private void OnSelectionChanged(EventArgs e)
		{
			if (SelectionChanged != null)
			{
				SelectionChanged(this, e);
			}
		}

		private void MenuItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
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

		private void SetMenuItemsToNewMinSIze()
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

		private void item_Selected(object sender, EventArgs e)
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
			graphics2D.Render(littleArrow, LocalBounds.Right - 8, LocalBounds.Top - 4, BorderColor);
		}

		private void DrawBorder(Graphics2D graphics2D)
		{
			RectangleDouble Bounds = LocalBounds;
			RoundedRect borderRect = new RoundedRect(this.LocalBounds, 0);
			Stroke strokeRect = new Stroke(borderRect, BorderWidth);
			graphics2D.Render(strokeRect, BorderColor);
		}

		public MenuItem AddItem(string itemName, string itemValue = null)
		{
			mainControlText.Margin = MenuItemsPadding;

			GuiWidget normalTextWithMargin = new GuiWidget();
			normalTextWithMargin.HAnchor = UI.HAnchor.ParentLeftRight | UI.HAnchor.FitToChildren;
			normalTextWithMargin.VAnchor = UI.VAnchor.FitToChildren;
			normalTextWithMargin.BackgroundColor = MenuItemsBackgroundColor;
			TextWidget normal = new TextWidget(itemName);
			normal.Margin = MenuItemsPadding;
			normal.TextColor = MenuItemsTextColor;
			normalTextWithMargin.AddChild(normal);

			GuiWidget hoverTextWithMargin = new GuiWidget();
			hoverTextWithMargin.HAnchor = UI.HAnchor.ParentLeftRight | UI.HAnchor.FitToChildren;
			hoverTextWithMargin.VAnchor = UI.VAnchor.FitToChildren;
			hoverTextWithMargin.BackgroundColor = MenuItemsBackgroundHoverColor;
			TextWidget hover = new TextWidget(itemName);
			hover.Margin = MenuItemsPadding;
			hover.TextColor = MenuItemsTextHoverColor;
			hoverTextWithMargin.AddChild(hover);


			MenuItem menuItem = new MenuItem(
				new MenuItemStatesView(normalTextWithMargin, hoverTextWithMargin),
				// If no itemValue is supplied, itemName will be used as the item value
				itemValue ?? itemName);

			menuItem.Name = itemName + " Menu Item";
			menuItem.Text = itemName;
			MenuItems.Add(menuItem);

			return menuItem;
		}
	}
}