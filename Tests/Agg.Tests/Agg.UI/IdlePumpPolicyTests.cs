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
using MatterHackers.Agg.UI;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Which window is allowed to drive the shared idle pump.
	/// </summary>
	/// <remarks>
	/// The host that asks these questions is WinForms-only, but the rule is a pure one and the case it
	/// exists for is not reproducible on demand anywhere: a suite has to have leaked a window on a thread
	/// that stopped pumping. So the rule is pinned here directly, on every platform, and the three facts
	/// are arguments rather than <c>Control</c> properties.
	/// </remarks>
	public class IdlePumpPolicyTests
	{
		/// <summary>
		/// The failure that cost seven Windows CI tests: the driver looked healthy - undisposed, handle
		/// created, still in the live list - but its thread had stopped pumping, so every idle tick was
		/// marshaled into a queue nobody drained while the live pump sat in WaitMessage.
		/// </summary>
		[Test]
		public async Task AWindowOnThisThreadTakesOverFromAStaleDriverOnAnother()
		{
			await Assert.That(IdlePumpPolicy.ShouldTakeOverDriving(
				haveDriver: true,
				driverIsOnThisThread: false,
				candidateIsOnThisThread: true,
				driverHeartbeatIsStale: true)).IsTrue()
				.Because("a driver that has stopped delivering is starving this thread; this thread can serve itself");
		}

		/// <summary>
		/// The other half of the trade, and the reason the heartbeat exists at all: a driver on another
		/// thread that is still delivering is doing its job. Taking the pump from it would starve a live
		/// window and start running its queued work on this thread instead.
		/// </summary>
		[Test]
		public async Task ALiveDriverOnAnotherThreadKeepsThePump()
		{
			await Assert.That(IdlePumpPolicy.ShouldTakeOverDriving(
				haveDriver: true,
				driverIsOnThisThread: false,
				candidateIsOnThisThread: true,
				driverHeartbeatIsStale: false)).IsFalse();
		}

		[Test]
		public async Task AHealthyDriverOnThisThreadIsLeftAlone()
		{
			// Swapping drivers needlessly drops timer ticks, so the rule must not churn.
			await Assert.That(IdlePumpPolicy.ShouldTakeOverDriving(
				haveDriver: true,
				driverIsOnThisThread: true,
				candidateIsOnThisThread: true,
				driverHeartbeatIsStale: false)).IsFalse();

			// Even a stale heartbeat is not a reason to take the pump from this thread: it is already this
			// thread's, so a handover would change nothing but the bookkeeping.
			await Assert.That(IdlePumpPolicy.ShouldTakeOverDriving(
				haveDriver: true,
				driverIsOnThisThread: true,
				candidateIsOnThisThread: true,
				driverHeartbeatIsStale: true)).IsFalse();
		}

		[Test]
		public async Task WithNoDriverThisThreadsWindowTakesIt()
		{
			// No driver means nothing is delivering, whatever the heartbeat happens to read.
			await Assert.That(IdlePumpPolicy.ShouldTakeOverDriving(
				haveDriver: false,
				driverIsOnThisThread: false,
				candidateIsOnThisThread: true,
				driverHeartbeatIsStale: false)).IsTrue();
		}

		/// <summary>
		/// A candidate that is not on the asking thread is no improvement - handing the pump to it would
		/// only move the problem, and the existing search for any live window still covers that case.
		/// </summary>
		[Test]
		public async Task AWindowOnAnotherThreadNeverTakesOver()
		{
			await Assert.That(IdlePumpPolicy.ShouldTakeOverDriving(
				haveDriver: true,
				driverIsOnThisThread: false,
				candidateIsOnThisThread: false,
				driverHeartbeatIsStale: true)).IsFalse();

			await Assert.That(IdlePumpPolicy.ShouldTakeOverDriving(
				haveDriver: false,
				driverIsOnThisThread: false,
				candidateIsOnThisThread: false,
				driverHeartbeatIsStale: true)).IsFalse();
		}

		/// <summary>
		/// The threshold has to sit well clear of both neighbours: ordinary pump jitter must not trip it, and
		/// it must fire long before the automation harness gives up on a window at 15 seconds.
		/// </summary>
		[Test]
		public async Task StalenessIsMeasuredAgainstTheNamedThreshold()
		{
			await Assert.That(IdlePumpPolicy.HeartbeatIsStale(IdlePumpPolicy.StaleDriverMilliseconds)).IsFalse()
				.Because("exactly at the threshold is still answering");
			await Assert.That(IdlePumpPolicy.HeartbeatIsStale(IdlePumpPolicy.StaleDriverMilliseconds + 1)).IsTrue();

			// A healthy pump ticks every 10ms; a window that has not drained in this long has stopped.
			await Assert.That(IdlePumpPolicy.HeartbeatIsStale(10)).IsFalse()
				.Because("one pump interval is not a stall");
			await Assert.That(IdlePumpPolicy.StaleDriverMilliseconds).IsLessThan(15000)
				.Because("the driver must be replaced long before the close watchdog gives up at 15s");
		}
	}
}
