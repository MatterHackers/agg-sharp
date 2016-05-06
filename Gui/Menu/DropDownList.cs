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

using MatterHackers.Agg.Image;
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

		public RGBA_Bytes TextColor
		{
			get
			{
				return mainControlText.TextColor;
			}
			set
			{
				mainControlText.TextColor = value;
			}
		}

		private PathStorage directionArrow = null;

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

		public string SelectedLabel
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
					selectedIndex = 0;
				}
			}
		}

		public string GetValue(int itemIndex)
		{
			if (itemIndex < 0 || itemIndex >= MenuItems.Count)
			{
				return "";
			}

			// Return the Value property of the selected MenuItem
			return MenuItems[itemIndex].Value;
		}

		public string SelectedValue
		{
			get { return GetValue(SelectedIndex); }
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

					// Not found, reset to "-Default-"
					index = 0;
				}
			}
		}

		public DropDownList(string noSelectionString, RGBA_Bytes normalColor, RGBA_Bytes hoverColor, Direction direction = Direction.Down, double maxHeight = 0, bool useLeftIcons = false)
			: base(direction, maxHeight)
		{
			UseLeftIcons = useLeftIcons;
			if (this.MenuDirection == Direction.Down)
			{
				directionArrow = new PathStorage();
				directionArrow.MoveTo(-4, 0);
				directionArrow.LineTo(4, 0);
				directionArrow.LineTo(0, -5);
			}
			else if (this.MenuDirection == Direction.Up)
			{
				directionArrow = new PathStorage();
				directionArrow.MoveTo(-4, -5);
				directionArrow.LineTo(4, -5);
				directionArrow.LineTo(0, 0);
			}
			else
			{
				throw new NotImplementedException("Pulldown direction has not been implemented");
			}

			MenuItems.CollectionChanged += MenuItems_CollectionChanged;

			mainControlText = new TextWidget(noSelectionString)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.ParentBottom | VAnchor.FitToChildren,
				HAnchor = HAnchor.ParentLeft | HAnchor.FitToChildren,
				Margin = new BorderDouble(right: 30),
				TextColor = RGBA_Bytes.Black
			};
			AddChild(mainControlText);

			MouseEnter += (s, e) => BackgroundColor = HoverColor;
			MouseLeave += (s, e) => BackgroundColor = NormalColor;

			NormalColor = normalColor;
			HoverColor = hoverColor;
			BackgroundColor = normalColor;
			BorderColor = RGBA_Bytes.White;
		}

		private void OnSelectionChanged(EventArgs e)
		{
			SelectionChanged?.Invoke(this, e);
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
			}
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			// Set new MinSIze
			Vector2 minSize = new Vector2(LocalBounds.Width, LocalBounds.Height);
			foreach (MenuItem item in MenuItems)
			{
				item.MinimumSize = new Vector2(LocalBounds.Width, LocalBounds.Height);
			}

			base.OnBoundsChanged(e);
		}

		private void MenuItem_Clicked(object sender, EventArgs e)
		{
			int newSelectedIndex = MenuItems.IndexOf(sender as MenuItem);
			SelectedIndex = newSelectedIndex == -1 ? 0 : newSelectedIndex;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			// Draw border
			var strokeRect = new Stroke(new RoundedRect(this.LocalBounds, 0), BorderWidth);
			graphics2D.Render(strokeRect, BorderColor);

			// Draw directional arrow
			if (directionArrow != null)
			{
				graphics2D.Render(directionArrow, LocalBounds.Right - 8, LocalBounds.Top - 4, BorderColor);
			}
		}

		public bool UseLeftIcons { get; private set; } = false;

		public MenuItem AddItem(string itemName, string itemValue = null, double pointSize = 12, EventHandler clickAction = null)
		{
			if (itemValue == null)
			{
				itemValue = itemName;
			}
			if (mainControlText.Text != "")
			{
				mainControlText.Margin = MenuItemsPadding;
			}

			BorderDouble currentPadding = MenuItemsPadding;
			if (UseLeftIcons)
			{
				currentPadding = new BorderDouble(MenuItemsPadding.Left + 20 + 3, MenuItemsPadding.Bottom, MenuItemsPadding.Right, MenuItemsPadding.Top);
			}

			MenuItem menuItem = new MenuItem(new MenuItemColorStatesView(itemName)
			{
				NormalBackgroundColor = MenuItemsBackgroundColor,
				OverBackgroundColor = MenuItemsBackgroundHoverColor,

				NormalTextColor = MenuItemsTextColor,
				OverTextColor = MenuItemsTextHoverColor,
				DisabledTextColor = RGBA_Bytes.Gray,

				PointSize = pointSize,
				Padding = currentPadding,
			}, itemValue);
			menuItem.Text = itemName;

			// MenuItem is a long lived object that is added and removed to new containers whenever the
			// menu is shown. To ensure that event registration is not duplicated, always remove before add
			menuItem.Selected -= MenuItem_Clicked;
			menuItem.Selected += MenuItem_Clicked;

			menuItem.Name = itemName + " Menu Item";
			if(clickAction != null)
			{
				menuItem.Selected += clickAction;
			}
			MenuItems.Add(menuItem);

			return menuItem;
		}

		public MenuItem AddItem(ImageBuffer leftImage, string itemName, string itemValue = null, double pointSize = 12)
		{
			mainControlText.Margin = MenuItemsPadding;

			GuiWidget normalTextWithMargin = GetMenuContent(itemName, leftImage, MenuItemsBackgroundColor);
			GuiWidget hoverTextWithMargin = GetMenuContent(itemName, leftImage, MenuItemsBackgroundHoverColor);

			MenuItem menuItem = new MenuItem(
				new MenuItemStatesView(normalTextWithMargin, hoverTextWithMargin),
				// If no itemValue is supplied, itemName will be used as the item value
				itemValue ?? itemName);

			// MenuItem is a long lived object that is added and removed to new containers whenever the
			// menu is shown. To ensure that event registration is not duplicated, always remove before add
			menuItem.Selected -= MenuItem_Clicked;
			menuItem.Selected += MenuItem_Clicked;

			menuItem.Name = itemName + " Menu Item";
			menuItem.Text = itemName;
			MenuItems.Add(menuItem);

			return menuItem;
		}

		private GuiWidget GetMenuContent(string itemName, ImageBuffer leftImage, RGBA_Bytes color)
		{
			var rowContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight | HAnchor.FitToChildren,
				VAnchor = VAnchor.FitToChildren,
				BackgroundColor = color
			};

			var textWidget = new TextWidget(itemName)
			{
				Margin = MenuItemsPadding,
				TextColor = MenuItemsTextColor
			};

			if (UseLeftIcons)
			{
				if (leftImage != null)
				{
					ImageBuffer scaledImage = ImageBuffer.CreateScaledImage(leftImage, 20, 20);
					rowContainer.AddChild(new ImageWidget(scaledImage)
					{
						VAnchor = VAnchor.ParentCenter,
						Margin = new BorderDouble(MenuItemsPadding.Left, MenuItemsPadding.Bottom, 3, MenuItemsPadding.Top),
					});
					textWidget.Margin = new BorderDouble(0, MenuItemsPadding.Bottom, MenuItemsPadding.Right, MenuItemsPadding.Top);
				}
				else
				{
					textWidget.Margin = new BorderDouble(MenuItemsPadding.Left + 20 + 3, MenuItemsPadding.Bottom, MenuItemsPadding.Right, MenuItemsPadding.Top);
				}
			}
			rowContainer.AddChild(textWidget);

			return rowContainer;
		}
	}
}