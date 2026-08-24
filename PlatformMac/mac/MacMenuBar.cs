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
*/

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MatterHackers.Agg.UI;

using static MatterHackers.Agg.Platform.Mac.AppKitConstants;
using static MatterHackers.Agg.Platform.Mac.ObjC;

namespace MatterHackers.Agg.Platform.Mac
{
	/// <summary>
	/// Materializes a <see cref="MenuBarModel"/> into the real top-of-screen NSMenu and hands it to
	/// <c>-[NSApplication setMainMenu:]</c>. The mac half of the menu bar; everything about what the menus
	/// contain lives in the shared model.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The runtime class.</b> A menu needs an Objective-C object to be its items' target and its own
	/// delegate, so one is built with <c>objc_allocateClassPair</c> exactly the way
	/// <c>MacSystemWindow.RegisterWindowDelegateClass</c> builds the window delegate:
	/// <c>[UnmanagedCallersOnly]</c> statics as the implementations, and a dictionary keyed on the
	/// NSMenuItem pointer instead of an ivar to find the model an item came from. A single shared instance
	/// serves every item and every menu.
	/// </para>
	/// <para>
	/// <b>Rebuilt on every open.</b> <see cref="Install"/> materializes the whole bar once, and after that
	/// each menu refills itself from its model in <c>menuNeedsUpdate:</c> as AppKit is about to show it. So
	/// the contents that move - the recent files, the gates that answer differently as the application runs -
	/// are read at the moment they are about to be looked at, which is the same "gathered when the menu
	/// opens" promise the in-app popup makes. There is no change notification to subscribe to and these menus
	/// are small, so rebuilding is both simpler and cheap. What refreshes is a menu's <em>contents</em>: the
	/// set of top-level menus, and each top-level entry's own <see cref="MenuItemModel.IsVisible"/> and
	/// <see cref="MenuItemModel.IsEnabled"/>, are read once by <see cref="Install"/> and then frozen - the
	/// menu-bar items themselves are not in <see cref="MenuOwners"/>, so nothing ever revisits them. A gate
	/// that has to be able to change its answer therefore belongs on an item inside a menu, not on the menu.
	/// </para>
	/// <para>
	/// A submenu whose provider returns nothing comes out empty, and an empty menu is a mac usability
	/// problem, not a mac feature. Filling it with a disabled "nothing here" row is the <em>model's</em> job
	/// and not this file's - MatterCAD's recent files provider already returns exactly that one placeholder
	/// item when the list is empty, and it is the same row the in-app submenu shows. Doing it here as well
	/// would be a second, differently worded answer to a question the model has already answered.
	/// </para>
	/// <para>
	/// <b>Ownership.</b> AppKit's collections retain: <c>-[NSMenu addItem:]</c> retains the item and
	/// <c>-[NSMenuItem setSubmenu:]</c> retains the menu, so everything built here is created at +1,
	/// handed to its parent, and released back down to the one reference the parent holds. The two
	/// exceptions are deliberate: the main menu keeps its +1 in <see cref="mainMenu"/> (released and
	/// replaced by a second <see cref="Install"/>), and the controller instance keeps the +1 that
	/// <c>alloc</c>/<c>init</c> gave it for the life of the process. The managed <see cref="MenuItemModel"/>s
	/// are rooted by the static dictionaries, which is what keeps an item's <c>Action</c> - and any closure
	/// it captured - alive for as long as the NSMenuItem that can fire it.
	/// </para>
	/// <para>
	/// <b>Key equivalents do not fire yet.</b> The chords registered from
	/// <see cref="MenuItemRole"/> are display only for now. Every key event is swallowed in
	/// <c>MacSystemWindow.DispatchEvent</c> before <c>-[NSApplication sendEvent:]</c> ever sees it, so
	/// AppKit's own menu key-equivalent dispatch is never reached; the shortcut draws next to the item and
	/// the item still works when picked with the mouse. Explicit forwarding to
	/// <c>performKeyEquivalent:</c> is a later step.
	/// </para>
	/// <para>
	/// <b>Three known caveats.</b> First, this process has no .app bundle, so the application menu - the
	/// first menu in the model, by the convention <see cref="MenuBarModel.Menus"/> states - is drawn with the
	/// process name rather than with the title set here. The binary is named <c>MatterCAD</c>, which is why
	/// that is acceptable; the title is set regardless, both because it costs nothing and because it is what
	/// a bundled build would use.
	/// </para>
	/// <para>
	/// Second, holding a menu open runs AppKit's
	/// own nested tracking run loop inside <c>sendEvent:</c>, which the pumped loop in
	/// <c>MacSystemWindow.RunEventLoop</c> is not servicing - so painting stalls while a menu is down,
	/// exactly as it does during a live resize. The idle NSTimer does keep firing through that nested loop,
	/// so work queued from elsewhere is not stranded by an open menu. Item actions are queued with
	/// <see cref="UiThread.RunOnIdle"/> all the same: AppKit dismisses the menu before it sends the action,
	/// but the tracking loop that ran it is still unwinding below that call, and deferring puts the work -
	/// usually a dialog - on the next ordinary idle tick instead, on a clean stack with the window drawing
	/// again.
	/// </para>
	/// <para>
	/// Third - and this one only bites whoever tries to verify the bar from a script - AppKit lays the menu
	/// bar out when the application is activated by a real event. Activating it synthetically (System Events'
	/// <c>set frontmost</c>) leaves on screen whatever the bar was drawn with before, which for a freshly
	/// launched process is the bare app-name placeholder - so a screenshot taken that way shows an apparently
	/// empty menu bar while <c>-[NSApp mainMenu]</c> is fully populated, and the accessibility tree agrees
	/// with the stale drawing rather than with the menu. One real click into the window draws it correctly.
	/// </para>
	/// </remarks>
	internal static class MacMenuBar
	{
		private static readonly object InstallLock = new object();

