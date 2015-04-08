using MatterHackers.VectorMath;
using System;

namespace MatterHackers.Agg.UI
{
	public class MouseEventArgs : EventArgs
	{
		private MouseButtons mouseButtons;
		private int numClicks;
		private double x;
		private double y;
		private int wheelDelta;

		//public MouseEventArgs(MouseButtons button, int clicks, int x, int y, int wheelDelta)
		//: this

		public MouseEventArgs(MouseEventArgs original, double newX, double newY)
			: this(original.Button, original.Clicks, newX, newY, original.WheelDelta)
		{
		}

		public MouseEventArgs(MouseButtons button, int clicks, double x, double y, int wheelDelta)
		{
			mouseButtons = button;
			numClicks = clicks;
			this.x = x;
			this.y = y;
			this.wheelDelta = wheelDelta;
		}

		public MouseButtons Button { get { return mouseButtons; } }

		public int Clicks { get { return numClicks; } }

		public int WheelDelta { get { return wheelDelta; } set { wheelDelta = value; } }

		//public Point Location { get; }
		public double X { get { return x; } set { x = value; } }

		public double Y { get { return y; } set { y = value; } }

		public Vector2 Position { get { return new Vector2(x, y); } }
	}

	public enum FlingDirection
	{
		Up,
		Down,
		Left,
		Right
	}

	public class FlingEventArgs : EventArgs
	{
		private FlingDirection direction;
		private double x;
		private double y;

		public FlingEventArgs(double originX, double originY, FlingDirection flingDirection)
		{
			this.direction = flingDirection;
			this.x = originX;
			this.y = originY;
		}

		public FlingDirection Direction { get { return direction; } }

		public double X { get { return x; } set { x = value; } }

		public double Y { get { return y; } set { y = value; } }

		public Vector2 Position { get { return new Vector2(x, y); } }
	}
}