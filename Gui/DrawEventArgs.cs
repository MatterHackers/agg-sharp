using System;

namespace MatterHackers.Agg.UI
{
	public delegate void DrawEventHandler(GuiWidget drawingWidget, DrawEventArgs e);

	public class DrawEventArgs : EventArgs
	{
		private Graphics2D internal_graphics2D;

		public Graphics2D graphics2D
		{
			get { return internal_graphics2D; }
		}

		public DrawEventArgs(Graphics2D graphics2D)
		{
			this.internal_graphics2D = graphics2D;
		}
	}
}