		/// <summary>Maps an NSMenuItem to the model whose <c>Action</c> picking it runs.</summary>
		private static readonly Dictionary<IntPtr, MenuItemModel> ItemModels = new Dictionary<IntPtr, MenuItemModel>();

		/// <summary>
		/// Maps an NSMenu to the model that supplies its children, so the delegate can rebuild a menu from
		/// its provider when it is about to open.
		/// </summary>
		private static readonly Dictionary<IntPtr, MenuItemModel> MenuOwners = new Dictionary<IntPtr, MenuItemModel>();

		/// <summary>The action every leaf item is given; <see cref="OnMenuItemSelected"/> is its receiver.</summary>
		private static readonly IntPtr SelMenuItemSelected = Sel("menuItemSelected:");

		private static IntPtr controller;
		private static IntPtr mainMenu;

		/// <summary>Gets a value indicating whether a menu bar built here is currently the main menu.</summary>
		internal static bool IsInstalled => mainMenu != IntPtr.Zero;

		/// <summary>
		/// Builds <paramref name="model"/> into a fresh NSMenu tree and makes it the application's main menu.
		/// Idempotent: a second call throws the previous tree away and replaces it. Main thread only, like
		/// every other AppKit call in this assembly.
		/// </summary>
		internal static void Install(MenuBarModel model)
		{
			if (model?.Menus == null)
			{
				return;
			}

			lock (InstallLock)
			{
				EnsureController();

				IntPtr previousMainMenu = mainMenu;
				ItemModels.Clear();
				MenuOwners.Clear();

				IntPtr newMainMenu = CreateMenu("MainMenu");

				foreach (MenuItemModel topLevel in TopLevelMenus(model.Menus))
				{
					// A top level entry is always a submenu, whatever the model says - an item directly in the
					// menu bar with an action of its own is not a thing AppKit draws, so it is given no action.
					IntPtr menuItem = CreateMenuItem(topLevel.Text, IntPtr.Zero, string.Empty);
					IntPtr subMenu = CreateMenu(topLevel.Text);

					MenuOwners[subMenu] = topLevel;
					PopulateMenu(subMenu, topLevel);

					Send_v_r(menuItem, Sel("setSubmenu:"), subMenu);
					Release(subMenu);

					Send_v_B(menuItem, Sel("setEnabled:"), IsEnabled(topLevel) ? YES : NO);

					Send_v_r(newMainMenu, Sel("addItem:"), menuItem);
					Release(menuItem);
				}

				mainMenu = newMainMenu;
				Send_v_r(NSApp(), Sel("setMainMenu:"), newMainMenu);

				// Only now that AppKit has retained the replacement, in case the two were somehow the same.
				Release(previousMainMenu);
			}
		}

