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
        static VectorPOD<CallBackAndState> holdFunctionsToCallOnIdle = new VectorPOD<CallBackAndState>();
        static VectorPOD<CallBackAndState> functionsToCallOnIdle = new VectorPOD<CallBackAndState>();
        static Stopwatch timer = new Stopwatch();

        struct CallBackAndState
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

            long timeToRunAt = timer.ElapsedMilliseconds + (int)(delayInSeconds * 1000);
            using (TimedLock.Lock(functionsToCallOnIdle, "PendingUiEvents AddAction()"))
            {
                int count = functionsToCallOnIdle.Count;
                if (functionsToCallOnIdle.Capacity() > count)
                {
                    functionsToCallOnIdle.Array[count].idleCallBack = callBack;
                    functionsToCallOnIdle.Array[count].stateInfo = state;
                    functionsToCallOnIdle.Array[count].absoluteMillisecondsToRunAt = timeToRunAt;
                    functionsToCallOnIdle.inc_size(1);
                }
                else
                {
                    functionsToCallOnIdle.Add(new CallBackAndState(callBack, state, timeToRunAt));
                }
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
                // make a copy so we don't keep this locked for long
                using (TimedLock.Lock(functionsToCallOnIdle, "PendingUiEvents AddAction()"))
                {
                    long currentMilliseconds = timer.ElapsedMilliseconds;
                    for (int i = functionsToCallOnIdle.Count - 1; i >= 0; i--)
                    {
                        if (functionsToCallOnIdle.Array[i].absoluteMillisecondsToRunAt <= currentMilliseconds)
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
            holdFunctionsToCallOnIdle.Clear();
            // make a copy so we don't keep this locked for long
            using (TimedLock.Lock(functionsToCallOnIdle, "PendingUiEvents AddAction()"))
            {
                long currentMilliseconds = timer.ElapsedMilliseconds;
                for(int i=functionsToCallOnIdle.Count-1; i>=0; i--)
                {
                    if (functionsToCallOnIdle.Array[i].absoluteMillisecondsToRunAt <= currentMilliseconds)
                    {
                        int count = holdFunctionsToCallOnIdle.Count;
                        if (holdFunctionsToCallOnIdle.Capacity() > count)
                        {
                            holdFunctionsToCallOnIdle.Array[count].idleCallBack = functionsToCallOnIdle.Array[i].idleCallBack;
                            holdFunctionsToCallOnIdle.Array[count].stateInfo = functionsToCallOnIdle.Array[i].stateInfo;
                            holdFunctionsToCallOnIdle.Array[count].absoluteMillisecondsToRunAt = functionsToCallOnIdle.Array[i].absoluteMillisecondsToRunAt;
                            holdFunctionsToCallOnIdle.inc_size(1);
                        }
                        else
                        {
                            holdFunctionsToCallOnIdle.Add(new CallBackAndState(functionsToCallOnIdle.Array[i].idleCallBack,
                                functionsToCallOnIdle.Array[i].stateInfo,
                                functionsToCallOnIdle.Array[i].absoluteMillisecondsToRunAt));
                        }

                        functionsToCallOnIdle.RemoveAt(i);
                    }
                }
            }

            // now call all the functions (we put them in backwards to make it easier to remove them as we went so run them backwards
            for(int i=holdFunctionsToCallOnIdle.Count-1; i>=0; i--)
            {
                holdFunctionsToCallOnIdle.Array[i].idleCallBack(holdFunctionsToCallOnIdle.Array[i].stateInfo);
            }
        }
    }
}
