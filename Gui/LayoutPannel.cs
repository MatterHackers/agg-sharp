namespace MatterHackers.Agg.UI
{
	public abstract class LayoutPanel : GuiWidget
	{
		public static BorderDouble DefaultPadding = new BorderDouble(2);
		public static BorderDouble DefaultMargin = new BorderDouble(3);

		public LayoutPanel(HAnchor hAnchor = UI.HAnchor.AbsolutePosition, VAnchor vAnchor = UI.VAnchor.AbsolutePosition)
			: base(hAnchor, vAnchor)
		{
			Padding = DefaultPadding;
			Margin = DefaultMargin;
		}
	}
}