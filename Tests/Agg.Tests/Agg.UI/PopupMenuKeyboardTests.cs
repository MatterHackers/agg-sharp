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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Keyboard navigation for <see cref="PopupMenu"/>, ported from the Rust <c>agg-gui</c> menu suite
	/// (<c>agg-gui/src/widgets/menu/state.rs</c> <c>handle_key</c> / <c>step_hover</c>). Each test names the
	/// rule it pins, and any deliberate difference from agg-gui carries a <c>DIVERGES from agg-gui</c> note.
	/// </summary>
	/// <remarks>
	/// Headless, in the style of <c>PopupSubMenuScrollTests</c> and <c>PopupMenuConformanceTests</c>: a real
	/// <see cref="SystemWindow"/> is driven as a plain widget and key events are pushed into it, so they take
	/// the same focus-chain route they take in a running app.
	/// <para>
	/// agg-sharp has no separate "hover index" - the highlight *is* keyboard focus, so these tests read
	/// <see cref="GuiWidget.Focused"/> to find the highlighted row.
	/// </para>
	/// <para>
	/// Menus close (and sub menus populate) from <see cref="UiThread.RunOnIdle"/>, and the pending action
	/// queue is process wide, so this class shares the constraint key every other queue draining class uses.
	/// </para>
	/// </remarks>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class PopupMenuKeyboardTests
	{
		/// <summary>
		/// Runs out whatever this test left on the idle queue.
		/// </summary>
		/// <remarks>
		/// <see cref="UiThread"/>'s pending action queue is process wide, and menus close (and sub menus
		/// build) from it. A test that ends with work still queued hands that work to whichever test pumps
		/// next, which then owns any exception it throws - a failure that rotates between innocent tests and
		/// reads as flake. Draining here keeps a test's leftovers inside its own result.
		/// </remarks>
		[After(Test)]
		public void DrainTheIdleQueue()
		{
			for (int i = 0; i < 4; i++)
			{
				UiThread.InvokePendingActions();
			}
		}

		/// <summary>
		/// agg-gui <c>step_hover</c>: with nothing hovered, Down uses a base of -1, so it lands on the first
		/// enabled row.
		/// </summary>
		[Test]
		public async Task DownFromNothingHighlightsTheFirstEnabledRow()
		{
			var harness = KeyMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("One");
				menu.CreateMenuItem("Two");
				menu.CreateMenuItem("Three");
			});

			await Assert.That(harness.HighlightedName).IsNull()
				.Because("a freshly shown menu highlights nothing");

			harness.KeyDown(Keys.Down);

			await Assert.That(harness.HighlightedName).IsEqualTo("One Menu Item");
		}

		/// <summary>
		/// agg-gui <c>step_hover</c>: with nothing hovered, Up uses a base of 0, so -1 wraps to the last
		/// enabled row.
		/// </summary>
		[Test]
		public async Task UpFromNothingHighlightsTheLastEnabledRow()
		{
			var harness = KeyMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("One");
				menu.CreateMenuItem("Two");
				menu.CreateMenuItem("Three");
			});

			harness.KeyDown(Keys.Up);

			await Assert.That(harness.HighlightedName).IsEqualTo("Three Menu Item");
		}

		/// <summary>
		/// agg-gui <c>step_hover</c> uses <c>rem_euclid</c>, so stepping past either end wraps rather than
		/// sticking.
		/// </summary>
		[Test]
		public async Task SteppingWrapsAtBothEnds()
		{
			var harness = KeyMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("One");
				menu.CreateMenuItem("Two");
			});

			harness.KeyDown(Keys.Down);
			harness.KeyDown(Keys.Down);
			await Assert.That(harness.HighlightedName).IsEqualTo("Two Menu Item");

			harness.KeyDown(Keys.Down);
			await Assert.That(harness.HighlightedName).IsEqualTo("One Menu Item")
				.Because("Down off the end wraps to the first row");

			harness.KeyDown(Keys.Up);
			await Assert.That(harness.HighlightedName).IsEqualTo("Two Menu Item")
				.Because("Up off the front wraps to the last row");
		}

		/// <summary>
		/// agg-gui <c>step_hover</c> walks a list of *enabled item* indices, so disabled rows and anything
		/// that is not a row at all (agg-sharp separators are <see cref="HorizontalLine"/>s) are stepped over.
		/// </summary>
		[Test]
		public async Task SteppingSkipsDisabledRowsAndSeparators()
		{
			var harness = KeyMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("One");
				menu.CreateSeparator();
				menu.CreateMenuItem("Disabled").Enabled = false;
				menu.CreateMenuItem("Three");
			});

			harness.KeyDown(Keys.Down);
			await Assert.That(harness.HighlightedName).IsEqualTo("One Menu Item");

			harness.KeyDown(Keys.Down);
			await Assert.That(harness.HighlightedName).IsEqualTo("Three Menu Item")
				.Because("the separator and the disabled row are not stops");

			// and wrapping the other way skips them too
			harness.KeyDown(Keys.Up);
			await Assert.That(harness.HighlightedName).IsEqualTo("One Menu Item");
		}

		/// <summary>
		/// agg-gui returns <c>EventResult::Consumed</c> for every key an open menu sees. agg-sharp menus live
		/// inside an application whose root window also listens for unhandled arrows (MatterCAD rotates the
		/// scene on them), so an arrow that leaks past an open menu is a visible bug.
		/// </summary>
		[Test]
		public async Task ArrowKeysAreConsumedSoTheyDoNotReachTheWindowBehindTheMenu()
		{
			var harness = KeyMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("One");
				menu.CreateMenuItem("Two");
			});

			int unhandledAtWindow = 0;
			harness.Window.KeyDown += (s, e) =>
			{
				if (!e.Handled)
				{
					unhandledAtWindow++;
				}
			};

			await Assert.That(harness.KeyDown(Keys.Down).Handled).IsTrue();
			await Assert.That(harness.KeyDown(Keys.Up).Handled).IsTrue();

			await Assert.That(unhandledAtWindow).IsEqualTo(0);
		}

		/// <summary>
		/// A menu taller than its window is reparented into a <see cref="ScrollableWidget"/> by
		/// <c>MakeMenuHaveScroll</c>. Stepping has to find the rows in their new home *and* scroll the
		/// highlighted one into view, or the highlight walks off the bottom of the visible strip.
		/// </summary>
		[Test]
		public async Task SteppingScrollsTheHighlightedRowIntoViewInATallMenu()
		{
			var harness = KeyMenuHarness.Show(
				menu =>
				{
					for (int i = 0; i < 20; i++)
					{
						menu.CreateMenuItem($"Recent {i}").MinimumSize = new Vector2(150, 48);
					}
				},
				windowSize: new Vector2(400, 300));

			// The menu really did get the scrolling treatment, otherwise this proves nothing
			await Assert.That(harness.Menu.Descendants<ScrollableWidget>().Any()).IsTrue();

			// Walk to the very last row
			for (int i = 0; i < 20; i++)
			{
				harness.KeyDown(Keys.Down);
			}

			await Assert.That(harness.HighlightedName).IsEqualTo("Recent 19 Menu Item")
				.Because("the rows are still found once they live inside the scroll area");

			var highlighted = harness.Highlighted;
			var rowOnScreen = highlighted.TransformToScreenSpace(highlighted.LocalBounds);
			var menuOnScreen = harness.Menu.TransformToScreenSpace(harness.Menu.LocalBounds);

			await Assert.That(rowOnScreen.Bottom).IsGreaterThanOrEqualTo(menuOnScreen.Bottom - 0.001);
			await Assert.That(rowOnScreen.Top).IsLessThanOrEqualTo(menuOnScreen.Top + 0.001);
		}

		/// <summary>
		/// Keeping the highlight visible must not mean re-centering the list under it. A row that is already
		/// fully on screen is left exactly where it is, so stepping through a long menu reads as a moving
		/// highlight over a still list rather than a still highlight over a list that jumps every keystroke.
		/// </summary>
		/// <remarks>
		/// This is the popup hosted half of <see cref="SteppingScrollsTheHighlightedRowIntoViewInATallMenu"/>.
		/// The two halves reach two different scrollers - <c>PopupWidget</c> owns one when it hosts the menu,
		/// <c>MakeMenuHaveScroll</c> builds one when it does not - and only the second used to do the minimum
		/// scroll that <see cref="ScrollableWidget.ScrollIntoView"/> does.
		/// </remarks>
		[Test]
		public async Task SteppingAPopupWidgetHostedMenuLeavesAnAlreadyVisibleRowWhereItIs()
		{
			var hosted = HostedMenu.Show(
				menu =>
				{
					for (int i = 0; i < 20; i++)
					{
						menu.CreateMenuItem($"Recent {i}").MinimumSize = new Vector2(150, 48);
					}
				},
				makeScrollable: true,
				maxHeight: 200);

			var scrollWindow = hosted.ScrollWindow;

			// The clamp really did engage, otherwise there is no scrolling to be wrong about
			await Assert.That(scrollWindow.Height).IsLessThan(hosted.Menu.Height);

			var scrollAtTop = scrollWindow.ScrollPosition.Y;

			// Step into the middle of the list first. At either end the scroll position is pinned by its own
			// limits, so a re-centering bug hides there - only an unpinned list can show it.
			for (int i = 0; i < 8; i++)
			{
				hosted.KeyDown(Keys.Down);
			}

			var scrolledIntoTheMiddle = scrollWindow.ScrollPosition.Y;
			await Assert.That(scrolledIntoTheMiddle).IsNotEqualTo(scrollAtTop)
				.Because("the list is genuinely scrolled off its starting position, not pinned there");

			// The row above the highlight is on screen already, so backing onto it asks for no scroll at all
			hosted.KeyDown(Keys.Up);

			await Assert.That(hosted.HighlightedName).IsEqualTo("Recent 6 Menu Item");
			await Assert.That(scrollWindow.ScrollPosition.Y).IsEqualTo(scrolledIntoTheMiddle).Within(0.001)
				.Because("that row was already fully visible, so nothing needed to move");
		}

		/// <summary>
		/// The other half of the same rule: a row that really is below the fold is brought just far enough
		/// to be seen, which leaves it flush with the near edge rather than parked in the middle.
		/// </summary>
		[Test]
		public async Task SteppingToAnOffScreenRowInAHostedMenuScrollsTheMinimum()
		{
			var hosted = HostedMenu.Show(
				menu =>
				{
					for (int i = 0; i < 20; i++)
					{
						menu.CreateMenuItem($"Recent {i}").MinimumSize = new Vector2(150, 48);
					}
				},
				makeScrollable: true,
				maxHeight: 200);

			var scrollWindow = hosted.ScrollWindow;

			// Eight rows into a viewport that holds roughly four is well past the fold
			for (int i = 0; i < 8; i++)
			{
				hosted.KeyDown(Keys.Down);
			}

			await Assert.That(hosted.HighlightedName).IsEqualTo("Recent 7 Menu Item");

			var highlighted = hosted.Highlighted;
			var rowOnScreen = highlighted.TransformToScreenSpace(highlighted.LocalBounds);
			var viewOnScreen = scrollWindow.TransformToScreenSpace(scrollWindow.LocalBounds);

			// Stepping downward off the bottom edge leaves the row against that edge. Centering it would
			// leave most of a viewport of rows below it, which is what this rules out.
			await Assert.That(rowOnScreen.Bottom).IsEqualTo(viewOnScreen.Bottom).Within(1.0);
		}

		/// <summary>
		/// Ports agg-gui <c>escape_closes_active_menu</c>, over the <c>ShowMenu</c> path every right click
		/// menu takes.
		/// </summary>
		[Test]
		public async Task EscapeClosesAnActiveMenu()
		{
			var harness = KeyMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("One");
				menu.CreateMenuItem("Two");
			});

			await Assert.That(harness.Menu.HasBeenClosed).IsFalse();

			var keyEvent = harness.KeyDown(Keys.Escape);
			harness.PumpIdle();

			await Assert.That(harness.Menu.HasBeenClosed).IsTrue();

			// agg-gui returns Consumed for Escape, and MatterCAD's root window cancels the current scene
			// operation on an unhandled Escape - a leak here would cancel a drag behind the menu
			await Assert.That(keyEvent.Handled).IsTrue();
		}

		/// <summary>
		/// Escape has to reach a menu that a <see cref="PopupWidget"/> is hosting too. That is the
		/// <c>PopupButton</c> shape: the popup takes focus, not the menu inside it, so without the shim in
		/// <see cref="PopupWidget.OnKeyDown"/> the menu never sees a key at all.
		/// </summary>
		[Test]
		public async Task EscapeClosesAPopupWidgetHostedMenu()
		{
			var hosted = HostedMenu.Show(menu =>
			{
				menu.CreateMenuItem("One");
				menu.CreateMenuItem("Two");
			});

			await Assert.That(hosted.Popup.HasBeenClosed).IsFalse();

			var keyEvent = hosted.KeyDown(Keys.Escape);
			hosted.PumpIdle();

			await Assert.That(hosted.Popup.HasBeenClosed).IsTrue();
			await Assert.That(keyEvent.Handled).IsTrue();
		}

		/// <summary>
		/// The stepping half of the same shim: arrows have to reach a hosted menu, not just Escape.
		/// </summary>
		[Test]
		public async Task ArrowKeysStepAPopupWidgetHostedMenu()
		{
			var hosted = HostedMenu.Show(menu =>
			{
				menu.CreateMenuItem("One");
				menu.CreateMenuItem("Two");
			});

			hosted.KeyDown(Keys.Down);
			await Assert.That(hosted.HighlightedName).IsEqualTo("One Menu Item");

			hosted.KeyDown(Keys.Down);
			await Assert.That(hosted.HighlightedName).IsEqualTo("Two Menu Item")
				.Because("once a row has focus the key routes through it, and must not be delivered twice");
		}

		/// <summary>
		/// agg-gui's <c>Key::Escape</c> arm calls <c>close()</c>, which drops the whole open path rather than
		/// one level of it. agg-sharp gets the same result from unfocusing the deepest menu: each level's
		/// Closed handler closes its parent when the parent does not contain focus.
		/// </summary>
		[Test]
		public async Task EscapeClosesAWholeSubMenuChain()
		{
			var harness = KeyMenuHarness.Show(menu =>
			{
				menu.CreateSubMenu(
					"More",
					menu.Theme,
					subMenu => subMenu.CreateSubMenu(
						"Even More",
						menu.Theme,
						leafMenu => leafMenu.CreateMenuItem("Leaf")));
			});

			var topButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().First();
			topButton.InvokeClick();
			harness.PumpIdle();

			var subMenu = topButton.SubMenu;
			await Assert.That(subMenu).IsNotNull();

			var subButton = subMenu.Children.OfType<PopupMenu.SubMenuItemButton>().First();
			subButton.InvokeClick();
			harness.PumpIdle();

			var leafMenu = subButton.SubMenu;
			await Assert.That(leafMenu).IsNotNull();
			await Assert.That(leafMenu.ContainsFocus).IsTrue()
				.Because("the newest menu is the one the keyboard is talking to");

			harness.KeyDown(Keys.Escape);
			harness.PumpIdle();

			await Assert.That(leafMenu.HasBeenClosed).IsTrue();
			await Assert.That(subMenu.HasBeenClosed).IsTrue();
			await Assert.That(harness.Menu.HasBeenClosed).IsTrue();
		}

		/// <summary>
		/// The <see cref="PopupWidget.OnKeyDown"/> shim must not disturb <see cref="DropDownList"/>, whose
		/// content widget is a plain flow layout rather than a <see cref="PopupMenu"/> and which drives its
		/// own Up/Down/Enter/Escape and type-ahead off the popup's KeyDown event.
		/// </summary>
		[Test]
		public async Task DropDownListKeyboardIsUnaffected()
		{
			var window = new SystemWindow(600, 400);

			var dropDownList = new DropDownList("- select -", Color.Black)
			{
				Name = "Fruit",
			};
			dropDownList.AddItem("Apple");
			dropDownList.AddItem("Banana");
			dropDownList.AddItem("Cherry");
			window.AddChild(dropDownList);
			dropDownList.Position = new Vector2(10, 200);

			// Down on the closed list opens it, exactly as DropDownList.OnKeyUp says
			dropDownList.Focus();
			window.OnKeyUp(new KeyEventArgs(Keys.Down));

			// DropDownContainer is internal to Gui, so it is found the way the automation framework would -
			// by the name it gives itself
			await Assert.That(OpenDropDown(window)).IsNotNull();

			// Stepping and Enter pick a row
			window.OnKeyDown(new KeyEventArgs(Keys.Down));
			window.OnKeyDown(new KeyEventArgs(Keys.Down));
			window.OnKeyDown(new KeyEventArgs(Keys.Enter));
			UiThread.InvokePendingActions();

			await Assert.That(dropDownList.SelectedIndex).IsEqualTo(2);

			// Escape closes without changing the selection
			dropDownList.Focus();
			window.OnKeyUp(new KeyEventArgs(Keys.Down));
			var container = OpenDropDown(window);

			window.OnKeyDown(new KeyEventArgs(Keys.Escape));
			UiThread.InvokePendingActions();

			await Assert.That(container.HasBeenClosed).IsTrue();
			await Assert.That(dropDownList.SelectedIndex).IsEqualTo(2);

			// Type-ahead still walks the list by prefix
			dropDownList.Focus();
			window.OnKeyUp(new KeyEventArgs(Keys.Down));
			window.OnKeyPress(new KeyPressEventArgs('B'));
			window.OnKeyDown(new KeyEventArgs(Keys.Enter));
			UiThread.InvokePendingActions();

			await Assert.That(dropDownList.SelectedIndex).IsEqualTo(1);
		}

		/// <summary>
		/// Ports agg-gui <c>keyboard_navigation_activates_hovered_row</c>: step to a row, press Enter, and
		/// the row's action runs.
		/// </summary>
		/// <remarks>
		/// agg-sharp needs no code of its own for this - the highlight is real focus, and
		/// <see cref="ThemedButton.OnKeyUp"/> clicks a focused button on Enter or Space. The test still
		/// belongs here: it is the claim that keyboard stepping ends somewhere useful.
		/// </remarks>
		[Test]
		[Arguments(Keys.Enter)]
		[Arguments(Keys.Space)]
		public async Task SteppingThenActivatingRunsTheHighlightedRow(Keys activationKey)
		{
			int openCount = 0;
			int closeCount = 0;

			var harness = KeyMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open").Click += (s, e) => openCount++;
				menu.CreateMenuItem("Close").Click += (s, e) => closeCount++;
			});

			harness.KeyDown(Keys.Down);
			harness.KeyDown(Keys.Down);
			await Assert.That(harness.HighlightedName).IsEqualTo("Close Menu Item");

			harness.KeyDown(activationKey);
			harness.KeyUp(activationKey);

			// ThemedButton activates from RunOnIdle, and the click then closes the menu from another
			harness.PumpIdle();

			await Assert.That(closeCount).IsEqualTo(1);
			await Assert.That(openCount).IsEqualTo(0);
			await Assert.That(harness.Menu.HasBeenClosed).IsTrue()
				.Because("choosing a row takes the menu down, just as clicking it does");
		}

		/// <summary>
		/// Nothing highlighted means nothing to activate - agg-gui's Enter arm is a no-op when
		/// <c>hover_path</c> is None.
		/// </summary>
		[Test]
		public async Task ActivatingWithNothingHighlightedDoesNothing()
		{
			int firedCount = 0;

			var harness = KeyMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open").Click += (s, e) => firedCount++;
				menu.CreateMenuItem("Close").Click += (s, e) => firedCount++;
			});

			harness.KeyDown(Keys.Enter);
			harness.KeyUp(Keys.Enter);
			harness.PumpIdle();

			await Assert.That(firedCount).IsEqualTo(0);
			await Assert.That(harness.Menu.HasBeenClosed).IsFalse();
		}

		/// <summary>
		/// agg-gui's <c>Key::ArrowRight</c> arm opens the hovered row's sub menu.
		/// </summary>
		[Test]
		public async Task RightOpensTheSubMenuOfTheHighlightedRow()
		{
			var harness = KeyMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open");
				menu.CreateSubMenu("More", menu.Theme, subMenu => subMenu.CreateMenuItem("Leaf"));
			});

			var subMenuButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			harness.KeyDown(Keys.Down);
			harness.KeyDown(Keys.Down);
			await Assert.That(harness.HighlightedName).IsEqualTo("More Menu Item");

			var keyEvent = harness.KeyDown(Keys.Right);
			harness.PumpIdle();

			await Assert.That(subMenuButton.SubMenu).IsNotNull();
			await Assert.That(keyEvent.Handled).IsTrue();
		}

		/// <summary>
		/// agg-gui's Enter arm opens a sub menu rather than firing an action when the hovered row has one.
		/// agg-sharp gets that from the row's ordinary click path, which is what opens sub menus.
		/// </summary>
		[Test]
		public async Task EnterOnASubMenuRowOpensItsSubMenu()
		{
			var harness = KeyMenuHarness.Show(menu =>
			{
				menu.CreateSubMenu("More", menu.Theme, subMenu => subMenu.CreateMenuItem("Leaf"));
			});

			var subMenuButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			harness.KeyDown(Keys.Down);
			harness.KeyDown(Keys.Enter);
			harness.KeyUp(Keys.Enter);
			harness.PumpIdle();

			await Assert.That(subMenuButton.SubMenu).IsNotNull();
			await Assert.That(harness.Menu.HasBeenClosed).IsFalse()
				.Because("opening a sub menu is not choosing an action");
		}

		/// <summary>
		/// agg-gui's <c>Key::ArrowLeft</c> arm pops one level off <c>open_path</c> and hovers what opened it.
		/// The parent menu has to survive that, which is why the opener is focused *before* the sub menu
		/// loses focus - <c>CreateSubMenu</c>'s Closed handler closes the parent too when the parent does
		/// not contain focus.
		/// </summary>
		[Test]
		public async Task LeftInASubMenuClosesOnlyThatLevelAndHighlightsItsOpener()
		{
			var harness = KeyMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open");
				menu.CreateSubMenu("More", menu.Theme, subMenu => subMenu.CreateMenuItem("Leaf"));
			});

			var subMenuButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			subMenuButton.InvokeClick();
			harness.PumpIdle();

			var subMenu = subMenuButton.SubMenu;
			await Assert.That(subMenu).IsNotNull();

			var keyEvent = harness.KeyDown(Keys.Left);
			harness.PumpIdle();

			await Assert.That(subMenu.HasBeenClosed).IsTrue();
			await Assert.That(harness.Menu.HasBeenClosed).IsFalse()
				.Because("Left backs out one level, it does not dismiss the menu");
			await Assert.That(harness.HighlightedName).IsEqualTo("More Menu Item")
				.Because("the row that opened the sub menu becomes the highlight again");
			await Assert.That(subMenuButton.SubMenu).IsNull();
			await Assert.That(keyEvent.Handled).IsTrue();
		}

		/// <summary>
		/// There is no level to back out of in a top level menu, so Left does nothing - but it is still
		/// consumed. An arrow that escapes an open menu reaches MatterCAD's root window, which rotates the
		/// scene with it.
		/// </summary>
		[Test]
		public async Task LeftInATopLevelMenuDoesNothingAndIsStillConsumed()
		{
			var harness = KeyMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("One");
				menu.CreateMenuItem("Two");
			});

			harness.KeyDown(Keys.Down);

			var keyEvent = harness.KeyDown(Keys.Left);
			harness.PumpIdle();

			await Assert.That(harness.Menu.HasBeenClosed).IsFalse();
			await Assert.That(harness.HighlightedName).IsEqualTo("One Menu Item");
			await Assert.That(keyEvent.Handled).IsTrue();
		}

		/// <summary>
		/// The open drop down list popup, or null when none is up. <c>DropDownContainer</c> is internal to
		/// Gui, so it is located by the name it gives itself.
		/// </summary>
		private static PopupWidget OpenDropDown(SystemWindow window)
		{
			return window.Descendants<PopupWidget>().FirstOrDefault(w => w.Name == "_OpenMenuContents" && !w.HasBeenClosed);
		}

		/// <summary>
		/// A <see cref="PopupMenu"/> hosted inside a <see cref="PopupWidget"/> - the shape a
		/// <c>PopupButton</c> builds, assembled here from agg-sharp parts only.
		/// </summary>
		private class HostedMenu
		{
			private HostedMenu(SystemWindow window, PopupWidget popup, PopupMenu menu)
			{
				this.Window = window;
				this.Popup = popup;
				this.Menu = menu;
			}

			public SystemWindow Window { get; }

			public PopupWidget Popup { get; }

			public PopupMenu Menu { get; }

			public PopupMenu.MenuItem Highlighted
				=> Window.Descendants<PopupMenu.MenuItem>().FirstOrDefault(item => item.Focused);

			public string HighlightedName => Highlighted?.Name;

			/// <summary>The viewport the popup clamps a tall menu into. <c>PopupWidget</c> keeps it private.</summary>
			public ScrollableWidget ScrollWindow => Popup.Descendants<ScrollableWidget>().First();

			public static HostedMenu Show(Action<PopupMenu> populate, bool makeScrollable = false, double maxHeight = 0)
			{
				var window = new SystemWindow(600, 400);
				var theme = new ThemeConfig();

				var anchor = new GuiWidget(50, 20)
				{
					Name = "Anchor",
				};
				window.AddChild(anchor);
				anchor.Position = new Vector2(10, 200);

				var menu = new PopupMenu(theme);
				populate(menu);

				var layoutEngine = new PopupLayoutEngine(menu, anchor, Direction.Down, maxHeight, alignToRightEdge: false);
				var popup = new PopupWidget(menu, layoutEngine, makeScrollable);

				// PopupButton focuses the popup, not the menu inside it - that is the whole reason the shim
				// in PopupWidget.OnKeyDown has to exist
				popup.Focus();

				return new HostedMenu(window, popup, menu);
			}

			public KeyEventArgs KeyDown(Keys key)
			{
				var keyEvent = new KeyEventArgs(key);
				Window.OnKeyDown(keyEvent);

				return keyEvent;
			}

			public void PumpIdle(int passes = 4)
			{
				for (int i = 0; i < passes; i++)
				{
					UiThread.InvokePendingActions();
				}
			}
		}

		/// <summary>
		/// A window with an anchor and a <see cref="PopupMenu"/> opened over it through the same
		/// <c>ShowMenu</c> path every right click menu takes, plus the key pushing helpers.
		/// </summary>
		private class KeyMenuHarness
		{
			private KeyMenuHarness(SystemWindow window, GuiWidget anchor, PopupMenu menu)
			{
				this.Window = window;
				this.Anchor = anchor;
				this.Menu = menu;
			}

			public SystemWindow Window { get; }

			public GuiWidget Anchor { get; }

			public PopupMenu Menu { get; }

			/// <summary>The row that currently owns keyboard focus - agg-sharp's menu highlight.</summary>
			public PopupMenu.MenuItem Highlighted
				=> Window.Descendants<PopupMenu.MenuItem>().FirstOrDefault(item => item.Focused);

			public string HighlightedName => Highlighted?.Name;

			public static KeyMenuHarness Show(Action<PopupMenu> populate, Vector2? windowSize = null, Vector2? anchorPosition = null)
			{
				var size = windowSize ?? new Vector2(600, 400);
				var window = new SystemWindow(size.X, size.Y);
				var theme = new ThemeConfig();

				var anchor = new GuiWidget(50, 20)
				{
					Name = "Anchor",
				};
				window.AddChild(anchor);
				anchor.Position = anchorPosition ?? new Vector2(10, size.Y - 30);

				var menu = new PopupMenu(theme);
				populate(menu);

				menu.ShowMenu(anchor, Vector2.Zero);

				return new KeyMenuHarness(window, anchor, menu);
			}

			/// <summary>
			/// Pushes a key press into the window the way the platform layer does, and hands back the event
			/// so a test can read <see cref="KeyEventArgs.Handled"/>.
			/// </summary>
			public KeyEventArgs KeyDown(Keys key)
			{
				var keyEvent = new KeyEventArgs(key);
				Window.OnKeyDown(keyEvent);

				return keyEvent;
			}

			public KeyEventArgs KeyUp(Keys key)
			{
				var keyEvent = new KeyEventArgs(key);
				Window.OnKeyUp(keyEvent);

				return keyEvent;
			}

			/// <summary>
			/// Drains the idle queue. Menus close (and sub menus populate) from RunOnIdle, and those
			/// handlers queue more work, so a single pass is not enough.
			/// </summary>
			public void PumpIdle(int passes = 4)
			{
				for (int i = 0; i < passes; i++)
				{
					UiThread.InvokePendingActions();
				}
			}
		}
	}
}
