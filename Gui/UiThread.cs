/*
Copyright (c) 2026, Lars Brubaker, John Lewin
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
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MatterHackers.Agg.UI
{
	public static class UiThread
	{
		private static List<DeferredAction> deferredActions = new List<DeferredAction>();

		/// <summary>
		/// Actions queued by RunOnIdle waiting for the next pump. Only ever touched under <see cref="locker"/>.
		/// </summary>
		private static readonly List<Action> callLater = new List<Action>();

		private static Stopwatch timer = Stopwatch.StartNew();

		private static readonly object locker = new object();

		public static long CurrentTimerMs => timer.ElapsedMilliseconds;

		/// <summary>
		/// Count of the current deferred actions
		/// </summary>
		public static int Count => deferredActions.Count;

		public static int CountExpired
		{
			get
			{
				int count = 0;

				// locker, not deferredActions: a pump removing expired entries holds locker, and locking a
				// different object here would let that removal break this indexed walk.
				lock (locker)
				{
					long currentMilliseconds = timer.ElapsedMilliseconds;
					for (int i = 0; i < deferredActions.Count; i++)
					{
						if (deferredActions[i].AbsoluteMillisecondsToRunAt <= currentMilliseconds)
						{
							count++;
						}
					}
				}

				return count;
			}
		}

		private static List<RunningInterval> intervalActions = new List<RunningInterval>();

		/// <summary>
		/// Repeats a given action at every given time-interval.
		/// </summary>
		/// <param name="action">The action to execute</param>
		/// <param name="intervalInSeconds">The invoke interval in seconds</param>
		/// <returns>Action to call to cancel interval</returns>
		public static RunningInterval SetInterval(Action action, double intervalInSeconds)
		{
			var runningInterval = new RunningInterval(action, intervalInSeconds);

			lock (locker)
			{
				intervalActions.Add(runningInterval);
			}

			return runningInterval;
		}

		public static void ClearInterval(RunningInterval runningInterval)
		{
			lock (locker)
			{
				if (runningInterval != null)
				{
					runningInterval.Shutdown();
					intervalActions.Remove(runningInterval);
				}
			}
		}

        /// <summary>
        /// If on the ui thread run this action now. If not queue for running on the ui thread.
        /// </summary>
        /// <param name="action">The action to run</param>
        public static void RunOnUiThread(Action action)
        {
			if (UiThread.IsUiThread)
			{
				action?.Invoke();
			}
			else
			{
				RunOnIdle(action);
			}
		}

        /// <summary>
        /// Queue this action to run on the uithread. It will queue even if currently on the ui thread.
        /// </summary>
        /// <param name="action">The action to run</param>
        public static void RunOnIdle(Action action)
		{
			lock (locker)
			{
				callLater.Add(action);
			}
		}

		/// <summary>
		/// Queue this action to run on the uithread after delayInSeconds has passed.
		/// </summary>
		/// <param name="action">The action to run</param>
		/// <param name="delayInSeconds">The time to wait</param>
		public static void RunOnIdle(Action action, double delayInSeconds)
		{
			lock (locker)
			{
				deferredActions.Add(new DeferredAction(action, timer.ElapsedMilliseconds + (int)(delayInSeconds * 1000)));
			}
		}

		public static bool IsUiThread
		{
			get
			{
				return Thread.CurrentThread.ManagedThreadId == uiThreadId;
			}
		}

		/// <summary>
		/// Declares the calling thread to be the UI thread - the thread that drains this queue and that
		/// <see cref="IsUiThread"/> answers for. Called by every platform host from its pump (through
		/// <see cref="MainLoopSynchronizationContext.InstallOnPumpThread"/>); idempotent and cheap enough to
		/// sit on the pump path.
		/// </summary>
		/// <remarks>
		/// <para><see cref="InvokePendingActions"/> also latches the id, but only as a fallback for a process
		/// with no host at all (a headless test that pumps the queue itself), and only while nothing has
		/// claimed it. That fallback cannot be the whole story, because anything may drain the queue: the
		/// automation harness unlatches the id between tests (<see cref="ResetForTests"/>) and test helpers
		/// pump from worker threads, so the first drain after a reset is a race the host's own pump can
		/// lose - and did, intermittently, under a parallel test run.</para>
		/// <para>What that costs is not a cosmetic wrong answer. Every "am I already there, or must I
		/// marshal?" decision reads <see cref="IsUiThread"/>, so a host told it is not the UI thread marshals
		/// work to itself: the mac screenshot capture re-queued its own request onto the pump it was already
		/// running on, frame after frame, until the caller's timeout expired having written no file. Letting
		/// the pump say so directly makes that self-correcting - the identity is restored on the next pump,
		/// whatever else touched the queue.</para>
		/// </remarks>
		public static void MarkCurrentThreadAsUiThread()
		{
			int callingThreadId = Thread.CurrentThread.ManagedThreadId;

			// Compared before writing so the pump is not dirtying a shared field on every idle tick.
			if (uiThreadId != callingThreadId)
			{
				uiThreadId = callingThreadId;
			}
		}

		/// <summary>
		/// Awaitable switch to the UI thread: everything after <c>await UiThread.SwitchToUiThreadAsync()</c>
		/// runs on the thread that pumps <see cref="InvokePendingActions"/>.
		/// </summary>
		/// <remarks>
		/// <para>This is the awaitable marshal the RunOnIdle-plus-TaskCompletionSource shapes existed to work
		/// around. Work that starts on a thread pool task (this app runs every long operation through
		/// Tasks.Execute, which is a Task.Run, deliberately keeping heavy sync work off the UI thread) captures
		/// no synchronization context, so its awaits resume on the pool. A UI-touching tail therefore had to be
		/// handed to <see cref="RunOnIdle(Action)"/> - and, when the caller needed to know it had finished or
		/// whether it threw, hand-plumbed back through a TaskCompletionSource, because RunOnIdle takes an
		/// Action and an async lambda passed to it is an async void. Awaiting this instead keeps the whole
		/// method one straight line: the continuation is still part of the caller's task, so a failure after
		/// the switch faults that task rather than escaping onto the idle pump.</para>
		/// <para>Once the switch lands, <see cref="MainLoopSynchronizationContext"/> is the current context (the
		/// hosts install it on the pump thread), so ORDINARY awaits later in the same method also resume on the
		/// UI thread without switching again.</para>
		/// <para>Awaiting from the UI thread continues inline - <see cref="SwitchToUiThreadAwaiter.IsCompleted"/>
		/// is <see cref="IsUiThread"/> - so it is free to call on a path that is sometimes already there, and it
		/// costs no extra pump.</para>
		/// <para>HAZARD: the continuation is queued through <see cref="RunOnIdle(Action)"/>, so it only runs if
		/// something drains the queue. On a host with no pump - a headless test with no window - the await never
		/// resumes and the caller is parked forever. Callers whose code can run headless must keep their
		/// existing "no window, do it here" guards rather than rely on this.</para>
		/// </remarks>
		public static SwitchToUiThreadAwaitable SwitchToUiThreadAsync()
		{
			return default(SwitchToUiThreadAwaitable);
		}

		private static int uiThreadId = -1;

		/// <summary>
		/// Raised when a queued UI-thread action throws after it has been dispatched.
		/// Automation tests subscribe so silent UI failures become test failures.
		/// </summary>
		public static event Action<Exception> UnhandledException;

		public static void ReportUnhandledException(Exception exception)
		{
			try
			{
				UnhandledException?.Invoke(exception);
			}
			catch
			{
			}
		}

		public static void InvokePendingActions()
		{
			// A fallback only, for a process with no platform host to declare a pump - a headless test that
			// drains the queue on its own thread. A host claims the identity outright; see
			// MarkCurrentThreadAsUiThread for why first-drain-wins cannot be the whole rule.
			if (uiThreadId == -1)
			{
				uiThreadId = Thread.CurrentThread.ManagedThreadId;
			}

			Action[] callNow;

			// Don't keep this locked for long
			lock (locker)
			{
				// Loop over deferred RunOnIdle actions which previously had not yet reached their execution time
				long currentMilliseconds = timer.ElapsedMilliseconds;
				for (int i = deferredActions.Count - 1; i >= 0; i--)
				{
					// If the deferred action has reach its execution time, push it to the list to execute and remove deferred
					var deferred = deferredActions[i];
					if (deferred.AbsoluteMillisecondsToRunAt <= currentMilliseconds)
					{
						callLater.Add(deferred.Execute);
						deferredActions.RemoveAt(i);
					}
				}

				// Loop over SetInterval functions, queuing for execution if interval period has elapsed
				for (int i = intervalActions.Count - 1; i >= 0; i--)
				{
					// If the SetInterval action has reach its execution time, push it to the list
					var intervalAction = intervalActions[i];
					if (intervalAction.AbsoluteMillisecondsToRunAt <= currentMilliseconds)
					{
						// Advance the due time here, inside the lock, rather than in Execute: Execute runs
						// outside the lock, so a second pump would still see the old due time and queue the
						// same interval a second time.
						intervalAction.ScheduleNextRun();
						callLater.Add(intervalAction.Execute);
					}
				}

				if (callLater.Count == 0)
				{
					return;
				}

				// Take a private copy to run outside the lock. It has to be a copy rather than a shared
				// buffer: a nested pump (an action that runs a message loop) or a second thread pumping
				// would otherwise clear and refill the very list being enumerated here.
				callNow = callLater.ToArray();
				callLater.Clear();
			}

			foreach (Action action in callNow)
			{
#if DEBUG
				action?.Invoke();
#else
				try
				{
					action?.Invoke();
				}
				catch (Exception invokeException)
				{
					ReportUnhandledException(invokeException);
				}
#endif
			}
		}

		/// <summary>
		/// Drains the queue from inside a nested pump - a loop that is itself running underneath a queued
		/// action and cannot finish until an awaited continuation makes progress.
		/// </summary>
		/// <remarks>
		/// The platform hosts guard their ordinary idle drain with a re-entrancy flag, which is the right
		/// protection for an idle action that runs a message loop of its own and the wrong one for a loop
		/// that is spinning precisely because it is waiting on work this queue now owns: a suspended await
		/// resumes by posting through <see cref="MainLoopSynchronizationContext"/> into this very queue, so
		/// a guarded (no-op) drain would leave such a loop waiting forever for the one thing that could
		/// release it. Those loops call this instead. Nesting is safe because
		/// <see cref="InvokePendingActions"/> runs from a private copy of the queue rather than the live
		/// list.
		/// </remarks>
		public static void DrainForNestedPump()
		{
			InvokePendingActions();
		}

		public class DeferredAction
		{
			protected Action action;
			internal long AbsoluteMillisecondsToRunAt;

			internal DeferredAction(Action action, long absoluteMillisecondsToRunAt)
			{
				this.action = action;
				this.AbsoluteMillisecondsToRunAt = absoluteMillisecondsToRunAt;
			}

			public virtual void Execute()
			{
				this.action?.Invoke();
			}
		}

        /// <summary>
        /// Stores actions that are pending execution along with their scheduled execution time in milliseconds.
        /// Each action is identified by a unique string id.
        /// </summary>
        private static Dictionary<string, (Action action, long executeMs)> pendingLimitedActions = new Dictionary<string, (Action action, long executeMs)>();

        /// <summary>
        /// Represents an interval during which the pending actions are checked and executed if their scheduled time has passed.
        /// </summary>
        private static RunningInterval pendingLimitedActionsInterval = null;

        /// <summary>
        /// Schedules the provided action to be run after a certain delay, replacing any previously scheduled action with the same id.
        /// The actions are run no more frequently than the specified delay.
        /// </summary>
        /// <param name="action">The action to be run.</param>
        /// <param name="idToEnforceLimit">The id associated with the action. Used to enforce frequency limit.</param>
        /// <param name="delayBeforeCall">The delay in seconds before the action should be run.</param>
        public static void RunWithFrequencyLimit(Action action, string idToEnforceLimit, double delayBeforeCall)
        {
            lock (locker)
            {
                void CheckOnLimitActions()
                {
                    if (pendingLimitedActions.Any())
					{
                        // check if any times have expired
                        foreach (var kvp in pendingLimitedActions)
                        {
                            if (kvp.Value.executeMs < UiThread.CurrentTimerMs)
                            {
                                var actionToRun = kvp.Value.action;
                                pendingLimitedActions.Remove(kvp.Key);
                                actionToRun?.Invoke();
                            }
                        }
                    }
                    else
					{
                        if (pendingLimitedActionsInterval != null)
						{
							// clear interval
							UiThread.ClearInterval(pendingLimitedActionsInterval);
							pendingLimitedActionsInterval = null;
                        }

                    }
                }

                if (pendingLimitedActionsInterval == null)
				{
                    pendingLimitedActionsInterval = UiThread.SetInterval(CheckOnLimitActions, .01);
                }

                // check if it is already in pendingLimitedActions
                if (pendingLimitedActions.ContainsKey(idToEnforceLimit))
                {
                    // update the time
                    pendingLimitedActions[idToEnforceLimit] = (action, UiThread.CurrentTimerMs + (long)(delayBeforeCall * 1000));
                }
                else
                {
                    // add it
                    pendingLimitedActions.Add(idToEnforceLimit, (action, UiThread.CurrentTimerMs + (long)(delayBeforeCall * 1000)));
                }
            }
        }

        
		public static void ExecuteWhen(Func<bool> readyCondition, Action actionToExecute, double secondsBeforeRecheck = .1, double maxSecondsToWait = 1)
		{
			long startTime = UiThread.CurrentTimerMs;
			RunningInterval interval = null;

			void WaitForCondition()
			{
				var ready = readyCondition();
				if (ready || UiThread.CurrentTimerMs > startTime + maxSecondsToWait * 1000)
				{
					if (ready)
					{
						actionToExecute();
					}
					UiThread.ClearInterval(interval);
				}
			}

			interval = UiThread.SetInterval(WaitForCondition, secondsBeforeRecheck);
		}

		/// <summary>
		/// Resets all static state in UiThread - used for test cleanup
		/// </summary>
		public static void ResetForTests()
		{
			lock (locker)
			{
				// Clear all deferred actions
				deferredActions.Clear();
				
				// Clear queued call later actions
				callLater.Clear();

				// Clear all running intervals
				foreach (var interval in intervalActions)
				{
					interval.Shutdown();
				}
				intervalActions.Clear();
				
				// Clear limited actions
				pendingLimitedActions.Clear();
				if (pendingLimitedActionsInterval != null)
				{
					pendingLimitedActionsInterval.Shutdown();
					pendingLimitedActionsInterval = null;
				}
				
				// Reset UI thread ID
				uiThreadId = -1;
			}
		}
	}

	/// <summary>
	/// What <see cref="UiThread.SwitchToUiThreadAsync"/> hands back - see there for what it is for and its one
	/// hazard. A struct with no state: the switch's only inputs are the current thread and the one idle queue.
	/// </summary>
	public readonly struct SwitchToUiThreadAwaitable
	{
		public SwitchToUiThreadAwaiter GetAwaiter() => default(SwitchToUiThreadAwaiter);
	}

	/// <summary>
	/// The awaiter for <see cref="UiThread.SwitchToUiThreadAsync"/>.
	/// </summary>
	public readonly struct SwitchToUiThreadAwaiter : ICriticalNotifyCompletion
	{
		/// <summary>
		/// True when there is nothing to do - the caller is already on the UI thread - which makes the await
		/// continue inline with no pump hop at all.
		/// </summary>
		public bool IsCompleted => UiThread.IsUiThread;

		/// <summary>
		/// Queues the continuation with the caller's <see cref="ExecutionContext"/> flowed, as
		/// <see cref="INotifyCompletion"/> requires. A compiled <c>await</c> never comes through here - the
		/// state machine builder calls <see cref="UnsafeOnCompleted"/> and restores the captured context
		/// around its own MoveNext - but agg-sharp is a library, and a hand-written awaiter user calling
		/// <c>OnCompleted</c> directly is entitled to have its context (AsyncLocals, impersonation) reach
		/// the continuation.
		/// </summary>
		public void OnCompleted(Action continuation)
		{
			var context = ExecutionContext.Capture();

			if (context == null)
			{
				// Flow was suppressed; there is nothing to restore and running it directly is the contract.
				UiThread.RunOnIdle(continuation);
				return;
			}

			UiThread.RunOnIdle(() => ExecutionContext.Run(context, state => ((Action)state)(), continuation));
		}

		// The bare queue: the state machine restores the captured ExecutionContext around its own MoveNext,
		// so flowing it here too would be redundant work on the path every compiled await takes.
		public void UnsafeOnCompleted(Action continuation) => UiThread.RunOnIdle(continuation);

		public void GetResult()
		{
		}
	}

	public class RunningInterval : UiThread.DeferredAction
	{
		private double intervalInSeconds;

		public RunningInterval(Action action, double intervalInSeconds)
			: base(action, 0)
		{
			this.intervalInSeconds = intervalInSeconds;
			this.AbsoluteMillisecondsToRunAt = this.NextRunMs;
		}

		public bool Active => action != null;

		public override void Execute()
		{
			// The next run was scheduled by ScheduleNextRun when this was queued
			base.Execute();
		}

		/// <summary>
		/// Pushes the next due time out by one interval. Called while the queue is locked, at the moment
		/// this interval is queued, so no other pump can see it as still due and queue it again.
		/// </summary>
		internal void ScheduleNextRun()
		{
			this.AbsoluteMillisecondsToRunAt = this.NextRunMs;
		}

		internal void Shutdown()
		{
			action = null;
		}

		private long NextRunMs => UiThread.CurrentTimerMs + (int)(intervalInSeconds * 1000);
	}
}