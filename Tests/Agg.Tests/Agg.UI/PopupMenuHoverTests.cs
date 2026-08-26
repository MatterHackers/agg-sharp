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
	/// Hover behavior for <see cref="PopupMenu"/>, ported from the Rust <c>agg-gui</c> menu suite
	/// (<c>agg-gui/src/widgets/menu/state.rs</c> <c>update_hover</c>). Hover opens sub menus with no timer,
	/// closes the sub menu a sibling row left open, and moves the highlight - which in agg-sharp is keyboard
	/// focus, so hover and keyboard share one highlight the way Windows menus do.
	/// </summary>
	/// <remarks>
	/// Headless, in the style of <c>PopupMenuConformanceTests</c>: a real <see cref="SystemWindow"/> is driven
	/// as a plain widget and mouse moves are pushed into it, so they take the same routing (and the same
	/// enter/leave bookkeeping) they take in a running app. Every coordinate is derived from laid out bounds -
	/// hard coded row positions would only pin the current font metrics.
	/// <para>
	/// Sub menus populate and close from <see cref="UiThread.RunOnIdle"/>, and the pending action queue is
	/// process wide, so this class shares the constraint key every other queue draining class uses.
	/// </para>
	/// </remarks>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class PopupMenuHoverTests
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
		/// Ports agg-gui <c>hover_opens_submenu_and_hit_tests_nested_rows</c>: the hover alone opens the sub
		/// menu - no click, and no dwell timer - and the rows it contains are immediately clickable.
		/// </summary>
		[Test]
		public async Task HoverOpensASubMenuAndItsRowsAreHitTestable()
		{
			int leafCount = 0;

			var harness = HoverMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open");
				menu.CreateSubMenu(
					"More",
					menu.Theme,
					subMenu => subMenu.CreateMenuItem("Leaf").Click += (s, e) => leafCount++);
			});

			var subMenuButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			harness.MoveTo(harness.CenterOf("More Menu Item"));
			harness.PumpIdle();

			await Assert.That(subMenuButton.SubMenu).IsNotNull()
				.Because("hovering a sub menu row opens it, with no click and no timer");

			await Assert.That(harness.Find("Leaf Menu Item")).IsNotNull();

			harness.Click(harness.CenterOf("Leaf Menu Item"));
			harness.PumpIdle();

			await Assert.That(leafCount).IsEqualTo(1);
		}

		/// <summary>
		/// agg-gui's <c>update_hover</c> truncates <c>open_path</c> when the hovered row is not on it, so
		/// crossing onto a plain sibling takes the open sub menu down and leaves the parent up.
		/// </summary>
		[Test]
		public async Task HoveringAPlainSiblingClosesTheSubMenuAndLeavesTheParentOpen()
		{
			var harness = HoverMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open");
				menu.CreateSubMenu("More", menu.Theme, subMenu => subMenu.CreateMenuItem("Leaf"));
			});

			var subMenuButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			harness.MoveTo(harness.CenterOf("More Menu Item"));
			harness.PumpIdle();

			var subMenu = subMenuButton.SubMenu;
			await Assert.That(subMenu).IsNotNull();

			harness.MoveTo(harness.CenterOf("Open Menu Item"));
			harness.PumpIdle();

			await Assert.That(subMenu.HasBeenClosed).IsTrue();
			await Assert.That(subMenuButton.SubMenu).IsNull();
			await Assert.That(harness.Menu.HasBeenClosed).IsFalse()
				.Because("only the sub menu went away, the menu the mouse is still in did not");

			// The closing sub menu restores focus to the row it was anchored to on its way out, so this also
			// pins that the highlight ends up where the mouse is rather than on the row it left
			await Assert.That(harness.HighlightedName).IsEqualTo("Open Menu Item");
		}

		/// <summary>
		/// A sub menu opens beside the row it belongs to and drops down from there, so any row of it below the
		/// first is reached by moving down and to the right - a path that crosses the rows underneath the
		/// opening row on the way. Those crossings are transit, not a choice of row, and taking the sub menu
		/// down on them makes every row of it but the top one unreachable with the mouse.
		/// </summary>
		[Test]
		public async Task MovingDiagonallyIntoAnOpenSubMenuDoesNotCloseItOnTheRowsCrossedOnTheWay()
		{
			var harness = HoverMenuHarness.Show(menu =>
			{
				menu.CreateSubMenu(
					"More",
					menu.Theme,
					subMenu =>
					{
						for (int i = 1; i <= 5; i++)
						{
							subMenu.CreateMenuItem($"Leaf {i}");
						}
					});

				menu.CreateMenuItem("Second");
				menu.CreateMenuItem("Third");
				menu.CreateMenuItem("Fourth");
			});

			var subMenuButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			harness.MoveTo(harness.CenterOf("More Menu Item"));
			harness.PumpIdle();

			var subMenu = subMenuButton.SubMenu;
			await Assert.That(subMenu).IsNotNull();

			// Straight line from the opening row to a row well down the sub menu, the way a hand moves and the
			// way AutomationRunner interpolates a mouse move. It passes over Second and Third en route.
			harness.MoveAlong(harness.CenterOf("More Menu Item"), harness.CenterOf("Leaf 4 Menu Item"));

			await Assert.That(subMenuButton.SubMenu).IsNotNull()
				.Because("the pointer was inside the wedge between the opening row and the sub menu the whole way");
			await Assert.That(subMenu.HasBeenClosed).IsFalse();
			await Assert.That(harness.Find("Leaf 4 Menu Item")).IsNotNull();

			// The rows crossed on the way are transit, so the highlight never chases them - it goes straight
			// from the opening row to the sub menu row the pointer set out for
			await Assert.That(harness.HighlightedName).IsEqualTo("Leaf 4 Menu Item");
		}

		/// <summary>
		/// Crossing from one sub menu parent to another swaps which sub menu is up, rather than stacking them.
		/// </summary>
		[Test]
		public async Task HoveringADifferentSubMenuParentSwapsTheSubMenus()
		{
			var harness = HoverMenuHarness.Show(menu =>
			{
				menu.CreateSubMenu("More", menu.Theme, subMenu => subMenu.CreateMenuItem("Leaf A"));
				menu.CreateSubMenu("Other", menu.Theme, subMenu => subMenu.CreateMenuItem("Leaf B"));
			});

			var moreButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().First();
			var otherButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().Last();

			harness.MoveTo(harness.CenterOf("More Menu Item"));
			harness.PumpIdle();

			var firstSubMenu = moreButton.SubMenu;
			await Assert.That(firstSubMenu).IsNotNull();

			harness.MoveTo(harness.CenterOf("Other Menu Item"));
			harness.PumpIdle();

			await Assert.That(firstSubMenu.HasBeenClosed).IsTrue();
			await Assert.That(moreButton.SubMenu).IsNull();
			await Assert.That(otherButton.SubMenu).IsNotNull();
			await Assert.That(harness.Menu.HasBeenClosed).IsFalse();
		}

		/// <summary>
		/// Coming back onto the row whose sub menu is already up (out of that sub menu, typically) must leave
		/// it alone. Re-opening it would flicker, and closing it would make the sub menu unreachable by any
		/// path that crosses its parent row.
		/// </summary>
		[Test]
		public async Task ReHoveringTheOpenParentRowLeavesItsSubMenuUp()
		{
			var harness = HoverMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open");
				menu.CreateSubMenu("More", menu.Theme, subMenu => subMenu.CreateMenuItem("Leaf"));
			});

			var subMenuButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			harness.MoveTo(harness.CenterOf("More Menu Item"));
			harness.PumpIdle();

			var subMenu = subMenuButton.SubMenu;
			await Assert.That(subMenu).IsNotNull();

			harness.MoveTo(harness.CenterOf("Leaf Menu Item"));
			harness.PumpIdle();

			harness.MoveTo(harness.CenterOf("More Menu Item"));
			harness.PumpIdle();

			await Assert.That(subMenu.HasBeenClosed).IsFalse();
			await Assert.That(subMenuButton.SubMenu).IsEqualTo(subMenu)
				.Because("the sub menu that was up is the same one, not a second one opened over it");
		}

		/// <summary>
		/// A sub menu claims its slot the moment it is asked for, but builds and shows itself from the idle
		/// queue. A dismissal that arrives in that window queues its own close *behind* the show, so the show
		/// has to notice it is stale - otherwise it puts a cancelled sub menu on screen and focuses it, and
		/// <see cref="PopupMenu.SubMenuItemButton.KeepMenuOpen"/> then holds the dismissed parent open on the
		/// strength of it.
		/// </summary>
		[Test]
		public async Task EscapeBeforeAQueuedSubMenuIsShownCancelsIt()
		{
			var harness = HoverMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open");
				menu.CreateSubMenu("More", menu.Theme, subMenu => subMenu.CreateMenuItem("Leaf"));
			});

			var subMenuButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			// Deliberately not pumped: the sub menu is spoken for, but has not been built or shown
			harness.MoveTo(harness.CenterOf("More Menu Item"));

			await Assert.That(subMenuButton.SubMenu).IsNotNull()
				.Because("the row claims its sub menu synchronously, ahead of the idle body that shows it");

			harness.Window.OnKeyDown(new KeyEventArgs(Keys.Escape));

			harness.PumpIdle();

			await Assert.That(harness.Menu.HasBeenClosed).IsTrue()
				.Because("Escape dismisses the menu even when a sub menu was already queued to open");
			await Assert.That(subMenuButton.SubMenu).IsNull()
				.Because("the row has to forget the sub menu it never opened, or it can never open another");
			await Assert.That(harness.Window.Descendants<PopupMenu>().Any(menu => !menu.HasBeenClosed)).IsFalse()
				.Because("no part of a dismissed chain may be left on screen");
		}

		/// <summary>
		/// Hover-open must not cost the click path: pressing a sub menu row still opens it, which is how a
		/// touch (and every existing caller) reaches a sub menu.
		/// </summary>
		[Test]
		public async Task ClickingASubMenuParentStillOpensIt()
		{
			var harness = HoverMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open");
				menu.CreateSubMenu("More", menu.Theme, subMenu => subMenu.CreateMenuItem("Leaf"));
			});

			var subMenuButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			harness.Click(harness.CenterOf("More Menu Item"));
			harness.PumpIdle();

			await Assert.That(subMenuButton.SubMenu).IsNotNull();
			await Assert.That(harness.Menu.HasBeenClosed).IsFalse();
		}

		/// <summary>
		/// The path a user actually takes to reach a sub menu row runs through the sub menu body, and no
		/// sibling row is entered on the way, so nothing closes.
		/// </summary>
		[Test]
		public async Task MovingFromTheParentRowIntoTheSubMenuKeepsItOpen()
		{
			var harness = HoverMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("Open");
				menu.CreateSubMenu("More", menu.Theme, subMenu => subMenu.CreateMenuItem("Leaf"));
			});

			var subMenuButton = harness.Menu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			harness.MoveTo(harness.CenterOf("More Menu Item"));
			harness.PumpIdle();

			var subMenu = subMenuButton.SubMenu;
			await Assert.That(subMenu).IsNotNull();

			harness.MoveTo(harness.CenterOf("Leaf Menu Item"));
			harness.PumpIdle();

			await Assert.That(subMenu.HasBeenClosed).IsFalse();
			await Assert.That(harness.Menu.HasBeenClosed).IsFalse();
			await Assert.That(harness.HighlightedName).IsEqualTo("Leaf Menu Item")
				.Because("the highlight follows the mouse into the sub menu");
		}

		/// <summary>
		/// agg-sharp has one highlight, not two: hovering an enabled row moves keyboard focus onto it, which
		/// is what Windows menus (and agg-gui's single <c>hover_path</c>) do. That is what makes Enter activate
		/// the row the mouse is pointing at.
		/// </summary>
		[Test]
		public async Task HoveringAnEnabledRowMovesTheKeyboardHighlight()
		{
			var harness = HoverMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("One");
				menu.CreateMenuItem("Two");
				menu.CreateMenuItem("Three");
			});

			// Arrow to a row first, so the test proves hover *moves* an existing highlight rather than
			// merely setting one from nothing
			harness.Window.OnKeyDown(new KeyEventArgs(Keys.Down));
			await Assert.That(harness.HighlightedName).IsEqualTo("One Menu Item");

			harness.MoveTo(harness.CenterOf("Three Menu Item"));

			await Assert.That(harness.HighlightedName).IsEqualTo("Three Menu Item");

			harness.MoveTo(harness.CenterOf("Two Menu Item"));

			await Assert.That(harness.HighlightedName).IsEqualTo("Two Menu Item");
		}

		/// <summary>
		/// Ports agg-gui <c>disabled_rows_do_not_become_hovered</c>.
		/// </summary>
		/// <remarks>
		/// agg-sharp needs no code for this: <see cref="GuiWidget.OnMouseMove"/> only routes to children that
		/// are Visible, Enabled and CanSelect, so a disabled row is never told the mouse entered it and can
		/// never take the highlight. The test pins that routing rule, which the hover work now depends on.
		/// </remarks>
		[Test]
		public async Task HoveringADisabledRowDoesNotMoveTheHighlight()
		{
			var harness = HoverMenuHarness.Show(menu =>
			{
				menu.CreateMenuItem("One");
				menu.CreateMenuItem("Disabled").Enabled = false;
			});

			harness.MoveTo(harness.CenterOf("One Menu Item"));
			await Assert.That(harness.HighlightedName).IsEqualTo("One Menu Item");

			// Prove the move really landed on the disabled row before believing the row is what refused it
			var disabledBounds = harness.BoundsOf("Disabled Menu Item");
			await Assert.That(disabledBounds.Height).IsGreaterThan(0);

			harness.MoveTo(disabledBounds.Center);
			harness.PumpIdle();

			await Assert.That(harness.Find("Disabled Menu Item").Focused).IsFalse();
			await Assert.That(harness.HighlightedName).IsEqualTo("One Menu Item")
				.Because("a disabled row cannot take the highlight, so the last enabled one keeps it");
			await Assert.That(harness.Menu.HasBeenClosed).IsFalse();
		}

		/// <summary>
		/// A window with an anchor and a <see cref="PopupMenu"/> opened over it through the same
		/// <c>ShowMenu</c> path every right click menu takes, plus the mouse pushing helpers.
		/// </summary>
		private class HoverMenuHarness
		{
			private HoverMenuHarness(SystemWindow window, GuiWidget anchor, PopupMenu menu)
			{
				this.Window = window;
				this.Anchor = anchor;
				this.Menu = menu;
			}

			public SystemWindow Window { get; }

			public GuiWidget Anchor { get; }

			public PopupMenu Menu { get; }

			/// <summary>The row that currently owns keyboard focus - agg-sharp's menu highlight.</summary>
			public string HighlightedName
				=> Window.Descendants<PopupMenu.MenuItem>().FirstOrDefault(item => item.Focused)?.Name;

			public static HoverMenuHarness Show(Action<PopupMenu> populate, Vector2? anchorPosition = null)
			{
				var window = new SystemWindow(600, 400);
				var theme = new ThemeConfig();

				var anchor = new GuiWidget(50, 20)
				{
					Name = "Anchor",
				};
				window.AddChild(anchor);
				anchor.Position = anchorPosition ?? new Vector2(10, 370);

				var menu = new PopupMenu(theme);
				populate(menu);

				menu.ShowMenu(anchor, Vector2.Zero);

				return new HoverMenuHarness(window, anchor, menu);
			}

			public GuiWidget Find(string name)
			{
				return Window.FindDescendant(name);
			}

			/// <summary>
			/// The center of a laid out row, in window space.
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
			/// Slides the mouse to a point with no button held, the way the platform layer reports a move.
			/// </summary>
			public void MoveTo(Vector2 point)
			{
				Window.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, 0));
			}

			/// <summary>
			/// Slides the mouse from one point to another in a straight line, pumping the idle queue between
			/// steps, the way a real move arrives - as a run of positions along the path rather than a jump to
			/// the end. What a menu does with the rows in between is the whole point of the moves that use this.
			/// </summary>
			public void MoveAlong(Vector2 from, Vector2 to, int steps = 10)
			{
				for (int i = 1; i <= steps; i++)
				{
					MoveTo(from + (to - from) * (i / (double)steps));
					PumpIdle();
				}
			}

			public void Click(Vector2 point, MouseButtons button = MouseButtons.Left)
			{
				Window.OnMouseDown(new MouseEventArgs(button, 1, point.X, point.Y, 0));
				Window.OnMouseUp(new MouseEventArgs(button, 1, point.X, point.Y, 0));
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
