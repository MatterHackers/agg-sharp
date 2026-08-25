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
	/// While the user drags a window edge, the X server resizes the window under whatever frame was last
	/// presented, and the events arrive as a burst of <c>ConfigureNotify</c> a few milliseconds apart. Waiting
	/// for the host's own pump to come round means the drawable and the window disagree for the whole drag,
	/// which reads as the same smear a frozen pump gives on macOS. A resize burst cannot be synthesised -
	/// there is no server here to drag against - so what is tested is the decision that handler makes: paint
	/// now, or leave it to the pump.
	/// </summary>
	public class X11ResizePaintTests
	{
		[Test]
		public async Task AResizeBurstOnAShownWindowPaintsImmediately()
		{
			await Assert.That(X11SystemWindow.ShouldPaintSynchronouslyForResize(
				inResizeBurst: true, showCompleted: true, isInsidePaint: false, hasClosed: false, webGpuInitialized: true)).IsTrue();
		}

		[Test]
		public async Task AnIsolatedResizeIsLeftToThePump()
		{
			// One deliberate resize, not a drag. The pump picks it up on its very next pass, and painting
			// from the event handler would only move the same frame a few milliseconds earlier.
			await Assert.That(X11SystemWindow.ShouldPaintSynchronouslyForResize(
				inResizeBurst: false, showCompleted: true, isInsidePaint: false, hasClosed: false, webGpuInitialized: true)).IsFalse();
		}

		[Test]
		public async Task TheShowSequencesOwnResizesAreLeftToThePump()
		{
			// The case a burst timer cannot separate on its own: mapping and settling the window emits
			// configures as closely spaced as a drag does, so to a timer they are a burst. Painting there
			// would draw before ShowSystemWindow has finished bringing the window up, which is why the show
			// gate exists alongside the timer rather than instead of it.
			await Assert.That(X11SystemWindow.ShouldPaintSynchronouslyForResize(
				inResizeBurst: true, showCompleted: false, isInsidePaint: false, hasClosed: false, webGpuInitialized: true)).IsFalse();
		}

		[Test]
		public async Task APaintAlreadyRunningIsNotReEntered()
		{
			await Assert.That(X11SystemWindow.ShouldPaintSynchronouslyForResize(
				inResizeBurst: true, showCompleted: true, isInsidePaint: true, hasClosed: false, webGpuInitialized: true)).IsFalse();
		}

		[Test]
		public async Task AClosedOrUninitializedWindowDoesNotPaint()
		{
			// A ConfigureNotify can still be sitting in the queue after the window is destroyed, and the
			// first ones arrive before the swapchain exists at all.
			await Assert.That(X11SystemWindow.ShouldPaintSynchronouslyForResize(
				inResizeBurst: true, showCompleted: true, isInsidePaint: false, hasClosed: true, webGpuInitialized: true)).IsFalse();
			await Assert.That(X11SystemWindow.ShouldPaintSynchronouslyForResize(
				inResizeBurst: true, showCompleted: true, isInsidePaint: false, hasClosed: false, webGpuInitialized: false)).IsFalse();
		}
	}
}