		/// <summary>
		/// The standard Command chord a role carries, or an empty string for a role that has none. Pure, and
		/// the whole of the role to shortcut mapping.
		/// </summary>
		internal static string KeyEquivalentFor(MenuItemRole role)
		{
			switch (role)
			{
				case MenuItemRole.Settings:
					return ",";

				case MenuItemRole.Quit:
					return "q";

				case MenuItemRole.OpenFile:
					return "o";

				default:
					return string.Empty;
			}
		}

		/// <summary>
		/// The items of <paramref name="items"/> whose <see cref="MenuItemModel.IsVisible"/> gate passes.
		/// </summary>
		/// <remarks>
		/// Hidden items are left out of the built menu rather than added and hidden, because the menu is
		/// rebuilt from the model every time it opens - so there is never a stale item to un-hide, and
		/// <c>setHidden:</c> would only be a second way to say the same thing.
		/// </remarks>
		internal static IReadOnlyList<MenuItemModel> VisibleItems(IReadOnlyList<MenuItemModel> items)
		{
			var visible = new List<MenuItemModel>();

			if (items != null)
			{
				foreach (MenuItemModel item in items)
				{
					if (item != null && (item.IsVisible == null || item.IsVisible()))
					{
						visible.Add(item);
					}
				}
			}

			return visible;
		}

		/// <summary>Whether <paramref name="item"/>'s enabled gate passes. Null means enabled.</summary>
		internal static bool IsEnabled(MenuItemModel item) => item?.IsEnabled == null || item.IsEnabled();

		/// <summary>
		/// The entries of <paramref name="menus"/> that can become menu-bar menus: visible, and not a
		/// separator. The menu bar has no dividers to draw, and an entry that asked to be one would otherwise
		/// come out as a blank, unopenable gap between File and Help.
		/// </summary>
		internal static IReadOnlyList<MenuItemModel> TopLevelMenus(IReadOnlyList<MenuItemModel> menus)
		{
			var topLevel = new List<MenuItemModel>();

			foreach (MenuItemModel menu in VisibleItems(menus))
			{
				if (!menu.IsSeparator)
				{
					topLevel.Add(menu);
				}
			}

			return topLevel;
		}

		/// <summary>
		/// The children <paramref name="container"/> currently has: its provider run now, filtered by the
		/// visibility gates as they answer now.
		/// </summary>
		/// <remarks>
		/// Every word of that is deliberate. The provider is called on each build rather than once, which is
		/// what makes a menu whose contents move - the recent files list - show what it should each time it is
		/// opened rather than what it held when the menu bar was installed.
		/// </remarks>
		internal static IReadOnlyList<MenuItemModel> ChildrenOf(MenuItemModel container)
			=> VisibleItems(container?.SubMenuItems?.Invoke());

		/// <summary>
		/// Fills an already created NSMenu with one native item per visible child of
		/// <paramref name="container"/>. Recurses into submenus.
		/// </summary>
		private static void PopulateMenu(IntPtr menu, MenuItemModel container)
		{
			foreach (MenuItemModel child in ChildrenOf(container))
			{
				IntPtr menuItem;

				if (child.IsSeparator)
				{
					// +separatorItem is autoreleased and shared-looking, but each call vends a distinct item;
					// it is not ours to release.
					menuItem = Send_r(Class("NSMenuItem"), Sel("separatorItem"));
					Send_v_r(menu, Sel("addItem:"), menuItem);
					continue;
				}

				// A model can carry a rich in-app submenu (PopupSubMenuOverride) with no plain children; there
				// is nothing for a native menu to draw from that, so it renders as a leaf.
				if (child.SubMenuItems != null)
				{
					menuItem = CreateMenuItem(child.Text, IntPtr.Zero, string.Empty);
					IntPtr subMenu = CreateMenu(child.Text);

					MenuOwners[subMenu] = child;
					PopulateMenu(subMenu, child);

					Send_v_r(menuItem, Sel("setSubmenu:"), subMenu);
					Release(subMenu);
				}
				else
				{
					menuItem = CreateMenuItem(child.Text, SelMenuItemSelected, KeyEquivalentFor(child.Role));
					Send_v_r(menuItem, Sel("setTarget:"), controller);
					ItemModels[menuItem] = child;
				}

				Send_v_B(menuItem, Sel("setEnabled:"), IsEnabled(child) ? YES : NO);

				Send_v_r(menu, Sel("addItem:"), menuItem);
				Release(menuItem);
			}
		}

