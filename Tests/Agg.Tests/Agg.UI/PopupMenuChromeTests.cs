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
	/// Pins the Windows 11 style menu chrome - rounded panel, inset and rounded row highlight, tight row
	/// pitch - so it cannot be undone by a stray tweak to a padding or a MinimumSize.
	/// </summary>
	/// <remarks>
	/// Everything here is asserted against the named <see cref="ThemeConfig"/> properties rather than
	/// against literal pixel counts, so retuning the look is a one line change in the theme and these stay
	/// true. What they are actually defending is the *relationship*: that the row is the theme's row
	/// height and not something taller, and that the highlight is held clear of a panel corner that is
	/// rounded. Those two together are what keeps the first and last rows from poking square corners out
	/// of the panel, which is the bug this styling exists to avoid.
	/// </remarks>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class PopupMenuChromeTests
	{
		/// <summary>
		/// Runs out whatever this test left on the idle queue.
		/// </summary>
		/// <remarks>
		/// <see cref="UiThread"/>'s pending action queue is process wide, and opening a popup posts focus
		/// handling to it. These tests never pump, so without this every popup they open leaves a close-check
		/// for whichever test pumps next - which then owns any exception it throws, a failure that rotates
		/// between innocent tests and reads as flake.
		/// </remarks>
		[After(Test)]
		public void DrainTheIdleQueue()
		{
			for (int i = 0; i < 4; i++)
			{
				UiThread.InvokePendingActions();
			}
		}

		[Test]
		public async Task MenuPanelIsRounded()
		{
			var theme = new ThemeConfig();
			var menu = new PopupMenu(theme);

			await Assert.That(menu.BackgroundRadius.NW).IsGreaterThan(0)
				.Because("a menu panel with square corners is the look this styling replaced");

			await Assert.That(menu.BackgroundRadius == new RadiusCorners(theme.MenuPopupRadius)).IsTrue()
				.Because("the panel rounds by the theme's named menu radius on all four corners");
		}

		[Test]
		public async Task MenuRowsAreTheThemeRowHeight()
		{
			var theme = new ThemeConfig();
			var (_, menu) = LaidOutMenu(theme);

			foreach (var row in menu.Children.OfType<PopupMenu.MenuItem>())
			{
				await Assert.That(row.MinimumSize.Y).IsEqualTo(theme.MenuRowHeight).Within(0.001)
					.Because("the row floor is the theme's menu row height, not the taller ButtonHeight");

				// A row is VAnchor.Fit, so the padded label can push it above the floor. It must not: the
				// label's vertical padding (PopupMenu.MenuLabelVerticalPadding) was brought down with the
				// row height precisely so the floor is what decides the pitch.
				await Assert.That(row.Height).IsEqualTo(theme.MenuRowHeight).Within(0.001)
					.Because("the label padding has to leave the row's own minimum height binding");
			}
		}

		[Test]
		public async Task RowHighlightIsInsetFromThePanelAndRoundedItself()
		{
			var theme = new ThemeConfig();
			var (_, menu) = LaidOutMenu(theme);

			var rows = menu.Children.OfType<PopupMenu.MenuItem>().ToList();

			await Assert.That(rows.Count).IsEqualTo(3);

			foreach (var row in rows)
			{
				// The highlight *is* the row's background (PopupMenu.MenuItem.BackgroundColor returns
				// HoverColor when hovered or focused), so rounding the row rounds the highlight.
				await Assert.That(row.BackgroundRadius.NW).IsGreaterThan(0)
					.Because("a square highlight in a rounded panel is what the inset look avoids");

				await Assert.That(row.BackgroundRadius == new RadiusCorners(theme.MenuRowRadius)).IsTrue();
			}

			var panel = menu.LocalBounds;

			// The first and last rows are the ones that meet the panel's rounded corners, so they are the
			// ones that have to be held clear of them.
			var first = rows.First().TransformToParentSpace(menu, rows.First().LocalBounds);
			var last = rows.Last().TransformToParentSpace(menu, rows.Last().LocalBounds);

			await Assert.That(panel.Top - first.Top).IsGreaterThan(0)
				.Because("the top row's highlight must not reach the top edge of a rounded panel");

			await Assert.That(last.Bottom - panel.Bottom).IsGreaterThan(0)
				.Because("the bottom row's highlight must not reach the bottom edge of a rounded panel");

			await Assert.That(first.Left - panel.Left).IsGreaterThan(0);
			await Assert.That(panel.Right - first.Right).IsGreaterThan(0);
		}

		/// <summary>
		/// The popup that hosts a menu for the drop down path draws its own outline, and that outline is
		/// around the menu panel - so it has to be rounded the same way or it puts the square corners back.
		/// </summary>
		[Test]
		public async Task APopupHostingAMenuWearsTheMenuCorners()
		{
			var theme = new ThemeConfig();
			var window = new SystemWindow(600, 400);

			var anchor = new GuiWidget(50, 20);
			window.AddChild(anchor);
			anchor.Position = new Vector2(10, 200);

			var menu = new PopupMenu(theme);
			menu.CreateMenuItem("Open");
			menu.CreateMenuItem("Close");

			var layoutEngine = new PopupLayoutEngine(menu, anchor, Direction.Down, maxHeight: 0, alignToRightEdge: false);
			var popup = new PopupWidget(menu, layoutEngine, makeScrollable: true);

			await Assert.That(popup.BackgroundRadius == new RadiusCorners(theme.MenuPopupRadius)).IsTrue()
				.Because("the host takes its corners from the content it is wrapping");
		}

		/// <summary>
		/// A menu too tall for its popup scrolls, and scrolling widens the popup by a scroll bar while the
		/// menu inside it - HAnchor.Left | Fit - keeps its old width. The rounded fill has to move to the
		/// popup when that happens, because the popup's bounds are what the rounded border is traced on.
		/// </summary>
		/// <remarks>
		/// Left on the menu the fill is a scroll bar narrower than the border around it, so the panel draws
		/// two rounded right edges; and since the menu rides inside the scroll area, its fill would slide up
		/// out of the border as the menu is scrolled.
		/// </remarks>
		[Test]
		public async Task AScrolledPopupCarriesTheRoundedFillItsBorderTraces()
		{
			var theme = new ThemeConfig();
			var window = new SystemWindow(600, 400);

			var anchor = new GuiWidget(50, 20);
			window.AddChild(anchor);
			anchor.Position = new Vector2(10, 200);

			var menu = new PopupMenu(theme);
			for (int i = 0; i < 8; i++)
			{
				menu.CreateMenuItem($"Item {i}");
			}

			// Two rows worth of height for an eight row menu, so the popup has to scroll
			var layoutEngine = new PopupLayoutEngine(menu, anchor, Direction.Down, maxHeight: theme.MenuRowHeight * 2, alignToRightEdge: false);
			var popup = new PopupWidget(menu, layoutEngine, makeScrollable: true);

			// The condition the fix is about: the popup is wider than the menu it is wrapping
			await Assert.That(popup.Width).IsGreaterThan(menu.Width)
				.Because("making the menu scroll widens the popup by a scroll bar");

			await Assert.That(popup.BackgroundColor.Alpha0To255).IsGreaterThan(0)
				.Because("the widget whose bounds the border traces is the one that has to carry the fill");

			await Assert.That(menu.BackgroundColor.Alpha0To255).IsEqualTo(0)
				.Because("two filled panels of different widths is the double edge this avoids");

			await Assert.That(popup.BackgroundRadius == new RadiusCorners(theme.MenuPopupRadius)).IsTrue()
				.Because("the fill it took over rounds the way the menu's did");
		}

		/// <summary>
		/// A three row menu parented into a window, laid out, so the rows have real bounds to measure.
		/// </summary>
		private static (SystemWindow window, PopupMenu menu) LaidOutMenu(ThemeConfig theme)
		{
			var window = new SystemWindow(400, 400);

			var menu = new PopupMenu(theme);
			menu.CreateMenuItem("First");
			menu.CreateMenuItem("Middle");
			menu.CreateMenuItem("Last");

			window.AddChild(menu);

			return (window, menu);
		}
	}
}
