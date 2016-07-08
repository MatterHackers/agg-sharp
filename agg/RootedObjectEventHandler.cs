using System;
using System.Collections.Generic;

namespace MatterHackers.Agg
{
	public class RootedObjectEventHandler
	{
		private EventHandler internalEvent;

		public void RegisterEvent(EventHandler functionToCallOnEvent, ref EventHandler unregisterEvents)
		{
			internalEvent += functionToCallOnEvent;

			unregisterEvents += (sender, e) =>
			{
				internalEvent -= functionToCallOnEvent;
			};
		}

		public void UnregisterEvent(EventHandler functionToCallOnEvent, ref EventHandler unregisterEvents)
		{
			internalEvent -= functionToCallOnEvent;
			// After we remove it, it will still be removed again in the unregisterEvents
			// But it is valid to attempt remove more than once.
		}

		public void CallEvents(Object sender, EventArgs e)
		{
			internalEvent?.Invoke(sender, e);
		}
	}
}