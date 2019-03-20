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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
	public class DropDownList : Menu
	{
		public event EventHandler SelectionChanged;

		/// <summary>
		/// Filter text captured while the droplist is open. Cleared when the menu is closed
		/// </summary>
		private StringBuilder listFilterText = null;

		/// <summary>
		/// The currently highlighted item matching the listFilterText
		/// </summary>
		private MenuItem highlightedItem;

		/// <summary>
		/// The MenuItems Index of the highlighted item
		/// </summary>
		private int highLightedIndex = 0;

		private Color lastRenderColor;

		private bool mouseInBounds;
		private Color disabledBorderColor;

		private int gradientDistance = 8;

		private RectangleDouble dropArrowBounds;

		protected TextWidget mainControlText;

		public Color NormalColor { get; set; }

		public Color HoverColor { get; set; }

		public Color TextColor
		{
			get => mainControlText.TextColor;
			set => mainControlText.TextColor = value;
		}

		private VertexStorage directionArrow = null;

		public BorderDouble MenuItemsPadding
		{
			get => mainControlText.Margin;
			set
			{
				// Ensure minimum right margin
				mainControlText.Margin = new BorderDouble(value.Left, value.Bottom, Math.Max(value.Right, 30), value.Top);
			}
		}

		public bool AutoScaleIcons { get; set; } = true;

		private int selectedIndex = -1;
		public int SelectedIndex
		{
			get => selectedIndex;
			set
			{
				if (SelectedIndex != value)
				{
					selectedIndex = value;

					mainControlText.Text = selectedIndex == -1 ? this.noSelectionString : MenuItems[SelectedIndex].Text;
					OnSelectionChanged(null);
					Invalidate();
				}
			}
		}

		protected override void ShowMenu()
		{
			menuVisible = true;

			base.ShowMenu();

			if (selectedIndex >= MenuItems.Count - 1)
			{
				selectedIndex = MenuItems.Count - 1;
			}

			// Show and highlight the previously selected or the first item
			if (selectedIndex >= 0)
			{
				var selectedMenuItem = MenuItems[selectedIndex];

				// Scroll the selected item into view
				DropDownContainer.ScrollIntoView(selectedMenuItem);

				// Highlight the selected item
				highlightedItem = selectedMenuItem;
				highlightedItem.ModifyStatesView(showHighlight: true);
			}
			else if (MenuItems.Count > 0)
			{
				highlightedItem = MenuItems[0];
				highlightedItem?.ModifyStatesView(showHighlight: true);
			}

			listFilterText = new StringBuilder();

			DropDownContainer.KeyPressed += (s, e) =>
			{
				listFilterText.Append(e.KeyChar);
				ApplyFilter();
			};

			DropDownContainer.KeyDown += (s, e) =>
			{
				if(e.Handled)
				{
					return;
				}

				switch (e.KeyCode)
				{
					case Keys.Up:
						if (highLightedIndex > 0)
						{
							highLightedIndex--;
						}
						e.Handled = true;
						break;

					case Keys.Down:
						if (highLightedIndex < this.MenuItems.Count - 1)
						{
							highLightedIndex++;
						}
						e.Handled = true;
						break;
				}

				// Remove existing
				highlightedItem?.ModifyStatesView(showHighlight: false);

				highlightedItem = this.MenuItems[highLightedIndex];
				highlightedItem?.ModifyStatesView(showHighlight: true);

				// if (!highlightedItem.Visible) // only scroll if clipped
				{
					DropDownContainer.ScrollIntoView(highlightedItem);
				}

				this.Invalidate();
			};

			DropDownContainer.KeyDown += (s, keyEvent) =>
			{
				switch (keyEvent.KeyCode)
				{
					case Keys.Escape:
						listFilterText = new StringBuilder();
						DropDownContainer.CloseMenu();
						break;

					case Keys.Enter:

						if (highlightedItem != null)
						{
							SelectedIndex = MenuItems.IndexOf(highlightedItem);
							DropDownContainer.CloseMenu();
							listFilterText = null;
						}

						break;

					case Keys.Back:
						if (listFilterText != null && listFilterText.Length > 0)
						{
							listFilterText.Length -= 1;
						}

						keyEvent.Handled = true;
						keyEvent.SuppressKeyPress = true;

						ApplyFilter();

						break;
				}
			};

			DropDownContainer.Closed += (s, e) =>
			{
				menuVisible = false;
				mainControlText.Text = selectedIndex == -1 ? this.noSelectionString : MenuItems[SelectedIndex].Text;
				this.OnMenuClose();
				this.Focus();
			};

			this.OnMenuOpen();
			this.Invalidate();
		}

		protected virtual void OnMenuOpen()
		{
		}

		protected virtual void OnMenuClose()
		{
		}

		private void ApplyFilter()
		{
			string text = listFilterText.ToString();

			mainControlText.Text = text;

			// Remove existing highlight
			highlightedItem?.ModifyStatesView(showHighlight: false);

			// Find menu items starting with the given filter text
			var firstMatchedItem = MenuItems.Where(m => m.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) == 0).FirstOrDefault();
			if (firstMatchedItem != null)
			{
				// Highlight our new match
				highlightedItem = firstMatchedItem;
				highLightedIndex = MenuItems.IndexOf(highlightedItem);
				highlightedItem.ModifyStatesView(showHighlight: true);

				// and scroll into view
				DropDownContainer.ScrollIntoView(highlightedItem);
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

					// If no match is found, mark as such
					selectedIndex = -1;
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

		private string noSelectionString;

		private static Color whiteSemiTransparent = new Color(255, 255, 255, 100);
		private static Color whiteTransparent = new Color(255, 255, 255, 0);

		private double pointSize = 12;
		protected bool menuVisible;

		public DropDownList(string noSelectionString, Color textColor, Direction direction = Direction.Down, double maxHeight = 0, bool useLeftIcons = false, double pointSize = 12)
			: base(direction, maxHeight)
		{
			this.pointSize = pointSize;

			UseLeftIcons = useLeftIcons;

			// Always Down, unless Up
			directionArrow = (this.MenuDirection == Direction.Up) ? DropArrow.UpArrow : DropArrow.DownArrow;

			MenuItems.CollectionChanged += MenuItems_CollectionChanged;

			this.noSelectionString = noSelectionString;

			mainControlText = new TextWidget(noSelectionString, pointSize: pointSize, textColor: textColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Bottom | VAnchor.Fit,
				HAnchor = HAnchor.Left | HAnchor.Fit,
				Margin = new BorderDouble(10, 7, 7, 7),
			};

			AddChild(mainControlText);

			NormalColor = whiteTransparent;
			var borderColor = new Color(textColor, 40);

			this.MenuItemsBorderWidth = 1;
			this.MenuItemsBackgroundColor = Color.White;
			this.MenuItemsBorderColor = borderColor;
			this.MenuItemsPadding = new BorderDouble(10, 7, 7, 7);
			this.MenuItemsBackgroundHoverColor = new Color("#EC6788FF");
			this.MenuItemsTextHoverColor = Color.Black;
			this.MenuItemsTextColor = Color.Black;
			this.HoverColor = whiteSemiTransparent;
			this.BackgroundColor = new Color(255, 255, 255, 0);
			this.Border = 1;
			this.BorderColor = borderColor;
		}

		private void OnSelectionChanged(EventArgs e)
		{
			SelectionChanged?.Invoke(this, e);
		}

		private void MenuItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.NewItems != null)
			{
				Vector2 minSize = new Vector2(LocalBounds.Width, LocalBounds.Height);

				string startText = mainControlText.Text;
				foreach (MenuItem item in MenuItems.ToList())
				{
					mainControlText.Text = item.Text;

					minSize.X = Math.Max(minSize.X, LocalBounds.Width);
					minSize.Y = Math.Max(minSize.Y, LocalBounds.Height);
				}
				mainControlText.Text = startText;

				this.MinimumSize = minSize;

				foreach (MenuItem item in e.NewItems)
				{
					item.MinimumSize = minSize;
				}
			}
		}

		public override Color BorderColor
		{
			get => this.Enabled ? base.BorderColor : disabledBorderColor;
			set
			{
				base.BorderColor = value;
				disabledBorderColor = new Color(value, 30);
			}
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			// Force child menu items to have parent width
			foreach (MenuItem item in MenuItems.ToList())
			{
				item.MinimumSize = new Vector2(LocalBounds.Width, 0);
			}

			dropArrowBounds = new RectangleDouble(LocalBounds.Right - DropArrow.ArrowHeight * 4, 0, LocalBounds.Right, this.Height);

			base.OnBoundsChanged(e);
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = true;
			base.OnMouseEnterBounds(mouseEvent);
			this.Invalidate();
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = false;
			base.OnMouseLeaveBounds(mouseEvent);
			this.Invalidate();
		}

		private void MenuItem_Clicked(object sender, EventArgs e)
		{
			var menuItem = sender as MenuItem;
			if (menuItem.CanHeldSelection)
			{
				int newSelectedIndex = MenuItems.IndexOf(menuItem);
				SelectedIndex = newSelectedIndex == -1 ? 0 : newSelectedIndex;
			}
		}

		Dictionary<Color, ImageBuffer> clippingBackgrounds = new Dictionary<Color, ImageBuffer>();

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			var background = this.BackgroundColor;

			// Retrieve or create per color clipping images used to occlude text under drop arrow
			if (background != Color.Transparent)
			{
				if (!clippingBackgrounds.TryGetValue(background, out ImageBuffer gradientBackground))
				{
					var gradientDistanceMinusBorder = (int)(gradientDistance - Border.Width);

					gradientBackground = agg_basics.TrasparentToColorGradientX(
						(int)(dropArrowBounds.Width + gradientDistanceMinusBorder),
						(int)(this.LocalBounds.Height - Border.Height),
						background,
						gradientDistance);

					lastRenderColor = background;
				}

				graphics2D.Render(gradientBackground, this.LocalBounds.Right - gradientBackground.Width, 0);
			}

			// Draw directional arrow
			if (directionArrow != null)
			{
				var center = dropArrowBounds.Center;
				center.Y += 1;

				graphics2D.Render(directionArrow, center, this.TextColor);
			}
		}

		public override void OnKeyUp(KeyEventArgs keyEvent)
		{
			// this must be called first to ensure we get the correct Handled state
			base.OnKeyUp(keyEvent);

			if (!keyEvent.Handled)
			{
				if (keyEvent.KeyCode == Keys.Down)
				{
					ShowMenu();
				}
			}
		}

		public override void OnFocusChanged(EventArgs e)
		{
			this.Invalidate();
			base.OnFocusChanged(e);
		}

		public bool UseLeftIcons { get; private set; } = false;

		public MenuItem AddItem(string itemName, string itemValue = null)
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

			MenuItem menuItem = new MenuItem(new MenuItemColorStatesView(itemName, MenuItemsTextColor)
			{
				NormalBackgroundColor = MenuItemsBackgroundColor,
				OverBackgroundColor = MenuItemsBackgroundHoverColor,

				NormalTextColor = MenuItemsTextColor,
				OverTextColor = MenuItemsTextHoverColor,
				DisabledTextColor = Color.Gray,

				PointSize = pointSize,
				Padding = currentPadding,
			}, itemValue);
			menuItem.Text = itemName;

			// MenuItem is a long lived object that is added and removed to new containers whenever the
			// menu is shown. To ensure that event registration is not duplicated, always remove before add
			menuItem.Selected -= MenuItem_Clicked;
			menuItem.Selected += MenuItem_Clicked;

			menuItem.Name = itemName + " Menu Item";
			MenuItems.Add(menuItem);

			return menuItem;
		}

		public int GutterWidth { get; set; } = 35;

		public MenuItem AddItem(ImageBuffer leftImage, string itemName, string itemValue = null, int pointSize = 12)
		{
			GuiWidget normalTextWithMargin = GetMenuContent(itemName, leftImage, MenuItemsBackgroundColor, MenuItemsTextColor, pointSize);
			GuiWidget hoverTextWithMargin = GetMenuContent(itemName, leftImage, MenuItemsBackgroundHoverColor, MenuItemsTextHoverColor, pointSize);

			var menuItem = new MenuItem(
				new MenuItemStatesView(normalTextWithMargin, hoverTextWithMargin),
				// If no itemValue is supplied, itemName will be used as the item value
				itemValue ?? itemName)
			{
				Name = itemName + " Menu Item",
				Text = itemName,
			};
			menuItem.Selected += MenuItem_Clicked;

			MenuItems.Add(menuItem);

			return menuItem;
		}

		private GuiWidget GetMenuContent(string itemName, ImageBuffer leftImage, Color color, Color textColor, int pointSize = 12)
		{
			var rowContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch | HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				BackgroundColor = color
			};

			var textWidget = new TextWidget(itemName, pointSize: pointSize)
			{
				Margin = MenuItemsPadding,
				TextColor = textColor,
				VAnchor = VAnchor.Center
			};

			if (UseLeftIcons || leftImage != null)
			{
				if (leftImage != null)
				{
					ImageBuffer imageBuffer = leftImage;

					if (this.AutoScaleIcons)
					{
						int size = (int)(20 * GuiWidget.DeviceScale + .5);
						leftImage.CreateScaledImage(size, size);
					}

					var imageContainer = new GuiWidget()
					{
						Width = this.GutterWidth * GuiWidget.DeviceScale,
						Height = this.GutterWidth * GuiWidget.DeviceScale,
						HAnchor = HAnchor.Absolute,
						VAnchor = VAnchor.Absolute | VAnchor.Center
					};
					rowContainer.AddChild(imageContainer);

					imageContainer.AddChild(new ImageWidget(imageBuffer)
					{
						VAnchor = VAnchor.Center,
						HAnchor = HAnchor.Center,
					});

					textWidget.Margin = 0;
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

	public static class MenuItemExtensions
	{
		public static void ModifyStatesView(this MenuItem menuItem, bool showHighlight)
		{
			var firstChild = menuItem.Children.FirstOrDefault();
			if (firstChild is MenuItemStatesView itemStatesView)
			{
				itemStatesView.Highlighted = showHighlight;
			}
			else if (firstChild is MenuItemColorStatesView colorStatesView)
			{
				colorStatesView.Highlighted = showHighlight;
			}

			firstChild?.Invalidate();
		}
	}
}