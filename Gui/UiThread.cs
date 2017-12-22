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
using System.Linq;

namespace MatterHackers.Agg.UI
{
	public static class UiThread
	{
		private static List<DeferredAction> functionsToCheckIfTimeToCall = new List<DeferredAction>();
		private static List<Action> callNextCycle = new List<Action>();
		private static Stopwatch timer = new Stopwatch();

		private class DeferredAction
		{
			internal Action action;
			internal long absoluteMillisecondsToRunAt;

			internal DeferredAction(Action action, long absoluteMillisecondsToRunAt)
			{
				this.action = action;
				this.absoluteMillisecondsToRunAt = absoluteMillisecondsToRunAt;
			}
		}

		public static void RunOnIdle(Action callBack)
		{
			lock (callNextCycle)
			{
				callNextCycle.Add(callBack);
			}
		}

		public static void RunOnIdle(Action callBack, double delayInSeconds = 0)
		{
			if (!timer.IsRunning)
			{
				timer.Start();
			}
			lock (functionsToCheckIfTimeToCall)
			{
				functionsToCheckIfTimeToCall.Add(new DeferredAction(callBack, timer.ElapsedMilliseconds + (int)(delayInSeconds * 1000)));
			}
		}

		public static long CurrentTimerMs => timer.ElapsedMilliseconds;

		public static int Count => functionsToCheckIfTimeToCall.Count;

		public static int CountExpired
		{
			get
			{
				int count = 0;
				lock (functionsToCheckIfTimeToCall)
				{
					long currentMilliseconds = timer.ElapsedMilliseconds;
					for (int i = 0; i < functionsToCheckIfTimeToCall.Count; i++)
					{
						if (functionsToCheckIfTimeToCall[i].absoluteMillisecondsToRunAt <= currentMilliseconds)
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
			List<Action> callThisCycle = callNextCycle;

			List<DeferredAction> holdFunctionsToCallOnIdle = new List<DeferredAction>();
			// make a copy so we don't keep this locked for long
			lock (functionsToCheckIfTimeToCall)
			{
				callNextCycle = new List<Action>();

				long currentMilliseconds = timer.ElapsedMilliseconds;
				for (int i = functionsToCheckIfTimeToCall.Count - 1; i >= 0; i--)
				{
					DeferredAction callBackAndState = functionsToCheckIfTimeToCall[i];
					if (callBackAndState.absoluteMillisecondsToRunAt <= currentMilliseconds)
					{
						holdFunctionsToCallOnIdle.Add(callBackAndState);
						functionsToCheckIfTimeToCall.RemoveAt(i);
					}
				}
			}

			foreach (Action action in callThisCycle)
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

			// now call all the functions (we put them in backwards to make it easier to remove them as we went so run them backwards
			for (int i = holdFunctionsToCallOnIdle.Count - 1; i >= 0; i--)
			{
				DeferredAction callBackAndState = holdFunctionsToCallOnIdle[i];
				callBackAndState.action();
			}
		}
	}
}