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
        
        static bool currentlyRunning = false;
        public static bool CurrentlyRunning { get { return currentlyRunning; } }

        public static void DoRunAllPending()
        {
            currentlyRunning = true;
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

            // now call all the functions
            foreach (CallBackAndState callBackAndState in holdFunctionsToCallOnIdle)
            {
                callBackAndState.idleCallBack(callBackAndState.stateInfo);
            }

            currentlyRunning = false;
        }
    }
}
