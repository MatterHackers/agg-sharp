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
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A finger driving a <see cref="MenuBarWidget"/> and the <see cref="PopupMenu"/> chain it opens, through a
	/// <see cref="SystemWindow"/> so the touch gestures (<see cref="TouchPressDeferral"/>) shape the events the
	/// way they do on a phone. As in agg-gui (<c>widgets/menu/state.rs</c> <c>update_hover</c>), a finger has
	/// no hover: a sub menu opens on the tap itself and stays up after the finger lifts.
	/// </summary>
	/// <remarks>
	/// The idle queue is pumped between the finger going down and coming up, because on a device those are
	/// separate frames - and that is where a hover-opened sub menu got shown, only to be closed by the press the
	/// lift delivered.
	/// </remarks>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class MenuTouchTapTests
	{
		[After(Test)]
		public void DrainTheIdleQueue()
		{
			PumpIdle();
		}

		[Test]
		public async Task TappingASubMenuRowOpensItAndItStaysOpenAfterTheFingerLifts()
		{
			var harness = Harness.Show();

			harness.Tap(harness.CenterOf("File Menu"));
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0);

			harness.Tap(harness.CenterOf("Recent Menu Item"));
			PumpIdle();

			var subMenu = harness.SubMenuOf("Recent Menu Item");
			await Assert.That(subMenu).IsNotNull()
				.Because("tapping a row with a sub menu opens it");
			await Assert.That(subMenu.HasBeenClosed).IsFalse()
				.Because("the sub menu stays up after the finger lifts, so its rows can be tapped");
			await Assert.That(harness.Find("Report Menu Item")).IsNotNull();
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0);

			harness.Tap(harness.CenterOf("Report Menu Item"));
			PumpIdle();

			await Assert.That(harness.Actions).IsEquivalentTo(new[] { "Report" });
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(0)
				.Because("choosing a row closes the whole chain");
			await Assert.That(harness.Bar.OpenMenuIndex).IsNull();
		}

		[Test]
		public async Task TappingTheRowOfAnOpenSubMenuAgainKeepsItOpen()
		{
			var harness = Harness.Show();

			harness.Tap(harness.CenterOf("File Menu"));
			harness.Tap(harness.CenterOf("Recent Menu Item"));
			PumpIdle();
			var subMenu = harness.SubMenuOf("Recent Menu Item");
			await Assert.That(subMenu).IsNotNull();

			// agg-gui's handle_left_down sets open_path to the pressed row: a second tap is not a toggle
			harness.Tap(harness.CenterOf("Recent Menu Item"));
			PumpIdle();

			await Assert.That(subMenu.HasBeenClosed).IsFalse();
			await Assert.That(harness.SubMenuOf("Recent Menu Item")).IsSameReferenceAs(subMenu);

			harness.Tap(harness.CenterOf("Report Menu Item"));
			PumpIdle();
			await Assert.That(harness.Actions).IsEquivalentTo(new[] { "Report" });
		}

		[Test]
		public async Task TappingOutsideAnOpenSubMenuClosesTheMenus()
		{
			var harness = Harness.Show();

			harness.Tap(harness.CenterOf("File Menu"));
			harness.Tap(harness.CenterOf("Recent Menu Item"));
			PumpIdle();
			await Assert.That(harness.SubMenuOf("Recent Menu Item")).IsNotNull();

			harness.Tap(new Vector2(harness.Window.Width - 10, 10));
			PumpIdle();

			await Assert.That(harness.OpenMenus.Count).IsEqualTo(0);
			await Assert.That(harness.Bar.OpenMenuIndex).IsNull();
			await Assert.That(harness.Actions.Count).IsEqualTo(0);
		}

		[Test]
		public async Task TappingAnotherTitleWhileAMenuIsOpenSwitchesToIt()
		{
			var harness = Harness.Show();

			harness.Tap(harness.CenterOf("File Menu"));
			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(0);

			// the finger landing on Edit is not a hover that switches the menu, so the tap then toggles it
			// closed again - the tap itself switches
			harness.Tap(harness.CenterOf("Edit Menu"));
			PumpIdle();

			await Assert.That(harness.Bar.OpenMenuIndex).IsEqualTo(1);
			await Assert.That(harness.Find("Copy Menu Item")).IsNotNull();
		}

		[Test]
		public async Task AMouseStillOpensASubMenuOnHoverAndClicksItsRow()
		{
			var harness = Harness.Show();

			harness.MouseClick(harness.CenterOf("File Menu"));
			harness.MouseMove(harness.CenterOf("Recent Menu Item"));
			PumpIdle();
			await Assert.That(harness.SubMenuOf("Recent Menu Item")).IsNotNull()
				.Because("a mouse hover still opens a sub menu");

			// agg-gui's handle_left_down: a press on a sub menu row sets open_path to it, so the sub menu stays
			var subMenu = harness.SubMenuOf("Recent Menu Item");
			harness.MouseClick(harness.CenterOf("Recent Menu Item"));
			PumpIdle();
			await Assert.That(subMenu.HasBeenClosed).IsFalse()
				.Because("clicking the row whose sub menu is open keeps it open");
			await Assert.That(harness.SubMenuOf("Recent Menu Item")).IsSameReferenceAs(subMenu);

			harness.MouseMove(harness.CenterOf("Report Menu Item"));
			harness.MouseClick(harness.CenterOf("Report Menu Item"));
			PumpIdle();

			await Assert.That(harness.Actions).IsEquivalentTo(new[] { "Report" });
			await Assert.That(harness.OpenMenus.Count).IsEqualTo(0);
		}

		private static void PumpIdle()
		{
			for (int i = 0; i < 4; i++)
			{
				UiThread.InvokePendingActions();
			}
		}

		/// <summary>A window with a File (New, Recent > Report) and Edit (Copy) menu bar.</summary>
		private class Harness
		{
			public SystemWindow Window { get; private set; }

			public MenuBarWidget Bar { get; private set; }

			public List<string> Actions { get; } = new List<string>();

			public List<PopupMenu> OpenMenus => Window.Descendants<PopupMenu>().Where(m => !m.HasBeenClosed).ToList();

			public static Harness Show()
			{
				var harness = new Harness { Window = new SystemWindow(600, 400) };
				var theme = new ThemeConfig();

				MenuItemModel Item(string text) => new MenuItemModel { Text = text, Action = () => harness.Actions.Add(text) };

				var menus = new List<MenuItemModel>
				{
					new MenuItemModel
					{
						Text = "File",
						SubMenuItems = () => new List<MenuItemModel>
						{
							Item("New"),
							new MenuItemModel { Text = "Recent", SubMenuItems = () => new List<MenuItemModel> { Item("Report") } },
						},
					},
					new MenuItemModel { Text = "Edit", SubMenuItems = () => new List<MenuItemModel> { Item("Copy") } },
				};

				harness.Bar = new MenuBarWidget(menus, theme) { VAnchor = VAnchor.Top };

				// one container deep, as a real host is - see MenuBarWidgetTests.MenuBarHarness.Show
				var host = new GuiWidget { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
				harness.Window.AddChild(host);
				host.AddChild(harness.Bar);

				return harness;
			}

			public GuiWidget Find(string name) => Window.FindDescendant(name);

			public Vector2 CenterOf(string name)
			{
				var widget = Find(name);
				return widget.TransformToScreenSpace(widget.LocalBounds).Center;
			}

			public PopupMenu SubMenuOf(string rowName) => (Find(rowName) as PopupMenu.SubMenuItemButton)?.SubMenu;

			/// <summary>A finger down and up on one spot, with the frames between them run.</summary>
			public void Tap(Vector2 point)
			{
				Window.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0) { PointerType = PointerType.Touch });
				PumpIdle();
				Window.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0) { PointerType = PointerType.Touch });
				PumpIdle();
			}

			public void MouseMove(Vector2 point) => Window.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, 0));

			public void MouseClick(Vector2 point)
			{
				Window.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
				Window.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
				PumpIdle();
			}
		}
	}
}
