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
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The process main thread, offered as a place other threads can run work.
	/// </summary>
	/// <remarks>
	/// <para>
	/// agg's window model is "the first window owns the loop": <c>ShowAsSystemWindow</c> does not return
	/// until the application is done, because the platform host runs its message pump inside that call
	/// (<c>Application.Run</c> on Windows, <c>MacSystemWindow.RunEventLoop</c> on macOS). A plain
	/// <c>Main</c> satisfies that by simply being the thread that calls Show, and every demo relies on it.
	/// </para>
	/// <para>
	/// A <em>test</em> process cannot: the test engine owns <c>Main</c> and runs test bodies on thread pool
	/// workers, so the thread that calls Show is whatever worker the engine picked. On Windows that is
	/// harmless - any thread may own a message pump - but AppKit permits window creation and essentially
	/// all UI work only on the process main thread, and violating it is not a failure but an
	/// <c>NSInternalInconsistencyException</c> that aborts the process, taking the whole run with it.
	/// </para>
	/// <para>
	/// So the main thread is claimed explicitly rather than implicitly. <see cref="RunHosted"/> is called
	/// from <c>Main</c>, starts the real work (the test engine, or an application) elsewhere, and keeps the
	/// main thread servicing this queue; a platform host that needs the main thread reaches it through
	/// <see cref="Invoke(Action)"/>. When nothing has claimed the main thread - every demo, and every
	/// non-macOS host - <see cref="Invoke(Action)"/> runs its work inline on the calling thread, which is
	/// exactly what happened before this type existed.
	/// </para>
	/// </remarks>
	public static class MainThreadDispatcher
	{
		/// <summary>
		/// How long the main thread parks between drains when nothing has signalled. Only a backstop -
		/// every enqueue sets <see cref="workSignal"/> - so it costs a wake per interval and no latency.
		/// </summary>
		private const int IdleWaitMilliseconds = 10;

		private static readonly object QueueLock = new object();

		private static readonly Queue<WorkItem> Pending = new Queue<WorkItem>();

		/// <summary>Set by every enqueue so the main thread's wait returns the moment there is work.</summary>
		private static readonly ManualResetEventSlim workSignal = new ManualResetEventSlim(false);

		private static int mainThreadId = -1;

		private static volatile bool hosted;

		private static volatile bool hostExited;

		/// <summary>
		/// Whether this OS insists UI work happen on the process main thread, and so whether
		/// <see cref="RunHosted"/> actually reserves it.
		/// </summary>
		/// <remarks>
		/// True only on macOS. Win32 lets any thread own a message pump, and X11 and Wayland have no
		/// equivalent rule either, so on those hosts reserving the main thread would buy nothing and change
		/// the threading of every process that has an entry point - including the Windows test run, which
		/// has always created its windows on thread pool workers and been fine. Settable so a host that
		/// knows better can say so before <see cref="RunHosted"/> is called.
		/// </remarks>
		public static bool MainThreadRequired { get; set; }
			= System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);

		/// <summary>
		/// True while <see cref="RunHosted"/> owns the main thread. False in an ordinary application, where
		/// the thread that calls <c>ShowAsSystemWindow</c> is already the main thread.
		/// </summary>
		public static bool IsHosted => hosted;

		/// <summary>
		/// True on the thread <see cref="RunHosted"/> was called from. Always false when nothing is hosted,
		/// because nothing has said which thread that would be.
		/// </summary>
		public static bool IsMainThread => hosted && Environment.CurrentManagedThreadId == Volatile.Read(ref mainThreadId);

		/// <summary>
		/// Gives the calling thread - which must be the process main thread - over to UI work, and runs
		/// <paramref name="body"/> elsewhere until it completes.
		/// </summary>
		/// <remarks>
		/// Where <see cref="MainThreadRequired"/> is false this is only a synchronous wrapper around an
		/// async body - the same thing <c>async Task&lt;int&gt; Main</c> compiles to - and nothing about the
		/// process's threading changes.
		/// </remarks>
		/// <param name="body">The real program: a test engine run, or an application's startup.</param>
		/// <returns><paramref name="body"/>'s result, once it has finished.</returns>
		/// <remarks>
		/// Not available in the browser: this method exists to block the calling thread, and blocking the
		/// one wasm thread deadlocks the page. A browser head's entry point is the rAF loop instead.
		/// </remarks>
		[UnsupportedOSPlatform("browser")]
		public static int RunHosted(Func<Task<int>> body)
		{
			if (body == null)
			{
				throw new ArgumentNullException(nameof(body));
			}

			if (hosted)
			{
				throw new InvalidOperationException(
					"The main thread is already hosted. RunHosted is the entry point of a process and there is only one.");
			}

			if (MainThreadRequired)
			{
				Volatile.Write(ref mainThreadId, Environment.CurrentManagedThreadId);
				hosted = true;
			}

			int exitCode = 0;
			ExceptionDispatchInfo failure = null;
			int bodyFinished = 0;

			// Deliberately not awaited: this method exists to block the main thread, and the completion it
			// waits on is the flag set below rather than the task. Nothing else observes the task.
			async Task RunBodyAsync()
			{
				try
				{
					exitCode = await body();
				}
				catch (Exception ex)
				{
					failure = ExceptionDispatchInfo.Capture(ex);
				}
				finally
				{
					Interlocked.Exchange(ref bodyFinished, 1);
					workSignal.Set();
				}
			}

			_ = Task.Run(RunBodyAsync);

			try
			{
				while (Volatile.Read(ref bodyFinished) == 0)
				{
					// Reset before draining, never after: an enqueue that lands between the reset and the
					// wait has already set the signal, so the wait returns immediately instead of sitting
					// out the interval with work in the queue.
					workSignal.Reset();

					DrainPending();

					if (Volatile.Read(ref bodyFinished) != 0)
					{
						break;
					}

					workSignal.Wait(IdleWaitMilliseconds);
				}

				// Anything queued in the instant the body finished still belongs on this thread.
				DrainPending();
			}
			finally
			{
				hostExited = true;
			}

			failure?.Throw();

			return exitCode;
		}

		/// <summary>
		/// Runs <paramref name="work"/> on the main thread and waits for it, re-throwing whatever it threw
		/// on the calling thread. Runs inline when nothing is hosted or the caller is already the main
		/// thread.
		/// </summary>
		/// <remarks>
		/// The wait is deliberately unbounded. A timeout here would not recover anything - it would let the
		/// caller carry on and touch AppKit from the wrong thread, which is the crash this whole mechanism
		/// exists to prevent - so a main thread that has stopped draining is left to surface as the hang it
		/// is.
		/// </remarks>
		public static void Invoke(Action work)
		{
			if (work == null)
			{
				return;
			}

			// IsBrowser is part of the condition rather than a separate guard because it is the same
			// case: wasm has one thread, so the caller always *is* the main thread and there is nothing
			// to marshal to. Saying so explicitly is also what lets the platform analyzer see that the
			// blocking Wait below is unreachable there.
			if (OperatingSystem.IsBrowser() || !hosted || IsMainThread)
			{
				work();
				return;
			}

			var item = Enqueue(work);

			item.Completed.Wait();
			item.Failure?.Throw();
		}

		/// <summary>
		/// Runs <paramref name="work"/> on the main thread and returns its result. See
		/// <see cref="Invoke(Action)"/>.
		/// </summary>
		public static T Invoke<T>(Func<T> work)
		{
			if (work == null)
			{
				return default;
			}

			if (!hosted || IsMainThread)
			{
				return work();
			}

			T result = default;

			// A statement body, not "() => result = work()": that expression has type T, so it would bind
			// to Func<T> and call this overload again rather than the Action one.
			Invoke(() =>
			{
				result = work();
			});

			return result;
		}

		/// <summary>
		/// Queues <paramref name="work"/> for the main thread without waiting for it. Runs inline when
		/// nothing is hosted or the caller is already the main thread.
		/// </summary>
		public static void Post(Action work)
		{
			if (work == null)
			{
				return;
			}

			if (!hosted || IsMainThread)
			{
				work();
				return;
			}

			Enqueue(work);
		}

		/// <summary>
		/// Runs everything queued for the main thread. Called by <see cref="RunHosted"/> and by any pump a
		/// platform host runs on the main thread, so work keeps flowing while a window owns the loop.
		/// </summary>
		public static void DrainPending()
		{
			while (true)
			{
				WorkItem item;

				lock (QueueLock)
				{
					if (Pending.Count == 0)
					{
						return;
					}

					item = Pending.Dequeue();
				}

				// Runs outside the lock: an item can be a whole message loop (a window being shown), and
				// holding the queue for its lifetime would stop anything else ever reaching this thread.
				item.Run();
			}
		}

		/// <summary>
		/// Parks the main thread for up to <paramref name="milliseconds"/>, returning early if work
		/// arrives. What a platform pump should idle on instead of sleeping, so a marshalled call is not
		/// held up for the length of the sleep.
		/// </summary>
		public static bool WaitForWork(int milliseconds)
		{
			// Nothing hosts the main thread in the browser and nothing may block it, so there is no
			// waiting to do - the caller's next tick is the rAF callback.
			if (OperatingSystem.IsBrowser())
			{
				return false;
			}

			if (!hosted)
			{
				Thread.Sleep(milliseconds);
				return false;
			}

			workSignal.Reset();

			lock (QueueLock)
			{
				if (Pending.Count > 0)
				{
					return true;
				}
			}

			return workSignal.Wait(milliseconds);
		}

		private static WorkItem Enqueue(Action work)
		{
			if (hostExited)
			{
				throw new InvalidOperationException(
					"The main thread host has already exited, so this work can never run on the main thread. "
					+ "Something is still driving UI after the process's main body returned.");
			}

			var item = new WorkItem(work);

			lock (QueueLock)
			{
				Pending.Enqueue(item);
			}

			workSignal.Set();

			return item;
		}

		/// <summary>One unit of work handed to the main thread, plus the result the requester waits on.</summary>
		private sealed class WorkItem
		{
			private readonly Action work;

			internal WorkItem(Action work)
			{
				this.work = work;
			}

			internal ManualResetEventSlim Completed { get; } = new ManualResetEventSlim(false);

			internal ExceptionDispatchInfo Failure { get; private set; }

			internal void Run()
			{
				try
				{
					this.work();
				}
				catch (Exception ex)
				{
					this.Failure = ExceptionDispatchInfo.Capture(ex);
				}
				finally
				{
					this.Completed.Set();
				}
			}
		}
	}
}
