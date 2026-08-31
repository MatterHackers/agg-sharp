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
using System.Diagnostics;
using System.Threading;

namespace MatterHackers.RenderCore
{
	/// <summary>
	/// Waits for a native graphics callback by pumping for it, with a wall-clock budget - so a driver that
	/// stops answering produces a report instead of a thread that never comes back.
	/// <para>
	/// <b>Why a count of spins is not a bound.</b> The loops this replaces gave up after a fixed number of
	/// iterations, which measures nothing: with a pump that returns immediately, a thousand iterations is
	/// under a millisecond, so a merely slow driver (a software rasterizer under CI load) is called dead;
	/// with a pump that blocks, one iteration can be forever, so the count never gets read at all. That
	/// second case is not hypothetical - a screenshot readback on a GPU-less Windows runner wedged the UI
	/// thread of a whole test process inside the first blocking poll, and the spin budget standing right
	/// beside it never got a turn. Time is what the caller actually has to spend, so time is the bound.
	/// </para>
	/// <para>
	/// <b>The pump must not block.</b> A budget can only be honoured between pumps, so a pump that can hang
	/// defeats it exactly when it matters. Where a blocking native wait genuinely cannot be avoided, the
	/// answer is not this type but <see cref="GpuStartup"/>'s: run it on a thread that can be abandoned.
	/// </para>
	/// <para>
	/// <b>Spinning, then resting.</b> Nearly every one of these callbacks lands in the first pump or two, so
	/// the loop spins hot at first - a rest there would add its own latency to every readback. Once it is
	/// clear the answer is not immediate, resting between pumps is what keeps a long wait from pinning a
	/// core, which on a single-core CI runner is the difference between waiting for the GPU and starving it.
	/// </para>
	/// </summary>
	public static class GpuCallbackPump
	{
		/// <summary>
		/// How long to pump for a callback before calling the driver unresponsive. Generous next to a healthy
		/// answer (milliseconds) and short enough that a caller which cannot paint until it returns is not
		/// mistaken for a hang.
		/// </summary>
		/// <remarks>
		/// Seven rather than ten seconds so that two of these fit inside <see cref="GpuStartup.DefaultBudget"/>
		/// with a second to spare: device acquisition is an adapter request followed by a device request, both
		/// bounded by this, both inside that one. A slow adapter followed by a device request that never
		/// answers would otherwise blow the outer budget first, and the caller would be told the generic "no
		/// device inside 15s" instead of the precise "wgpuAdapterRequestDevice never called back". The inner
		/// message names which half stopped answering, so the inner budget has to be the one that fires.
		/// <para>
		/// A call site with no outer budget over it is free to pass its own - see the readback in
		/// <c>WebGpuRenderDevice.MapAndCopy</c>, whose ceiling is how slow a legitimate full-window copy can
		/// be on a software rasterizer rather than anyone else's deadline.
		/// </para>
		/// </remarks>
		public static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(7);

		/// <summary>
		/// How many pumps run back to back before the loop starts resting between them.
		/// </summary>
		/// <remarks>
		/// Tens, not thousands. Measured on Metal, a 512x384 texture read-back takes about 1000 pumps and 5
		/// to 9 ms to resolve - the wait is the GPU finishing, not the pumping, so nearly all of those calls
		/// were asking a driver that was still working. Each one is two FFI calls taking wgpu's device lock,
		/// which is the same lock the submission being waited on needs, so spinning through them competes
		/// with the very work it is waiting for. A short hot phase still collects an answer that is already
		/// queued without ever sleeping; past that, resting is both kinder and no slower, because the wall
		/// clock belongs to the GPU either way.
		/// </remarks>
		public const int DefaultHotSpins = 64;

		/// <summary>
		/// The first rest, and the step the backoff doubles from.
		/// </summary>
		private static readonly TimeSpan FirstRest = TimeSpan.FromMilliseconds(1);

		/// <summary>
		/// The longest a rest gets. A wait that has already lasted this long is a driver in trouble, and
		/// checking on it a hundred times a second is plenty.
		/// </summary>
		private static readonly TimeSpan LongestRest = TimeSpan.FromMilliseconds(10);

