using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg
{
    public class RootedObjectEventHandler
    {
#if DEBUG
        private event EventHandler InternalEventForDebug;
        private List<EventHandler> DebugEventDelegates = new List<EventHandler>();

        private event EventHandler InternalEvent
        {
            //Wraps the PrivateClick event delegate so that we can track which events have been added and clear them if necessary            
            add
            {
                InternalEventForDebug += value;
                DebugEventDelegates.Add(value);
            }

            remove
            {
                InternalEventForDebug -= value;
                DebugEventDelegates.Remove(value);
            }
        }
#else
        EventHandler InternalEvent;
#endif

        public void RegisterEvent(EventHandler functionToCallOnEvent, ref EventHandler functionThatWillBeCalledToUnregisterEvent)
        {
            InternalEvent += functionToCallOnEvent;
            functionThatWillBeCalledToUnregisterEvent += (sender, e) =>
            {
                InternalEvent -= functionToCallOnEvent;
            };
        }

        public void UnregisterEvent(EventHandler functionToCallOnEvent, ref EventHandler functionThatWillBeCalledToUnregisterEvent)
        {
            InternalEvent -= functionToCallOnEvent;
            // After we remove it it will still be removed again in the functionThatWillBeCalledToUnregisterEvent
            // But it is valid to attempt remove more than once.
        }

        public void CallEvents(Object sender, EventArgs e)
        {
#if DEBUG
            if (InternalEventForDebug != null)
            {
                InternalEventForDebug(sender, e);
            }
#else
            if (InternalEvent != null)
            {
                InternalEvent(sender, e);
            }
#endif
        }
    }
}
