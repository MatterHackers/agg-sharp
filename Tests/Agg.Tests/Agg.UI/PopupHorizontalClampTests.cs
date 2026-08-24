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

using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The horizontal half of agg-gui's <c>popup_clamps_to_viewport</c>
	/// (<c>agg-gui/src/widgets/menu/mod.rs</c>), which <see cref="PopupMenuConformanceTests"/> could not
	/// port while <c>BestPopupPosition</c> clamped only vertically.
	/// </summary>
	/// <remarks>
	/// Every test here mates the popup with an <c>AltMate</c> that resolves to the same offset as its
	/// <c>Mate</c>, which is the case the clamp exists for: flipping to the alt mate is only a rescue when
	/// the two mates actually differ, and several perfectly ordinary combinations (anchor left to popup
	/// left, for one) resolve to no offset at all - leaving the popup exactly where it did not fit.
	/// <para>
	/// These are headless. A <see cref="SystemWindow"/> is used as a plain widget and the popup is opened
	/// through the real <see cref="SystemWindowExtension.ShowPopup(SystemWindow, ThemeConfig, MatePoint, MatePoint, RectangleDouble, int)"/>
	/// entry point, so the position asserted is the one production placement produced. The idle queue is
	/// process wide (ShowPopup's focus handling posts to it), so this class shares the constraint key the
	/// other popup suites use.
	/// </para>
	/// </remarks>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class PopupHorizontalClampTests
	{
		/// <summary>
		/// Runs out whatever this test left on the idle queue.
		/// </summary>
		/// <remarks>
		/// <see cref="UiThread"/>'s pending action queue is process wide, and <c>ShowPopup</c> posts its focus
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

		private const double WindowWidth = 600;
		private const double WindowHeight = 400;

		/// <summary>
		/// A popup anchored hard against the right edge, whose mate flip cannot save it, is pulled back
		/// until its right edge is on the window's.
		/// </summary>
		[Test]
		public async Task PopupAtTheRightEdgeIsClampedInsideTheWindow()
		{
			var popup = ShowPopup(anchorX: 550, popupWidth: 200, anchorEdge: MateEdge.Left, popupEdge: MateEdge.Left);

			// Without the clamp this is 550, hanging 150 pixels past the right edge
			await Assert.That(popup.Position.X).IsEqualTo(WindowWidth - popup.Width);
			await Assert.That(popup.Position.X + popup.Width).IsLessThanOrEqualTo(WindowWidth);
			await Assert.That(popup.Position.X).IsGreaterThanOrEqualTo(0);
		}

		/// <summary>
		/// The mirror case: a popup right aligned to an anchor near the left edge would start at a negative
		/// X, and is pushed back to the window's left edge.
		/// </summary>
		[Test]
		public async Task PopupAtTheLeftEdgeIsClampedInsideTheWindow()
		{
			// Right edge of the popup mated to the left edge of the anchor, so the popup extends left
			var popup = ShowPopup(anchorX: 10, popupWidth: 200, anchorEdge: MateEdge.Left, popupEdge: MateEdge.Right);

			// Without the clamp this is -190, with almost the whole popup off the left of the window
			await Assert.That(popup.Position.X).IsEqualTo(0);
			await Assert.That(popup.Position.X + popup.Width).IsLessThanOrEqualTo(WindowWidth);
		}

		/// <summary>
		/// The clamp must not become a general alignment rule: a popup that already fits lands exactly where
		/// the mate put it, to the pixel. Callers (drop downs, tool tips, sub menus) position by mating
		/// edges and would be visibly wrong if placement drifted.
		/// </summary>
		[Test]
		[Arguments(MateEdge.Left, MateEdge.Left, 200d)]
		[Arguments(MateEdge.Right, MateEdge.Left, 250d)]
		[Arguments(MateEdge.Left, MateEdge.Right, 50d)]
		[Arguments(MateEdge.Right, MateEdge.Right, 100d)]
		public async Task APopupThatFitsKeepsItsExactMateAlignedPosition(MateEdge anchorEdge, MateEdge popupEdge, double expectedX)
		{
			// The anchor is 50 wide at x = 200, well away from either edge, and the popup is 150 wide, so
			// every one of the four mate combinations lands entirely inside the window
			var popup = ShowPopup(anchorX: 200, popupWidth: 150, anchorEdge: anchorEdge, popupEdge: popupEdge);

			await Assert.That(popup.Position.X).IsEqualTo(expectedX);
		}

		/// <summary>
		/// Nothing can show all of a popup wider than the window. It is left aligned rather than right
		/// aligned so the left edge - where menu icons and the start of every label live - is the part that
		/// survives.
		/// </summary>
		[Test]
		public async Task APopupWiderThanTheWindowIsLeftAligned()
		{
			var popup = ShowPopup(anchorX: 100, popupWidth: WindowWidth + 200, anchorEdge: MateEdge.Left, popupEdge: MateEdge.Left);

			await Assert.That(popup.Position.X).IsEqualTo(0);
		}

		/// <summary>
		/// Opens a popup of the given width against an anchor at the given X, with <c>AltMate</c> set to the
		/// same edges as <c>Mate</c> so the alt mate flip is a no-op and the clamp is what is under test.
		/// </summary>
		/// <returns>The popup widget, positioned by <c>BestPopupPosition</c>.</returns>
		private static GuiWidget ShowPopup(double anchorX, double popupWidth, MateEdge anchorEdge, MateEdge popupEdge)
		{
			var window = new SystemWindow(WindowWidth, WindowHeight);

			var anchor = new GuiWidget(50, 20)
			{
				Name = "Anchor",
			};
			window.AddChild(anchor);
			anchor.Position = new Vector2(anchorX, 200);

			var popup = new GuiWidget(popupWidth, 100)
			{
				Name = "Popup",
			};

			window.ShowPopup(
				new ThemeConfig(),
				new MatePoint(anchor)
				{
					Mate = new MateOptions(anchorEdge, MateEdge.Bottom),
					AltMate = new MateOptions(anchorEdge, MateEdge.Bottom),
				},
				new MatePoint(popup)
				{
					Mate = new MateOptions(popupEdge, MateEdge.Top),
					AltMate = new MateOptions(popupEdge, MateEdge.Top),
				});

			return popup;
		}
	}
}
