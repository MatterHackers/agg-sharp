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
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolygonPathing
{
    using Polygon = List<IntPoint>;
    public class Extra
    {
        public static bool Inside(IList<IntPoint> polygon, IntPoint position, bool toleranceOnOutside = true)
        {
            IntPoint point = position;

            const float epsilon = 0.5f;

            bool inside = false;

            // Must have 3 or more edges
            if (polygon.Count < 3) return false;

            IntPoint oldPoint = polygon[polygon.Count - 1];
            float oldSqDist = (oldPoint - point).LengthSquared();

            for (int i = 0; i < polygon.Count; i++)
            {
                IntPoint newPoint = polygon[i];
                float newSqDist = (newPoint - point).LengthSquared();

                if (oldSqDist + newSqDist + 2.0f * System.Math.Sqrt(oldSqDist * newSqDist) - (newPoint - oldPoint).LengthSquared() < epsilon)
                {
                    return toleranceOnOutside;
                }

                IntPoint left;
                IntPoint right;
                if (newPoint.X > oldPoint.X)
                {
                    left = oldPoint;
                    right = newPoint;
                }
                else
                {
                    left = newPoint;
                    right = oldPoint;
                }

                if (left.X < point.X && point.X <= right.X && (point.Y - left.Y) * (right.X - left.X) < (right.Y - left.Y) * (point.X - left.X))
                {
                    inside = !inside;
                }

                oldPoint = newPoint;
                oldSqDist = newSqDist;
            }

            return inside;
        }

        bool InLineOfSight(Polygon polygon, IntPoint start, IntPoint end, long epsilon = 0)
        {
            // Not in LOS if any of the ends is outside the polygon
            if (!Inside(polygon, start) || !Inside(polygon, end)) return false;

            // In LOS if it’s the same start and end location
            if ((start - end).Length() <= epsilon)
            {
                return true;
            }

            // Not in LOS if any edge is intersected by the start-end line segment
            for (int i = 0; i < polygon.Count; i++)
            {
                if (LineSegmentsCross(start, end, polygon[i], polygon[(i + 1) % polygon.Count]))
                {
                    return false;
                }
            }

            // Finally the middle point in the segment determines if in LOS or not
            return Inside(polygon, (start + end) / 2);
        }

        public static bool LineSegmentsCross(IntPoint a, IntPoint b, IntPoint c, IntPoint d)
        {
            float denominator = ((b.X - a.X) * (d.Y - c.Y)) - ((b.Y - a.Y) * (d.X - c.X));

            if (denominator == 0)
            {
                return false;
            }

            float numerator1 = ((a.Y - c.Y) * (d.X - c.X)) - ((a.X - c.X) * (d.Y - c.Y));

            float numerator2 = ((a.Y - c.Y) * (b.X - a.X)) - ((a.X - c.X) * (b.Y - a.Y));

            if (numerator1 == 0 || numerator2 == 0)
            {
                return false;
            }

            float r = numerator1 / denominator;
            float s = numerator2 / denominator;

            return (r > 0 && r < 1) && (s > 0 && s < 1);
        }

    }
}
