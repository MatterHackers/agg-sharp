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

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Waits out one <see cref="ProgressReporter.YieldThrottleMs"/> window, for a test that needs the next
	/// yield to actually happen rather than be swallowed as too soon after the last.
	/// </summary>
	/// <remarks>
	/// It waits on the throttle's OWN clock rather than sleeping the window plus a margin, because that clock
	/// is coarse. <see cref="ProgressReporter"/> measures with <see cref="Environment.TickCount64"/>, which on
	/// Windows is <c>GetTickCount64</c> and advances one system tick at a time (~15.6 ms). A sleep of "window
	/// plus a small margin" can therefore MEASURE as a tick less than it really was - a real 60 ms sleep reads
	/// as 46.875 ms whenever it starts and ends inside the wrong pair of ticks - and the yield after it is
	/// silently dropped, leaving a test that counts yields short of its expected count for no reason it can
	/// see. Any margin under one tick is a coin flip; measured against real <c>Task.Delay</c> timings, a 10 ms
	/// margin loses that flip about one time in ten. Re-reading the same clock until it agrees the window has
	/// passed is correct at any tick granularity.
	/// <para>
	/// One copy for the whole suite, so the rule is stated once rather than guessed at by each test that
	/// needs it.
	/// </para>
	/// </remarks>
	public static class ProgressThrottleWait
	{
		/// <summary>How long each re-check waits once the first sleep is done.</summary>
		private const int StepMs = 5;

		/// <summary>
		/// Returns once <see cref="Environment.TickCount64"/> - the clock the throttle reads - says a full
		/// throttle window has passed since this was called.
		/// </summary>
		public static async Task WaitOutTheWindowAsync()
		{
			long start = Environment.TickCount64;

			await Task.Delay((int)ProgressReporter.YieldThrottleMs);

			while (Environment.TickCount64 - start < ProgressReporter.YieldThrottleMs)
			{
				await Task.Delay(StepMs);
			}
		}
	}
}
