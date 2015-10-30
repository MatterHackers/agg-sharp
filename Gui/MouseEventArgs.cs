using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
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

		public Vector2 Position { get { return new Vector2(x, y); } }
		public double X { get { return x; } set { x = value; } }

		public double Y { get { return y; } set { y = value; } }
	}

	public class MouseEventArgs : EventArgs
	{
		private MouseButtons mouseButtons;
		private int numClicks;
		private List<Vector2> positions = new List<Vector2>();
		private int wheelDelta;

		//public MouseEventArgs(MouseButtons button, int clicks, int x, int y, int wheelDelta)
		//: this

		public MouseEventArgs(MouseEventArgs original, double newX, double newY)
			: this(original.Button, original.Clicks, newX, newY, original.WheelDelta)
		{
			positions[0] = new Vector2(newX, newY);
			for (int i = 1; i < original.NumPositions; i++)
			{
				positions.Add(original.GetPosition(i));
			}
		}

		public MouseEventArgs(MouseButtons button, int clicks, double x, double y, int wheelDelta)
			: this(button, clicks, new Vector2[] { new Vector2(x, y) }, wheelDelta)
		{
		}

        public MouseEventArgs(MouseButtons button, int clicks, Vector2[] positions, int wheelDelta)
		{
			mouseButtons = button;
			numClicks = clicks;

			this.positions = new List<Vector2>(positions);
			this.wheelDelta = wheelDelta;
		}

		public MouseButtons Button { get { return mouseButtons; } }

		public int Clicks { get { return numClicks; } }

		public int NumPositions
		{
			get
			{
				return positions.Count;
			}
		}

		public Vector2 Position { get { return positions[0]; } }

		public int WheelDelta { get { return wheelDelta; } set { wheelDelta = value; } }

		//public Point Location { get; }
		public double X { get { return positions[0].x; } set { positions[0] = new Vector2(value, positions[0].y); } }

		public double Y { get { return positions[0].y; } set { positions[0] = new Vector2(positions[0].x, value); } }

		public Vector2 GetPosition(int index)
		{
			if (index < positions.Count)
			{
				return positions[index];
			}

			return positions[0];
		}
	}
}