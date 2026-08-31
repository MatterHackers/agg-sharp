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
using System.Runtime.ExceptionServices;
using System.Threading;

namespace MatterHackers.RenderCore
{
	/// <summary>
	/// Pays a render device's "wait for the GPU to finish what it was given" with a wall-clock budget, so
	/// closing a window can never be held hostage by it.
	/// <para>
	/// <b>The wait this exists for.</b> Releasing a swapchain is not free: before it can unconfigure and
	/// release a surface, wgpu waits on the device fence for the last submitted work, with an effectively
	/// infinite timeout. On a machine with a real GPU that is microseconds and nobody notices. On a software
	/// rasterizer (WARP on GitHub's GPU-less Windows runners) one frame can take tens of seconds, so the
	/// same call parks the UI thread for minutes - inside <c>WM_CLOSE</c>, where it cannot even process the
	/// posted force-close that would otherwise save it.
	/// </para>
	/// <para>
	/// <b>Only the drain may be abandoned - never the releases.</b> This is the constraint the whole design
	/// turns on. Unconfiguring and releasing a surface are real calls against the native window it was made
	/// over: X requests on a display this process shares without <c>XInitThreads</c> (a torn protocol stream
	/// reaches Xlib's fatal handler, which aborts uncatchably), a DXGI swapchain over an HWND, a
	/// CAMetalLayer. Every host destroys that window the instant its close path returns - X11 calls
	/// <c>XDestroyWindow</c> on the line after the layer dispose, WinForms destroys the control's window
	/// inside <c>Control.Dispose</c>, mac releases the metal layer right after. A release still in flight on
	/// another thread would race that destruction. So the split is: the queue drain (a fence wait that
	/// touches no window - the wgpu device's <c>WaitForGpuIdle</c>) runs here under a budget, and the
	/// caller performs the releases itself, on its own thread, only if the drain came back in time.
	/// </para>
	/// <para>
	/// <b>What over-budget costs.</b> The caller releases nothing: no surface, no device, no queue, no
	/// instance. That is a real leak - those objects live until the process exits - and it is deliberately
	/// preferred over the alternatives, because it is the only outcome with no race in it. Nothing that
	/// touches the window is ever in flight, so the host can destroy its window immediately; and nothing is
	/// freed while the abandoned drain still holds it, so the drain cannot fault either. The abandoned
	/// thread sits in its one blocking poll of a device that will never be released, returns when the queue
	/// finally goes idle, and exits.
	/// </para>
	/// <para>
	/// <b>One residual, and the shape of its answer.</b> Draining the queue does not provably bound the
	/// release that follows: dx12's <c>destroy_swapchain</c> also waits on the presentation waitable, which
	/// the queue fence does not gate. There is no one-line mitigation for that under this design's own rule
	/// - a present wait is window-bound, so it may not be abandoned either. If it ever shows up on CI, the
	/// structural answer is to unconfigure the swapchain at close-<em>request</em> time, while the message
	/// pump is still running and can absorb the wait, rather than inside <c>WM_CLOSE</c> where nothing can.
	/// </para>
	/// <para>
	/// <b>A dedicated thread, not the thread pool.</b> The whole point of the budget is that the work may
	/// block for minutes. A pool thread doing that is a pool thread the rest of the application does not
	/// have, and the pool only grows one thread per (roughly) half second to replace it.
	/// </para>
	/// </summary>
	public static class GpuTeardown
	{
		/// <summary>
		/// How long a close waits for the GPU to drain before walking away. Long enough that a healthy
		/// device (microseconds) and even a badly loaded one always finish inside it, short enough to stay
		/// well under the watchdogs that photograph a "hung" UI.
		/// </summary>
		public static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(5);

		/// <summary>
		/// Runs <paramref name="drain"/> with <see cref="DefaultBudget"/>, on a background thread wherever
		/// there is one, reporting to the console.
		/// </summary>
		/// <param name="drain">
		/// The GPU wait - and only the wait. Anything that touches the native window belongs on the
		/// caller's thread, after this returns true.
		/// </param>
		/// <param name="label">Names the device in the messages and the thread name.</param>
		/// <returns>True if the GPU went idle inside the budget, so the caller may release.</returns>
		public static bool DrainWithinBudget(Action drain, string label)
			=> DrainWithinBudget(drain, label, DefaultBudget, !OperatingSystem.IsBrowser(), null);

