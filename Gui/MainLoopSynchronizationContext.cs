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

using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Makes <c>await</c> resume on the application's main loop by default, the way it already does in a
	/// browser. Installed on the thread that pumps <see cref="UiThread.InvokePendingActions"/>, so a
	/// continuation captured anywhere on the UI thread comes back to the UI thread without the caller
	/// having to marshal it by hand with <see cref="UiThread.RunOnIdle(Action)"/>.
	/// </summary>
	/// <remarks>
	/// <para>There is exactly one queue and one pump: <see cref="Post"/> enqueues through
	/// <see cref="UiThread.RunOnIdle(Action)"/>, so posted continuations and hand written RunOnIdle work
	/// share a single FIFO. A continuation posted while the pump is running the current batch executes on
	/// the NEXT pump, never re-entrantly inside the current one.</para>
	/// <para>That costs latency: a resumption waits for the next pump - up to one idle tick, about 10ms on
	/// Windows Forms - so an N deep chain of genuinely suspending awaits can take up to N ticks to unwind.
	/// Predictable ordering is worth that, but work that cannot afford it should not be hopping the loop
	/// once per await.</para>
	/// <para>On WebAssembly nothing installs this - Blazor supplies its own single threaded dispatcher and
	/// that is already the model this context imitates.</para>
	/// <para><see cref="SynchronizationContext.OperationStarted"/> and
	/// <see cref="SynchronizationContext.OperationCompleted"/> are deliberately not overridden. They exist
	/// so a context can keep an async void operation alive against a loop that would otherwise exit; the
	/// platform hosts own their message loops' lifetime, so the base no-ops are correct here (Blazor's
	/// dispatcher takes the same position).</para>
	/// </remarks>
	public sealed class MainLoopSynchronizationContext : SynchronizationContext
	{
		private MainLoopSynchronizationContext()
		{
		}

		/// <summary>
		/// The single instance. There is only one main loop, so there is only ever one context - and
		/// <see cref="CreateCopy"/> hands back this same object rather than a clone.
		/// </summary>
		public static MainLoopSynchronizationContext Instance { get; } = new MainLoopSynchronizationContext();

		/// <summary>
		/// How long <see cref="Send"/> from a thread other than the main loop will wait for the pump before
		/// giving up. Bounded on purpose: a dead or blocked pump must fail loudly rather than park the
		/// calling thread forever.
		/// </summary>
		public static TimeSpan SendFromOtherThreadTimeout { get; set; } = TimeSpan.FromSeconds(10);

		/// <summary>
		/// Raised (on the calling thread) every time <see cref="Send"/> is used from off the main loop.
		/// That shape is legacy - it blocks a thread on the UI - so it is reported rather than hidden.
		/// </summary>
		public static event Action<string> BlockingSendObserved;

		/// <summary>
		/// Installs this context on the calling thread if it is not already installed. Called by each
		/// platform host from the thread that pumps the idle queue; cheap and idempotent, so it can sit
		/// directly on the pump path rather than needing a separate one-time startup hook.
		/// </summary>
		public static void InstallOnPumpThread()
		{
			if (Current is MainLoopSynchronizationContext)
			{
				return;
			}

			SetSynchronizationContext(Instance);
		}

		/// <summary>
		/// Installs the context for the duration of the returned scope and restores whatever was current
		/// when the scope is disposed. For hosts that BORROW a thread rather than own it for the life of
		/// the process - the test harness borrows a runner thread per test - because leaving the context
		/// latched on a borrowed thread would route later, unrelated awaits into a queue nobody pumps.
		/// </summary>
		public static IDisposable InstallForScope()
		{
			var previous = Current;
			SetSynchronizationContext(Instance);

			return new InstallScope(previous);
		}

		private sealed class InstallScope : IDisposable
		{
			private readonly SynchronizationContext previous;

			internal InstallScope(SynchronizationContext previous)
			{
				this.previous = previous;
			}

			public void Dispose()
			{
				SetSynchronizationContext(previous);
			}
		}

		/// <summary>
		/// Queues work for the next pump of the main loop. Never runs inline, even when called from the
		/// main loop thread - that is what makes await continuations serialize behind whatever the loop is
		/// already doing instead of re-entering it.
		/// </summary>
		public override void Post(SendOrPostCallback d, object state)
		{
			// Checked here rather than left to blow up on the pump: the BCL contract is an
			// ArgumentNullException, and a NullReferenceException raised one tick later on the main loop
			// names neither the caller nor the mistake.
			if (d == null)
			{
				throw new ArgumentNullException(nameof(d));
			}

			UiThread.RunOnIdle(() => d(state));
		}

		/// <summary>
		/// Runs work on the main loop and waits for it. Inline when already on the main loop; from any
		/// other thread this queues and blocks, bounded by <see cref="SendFromOtherThreadTimeout"/>.
		/// </summary>
		/// <remarks>
		/// A Send that times out does NOT run its work afterwards. The queued item stays in the queue - it
		/// cannot be pulled back out - but it checks on the way in whether the caller has already given up,
		/// and if so does nothing. Otherwise a caller that timed out and retried, or fell back to another
		/// path, would have the abandoned work applied a second time whenever the loop recovered. Work that
		/// had already passed that check when the wait gave up still runs to completion - the check closes
		/// the window that lasts as long as the timeout, not the instant at the end of it.
		/// </remarks>
		public override void Send(SendOrPostCallback d, object state)
		{
			if (d == null)
			{
				throw new ArgumentNullException(nameof(d));
			}

			if (UiThread.IsUiThread)
			{
				d(state);
				return;
			}

			ReportBlockingSend();

			ExceptionDispatchInfo failure = null;
			var completed = new ManualResetEventSlim(false);

			// Volatile because it is written by this thread and read by the pump thread. Set before the
			// TimeoutException below is thrown, so the throw and the abandonment are one decision.
			bool abandoned = false;

			UiThread.RunOnIdle(() =>
			{
				try
				{
					if (Volatile.Read(ref abandoned))
					{
						return;
					}

					d(state);
				}
				catch (Exception sentWorkException)
				{
					if (Volatile.Read(ref abandoned))
					{
						// The caller is long gone and its `failure` will never be read, so reporting it
						// through the field would swallow the exception entirely. This is the same channel
						// any other throw out of a queued action takes.
						UiThread.ReportUnhandledException(sentWorkException);
					}
					else
					{
						failure = ExceptionDispatchInfo.Capture(sentWorkException);
					}
				}
				finally
				{
					completed.Set();
				}
			});

			if (!completed.Wait(SendFromOtherThreadTimeout))
			{
				Volatile.Write(ref abandoned, true);

				// Deliberately not disposed: the work is still queued and will call Set on the pump thread
				// if the loop ever recovers. Disposing here would turn that into an ObjectDisposedException
				// on the UI thread, which is a far worse failure than leaking one event.
				throw new TimeoutException(
					$"{nameof(MainLoopSynchronizationContext)}.{nameof(Send)} waited {SendFromOtherThreadTimeout.TotalSeconds:0.##}s"
					+ " for the main loop to pump and gave up. The main loop is blocked, or has not started, or has already exited.");
			}

			completed.Dispose();

			failure?.Throw();
		}

		/// <inheritdoc/>
		public override SynchronizationContext CreateCopy()
		{
			return this;
		}

		private static void ReportBlockingSend()
		{
			var thread = Thread.CurrentThread;
			var message = $"{nameof(MainLoopSynchronizationContext)}.{nameof(Send)} was called from thread"
				+ $" {thread.ManagedThreadId} ('{thread.Name ?? "unnamed"}'), which is not the main loop."
				+ " This is a legacy shaped blocking marshal: the calling thread is parked until the main loop"
				+ " pumps, and it deadlocks outright if the main loop is waiting on this thread. Await work that"
				+ " resumes on the main loop instead.";

			Console.Error.WriteLine(message);

			try
			{
				BlockingSendObserved?.Invoke(message);
			}
			catch
			{
				// A diagnostic listener must never change the outcome of the Send it is reporting on.
			}
		}
	}
}
