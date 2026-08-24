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
using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Mac;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The parts of the native menu builder that are decisions rather than AppKit calls: which standard
	/// chord a role carries, and which items make it into a built menu. Nothing here touches objc - a menu
	/// can only be built on the main thread of a process with an NSApplication, which a test is not.
	/// </summary>
	public class MacMenuBarTests
	{
		[Test]
		public async Task RolesCarryTheirStandardChords()
		{
			await Assert.That(MacMenuBar.KeyEquivalentFor(MenuItemRole.Settings)).IsEqualTo(",");
			await Assert.That(MacMenuBar.KeyEquivalentFor(MenuItemRole.Quit)).IsEqualTo("q");
			await Assert.That(MacMenuBar.KeyEquivalentFor(MenuItemRole.OpenFile)).IsEqualTo("o");
		}

		[Test]
		public async Task RolesWithoutAStandardChordGetNone()
		{
			// About and Help have conventional positions but no conventional shortcut, and an item with no
			// role must never be given one. An empty key equivalent is what NSMenuItem wants for "none" -
			// never nil, which is why these are string.Empty and not null.
			await Assert.That(MacMenuBar.KeyEquivalentFor(MenuItemRole.None)).IsEqualTo(string.Empty);
			await Assert.That(MacMenuBar.KeyEquivalentFor(MenuItemRole.About)).IsEqualTo(string.Empty);
			await Assert.That(MacMenuBar.KeyEquivalentFor(MenuItemRole.Help)).IsEqualTo(string.Empty);
		}

		[Test]
		public async Task VisibilityGatesAreEvaluatedWhenTheMenuIsBuilt()
		{
			bool showOptional = false;

			var items = new List<MenuItemModel>
			{
				new MenuItemModel { Text = "Always" },
				new MenuItemModel { Text = "Optional", IsVisible = () => showOptional },
				new MenuItemModel { Text = "Never", IsVisible = () => false },
			};

			IReadOnlyList<MenuItemModel> first = MacMenuBar.VisibleItems(items);
			await Assert.That(first.Count).IsEqualTo(1);
			await Assert.That(first[0].Text).IsEqualTo("Always");

			// The same model, a second opening, a different answer - which is the whole reason the gate is a
			// delegate and the menu is rebuilt rather than built once.
			showOptional = true;

			IReadOnlyList<MenuItemModel> second = MacMenuBar.VisibleItems(items);
			await Assert.That(second.Count).IsEqualTo(2);
			await Assert.That(second[1].Text).IsEqualTo("Optional");
		}

		[Test]
		public async Task AnEmptyOrMissingListIsNoItems()
		{
			await Assert.That(MacMenuBar.VisibleItems(null).Count).IsEqualTo(0);
			await Assert.That(MacMenuBar.VisibleItems(new List<MenuItemModel>()).Count).IsEqualTo(0);
		}

		/// <summary>
		/// The menu bar draws no dividers, so a separator among the top level menus has nowhere to go; drawn
		/// anyway it would be a blank, unopenable gap. Its gates still apply like any other entry's, because
		/// a whole menu can be gated off (or, as here, drawn disabled) as easily as one item can.
		/// </summary>
		[Test]
		public async Task TheMenuBarTakesVisibleNonSeparatorMenusOnly()
		{
			var menus = new List<MenuItemModel>
			{
				new MenuItemModel { Text = "File" },
				new MenuItemModel { IsSeparator = true },
				new MenuItemModel { Text = "Hidden", IsVisible = () => false },
				new MenuItemModel { Text = "Off", IsEnabled = () => false },
			};

			IReadOnlyList<MenuItemModel> topLevel = MacMenuBar.TopLevelMenus(menus);

			await Assert.That(topLevel.Count).IsEqualTo(2);
			await Assert.That(topLevel[0].Text).IsEqualTo("File");
			await Assert.That(topLevel[1].Text).IsEqualTo("Off");

			// A disabled menu is still a menu in the bar - it is drawn greyed, not dropped.
			await Assert.That(MacMenuBar.IsEnabled(topLevel[0])).IsTrue();
			await Assert.That(MacMenuBar.IsEnabled(topLevel[1])).IsFalse();
		}

		/// <summary>
		/// What makes a native menu live: its children are asked for again every time it is about to open, so
		/// a list that grew while the application ran - the recent files - opens showing what it grew into.
		/// </summary>
		[Test]
		public async Task AskingForChildrenRunsTheProviderEachTime()
		{
			var recents = new List<MenuItemModel> { new MenuItemModel { Text = "First" } };
			int timesAsked = 0;

			var container = new MenuItemModel
			{
				Text = "Open Recent",
				SubMenuItems = () =>
				{
					timesAsked++;
					return recents;
				}
			};

			await Assert.That(MacMenuBar.ChildrenOf(container).Count).IsEqualTo(1);

			recents.Add(new MenuItemModel { Text = "Second" });

			IReadOnlyList<MenuItemModel> reopened = MacMenuBar.ChildrenOf(container);
			await Assert.That(reopened.Count).IsEqualTo(2);
			await Assert.That(reopened[1].Text).IsEqualTo("Second");
			await Assert.That(timesAsked).IsEqualTo(2);
		}

		[Test]
		public async Task AnItemThatDescribesNoChildrenHasNone()
		{
			// A leaf, and a submenu whose provider came back empty. Neither is a native menu's problem to
			// paper over: the model owns any "nothing here" placeholder it wants shown.
			await Assert.That(MacMenuBar.ChildrenOf(new MenuItemModel { Text = "Leaf" }).Count).IsEqualTo(0);
			await Assert.That(MacMenuBar.ChildrenOf(new MenuItemModel { SubMenuItems = () => new List<MenuItemModel>() }).Count).IsEqualTo(0);
		}

		[Test]
		public async Task AnUngatedItemIsEnabled()
		{
			await Assert.That(MacMenuBar.IsEnabled(new MenuItemModel { Text = "Plain" })).IsTrue();
			await Assert.That(MacMenuBar.IsEnabled(new MenuItemModel { IsEnabled = () => false })).IsFalse();
			await Assert.That(MacMenuBar.IsEnabled(new MenuItemModel { IsEnabled = () => true })).IsTrue();
		}
	}
}
