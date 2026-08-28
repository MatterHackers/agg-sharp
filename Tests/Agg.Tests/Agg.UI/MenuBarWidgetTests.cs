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
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Behavior tests for <see cref="MenuBarWidget"/>, ported from the Rust <c>agg-gui</c> MenuBar suite
	/// (<c>agg-gui/src/widgets/menu/widget/tests_1.rs</c> and <c>tests_2.rs</c>). Each test names the
	/// agg-gui test it came from; anywhere agg-sharp deliberately behaves differently is called out with a
	/// <c>DIVERGES from agg-gui</c> comment rather than the assertion being softened.
	/// </summary>
	/// <remarks>
	/// Headless, like <see cref="PopupMenuConformanceTests"/>: a real <see cref="SystemWindow"/> hosts the
	/// bar and mouse events are pushed straight into it. Click points always come from the laid out bounds
	/// of the title being clicked, so the tests do not pin the current font metrics.
	/// <para>
	/// Menus close from <see cref="UiThread.RunOnIdle"/> and that queue is process wide, so this class
	/// shares the constraint key every other queue draining class uses.
	/// </para>
	/// </remarks>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class MenuBarWidgetTests
	{
		/// <summary>
		/// Runs out whatever this test left on the idle queue, so a leftover close never fires inside the
		/// next test's result. Same reasoning as PopupMenuConformanceTests.DrainTheIdleQueue.
		/// </summary>
		[After(Test)]
		public void DrainTheIdleQueue()
		{
			for (int i = 0; i < 4; i++)
			{
				UiThread.InvokePendingActions();
			}
		}

		/// <summary>
		/// Ports agg-gui <c>simple_mouse_click_opens_menu_without_release_activation</c>: a press and
		/// release on a top level title leaves that menu open rather than activating anything.
		/// </summary>
		[Test]
		public async Task ClickOnATitleOpensItsMenu()
		{
			var harness = MenuBarHarness.Show();

			await Assert.That(harness.Bar.OpenMenuIndex).IsNull();

			harness.Click(harness.CenterOfTitle("File"));
			harness.PumpIdle();

			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0);
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(1);

			// The popup really is the File menu, built from the model's SubMenuItems
			await Assert.That(harness.Find("New Menu Item")).IsNotNull();
		}

		/// <summary>
		/// Ports agg-gui <c>click_on_currently_open_top_menu_closes_popup</c>: clicking the open title is
		/// the desktop toggle, not a reopen.
		/// </summary>
		[Test]
		public async Task ClickingTheOpenTitleClosesTheMenu()
		{
			var harness = MenuBarHarness.Show();

			harness.Click(harness.CenterOfTitle("File"));
			harness.PumpIdle();
			await Assert.That(harness.Bar.AnyMenuOpen).IsTrue();

			harness.Click(harness.CenterOfTitle("File"));
			harness.PumpIdle();

			await Assert.That(harness.Bar.AnyMenuOpen).IsFalse()
				.Because("clicking the currently open title must close it, not reopen it");
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(0);
		}

		/// <summary>
		/// Ports agg-gui <c>escape_closes_active_menu</c>.
		/// </summary>
		[Test]
		public async Task EscapeClosesTheOpenMenu()
		{
			var harness = MenuBarHarness.Show();

			harness.Click(harness.CenterOfTitle("File"));
			harness.PumpIdle();
			await Assert.That(harness.Bar.AnyMenuOpen).IsTrue();

			harness.Window.OnKeyDown(new KeyEventArgs(Keys.Escape));
			harness.PumpIdle();

			await Assert.That(harness.Bar.AnyMenuOpen).IsFalse()
				.Because("Escape must close the active menu");
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(0);
		}

		/// <summary>
		/// Ports agg-gui <c>top_level_menu_tracks_hover</c>. agg-gui asserts its <c>hover_index</c>; here
		/// the visible consequence is asserted instead - the title's background picks up the hover tint and
		/// gives it back when the cursor leaves. The titles are not selectable (the bar does the hit
		/// testing, exactly as agg-gui's <c>menu_at</c> does), so this highlight cannot come from
		/// ThemedButton's own hover handling.
		/// </summary>
		[Test]
		public async Task HoveringATitleHighlightsIt()
		{
			var harness = MenuBarHarness.Show();

			var file = harness.Title("File");
			var edit = harness.Title("Edit");

			var resting = file.BackgroundColor;

			harness.MoveTo(harness.CenterOfTitle("File"));

			await Assert.That(file.BackgroundColor).IsNotEqualTo(resting)
				.Because("the hovered title has to read differently from a resting one");
			await Assert.That(edit.BackgroundColor).IsEqualTo(resting)
				.Because("only the title under the cursor highlights");

			harness.MoveTo(harness.CenterOfTitle("Edit"));

			await Assert.That(file.BackgroundColor).IsEqualTo(resting);
			await Assert.That(edit.BackgroundColor).IsNotEqualTo(resting);
		}

		/// <summary>
		/// Ports the desktop half of agg-gui <c>mobile_backdrop_tap_dismisses_popup</c>: with a menu open, a
		/// press that lands on neither a title nor the popup body dismisses it. Asserted from the bar's own
		/// neutral space (right of the last title) because that is the point the bar itself has to handle -
		/// a press on the window backdrop is dismissed by the popup's existing outside click path, which
		/// PopupMenuConformanceTests already covers.
		/// </summary>
		[Test]
		public async Task ClickInNeutralSpaceClosesTheMenu()
		{
			var harness = MenuBarHarness.Show();

			harness.Click(harness.CenterOfTitle("File"));
			harness.PumpIdle();
			await Assert.That(harness.Bar.AnyMenuOpen).IsTrue();

			var neutral = harness.PointOnBarBesideTheTitles();
			await Assert.That(harness.TitleBounds("Edit").Contains(neutral)).IsFalse()
				.Because("the dismissing press has to land off every title");

			harness.Click(neutral);
			harness.PumpIdle();

			await Assert.That(harness.Bar.AnyMenuOpen).IsFalse();
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(0);
		}

		/// <summary>
		/// Ports agg-gui <c>hover_after_release_switches_open_top_menu_on_desktop</c>: with a menu already
		/// open, moving the cursor onto a different title switches the open popup to that title - no press
		/// needed. Hover alone never opens the first menu, which is why this test has to click first.
		/// </summary>
		[Test]
		public async Task HoveringASiblingTitleWhileOpenSwitchesTheMenu()
		{
			var harness = MenuBarHarness.Show();

			harness.Click(harness.CenterOfTitle("File"));
			harness.PumpIdle();
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0);

			harness.MoveTo(harness.CenterOfTitle("Edit"));
			harness.PumpIdle();

			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(1)
				.Because("moving onto a sibling title while a menu is open must switch to that menu");

			// The outgoing popup has to be the only casualty - Edit's is left standing
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(1);
			await Assert.That(harness.Find("Copy Menu Item")).IsNotNull();
			await Assert.That(harness.Find("New Menu Item")).IsNull();
		}

		/// <summary>
		/// Ports agg-gui <c>tap_on_other_top_menu_switches_open_popup</c>: pressing a sibling title while
		/// another menu is open opens the sibling, rather than only dismissing what was open. No mouse move
		/// precedes the press, so this is the press path rather than the hover switch.
		/// </summary>
		[Test]
		public async Task PressingASiblingTitleSwitchesWithoutAMouseMove()
		{
			var harness = MenuBarHarness.Show();

			harness.Click(harness.CenterOfTitle("File"));
			harness.PumpIdle();
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0);

			harness.Click(harness.CenterOfTitle("Edit"));
			harness.PumpIdle();

			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(1)
				.Because("pressing a different title must open it, not just close the one that was open");
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(1);
			await Assert.That(harness.Find("Copy Menu Item")).IsNotNull();
			await Assert.That(harness.Find("New Menu Item")).IsNull();
		}

		/// <summary>
		/// Ports agg-gui <c>desktop_press_press_press_neutral_closes_active_menu</c>: presses with no
		/// intervening releases - A opens, B switches, neutral space closes and it stays closed once the
		/// deferred teardown has run.
		/// </summary>
		[Test]
		public async Task PressPressThenNeutralPressClosesTheMenu()
		{
			var harness = MenuBarHarness.Show();

			harness.Press(harness.CenterOfTitle("File"));
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0);

			harness.Press(harness.CenterOfTitle("Edit"));
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(1);

			harness.Press(harness.PointOnBarBesideTheTitles());
			harness.PumpIdle();

			await Assert.That(harness.Bar.AnyMenuOpen).IsFalse();
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(0)
				.Because("the neutral press has to take down the menu the switch left open");

			// Nothing queued may bring it back
			harness.PumpIdle();
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(0);
		}

		/// <summary>
		/// Ports agg-gui <c>click_close_suppresses_hover_until_cursor_leaves</c>: after the toggle click that
		/// closes a menu, the title still under the cursor must stop reading as highlighted - otherwise a
		/// dismissed menu still looks selected. The suppression lifts once the cursor moves elsewhere.
		/// </summary>
		[Test]
		public async Task ClickToCloseSuppressesHoverUntilTheCursorLeaves()
		{
			var harness = MenuBarHarness.Show();

			var file = harness.Title("File");
			var resting = file.BackgroundColor;

			// The cursor is over File for the whole open/close cycle, which is what makes the stale
			// highlight possible in the first place
			harness.MoveTo(harness.CenterOfTitle("File"));
			await Assert.That(file.BackgroundColor).IsNotEqualTo(resting);

			harness.Click(harness.CenterOfTitle("File"));
			harness.PumpIdle();
			await Assert.That(harness.Bar.AnyMenuOpen).IsTrue();

			harness.Click(harness.CenterOfTitle("File"));
			harness.PumpIdle();

			await Assert.That(harness.Bar.AnyMenuOpen).IsFalse();
			await Assert.That(file.BackgroundColor).IsEqualTo(resting)
				.Because("the just closed title must not keep painting as hovered under a stationary cursor");

			// Leaving and coming back is a fresh hover
			harness.MoveTo(harness.CenterOfTitle("Edit"));
			await Assert.That(harness.Title("Edit").BackgroundColor).IsNotEqualTo(resting);

			harness.MoveTo(harness.CenterOfTitle("File"));
			await Assert.That(file.BackgroundColor).IsNotEqualTo(resting)
				.Because("suppression lasts only until the cursor leaves the title");
			await Assert.That(harness.Bar.AnyMenuOpen).IsFalse()
				.Because("hover alone never opens a menu - only a hover while one is already open switches");
		}

		/// <summary>
		/// Ports agg-gui <c>mouse_down_drag_release_activates_popup_item</c>: the press-drag-release gesture.
		/// The button goes down on a title, the cursor drags into the popup that opened under it, and letting
		/// go over a row chooses that row - all without an intervening click.
		/// </summary>
		/// <remarks>
		/// The bar holds the mouse capture for the whole gesture (agg-sharp routes every move and the up to
		/// the widget the press landed on), so the rows never see the pointer themselves. The highlight
		/// assertion is what proves the bar is feeding the popup rather than only guessing at the release.
		/// </remarks>
		[Test]
		public async Task PressDragToARowAndReleaseActivatesIt()
		{
			var harness = MenuBarHarness.Show();

			harness.Press(harness.CenterOfTitle("File"));
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0);

			var row = harness.CenterOfRow("New Menu Item");
			harness.DragTo(row);

			await Assert.That(harness.Find("New Menu Item").Focused).IsTrue()
				.Because("the row a drag from the bar is over has to take the highlight");

			harness.Release(row);
			harness.PumpIdle();

			await Assert.That(harness.Actions.Count).IsEqualTo(1)
				.Because("releasing over a row must choose it");
			await Assert.That(harness.Actions[0]).IsEqualTo("New");

			await Assert.That(harness.Bar.AnyMenuOpen).IsFalse();
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(0);
		}

		/// <summary>
		/// Ports agg-gui <c>moving_across_top_menus_switches_open_popup</c>: with the button still held after
		/// the press that opened a menu, dragging over a sibling title switches the open popup to it.
		/// </summary>
		/// <remarks>
		/// The tail of the test is agg-gui's <c>arm_mouse_up_activation</c> re-arm: the gesture is still in
		/// progress, so the menu the drag switched to has to answer to the release the same way the first one
		/// would have. Without it the second half of a single drag would be dead.
		/// </remarks>
		[Test]
		public async Task DraggingAcrossTitlesWhilePressedSwitchesTheMenu()
		{
			var harness = MenuBarHarness.Show();

			harness.Press(harness.CenterOfTitle("File"));
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0);

			harness.DragTo(harness.CenterOfTitle("Edit"));
			harness.PumpIdle();

			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(1)
				.Because("dragging over a sibling title with the button held must switch to that menu");
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(1);

			var row = harness.CenterOfRow("Copy Menu Item");
			harness.DragTo(row);
			harness.Release(row);
			harness.PumpIdle();

			await Assert.That(harness.Actions.Count).IsEqualTo(1)
				.Because("the switch has to leave the release still able to choose a row");
			await Assert.That(harness.Actions[0]).IsEqualTo("Copy");
		}

		/// <summary>
		/// Ports agg-gui <c>desktop_drag_and_release_on_sibling_keeps_new_menu_open</c>: a release that lands
		/// back on a title is the end of the gesture and nothing else. It is not an activation (there is no
		/// row under it) and it is not the click-to-close toggle - that belongs to the press.
		/// </summary>
		[Test]
		public async Task DragReleaseOnASiblingTitleKeepsItsMenuOpen()
		{
			var harness = MenuBarHarness.Show();

			harness.Press(harness.CenterOfTitle("File"));
			harness.DragTo(harness.CenterOfTitle("Edit"));
			harness.PumpIdle();
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(1);

			harness.Release(harness.CenterOfTitle("Edit"));
			harness.PumpIdle();

			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(1)
				.Because("letting go on a top level title must leave that title's menu up");
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(1);
			await Assert.That(harness.Actions.Count).IsEqualTo(0);
		}

		/// <summary>
		/// Ports agg-gui <c>desktop_drag_release_in_neutral_space_closes_popup</c>. Neutral space is agg-gui's
		/// <c>body_contains</c> read inside out - outside both the bar's titles and the open popup's body.
		/// </summary>
		[Test]
		public async Task DragReleaseInNeutralSpaceClosesTheMenu()
		{
			var harness = MenuBarHarness.Show();

			harness.Press(harness.CenterOfTitle("File"));
			await Assert.That(harness.Bar.AnyMenuOpen).IsTrue();

			var neutral = harness.PointOffTheBarAndAnyMenu();
			await Assert.That(harness.AnyOpenMenuCovers(neutral)).IsFalse()
				.Because("the release has to land off the popup body for this to be the neutral case");

			harness.DragTo(neutral);
			harness.Release(neutral);
			harness.PumpIdle();

			await Assert.That(harness.Bar.AnyMenuOpen).IsFalse();
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(0);
			await Assert.That(harness.Actions.Count).IsEqualTo(0);
		}

		/// <summary>
		/// Ports agg-gui <c>desktop_drag_switch_then_release_off_closes</c>: the same cancel, reached after the
		/// drag has already switched menus once.
		/// </summary>
		[Test]
		public async Task DragSwitchThenReleaseOffCloses()
		{
			var harness = MenuBarHarness.Show();

			harness.Press(harness.CenterOfTitle("File"));
			harness.DragTo(harness.CenterOfTitle("Edit"));
			harness.PumpIdle();
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(1);

			var neutral = harness.PointOffTheBarAndAnyMenu();
			await Assert.That(harness.AnyOpenMenuCovers(neutral)).IsFalse();

			harness.DragTo(neutral);
			harness.Release(neutral);
			harness.PumpIdle();

			await Assert.That(harness.Bar.AnyMenuOpen).IsFalse();
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(0);
			await Assert.That(harness.Actions.Count).IsEqualTo(0);
		}

		/// <summary>
		/// Ports agg-gui <c>arrow_keys_switch_open_top_menus</c>: with a menu open and no sub menu below it,
		/// Right walks the open menu to the next title and Left to the previous, wrapping at both ends
		/// (agg-gui's <c>rem_euclid</c>, so Left from the first title lands on the last).
		/// </summary>
		[Test]
		public async Task LeftAndRightArrowsSwitchTheOpenTopMenu()
		{
			var harness = MenuBarHarness.Show();

			harness.Click(harness.CenterOfTitle("File"));
			harness.PumpIdle();
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0);

			var keyEvent = harness.KeyDown(Keys.Right);
			harness.PumpIdle();

			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(1)
				.Because("Right with nothing to open in the menu itself moves along the bar");
			await Assert.That(keyEvent.Handled).IsTrue();
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(1);
			await Assert.That(harness.Find("Copy Menu Item")).IsNotNull();

			harness.KeyDown(Keys.Left);
			harness.PumpIdle();

			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0);
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(1);

			// The wrap: there is no title before the first one, so Left comes out on the last
			harness.KeyDown(Keys.Left);
			harness.PumpIdle();

			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(1)
				.Because("Left from the first title wraps to the last");

			harness.KeyDown(Keys.Right);
			harness.PumpIdle();

			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0)
				.Because("Right from the last title wraps to the first");
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(1)
				.Because("every switch leaves exactly one menu standing");
		}

		/// <summary>
		/// Ports the Right half of agg-gui's <c>should_switch_top_menu</c> guard: the row's own sub menu comes
		/// first. Only a Right the menu has no use for reaches the bar.
		/// </summary>
		[Test]
		public async Task ArrowRightOnASubmenuRowOpensTheSubmenuNotTheNextMenu()
		{
			var harness = MenuBarHarness.Show();

			harness.Click(harness.CenterOfTitle("File"));
			harness.PumpIdle();

			harness.HighlightRow("Recent Menu Item");

			harness.KeyDown(Keys.Right);
			harness.PumpIdle();

			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0)
				.Because("Right on a row that opens a sub menu must not walk the bar as well");
			await Assert.That(harness.SubMenuOf("Recent Menu Item")).IsNotNull();
			await Assert.That(harness.Find("Report Menu Item")).IsNotNull();
		}

		/// <summary>
		/// Ports the Left half of the same guard: a Left inside an open sub menu backs out of that one level
		/// and stops there - the bar never sees it, so the top level menu does not change.
		/// </summary>
		[Test]
		public async Task ArrowLeftInsideASubmenuBacksOutOneLevelOnly()
		{
			var harness = MenuBarHarness.Show();

			harness.Click(harness.CenterOfTitle("File"));
			harness.PumpIdle();

			harness.HighlightRow("Recent Menu Item");
			harness.KeyDown(Keys.Right);
			harness.PumpIdle();

			var subMenu = harness.SubMenuOf("Recent Menu Item");
			await Assert.That(subMenu).IsNotNull();

			harness.KeyDown(Keys.Left);
			harness.PumpIdle();

			await Assert.That(subMenu.HasBeenClosed).IsTrue()
				.Because("Left in a sub menu backs out of it");
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0)
				.Because("backing out of a sub menu is not a request to change top level menu");
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(1)
				.Because("only the sub menu goes - the menu it hung off is still up");
			await Assert.That(harness.Find("Recent Menu Item").Focused).IsTrue()
				.Because("the row that opened the sub menu takes the highlight back");
		}

		/// <summary>
		/// The sub menu case of the press-drag-release gesture: letting go on a row that opens a sub menu
		/// opens it rather than dismissing the chain the way a chosen action would.
		/// </summary>
		[Test]
		public async Task ReleasingADragOnASubmenuRowOpensTheSubmenu()
		{
			var harness = MenuBarHarness.Show();

			harness.Press(harness.CenterOfTitle("File"));
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0);

			var row = harness.CenterOfRow("Recent Menu Item");
			harness.DragTo(row);
			harness.Release(row);
			harness.PumpIdle();

			await Assert.That(harness.SubMenuOf("Recent Menu Item")).IsNotNull()
				.Because("releasing on a sub menu row opens its sub menu");
			await Assert.That(harness.Find("Report Menu Item")).IsNotNull();
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0)
				.Because("opening a sub menu is not choosing an action, so nothing is dismissed");
			await Assert.That(harness.Actions.Count).IsEqualTo(0);
		}

		/// <summary>
		/// A menu whose model comes back empty cannot open, and the bar must not keep asking. Building the
		/// model is the application's work - MatterCAD's menus walk the scene to decide what is in them - so a
		/// rebuild per mouse move over a title that will never open is real cost for nothing.
		/// </summary>
		[Test]
		public async Task HoveringATitleThatOpensNothingBuildsItsModelOnlyOnce()
		{
			int modelBuilds = 0;

			var harness = MenuBarHarness.Show(new MenuItemModel
			{
				Text = "Empty",
				SubMenuItems = () =>
				{
					modelBuilds++;

					return new List<MenuItemModel>();
				},
			});

			// Something has to be open for hover switching to run at all
			harness.Click(harness.CenterOfTitle("File"));
			harness.PumpIdle();
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0);

			var empty = harness.TitleBounds("Empty");

			for (int i = 0; i < 4; i++)
			{
				harness.MoveTo(new Vector2(empty.Left + 2 + i, empty.Center.Y));
				harness.PumpIdle();
			}

			await Assert.That(modelBuilds).IsEqualTo(1)
				.Because("a title that declined to open must not be re-attempted on every move over it");
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0)
				.Because("an empty menu leaves whatever was open alone");

			// Leaving and coming back is a fresh ask - the model may have something in it by then
			harness.MoveTo(harness.CenterOfTitle("File"));
			harness.MoveTo(empty.Center);
			harness.PumpIdle();

			await Assert.That(modelBuilds).IsEqualTo(2)
				.Because("the decline is remembered only while the cursor stays on that title");
		}

		/// <summary>
		/// The bar is not always at the top of the window - MatterCAD docks one at the bottom of the Variable
		/// Sheet editor - and a menu with no room below its title has to open upward instead of being clamped
		/// down over the bar. Same rule <see cref="PopupDirectionFlipTests"/> pins for drop downs.
		/// </summary>
		[Test]
		public async Task AMenuWithNoRoomBelowItsTitleOpensUpward()
		{
			var harness = MenuBarHarness.Show(barAnchor: VAnchor.Bottom);

			// A press rather than a click, so the drag half of the gesture below is still armed
			harness.Press(harness.CenterOfTitle("File"));
			harness.PumpIdle();

			var popup = harness.PopupBounds();
			var title = harness.TitleBounds("File");

			await Assert.That(popup.Bottom).IsGreaterThanOrEqualTo(title.Top - 0.001)
				.Because("with nothing below the title the panel has to hang above it, not cover the bar");
			await Assert.That(popup.Top).IsLessThanOrEqualTo(harness.Window.Height + 0.001)
				.Because("the flipped panel still has to be fully on screen");

			// The drag-release gesture reads positions in screen space, so it must work just as well when the
			// panel is above the press as below it
			var row = harness.CenterOfRow("New Menu Item");
			harness.DragTo(row);

			await Assert.That(harness.Find("New Menu Item").Focused).IsTrue()
				.Because("a drag up into the flipped panel has to highlight the row it reaches");
		}

		/// <summary>
		/// The other half of the flip: the preferred direction is still below, and a bar with room under it
		/// keeps opening that way.
		/// </summary>
		[Test]
		public async Task AMenuWithRoomBelowItsTitleOpensDownward()
		{
			var harness = MenuBarHarness.Show();

			harness.Click(harness.CenterOfTitle("File"));
			harness.PumpIdle();

			var popup = harness.PopupBounds();
			var title = harness.TitleBounds("File");

			await Assert.That(popup.Top).IsLessThanOrEqualTo(title.Bottom + 0.001)
				.Because("a title with room under it opens its menu downward");
			await Assert.That(popup.Bottom).IsGreaterThanOrEqualTo(-0.001);
		}

		/// <summary>
		/// A window hosting a <see cref="MenuBarWidget"/> built from a two menu model, with mouse events
		/// pushed through the window the way the platform would deliver them.
		/// </summary>
		private class MenuBarHarness
		{
			private MenuBarHarness(SystemWindow window, MenuBarWidget bar, List<string> actions)
			{
				this.Window = window;
				this.Bar = bar;
				this.Actions = actions;
			}

			public SystemWindow Window { get; }

			public MenuBarWidget Bar { get; }

			/// <summary>The text of every menu item that has been chosen, in the order they fired.</summary>
			public List<string> Actions { get; }

			/// <summary>Every menu panel still alive in the window.</summary>
			public List<PopupMenu> OpenMenus => Window.Descendants<PopupMenu>().Where(m => !m.HasBeenClosed).ToList();

			/// <summary>
			/// Shows the two menu fixture, optionally with <paramref name="extraMenu"/> added after it. The
			/// extra menu goes last so the tests that reach for "the title beside the others" and the arrow
			/// key wrap keep describing the same two menu bar.
			/// </summary>
			/// <param name="extraMenu">An optional third menu, added after File and Edit.</param>
			/// <param name="barAnchor">
			/// Where in the window the bar is docked. <see cref="VAnchor.Bottom"/> is the case with no room
			/// under the titles - MatterCAD's Variable Sheet editor docks its bar there.
			/// </param>
			public static MenuBarHarness Show(MenuItemModel extraMenu = null, VAnchor barAnchor = VAnchor.Top)
			{
				var window = new SystemWindow(600, 400);
				var theme = new ThemeConfig();

				var actions = new List<string>();

				MenuItemModel Item(string text)
				{
					return new MenuItemModel { Text = text, Action = () => actions.Add(text) };
				}

				var menus = new List<MenuItemModel>
				{
					new MenuItemModel
					{
						Text = "File",
						SubMenuItems = () => new List<MenuItemModel>
						{
							Item("New"),
							Item("Open"),

							// The one row in the fixture that opens a sub menu. The arrow key tests need a
							// menu where Right has somewhere of its own to go, and the drag tests need a row
							// whose release opens rather than chooses.
							new MenuItemModel
							{
								Text = "Recent",
								SubMenuItems = () => new List<MenuItemModel>
								{
									Item("Report"),
								},
							},
						},
					},
					new MenuItemModel
					{
						Text = "Edit",
						SubMenuItems = () => new List<MenuItemModel>
						{
							Item("Copy"),
						},
					},
				};

				if (extraMenu != null)
				{
					menus.Add(extraMenu);
				}

				var bar = new MenuBarWidget(menus, theme)
				{
					VAnchor = barAnchor,
				};

				// One container deep rather than parented straight to the window, and that one container is
				// what makes this harness model a real host. A widget with no parent has CanSelect false
				// (GuiWidget.CanSelect), and a top level SystemWindow has no parent, so the focus grab
				// GuiWidget.OnMouseDown performs as the press unwinds - "no child of mine came out of that
				// with the focus, so I will take it" - is a no-op for the window and used to be invisible
				// here. Every real host does perform it, and it is what took the focus back off a menu the
				// press had just opened. The host fills the window so every click point is unchanged.
				var host = new GuiWidget
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Stretch,
					Name = "Bar Host",
				};
				window.AddChild(host);
				host.AddChild(bar);

				return new MenuBarHarness(window, bar, actions);
			}

			public GuiWidget Find(string name)
			{
				return Window.FindDescendant(name);
			}

			public GuiWidget Title(string menuText)
			{
				return Find($"{menuText} Menu");
			}

			/// <summary>The laid out rectangle of a title, in window space.</summary>
			public RectangleDouble TitleBounds(string menuText)
			{
				var widget = Title(menuText);
				return widget.TransformToScreenSpace(widget.LocalBounds);
			}

			public Vector2 CenterOfTitle(string menuText)
			{
				return TitleBounds(menuText).Center;
			}

			/// <summary>The center of a popup row, in window space. Rows are named "&lt;text&gt; Menu Item".</summary>
			public Vector2 CenterOfRow(string rowName)
			{
				var row = Find(rowName);

				return row.TransformToScreenSpace(row.LocalBounds).Center;
			}

			/// <summary>The sub menu the named row currently has up, or null when it has none.</summary>
			public PopupMenu SubMenuOf(string rowName)
			{
				return (Find(rowName) as PopupMenu.SubMenuItemButton)?.SubMenu;
			}

			/// <summary>
			/// Steps the keyboard highlight onto the named row with Down arrows.
			/// </summary>
			/// <remarks>
			/// A loop rather than a counted run of Downs, so the test does not pin whether a freshly shown
			/// menu already starts with a row highlighted. Hovering the row instead is not an option - a
			/// hover on a sub menu row opens the sub menu, which is the thing under test.
			/// </remarks>
			public void HighlightRow(string rowName)
			{
				for (int i = 0; i < 10 && !Find(rowName).Focused; i++)
				{
					KeyDown(Keys.Down);
				}
			}

			public KeyEventArgs KeyDown(Keys key)
			{
				var keyEvent = new KeyEventArgs(key);

				Window.OnKeyDown(keyEvent);

				return keyEvent;
			}

			/// <summary>
			/// A point that is on neither the bar nor any open menu - agg-gui's neutral space. The bar hugs the
			/// top of the window and the menus hang off the titles on the left, so the bottom right corner is
			/// clear. The tests assert that rather than trusting it.
			/// </summary>
			public Vector2 PointOffTheBarAndAnyMenu()
			{
				return new Vector2(Window.Width - 10, 10);
			}

			/// <summary>The laid out rectangle of the one open menu panel, in window space.</summary>
			public RectangleDouble PopupBounds()
			{
				var popup = OpenMenus.Single();

				return popup.TransformToScreenSpace(popup.LocalBounds);
			}

			public bool AnyOpenMenuCovers(Vector2 point)
			{
				return OpenMenus.Any(menu => menu.TransformToScreenSpace(menu.LocalBounds).Contains(point));
			}

			/// <summary>
			/// A point on the bar that no title covers. The bar stretches the full window width while the
			/// titles only fit their text, so there is always slack to the right of the last one.
			/// </summary>
			public Vector2 PointOnBarBesideTheTitles()
			{
				var barBounds = Bar.TransformToScreenSpace(Bar.LocalBounds);
				var lastTitle = TitleBounds("Edit");

				return new Vector2((lastTitle.Right + barBounds.Right) / 2, barBounds.Center.Y);
			}

			public void Click(Vector2 point, MouseButtons button = MouseButtons.Left)
			{
				Window.OnMouseDown(new MouseEventArgs(button, 1, point.X, point.Y, 0));
				Window.OnMouseUp(new MouseEventArgs(button, 1, point.X, point.Y, 0));
			}

			/// <summary>A press with no matching release, for the press-press sequences agg-gui tests.</summary>
			public void Press(Vector2 point, MouseButtons button = MouseButtons.Left)
			{
				Window.OnMouseDown(new MouseEventArgs(button, 1, point.X, point.Y, 0));
			}

			/// <summary>The release half of a press-drag-release.</summary>
			public void Release(Vector2 point, MouseButtons button = MouseButtons.Left)
			{
				Window.OnMouseUp(new MouseEventArgs(button, 1, point.X, point.Y, 0));
			}

			/// <summary>
			/// A move with the left button still down. The platform reports the held button on the move, and
			/// the middle of a press-drag-release is nothing else.
			/// </summary>
			public void DragTo(Vector2 point)
			{
				Window.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, point.X, point.Y, 0));
			}

			public void MoveTo(Vector2 point)
			{
				Window.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, 0));
			}

			/// <summary>
			/// Drains the idle queue. Menus close from RunOnIdle and those handlers queue more work, so a
			/// single pass is not enough.
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
