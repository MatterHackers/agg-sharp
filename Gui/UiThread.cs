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
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public delegate void IdleCallback(object state);
    public static class UiThread
    {
        static List<CallBackAndState> functionsToCallOnIdle = new List<CallBackAndState>();
        static Stopwatch timer = new Stopwatch();

        class CallBackAndState
        {
            internal IdleCallback idleCallBack;
            internal object stateInfo;
            internal long absoluteMillisecondsToRunAt;

            internal CallBackAndState(IdleCallback idleCallBack, object stateInfo, long absoluteMillisecondsToRunAt)
            {
                this.idleCallBack = idleCallBack;
                this.stateInfo = stateInfo;
                this.absoluteMillisecondsToRunAt = absoluteMillisecondsToRunAt;
            }
        }

        public static void RunOnIdle(IdleCallback callBack, double delayInSeconds)
        {
            RunOnIdle(callBack, null, delayInSeconds);
        }

        public static void RunOnIdle(IdleCallback callBack, object state = null, double delayInSeconds = 0)
        {
            if(!timer.IsRunning)
            {
                timer.Start();
            }
            using (TimedLock.Lock(functionsToCallOnIdle, "PendingUiEvents AddAction()"))
            {
                functionsToCallOnIdle.Add(new CallBackAndState(callBack, state, timer.ElapsedMilliseconds + (int)(delayInSeconds * 1000)));
            }
        }

        public static int Count
        {
            get
            {
                return functionsToCallOnIdle.Count;
            }
        }

        public static int CountExpired
        {
            get
            {
                int count = 0;
                using (TimedLock.Lock(functionsToCallOnIdle, "PendingUiEvents AddAction()"))
                {
                    long currentMilliseconds = timer.ElapsedMilliseconds;
                    for (int i = 0; i < functionsToCallOnIdle.Count; i++ )
                    {
                        if (functionsToCallOnIdle[i].absoluteMillisecondsToRunAt <= currentMilliseconds)
                        {
                            count++;
                        }
                    }
                }
                return count;
            }
        }
        
        public static void DoRunAllPending()
        {
            List<CallBackAndState> holdFunctionsToCallOnIdle = new List<CallBackAndState>();
            // make a copy so we don't keep this locked for long
            using (TimedLock.Lock(functionsToCallOnIdle, "PendingUiEvents AddAction()"))
            {
                long currentMilliseconds = timer.ElapsedMilliseconds;
                for(int i=functionsToCallOnIdle.Count-1; i>=0; i--)
                {
                    CallBackAndState callBackAndState = functionsToCallOnIdle[i];
                    if (callBackAndState.absoluteMillisecondsToRunAt <= currentMilliseconds)
                    {
                        holdFunctionsToCallOnIdle.Add(new CallBackAndState(callBackAndState.idleCallBack, callBackAndState.stateInfo, callBackAndState.absoluteMillisecondsToRunAt));
                        functionsToCallOnIdle.RemoveAt(i);
                    }
                }
            }

            // now call all the functions (we put them in backwards to make it easier to remove them as we went so run them backwards
            for(int i=holdFunctionsToCallOnIdle.Count-1; i>=0; i--)
            {
                CallBackAndState callBackAndState = holdFunctionsToCallOnIdle[i];
				callBackAndState.idleCallBack(callBackAndState.stateInfo);
            }
        }
    }
}
