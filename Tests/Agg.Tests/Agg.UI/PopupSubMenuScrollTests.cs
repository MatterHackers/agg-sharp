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
	/// A PopupMenu is positioned relative to the widget that opened it, which means a tall menu
	/// (MatterCAD's "Open Recent" has 20 items) can easily be taller than the window. These tests pin
	/// the behavior that such a menu - sub menu or top level - is clamped to the window and made
	/// scrollable rather than being drawn off the top of the screen where its items are unreachable.
	/// </summary>
	// CreateSubMenu populates from UiThread.RunOnIdle, and the pending action queue is process wide.
	// Several other test classes drain that same queue, so share their constraint key rather than
	// inventing one - a private key only serializes this class against itself, which would let this
	// test run another class's queued work (and report that class's exception as our failure).
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class PopupSubMenuScrollTests
	{
		[Test]
		public async Task TallSubMenuIsClampedToWindowAndScrollable()
		{
			var systemWindow = new SystemWindow(400, 300);
			var theme = new ThemeConfig();

			var popupMenu = new PopupMenu(theme);
			systemWindow.AddChild(popupMenu);

			GuiWidget firstItem = null;

			popupMenu.CreateSubMenu(
				"Open Recent",
				theme,
				(subMenu) =>
				{
					for (int i = 0; i < 20; i++)
					{
						var item = subMenu.CreateMenuItem($"Recent {i}");
						item.MinimumSize = new Vector2(150, 48);
						firstItem ??= item;
					}
				});

			var subMenuButton = popupMenu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			subMenuButton.InvokeClick();

			// CreateSubMenu populates and shows the sub menu from RunOnIdle, so pump the queue
			UiThread.InvokePendingActions();

			var subMenu = subMenuButton.SubMenu;
			await Assert.That(subMenu).IsNotNull();

			var bounds = subMenu.BoundsRelativeToParent;

			// The menu content is ~960 tall - it must be shrunk to fit the 300 tall window
			await Assert.That(bounds.Height).IsLessThanOrEqualTo(systemWindow.Height);

			// and it must be positioned inside the window, not hanging off the top or bottom
			await Assert.That(bounds.Top).IsLessThanOrEqualTo(systemWindow.Height);
			await Assert.That(bounds.Bottom).IsGreaterThanOrEqualTo(0);

			// The items that no longer fit are reached by scrolling
			await Assert.That(subMenu.Descendants<ScrollableWidget>().Any()).IsTrue();

			// The whole point of the clamp is that the menu starts at its beginning - a menu scrolled (or
			// positioned) to its end looks identical by size and position but hides the items users expect
			var firstItemOnScreen = firstItem.TransformToScreenSpace(firstItem.LocalBounds);
			await Assert.That(firstItemOnScreen.Bottom).IsLessThan(systemWindow.Height);
			await Assert.That(firstItemOnScreen.Top).IsGreaterThan(0);
		}

		[Test]
		public async Task ShortSubMenuIsUnchanged()
		{
			var systemWindow = new SystemWindow(400, 300);
			var theme = new ThemeConfig();

			var popupMenu = new PopupMenu(theme);
			systemWindow.AddChild(popupMenu);

			popupMenu.CreateSubMenu(
				"Modify",
				theme,
				(subMenu) =>
				{
					subMenu.CreateMenuItem("Scale");
					subMenu.CreateMenuItem("Rotate");
				});

			var subMenuButton = popupMenu.Children.OfType<PopupMenu.SubMenuItemButton>().First();

			subMenuButton.InvokeClick();

			UiThread.InvokePendingActions();

			var subMenu = subMenuButton.SubMenu;
			await Assert.That(subMenu).IsNotNull();

			// A menu that fits keeps its natural size and gains no scrolling machinery
			await Assert.That(subMenu.Height).IsLessThan(systemWindow.Height);
			await Assert.That(subMenu.Descendants<ScrollableWidget>().Any()).IsFalse();
			await Assert.That(subMenu.Children.OfType<PopupMenu.MenuItem>().Count()).IsEqualTo(2);
		}

		[Test]
		public async Task TallTopLevelMenuIsClampedToWindowAndScrollable()
		{
			var systemWindow = new SystemWindow(400, 300);
			var theme = new ThemeConfig();

			var anchor = new GuiWidget(50, 20)
			{
				Name = "Anchor",
			};
			systemWindow.AddChild(anchor);

			var popupMenu = new PopupMenu(theme);

			GuiWidget firstItem = null;
			for (int i = 0; i < 20; i++)
			{
				var item = popupMenu.CreateMenuItem($"Recent {i}");
				item.MinimumSize = new Vector2(150, 48);
				firstItem ??= item;
			}

			// This is the path every right click menu takes
			popupMenu.ShowMenu(anchor, new Vector2(10, 10));

			var bounds = popupMenu.BoundsRelativeToParent;

			// The menu content is ~960 tall - it must be shrunk to fit the 300 tall window
			await Assert.That(bounds.Height).IsLessThanOrEqualTo(systemWindow.Height);

			await Assert.That(bounds.Top).IsLessThanOrEqualTo(systemWindow.Height);
			await Assert.That(bounds.Bottom).IsGreaterThanOrEqualTo(0);

			await Assert.That(popupMenu.Descendants<ScrollableWidget>().Any()).IsTrue();

			// and it opens at its first item, not scrolled or shoved to the end
			var firstItemOnScreen = firstItem.TransformToScreenSpace(firstItem.LocalBounds);
			await Assert.That(firstItemOnScreen.Bottom).IsLessThan(systemWindow.Height);
			await Assert.That(firstItemOnScreen.Top).IsGreaterThan(0);
		}

		[Test]
		public async Task PopupTallerThanWindowShowsItsTop()
		{
			// A popup that is taller than the window cannot be fully clamped, so the clamp has to choose an
			// end to sacrifice. Showing the top is the only useful choice - the bottom aligned alternative
			// puts the first items (the ones a user is looking for) above the top of the window.
			var systemWindow = new SystemWindow(400, 300);
			var theme = new ThemeConfig();

			var anchor = new GuiWidget(50, 20);
			systemWindow.AddChild(anchor);

			var tallPopup = new GuiWidget(150, 960);

			systemWindow.ShowPopup(
				theme,
				new MatePoint(anchor)
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Bottom)
				},
				new MatePoint(tallPopup)
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
					AltMate = new MateOptions(MateEdge.Right, MateEdge.Bottom)
				});

			var bounds = tallPopup.BoundsRelativeToParent;

			await Assert.That(bounds.Top).IsEqualTo(systemWindow.Height);
		}
	}
}
