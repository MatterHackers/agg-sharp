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
	/// Builds a render device with a wall-clock budget, so a window that cannot get one fails instead of
	/// never opening.
	/// <para>
	/// <b>The stall this exists for.</b> Acquiring an adapter and opening a device are synchronous native
	/// calls - DXGI enumeration, then creating the D3D12 device. On a machine with a real GPU they are
	/// quick; on a loaded software rasterizer (WARP on GitHub's GPU-less Windows runners) one of them
	/// occasionally does not come back. The callback spin budgets inside the device only cover a callback
	/// that fails to arrive <em>after</em> a request returns; nothing inside the process can bound the
	/// request call itself. Because this runs from the window's Load, inside <c>Show()</c>, a stall there
	/// is a <c>Show()</c> that never returns: no window, no first paint, and every watcher of that window
	/// waiting on something that will never happen.
	/// </para>
	/// <para>
	/// <b>What over-budget costs.</b> The caller gets null and a window with no device: it will not paint,
	/// and it should say so. That is a bad outcome and a deliberate one - a window that reports a render
	/// failure can be closed, retried and diagnosed, while one still inside <c>Show()</c> can only be
	/// killed.
	/// </para>
	/// <para>
	/// <b>A device that arrives late is leaked, not disposed.</b> Same rule as
	/// <see cref="GpuTeardown"/>, and the same reason: releasing a device releases its swapchain, which
	/// talks to the native window, and by then nothing knows whether that window still exists. Leaking one
	/// device on a path that has already failed beats a release racing a window's destruction.
	/// </para>
	/// </summary>
	public static class GpuStartup
	{
		/// <summary>
		/// How long a window waits for its device before giving up. Generous next to a healthy acquisition
		/// (milliseconds with a real GPU, a second or two on a software rasterizer), and short enough to
		/// leave a report before the automation harness's own 30 second first-draw timeout turns the same
		/// failure into a silence.
		/// </summary>
		public static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(15);

		/// <summary>
		/// Runs <paramref name="create"/> with <see cref="DefaultBudget"/>, on a background thread wherever
		/// there is one, reporting to the console.
		/// </summary>
		/// <typeparam name="T">The device type being built.</typeparam>
		/// <param name="create">Builds the device. Must not touch UI-thread-affine state.</param>
		/// <param name="label">Names the device in the timeout message and the thread name.</param>
		/// <returns>The device, or null if it did not arrive inside the budget.</returns>
		public static T CreateWithinBudget<T>(Func<T> create, string label)
			where T : class
			=> CreateWithinBudget(create, label, DefaultBudget, !OperatingSystem.IsBrowser(), null);

		/// <summary>
		/// Runs <paramref name="create"/>, returning as soon as it finishes or the budget expires.
		/// </summary>
		/// <typeparam name="T">The device type being built.</typeparam>
		/// <param name="create">Builds the device. Must not touch UI-thread-affine state.</param>
		/// <param name="label">Names the device in the messages and the thread name.</param>
		/// <param name="budget">How long to wait before giving up on it.</param>
		/// <param name="backgroundThreadAvailable">
		/// False where the platform has no second thread to build on - the browser, which is single
		/// threaded and where <c>new Thread(...)</c> throws. The device is then built inline and this cannot
		/// time out, which costs nothing there: the stall this budget exists for is a native adapter or
		/// device request wedging on a software rasterizer, and the browser has no such request to wedge.
		/// Passed in rather than sniffed with <c>OperatingSystem.IsBrowser()</c> so a desktop test can
		/// exercise that leg.
		/// </param>
		/// <param name="report">
		/// Where the timeout diagnostics go; the console when null. Called from whichever thread notices, so
		/// an implementation must tolerate being called from the build thread after this has returned.
		/// </param>
		/// <returns>The device, or null if it did not arrive inside the budget.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="create"/> is null.</exception>
		public static T CreateWithinBudget<T>(
			Func<T> create,
			string label,
			TimeSpan budget,
			bool backgroundThreadAvailable,
			Action<string> report)
			where T : class
		{
			if (create == null)
			{
				throw new ArgumentNullException(nameof(create));
			}

			var reportTo = report ?? Console.WriteLine;

			// Two spellings of one fact, and both earn their place. The parameter is the seam a desktop
			// test drives to exercise this leg; OperatingSystem.IsBrowser() is what the platform
			// compatibility analyzer reads to prove the Thread.Start() below is unreachable on wasm - it
			// tracks platform checks, not a bool that arrived through a parameter.
			if (!backgroundThreadAvailable || OperatingSystem.IsBrowser())
			{
				return create();
			}

			// One gate over the "who owns the outcome" decision, exactly as GpuTeardown does: without it a
			// build that finishes just as the join times out is neither returned nor reported.
			var gate = new object();
			bool abandoned = false;
			T built = null;
			ExceptionDispatchInfo failure = null;

			var thread = new Thread(() =>
			{
				T result = null;
				Exception thrown = null;

				try
				{
					result = create();
				}
				catch (Exception exception)
				{
					thrown = exception;
				}

				lock (gate)
				{
					if (!abandoned)
					{
						built = result;
						failure = thrown == null ? null : ExceptionDispatchInfo.Capture(thrown);
						return;
					}
				}

				// Nobody is waiting any more. A device that arrives now is leaked on purpose (see the type's
				// remarks); a failure is reported here because there is nowhere left to throw it, and an
				// exception escaping this thread would take the process down.
				if (thrown != null)
				{
					reportTo($"GpuStartup: '{label}' failed after it was abandoned: {thrown}");
				}
				else if (result != null)
				{
					reportTo(
						$"GpuStartup: '{label}' arrived after it was abandoned and is being leaked rather than"
						+ " released - releasing it would put its swapchain teardown next to a window that may"
						+ " already be gone.");
				}
			})
			{
				IsBackground = true,
				Name = $"gpu-startup ({label})",
			};

			var elapsed = Stopwatch.StartNew();
			thread.Start();

			if (thread.Join(budget))
			{
				failure?.Throw();
				return built;
			}

			lock (gate)
			{
				abandoned = true;
			}

			reportTo(
				$"GpuStartup: '{label}' did not produce a device inside {budget.TotalSeconds:0.#}s"
				+ $" (waited {elapsed.Elapsed.TotalSeconds:0.#}s). The adapter or device request has not returned;"
				+ " this window will have no device and cannot paint.");

			return null;
		}
	}
}
