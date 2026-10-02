/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// On a phone-width window a sub menu can fit neither to the right of the row that opened it nor to the
	/// left. It used to be clamped back on screen right over its parent, covering the very row (label and icon)
	/// the user had just tapped. It now drops below that row instead, so the row stays readable.
	/// </summary>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class NarrowSubMenuPlacementTests
	{
		[After(Test)]
		public void DrainTheIdleQueue()
		{
			for (int i = 0; i < 4; i++)
			{
				UiThread.InvokePendingActions();
			}
		}

		[Test]
		public async Task ASubMenuThatFitsNeitherSideOpensBelowItsRowOnANarrowWindow()
		{
			// Lars's phone: about 411 CSS pixels across
			var (window, row, subMenu) = OpenColorSubMenu(411, 800);

			var rowBounds = row.TransformToScreenSpace(row.LocalBounds);
			var parentBounds = row.Parent.TransformToScreenSpace(row.Parent.LocalBounds);
			var subMenuBounds = subMenu.TransformToScreenSpace(subMenu.LocalBounds);

			// The fixture really is the case under test: neither side of the parent has room
			await Assert.That(parentBounds.Right + subMenu.Width).IsGreaterThan(window.Width);
			await Assert.That(parentBounds.Left - subMenu.Width).IsLessThan(0);

			bool overlapsRow = subMenuBounds.Left < rowBounds.Right
				&& subMenuBounds.Right > rowBounds.Left
				&& subMenuBounds.Bottom < rowBounds.Top
				&& subMenuBounds.Top > rowBounds.Bottom;
			await Assert.That(overlapsRow).IsFalse()
				.Because("the sub menu must not cover the row that opened it");
			await Assert.That(subMenuBounds.Top).IsLessThanOrEqualTo(rowBounds.Bottom)
				.Because("with room below the row, the sub menu hangs from it");

			await Assert.That(subMenuBounds.Left).IsGreaterThanOrEqualTo(0);
			await Assert.That(subMenuBounds.Right).IsLessThanOrEqualTo(window.Width);
			await Assert.That(subMenuBounds.Bottom).IsGreaterThanOrEqualTo(0);
			await Assert.That(subMenuBounds.Top).IsLessThanOrEqualTo(window.Height);
		}

		[Test]
		public async Task ASubMenuOnAWideWindowStillOpensToTheRightOfItsRow()
		{
			var (window, row, subMenu) = OpenColorSubMenu(1200, 800);

			var rowBounds = row.TransformToScreenSpace(row.LocalBounds);
			var subMenuBounds = subMenu.TransformToScreenSpace(subMenu.LocalBounds);

			await Assert.That(subMenuBounds.Left).IsGreaterThanOrEqualTo(rowBounds.Right - .001)
				.Because("a sub menu with room to the right opens there");
			await Assert.That(subMenuBounds.Top).IsEqualTo(rowBounds.Top)
				.Because("its top lines up with the row that opened it");
		}

		/// <summary>
		/// Opens View, then its Color row, in a menu bar docked at the top of a window of the given size.
		/// The rows are long enough that the menus are wider than half of a 411 pixel window.
		/// </summary>
		private static (SystemWindow window, GuiWidget row, PopupMenu subMenu) OpenColorSubMenu(int width, int height)
		{
			var window = new SystemWindow(width, height);
			var theme = new ThemeConfig();

			var menus = new List<MenuItemModel>
			{
				new MenuItemModel
				{
					Text = "View",
					SubMenuItems = () => new List<MenuItemModel>
					{
						new MenuItemModel { Text = "Show the long descriptive panel", Action = () => { } },
						new MenuItemModel
						{
							Text = "Color",
							SubMenuItems = () => new List<MenuItemModel>
							{
								new MenuItemModel { Text = "A rather long colour choice name", Action = () => { } },
								new MenuItemModel { Text = "Another long colour choice name", Action = () => { } },
							},
						},
						new MenuItemModel { Text = "Rows under the opening row", Action = () => { } },
					},
				},
			};

			var bar = new MenuBarWidget(menus, theme)
			{
				VAnchor = VAnchor.Top,
			};

			// A host between window and bar, as in MenuBarWidgetTests: every real host has one
			var host = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			window.AddChild(host);
			host.AddChild(bar);

			Click(window, Center(window.FindDescendant("View Menu")));
			Pump();

			var row = window.FindDescendant("Color Menu Item");
			Click(window, Center(row));
			Pump();

			var subMenu = (row as PopupMenu.SubMenuItemButton)?.SubMenu;

			return (window, row, subMenu);
		}

		private static Vector2 Center(GuiWidget widget)
		{
			return widget.TransformToScreenSpace(widget.LocalBounds).Center;
		}

		private static void Click(SystemWindow window, Vector2 point)
		{
			window.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
			window.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
		}

		private static void Pump()
		{
			for (int i = 0; i < 4; i++)
			{
				UiThread.InvokePendingActions();
			}
		}
	}
}
