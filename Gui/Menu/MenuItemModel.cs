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
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The standard place an item occupies in a platform menu bar. Platforms that have such a concept
	/// (the mac app menu) use this to give the item its conventional shortcut and position; everything
	/// else ignores it.
	/// </summary>
	public enum MenuItemRole
	{
		None,
		About,
		Settings,
		Quit,
		OpenFile,
		Help
	}

	/// <summary>
	/// One entry in a menu, described rather than built: a leaf command, a separator, or a submenu.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Which of those three an item is comes from the first of these that applies:
	/// <see cref="IsSeparator"/>, then a submenu (<see cref="SubMenuItems"/> or
	/// <see cref="PopupSubMenuOverride"/>), then a leaf running <see cref="Action"/>. A renderer never
	/// looks past the first match, so a separator that also carries an action is just a separator.
	/// </para>
	/// <para>
	/// This is the platform neutral half of a menu. The same model is rendered into the in-app
	/// <see cref="PopupMenu"/> by <see cref="MenuModelPopupBuilder"/> and (on the mac) into a native
	/// NSMenu, so anything specific to one of those - icons, custom submenu widgets - is optional and
	/// the other renderer is free to drop it.
	/// </para>
	/// <para>
	/// Building a model must be side effect free. Every application touch belongs inside
	/// <see cref="Action"/>, <see cref="IsVisible"/>, <see cref="IsEnabled"/> or
	/// <see cref="SubMenuItems"/> - the lambdas are what run, and only when the menu is shown or picked.
	/// That is what lets a menu be constructed and asserted on headlessly, and what lets a native menu
	/// re-evaluate its gates every time it opens.
	/// </para>
	/// </remarks>
	public class MenuItemModel
	{
		/// <summary>Gets or sets the item's label, already localized by whoever produced the model.</summary>
		public string Text { get; set; }

		/// <summary>
		/// Gets or sets the icon drawn in the popup rendering. Native menu bars ignore it.
		/// </summary>
		public ImageBuffer Icon { get; set; }

		/// <summary>
		/// Gets or sets the hover text shown in the popup rendering. Native menu bars ignore it, as they do
		/// <see cref="Icon"/>.
		/// </summary>
		public string ToolTipText { get; set; }

		/// <summary>
		/// Gets or sets what running the item does. Ignored when the item is a separator or a submenu.
		/// </summary>
		/// <remarks>
		/// The popup builder invokes this inline from the widget's Click, which is what the imperative menus
		/// have always done; the native menu builder defers it through <see cref="UiThread.RunOnIdle"/>, since
		/// its callback runs inside AppKit's menu tracking loop. An action that must not run inline (one that
		/// tears down the widget that raised it, say) has to wrap itself in RunOnIdle - the popup builder will
		/// not do it for you.
		/// </remarks>
		public Action Action { get; set; }

		/// <summary>
		/// Gets or sets the test for whether this item appears at all. Null means always visible.
		/// Evaluated each time the menu is built, so a gate can change between openings.
		/// </summary>
		public Func<bool> IsVisible { get; set; }

		/// <summary>
		/// Gets or sets the test for whether this item can be picked. Null means always enabled.
		/// Evaluated each time the menu is built.
		/// </summary>
		public Func<bool> IsEnabled { get; set; }

		/// <summary>Gets or sets the standard menu-bar role this item fills, if any.</summary>
		public MenuItemRole Role { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether this entry is a divider rather than a command. This wins
		/// over everything else on the item - no text, action or children are read from a separator.
		/// </summary>
		public bool IsSeparator { get; set; }

		/// <summary>
		/// Gets or sets the provider of this item's children. Non-null makes the item a submenu, which means
		/// <see cref="Action"/> is not used.
		/// Deliberately a delegate rather than a list: contents like a recent files list are gathered
		/// when the menu opens, not when the model is built.
		/// </summary>
		public Func<IReadOnlyList<MenuItemModel>> SubMenuItems { get; set; }

		/// <summary>
		/// Gets or sets a hand written population of the in-app submenu, used instead of rendering
		/// <see cref="SubMenuItems"/>. This is the escape hatch for submenus whose rows are richer than a
		/// title (MatterCAD's Open Recent draws thumbnails); a model that sets both gets the rich rows in
		/// the app and the plain <see cref="SubMenuItems"/> titles in a native menu.
		/// </summary>
		public Action<PopupMenu> PopupSubMenuOverride { get; set; }

		/// <summary>
		/// Gets or sets the widget name used to find this item from automation. Null leaves
		/// <see cref="PopupMenu"/>'s own <c>"{Text} Menu Item"</c> naming in place, which is what existing
		/// automation and product tours already search for.
		/// </summary>
		public string AutomationName { get; set; }

		/// <summary>Gets or sets arbitrary data the producer wants to carry along with the item.</summary>
		public object Tag { get; set; }
	}

	/// <summary>
	/// A whole menu bar: the top level menus, each one a <see cref="MenuItemModel"/> with children.
	/// </summary>
	public class MenuBarModel
	{
		/// <summary>
		/// Gets or sets the top level menus in display order. By convention the first entry is the
		/// application menu, which is where the mac puts About/Settings/Quit.
		/// </summary>
		public IReadOnlyList<MenuItemModel> Menus { get; set; }
	}
}
