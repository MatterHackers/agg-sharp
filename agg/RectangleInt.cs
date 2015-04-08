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

using Newtonsoft.Json;
using System;

namespace MatterHackers.Agg
{
	public struct RectangleInt
	{
		public int Left, Bottom, Right, Top;

		public RectangleInt(int left, int bottom, int right, int top)
		{
			Left = left;
			Bottom = bottom;
			Right = right;
			Top = top;
		}

		public void SetRect(int left, int bottom, int right, int top)
		{
			init(left, bottom, right, top);
		}

		public void init(int x1_, int y1_, int x2_, int y2_)
		{
			Left = x1_;
			Bottom = y1_;
			Right = x2_;
			Top = y2_;
		}

		// This function assumes the rect is normalized
		[JsonIgnoreAttribute]
		public int Width
		{
			get
			{
				return Right - Left;
			}
		}

		// This function assumes the rect is normalized
		[JsonIgnoreAttribute]
		public int Height
		{
			get
			{
				return Top - Bottom;
			}
		}

		public RectangleInt normalize()
		{
			int t;
			if (Left > Right) { t = Left; Left = Right; Right = t; }
			if (Bottom > Top) { t = Bottom; Bottom = Top; Top = t; }
			return this;
		}

		public void ExpandToInclude(RectangleInt rectToInclude)
		{
			if (Right < rectToInclude.Right) Right = rectToInclude.Right;
			if (Top < rectToInclude.Top) Top = rectToInclude.Top;
			if (Left > rectToInclude.Left) Left = rectToInclude.Left;
			if (Bottom > rectToInclude.Bottom) Bottom = rectToInclude.Bottom;
		}

		public bool clip(RectangleInt r)
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

		public bool hit_test(int x, int y)
		{
			return (x >= Left && x <= Right && y >= Bottom && y <= Top);
		}

		public bool IntersectRectangles(RectangleInt rectToCopy, RectangleInt rectToIntersectWith)
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

		public bool IntersectWithRectangle(RectangleInt rectToIntersectWith)
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

		public static bool DoIntersect(RectangleInt rect1, RectangleInt rect2)
		{
			int x1 = rect1.Left;
			int y1 = rect1.Bottom;
			int x2 = rect1.Right;
			int y2 = rect1.Top;

			if (x1 < rect2.Left) x1 = rect2.Left;
			if (y1 < rect2.Bottom) y1 = rect2.Bottom;
			if (x2 > rect2.Right) x2 = rect2.Right;
			if (y2 > rect2.Top) y2 = rect2.Top;

			if (x1 < x2 && y1 < y2)
			{
				return true;
			}

			return false;
		}

		//---------------------------------------------------------unite_rectangles
		public void unite_rectangles(RectangleInt r1, RectangleInt r2)
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

		public void Inflate(int inflateSize)
		{
			Left = Left - inflateSize;
			Bottom = Bottom - inflateSize;
			Right = Right + inflateSize;
			Top = Top + inflateSize;
		}

		public void Offset(int x, int y)
		{
			Left = Left + x;
			Bottom = Bottom + y;
			Right = Right + x;
			Top = Top + y;
		}

		public override int GetHashCode()
		{
			return new { x1 = Left, x2 = Right, y1 = Bottom, y2 = Top }.GetHashCode();
		}

		public static bool ClipRects(RectangleInt pBoundingRect, ref RectangleInt pSourceRect, ref RectangleInt pDestRect)
		{
			// clip off the top so we don't write into random memory
			if (pDestRect.Top < pBoundingRect.Top)
			{
				// This type of clipping only works when we aren't scaling an image...
				// If we are scaling an image, the source and dest sizes won't match
				if (pSourceRect.Height != pDestRect.Height)
				{
					throw new Exception("source and dest rects must have the same height");
				}

				pSourceRect.Top += pBoundingRect.Top - pDestRect.Top;
				pDestRect.Top = pBoundingRect.Top;
				if (pDestRect.Top >= pDestRect.Bottom)
				{
					return false;
				}
			}
			// clip off the bottom
			if (pDestRect.Bottom > pBoundingRect.Bottom)
			{
				// This type of clipping only works when we arenst scaling an image...
				// If we are scaling an image, the source and desst sizes won't match
				if (pSourceRect.Height != pDestRect.Height)
				{
					throw new Exception("source and dest rects must have the same height");
				}

				pSourceRect.Bottom -= pDestRect.Bottom - pBoundingRect.Bottom;
				pDestRect.Bottom = pBoundingRect.Bottom;
				if (pDestRect.Bottom <= pDestRect.Top)
				{
					return false;
				}
			}

			// clip off the left
			if (pDestRect.Left < pBoundingRect.Left)
			{
				// This type of clipping only works when we aren't scaling an image...
				// If we are scaling an image, the source and dest sizes won't match
				if (pSourceRect.Width != pDestRect.Width)
				{
					throw new Exception("source and dest rects must have the same width");
				}

				pSourceRect.Left += pBoundingRect.Left - pDestRect.Left;
				pDestRect.Left = pBoundingRect.Left;
				if (pDestRect.Left >= pDestRect.Right)
				{
					return false;
				}
			}
			// clip off the right
			if (pDestRect.Right > pBoundingRect.Right)
			{
				// This type of clipping only works when we aren't scaling an image...
				// If we are scaling an image, the source and dest sizes won't match
				if (pSourceRect.Width != pDestRect.Width)
				{
					throw new Exception("source and dest rects must have the same width");
				}

				pSourceRect.Right -= pDestRect.Right - pBoundingRect.Right;
				pDestRect.Right = pBoundingRect.Right;
				if (pDestRect.Right <= pDestRect.Left)
				{
					return false;
				}
			}

			return true;
		}

		//***************************************************************************************************************************************************
		public static bool ClipRect(RectangleInt pBoundingRect, ref RectangleInt pDestRect)
		{
			// clip off the top so we don't write into random memory
			if (pDestRect.Top < pBoundingRect.Top)
			{
				pDestRect.Top = pBoundingRect.Top;
				if (pDestRect.Top >= pDestRect.Bottom)
				{
					return false;
				}
			}
			// clip off the bottom
			if (pDestRect.Bottom > pBoundingRect.Bottom)
			{
				pDestRect.Bottom = pBoundingRect.Bottom;
				if (pDestRect.Bottom <= pDestRect.Top)
				{
					return false;
				}
			}

			// clip off the left
			if (pDestRect.Left < pBoundingRect.Left)
			{
				pDestRect.Left = pBoundingRect.Left;
				if (pDestRect.Left >= pDestRect.Right)
				{
					return false;
				}
			}

			// clip off the right
			if (pDestRect.Right > pBoundingRect.Right)
			{
				pDestRect.Right = pBoundingRect.Right;
				if (pDestRect.Right <= pDestRect.Left)
				{
					return false;
				}
			}

			return true;
		}
	}
}