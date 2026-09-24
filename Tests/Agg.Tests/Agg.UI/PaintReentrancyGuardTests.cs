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

using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A window never paints inside its own paint. The field crash ("A full-frame capture is already in
	/// progress.") was a WM_PAINT dispatched while the same window was mid-paint: the UI thread is STA, so a
	/// pumping wait inside the 3D draw can run the idle pump's Invoke, whose Update() paints synchronously.
	/// The nested request has to be deferred - not drawn, and not lost either.
	/// </summary>
	public class PaintReentrancyGuardTests
	{
		[Test]
		public async Task APaintOutsideAnyOtherPaintRuns()
		{
			var guard = new PaintReentrancyGuard();

			await Assert.That(guard.TryBeginPaint()).IsTrue();
			await Assert.That(guard.IsPainting).IsTrue();

			// Nothing asked for a paint while it ran, so there is nothing to request afterwards.
			await Assert.That(guard.EndPaint()).IsFalse();
			await Assert.That(guard.IsPainting).IsFalse();
		}

		[Test]
		public async Task APaintRequestedDuringAPaintIsDeferredThenRequested()
		{
			var guard = new PaintReentrancyGuard();

			await Assert.That(guard.TryBeginPaint()).IsTrue();

			// The re-entered WM_PAINT: not executed ...
			await Assert.That(guard.TryBeginPaint()).IsFalse();
			await Assert.That(guard.TryBeginPaint()).IsFalse();

			// ... and the outer paint still owns the window.
			await Assert.That(guard.IsPainting).IsTrue();

			// WinForms validated the nested paint's region before OnPaint ran, so the frame it asked for is
			// only kept by the outer paint asking again once it is done - once, however many were deferred.
			await Assert.That(guard.EndPaint()).IsTrue();
			await Assert.That(guard.IsPainting).IsFalse();

			// The deferred request was spent; the next paint starts clean.
			await Assert.That(guard.TryBeginPaint()).IsTrue();
			await Assert.That(guard.EndPaint()).IsFalse();
		}
	}
}
