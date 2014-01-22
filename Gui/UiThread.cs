using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public delegate void IdleCallback(object state);
    public static class UiThread
    {
        static List<CallBackAndState> functionsToCallOnIdle = new List<CallBackAndState>();

        class CallBackAndState
        {
            internal IdleCallback idleCallBack;
            internal object stateInfo;

            internal CallBackAndState(IdleCallback idleCallBack, object stateInfo)
            {
                this.idleCallBack = idleCallBack;
                this.stateInfo = stateInfo;
            }
        }

        public static void RunOnIdle(IdleCallback callBack, object state = null)
        {
            using (TimedLock.Lock(functionsToCallOnIdle, "PendingUiEvents AddAction()"))
            {
                functionsToCallOnIdle.Add(new CallBackAndState(callBack, state));
            }
        }

        public static void DoRunAllPending()
        {
            List<CallBackAndState> holdFunctionsToCallOnIdle = new List<CallBackAndState>();
            // make a copy so we don't keep this locked for long
            using (TimedLock.Lock(functionsToCallOnIdle, "PendingUiEvents AddAction()"))
            {
                foreach (CallBackAndState callBackAndState in functionsToCallOnIdle)
                {
                    holdFunctionsToCallOnIdle.Add(new CallBackAndState(callBackAndState.idleCallBack, callBackAndState.stateInfo));
                }
                functionsToCallOnIdle.Clear();
            }

            // now call all the functions
            foreach (CallBackAndState callBackAndState in holdFunctionsToCallOnIdle)
            {
                callBackAndState.idleCallBack(callBackAndState.stateInfo);
            }
        }
    }
}
