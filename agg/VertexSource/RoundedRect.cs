using MatterHackers.VectorMath;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
//
// Rounded rectangle vertex generator
//
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.VertexSource
{
	//------------------------------------------------------------rounded_rect
	//
	// See Implementation agg_rounded_rect.cpp
	//
	public class RoundedRect : IVertexSource
	{
		private RectangleDouble bounds;
		private Vector2 leftBottomRadius;
		private Vector2 rightBottomRadius;
		private Vector2 rightTopRadius;
		private Vector2 leftTopRadius;
		private int state;
		private Arc currentProcessingArc = new Arc();

		public RoundedRect(double left, double bottom, double right, double top, double radius)
		{
			bounds = new RectangleDouble(left, bottom, right, top);
			leftBottomRadius.x = radius;
			leftBottomRadius.y = radius;
			rightBottomRadius.x = radius;
			rightBottomRadius.y = radius;
			rightTopRadius.x = radius;
			rightTopRadius.y = radius;
			leftTopRadius.x = radius;
			leftTopRadius.y = radius;

			if (left > right)
			{
				bounds.Left = right;
				bounds.Right = left;
			}

			if (bottom > top)
			{
				bounds.Bottom = top;
				bounds.Top = bottom;
			}
		}

		public RoundedRect(RectangleDouble bounds, double r)
			: this(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top, r)
		{
		}

		public RoundedRect(RectangleInt bounds, double r)
			: this(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top, r)
		{
		}

		public void rect(double left, double bottom, double right, double top)
		{
			bounds = new RectangleDouble(left, bottom, right, top);
			if (left > right) { bounds.Left = right; bounds.Right = left; }
			if (bottom > top) { bounds.Bottom = top; bounds.Top = bottom; }
		}

		public void radius(double r)
		{
			leftBottomRadius.x = leftBottomRadius.y = rightBottomRadius.x = rightBottomRadius.y = rightTopRadius.x = rightTopRadius.y = leftTopRadius.x = leftTopRadius.y = r;
		}

		public void radius(double rx, double ry)
		{
			leftBottomRadius.x = rightBottomRadius.x = rightTopRadius.x = leftTopRadius.x = rx;
			leftBottomRadius.y = rightBottomRadius.y = rightTopRadius.y = leftTopRadius.y = ry;
		}

		public void radius(double leftBottomRadius, double rightBottomRadius, double rightTopRadius, double leftTopRadius)
		{
			this.leftBottomRadius = new Vector2(leftBottomRadius, leftBottomRadius);
			this.rightBottomRadius = new Vector2(rightBottomRadius, rightBottomRadius);
			this.rightTopRadius = new Vector2(rightTopRadius, rightTopRadius);
			this.leftTopRadius = new Vector2(leftTopRadius, leftTopRadius);
		}

		public void radius(double rx1, double ry1, double rx2, double ry2,
							  double rx3, double ry3, double rx4, double ry4)
		{
			leftBottomRadius.x = rx1; leftBottomRadius.y = ry1; rightBottomRadius.x = rx2; rightBottomRadius.y = ry2;
			rightTopRadius.x = rx3; rightTopRadius.y = ry3; leftTopRadius.x = rx4; leftTopRadius.y = ry4;
		}

		public void normalize_radius()
		{
			double dx = Math.Abs(bounds.Top - bounds.Bottom);
			double dy = Math.Abs(bounds.Right - bounds.Left);

			double k = 1.0;
			double t;
			t = dx / (leftBottomRadius.x + rightBottomRadius.x); if (t < k) k = t;
			t = dx / (rightTopRadius.x + leftTopRadius.x); if (t < k) k = t;
			t = dy / (leftBottomRadius.y + rightBottomRadius.y); if (t < k) k = t;
			t = dy / (rightTopRadius.y + leftTopRadius.y); if (t < k) k = t;

			if (k < 1.0)
			{
				leftBottomRadius.x *= k; leftBottomRadius.y *= k; rightBottomRadius.x *= k; rightBottomRadius.y *= k;
				rightTopRadius.x *= k; rightTopRadius.y *= k; leftTopRadius.x *= k; leftTopRadius.y *= k;
			}
		}

		public void approximation_scale(double s)
		{
			currentProcessingArc.approximation_scale(s);
		}

		public double approximation_scale()
		{
			return currentProcessingArc.approximation_scale();
		}

		public IEnumerable<VertexData> Vertices()
		{
			currentProcessingArc.init(bounds.Left + leftBottomRadius.x, bounds.Bottom + leftBottomRadius.y, leftBottomRadius.x, leftBottomRadius.y, Math.PI, Math.PI + Math.PI * 0.5);
			foreach (VertexData vertexData in currentProcessingArc.Vertices())
			{
				if (ShapePath.is_stop(vertexData.command))
				{
					break;
				}
				yield return vertexData;
			}
			currentProcessingArc.init(bounds.Right - rightBottomRadius.x, bounds.Bottom + rightBottomRadius.y, rightBottomRadius.x, rightBottomRadius.y, Math.PI + Math.PI * 0.5, 0.0);
			foreach (VertexData vertexData in currentProcessingArc.Vertices())
			{
				if (ShapePath.is_move_to(vertexData.command))
				{
					// skip the initial moveto
					continue;
				}
				if (ShapePath.is_stop(vertexData.command))
				{
					break;
				}
				yield return vertexData;
			}

			currentProcessingArc.init(bounds.Right - rightTopRadius.x, bounds.Top - rightTopRadius.y, rightTopRadius.x, rightTopRadius.y, 0.0, Math.PI * 0.5);
			foreach (VertexData vertexData in currentProcessingArc.Vertices())
			{
				if (ShapePath.is_move_to(vertexData.command))
				{
					// skip the initial moveto
					continue;
				}
				if (ShapePath.is_stop(vertexData.command))
				{
					break;
				}
				yield return vertexData;
			}

			currentProcessingArc.init(bounds.Left + leftTopRadius.x, bounds.Top - leftTopRadius.y, leftTopRadius.x, leftTopRadius.y, Math.PI * 0.5, Math.PI);
			foreach (VertexData vertexData in currentProcessingArc.Vertices())
			{
				if (ShapePath.is_move_to(vertexData.command))
				{
					// skip the initial moveto
					continue;
				}
				if (ShapePath.is_stop(vertexData.command))
				{
					break;
				}
				yield return vertexData;
			}

			yield return new VertexData(ShapePath.FlagsAndCommand.CommandEndPoly | ShapePath.FlagsAndCommand.FlagClose | ShapePath.FlagsAndCommand.FlagCCW, new Vector2());
			yield return new VertexData(ShapePath.FlagsAndCommand.CommandStop, new Vector2());
		}

		public void rewind(int unused)
		{
			state = 0;
		}

		public ShapePath.FlagsAndCommand vertex(out double x, out double y)
		{
			x = 0;
			y = 0;
			ShapePath.FlagsAndCommand cmd = ShapePath.FlagsAndCommand.CommandStop;
			switch (state)
			{
				case 0:
					currentProcessingArc.init(bounds.Left + leftBottomRadius.x, bounds.Bottom + leftBottomRadius.y, leftBottomRadius.x, leftBottomRadius.y,
							   Math.PI, Math.PI + Math.PI * 0.5);
					currentProcessingArc.rewind(0);
					state++;
					goto case 1;

				case 1:
					cmd = currentProcessingArc.vertex(out x, out y);
					if (ShapePath.is_stop(cmd))
					{
						state++;
					}
					else
					{
						return cmd;
					}
					goto case 2;

				case 2:
					currentProcessingArc.init(bounds.Right - rightBottomRadius.x, bounds.Bottom + rightBottomRadius.y, rightBottomRadius.x, rightBottomRadius.y,
							   Math.PI + Math.PI * 0.5, 0.0);
					currentProcessingArc.rewind(0);
					state++;
					goto case 3;

				case 3:
					cmd = currentProcessingArc.vertex(out x, out y);
					if (ShapePath.is_stop(cmd))
					{
						state++;
					}
					else
					{
						return ShapePath.FlagsAndCommand.CommandLineTo;
					}
					goto case 4;

				case 4:
					currentProcessingArc.init(bounds.Right - rightTopRadius.x, bounds.Top - rightTopRadius.y, rightTopRadius.x, rightTopRadius.y,
							   0.0, Math.PI * 0.5);
					currentProcessingArc.rewind(0);
					state++;
					goto case 5;

				case 5:
					cmd = currentProcessingArc.vertex(out x, out y);
					if (ShapePath.is_stop(cmd))
					{
						state++;
					}
					else
					{
						return ShapePath.FlagsAndCommand.CommandLineTo;
					}
					goto case 6;

				case 6:
					currentProcessingArc.init(bounds.Left + leftTopRadius.x, bounds.Top - leftTopRadius.y, leftTopRadius.x, leftTopRadius.y,
							   Math.PI * 0.5, Math.PI);
					currentProcessingArc.rewind(0);
					state++;
					goto case 7;

				case 7:
					cmd = currentProcessingArc.vertex(out x, out y);
					if (ShapePath.is_stop(cmd))
					{
						state++;
					}
					else
					{
						return ShapePath.FlagsAndCommand.CommandLineTo;
					}
					goto case 8;

				case 8:
					cmd = ShapePath.FlagsAndCommand.CommandEndPoly
						| ShapePath.FlagsAndCommand.FlagClose
						| ShapePath.FlagsAndCommand.FlagCCW;
					state++;
					break;
			}
			return cmd;
		}
	};
}