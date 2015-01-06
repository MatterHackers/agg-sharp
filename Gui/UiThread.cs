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
                int newItemIndex = functionsToCallOnIdle.Count;
                if (functionsToCallOnIdle.Capacity() > newItemIndex)
                {
                    functionsToCallOnIdle.Array[newItemIndex].idleCallBack = callBack;
                    functionsToCallOnIdle.Array[newItemIndex].stateInfo = state;
                    functionsToCallOnIdle.Array[newItemIndex].absoluteMillisecondsToRunAt = timeToRunAt;
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
                    for (int i = 0; i < functionsToCallOnIdle.Count; i++)
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
            using (TimedLock.Lock(functionsToCallOnIdle, "PendingUiEvents AddAction()"))
            {
                holdFunctionsToCallOnIdle.Clear();
                // make a copy so we don't keep this locked for long
                long currentMilliseconds = timer.ElapsedMilliseconds;
                // We go back to front so that it is easier to remove items, we don't have to change our indexer.
                for(int functionToCallIndex=functionsToCallOnIdle.Count-1; functionToCallIndex>=0; functionToCallIndex--)
                {
                    if (functionsToCallOnIdle.Array[functionToCallIndex].absoluteMillisecondsToRunAt <= currentMilliseconds)
                    {
                        int newHoldIndex = holdFunctionsToCallOnIdle.Count;
                        if (holdFunctionsToCallOnIdle.Capacity() > newHoldIndex)
                        {
                            holdFunctionsToCallOnIdle.Array[newHoldIndex].idleCallBack = functionsToCallOnIdle.Array[functionToCallIndex].idleCallBack;
                            holdFunctionsToCallOnIdle.Array[newHoldIndex].stateInfo = functionsToCallOnIdle.Array[functionToCallIndex].stateInfo;
                            holdFunctionsToCallOnIdle.Array[newHoldIndex].absoluteMillisecondsToRunAt = functionsToCallOnIdle.Array[functionToCallIndex].absoluteMillisecondsToRunAt;
                            holdFunctionsToCallOnIdle.inc_size(1);
                        }
                        else
                        {
                            holdFunctionsToCallOnIdle.Add(new CallBackAndState(functionsToCallOnIdle.Array[functionToCallIndex].idleCallBack,
                                functionsToCallOnIdle.Array[functionToCallIndex].stateInfo,
                                functionsToCallOnIdle.Array[functionToCallIndex].absoluteMillisecondsToRunAt));
                        }

                        functionsToCallOnIdle.RemoveAt(functionToCallIndex);
                    }
                }
            }

            // now call all the functions (we put them in backwards to make it easier to remove them as we went so run them backwards)
            for (int holdFunctionIndex = holdFunctionsToCallOnIdle.Count - 1; holdFunctionIndex >= 0; holdFunctionIndex--)
            {
                holdFunctionsToCallOnIdle.Array[holdFunctionIndex].idleCallBack(holdFunctionsToCallOnIdle.Array[holdFunctionIndex].stateInfo);
            }
        }
    }
}
