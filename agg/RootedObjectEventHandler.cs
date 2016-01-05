using System;
using System.Collections.Generic;

namespace MatterHackers.Agg
{
#if false
    public interface IReceiveRootedWeakEvent
    {
        void RootedEvent(string eventType, EventArgs e);
    }

    public class RootedObjectWeakEventHandler
    {
        List<WeakReference> classesToCall = new List<WeakReference>();
        List<string> eventTypes = new List<string>();

        public void Register(IReceiveRootedWeakEvent objectToCall, string eventType)
        {
            classesToCall.Add(new WeakReference(objectToCall));
            eventTypes.Add(eventType);
        }

        public void Unregister(IReceiveRootedWeakEvent objectToCall)
        {
            for (int i = classesToCall.Count - 1; i >= 0; i--)
            {
                if (classesToCall[i].Target == objectToCall)
                {
                    classesToCall.RemoveAt(i);
                }
            }
        }

        public void CallEvents(Object sender, EventArgs e)
        {
            for(int i=classesToCall.Count-1; i>=0; i--)
            {
                IReceiveRootedWeakEvent reciever = classesToCall[i].Target as IReceiveRootedWeakEvent;
                if (reciever == null)
                {
                    classesToCall.RemoveAt(i);
                    eventTypes.RemoveAt(i);
                }
                else
                {
                    reciever.RootedEvent(eventTypes[i], e);
                }
            }
        }
    }
#endif

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
			// After we remove it, it will still be removed again in the functionThatWillBeCalledToUnregisterEvent
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