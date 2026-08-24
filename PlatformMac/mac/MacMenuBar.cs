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
	/// <b>Key equivalents are reached by hand.</b> Every key event is swallowed in
	/// <c>MacSystemWindow.DispatchEvent</c> before <c>-[NSApplication sendEvent:]</c> ever sees it, so
	/// AppKit's own key-equivalent dispatch is never reached on its own. Instead a Command chord the managed
	/// window left unhandled is offered to <see cref="PerformKeyEquivalent"/>, which sends
	/// <c>performKeyEquivalent:</c> straight to the main menu. Which item a chord belongs to is answered
	/// from the model, in <c>menuHasKeyEquivalent:forEvent:target:action:</c>, rather than by letting AppKit
	/// look through the menus - see the remarks on <see cref="OnMenuHasKeyEquivalent"/> for why that
	/// distinction is the whole point.
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

		/// <summary>
		/// The NSMenus hanging directly off the menu bar - File, Help and the rest - and nothing deeper. This
		/// is the one depth at which an item is given a key equivalent, because it is the one depth
		/// <see cref="MatchKeyEquivalent"/> searches; drawing "⌘O" beside an item nested any deeper would be
		/// advertising a shortcut that could never fire. Written only by <see cref="Install"/>, which is also
		/// the only thing that can invalidate it: a top level menu is never a submenu of another menu, so the
		/// rebuild in <see cref="OnMenuNeedsUpdate"/> can neither add to nor retire an entry here.
		/// </summary>
		private static readonly HashSet<IntPtr> MenuBarMenus = new HashSet<IntPtr>();

		/// <summary>The action every leaf item is given; <see cref="OnMenuItemSelected"/> is its receiver.</summary>
		private static readonly IntPtr SelMenuItemSelected = Sel("menuItemSelected:");

		/// <summary>
		/// The action a matched key equivalent is dispatched through; <see cref="OnMenuKeyEquivalentFired"/> is
		/// its receiver. Separate from <see cref="SelMenuItemSelected"/> because a chord is matched against the
		/// model and never against an NSMenuItem, so there is no item pointer to look the model up by.
		/// </summary>
		private static readonly IntPtr SelMenuKeyEquivalentFired = Sel("menuKeyEquivalentFired:");

		private static IntPtr controller;
		private static IntPtr mainMenu;

		/// <summary>The model the current bar was built from, which is also what a chord is matched against.</summary>
		private static MenuBarModel installedModel;

		/// <summary>
		/// The item <see cref="OnMenuHasKeyEquivalent"/> matched, waiting for the action AppKit sends straight
		/// afterwards to run it. See that method's remarks for why a field is enough.
		/// </summary>
		private static MenuItemModel pendingKeyEquivalent;

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
				MenuBarMenus.Clear();
				installedModel = model;
				pendingKeyEquivalent = null;

				IntPtr newMainMenu = CreateMenu("MainMenu");

				foreach (MenuItemModel topLevel in TopLevelMenus(model.Menus))
				{
					// A top level entry is always a submenu, whatever the model says - an item directly in the
					// menu bar with an action of its own is not a thing AppKit draws, so it is given no action.
					IntPtr menuItem = CreateMenuItem(topLevel.Text, IntPtr.Zero, string.Empty);
					IntPtr subMenu = CreateMenu(topLevel.Text);

					MenuOwners[subMenu] = topLevel;
					MenuBarMenus.Add(subMenu);
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
		/// Offers a key event to the installed menu bar's key equivalents, and reports whether one took it.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The window pump swallows the key events belonging to its own windows before AppKit sees them, so
		/// for those this is the only way a menu shortcut can fire; <c>MacSystemWindow.HandleKeyDown</c>
		/// calls it for the Command chords the managed window did not handle itself. (An event for a window
		/// this assembly does not own - a native open panel - is passed on to <c>sendEvent:</c> as it always
		/// was, and AppKit searches the menu bar for it without being asked to.)
		/// </para>
		/// <para>
		/// "Did not handle" is decided the instant the key down returns, so a handler that finishes
		/// asynchronously has not said no - it has not answered yet, and its chord arrives here anyway. A
		/// role's chord must therefore not be one the application also handles; see the comment at the call
		/// site for what that costs if it ever is.
		/// </para>
		/// <para>
		/// False the moment no bar built here is installed, before any message is sent. That is what keeps
		/// every other agg application on the mac behaving exactly as it did before menus existed: no menu, no
		/// forwarding, and in particular no chance of waking a main menu somebody else set up.
		/// </para>
		/// <para>
		/// The lock is held across the message on purpose. <c>performKeyEquivalent:</c> calls back into
		/// <see cref="OnMenuHasKeyEquivalent"/> synchronously on this same thread, where a monitor is
		/// reentrant; what it buys is that a concurrent <see cref="Install"/> cannot release
		/// <see cref="mainMenu"/> out from under the send.
		/// </para>
		/// </remarks>
		internal static bool PerformKeyEquivalent(IntPtr nsEvent)
		{
			if (nsEvent == IntPtr.Zero)
			{
				return false;
			}

			lock (InstallLock)
			{
				if (mainMenu == IntPtr.Zero)
				{
					return false;
				}

				return Send_B_r(mainMenu, Sel("performKeyEquivalent:"), nsEvent) != NO;
			}
		}

		/// <summary>
		/// The item of <paramref name="model"/> whose role gives it the chord a key event carries, or null for
		/// a chord no item claims. Pure - it is the whole of the shortcut matching, and answers without an
		/// NSMenu in sight.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Only a plain Command chord can match, because a plain Command chord is the only thing
		/// <see cref="KeyEquivalentFor"/> ever registers: Command with Shift, Option or Control also held is a
		/// different shortcut and belongs to nobody here. <c>charactersIgnoringModifiers</c> is the text to
		/// compare - it resolves the keyboard layout while factoring Command back out - and the comparison
		/// ignores case so that a chord typed with caps lock down still finds its item.
		/// </para>
		/// <para>
		/// The gates apply exactly as they do when the menu is drawn: an item hidden by
		/// <see cref="MenuItemModel.IsVisible"/> is not there to be matched, and one greyed out by
		/// <see cref="MenuItemModel.IsEnabled"/> cannot be run by its shortcut any more than it could be
		/// clicked.
		/// </para>
		/// <para>
		/// <b>A chord no role carries is rejected before a single provider runs.</b> That test is not an
		/// optimization, it is what makes forwarding affordable at all: every Command chord the application
		/// leaves unhandled arrives here, and merely enumerating the top level menus means running each of
		/// their <c>SubMenuItems</c> providers - in MatterCAD those recolor icons and rasterize the About
		/// text. Since <see cref="KeyEquivalentFor"/> hands out a closed, tiny set of chords,
		/// <see cref="RoleKeyEquivalents"/> answers "could anything here possibly want this?" from the event
		/// alone, and the overwhelmingly common miss costs one hash lookup.
		/// </para>
		/// <para>
		/// The walk then stops at the children of the top level menus, and that bound is deliberate too.
		/// Going deeper would mean running the nested submenu providers - the recent files list among them,
		/// which reads the disk. No role that carries a chord is ever nested that deep: About, Settings,
		/// Quit, Open System File all sit directly in a menu, which is what a standard mac shortcut means.
		/// <see cref="PopulateMenu"/> draws a key equivalent at exactly this depth and no other, so what is
		/// shown and what can fire are the same set of items.
		/// </para>
		/// </remarks>
		internal static MenuItemModel MatchKeyEquivalent(MenuBarModel model, string charactersIgnoringModifiers, ulong modifierFlags)
		{
			if (model?.Menus == null
				|| string.IsNullOrEmpty(charactersIgnoringModifiers)
				|| !IsPlainCommandChord(modifierFlags)
				|| !RoleKeyEquivalents.Contains(charactersIgnoringModifiers))
			{
				return null;
			}

			foreach (MenuItemModel menu in TopLevelMenus(model.Menus))
			{
				foreach (MenuItemModel item in ChildrenOf(menu))
				{
					// A separator has no shortcut and a submenu is opened, never run.
					if (item.IsSeparator || item.SubMenuItems != null)
					{
						continue;
					}

					string chord = KeyEquivalentFor(item.Role);

					if (chord.Length > 0
						&& string.Equals(chord, charactersIgnoringModifiers, StringComparison.OrdinalIgnoreCase)
						&& IsEnabled(item))
					{
						return item;
					}
				}
			}

			return null;
		}

		/// <summary>
		/// Whether a modifier-flags word is Command and nothing else. Caps lock, Fn and the numeric-pad bit are
		/// not part of a chord's identity and are ignored.
		/// </summary>
		private static bool IsPlainCommandChord(ulong modifierFlags)
		{
			const ulong ChordModifiers = NSEventModifierFlagCommand
				| NSEventModifierFlagShift
				| NSEventModifierFlagControl
				| NSEventModifierFlagOption;

			return (modifierFlags & ChordModifiers) == NSEventModifierFlagCommand;
		}

		/// <summary>
		/// Every chord any role carries, which is <see cref="KeyEquivalentFor"/> read backwards. Derived from
		/// that method rather than written out a second time, so a role gaining a shortcut cannot leave this
		/// set behind. Case-insensitive, matching how a chord is compared.
		/// </summary>
		private static readonly HashSet<string> RoleKeyEquivalents = BuildRoleKeyEquivalents();

		private static HashSet<string> BuildRoleKeyEquivalents()
		{
			var chords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (MenuItemRole role in Enum.GetValues<MenuItemRole>())
			{
				string chord = KeyEquivalentFor(role);

				if (chord.Length > 0)
				{
					chords.Add(chord);
				}
			}

			return chords;
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
		/// <remarks>
		/// Only a menu hanging directly off the menu bar hands its children key equivalents, because that is
		/// the only depth <see cref="MatchKeyEquivalent"/> looks at - see <see cref="MenuBarMenus"/>. A role
		/// carried by an item nested deeper still works when it is picked; it simply does not claim a chord it
		/// could not answer to.
		/// </remarks>
		private static void PopulateMenu(IntPtr menu, MenuItemModel container)
		{
			bool shortcutsFireFromHere = MenuBarMenus.Contains(menu);

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
					menuItem = CreateMenuItem(
						child.Text,
						SelMenuItemSelected,
						shortcutsFireFromHere ? KeyEquivalentFor(child.Role) : string.Empty);
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

			// Type encodings: 'v' is void, '@' is id, ':' is SEL, 'B' is BOOL, and '^' prefixes a pointer to
			// what follows - so "^@" is id* and "^:" is SEL*, the two out-parameters of the key equivalent
			// hook. ('B' is BOOL as arm64 spells it, where the type is C's bool; the 64 bit Intel spelling is
			// the one byte 'c'. Both pass identically, and an encoding is only read by the forwarding
			// machinery, which none of these go through.)
			AddMethod(cls, "menuItemSelected:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnMenuItemSelected, "v@:@");
			AddMethod(cls, "menuNeedsUpdate:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnMenuNeedsUpdate, "v@:@");
			AddMethod(
				cls,
				"menuHasKeyEquivalent:forEvent:target:action:",
				(IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, byte>)&OnMenuHasKeyEquivalent,
				"B@:@@^@^:");
			AddMethod(cls, "menuKeyEquivalentFired:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnMenuKeyEquivalentFired, "v@:@");

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
		/// AppKit is looking for the item a key equivalent belongs to: answer from the model, and if one
		/// claims the chord, name the target and action to run it with.
		/// </summary>
		/// <remarks>
		/// <para>
		/// <b>This exists to stop a rebuild storm, not to be clever.</b> Without it, AppKit answers "does any
		/// item here match this event?" the only other way it can - by updating each menu (which is
		/// <see cref="OnMenuNeedsUpdate"/>, which re-runs every provider, recent files and their disk reads
		/// included) and then reading the items it just built. That would happen on every Command chord
		/// forwarded here, most of which match nothing at all. Implementing this hook takes that path out
		/// entirely: a delegate that answers the question is asked instead of the menu being built to answer
		/// it, for a match and for a miss alike. NO means this menu has no equivalent for the event and AppKit
		/// moves on without updating or searching it, which is exactly the silence a nonsense chord should
		/// make.
		/// </para>
		/// <para>
		/// <b>The whole model, whichever menu is asking.</b> AppKit asks per menu, and this answers from the
		/// entire installed bar every time rather than from the menu named in <paramref name="menu"/>. That is
		/// on purpose: it means the first menu asked already gives the final answer, so the chord fires no
		/// matter how AppKit chooses to walk (or not walk) from the main menu into its submenus. Observed on
		/// macOS 26: a match is answered by the main menu itself and the search stops there, while a miss
		/// walks on and asks every menu and submenu in turn. A YES stops the search, so no second menu can
		/// claim the same chord.
		/// </para>
		/// <para>
		/// Answering the same question once per menu is only affordable because the answer is cheap, which is
		/// <see cref="MatchKeyEquivalent"/>'s job and not this one's: a chord no role carries - which is
		/// nearly all of them - costs a hash lookup, and only one of the handful of chords a role does carry
		/// gets as far as enumerating the top level menus. Even that is per menu asked, so a role chord that
		/// no currently visible, enabled item claims runs the top level providers a few times over. Cheap
		/// providers up there are therefore part of the bargain - and still nothing beside the alternative,
		/// which is rebuilding every menu from every provider on every unhandled Command keystroke.
		/// </para>
		/// <para>
		/// <b>Why the match is handed over in a field.</b> The item was found in the model, so there is no
		/// NSMenuItem for <see cref="OnMenuItemSelected"/> to look up - hence its own selector, and hence
		/// <see cref="pendingKeyEquivalent"/> carrying the match to it. The handoff is as short as a handoff
		/// gets: AppKit sends the action from inside the same <c>performKeyEquivalent:</c> call that this
		/// returned YES to, on this thread, before anything else can run. A match that AppKit somehow never
		/// dispatched would simply be overwritten by the next one - it cannot fire late or fire twice.
		/// </para>
		/// </remarks>
		/// <param name="self">The controller instance, which is also the target a match is dispatched to.</param>
		/// <param name="cmd">The selector being sent. Unused.</param>
		/// <param name="menu">The menu AppKit is asking about. Deliberately unused; see the remarks.</param>
		/// <param name="nsEvent">The key event being matched.</param>
		/// <param name="target">Out-parameter, an <c>id *</c>: where to write the object to send the action to.</param>
		/// <param name="action">Out-parameter, a <c>SEL *</c>: where to write the action to send.</param>
		/// <returns>YES when an item claims the chord and target and action have been written; NO otherwise.</returns>
		[UnmanagedCallersOnly]
		private static unsafe byte OnMenuHasKeyEquivalent(IntPtr self, IntPtr cmd, IntPtr menu, IntPtr nsEvent, IntPtr target, IntPtr action)
		{
			try
			{
				// Both are documented as nullable. With nowhere to report the dispatch there is no honest way
				// to claim the chord, so decline it rather than leave a match stranded in the field.
				if (target == IntPtr.Zero || action == IntPtr.Zero)
				{
					return NO;
				}

				lock (InstallLock)
				{
					MenuItemModel matched = MatchKeyEquivalent(
						installedModel,
						FromNSString(Send_r(nsEvent, Sel("charactersIgnoringModifiers"))),
						Send_Q(nsEvent, Sel("modifierFlags")));

					if (matched == null)
					{
						return NO;
					}

					pendingKeyEquivalent = matched;
				}

				*(IntPtr*)target = controller;
				*(IntPtr*)action = SelMenuKeyEquivalentFired;

				return YES;
			}
			catch (Exception ex)
			{
				// An exception must never cross back into Objective-C, and a chord that could not be matched
				// is one nobody claims.
				Console.Error.WriteLine($"MacMenuBar menuHasKeyEquivalent:forEvent:target:action: threw {ex}");
				return NO;
			}
		}

		/// <summary>
		/// Runs the item <see cref="OnMenuHasKeyEquivalent"/> matched. The key equivalent half of
		/// <see cref="OnMenuItemSelected"/>, and deferred to idle for the same reason.
		/// </summary>
		[UnmanagedCallersOnly]
		private static void OnMenuKeyEquivalentFired(IntPtr self, IntPtr cmd, IntPtr sender)
		{
			try
			{
				MenuItemModel model;
				lock (InstallLock)
				{
					model = pendingKeyEquivalent;
					pendingKeyEquivalent = null;
				}

				Action action = model?.Action;
				if (action != null)
				{
					UiThread.RunOnIdle(action);
				}
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"MacMenuBar menuKeyEquivalentFired: threw {ex}");
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
