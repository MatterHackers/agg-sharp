/*
Copyright (c) 2015, Lars Brubaker
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

using ClipperLib;
using System;
using System.Collections.Generic;

namespace PolygonPathing
{
    public static class IntPointHelper
    {
        public static long Cross(this IntPoint left, IntPoint right)
        {
            return left.X * right.Y - left.Y * right.X;
        }

        public static IntPoint CrossZ(this IntPoint thisPoint)
        {
            return new IntPoint(-thisPoint.Y, thisPoint.X);
        }

        public static long Dot(this IntPoint thisPoint, IntPoint otherPoint)
        {
            return thisPoint.X * otherPoint.X + thisPoint.Y * otherPoint.Y;
        }

        public static IntPoint GetPerpendicularLeft(this IntPoint thisPoint)
        {
            return new IntPoint(thisPoint.Y, -thisPoint.X);
        }

        public static IntPoint GetPerpendicularRight(this IntPoint thisPoint)
        {
            return new IntPoint(-thisPoint.Y, thisPoint.X);
        }
        public static long Length(this IntPoint thisPoint)
        {
            return (long)Math.Sqrt(thisPoint.LengthSquared());
        }

        public static long LengthSquared(this IntPoint thisPoint)
        {
            return thisPoint.X * thisPoint.X + thisPoint.Y * thisPoint.Y;
        }

        public static IntPoint Normal(this IntPoint thisPoint, long length)
        {
            long thisLength = thisPoint.Length();
            if (thisLength == 0) // avoid the devide by 0
            {
                return new IntPoint(length, 0);
            }

            return thisPoint * length / thisLength;
        }

        public class IntPointSorterYX : IComparer<IntPoint>
        {
            public virtual int Compare(IntPoint a, IntPoint b)
            {
                if (a.Y == b.Y)
                {
                    return a.X.CompareTo(b.X);
                }
                else
                {
                    return a.Y.CompareTo(b.Y);
                }
            }
        }
    }
}