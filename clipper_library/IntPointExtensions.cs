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

using System;

namespace ClipperLib
{
    public enum Intersection
    {
        None,
        Colinear,
        Intersect
    }

    public static class IntPointExtensions
    {
        public static bool CalcIntersection(IntPoint a1,
            IntPoint a2,
            IntPoint b1,
            IntPoint b2,
            out IntPoint position)
        {
            position = default(IntPoint);

            long intersection_epsilon = 1;
            long num = (a1.Y - b1.Y) * (b2.X - b1.X) - (a1.X - b1.X) * (b2.Y - b1.Y);
            long den = (a2.X - a1.X) * (b2.Y - b1.Y) - (a2.Y - a1.Y) * (b2.X - b1.X);
            if (Math.Abs(den) < intersection_epsilon)
            {
                return false;
            }

            position.X = a1.X + (a2.X - a1.X) * num / den;
            position.Y = a1.Y + (a2.Y - a1.Y) * num / den;

            return true;
        }

        public static long Cross(this IntPoint left, IntPoint right)
        {
            return left.X * right.Y - left.Y * right.X;
        }

        public static bool DoIntersect(IntPoint startA, IntPoint endA, IntPoint startB, IntPoint endB)
        {
            return GetIntersection(startA, endA, startB, endB) != Intersection.None;
        }

        public static long Dot(this IntPoint point1, IntPoint point2)
        {
            return point1.X * point2.X + point1.Y * point2.Y;
        }

        public static Intersection GetIntersection(IntPoint startA, IntPoint endA, IntPoint startB, IntPoint endB)
        {
            // Find the four orientations needed for general and
            // special cases
            int o1 = Orientation(startA, endA, startB);
            int o2 = Orientation(startA, endA, endB);
            int o3 = Orientation(startB, endB, startA);
            int o4 = Orientation(startB, endB, endA);

            // Special Cases
            // startA, endA and startB are collinear and startB lies on segment startA endA
            if (o1 == 0 && startB.OnSegment(startA, endA)) return Intersection.Colinear;

            // startA, endA and startB are collinear and endB lies on segment startA-endA
            if (o2 == 0 && endB.OnSegment(startA, endA)) return Intersection.Colinear;

            // startB, endB and startA are collinear and startA lies on segment startB-endB
            if (o3 == 0 && startA.OnSegment(startB, endB)) return Intersection.Colinear;
            
            // startB, endB and endA are collinear and endA lies on segment startB-endB
            if (o4 == 0 && endA.OnSegment(startB, endB)) return Intersection.Colinear;

            // General case
            if (o1 != o2 && o3 != o4)
            {
                return Intersection.Intersect;
            }

            return Intersection.None; // Doesn't fall in any of the above cases
        }

        public static IntPoint GetLength(this IntPoint pointToSet, long len)
        {
            long _len = pointToSet.Length();
            if (_len < 1)
            {
                return new IntPoint(len, 0);
            }

            return pointToSet * len / _len;
        }

        public static IntPoint GetPerpendicularLeft(this IntPoint startingDirection)
        {
            return new IntPoint(-startingDirection.Y, startingDirection.X);
        }

        public static IntPoint GetPerpendicularRight(this IntPoint startingDirection)
        {
            return new IntPoint(startingDirection.Y, -startingDirection.X);
        }

        public static IntPoint GetRotated(this IntPoint thisPoint, double radians)
        {
            double cos = (double)Math.Cos(radians);
            double sin = (double)Math.Sin(radians);

            IntPoint output;
            output.X = (long)(Math.Round(thisPoint.X * cos - thisPoint.Y * sin));
            output.Y = (long)(Math.Round(thisPoint.Y * cos + thisPoint.X * sin));

            return output;
        }

        public static double GetTurnAmount(this IntPoint currentPoint, IntPoint prevPoint, IntPoint nextPoint)
        {
            if (prevPoint != currentPoint
                && currentPoint != nextPoint
                && nextPoint != prevPoint)
            {
                prevPoint = currentPoint - prevPoint;
                nextPoint -= currentPoint;

                double prevAngle = Math.Atan2(prevPoint.Y, prevPoint.X);
                IntPoint rotatedPrev = prevPoint.GetRotated(-prevAngle);

                // undo the rotation
                nextPoint = nextPoint.GetRotated(-prevAngle);
                double angle = Math.Atan2(nextPoint.Y, nextPoint.X);

                return angle;
            }

            return 0;
        }

        public static bool IsShorterThen(this IntPoint pointToCheck, long len)
        {
            if (pointToCheck.X > len || pointToCheck.X < -len)
            {
                return false;
            }

            if (pointToCheck.Y > len || pointToCheck.Y < -len)
            {
                return false;
            }

            return pointToCheck.LengthSquared() <= len * len;
        }

        public static long Length(this IntPoint pointToMeasure)
        {
            return (long)Math.Sqrt(pointToMeasure.LengthSquared());
        }

        public static long LengthSquared(this IntPoint pointToMeasure)
        {
            return pointToMeasure.X * pointToMeasure.X + pointToMeasure.Y * pointToMeasure.Y;
        }

        public static bool OnSegment(this IntPoint testPosition, IntPoint start, IntPoint end)
        {
            if (start == end)
            {
                if (testPosition == start)
                {
                    return true;
                }

                return false;
            }

            IntPoint segmentDelta = end - start;
            long segmentLength = segmentDelta.Length();
            IntPoint pointRelStart = testPosition - start;
            long distanceFromStart = segmentDelta.Dot(pointRelStart) / segmentLength;

            if (distanceFromStart >= 0 && distanceFromStart <= segmentLength)
            {
                IntPoint segmentDeltaLeft = segmentDelta.GetPerpendicularLeft();
                long distanceFromStartLeft = segmentDeltaLeft.Dot(pointRelStart) / segmentLength;

                if (distanceFromStartLeft == 0)
                {
                    return true;
                }
            }

            return false;
        }

        // To find orientation of ordered triplet (p, q, r).
        // The function returns following values
        // 0 --> p, q and r are collinear
        // 1 --> Clockwise
        // 2 --> Counterclockwise
        public static int Orientation(IntPoint start, IntPoint end, IntPoint test)
        {
            // See http://www.geeksforgeeks.org/orientation-3-ordered-points/
            // for details of below formula.
            long val = (end.Y - start.Y) * (test.X - end.X) -
                      (end.X - start.X) * (test.Y - end.Y);

            if (val == 0)
            {
                return 0;
            }

            return (val > 0) ? 1 : 2; // clockwise or counterclockwise
        }
    }
}