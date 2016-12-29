using System;

namespace MatterHackers.Agg.UI
{
	public delegate void DrawEventHandler(GuiWidget drawingWidget, DrawEventArgs e);

	public class DrawEventArgs : EventArgs
	{
		public Graphics2D graphics2D { get; }

		public DrawEventArgs(Graphics2D graphics2D)
		{
			this.graphics2D = graphics2D;
		}
	}
}