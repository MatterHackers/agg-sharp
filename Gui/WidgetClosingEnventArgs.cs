namespace MatterHackers.Agg.UI
{
	public class ClosingEventArgs
	{
		public bool Cancel { get; set; }
	}

	public class ClosedEventArgs
	{
		public bool OsEvent { get; private set; }
		
		public ClosedEventArgs(bool osEvent)
		{
			OsEvent = osEvent;
		}
	}
}