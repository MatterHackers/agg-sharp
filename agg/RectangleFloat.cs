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
	public struct RectangleFloat
	{
		public float Left, Bottom, Right, Top;

		public static readonly RectangleFloat ZeroIntersection = new RectangleFloat(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

		public RectangleFloat(float left, float bottom, float right, float top)
		{
			this.Left = left;
			this.Bottom = bottom;
			this.Right = right;
			this.Top = top;
		}

		public RectangleFloat(RectangleInt intRect)
		{
			Left = intRect.Left;
			Bottom = intRect.Bottom;
			Right = intRect.Right;
			Top = intRect.Top;
		}

		public void SetRect(float left, float bottom, float right, float top)
		{
			init(left, bottom, right, top);
		}

		public static bool operator ==(RectangleFloat a, RectangleFloat b)
		{
			if (a.Left == b.Left && a.Bottom == b.Bottom && a.Right == b.Right && a.Top == b.Top)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(RectangleFloat a, RectangleFloat b)
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
			if (obj.GetType() == typeof(RectangleFloat))
			{
				return this == (RectangleFloat)obj;
			}
			return false;
		}

		public bool Equals(RectangleFloat other, float epsilon)
		{
			return Math.Abs(Left - other.Left) <= epsilon
				&& Math.Abs(Bottom - other.Bottom) <= epsilon
				&& Math.Abs(Right - other.Right) <= epsilon
				&& Math.Abs(Top - other.Top) <= epsilon;
		}

		public void init(float left, float bottom, float right, float top)
		{
			Left = left;
			Bottom = bottom;
			Right = right;
			Top = top;
		}

		// This function assumes the rect is normalized
		[JsonIgnoreAttribute]
		public float Width
		{
			get
			{
				return Right - Left;
			}
		}

		// This function assumes the rect is normalized
		[JsonIgnoreAttribute]
		public float Height
		{
			get
			{
				return Top - Bottom;
			}
		}

		public RectangleFloat normalize()
		{
			float t;
			if (Left > Right) { t = Left; Left = Right; Right = t; }
			if (Bottom > Top) { t = Bottom; Bottom = Top; Top = t; }
			return this;
		}

		public bool clip(RectangleFloat r)
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

		public bool Contains(float x, float y)
		{
			return (x >= Left && x <= Right && y >= Bottom && y <= Top);
		}

		public bool Contains(RectangleFloat innerRect)
		{
			if (Contains(innerRect.Left, innerRect.Bottom) && Contains(innerRect.Right, innerRect.Top))
			{
				return true;
			}

			return false;
		}

		public bool Contains(Vector2 position)
		{
			return Contains((float)position.X, (float)position.Y);
		}

		public bool IntersectRectangles(RectangleFloat rectToCopy, RectangleFloat rectToIntersectWith)
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

		public bool IntersectWithRectangle(RectangleFloat rectToIntersectWith)
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

		public void unite_rectangles(RectangleFloat r1, RectangleFloat r2)
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

		public void ExpandToInclude(RectangleFloat rectToInclude)
		{
			if (Right < rectToInclude.Right) Right = rectToInclude.Right;
			if (Top < rectToInclude.Top) Top = rectToInclude.Top;
			if (Left > rectToInclude.Left) Left = rectToInclude.Left;
			if (Bottom > rectToInclude.Bottom) Bottom = rectToInclude.Bottom;
		}

		public void ExpandToInclude(Vector2 position)
		{
			ExpandToInclude((float)position.X, (float)position.Y);
		}

		public void ExpandToInclude(float x, float y)
		{
			if (Right < x) Right = x;
			if (Top < y) Top = y;
			if (Left > x) Left = x;
			if (Bottom > y) Bottom = y;
		}

		public void Inflate(float inflateSize)
		{
			Left = Left - inflateSize;
			Bottom = Bottom - inflateSize;
			Right = Right + inflateSize;
			Top = Top + inflateSize;
		}

		public void Offset(Vector2 offset)
		{
			Offset((float)offset.X, (float)offset.Y);
		}

		public void Offset(float x, float y)
		{
			Left = Left + x;
			Bottom = Bottom + y;
			Right = Right + x;
			Top = Top + y;
		}

		static public RectangleFloat operator *(RectangleFloat a, float b)
		{
			return new RectangleFloat(a.Left * b, a.Bottom * b, a.Right * b, a.Top * b);
		}

		static public RectangleFloat operator *(float b, RectangleFloat a)
		{
			return new RectangleFloat(a.Left * b, a.Bottom * b, a.Right * b, a.Top * b);
		}

		public float XCenter
		{
			get { return (Right - Left) / 2; }
		}

		public float YCenter
		{
			get { return (Top - Bottom) / 2; }
		}

#if false
		public void Inflate(BorderFloat borderFloat)
		{
			Left -= borderFloat.Left;
			Right += borderFloat.Right;
			Bottom -= borderFloat.Bottom;
			Top += borderFloat.Top;
		}

		public void Deflate(BorderFloat borderFloat)
		{
			Left += borderFloat.Left;
			Right -= borderFloat.Right;
			Bottom += borderFloat.Bottom;
			Top -= borderFloat.Top;
		}
#endif

		public override string ToString()
		{
			return string.Format("L:{0}, B:{1}, R:{2}, T:{3}", Left, Bottom, Right, Top);
		}
	}
}