		/// <summary>
		/// Pumps until the callback has answered or <see cref="DefaultBudget"/> has passed, timing with a
		/// <see cref="Stopwatch"/> and resting with <see cref="Thread.Sleep(TimeSpan)"/>.
		/// </summary>
		/// <param name="answered">Whether the callback has landed. Read after every pump.</param>
		/// <param name="pump">Gives the driver a chance to deliver callbacks. Must not block.</param>
		/// <returns>True if it answered; false if the budget ran out first.</returns>
		public static bool UntilAnswered(Func<bool> answered, Action pump)
			=> UntilAnswered(answered, pump, DefaultBudget);

		/// <summary>
		/// Pumps until the callback has answered or <paramref name="budget"/> has passed, timing with a
		/// <see cref="Stopwatch"/> and resting with <see cref="Thread.Sleep(TimeSpan)"/>.
		/// </summary>
		/// <param name="answered">Whether the callback has landed. Read after every pump.</param>
		/// <param name="pump">Gives the driver a chance to deliver callbacks. Must not block.</param>
		/// <param name="budget">How long to pump before giving up.</param>
		/// <returns>True if it answered; false if the budget ran out first.</returns>
		public static bool UntilAnswered(Func<bool> answered, Action pump, TimeSpan budget)
		{
			var elapsed = Stopwatch.StartNew();

			// Backing off rather than resting a fixed slice: the first rests are the ones a normal wait pays,
			// so they stay short, while a wait that is going to end in a timeout spends its last seconds
			// asleep instead of hammering a driver that has already stopped answering.
			var rest = FirstRest;

			return UntilAnswered(
				answered,
				pump,
				budget,
				DefaultHotSpins,
				() => elapsed.Elapsed,
				() =>
				{
					Thread.Sleep(rest);

					var doubled = rest + rest;
					rest = doubled < LongestRest ? doubled : LongestRest;
				});
		}

		/// <summary>
		/// Pumps until the callback has answered or the budget has passed, with the clock and the rest handed
		/// in.
		/// </summary>
		/// <param name="answered">Whether the callback has landed. Read after every pump.</param>
		/// <param name="pump">Gives the driver a chance to deliver callbacks. Must not block.</param>
		/// <param name="budget">How long to pump before giving up.</param>
		/// <param name="hotSpins">How many pumps run back to back before <paramref name="rest"/> is called.</param>
		/// <param name="elapsed">
		/// How long this wait has been running. A parameter rather than a <see cref="Stopwatch"/> read inside
		/// the loop so a test can prove the deadline is honoured without spending it - the same reason
		/// <see cref="GpuStartup.CreateWithinBudget{T}(Func{T}, string, TimeSpan, bool, Action{string})"/>
		/// takes its platform as a parameter instead of sniffing for it.
		/// </param>
		/// <param name="rest">Yields the core between pumps once the hot spins are used up.</param>
		/// <returns>True if it answered; false if the budget ran out first.</returns>
		/// <remarks>
		/// Always pumps at least once, whatever the budget: a callback that is already queued costs one pump
		/// to collect, and a zero budget should still collect it rather than declare a healthy driver dead.
		/// </remarks>
		/// <exception cref="ArgumentNullException">Any delegate is null.</exception>
		public static bool UntilAnswered(
			Func<bool> answered,
			Action pump,
			TimeSpan budget,
			int hotSpins,
			Func<TimeSpan> elapsed,
			Action rest)
		{
			if (answered == null)
			{
				throw new ArgumentNullException(nameof(answered));
			}

			if (pump == null)
			{
				throw new ArgumentNullException(nameof(pump));
			}

			if (elapsed == null)
			{
				throw new ArgumentNullException(nameof(elapsed));
			}

			if (rest == null)
			{
				throw new ArgumentNullException(nameof(rest));
			}

			int spins = 0;

			while (true)
			{
				pump();

				if (answered())
				{
					return true;
				}

				if (elapsed() >= budget)
				{
					return false;
				}

				if (++spins >= hotSpins)
				{
					rest();
				}
			}
		}
	}
}
