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
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A stand in for a platform host's idle pump: one thread that installs
	/// <see cref="MainLoopSynchronizationContext"/> and then calls <see cref="UiThread.InvokePendingActions"/>
	/// in a loop, exactly the way WinformsSystemWindow and AutomationRunner do.
	/// </summary>
	/// <remarks>
	/// Shared by every test that needs a real main loop to resume onto. It resets UiThread's process wide
	/// statics in both its constructor and Dispose, so any test class using it has to take the
	/// ShowWindowAndExecuteTests NotInParallel key - nothing else may be driving UiThread meanwhile.
	/// </remarks>
	internal sealed class UiThreadTestPump : IDisposable
	{
		private readonly Thread thread;
		private readonly ManualResetEventSlim ready = new ManualResetEventSlim(false);
		private volatile bool stopRequested;

		public UiThreadTestPump()
		{
			// Hand ourselves a clean queue and an unlatched ui thread id so the pump thread below becomes
			// UiThread's ui thread.
			UiThread.ResetForTests();

			thread = new Thread(() =>
			{
				MainLoopSynchronizationContext.InstallOnPumpThread();

				// Latches UiThread.IsUiThread onto this thread before anyone can observe it.
				UiThread.InvokePendingActions();

				ThreadId = Environment.CurrentManagedThreadId;
				ready.Set();

				while (!stopRequested)
				{
					UiThread.InvokePendingActions();
					Thread.Sleep(1);
				}

				UiThread.InvokePendingActions();
			})
			{
				IsBackground = true,
				Name = "UiThread test pump"
			};

			thread.Start();
			ready.Wait();
		}

		public int ThreadId { get; private set; }

		/// <summary>
		/// Runs <paramref name="work"/> on the pump thread under the installed context and completes when
		/// the whole async chain - including every continuation - has finished.
		/// </summary>
		public Task RunOnPump(Func<Task> work)
		{
			var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

			UiThread.RunOnIdle(async () =>
			{
				try
				{
					await work();
					completion.TrySetResult(true);
				}
				catch (Exception workException)
				{
					completion.TrySetException(workException);
				}
			});

			return completion.Task;
		}

		/// <summary>
		/// Stops the pump thread and clears UiThread's statics. Must be called from a thread other than the
		/// pump: disposing from queued work would have the pump wait for itself to exit.
		/// </summary>
		public void Dispose()
		{
			// Loudly, rather than the 10s Join timeout below: a test disposing its pump from inside pumped
			// work is a bug in the test, and the deadlock it would otherwise produce hides that.
			if (Thread.CurrentThread == thread)
			{
				throw new InvalidOperationException(
					"UiThreadTestPump.Dispose was called on the pump thread itself, which would wait for that thread to join itself. Dispose from the thread that constructed the pump.");
			}

			stopRequested = true;

			// Bounded: a pump thread that will not come back is a real failure, and an unbounded Join
			// would report it as a hung test run rather than as itself.
			bool stopped = thread.Join(TimeSpan.FromSeconds(10));

			// The pump thread is gone; leave no latched ui thread id or queued work behind.
			UiThread.ResetForTests();
			ready.Dispose();

			if (!stopped)
			{
				throw new TimeoutException(
					"The test pump thread did not stop within 10s - queued work is still blocking it.");
			}
		}
	}
}
