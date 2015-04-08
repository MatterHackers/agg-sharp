namespace MatterHackers.Agg.UI
{
	public delegate void WidgetClosingEventHandler(object sender, WidgetClosingEnventArgs closingEvent);

	public class WidgetClosingEnventArgs
	{
		public enum ClosingReason { None, WindowsShutDown, UserClosing, TaskManagerClosing, FormOwnerClosing, ApplicationExitCall };

		public bool Cancel { get; set; }

		public WidgetClosingEnventArgs()
		{
		}
	}
}