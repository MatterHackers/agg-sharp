using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public class RootedObjectEventHandler
    {
        EventHandler InternalEvent;
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
            if (InternalEvent != null)
            {
                InternalEvent(this, e);
            }
        }
    }
}
