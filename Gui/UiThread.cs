/*
Copyright (c) 2015, Lars Brubaker
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

namespace MatterHackers.Agg.UI
{
	public static class UiThread
	{
		private static List<DeferredAction> deferredActions = new List<DeferredAction>();
		private static List<Action> callLater = new List<Action>();
		private static Stopwatch timer = new Stopwatch();

		private class DeferredAction
		{
			internal Action Action;
			internal long AbsoluteMillisecondsToRunAt;

			internal DeferredAction(Action action, long absoluteMillisecondsToRunAt)
			{
				this.Action = action;
				this.AbsoluteMillisecondsToRunAt = absoluteMillisecondsToRunAt;
			}
		}

		public static void RunOnIdle(Action action)
		{
			lock (callLater)
			{
				callLater.Add(action);
			}
		}

		public static void RunOnIdle(Action action, double delayInSeconds = 0)
		{
			if (!timer.IsRunning)
			{
				timer.Start();
			}

			lock (deferredActions)
			{
				deferredActions.Add(new DeferredAction(action, timer.ElapsedMilliseconds + (int)(delayInSeconds * 1000)));
			}
		}

		public static long CurrentTimerMs => timer.ElapsedMilliseconds;

		public static int Count => deferredActions.Count;

		public static int CountExpired
		{
			get
			{
				int count = 0;
				lock (deferredActions)
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

		public static void InvokePendingActions()
		{
			List<Action> callNow = callLater;

			// Don't keep this locked for long
			lock (deferredActions)
			{
				callLater = new List<Action>();

				long currentMilliseconds = timer.ElapsedMilliseconds;
				for (int i = deferredActions.Count - 1; i >= 0; i--)
				{
					// If the deferred action has reach its execution time, push it to the list to execute and remove deferred
					var deferred = deferredActions[i];
					if (deferred.AbsoluteMillisecondsToRunAt <= currentMilliseconds)
					{
						callNow.Add(deferred.Action);
						deferredActions.RemoveAt(i);
					}
				}
			}

			foreach (Action action in callNow)
			{
				try
				{
					action?.Invoke();
				}
				catch (Exception invokeException)
				{
#if DEBUG
					throw (invokeException);
#endif
				}
			}
		}
	}
}