		/// <summary>Creates an NSMenu at +1 with autoenabling off and this class's controller as delegate.</summary>
		private static IntPtr CreateMenu(string title)
		{
			IntPtr menu = Send_r_r(Alloc(Class("NSMenu")), Sel("initWithTitle:"), NSString(title ?? string.Empty));

			// Every enabled state is written explicitly from the model, so AppKit must not also go looking for
			// a validateMenuItem: or a responder that implements the action.
			Send_v_B(menu, Sel("setAutoenablesItems:"), NO);
			Send_v_r(menu, Sel("setDelegate:"), controller);

			return menu;
		}

		/// <summary>
		/// Creates an NSMenuItem at +1 targeting nothing yet. A non-empty <paramref name="keyEquivalent"/>
		/// also gets the Command modifier, which is the only chord any role maps to.
		/// </summary>
		/// <param name="title">The item's label.</param>
		/// <param name="action">
		/// The selector picking the item sends. <see cref="IntPtr.Zero"/> for an item that is never picked -
		/// a submenu parent - rather than a real selector AppKit happens to ignore.
		/// </param>
		/// <param name="keyEquivalent">The item's shortcut character, or an empty string for none.</param>
		private static IntPtr CreateMenuItem(string title, IntPtr action, string keyEquivalent)
		{
			IntPtr item = Send_r_r_r_r(
				Alloc(Class("NSMenuItem")),
				Sel("initWithTitle:action:keyEquivalent:"),
				NSString(title ?? string.Empty),
				action,
				NSString(keyEquivalent ?? string.Empty));

			if (!string.IsNullOrEmpty(keyEquivalent))
			{
				Send_v_Q(item, Sel("setKeyEquivalentModifierMask:"), NSEventModifierFlagCommand);
			}

			return item;
		}

		private static IntPtr NSApp() => Send_r(Class("NSApplication"), Sel("sharedApplication"));

