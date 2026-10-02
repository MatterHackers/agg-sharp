/*
Copyright (c) 2026, Lars Brubaker
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
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Renders a <see cref="MenuItemModel"/> list into a <see cref="PopupMenu"/>, producing exactly what
	/// the equivalent hand written <c>CreateMenuItem</c>/<c>CreateSubMenu</c>/<c>CreateSeparator</c> calls
	/// would have produced - same widgets, same names - so a menu can be moved onto the model without
	/// changing how it looks or how automation finds it.
	/// </summary>
	public static class MenuModelPopupBuilder
	{
		/// <summary>
		/// Adds <paramref name="items"/> to <paramref name="popupMenu"/> in order.
		/// </summary>
		/// <param name="popupMenu">The menu to append to. Existing children are left alone.</param>
		/// <param name="items">The items to render. Null or empty adds nothing.</param>
		/// <param name="theme">
		/// The theme handed to any sub menus this creates. Null takes the menu's own theme, which is what a
		/// sub menu wants - a different theme would draw it unlike the menu it hangs off.
		/// </param>
		/// <remarks>
		/// <see cref="MenuItemModel.IsVisible"/> and <see cref="MenuItemModel.IsEnabled"/> are evaluated
		/// here, once, which means the menu reflects the state at the moment it was built - the same
		/// semantics the imperative callers have always had.
		/// </remarks>
		public static void AddItems(PopupMenu popupMenu, IReadOnlyList<MenuItemModel> items, ThemeConfig theme = null)
		{
			if (items == null)
			{
				return;
			}

			theme ??= popupMenu.Theme;

			// The run of radio items being built; anything else ends it, so a separator splits two groups
			List<GuiWidget> radioGroup = null;

			foreach (var item in items)
			{
				if (item == null
					|| item.IsVisible?.Invoke() == false)
				{
					continue;
				}

				bool isRadioItem = item.IsChecked != null
					&& item.IsRadio
					&& !item.IsSeparator
					&& item.SubMenuItems == null
					&& item.PopupSubMenuOverride == null;
				if (!isRadioItem)
				{
					radioGroup = null;
				}

				if (item.IsSeparator)
				{
					popupMenu.CreateSeparator();
					continue;
				}

				if (item.SubMenuItems != null
					|| item.PopupSubMenuOverride != null)
				{
					AddSubMenu(popupMenu, item, theme);
					continue;
				}

				if (item.IsChecked != null)
				{
					if (isRadioItem)
					{
						radioGroup ??= new List<GuiWidget>();
					}

					AddCheckItem(popupMenu, item, radioGroup, IconFor(item, theme));
					continue;
				}

				var menuItem = popupMenu.CreateMenuItem(item.Text, IconFor(item, theme), item.ShortcutText);

				ApplyItemProperties(menuItem, item);
				FollowThemeWithGlyph(menuItem, item, theme, () => menuItem.Image, i => menuItem.Image = i);

				menuItem.Click += (s, e) => item.Action?.Invoke();
			}
		}

		/// <summary>
		/// Builds a checkable leaf through <see cref="PopupMenu.CreateBoolMenuItem(string, Func{bool}, Action{bool}, bool, IList{GuiWidget})"/>,
		/// the same widgets the hand written check and radio menus use. The flag the setter is handed is
		/// ignored: the model's <see cref="MenuItemModel.Action"/> owns the state change, and the next build
		/// reads the result back through <see cref="MenuItemModel.IsChecked"/>.
		/// </summary>
		private static void AddCheckItem(PopupMenu popupMenu, MenuItemModel item, List<GuiWidget> radioGroup, ImageBuffer icon)
		{
			bool useRadioStyle = radioGroup != null;
			Action<bool> setter = _ => item.Action?.Invoke();

			var menuItem = icon == null
				? popupMenu.CreateBoolMenuItem(item.Text, item.IsChecked, setter, useRadioStyle, radioGroup)
				: popupMenu.CreateBoolMenuItem(item.Text, icon, item.IsChecked, setter, useRadioStyle, radioGroup);

			ApplyItemProperties(menuItem, item);

			if (icon != null)
			{
				// The icon sits beside the label (CreateBoolMenuItem's icon overload); the row's own Image is the check
				var iconWidget = menuItem.Descendants<ImageWidget>().First();
				FollowThemeWithGlyph(iconWidget, item, popupMenu.Theme, () => iconWidget.Image, i => iconWidget.Image = i);
			}

			if (item.CloseMenuOnPick)
			{
				// What a command row does (PopupMenu.CreateMenuItem): losing focus closes the menu chain. Added
				// after CreateBoolMenuItem's own Click, so the mark and the action land first.
				menuItem.Click += (s, e) => popupMenu.Unfocus();
			}
		}

		private static void AddSubMenu(PopupMenu popupMenu, MenuItemModel item, ThemeConfig theme)
		{
			// The override exists for submenus whose rows are richer than a title; when it is present the
			// model's SubMenuItems (if any) are the plain description a native menu bar would use instead.
			var populate = item.PopupSubMenuOverride
				?? (subMenu => AddItems(subMenu, item.SubMenuItems?.Invoke(), theme));

			var subMenuItemButton = popupMenu.CreateSubMenu(item.Text, theme, populate, IconFor(item, theme));

			// A sub menu gets the same name and gate handling the leaves get
			ApplyItemProperties(subMenuItemButton, item);
			FollowThemeWithGlyph(subMenuItemButton, item, theme, () => subMenuItemButton.Image, i => subMenuItemButton.Image = i);
		}

		/// <summary>
		/// Redraws a glyph icon in the theme's new text colour when the theme changes under an open menu. An
		/// <see cref="MenuItemModel.Icon"/> is the caller's own picture (the Color menu's swatches) and is kept.
		/// </summary>
		private static void FollowThemeWithGlyph(GuiWidget widget, MenuItemModel item, ThemeConfig theme, Func<ImageBuffer> get, Action<ImageBuffer> set)
		{
			if (item.Icon == null
				&& !string.IsNullOrEmpty(item.IconGlyph))
			{
				ThemeBindings.Bind(widget, "Icon", theme, t => IconFor(item, t), get, set);
			}
		}

		/// <summary>
		/// The item's <see cref="MenuItemModel.Icon"/>, or else its <see cref="MenuItemModel.IconGlyph"/> drawn in
		/// the theme's text colour at the size of the menu's own check and radio icons.
		/// </summary>
		private static ImageBuffer IconFor(MenuItemModel item, ThemeConfig theme)
		{
			return item.Icon
				?? GlyphIcon.Render(item.IconGlyph, item.IconTypeFace, theme.TextColor, (int)Math.Round(16 * GuiWidget.DeviceScale));
		}

		private static void ApplyItemProperties(PopupMenu.MenuItem menuItem, MenuItemModel item)
		{
			if (item.AutomationName != null)
			{
				menuItem.Name = item.AutomationName;
			}

			if (item.ToolTipText != null)
			{
				menuItem.ToolTipText = item.ToolTipText;
			}

			menuItem.Enabled = item.IsEnabled?.Invoke() ?? true;
		}
	}
}
