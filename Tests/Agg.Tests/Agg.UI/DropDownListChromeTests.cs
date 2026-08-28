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
using MatterHackers.Agg.Image;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Pins the Windows 11 style chrome on the legacy menu row family - <see cref="Menu"/>,
	/// <see cref="MenuItem"/> and the two states views - so a drop down list popup looks like a
	/// <see cref="PopupMenu"/> popup rather than the square cornered panel it used to be.
	/// </summary>
	/// <remarks>
	/// The two families paint their highlight in completely different ways (a PopupMenu row *is* its
	/// highlight; here it is a states view's background, or the background of one of two swapped child
	/// widgets), so what is asserted is the resulting look and not a shared implementation. Every number
	/// comes from <see cref="ThemeConfig"/>, which is what ties the two families together: if the theme
	/// retunes the menu radius, both move and these stay true.
	/// </remarks>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class DropDownListChromeTests
	{
		private const string PopupName = "_OpenMenuContents";

		/// <summary>
		/// Runs out whatever this test left on the idle queue.
		/// </summary>
		/// <remarks>
		/// <see cref="UiThread"/>'s pending action queue is process wide, and opening a drop down posts focus
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
		public async Task DropDownPopupIsRounded()
		{
			var theme = new ThemeConfig();
			var (systemWindow, _) = OpenDropDown();

			var popup = systemWindow.Descendants<GuiWidget>().First(w => w.Name == PopupName);

			await Assert.That(popup.BackgroundRadius.NW).IsGreaterThan(0)
				.Because("a drop down panel with square corners is the look this styling replaced");

			await Assert.That(popup.BackgroundRadius == new RadiusCorners(theme.MenuPopupRadius)).IsTrue()
				.Because("a drop down panel rounds by the same theme radius a PopupMenu panel does");
		}

		[Test]
		public async Task TextRowHighlightIsRoundedAndInsetFromThePanel()
		{
			var theme = new ThemeConfig();
			var (systemWindow, _) = OpenDropDown();

			var popup = systemWindow.Descendants<GuiWidget>().First(w => w.Name == PopupName);

			var rows = popup.Descendants<MenuItemColorStatesView>().ToList();

			await Assert.That(rows.Count).IsEqualTo(2);

			foreach (var row in rows)
			{
				// This states view paints the highlight as its own BackgroundColor (see its Highlighted
				// setter), so rounding the view rounds the highlight.
				await Assert.That(row.BackgroundRadius.NW).IsGreaterThan(0)
					.Because("a square highlight in a rounded panel is what the inset look avoids");

				await Assert.That(row.BackgroundRadius == new RadiusCorners(theme.MenuRowRadius)).IsTrue();
			}

			var panel = popup.LocalBounds;

			// MenuItem's own margin is the inset here - the highlight must land clear of the panel's rounded
			// corners on all four sides, or the corners cut it and the top and bottom rows go square again.
			foreach (var row in new[] { rows.First(), rows.Last() })
			{
				var bounds = row.TransformToParentSpace(popup, row.LocalBounds);

				await Assert.That(panel.Top - bounds.Top).IsGreaterThan(0)
					.Because("no row highlight may reach the top edge of a rounded panel");

				await Assert.That(bounds.Bottom - panel.Bottom).IsGreaterThan(0)
					.Because("no row highlight may reach the bottom edge of a rounded panel");

				await Assert.That(bounds.Left - panel.Left).IsGreaterThan(0);
				await Assert.That(panel.Right - bounds.Right).IsGreaterThan(0);
			}
		}

		/// <summary>
		/// The icon carrying overload builds a <see cref="MenuItemStatesView"/> instead, which swaps two
		/// full bleed child widgets rather than changing a color - so the rounding has to go on those
		/// children, and on both of them, since either one can be the visible row.
		/// </summary>
		[Test]
		public async Task IconRowStatesAreRounded()
		{
			var theme = new ThemeConfig();
			var (systemWindow, _) = OpenDropDown();

			var popup = systemWindow.Descendants<GuiWidget>().First(w => w.Name == PopupName);

			var statesView = popup.Descendants<MenuItemStatesView>().Single();

			await Assert.That(statesView.Children.Count).IsEqualTo(2);

			foreach (var state in statesView.Children)
			{
				await Assert.That(state.BackgroundRadius.NW).IsGreaterThan(0)
					.Because("the visible state widget is what carries the row fill, hovered or not");

				await Assert.That(state.BackgroundRadius == new RadiusCorners(theme.MenuRowRadius)).IsTrue();
			}
		}

		/// <summary>
		/// A two text row plus one icon row list, opened, so everything has real bounds to measure.
		/// </summary>
		private static (SystemWindow systemWindow, DropDownList dropDown) OpenDropDown()
		{
			var systemWindow = new SystemWindow(400, 300);

			var dropDown = new DropDownList("no selection", Color.Black)
			{
				Name = "dropDown",
			};

			dropDown.AddItem("Item 0");
			dropDown.AddItem("Item 1");
			dropDown.AddItem(new ImageBuffer(16, 16), "Item 2");

			systemWindow.AddChild(dropDown);
			dropDown.Position = new Vector2(10, 200);

			dropDown.InvokeClick();

			return (systemWindow, dropDown);
		}
	}
}