		/// <summary>
		/// Defines <c>AggMacMenuController</c> and allocates the one instance of it, once. It is both the
		/// target every item's action is sent to and the <c>NSMenuDelegate</c> of every menu built here.
		/// </summary>
		private static unsafe void EnsureController()
		{
			if (controller != IntPtr.Zero)
			{
				return;
			}

			EnsureFrameworksLoaded();

			IntPtr cls = objc_allocateClassPair(Class("NSObject"), "AggMacMenuController", 0);
			if (cls == IntPtr.Zero)
			{
				throw new InvalidOperationException(
					"objc_allocateClassPair(\"AggMacMenuController\") returned nil - the name is already registered.");
			}

			// Type encodings: 'v' is void, '@' is id, ':' is SEL.
			AddMethod(cls, "menuItemSelected:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnMenuItemSelected, "v@:@");
			AddMethod(cls, "menuNeedsUpdate:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnMenuNeedsUpdate, "v@:@");

			objc_registerClassPair(cls);

			// alloc/init already leaves it at +1, and nothing ever releases it: one instance serves every menu
			// for the life of the process, and AppKit holds a menu's delegate weakly.
			controller = Init(Alloc(cls));
			if (controller == IntPtr.Zero)
			{
				throw new InvalidOperationException("[[AggMacMenuController alloc] init] returned nil.");
			}
		}

		private static void AddMethod(IntPtr cls, string selectorName, IntPtr implementation, string typeEncoding)
		{
			if (class_addMethod(cls, Sel(selectorName), implementation, typeEncoding) == NO)
			{
				throw new InvalidOperationException($"class_addMethod failed for -[AggMacMenuController {selectorName}].");
			}
		}

		[UnmanagedCallersOnly]
		private static void OnMenuItemSelected(IntPtr self, IntPtr cmd, IntPtr sender)
		{
			// An exception must never cross back into Objective-C: there is no managed frame above this to
			// catch it and the runtime tears the process down.
			try
			{
				MenuItemModel model;
				lock (InstallLock)
				{
					ItemModels.TryGetValue(sender, out model);
				}

				Action action = model?.Action;
				if (action != null)
				{
					// Not inline: the menu is dismissed by the time this arrives, but the tracking loop that ran
					// it is still unwinding below this frame, so a dialog opened here would nest inside it. The
					// next idle tick runs it on a clean stack. See the class remarks.
					UiThread.RunOnIdle(action);
				}
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"MacMenuBar menuItemSelected: threw {ex}");
			}
		}

		/// <summary>
		/// AppKit is about to show <paramref name="menu"/>: throw its contents away and build them again from
		/// the model, so that what opens is what the application would answer right now.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Always on the main thread - AppKit sends this from the menu tracking that is running inside
		/// <c>sendEvent:</c>, which is the pump's own thread - so nothing here marshals. It does take
		/// <see cref="InstallLock"/>, which is the same lock <see cref="Install"/> holds while it clears both
		/// dictionaries and rebuilds the whole bar; the two therefore cannot interleave. Their orders both
		/// end well: a rebuild that lands first has its entries thrown away by the following Install, and a
		/// rebuild that arrives after an Install is either for a menu the new bar still owns (found, rebuilt)
		/// or for one from the bar that was replaced (absent from <see cref="MenuOwners"/>, left alone).
		/// </para>
		/// <para>
		/// It also means <see cref="PopulateMenu"/> and every model provider it runs - <c>SubMenuItems</c>,
		/// <c>IsVisible</c>, <c>IsEnabled</c> - execute holding <see cref="InstallLock"/> inside an
		/// Objective-C callback AppKit is blocked on, so none of them may wait on another thread: they have to
		/// answer from what is already in hand.
		/// </para>
		/// <para>
		/// A menu that describes no children - the main menu, and any menu belonging to a replaced bar - is
		/// not in <see cref="MenuOwners"/> and is deliberately left exactly as it is. Emptying it because
		/// nothing was found would take the menu bar down.
		/// </para>
		/// </remarks>
		[UnmanagedCallersOnly]
		private static void OnMenuNeedsUpdate(IntPtr self, IntPtr cmd, IntPtr menu)
		{
			try
			{
				lock (InstallLock)
				{
					if (!MenuOwners.TryGetValue(menu, out MenuItemModel container))
					{
						return;
					}

					// Before removeAllItems, not after: that call releases the items, which is the last
					// reference each of them has, so afterwards there is nothing left to read a submenu off.
					ForgetContents(menu);

					Send_v(menu, Sel("removeAllItems"));

					PopulateMenu(menu, container);
				}
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"MacMenuBar menuNeedsUpdate: threw {ex}");
			}
		}

		/// <summary>
		/// Drops the dictionary entries for everything currently inside <paramref name="menu"/>, submenus and
		/// their contents included. The menu itself stays: it is being refilled, not thrown away.
		/// </summary>
		/// <remarks>
		/// Stale pointers are not merely untidy. An NSMenuItem left in <see cref="ItemModels"/> after it has
		/// been deallocated is a dangling key, and the allocator is free to hand that same address to a later
		/// NSMenuItem - which would then find, and run, the previous occupant's action.
		/// </remarks>
		private static void ForgetContents(IntPtr menu)
		{
			long count = Send_q(menu, Sel("numberOfItems"));

			for (long index = 0; index < count; index++)
			{
				IntPtr item = Send_r_q(menu, Sel("itemAtIndex:"), index);

				ItemModels.Remove(item);

				IntPtr subMenu = Send_r(item, Sel("submenu"));
				if (subMenu != IntPtr.Zero)
				{
					ForgetContents(subMenu);
					MenuOwners.Remove(subMenu);
				}
			}
		}
	}
}
