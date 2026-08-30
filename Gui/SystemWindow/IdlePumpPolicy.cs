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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Which window should drive the shared idle pump.
	/// </summary>
	/// <remarks>
	/// <para>
	/// One timer serves the whole process, and it does not run on a UI thread - it ticks on a pool thread
	/// and marshals the idle work to <em>one</em> window, the driver. Everything that reaches the UI thread
	/// between input events goes that way: queued <c>RunOnIdle</c> actions, agg's deferred invalidates, and
	/// the force-close an automation harness posts when a test is over. So the driver is not a detail - it
	/// decides which message loop is alive.
	/// </para>
	/// <para>
	/// <b>The failure this policy exists to stop.</b> A driver that is undisposed and has a handle can still
	/// be the wrong one: if its window belongs to a thread that is no longer pumping, every tick marshals
	/// into a queue nobody is draining, while the thread that <em>is</em> pumping sits in <c>WaitMessage</c>
	/// with nothing to wake it. It never repaints, never runs queued work, and never processes the posted
	/// close - the window then "fails to close" and the test's own interactions (focus, tooltips, click
	/// routing - everything that needs an idle turn) quietly stop working too. Seven Windows CI failures in
	/// one family, all with the same idle UI-thread stack, came from exactly this.
	/// </para>
	/// <para>
	/// <b>Taking the pump is not free, which is why it needs evidence.</b> The driver drains the
	/// process-wide <c>UiThread</c> queue on its own thread, so moving the pump moves whose thread that work
	/// runs on - steal it from a thread that is genuinely pumping and you have both starved a live window
	/// and started running its queued widget code on someone else's thread. So a driver on another thread is
	/// not assumed dead: it is given a heartbeat, stamped every time an idle drain actually reaches a UI
	/// thread, and only a stale one is replaced. A thread that has stopped pumping goes maximally stale
	/// within a few ticks and loses the pump; a busy but living one keeps it.
	/// </para>
	/// <para>
	/// The facts are passed in rather than read from a <c>Control</c> so the rule can be tested on any
	/// platform; the WinForms host answers them with <c>IsDisposed</c>, <c>IsHandleCreated</c>,
	/// <c>InvokeRequired</c> and the heartbeat it stamps on its own idle drain.
	/// </para>
	/// </remarks>
	public static class IdlePumpPolicy
	{
		/// <summary>
		/// Describes the window currently driving the idle pump - which thread owns it, and whether it is
		/// still in a state to marshal anything - or null where no host has published one.
		/// </summary>
		/// <remarks>
		/// A hook rather than a property because the driver is a platform object (a WinForms Form) that the
		/// cross-platform half of this library, and the automation harness that needs to report it, cannot
		/// name. The host sets this once; a watchdog calls it when a window will not close, which is exactly
		/// when "who was supposed to be waking that thread?" is the question worth answering.
		/// </remarks>
		public static Func<string> DescribeDriver { get; set; }

		/// <summary>
		/// How long an idle drain may go unseen before the driver counts as stale. Two orders of magnitude
		/// above a healthy pump interval (the timer ticks every 10ms), so ordinary jitter - a slow frame, a
		/// long queued action, a loaded CI box - never trips it; and thirty times under the automation
		/// harness's 15 second close watchdog, so a driver that really has stopped is replaced long before
		/// anything gives up on the window.
		/// </summary>
		public const long StaleDriverMilliseconds = 500;

		/// <summary>
		/// Whether a driver that last drained <paramref name="millisecondsSinceLastDrain"/> ago has stopped
		/// answering. A driver that has never drained at all is stale by the same measure - the host stamps
		/// the heartbeat when it hands the pump over, so a new driver gets one full window to prove itself.
		/// </summary>
		/// <param name="millisecondsSinceLastDrain">Elapsed time since an idle drain last reached a UI thread.</param>
		public static bool HeartbeatIsStale(long millisecondsSinceLastDrain)
			=> millisecondsSinceLastDrain > StaleDriverMilliseconds;

		/// <summary>
		/// Whether a window on the calling thread should take the idle pump over from the current driver.
		/// </summary>
		/// <param name="haveDriver">True if a driver is currently set and still usable (alive, with a handle).</param>
		/// <param name="driverIsOnThisThread">
		/// True if the current driver's window belongs to the thread asking. False means its ticks are being
		/// marshaled to a thread this one cannot benefit from - fine while that thread is really pumping,
		/// which is what <paramref name="driverHeartbeatIsStale"/> answers.
		/// </param>
		/// <param name="candidateIsOnThisThread">True if the window offering to drive belongs to the thread asking.</param>
		/// <param name="driverHeartbeatIsStale">
		/// True if no idle drain has reached a UI thread within <see cref="StaleDriverMilliseconds"/>; see
		/// <see cref="HeartbeatIsStale"/>. Meaningless when there is no driver.
		/// </param>
		/// <returns>True if the candidate should become the driver.</returns>
		public static bool ShouldTakeOverDriving(
			bool haveDriver,
			bool driverIsOnThisThread,
			bool candidateIsOnThisThread,
			bool driverHeartbeatIsStale)
		{
			if (!candidateIsOnThisThread)
			{
				// A window that cannot serve this thread is no improvement on one that cannot either.
				return false;
			}

			if (!haveDriver)
			{
				return true;
			}

			// A driver on the asking thread is serving it by definition. One on another thread is only worth
			// displacing when it has stopped delivering: taking the pump from a thread that is still pumping
			// starves that thread and moves its queued work onto this one.
			return !driverIsOnThisThread && driverHeartbeatIsStale;
		}
	}
}
