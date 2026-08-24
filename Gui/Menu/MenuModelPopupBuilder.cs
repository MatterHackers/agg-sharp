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

using System.Collections.Generic;

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

			foreach (var item in items)
			{
				if (item == null
					|| item.IsVisible?.Invoke() == false)
				{
					continue;
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

				var menuItem = popupMenu.CreateMenuItem(item.Text, item.Icon);

				ApplyItemProperties(menuItem, item);

				menuItem.Click += (s, e) => item.Action?.Invoke();
			}
		}

		private static void AddSubMenu(PopupMenu popupMenu, MenuItemModel item, ThemeConfig theme)
		{
			// The override exists for submenus whose rows are richer than a title; when it is present the
			// model's SubMenuItems (if any) are the plain description a native menu bar would use instead.
			var populate = item.PopupSubMenuOverride
				?? (subMenu => AddItems(subMenu, item.SubMenuItems?.Invoke(), theme));

			var subMenuItemButton = popupMenu.CreateSubMenu(item.Text, theme, populate, item.Icon);

			// A sub menu gets the same name and gate handling the leaves get
			ApplyItemProperties(subMenuItemButton, item);
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
