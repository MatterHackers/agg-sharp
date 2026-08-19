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
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THE
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Drag the window from a Retina display to a standard one and its surface loses half its pixels in one
	/// step. The host hands that new size to the SystemWindow, and if the widget tree lays out any taller than
	/// the surface it just got, the excess is not merely wasted - agg is y-up, so the overflow falls off the
	/// <em>top</em> and the application's toolbars disappear under the title bar until the user resizes the
	/// window by hand.
	/// <para>
	/// These tests pin the rule that prevents that: a size reported by a platform host is physical reality and
	/// beats <see cref="GuiWidget.MinimumSize"/>, which at that instant is still the application's minimum for
	/// the display the window just left. Interactive minimums are enforced by the native window itself
	/// (<c>setContentMinSize:</c> on the mac, <c>Form.MinimumSize</c> on Windows), so nothing is lost by
	/// letting agg's copy stand down here.
	/// </para>
	/// </summary>
	public class PlatformSurfaceBoundsTests
	{
		[Test]
		public async Task APlatformSurfaceSmallerThanTheMinimumIsHonored()
		{
			// The Retina -> standard case exactly: the application sized its minimum in device pixels for a 2x
			// display, and the window is now on a 1x one whose whole surface is smaller than that minimum.
			var systemWindow = new SystemWindow(1024, 1530);
			systemWindow.MinimumSize = new Vector2(900, 900);

			systemWindow.SetBoundsFromPlatform(1024, 765);

			await Assert.That(systemWindow.LocalBounds.Height).IsEqualTo(765);
			await Assert.That(systemWindow.LocalBounds.Width).IsEqualTo(1024);
		}

		[Test]
		public async Task AnOrdinaryBoundsAssignmentStillClamps()
		{
			// Only the platform path is exempt. Everything else - application code, layout, the widget tree -
			// keeps the long-standing contract that assigning LocalBounds cannot take a widget below its
			// MinimumSize.
			var systemWindow = new SystemWindow(1024, 1530);
			systemWindow.MinimumSize = new Vector2(900, 900);

			systemWindow.LocalBounds = new RectangleDouble(0, 0, 1024, 765);

			await Assert.That(systemWindow.LocalBounds.Height).IsEqualTo(900);
		}

		[Test]
		public async Task AnOrdinaryWidgetStillGrowsToANewMinimum()
		{
			// The seam SystemWindow overrides has to leave every other widget exactly as it was: raising
			// MinimumSize above the current bounds still pushes the bounds up.
			var widget = new GuiWidget(100, 100);

			widget.MinimumSize = new Vector2(300, 200);

			await Assert.That(widget.LocalBounds.Width).IsEqualTo(300);
			await Assert.That(widget.LocalBounds.Height).IsEqualTo(200);
		}

		[Test]
		public async Task ALaterLargerSurfaceGrowsTheWindowBack()
		{
			// Bypassing the clamp must not pin the window small - the next report is just as authoritative as
			// the one that shrank it (drag back to the Retina display, or maximize).
			var systemWindow = new SystemWindow(1024, 1530);
			systemWindow.MinimumSize = new Vector2(900, 900);

			systemWindow.SetBoundsFromPlatform(1024, 765);
			systemWindow.SetBoundsFromPlatform(2048, 1530);

			await Assert.That(systemWindow.LocalBounds.Height).IsEqualTo(1530);
			await Assert.That(systemWindow.LocalBounds.Width).IsEqualTo(2048);
		}

		[Test]
		public async Task RaisingTheMinimumAfterASurfaceReportDoesNotInflateTheWindow()
		{
			// This is the second half of the bug. The application learns about the scale change from the idle
			// queue, after the surface has already shrunk, and recomputes its minimum - if that recomputation
			// (or any stale one that lands late) could inflate the bounds again, the toolbars go back under the
			// title bar. Once a host has stated the surface size, the surface size is what the window lays out
			// to; the minimum is still published to the native window, which is what enforces it against the
			// user's drag.
			var systemWindow = new SystemWindow(1024, 1530);

			systemWindow.SetBoundsFromPlatform(1024, 765);
			systemWindow.MinimumSize = new Vector2(900, 900);

			await Assert.That(systemWindow.LocalBounds.Height).IsEqualTo(765);
			await Assert.That(systemWindow.MinimumSize.Y).IsEqualTo(900);
		}
	}
}
