/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// MatterCAD's library File menu is a PopupMenu shown through PopupWidget/PopupLayoutEngine, and it
	/// ran off the bottom of the window with no scroll bar. These tests pin the rule that any popup menu
	/// without the vertical space for its items gets a scroll bar - on every open, not just the first,
	/// and when the space it has changes after it was shown.
	/// </summary>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class PopupMenuOverflowScrollTests
	{
		[Test]
		public async Task TallMenuFromButtonNearTopScrolls()
		{
			var (systemWindow, _, popup) = ShowMenuPopup(itemCount: 16);

			await AssertPopupIsScrolledInsideWindow(systemWindow, popup);
		}

		[Test]
		public async Task TallMenuScrollsOnEveryOpenNotJustTheFirst()
		{
			// PopupButton builds its PopupLayoutEngine once and reuses it for every open, so the second
			// open of the same menu goes through an engine that has already made its fit decision
			var (systemWindow, engine, firstPopup) = ShowMenuPopup(itemCount: 16);

			await AssertPopupIsScrolledInsideWindow(systemWindow, firstPopup);

			firstPopup.CloseMenu();

			var secondMenu = BuildMenu(itemCount: 16);
			var secondPopup = new PopupWidget(secondMenu, engine, makeScrollable: true);

			await AssertPopupIsScrolledInsideWindow(systemWindow, secondPopup);
		}

		[Test]
		public async Task MenuThatGrowsAfterBeingShownScrolls()
		{
			// A menu can be filled after the popup is up (anything populated from RunOnIdle), which lays
			// it out a second time. The first layout said it fit, and nothing re-asked.
			var (systemWindow, _, popup) = ShowMenuPopup(itemCount: 2);

			var menu = popup.Descendants<PopupMenu>().First();
			AddItems(menu, itemCount: 16);

			await AssertPopupIsScrolledInsideWindow(systemWindow, popup);
		}

		[Test]
		public async Task MenuReclampsWhenTheWindowShrinksUnderIt()
		{
			var (systemWindow, _, popup) = ShowMenuPopup(itemCount: 4);

			systemWindow.Height = 120;

			await AssertPopupIsScrolledInsideWindow(systemWindow, popup);
		}

		[Test]
		public async Task MenuShownThroughShowPopupScrolls()
		{
			// The other popup path: a menu positioned by mating it to an anchor rather than by a
			// PopupLayoutEngine. Every caller of ShowPopup gets the clamp, not just the handful that
			// remembered to ask for it themselves.
			var systemWindow = new SystemWindow(400, 300);
			var anchor = AddAnchor(systemWindow);

			var menu = BuildMenu(itemCount: 16);

			systemWindow.ShowPopup(
				menu.Theme,
				new MatePoint(anchor)
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Top)
				},
				new MatePoint(menu)
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Bottom)
				});

			var bounds = menu.BoundsRelativeToParent;

			await Assert.That(bounds.Height).IsLessThanOrEqualTo(systemWindow.Height);
			await Assert.That(bounds.Bottom).IsGreaterThanOrEqualTo(0);
			await Assert.That(bounds.Top).IsLessThanOrEqualTo(systemWindow.Height);

			await Assert.That(menu.Descendants<ScrollableWidget>().Any()).IsTrue();
		}

		[Test]
		public async Task ShortMenuGainsNoScrollBar()
		{
			var (systemWindow, _, popup) = ShowMenuPopup(itemCount: 2);

			await Assert.That(popup.Descendants<ScrollBar>().Any(bar => bar.Visible)).IsFalse();
			await Assert.That(popup.BoundsRelativeToParent.Bottom).IsGreaterThanOrEqualTo(0);
			await Assert.That(popup.BoundsRelativeToParent.Top).IsLessThanOrEqualTo(systemWindow.Height);
		}

		[Test]
		public async Task OptingOutOfScrollingKeepsTheMenuUnscrolled()
		{
			// MatterCAD has a handful of popups built with makeScrollable: false (color pickers, docking
			// tabs). They must keep their own sizing rather than quietly growing a scroll bar.
			var systemWindow = new SystemWindow(400, 300);
			var anchor = AddAnchor(systemWindow);

			var menu = BuildMenu(itemCount: 16);
			var engine = new PopupLayoutEngine(menu, anchor, Direction.Down, maxHeight: 0, alignToRightEdge: false);
			var popup = new PopupWidget(menu, engine, makeScrollable: false);

			await Assert.That(popup.Descendants<ScrollableWidget>().Any()).IsFalse();
			await Assert.That(popup.Height).IsEqualTo(menu.Height).Within(0.001);
		}

		private static async Task AssertPopupIsScrolledInsideWindow(SystemWindow systemWindow, PopupWidget popup)
		{
			var bounds = popup.BoundsRelativeToParent;

			await Assert.That(bounds.Height).IsLessThanOrEqualTo(systemWindow.Height);
			await Assert.That(bounds.Bottom).IsGreaterThanOrEqualTo(0);
			await Assert.That(bounds.Top).IsLessThanOrEqualTo(systemWindow.Height);

			// and the items that no longer fit are reachable
			var scrollingWindow = popup.Descendants<ScrollableWidget>().First();
			await Assert.That(scrollingWindow.VerticalScrollBar.Visible).IsTrue();
		}

		private static (SystemWindow systemWindow, PopupLayoutEngine engine, PopupWidget popup) ShowMenuPopup(int itemCount)
		{
			var systemWindow = new SystemWindow(400, 300);
			var anchor = AddAnchor(systemWindow);

			var menu = BuildMenu(itemCount);

			var engine = new PopupLayoutEngine(menu, anchor, Direction.Down, maxHeight: 0, alignToRightEdge: false);
			var popup = new PopupWidget(menu, engine, makeScrollable: true);

			return (systemWindow, engine, popup);
		}

		private static GuiWidget AddAnchor(SystemWindow systemWindow)
		{
			// Anchored near the top of the window, which is where a toolbar menu button lives - and, like a
			// toolbar, it follows the top edge when the window is resized
			var anchor = new GuiWidget(60, 20)
			{
				Name = "Anchor",
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Top,
				Margin = new BorderDouble(left: 10, top: 20),
			};

			systemWindow.AddChild(anchor);

			return anchor;
		}

		private static PopupMenu BuildMenu(int itemCount)
		{
			var menu = new PopupMenu(new ThemeConfig());

			AddItems(menu, itemCount);

			return menu;
		}

		private static void AddItems(PopupMenu menu, int itemCount)
		{
			for (int i = 0; i < itemCount; i++)
			{
				var item = menu.CreateMenuItem($"Item {i}");
				item.MinimumSize = new Vector2(150, 48);
			}
		}
	}
}
