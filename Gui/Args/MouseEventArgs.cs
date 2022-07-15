using System;
using System.Collections.Generic;
using MatterHackers.VectorMath;

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
		private List<Vector2> positions = new List<Vector2>();

		public List<string> DragFiles { get; private set; } = null;

		private bool acceptDrop = false;

		public bool AcceptDrop
		{
			get
			{
				return acceptDrop;
			}

			set
			{
				if (value != acceptDrop)
				{
					acceptDrop = value;
				}
			}
		}

		public MouseEventArgs(MouseEventArgs original, double newX, double newY)
			: this(original.Button, original.Clicks, newX, newY, original.WheelDelta, original.DragFiles)
		{
			positions[0] = new Vector2(newX, newY);
			for (int i = 1; i < original.NumPositions; i++)
			{
				positions.Add(original.GetPosition(i));
			}
		}

		public MouseEventArgs(MouseButtons button, int clicks, double x, double y, int wheelDelta, List<string> dragDropFiles = null)
			: this(button, clicks, new Vector2[] { new Vector2(x, y) }, wheelDelta, dragDropFiles)
		{
		}

		public MouseEventArgs(MouseButtons button, int clicks, Vector2[] positions, int wheelDelta, List<string> dragDropFiles)
		{
			Button = button;
			Clicks = clicks;
			DragFiles = dragDropFiles;

			this.positions = new List<Vector2>(positions);
			this.WheelDelta = wheelDelta;
		}

		public MouseButtons Button { get; private set; }

		public int Clicks { get; private set; }

		public int NumPositions
		{
			get
			{
				return positions.Count;
			}
		}

		public Vector2 Position { get { return positions[0]; } }

		public int WheelDelta { get; set; }

		// public Point Location { get; }
		public double X { get { return positions[0].X; } set { positions[0] = new Vector2(value, positions[0].Y); } }

		public double Y { get { return positions[0].Y; } set { positions[0] = new Vector2(positions[0].X, value); } }

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