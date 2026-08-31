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

using System;
using System.Threading.Tasks;
using MatterHackers.RenderCore;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.RenderCore
{
	/// <summary>
	/// The wall-clock budget around a native graphics callback.
	/// </summary>
	/// <remarks>
	/// No GPU here, and none needed: the pump, the clock and the rest are all parameters, so a driver that
	/// answers late, one that never answers at all and one that is merely slow can each be built out of
	/// delegates and the deadline proved without spending it.
	/// </remarks>
	public class GpuCallbackPumpTests
	{
		/// <summary>A clock that only moves when the wait pumps, so a test never waits in real time.</summary>
		private sealed class PumpClock
		{
			private readonly TimeSpan perPump;

			public PumpClock(TimeSpan perPump) => this.perPump = perPump;

			public int Pumps { get; private set; }

			public int Rests { get; private set; }

			public void Pump() => this.Pumps++;

			public void Rest() => this.Rests++;

			public TimeSpan Elapsed => this.perPump * this.Pumps;
		}

		[Test]
		public async Task ACallbackAlreadyWaitingIsCollectedByTheFirstPump()
		{
			var clock = new PumpClock(TimeSpan.FromMilliseconds(1));

			bool answered = GpuCallbackPump.UntilAnswered(
				() => clock.Pumps >= 1,
				clock.Pump,
				TimeSpan.FromSeconds(10),
				hotSpins: 4,
				() => clock.Elapsed,
				clock.Rest);

			await Assert.That(answered).IsTrue();
			await Assert.That(clock.Pumps).IsEqualTo(1);
			await Assert.That(clock.Rests).IsEqualTo(0)
				.Because("an answer that is already there must not cost a sleep");
		}

		/// <summary>
		/// The failure this whole type exists for: the driver stops answering, and the caller gets its thread
		/// back with a false instead of pumping forever. On the UI thread that difference is a window that can
		/// still close against one that cannot.
		/// </summary>
		[Test]
		public async Task ACallbackThatNeverArrivesGivesUpAtTheDeadline()
		{
			var clock = new PumpClock(TimeSpan.FromMilliseconds(10));

			bool answered = GpuCallbackPump.UntilAnswered(
				() => false,
				clock.Pump,
				TimeSpan.FromMilliseconds(100),
				hotSpins: 4,
				() => clock.Elapsed,
				clock.Rest);

			await Assert.That(answered).IsFalse();
			await Assert.That(clock.Pumps).IsEqualTo(10)
				.Because("the wait ends on the clock - ten pumps of ten milliseconds is the hundred it was given");
		}

		/// <summary>
		/// The bug in what this replaced: a fixed number of iterations is not a measure of anything. A driver
		/// that is slow rather than dead - a software rasterizer reading back a full window under CI load -
		/// has to be waited for, however many pumps that takes.
		/// </summary>
		[Test]
		public async Task ASlowDriverIsWaitedForHoweverManyPumpsItTakes()
		{
			var clock = new PumpClock(TimeSpan.FromMilliseconds(1));
			const int pumpsBeforeTheAnswer = 5000;

			bool answered = GpuCallbackPump.UntilAnswered(
				() => clock.Pumps >= pumpsBeforeTheAnswer,
				clock.Pump,
				TimeSpan.FromSeconds(10),
				hotSpins: GpuCallbackPump.DefaultHotSpins,
				() => clock.Elapsed,
				clock.Rest);

			await Assert.That(answered).IsTrue()
				.Because("five thousand pumps inside the budget is a slow answer, not a missing one - the old "
					+ "thousand-spin bound would have called this driver dead");
			await Assert.That(clock.Pumps).IsEqualTo(pumpsBeforeTheAnswer);
		}

		/// <summary>
		/// Hot at first, resting after - so a callback that lands immediately pays no latency, and one that
		/// takes longer does not spend that time pinning a core the driver may need.
		/// </summary>
		/// <remarks>
		/// Runs on the shipped <see cref="GpuCallbackPump.DefaultHotSpins"/> rather than a convenient small
		/// one, because the size of that number is the point of it. Measured on Metal, a texture read-back
		/// takes about a thousand pumps to resolve: every pump past the hot phase is two FFI calls taking the
		/// same device lock the submission being waited on needs, so a hot phase of tens keeps the answer
		/// that is already there free while handing a driver that is still working its lock back.
		/// </remarks>
		[Test]
		public async Task TheLoopSpinsHotBeforeItStartsResting()
		{
			var clock = new PumpClock(TimeSpan.FromMilliseconds(1));
			const int restsExpected = 6;
			int pumpsBeforeTheAnswer = GpuCallbackPump.DefaultHotSpins + restsExpected;

			GpuCallbackPump.UntilAnswered(
				() => clock.Pumps >= pumpsBeforeTheAnswer,
				clock.Pump,
				TimeSpan.FromSeconds(10),
				GpuCallbackPump.DefaultHotSpins,
				() => clock.Elapsed,
				clock.Rest);

			await Assert.That(GpuCallbackPump.DefaultHotSpins).IsLessThan(100)
				.Because("the hot phase is meant to catch an answer that has already landed, not to be the wait");
			await Assert.That(clock.Rests).IsEqualTo(restsExpected)
				.Because("the hot spins run back to back and every pump after them rests");
		}

		[Test]
		public async Task AZeroBudgetStillCollectsAnAnswerThatIsAlreadyThere()
		{
			var clock = new PumpClock(TimeSpan.FromMilliseconds(1));

			bool answered = GpuCallbackPump.UntilAnswered(
				() => clock.Pumps >= 1,
				clock.Pump,
				TimeSpan.Zero,
				hotSpins: 4,
				() => clock.Elapsed,
				clock.Rest);

			await Assert.That(answered).IsTrue()
				.Because("the deadline is read after the pump, so a queued callback is never thrown away unread");
		}
	}
}
