using System;

namespace MatterHackers.Agg.UI
{
	public class InvalidateEventArgs : EventArgs
	{
		private RectangleDouble invalidRectangle;

		public RectangleDouble InvalidRectangle
		{
			get
			{
				return invalidRectangle;
			}
		}

		public InvalidateEventArgs(RectangleDouble invalidRectangle)
		{
			this.invalidRectangle = invalidRectangle;
		}
	}
}