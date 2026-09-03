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
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Pins the rule ShowSystemWindow uses to decide whether the calling thread should become a
	/// message loop. WinForms' Application.MessageLoop is per-thread, so on its own it cannot tell
	/// "nobody anywhere is pumping" from "the UI thread is pumping and I am not it" - and answering
	/// the second case with Application.Run steals the caller's thread forever, while answering the
	/// first by deferring shows a window that never appears.
	/// </summary>
	public class WinformsMessageLoopDecisionTests
	{
		// Case A: a normal sequential start, and the explicit override the automation runner relies on.
		[Test]
		public async Task CaseAFirstWindowAlwaysStartsTheLoop()
		{
			await Assert.That(WinformsSystemWindow.ShouldStartMessageLoop(
				firstWindow: true,
				thisThreadHasLoop: false,
				anotherThreadIsPumping: false,
				deferredShowCanRun: false)).IsTrue();
		}

		[Test]
		public async Task LooplessThreadWithNoPumpAnywhereStartsTheLoop()
		{
			await Assert.That(WinformsSystemWindow.ShouldStartMessageLoop(
				firstWindow: false,
				thisThreadHasLoop: false,
				anotherThreadIsPumping: false,
				deferredShowCanRun: false)).IsTrue();
		}

		// Case C: an automation or worker thread asking for a window while the UI thread pumps with a live
		// idle driver must defer, not start a second loop that never returns.
		[Test]
		public async Task CaseCLooplessThreadDefersWhenAnotherThreadPumpsWithADriver()
		{
			await Assert.That(WinformsSystemWindow.ShouldStartMessageLoop(
				firstWindow: false,
				thisThreadHasLoop: false,
				anotherThreadIsPumping: true,
				deferredShowCanRun: true)).IsFalse();
		}

		// Case B: the stale latch of commit 95ac9254 - a previous Application.Run never returned, so the
		// latch says "not first", but the idle pump has no driver and a deferred show would never run.
		[Test]
		public async Task CaseBLooplessThreadStartsTheLoopWhenTheDeferredShowCouldNotRun()
		{
			await Assert.That(WinformsSystemWindow.ShouldStartMessageLoop(
				firstWindow: false,
				thisThreadHasLoop: false,
				anotherThreadIsPumping: true,
				deferredShowCanRun: false)).IsTrue();
		}

		[Test]
		public async Task ThreadThatAlreadyPumpsDefers()
		{
			await Assert.That(WinformsSystemWindow.ShouldStartMessageLoop(
				firstWindow: false,
				thisThreadHasLoop: true,
				anotherThreadIsPumping: false,
				deferredShowCanRun: false)).IsFalse();
		}
	}
}
