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

using MatterHackers.Agg;
using System;
using System.Collections.Generic;

namespace ClipperLib
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public static class PolygonExtensions
    {
        public static IntRect ExpandToInclude(this IntRect inRect, IntRect otherRect)
        {
            if (otherRect.minX < inRect.minX) inRect.minX = otherRect.minX;
            if (otherRect.minY < inRect.minY) inRect.minY = otherRect.minY;
            if (otherRect.maxX > inRect.maxX) inRect.maxX = otherRect.maxX;
            if (otherRect.maxY > inRect.maxY) inRect.maxY = otherRect.maxY;

            return inRect;
        }

        public static RectangleDouble GetBounds(this Polygon poly)
        {
            RectangleDouble bounds = RectangleDouble.ZeroIntersection;
            foreach (var point in poly)
            {
                bounds.ExpandToInclude(point.X, point.Y);
            }

            return bounds;
        }

        public static RectangleLong GetBoundsLong(this Polygon poly)
        {
            var bounds = RectangleLong.ZeroIntersection;
            foreach (var point in poly)
            {
                bounds.ExpandToInclude(point.X, point.Y);
            }

            return bounds;
        }

        public static IEnumerable<(int pointIndex, Intersection intersection, IntPoint position)> GetIntersections(this Polygon polygon, IntPoint start, IntPoint end)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                IntPoint edgeStart = polygon[i];
                IntPoint edgeEnd = polygon[(i + 1) % polygon.Count];

                var intersection = IntPointExtensions.GetIntersection(start, end, edgeStart, edgeEnd);
                if (intersection != Intersection.None)
                {
                    IntPointExtensions.CalcIntersection(start, end, edgeStart, edgeEnd, out IntPoint position);
                    yield return (i, intersection, position);
                }
            }
        }

        /// <summary>
        /// Return 1 if ccw -1 if cw
        /// </summary>
        /// <param name="polygon"></param>
        /// <returns></returns>
        public static int GetWindingDirection(this Polygon polygon)
        {
            int pointCount = polygon.Count;
            double totalTurns = 0;
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                IntPoint currentPoint = polygon[pointIndex];
                int nextIndex = (pointIndex + 1) % pointCount;
                IntPoint nextPoint = polygon[nextIndex];

                // sum  (x2 − x1)(y2 + y1)
                double turnAmount = (nextPoint.X + currentPoint.X) * (nextPoint.Y + currentPoint.Y);

                totalTurns += turnAmount;
            }

            return totalTurns > 0 ? 1 : -1;
        }

        public static int GetWindingDirection2(this Polygon polygon)
        {
            int pointCount = polygon.Count;
            double totalTurns = 0;
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                int prevIndex = ((pointIndex + pointCount - 1) % pointCount);
                int nextIndex = ((pointIndex + 1) % pointCount);
                IntPoint prevPoint = polygon[prevIndex];
                IntPoint currentPoint = polygon[pointIndex];
                IntPoint nextPoint = polygon[nextIndex];

                double turnAmount = currentPoint.GetTurnAmount(prevPoint, nextPoint);

                totalTurns += turnAmount;
            }

            return totalTurns > 0 ? 1 : -1;
        }

        public static double Length(this Polygon polygon, bool isClosed = true)
        {
            return Math.Sqrt(polygon.LengthSquared(isClosed));
        }

        public static double LengthSquared(this Polygon polygon, bool isClosed = true)
        {
            double length = 0;
            if (polygon.Count > 1)
            {
                IntPoint previousPoint = polygon[0];
                if (isClosed)
                {
                    previousPoint = polygon[polygon.Count - 1];
                }
                for (int i = isClosed ? 0 : 1; i < polygon.Count; i++)
                {
                    IntPoint currentPoint = polygon[i];
                    length += (previousPoint - currentPoint).LengthSquared();
                    previousPoint = currentPoint;
                }
            }

            return length;
        }

        public static Polygons Offset(this Polygon polygon, double distance)
        {
            var offseter = new ClipperOffset();
            offseter.AddPath(polygon, JoinType.jtRound, EndType.etClosedPolygon);

            var solution = new Polygons();
            offseter.Execute(ref solution, distance);

            return solution;
        }

        public static IntPoint PositionAllongPath(this Polygon polygon, double ratioAlongPath, bool isClosed = true)
        {
            var position = new IntPoint();
            var totalLength = polygon.Length(isClosed);
            var distanceToGoal = (long)(totalLength * ratioAlongPath + .5);
            long length = 0;
            if (polygon.Count > 1)
            {
                position = polygon[0];
                IntPoint currentPoint = polygon[0];

                int polygonCount = polygon.Count;
                for (int i = 1; i < (isClosed ? polygonCount + 1 : polygonCount); i++)
                {
                    IntPoint nextPoint = polygon[i % polygonCount];
                    var segmentLength = (nextPoint - currentPoint).Length();
                    if (length + segmentLength > distanceToGoal)
                    {
                        // return the distance along this segment
                        var distanceAlongThisSegment = distanceToGoal - length;
                        var delteFromCurrent = (nextPoint - currentPoint) * distanceAlongThisSegment / segmentLength;
                        return currentPoint + delteFromCurrent;
                    }
                    position = nextPoint;
                    length += segmentLength;
                    currentPoint = nextPoint;
                }
            }

            return position;
        }

        public static Polygon Rotate(this Polygon poly, double radians)
        {
            var output = new Polygon(poly.Count);

            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);

            foreach (var point in poly)
            {
                output.Add(new IntPoint(point.X * cos - point.Y * sin, point.Y * cos + point.X * sin));
            }

            return output;
        }

        public static Polygon Scale(this Polygon poly, double scaleX, double scaleY)
        {
            var output = new Polygon(poly.Count);
            foreach (var point in poly)
            {
                output.Add(new IntPoint(point.X * scaleX, point.Y * scaleY));
            }

            return output;
        }

        public static Polygons Subtract(this Polygon polygon, Polygons other)
        {
            return new Polygons() { polygon }.CombinePolygons(other, ClipType.ctDifference);
        }

        public static Polygons Subtract(this Polygon polygon, Polygon other)
        {
            return new Polygons() { polygon }.CombinePolygons(new Polygons() { other }, ClipType.ctDifference);
        }

        public static Polygon Translate(this Polygon poly, double x, double y, double scale = 1)
        {
            var output = new Polygon(poly.Count);
            foreach (var point in poly)
            {
                output.Add(new IntPoint(point.X + x * scale, point.Y + y * scale));
            }

            return output;
        }
    }
}