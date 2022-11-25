/*
Copyright (c) 2022, Lars Brubaker
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
    public struct RectangleLong
    {
        public long Left, Bottom, Right, Top;

        public long minX { get => Left; set => Left = value; }
        public long minY { get => Bottom; set => Bottom = value; }
        public long maxX { get => Right; set => Right = value; }
        public long maxY { get => Top; set => Top = value; }
        

        public static readonly RectangleLong ZeroIntersection = new RectangleLong(long.MaxValue, long.MaxValue, long.MinValue, long.MinValue);

        public RectangleLong(long left, long bottom, long right, long top)
        {
            Left = left;
            Bottom = bottom;
            Right = right;
            Top = top;
        }

        // This function assumes the rect is normalized
        [JsonIgnoreAttribute]
        public long Height
        {
            get
            {
                return Top - Bottom;
            }
        }

        // This function assumes the rect is normalized
        [JsonIgnoreAttribute]
        public long Width
        {
            get
            {
                return Right - Left;
            }
        }

        public static bool ClipRect(RectangleLong pBoundingRect, ref RectangleLong pDestRect)
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

        public static bool ClipRects(RectangleLong pBoundingRect, ref RectangleLong pSourceRect, ref RectangleLong pDestRect)
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
                // This type of clipping only works when we aren't scaling an image...
                // If we are scaling an image, the source and dest sizes won't match
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

        public static bool DoIntersect(RectangleLong rect1, RectangleLong rect2)
        {
            long x1 = rect1.Left;
            long y1 = rect1.Bottom;
            long x2 = rect1.Right;
            long y2 = rect1.Top;

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

        public bool clip(RectangleLong r)
        {
            if (Right > r.Right) Right = r.Right;
            if (Top > r.Top) Top = r.Top;
            if (Left < r.Left) Left = r.Left;
            if (Bottom < r.Bottom) Bottom = r.Bottom;
            return Left <= Right && Bottom <= Top;
        }

        public void ExpandToInclude(RectangleLong rectToInclude)
        {
            if (Right < rectToInclude.Right) Right = rectToInclude.Right;
            if (Top < rectToInclude.Top) Top = rectToInclude.Top;
            if (Left > rectToInclude.Left) Left = rectToInclude.Left;
            if (Bottom > rectToInclude.Bottom) Bottom = rectToInclude.Bottom;
        }

        public void ExpandToInclude(long x, long y)
        {
            if (Right < x) Right = x;
            if (Top < y) Top = y;
            if (Left > x) Left = x;
            if (Bottom > y) Bottom = y;
        }

        public override int GetHashCode()
        {
            var hash = Left.GetLongHashCode(Bottom.GetLongHashCode(Right.GetLongHashCode(Top.GetLongHashCode())));
            return (int)hash;
        }

        public bool hit_test(long x, long y)
        {
            return (x >= Left && x <= Right && y >= Bottom && y <= Top);
        }

        public void Inflate(long inflateSize)
        {
            Left = Left - inflateSize;
            Bottom = Bottom - inflateSize;
            Right = Right + inflateSize;
            Top = Top + inflateSize;
        }

        public void init(long x1_, long y1_, long x2_, long y2_)
        {
            Left = x1_;
            Bottom = y1_;
            Right = x2_;
            Top = y2_;
        }

        public bool IntersectRectangles(RectangleLong rectToCopy, RectangleLong rectToIntersectWith)
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

        public bool IntersectWithRectangle(RectangleLong rectToIntersectWith)
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

        public bool is_valid()
        {
            return Left <= Right && Bottom <= Top;
        }

        public RectangleLong normalize()
        {
            long t;
            if (Left > Right) { t = Left; Left = Right; Right = t; }
            if (Bottom > Top) { t = Bottom; Bottom = Top; Top = t; }
            return this;
        }

        public void Offset(long x, long y)
        {
            Left = Left + x;
            Bottom = Bottom + y;
            Right = Right + x;
            Top = Top + y;
        }

        public void SetRect(long left, long bottom, long right, long top)
        {
            init(left, bottom, right, top);
        }

        //---------------------------------------------------------unite_rectangles
        public void unite_rectangles(RectangleLong r1, RectangleLong r2)
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
    }
}