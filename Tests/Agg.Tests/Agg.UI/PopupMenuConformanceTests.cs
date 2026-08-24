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
using System.Reflection;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Behavior conformance tests for the <see cref="PopupMenu"/> stack, ported from the Rust
	/// <c>agg-gui</c> menu suite (<c>agg-gui/src/widgets/menu/mod.rs</c>). Each test names the agg-gui
	/// test it came from, and any place where agg-sharp deliberately (or accidentally) behaves
	/// differently is marked with a <c>DIVERGES from agg-gui</c> comment rather than being weakened.
	/// </summary>
	/// <remarks>
	/// These are headless: a <see cref="SystemWindow"/> is used as a plain widget and mouse events are
	/// pushed into it directly, exactly as <c>MouseInteractionTests</c> does. Click points are always
	/// derived from the real laid out bounds of the widget being clicked - agg-gui's suite samples
	/// <c>layouts[..].rows[..].rect</c> for the same reason, and hard coded row coordinates would only
	/// pin the current font metrics.
	/// <para>
	/// The menu machinery closes menus from <see cref="UiThread.RunOnIdle"/>, and the pending action
	/// queue is process wide, so this class shares the constraint key every other queue draining test
	/// class uses.
	/// </para>
	/// </remarks>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class PopupMenuConformanceTests
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
		/// Ports agg-gui <c>outside_click_dismisses_menu</c>.
		/// </summary>
		[Test]
		public async Task OutsideClickDismissesMenu()
		{
			var harness = MenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open");
				menu.CreateMenuItem("Close");
			});

			await Assert.That(harness.Menu.HasBeenClosed).IsFalse();

			var outside = harness.PointOutsideMenu();
			await Assert.That(harness.MenuBounds.Contains(outside)).IsFalse()
				.Because("the dismissing press has to land off every menu panel");

			harness.Click(outside);
			harness.PumpIdle();

			await Assert.That(harness.Menu.HasBeenClosed).IsTrue();
		}

		/// <summary>
		/// Ports the portable half of agg-gui <c>action_click_consumes_and_suppresses_followup_mouse_up</c>.
		/// agg-gui has an explicit <c>take_suppress_mouse_up</c> latch because its menu is painted into a
		/// single event stream; agg-sharp routes through the widget tree instead, so the equivalent claim
		/// is that the press is consumed by the menu and never reaches the widget the menu is drawn over.
		/// </summary>
		[Test]
		public async Task ActionClickFiresOnceAndDoesNotLeakToTheWidgetBeneath()
		{
			int openCount = 0;

			var harness = MenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open").Click += (s, e) => openCount++;
				menu.CreateMenuItem("Close");
			});

			harness.Click(harness.CenterOf("Open Menu Item"));
			harness.PumpIdle();

			await Assert.That(openCount).IsEqualTo(1);
			await Assert.That(harness.Menu.HasBeenClosed).IsTrue()
				.Because("an action item closes the menu it belongs to");

			// The menu covers the counting widget completely, and mouse routing hands the press to the
			// topmost child only, so nothing under the menu sees the press or the release that follows it.
			await Assert.That(harness.Beneath.LeftClicks).IsEqualTo(0);
			await Assert.That(harness.Beneath.MouseDowns).IsEqualTo(0);
			await Assert.That(harness.Beneath.MouseUps).IsEqualTo(0);
		}

		/// <summary>
		/// Ports agg-gui <c>disabled_rows_do_not_fire_actions</c>.
		/// </summary>
		[Test]
		public async Task DisabledRowsDoNotFireActionsAndLeaveTheMenuOpen()
		{
			int firedCount = 0;

			var harness = MenuHarness.Show(menu =>
			{
				var disabled = menu.CreateMenuItem("Disabled");
				disabled.Enabled = false;
				disabled.Click += (s, e) => firedCount++;

				menu.CreateMenuItem("Open");
			});

			// Nothing firing is the easiest assertion in the world to satisfy by accident, so prove the
			// press really landed on the disabled row before believing the row is what refused it
			var disabledBounds = harness.BoundsOf("Disabled Menu Item");
			await Assert.That(disabledBounds.Height).IsGreaterThan(0);
			await Assert.That(harness.MenuBounds.Contains(disabledBounds.Center)).IsTrue();

			harness.Click(disabledBounds.Center);
			harness.PumpIdle();

			await Assert.That(firedCount).IsEqualTo(0);

			// Matches agg-gui: a press on a disabled row is absorbed by the menu panel, which keeps focus,
			// so the menu is still there for the user's next attempt. Mouse routing skips disabled children
			// outright (GuiWidget.OnMouseDown), so the PopupMenu itself takes the press.
			await Assert.That(harness.Menu.HasBeenClosed).IsFalse();
		}

		/// <summary>
		/// Ports the check half of agg-gui <c>keep_open_check_and_radio_actions_do_not_close</c>.
		/// </summary>
		[Test]
		public async Task CheckboxMenuItemTogglesAndKeepsTheMenuOpen()
		{
			bool checkState = false;

			var harness = MenuHarness.Show(menu =>
			{
				menu.CreateBoolMenuItem("Check", () => checkState, value => checkState = value);
				menu.CreateMenuItem("Open");
			});

			var checkItem = (PopupMenu.CheckboxMenuItem)harness.Find("Check Menu Item");

			harness.Click(harness.CenterOf("Check Menu Item"));
			harness.PumpIdle();

			await Assert.That(checkState).IsTrue();
			await Assert.That(checkItem.Checked).IsTrue();

			// agg-gui needs an explicit opt in (`keep_open()`) for this; agg-sharp gets it for free because
			// CreateBoolMenuItem, unlike CreateMenuItem, never unfocuses the item it built - so the popup
			// still contains focus and its close-on-focus-lost path never runs.
			await Assert.That(harness.Menu.HasBeenClosed).IsFalse();

			// A second press toggles back off, which is the whole point of leaving the menu up
			harness.Click(harness.CenterOf("Check Menu Item"));
			harness.PumpIdle();

			await Assert.That(checkState).IsFalse();
			await Assert.That(checkItem.Checked).IsFalse();
			await Assert.That(harness.Menu.HasBeenClosed).IsFalse();
		}

		/// <summary>
		/// Ports the radio half of agg-gui <c>keep_open_check_and_radio_actions_do_not_close</c>: picking a
		/// radio row checks it, unchecks every sibling, and re-picking the checked row is a no-op.
		/// </summary>
		[Test]
		public async Task RadioMenuItemsAreExclusiveAndKeepTheMenuOpen()
		{
			var siblings = new List<GuiWidget>();
			var setterCalls = new List<string>();

			var harness = MenuHarness.Show(menu =>
			{
				menu.CreateBoolMenuItem("Radio A", () => true, value => setterCalls.Add("A"), useRadioStyle: true, siblingRadioButtonList: siblings);
				menu.CreateBoolMenuItem("Radio B", () => false, value => setterCalls.Add("B"), useRadioStyle: true, siblingRadioButtonList: siblings);
			});

			var radioA = (PopupMenu.RadioMenuItem)harness.Find("Radio A Menu Item");
			var radioB = (PopupMenu.RadioMenuItem)harness.Find("Radio B Menu Item");

			// OnLoad is what normally registers an item with its sibling list, and OnLoad only runs on the
			// first draw - so register here the way a drawn menu would.
			siblings.Add(radioA);
			siblings.Add(radioB);

			await Assert.That(radioA.Checked).IsTrue();
			await Assert.That(radioB.Checked).IsFalse();

			harness.Click(harness.CenterOf("Radio B Menu Item"));
			harness.PumpIdle();

			await Assert.That(radioB.Checked).IsTrue();
			await Assert.That(radioA.Checked).IsFalse()
				.Because("checking one radio item unchecks its SiblingRadioButtonList");
			await Assert.That(setterCalls).IsEquivalentTo(new[] { "B" });
			await Assert.That(harness.Menu.HasBeenClosed).IsFalse();

			// Re-clicking the already checked row does nothing at all - no setter, no state change
			harness.Click(harness.CenterOf("Radio B Menu Item"));
			harness.PumpIdle();

			await Assert.That(radioB.Checked).IsTrue();
			await Assert.That(radioA.Checked).IsFalse();
			await Assert.That(setterCalls).IsEquivalentTo(new[] { "B" });
			await Assert.That(harness.Menu.HasBeenClosed).IsFalse();
		}

		/// <summary>
		/// The click-open analog of agg-gui <c>hover_opens_submenu_and_hit_tests_nested_rows</c>: agg-sharp
		/// opens sub menus on click rather than on hover, and the nested rows must be hit testable in the
		/// window once the sub menu is up.
		/// </summary>
		/// <remarks>
		/// DIVERGES from agg-gui: there is no hover-open in agg-sharp at all, so the hover half of that test
		/// is not ported. Adding hover-open is a feature, not a test gap.
		/// </remarks>
		[Test]
		public async Task ClickingASubMenuParentOpensItAndItsRowsAreHitTestable()
		{
			int leafCount = 0;

			var harness = MenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open");
				menu.CreateSubMenu(
					"More",
					menu.Theme,
					subMenu =>
					{
						subMenu.CreateMenuItem("Leaf").Click += (s, e) => leafCount++;
						subMenu.CreateMenuItem("Checked");
					});
			});

			var subMenuButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			harness.Click(harness.CenterOf("More Menu Item"));

			// CreateSubMenu populates and shows the sub menu from RunOnIdle
			harness.PumpIdle();

			var subMenu = subMenuButton.SubMenu;
			await Assert.That(subMenu).IsNotNull();
			await Assert.That(harness.Menu.HasBeenClosed).IsFalse()
				.Because("the parent menu is held open while its sub menu has focus");

			var leaf = harness.Find("Leaf Menu Item");
			await Assert.That(leaf).IsNotNull();

			harness.Click(harness.CenterOf("Leaf Menu Item"));
			harness.PumpIdle();

			await Assert.That(leafCount).IsEqualTo(1);
			await Assert.That(subMenu.HasBeenClosed).IsTrue();
			await Assert.That(harness.Menu.HasBeenClosed).IsTrue()
				.Because("choosing a leaf tears down the whole menu stack, not just the sub menu");
		}

		/// <summary>
		/// Closing a sub menu without choosing anything returns to the parent-only state.
		/// </summary>
		[Test]
		public async Task ClosingASubMenuWithoutASelectionLeavesNoSubMenu()
		{
			var harness = MenuHarness.Show(menu =>
			{
				menu.CreateSubMenu(
					"More",
					menu.Theme,
					subMenu => subMenu.CreateMenuItem("Leaf"));
			});

			var subMenuButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			harness.Click(harness.CenterOf("More Menu Item"));
			harness.PumpIdle();

			var subMenu = subMenuButton.SubMenu;
			await Assert.That(subMenu).IsNotNull();

			// Dismiss the sub menu the way a click elsewhere would - by taking focus off it
			subMenu.Unfocus();
			harness.PumpIdle();

			await Assert.That(subMenu.HasBeenClosed).IsTrue();
			await Assert.That(subMenuButton.SubMenu).IsNull()
				.Because("the parent forgets a sub menu that closed, so the next click can open a fresh one");
		}

		/// <summary>
		/// Ports the vertical half of agg-gui <c>popup_clamps_to_viewport</c> for the case
		/// <c>PopupSubMenuScrollTests</c> does not cover: a short menu opened near the bottom edge of the
		/// window, which has to flip (or clamp) upward instead of hanging below the window.
		/// </summary>
		/// <remarks>
		/// The horizontal half lives in <see cref="PopupHorizontalClampTests"/>.
		/// </remarks>
		[Test]
		public async Task MenuOpenedAtTheBottomEdgeStaysInsideTheWindow()
		{
			var harness = MenuHarness.Show(
				menu =>
				{
					menu.CreateMenuItem("One");
					menu.CreateMenuItem("Two");
					menu.CreateMenuItem("Three");
				},
				anchorPosition: new Vector2(10, 2));

			var bounds = harness.Menu.BoundsRelativeToParent;

			await Assert.That(bounds.Bottom).IsGreaterThanOrEqualTo(0);
			await Assert.That(bounds.Top).IsLessThanOrEqualTo(harness.Window.Height);

			// Every row has to be reachable, not merely the panel rectangle
			foreach (var item in harness.Menu.Descendants<PopupMenu.MenuItem>())
			{
				var itemBounds = item.TransformToScreenSpace(item.LocalBounds);
				await Assert.That(itemBounds.Bottom).IsGreaterThanOrEqualTo(0);
				await Assert.That(itemBounds.Top).IsLessThanOrEqualTo(harness.Window.Height);
			}
		}

		/// <summary>
		/// Ports agg-gui <c>right_click_ignored_when_menu_closed</c>: with no menu up, a right press is not
		/// swallowed by the menu machinery and reaches the widget it landed on.
		/// </summary>
		[Test]
		public async Task RightClickIsIgnoredByTheMenuMachineryWhenNoMenuIsOpen()
		{
			var systemWindow = new SystemWindow(600, 400);

			var target = new ClickCountingWidget
			{
				Name = "Target",
				LocalBounds = new RectangleDouble(0, 0, 600, 400),
			};
			systemWindow.AddChild(target);

			systemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Right, 1, 50, 50, 0));
			systemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Right, 1, 50, 50, 0));
			UiThread.InvokePendingActions();

			await Assert.That(target.RightMouseDowns).IsEqualTo(1);
			await Assert.That(systemWindow.Descendants<PopupMenu>().Any()).IsFalse();
		}

		/// <summary>
		/// Ports agg-gui <c>right_click_dismisses_menu</c>: a non-left press over an open menu takes it down
		/// and is consumed, so the row it landed on never runs.
		/// </summary>
		[Test]
		public async Task RightPressDismissesTheMenuWithoutActivatingTheRowUnderIt()
		{
			int openCount = 0;

			var harness = MenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open").Click += (s, e) => openCount++;
				menu.CreateMenuItem("Close");
			});

			var openRow = harness.Find("Open Menu Item");

			harness.Click(harness.CenterOf("Open Menu Item"), MouseButtons.Right);
			harness.PumpIdle();

			await Assert.That(harness.Menu.HasBeenClosed).IsTrue();
			await Assert.That(openCount).IsEqualTo(0)
				.Because("the press only dismisses - it must not also choose the row it landed on");

			// The row never even saw the press, which is what agg-gui's EventResult::Consumed buys there
			await Assert.That(openRow.Focused).IsFalse();
		}

		/// <summary>
		/// Ports agg-gui <c>middle_click_dismisses_menu</c>: the rule is "anything but left", not "right".
		/// </summary>
		[Test]
		public async Task MiddlePressDismissesTheMenu()
		{
			var harness = MenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open");
				menu.CreateMenuItem("Close");
			});

			harness.Press(harness.CenterOf("Open Menu Item"), MouseButtons.Middle);
			harness.PumpIdle();

			await Assert.That(harness.Menu.HasBeenClosed).IsTrue();
		}

		/// <summary>
		/// The same dismissal over the <c>PopupButton</c> shape, where a <see cref="PopupWidget"/> hosts the
		/// menu. That path has an extra hazard the <c>ShowMenu</c> path does not: the widgets between the
		/// window and the menu re-focus themselves as the press unwinds back up through them, so an unfocus
		/// done during dispatch is simply undone before the idle close ever gets to look at it.
		/// </summary>
		[Test]
		public async Task RightPressDismissesAPopupWidgetHostedMenu()
		{
			var window = new SystemWindow(600, 400);

			var anchor = new GuiWidget(50, 20)
			{
				Name = "Anchor",
			};
			window.AddChild(anchor);
			anchor.Position = new Vector2(10, 200);

			var menu = new PopupMenu(new ThemeConfig());
			menu.CreateMenuItem("Open");
			menu.CreateMenuItem("Close");

			var layoutEngine = new PopupLayoutEngine(menu, anchor, Direction.Down, maxHeight: 0, alignToRightEdge: false);
			var popup = new PopupWidget(menu, layoutEngine, makeScrollable: true);
			popup.Focus();

			await Assert.That(popup.HasBeenClosed).IsFalse();

			var openRow = window.FindDescendant("Open Menu Item");
			var rowCenter = openRow.TransformToScreenSpace(openRow.LocalBounds).Center;

			window.OnMouseDown(new MouseEventArgs(MouseButtons.Right, 1, rowCenter.X, rowCenter.Y, 0));

			for (int i = 0; i < 4; i++)
			{
				UiThread.InvokePendingActions();
			}

			await Assert.That(popup.HasBeenClosed).IsTrue();
		}

		/// <summary>
		/// agg-gui's dismissal drops the whole <c>open_path</c>, not the deepest level of it, so a non-left
		/// press inside a sub menu takes the parent down with it.
		/// </summary>
		[Test]
		public async Task RightPressOverASubMenuRowClosesTheWholeChain()
		{
			int leafCount = 0;

			var harness = MenuHarness.Show(menu =>
			{
				menu.CreateSubMenu(
					"More",
					menu.Theme,
					subMenu => subMenu.CreateMenuItem("Leaf").Click += (s, e) => leafCount++);
			});

			var subMenuButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			harness.Click(harness.CenterOf("More Menu Item"));
			harness.PumpIdle();

			var subMenu = subMenuButton.SubMenu;
			await Assert.That(subMenu).IsNotNull();

			harness.Click(harness.CenterOf("Leaf Menu Item"), MouseButtons.Right);
			harness.PumpIdle();

			await Assert.That(leafCount).IsEqualTo(0);
			await Assert.That(subMenu.HasBeenClosed).IsTrue();
			await Assert.That(harness.Menu.HasBeenClosed).IsTrue()
				.Because("a dismissing press drops the whole open chain, not just its deepest level");
		}

		/// <summary>
		/// The precise half of agg-gui <c>action_click_consumes_and_suppresses_followup_mouse_up</c>: agg-gui
		/// needs a <c>take_suppress_mouse_up</c> latch so the release that follows an action click does not
		/// reach whatever the menu was covering. agg-sharp needs no latch at all - mouse routing hands a press
		/// to the topmost child that contains the point and to nobody else - and this pins that, with a small
		/// button directly under the row being clicked rather than a full window backdrop.
		/// </summary>
		[Test]
		public async Task ClickingARowNeverReachesAButtonDirectlyUnderIt()
		{
			int openCount = 0;

			var harness = MenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open").Click += (s, e) => openCount++;
				menu.CreateMenuItem("Close");
			});

			// A button exactly where the row is, sitting under the menu in the window's child order
			var buried = harness.AddWidgetUnderMenu(harness.BoundsOf("Open Menu Item"));

			harness.Click(harness.CenterOf("Open Menu Item"));
			harness.PumpIdle();

			await Assert.That(openCount).IsEqualTo(1);
			await Assert.That(harness.Menu.HasBeenClosed).IsTrue();

			await Assert.That(buried.MouseDowns).IsEqualTo(0);
			await Assert.That(buried.MouseUps).IsEqualTo(0);
			await Assert.That(buried.LeftClicks).IsEqualTo(0);
		}

		/// <summary>
		/// DIVERGES from agg-gui: agg-gui consumes the press that dismisses a menu from outside it, so the
		/// widget under that press sees nothing. agg-sharp deliberately lets it through.
		/// </summary>
		/// <remarks>
		/// A popup opened from a button (MatterCAD's PopupButton) is dismissed by pressing that button again,
		/// and the button only toggles because the press reaches it; swallowing the press would leave the
		/// second click doing nothing at all. The same pass-through is what lets a right click somewhere else
		/// raise that widget's own context menu in one gesture rather than two. agg-sharp has no single menu
		/// event stream to consume from - the press simply routes to the topmost widget under it, and an
		/// outside press is by definition not over the menu - so the divergence is structural, not a choice
		/// that could be reversed cheaply.
		/// </remarks>
		[Test]
		public async Task AnOutsidePressBothDismissesTheMenuAndReachesTheWidgetBeneath()
		{
			var harness = MenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open");
				menu.CreateMenuItem("Close");
			});

			var outside = harness.PointOutsideMenu();
			await Assert.That(harness.MenuBounds.Contains(outside)).IsFalse();

			harness.Click(outside);
			harness.PumpIdle();

			await Assert.That(harness.Menu.HasBeenClosed).IsTrue();
			await Assert.That(harness.Beneath.MouseDowns).IsEqualTo(1);
			await Assert.That(harness.Beneath.LeftClicks).IsEqualTo(1);
		}

		/// <summary>
		/// <c>SystemWindowExtension.ShowPopup</c>'s CloseMenu restores focus to the widget the menu was
		/// anchored to, so the keyboard is not left stranded on a widget that no longer exists.
		/// </summary>
		[Test]
		public async Task ClosingAMenuByChoosingAnItemRestoresFocusToTheAnchor()
		{
			var harness = MenuHarness.Show(menu => menu.CreateMenuItem("Open"));

			await Assert.That(harness.Anchor.Focused).IsFalse()
				.Because("the menu took focus when it was shown");

			harness.Click(harness.CenterOf("Open Menu Item"));
			harness.PumpIdle();

			await Assert.That(harness.Menu.HasBeenClosed).IsTrue();
			await Assert.That(harness.Anchor.Focused).IsTrue();
		}

		/// <summary>
		/// <see cref="PopupMenu.GetYAnchor"/> is pure, so it can be pinned directly. Rather than repeating
		/// its formulas the test places a popup at the offset it returns and checks that the two named
		/// edges land on each other - the round-trip style agg-gui's <c>align.rs</c> tests use.
		/// </summary>
		[Test]
		[Arguments(MateEdge.Top, MateEdge.Bottom)]
		[Arguments(MateEdge.Top, MateEdge.Top)]
		[Arguments(MateEdge.Bottom, MateEdge.Top)]
		[Arguments(MateEdge.Bottom, MateEdge.Bottom)]
		public async Task GetYAnchorPutsThePopupEdgeOnTheAnchorEdge(MateEdge anchorEdge, MateEdge popupEdge)
		{
			// The anchor's local bounds, with its bottom left at the origin - that is the point every
			// offset GetYAnchor returns is added to in BestPopupPosition.
			var anchorBounds = new RectangleDouble(0, 0, 60, 20);
			var popupWidget = new GuiWidget(150, 90);

			var offset = PopupMenu.GetYAnchor(
				new MateOptions(MateEdge.Left, anchorEdge),
				new MateOptions(MateEdge.Left, popupEdge),
				popupWidget,
				anchorBounds);

			double popupBottom = offset.Y;
			double popupTop = offset.Y + popupWidget.Height;

			double anchorY = anchorEdge == MateEdge.Top ? anchorBounds.Top : anchorBounds.Bottom;
			double popupY = popupEdge == MateEdge.Top ? popupTop : popupBottom;

			await Assert.That(popupY).IsEqualTo(anchorY).Within(0.001);
		}

		/// <summary>
		/// The horizontal twin of <see cref="GetYAnchorPutsThePopupEdgeOnTheAnchorEdge"/>, over
		/// <see cref="PopupMenu.GetXAnchor"/>.
		/// </summary>
		[Test]
		[Arguments(MateEdge.Left, MateEdge.Left)]
		[Arguments(MateEdge.Left, MateEdge.Right)]
		[Arguments(MateEdge.Right, MateEdge.Left)]
		[Arguments(MateEdge.Right, MateEdge.Right)]
		public async Task GetXAnchorPutsThePopupEdgeOnTheAnchorEdge(MateEdge anchorEdge, MateEdge popupEdge)
		{
			var anchorBounds = new RectangleDouble(0, 0, 60, 20);
			var popupWidget = new GuiWidget(150, 90);

			var offset = PopupMenu.GetXAnchor(
				new MateOptions(anchorEdge, MateEdge.Bottom),
				new MateOptions(popupEdge, MateEdge.Bottom),
				popupWidget,
				anchorBounds);

			double popupLeft = offset.X;
			double popupRight = offset.X + popupWidget.Width;

			double anchorX = anchorEdge == MateEdge.Right ? anchorBounds.Right : anchorBounds.Left;
			double popupX = popupEdge == MateEdge.Right ? popupRight : popupLeft;

			await Assert.That(popupX).IsEqualTo(anchorX).Within(0.001);
		}

		/// <summary>
		/// Opening a menu takes down a tooltip the mouse armed on its way to the menu, before it is ever
		/// drawn. Nothing is on screen yet at that moment, so the only evidence a caller can see is the menu
		/// being covered by a tooltip a fraction of a second later.
		/// </summary>
		/// <remarks>
		/// This is what <c>ShowMenu</c> buys by routing through <c>PopupMenu.ClearToolTipsAbove</c>, and why
		/// callers must not hand-roll their own <c>systemWindow.ShowPopup</c> call for a context menu
		/// (MatterCAD's bed menu did, and got no tooltip clearing).
		/// </remarks>
		[Test]
		public async Task ShowMenuClearsArmedTooltips()
		{
			var (window, hovered) = HoverAWidgetWithAToolTip();

			try
			{
				await Assert.That(ArmedToolTipWidget(window)).IsEqualTo(hovered)
					.Because("the mouse move has to have armed a tooltip for the clearing to mean anything");

				var menu = new PopupMenu(new ThemeConfig());
				menu.CreateMenuItem("Open");
				menu.ShowMenu(hovered, Vector2.Zero);

				await Assert.That(ArmedToolTipWidget(window)).IsNull();
			}
			finally
			{
				window.ToolTipManager.Dispose();
			}
		}

		/// <summary>
		/// The same for a tooltip that is already on screen when the menu opens.
		/// </summary>
		[Test]
		public async Task ShowMenuTakesDownAToolTipThatIsAlreadyShowing()
		{
			var (window, hovered) = HoverAWidgetWithAToolTip();

			try
			{
				// Put the tooltip up now rather than waiting out ToolTipManager.InitialDelay - the delay is
				// real time, and no test may sleep. This is the manager's own show path, just not on its own
				// schedule.
				var doShowToolTip = typeof(ToolTipManager).GetMethod("DoShowToolTip", BindingFlags.Instance | BindingFlags.NonPublic);
				await Assert.That((bool)doShowToolTip.Invoke(window.ToolTipManager, null)).IsTrue();

				var toolTip = window.FindDescendant("ToolTipWidget");
				await Assert.That(toolTip).IsNotNull();
				await Assert.That(window.ToolTipManager.CurrentText).IsEqualTo(hovered.ToolTipText);

				var menu = new PopupMenu(new ThemeConfig());
				menu.CreateMenuItem("Open");
				menu.ShowMenu(hovered, Vector2.Zero);

				await Assert.That(toolTip.HasBeenClosed).IsTrue();
				await Assert.That(window.ToolTipManager.CurrentText).IsEqualTo("");
			}
			finally
			{
				window.ToolTipManager.Dispose();
			}
		}

		/// <summary>
		/// A window holding one widget with tooltip text, with the mouse moved over it through the real
		/// <see cref="SystemWindow.OnMouseMove"/> path - which is what arms the window's ToolTipManager.
		/// </summary>
		private static (SystemWindow window, GuiWidget hovered) HoverAWidgetWithAToolTip()
		{
			var window = new SystemWindow(600, 400);

			var hovered = new GuiWidget(120, 30)
			{
				Name = "Hovered",
				ToolTipText = "What this button does",
			};
			window.AddChild(hovered);
			hovered.Position = new Vector2(10, 200);

			var hoverPoint = hovered.TransformToScreenSpace(hovered.LocalBounds).Center;
			window.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, hoverPoint.X, hoverPoint.Y, 0));

			return (window, hovered);
		}

		/// <summary>
		/// The widget whose tooltip is waiting out its delay. Private state, but the whole point of clearing
		/// an armed tooltip is that it has produced nothing observable yet.
		/// </summary>
		private static GuiWidget ArmedToolTipWidget(SystemWindow window)
		{
			var field = typeof(ToolTipManager).GetField("widgetThatWantsToShowToolTip", BindingFlags.Instance | BindingFlags.NonPublic);
			return (GuiWidget)field.GetValue(window.ToolTipManager);
		}

		/// <summary>
		/// Counts the mouse traffic a widget actually receives, so a test can prove a press was consumed by
		/// something drawn over it rather than merely assuming so.
		/// </summary>
		private class ClickCountingWidget : GuiWidget
		{
			public int LeftClicks { get; private set; }

			public int MouseDowns { get; private set; }

			public int MouseUps { get; private set; }

			public int RightMouseDowns { get; private set; }

			public override void OnMouseDown(MouseEventArgs mouseEvent)
			{
				MouseDowns++;

				if (mouseEvent.Button == MouseButtons.Right)
				{
					RightMouseDowns++;
				}

				base.OnMouseDown(mouseEvent);
			}

			public override void OnMouseUp(MouseEventArgs mouseEvent)
			{
				MouseUps++;
				base.OnMouseUp(mouseEvent);
			}

			protected override void OnClick(MouseEventArgs mouseEvent)
			{
				if (mouseEvent.Button == MouseButtons.Left)
				{
					LeftClicks++;
				}

				base.OnClick(mouseEvent);
			}
		}

		/// <summary>
		/// A window with a click counting backdrop, an anchor widget, and a <see cref="PopupMenu"/> opened
		/// over them through the same <c>ShowMenu</c> path every right click menu takes.
		/// </summary>
		private class MenuHarness
		{
			private MenuHarness(SystemWindow window, ClickCountingWidget beneath, GuiWidget anchor, PopupMenu menu)
			{
				this.Window = window;
				this.Beneath = beneath;
				this.Anchor = anchor;
				this.Menu = menu;
			}

			public SystemWindow Window { get; }

			/// <summary>The full window backdrop the menu is drawn over.</summary>
			public ClickCountingWidget Beneath { get; }

			public GuiWidget Anchor { get; }

			public PopupMenu Menu { get; }

			public RectangleDouble MenuBounds => Menu.TransformToScreenSpace(Menu.LocalBounds);

			public static MenuHarness Show(Action<PopupMenu> populate, Vector2? anchorPosition = null)
			{
				var window = new SystemWindow(600, 400);
				var theme = new ThemeConfig();

				var beneath = new ClickCountingWidget
				{
					Name = "Beneath",
					LocalBounds = new RectangleDouble(0, 0, 600, 400),
				};
				window.AddChild(beneath);

				var anchor = new GuiWidget(50, 20)
				{
					Name = "Anchor",
				};
				window.AddChild(anchor);
				anchor.Position = anchorPosition ?? new Vector2(10, 370);

				var menu = new PopupMenu(theme);
				populate(menu);

				menu.ShowMenu(anchor, Vector2.Zero);

				return new MenuHarness(window, beneath, anchor, menu);
			}

			public GuiWidget Find(string name)
			{
				return Window.FindDescendant(name);
			}

			/// <summary>
			/// The center of a laid out row, in window space. Deriving the click point from the widget is
			/// what keeps these tests from depending on row heights or font metrics.
			/// </summary>
			public Vector2 CenterOf(string name)
			{
				return BoundsOf(name).Center;
			}

			/// <summary>
			/// The laid out rectangle of a row, in window space.
			/// </summary>
			public RectangleDouble BoundsOf(string name)
			{
				var widget = Find(name);
				return widget.TransformToScreenSpace(widget.LocalBounds);
			}

			/// <summary>
			/// A point in the window that no menu panel covers, found by walking right of the widest panel.
			/// </summary>
			public Vector2 PointOutsideMenu()
			{
				var panels = Window.Descendants<PopupMenu>().Select(m => m.TransformToScreenSpace(m.LocalBounds)).ToList();

				double right = panels.Max(p => p.Right);
				double bottom = panels.Min(p => p.Bottom);

				return new Vector2(right + 20, bottom - 20);
			}

			/// <summary>
			/// Drops a click counting widget at the given window space rectangle, underneath the menu in the
			/// window's child order, so a test can prove a press never got past the menu to it.
			/// </summary>
			public ClickCountingWidget AddWidgetUnderMenu(RectangleDouble screenBounds)
			{
				var buried = new ClickCountingWidget
				{
					Name = "Buried",
					LocalBounds = new RectangleDouble(0, 0, screenBounds.Width, screenBounds.Height),
				};

				// The menu was added last (it is the topmost child), so insert just below it
				Window.AddChild(buried, Window.Children.Count - 1);
				buried.Position = new Vector2(screenBounds.Left, screenBounds.Bottom);

				return buried;
			}

			public void Click(Vector2 point, MouseButtons button = MouseButtons.Left)
			{
				Window.OnMouseDown(new MouseEventArgs(button, 1, point.X, point.Y, 0));
				Window.OnMouseUp(new MouseEventArgs(button, 1, point.X, point.Y, 0));
			}

			/// <summary>
			/// A press with no matching release - what a dismissal has to act on, since the release lands
			/// after the menu is already gone.
			/// </summary>
			public void Press(Vector2 point, MouseButtons button = MouseButtons.Left)
			{
				Window.OnMouseDown(new MouseEventArgs(button, 1, point.X, point.Y, 0));
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
