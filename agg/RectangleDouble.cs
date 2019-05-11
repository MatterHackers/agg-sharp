/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;

namespace MatterHackers.Agg
{
	public struct RectangleDouble
	{
		public double Left, Bottom, Right, Top;

		public static readonly RectangleDouble ZeroIntersection = new RectangleDouble(double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);

		public RectangleDouble(double left, double bottom, double right, double top)
		{
			this.Left = left;
			this.Bottom = bottom;
			this.Right = right;
			this.Top = top;
		}

		public RectangleDouble(RectangleInt intRect)
		{
			Left = intRect.Left;
			Bottom = intRect.Bottom;
			Right = intRect.Right;
			Top = intRect.Top;
		}

		public RectangleDouble(Vector2 position1, Vector2 position2) :
			this(Math.Min(position1.X, position2.X), Math.Min(position1.Y, position2.Y), Math.Max(position1.X, position2.X), Math.Max(position1.Y, position2.Y))
		{
		}

		public void SetRect(double left, double bottom, double right, double top)
		{
			init(left, bottom, right, top);
		}

		public static bool operator ==(RectangleDouble a, RectangleDouble b)
		{
			if (a.Left == b.Left && a.Bottom == b.Bottom && a.Right == b.Right && a.Top == b.Top)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(RectangleDouble a, RectangleDouble b)
		{
			if (a.Left != b.Left || a.Bottom != b.Bottom || a.Right != b.Right || a.Top != b.Top)
			{
				return true;
			}

			return false;
		}

		public override int GetHashCode()
		{
			return new { x1 = Left, x2 = Right, y1 = Bottom, y2 = Top }.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			if (obj.GetType() == typeof(RectangleDouble))
			{
				return this == (RectangleDouble)obj;
			}
			return false;
		}

		public bool Equals(RectangleDouble other, double epsilon)
		{
			return Math.Abs(Left - other.Left) <= epsilon
				&& Math.Abs(Bottom - other.Bottom) <= epsilon
				&& Math.Abs(Right - other.Right) <= epsilon
				&& Math.Abs(Top - other.Top) <= epsilon;
		}

		public void init(double left, double bottom, double right, double top)
		{
			Left = left;
			Bottom = bottom;
			Right = right;
			Top = top;
		}

		// This function assumes the rect is normalized
		[JsonIgnoreAttribute]
		public double Width
		{
			get
			{
				return Right - Left;
			}
		}

		// This function assumes the rect is normalized
		[JsonIgnoreAttribute]
		public double Height
		{
			get
			{
				return Top - Bottom;
			}
		}

		public RectangleDouble normalize()
		{
			double t;
			if (Left > Right) { t = Left; Left = Right; Right = t; }
			if (Bottom > Top) { t = Bottom; Bottom = Top; Top = t; }
			return this;
		}

		public bool clip(RectangleDouble r)
		{
			if (Right > r.Right) Right = r.Right;
			if (Top > r.Top) Top = r.Top;
			if (Left < r.Left) Left = r.Left;
			if (Bottom < r.Bottom) Bottom = r.Bottom;
			return Left <= Right && Bottom <= Top;
		}

		public bool is_valid()
		{
			return Left <= Right && Bottom <= Top;
		}

		public bool Contains(double x, double y)
		{
			return (x >= Left && x <= Right && y >= Bottom && y <= Top);
		}

		public bool Contains(RectangleDouble innerRect)
		{
			if (Contains(innerRect.Left, innerRect.Bottom) && Contains(innerRect.Right, innerRect.Top))
			{
				return true;
			}

			return false;
		}

		public bool Contains(Vector2 position)
		{
			return Contains(position.X, position.Y);
		}

		public bool Contains(Point2D position)
		{
			return Contains(position.x, position.y);
		}

		public bool IntersectRectangles(RectangleDouble rectToCopy, RectangleDouble rectToIntersectWith)
		{
			Left = rectToCopy.Left;
			Bottom = rectToCopy.Bottom;
			Right = rectToCopy.Right;
			Top = rectToCopy.Top;

			if (Left < rectToIntersectWith.Left) Left = rectToIntersectWith.Left;
			if (Bottom < rectToIntersectWith.Bottom) Bottom = rectToIntersectWith.Bottom;
			if (Right > rectToIntersectWith.Right) Right = rectToIntersectWith.Right;
			if (Top > rectToIntersectWith.Top) Top = rectToIntersectWith.Top;

			if (Left < Right && Bottom < Top)
			{
				return true;
			}

			return false;
		}

		public bool IsTouching(RectangleDouble rectToIntersectWith)
		{
			RectangleDouble temp = this;

			return temp.IntersectWithRectangle(rectToIntersectWith);
		}

		public bool IntersectWithRectangle(RectangleDouble rectToIntersectWith)
		{
			if (Left < rectToIntersectWith.Left) Left = rectToIntersectWith.Left;
			if (Bottom < rectToIntersectWith.Bottom) Bottom = rectToIntersectWith.Bottom;
			if (Right > rectToIntersectWith.Right) Right = rectToIntersectWith.Right;
			if (Top > rectToIntersectWith.Top) Top = rectToIntersectWith.Top;

			if (Left < Right && Bottom < Top)
			{
				return true;
			}

			return false;
		}

		public void unite_rectangles(RectangleDouble r1, RectangleDouble r2)
		{
			Left = r1.Left;
			Bottom = r1.Bottom;
			Right = r1.Right;
			Right = r1.Top;
			if (Right < r2.Right) Right = r2.Right;
			if (Top < r2.Top) Top = r2.Top;
			if (Left > r2.Left) Left = r2.Left;
			if (Bottom > r2.Bottom) Bottom = r2.Bottom;
		}

		public void ExpandToInclude(RectangleDouble rectToInclude)
		{
			if (Right < rectToInclude.Right)
			{
				Right = rectToInclude.Right;
			}

			if (Top < rectToInclude.Top)
			{
				Top = rectToInclude.Top;
			}

			if (Left > rectToInclude.Left)
			{
				Left = rectToInclude.Left;
			}

			if (Bottom > rectToInclude.Bottom)
			{
				Bottom = rectToInclude.Bottom;
			}
		}

		public void ExpandToInclude(Vector2 position)
		{
			ExpandToInclude(position.X, position.Y);
		}

		public void ExpandToInclude(double x, double y)
		{
			if (Right < x) Right = x;
			if (Top < y) Top = y;
			if (Left > x) Left = x;
			if (Bottom > y) Bottom = y;
		}

		public void Inflate(int inflateSize)
		{
			Left = Left - inflateSize;
			Bottom = Bottom - inflateSize;
			Right = Right + inflateSize;
			Top = Top + inflateSize;
		}

		public void Inflate(double inflateSize)
		{
			Left = Left - inflateSize;
			Bottom = Bottom - inflateSize;
			Right = Right + inflateSize;
			Top = Top + inflateSize;
		}

		public void Inflate(BorderDouble borderDouble)
		{
			Left -= borderDouble.Left;
			Right += borderDouble.Right;
			Bottom -= borderDouble.Bottom;
			Top += borderDouble.Top;
		}

		public void Deflate(BorderDouble borderDouble)
		{
			Left += borderDouble.Left;
			Right -= borderDouble.Right;
			Bottom += borderDouble.Bottom;
			Top -= borderDouble.Top;
		}

		public void Offset(Vector2 offset)
		{
			Offset(offset.X, offset.Y);
		}

		public void Offset(double x, double y)
		{
			Left = Left + x;
			Bottom = Bottom + y;
			Right = Right + x;
			Top = Top + y;
		}

		static public RectangleDouble operator *(RectangleDouble a, double b)
		{
			return new RectangleDouble(a.Left * b, a.Bottom * b, a.Right * b, a.Top * b);
		}

		static public RectangleDouble operator *(double b, RectangleDouble a)
		{
			return new RectangleDouble(a.Left * b, a.Bottom * b, a.Right * b, a.Top * b);
		}

		[JsonIgnore]
		public Vector2 Center => new Vector2(XCenter, YCenter);

		[JsonIgnore]
		public double XCenter => (Right + Left) / 2;

		[JsonIgnore]
		public double YCenter => (Top + Bottom) / 2;

		public override string ToString()
		{
			return string.Format("L:{0}, B:{1}, R:{2}, T:{3}", Left, Bottom, Right, Top);
		}

		[Flags]
		public enum OutCode
		{
			Inside = 0,
			Left = 1,
			Right = 2,
			Bottom = 4,
			Top = 8,
			Surrounded = Left | Right | Bottom | Top
		}

		public OutCode ComputeOutCode(double x, double y)
		{
			var code = OutCode.Inside;

			if (x < this.Left) code |= OutCode.Left;
			if (x > this.Right) code |= OutCode.Right;
			if (y < this.Bottom) code |= OutCode.Bottom;
			if (y > this.Top) code |= OutCode.Top;

			return code;
		}

		public OutCode ComputeOutCode(Vector2 p)
		{
			return ComputeOutCode(p.X, p.Y); 
		}

		private Vector2 CalculateIntersection(Vector2 p1, Vector2 p2, OutCode clipTo)
		{
			var dx = (p2.X - p1.X);
			var dy = (p2.Y - p1.Y);

			var slopeY = dx / dy; // slope to use for possibly-vertical lines
			var slopeX = dy / dx; // slope to use for possibly-horizontal lines

			if (clipTo.HasFlag(OutCode.Top))
			{
				return new Vector2(
					p1.X + slopeY * (this.Top - p1.Y),
					this.Top
					);
			}
			if (clipTo.HasFlag(OutCode.Bottom))
			{
				return new Vector2(
					p1.X + slopeY * (this.Bottom - p1.Y),
					this.Bottom
					);
			}
			if (clipTo.HasFlag(OutCode.Right))
			{
				return new Vector2(
					this.Right,
					p1.Y + slopeX * (this.Right - p1.X)
					);
			}
			if (clipTo.HasFlag(OutCode.Left))
			{
				return new Vector2(
					this.Left,
					p1.Y + slopeX * (this.Left - p1.X)
					);
			}
			throw new ArgumentOutOfRangeException("clipTo = " + clipTo);
		}

		private Tuple<Vector2, Vector2> ClipSegment(Vector2 p1, Vector2 p2)
		{
			// classify the endpoints of the line
			var outCodeP1 = this.ComputeOutCode(p1);
			var outCodeP2 = this.ComputeOutCode(p2);
			var accept = false;

			while (true)
			{ // should only iterate twice, at most
			  // Case 1:
			  // both endpoints are within the clipping region
				if ((outCodeP1 | outCodeP2) == OutCode.Inside)
				{
					accept = true;
					break;
				}

				// Case 2:
				// both endpoints share an excluded region, impossible for a line between them to be within the clipping region
				if ((outCodeP1 & outCodeP2) != 0)
				{
					break;
				}

				// Case 3:
				// The endpoints are in different regions, and the segment is partially within the clipping rectangle

				// Select one of the endpoints outside the clipping rectangle
				var outCode = outCodeP1 != OutCode.Inside ? outCodeP1 : outCodeP2;

				// calculate the intersection of the line with the clipping rectangle
				var p = this.CalculateIntersection(p1, p2, outCode);

				// update the point after clipping and recalculate outcode
				if (outCode == outCodeP1)
				{
					p1 = p;
					outCodeP1 = this.ComputeOutCode(p1);
				}
				else
				{
					p2 = p;
					outCodeP2 = this.ComputeOutCode(p2);
				}
			}
			// if clipping area contained a portion of the line
			if (accept)
			{
				return new Tuple<Vector2, Vector2>(p1, p2);
			}

			// the line did not intersect the clipping area
			return null;
		}

		public Vector2 Clamp(Vector2 actualNozzlePosition)
		{
			var newX = Math.Min(Right, Math.Max(Left, actualNozzlePosition.X));
			var newY = Math.Min(Top, Math.Max(Bottom, actualNozzlePosition.Y));
			return new Vector2(newX, newY);
		}

		public bool ClipLine(Vector2 p1, Vector2 p2)
		{
			if (this.ClipSegment(p1, p2) != null)
			{
				return true;
			}

			return false;
		}
	}
}