		/// <summary>
		/// Runs <paramref name="drain"/>, returning as soon as it finishes or the budget expires - whichever
		/// comes first.
		/// </summary>
		/// <param name="drain">
		/// The GPU wait - and only the wait; see the type's remarks for why releasing here would be a bug.
		/// </param>
		/// <param name="label">Names the device in the messages and the thread name.</param>
		/// <param name="budget">How long to wait before abandoning the drain.</param>
		/// <param name="backgroundThreadAvailable">
		/// False where the platform has no second thread to detach to - the browser, whose wasm build is
		/// single threaded and where <c>new Thread(...)</c> throws. The drain then runs inline and this
		/// returns true, because there is nothing to bound it with (and nothing to bound: the browser's
		/// device poll is a stub). Passed in rather than sniffed with <c>OperatingSystem.IsBrowser()</c> so
		/// a desktop test can exercise that leg.
		/// </param>
		/// <param name="report">
		/// Where the diagnostics go; the console when null. A caller that has somewhere better to put them
		/// (a smoke log, a test) passes its own. Called from whichever thread notices, so an implementation
		/// must tolerate being called from the drain thread after this method has returned.
		/// </param>
		/// <returns>True if the GPU went idle inside the budget (or the drain ran inline).</returns>
		/// <exception cref="ArgumentNullException"><paramref name="drain"/> is null.</exception>
		public static bool DrainWithinBudget(
			Action drain,
			string label,
			TimeSpan budget,
			bool backgroundThreadAvailable,
			Action<string> report)
		{
			if (drain == null)
			{
				throw new ArgumentNullException(nameof(drain));
			}

			var reportTo = report ?? Console.WriteLine;

			// Two spellings of one fact, and both earn their place. The parameter is the seam a desktop
			// test drives to exercise this leg; OperatingSystem.IsBrowser() is what the platform
			// compatibility analyzer reads to prove the Thread.Start() below is unreachable on wasm - it
			// tracks platform checks, not a bool that arrived through a parameter.
			if (!backgroundThreadAvailable || OperatingSystem.IsBrowser())
			{
				drain();
				return true;
			}

			// One gate over the whole "who reports the failure" decision. Without it the two threads can
			// both decide the other one will: the drain throws just as the join times out, the caller has
			// already stopped looking, and the exception disappears.
			var gate = new object();
			bool abandoned = false;
			ExceptionDispatchInfo failure = null;

			var thread = new Thread(() =>
			{
				try
				{
					drain();
				}
				catch (Exception exception)
				{
					// An exception on a thread nobody joins takes the process down with it, so it is never
					// allowed to escape: it is either handed back to the caller below, or - if the caller
					// has already given up on this drain - reported from here, which is the only place left
					// that knows about it.
					lock (gate)
					{
						if (!abandoned)
						{
							failure = ExceptionDispatchInfo.Capture(exception);
							return;
						}
					}

					reportTo($"GpuTeardown: the abandoned drain of '{label}' then failed: {exception}");
				}
			})
			{
				IsBackground = true,
				Name = $"gpu-drain ({label})",
			};

			var elapsed = Stopwatch.StartNew();
			thread.Start();

			if (thread.Join(budget))
			{
				failure?.Throw();
				return true;
			}

			ExceptionDispatchInfo lateFailure;
			lock (gate)
			{
				abandoned = true;

				// A failure captured in the moment between the join timing out and this lock is nobody's
				// otherwise: the drain thread has already decided to hand it back.
				lateFailure = failure;
			}

			reportTo(
				$"GpuTeardown: '{label}' did not go idle inside {budget.TotalSeconds:0.#}s and was abandoned"
				+ $" (waited {elapsed.Elapsed.TotalSeconds:0.#}s). Nothing is being released, so the device and"
				+ " its swapchain leak until the process exits - the alternative is releasing them while the"
				+ " window they draw to is destroyed.");

			if (lateFailure != null)
			{
				reportTo($"GpuTeardown: the abandoned drain of '{label}' had already failed: {lateFailure.SourceException}");
			}

			return false;
		}
	}
}
