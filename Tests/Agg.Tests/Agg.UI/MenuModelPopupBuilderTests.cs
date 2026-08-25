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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The declarative menu model exists so the same description can be rendered in-app and (on the mac)
	/// natively. These tests pin the in-app half: that a model produces exactly the widgets - and the
	/// automation names - that the equivalent hand written PopupMenu calls used to.
	/// </summary>
	// CreateSubMenu populates from UiThread.RunOnIdle, and the pending action queue is process wide, so
	// share the key the other menu tests use rather than draining another class's queued work.
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class MenuModelPopupBuilderTests
	{
		[Test]
		public async Task ItemsRenderInOrderWithPopupNamingConvention()
		{
			var theme = new ThemeConfig();
			var popupMenu = new PopupMenu(theme);

			MenuModelPopupBuilder.AddItems(
				popupMenu,
				new List<MenuItemModel>
				{
					new MenuItemModel() { Text = "Open" },
					new MenuItemModel() { Text = "Save", AutomationName = "Custom Save Name" },
					new MenuItemModel() { Text = "Close" },
				},
				theme);

			// An item with no AutomationName keeps PopupMenu's own "{Text} Menu Item" naming, which is what
			// ProductTour.json and the automation tests search for
			await Assert.That(ChildNames(popupMenu)).IsEqualTo("Open Menu Item|Custom Save Name|Close Menu Item");
		}

		[Test]
		public async Task InvisibleItemsAreNotBuilt()
		{
			var theme = new ThemeConfig();
			var popupMenu = new PopupMenu(theme);

			MenuModelPopupBuilder.AddItems(
				popupMenu,
				new List<MenuItemModel>
				{
					new MenuItemModel() { Text = "Always" },
					new MenuItemModel() { Text = "Hidden", IsVisible = () => false },
					new MenuItemModel() { Text = "Shown", IsVisible = () => true },
				},
				theme);

			await Assert.That(ChildNames(popupMenu)).IsEqualTo("Always Menu Item|Shown Menu Item");
		}

		[Test]
		public async Task EnabledGateReachesTheWidget()
		{
			var theme = new ThemeConfig();
			var popupMenu = new PopupMenu(theme);

			MenuModelPopupBuilder.AddItems(
				popupMenu,
				new List<MenuItemModel>
				{
					new MenuItemModel() { Text = "Undo", IsEnabled = () => false },
					new MenuItemModel() { Text = "Redo" },
				},
				theme);

			var items = popupMenu.Children.OfType<PopupMenu.MenuItem>().ToList();

			await Assert.That(items[0].Enabled).IsFalse();

			// No IsEnabled at all means enabled - it must not be read as "false"
			await Assert.That(items[1].Enabled).IsTrue();
		}

		[Test]
		public async Task ToolTipTextReachesTheWidget()
		{
			var theme = new ThemeConfig();
			var popupMenu = new PopupMenu(theme);

			MenuModelPopupBuilder.AddItems(
				popupMenu,
				new List<MenuItemModel>
				{
					new MenuItemModel()
					{
						Text = "Force What's New",
						ToolTipText = "Shows What's New on the next startup",
					},
					new MenuItemModel() { Text = "Plain" },
				},
				theme);

			var items = popupMenu.Children.OfType<PopupMenu.MenuItem>().ToList();

			await Assert.That(items[0].ToolTipText).IsEqualTo("Shows What's New on the next startup");

			// An item that says nothing about tool tips must not acquire one
			await Assert.That(items[1].ToolTipText).IsNull();
		}

		[Test]
		public async Task SubMenuButtonTakesAutomationNameAndEnabledGate()
		{
			var popupMenu = new PopupMenu(new ThemeConfig());

			// No theme argument here on purpose - the builder is expected to fall back to the menu's own
			// theme, so a sub menu never draws unlike the menu it hangs off
			MenuModelPopupBuilder.AddItems(
				popupMenu,
				new List<MenuItemModel>
				{
					new MenuItemModel()
					{
						Text = "Open Recent",
						AutomationName = "Custom Name MenuItem",
						SubMenuItems = () => new List<MenuItemModel>(),
					},
					new MenuItemModel()
					{
						Text = "Export",
						IsEnabled = () => false,
						SubMenuItems = () => new List<MenuItemModel>(),
					},
				});

			var subMenuButtons = popupMenu.Children.OfType<PopupMenu.SubMenuItemButton>().ToList();

			await Assert.That(subMenuButtons[0].Name).IsEqualTo("Custom Name MenuItem");
			await Assert.That(subMenuButtons[0].Enabled).IsTrue();

			await Assert.That(subMenuButtons[1].Name).IsEqualTo("Export Menu Item");
			await Assert.That(subMenuButtons[1].Enabled).IsFalse();
		}

		[Test]
		public async Task SeparatorBuildsASeparatorWidget()
		{
			var theme = new ThemeConfig();
			var popupMenu = new PopupMenu(theme);

			MenuModelPopupBuilder.AddItems(
				popupMenu,
				new List<MenuItemModel>
				{
					new MenuItemModel() { Text = "Settings" },
					new MenuItemModel() { IsSeparator = true },
					new MenuItemModel() { Text = "Quit" },
				},
				theme);

			await Assert.That(popupMenu.Children.Count).IsEqualTo(3);
			await Assert.That(popupMenu.Children[1]).IsTypeOf<HorizontalLine>();
		}

		[Test]
		public async Task SubMenuItemsRenderRecursivelyWhenOpened()
		{
			var systemWindow = new SystemWindow(400, 300);
			var theme = new ThemeConfig();

			var popupMenu = new PopupMenu(theme);
			systemWindow.AddChild(popupMenu);

			MenuModelPopupBuilder.AddItems(
				popupMenu,
				new List<MenuItemModel>
				{
					new MenuItemModel()
					{
						Text = "Open Recent",
						SubMenuItems = () => new List<MenuItemModel>
						{
							new MenuItemModel() { Text = "First.mcx" },
							new MenuItemModel() { IsSeparator = true },
							new MenuItemModel() { Text = "Second.mcx", IsEnabled = () => false },
							new MenuItemModel() { Text = "Never.mcx", IsVisible = () => false },
						},
					},
				},
				theme);

			var subMenu = OpenSubMenu(popupMenu);

			await Assert.That(subMenu).IsNotNull();

			// The children of a submenu go through the same builder, so they get the same naming, separators
			// and gates the top level does (the empty middle entry is the unnamed separator line)
			await Assert.That(ChildNames(subMenu)).IsEqualTo("First.mcx Menu Item||Second.mcx Menu Item");
			await Assert.That(subMenu.Children[1]).IsTypeOf<HorizontalLine>();
			await Assert.That(subMenu.Children.OfType<PopupMenu.MenuItem>().Last().Enabled).IsFalse();
		}

		[Test]
		public async Task PopupSubMenuOverrideWinsOverSubMenuItems()
		{
			var systemWindow = new SystemWindow(400, 300);
			var theme = new ThemeConfig();

			var popupMenu = new PopupMenu(theme);
			systemWindow.AddChild(popupMenu);

			MenuModelPopupBuilder.AddItems(
				popupMenu,
				new List<MenuItemModel>
				{
					new MenuItemModel()
					{
						Text = "Open Recent",
						// The plain titles a native menu bar would show...
						SubMenuItems = () => new List<MenuItemModel>
						{
							new MenuItemModel() { Text = "Plain" },
						},
						// ...while the app draws its own richer rows
						PopupSubMenuOverride = (subMenu) => subMenu.CreateMenuItem("Rich Row"),
					},
				},
				theme);

			var subMenu = OpenSubMenu(popupMenu);

			await Assert.That(ChildNames(subMenu)).IsEqualTo("Rich Row Menu Item");
		}

		[Test]
		public async Task ClickingABuiltItemRunsTheModelAction()
		{
			var theme = new ThemeConfig();
			var popupMenu = new PopupMenu(theme);

			int clickCount = 0;

			MenuModelPopupBuilder.AddItems(
				popupMenu,
				new List<MenuItemModel>
				{
					new MenuItemModel() { Text = "Do It", Action = () => clickCount++ },
					new MenuItemModel() { Text = "No Action" },
				},
				theme);

			var items = popupMenu.Children.OfType<PopupMenu.MenuItem>().ToList();

			items[0].InvokeClick();

			// An item with no action must click harmlessly rather than throwing
			items[1].InvokeClick();

			await Assert.That(clickCount).IsEqualTo(1);
		}

		/// <summary>
		/// The names of <paramref name="popupMenu"/>'s children joined in order, so a single assertion covers
		/// both what was built and the order it was built in (TUnit's IsEquivalentTo ignores order).
		/// </summary>
		private static string ChildNames(PopupMenu popupMenu)
		{
			return string.Join("|", popupMenu.Children.Select(child => child.Name));
		}

		/// <summary>
		/// Clicks the (single) sub menu button in <paramref name="popupMenu"/> and returns the menu it opens.
		/// </summary>
		/// <remarks>
		/// Sub menus are populated from <see cref="UiThread.RunOnIdle"/> inside PopupMenu.CreateSubMenu, so
		/// nothing exists to assert on until the idle queue is pumped.
		/// </remarks>
		private static PopupMenu OpenSubMenu(PopupMenu popupMenu)
		{
			var subMenuButton = popupMenu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			subMenuButton.InvokeClick();

			UiThread.InvokePendingActions();

			return subMenuButton.SubMenu;
		}
	}
}
