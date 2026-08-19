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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// While the user drags a window edge, AppKit runs a nested tracking loop that freezes the window host's
	/// own pump, so the only chance to draw the resized widget tree is inside the <c>windowDidResize:</c>
	/// callback itself. A real live resize cannot be synthesised - AppKit only raises <c>inLiveResize</c> for
	/// a genuine drag, inside its own loop - so what is tested here is the decision that callback makes:
	/// paint now, or leave it to the pump.
	/// </summary>
	public class MacLiveResizePaintTests
	{
		[Test]
		public async Task ALiveResizeTickPaintsImmediately()
		{
			// The whole point of the fix: without this the CAMetalLayer just stretches its last drawable
			// until the mouse comes up.
			await Assert.That(MacSystemWindow.ShouldPaintSynchronouslyForResize(
				inLiveResize: true, isInsidePaint: false, hasClosed: false, webGpuInitialized: true)).IsTrue();
		}

		[Test]
		public async Task AResizeOutsideALiveDragIsLeftToThePump()
		{
			// Screen changes, backing-scale changes and the sizing done during ShowSystemWindowOnMainThread
			// all come through the same callback. The pump is running for those, and painting eagerly there
			// would draw before the show/settle sequence has finished.
			await Assert.That(MacSystemWindow.ShouldPaintSynchronouslyForResize(
				inLiveResize: false, isInsidePaint: false, hasClosed: false, webGpuInitialized: true)).IsFalse();
		}

		[Test]
		public async Task APaintAlreadyRunningIsNotReEntered()
		{
			await Assert.That(MacSystemWindow.ShouldPaintSynchronouslyForResize(
				inLiveResize: true, isInsidePaint: true, hasClosed: false, webGpuInitialized: true)).IsFalse();
		}

		[Test]
		public async Task AClosedOrUninitializedWindowDoesNotPaint()
		{
			// A resize notification can still arrive while the window is tearing down, and the first ones
			// arrive before the swapchain exists at all.
			await Assert.That(MacSystemWindow.ShouldPaintSynchronouslyForResize(
				inLiveResize: true, isInsidePaint: false, hasClosed: true, webGpuInitialized: true)).IsFalse();
			await Assert.That(MacSystemWindow.ShouldPaintSynchronouslyForResize(
				inLiveResize: true, isInsidePaint: false, hasClosed: false, webGpuInitialized: false)).IsFalse();
		}
	}
}
