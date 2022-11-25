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

using ClipperLib;
using KdTree;
using KdTree.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using Polygon = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;

namespace MatterHackers.QuadTree
{
    public enum Intersection
    {
        None,
        Colinear,
        Intersect
    }

    public static class QTPolygonExtensions
    {
        public static double Area(this Polygon polygon)
        {
            return Clipper.Area(polygon);
        }

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

        public static IntPoint Center(this Polygon polygon)
        {
            var center = default(IntPoint);
            for (int positionIndex = 0; positionIndex < polygon.Count; positionIndex++)
            {
                center += polygon[positionIndex];
            }

            center /= polygon.Count;
            return center;
        }

        /// <summary>
        /// Cut off the end of the polygon to change it to be the new length
        /// </summary>
        /// <param name="inPolygon"></param>
        /// <param name="newLength"></param>
        /// <returns></returns>
        public static Polygon CutToLength(this Polygon inPolygon, long newLength, bool isClosed = false)
        {
            var polygon = new Polygon(inPolygon);

            if (polygon.Count > 1)
            {
                var lastPoint = polygon[0];
                for (int i = 1; i < polygon.Count; i++)
                {
                    // Calculate distance between 2 points
                    var currentPoint = polygon[i];
                    long segmentLength = (currentPoint - lastPoint).Length();

                    // If distance exceeds clip distance:
                    //  - Sets the new last path point
                    if (segmentLength > newLength)
                    {
                        if (newLength > 50) // Don't clip segments less than 50 um. We get too much truncation error.
                        {
                            IntPoint dir = (currentPoint - lastPoint) * newLength / segmentLength;

                            IntPoint clippedEndpoint = dir + lastPoint;

                            polygon[i] = clippedEndpoint;
                            return new Polygon(polygon.GetRange(0, i + 1));
                        }
                        else
                        {
                            return new Polygon(polygon.GetRange(0, i));
                        }
                    }
                    else if (segmentLength == newLength)
                    {
                        // Pops off last point because it is at the limit distance
                        return new Polygon(polygon.GetRange(0, i + 1));
                    }
                    else
                    {
                        // Pops last point and reduces distance remaining to target
                        newLength -= segmentLength;
                        lastPoint = currentPoint;
                    }
                }
            }

            return polygon;
        }

        // The main function that returns true if line segment 'startA-endA'
        // and 'startB-endB' intersect.
        public static bool DoIntersect(IntPoint startA, IntPoint endA, IntPoint startB, IntPoint endB)
        {
            return GetIntersection(startA, endA, startB, endB) != Intersection.None;
        }

        public static (int index, IntPoint position) FindClosestPoint(this Polygon polygon, IntPoint position, Func<int, IntPoint, bool> considerPoint = null)
        {
            var polyPointPosition = (-1, new IntPoint());

            long bestDist = long.MaxValue;
            for (int pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
            {
                var point = polygon[pointIndex];
                long length = (point - position).Length();
                if (length < bestDist)
                {
                    if (considerPoint == null || considerPoint(pointIndex, point))
                    {
                        bestDist = length;
                        polyPointPosition = (pointIndex, point);
                    }
                }
            }

            return polyPointPosition;
        }

        public static int FindClosestPositionIndex(this Polygon polygon, IntPoint position, INearestNeighbours<int> nearestNeighbours = null)
        {
            if (nearestNeighbours != null)
            {
                return nearestNeighbours.GetNearestNeighbour(position);
            }
            else
            {
                int bestPointIndex = -1;
                double closestDist = double.MaxValue;
                for (int pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
                {
                    double dist = (polygon[pointIndex] - position).LengthSquared();
                    if (dist < closestDist)
                    {
                        bestPointIndex = pointIndex;
                        closestDist = dist;
                    }
                }

                return bestPointIndex;
            }
        }

        public static IEnumerable<(int pointIndex, IntPoint position)> FindCrossingPoints(this Polygon polygon, IntPoint start, IntPoint end, QuadTree<int> edgeQuadTree = null)
        {
            var edgeIterator = new PolygonEdgeIterator(polygon, 1, edgeQuadTree);
            foreach (var i in edgeIterator.GetTouching(new Quad(start, end)))
            {
                IntPoint edgeStart = polygon[i];
                IntPoint edgeEnd = polygon[(i + 1) % polygon.Count];
                if (OnSegment(edgeStart, start, edgeEnd))
                {
                    yield return (i, start);
                }
                else if (OnSegment(edgeStart, end, edgeEnd))
                {
                    yield return (i, end);
                }
                else if (DoIntersect(start, end, edgeStart, edgeEnd)
                    && CalcIntersection(start, end, edgeStart, edgeEnd, out IntPoint intersection))
                {
                    yield return (i, intersection);
                }
            }
        }

        public static Intersection FindIntersection(this Polygon polygon, IntPoint start, IntPoint end, QuadTree<int> edgeQuadTree = null)
        {
            Intersection bestIntersection = Intersection.None;

            var edgeIterator = new PolygonEdgeIterator(polygon, 1, edgeQuadTree);
            foreach (var i in edgeIterator.GetTouching(new Quad(start, end)))
            {
                IntPoint edgeStart = polygon[i];
                IntPoint edgeEnd = polygon[(i + 1) % polygon.Count];
                // if we share a vertex we cannot be crossing the line
                if (start == edgeStart || start == edgeEnd || end == edgeStart || end == edgeEnd)
                {
                    bestIntersection = Intersection.Colinear;
                }
                else
                {
                    var result = GetIntersection(start, end, edgeStart, edgeEnd);
                    if (result == Intersection.Intersect)
                    {
                        return Intersection.Intersect;
                    }
                    else if (result == Intersection.Colinear)
                    {
                        bestIntersection = Intersection.Colinear;
                    }
                }
            }

            return bestIntersection;
        }

        /// <summary>
        /// Return the point index or -1 if not a vertex of the polygon
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public static int FindPoint(this Polygon polygon, IntPoint position, INearestNeighbours<int> nearestNeighbours = null)
        {
            if (nearestNeighbours != null)
            {
                var index = nearestNeighbours.GetNearestNeighbour(position);
                if (position == polygon[index])
                {
                    return index;
                }
            }
            else
            {
                for (int i = 0; i < polygon.Count; i++)
                {
                    if (position == polygon[i])
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        public static bool FindThinLines(this Polygon polygon, long overlapMergeAmount_um, long minimumRequiredWidth_um, out Polygons onlyMergeLines, bool pathIsClosed = true)
        {
            return QTPolygonsExtensions.FindThinLines(new Polygons { polygon }, overlapMergeAmount_um, minimumRequiredWidth_um, out onlyMergeLines, pathIsClosed);
        }

        public static QuadTree<int> GetEdgeQuadTree(this Polygon polygon, int splitCount = 5, long expandDist = 1)
        {
            var bounds = polygon.GetBoundsLong();
            bounds.Inflate(expandDist);
            var quadTree = new QuadTree<int>(splitCount, bounds.minX, bounds.minY, bounds.maxX, bounds.maxY);
            for (int i = 0; i < polygon.Count; i++)
            {
                var currentPoint = polygon[i];
                var nextPoint = polygon[i == polygon.Count - 1 ? 0 : i + 1];
                quadTree.Insert(i, new Quad(Math.Min(nextPoint.X, currentPoint.X) - expandDist,
                    Math.Min(nextPoint.Y, currentPoint.Y) - expandDist,
                    Math.Max(nextPoint.X, currentPoint.X) + expandDist,
                    Math.Max(nextPoint.Y, currentPoint.Y) + expandDist));
            }

            return quadTree;
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
            if (o1 == 0 && OnSegment(startA, startB, endA)) return Intersection.Colinear;

            // startA, endA and startB are collinear and endB lies on segment startA-endA
            if (o2 == 0 && OnSegment(startA, endB, endA)) return Intersection.Colinear;

            // startB, endB and startA are collinear and startA lies on segment startB-endB
            if (o3 == 0 && OnSegment(startB, startA, endB)) return Intersection.Colinear;

            // startB, endB and endA are collinear and endA lies on segment startB-endB
            if (o4 == 0 && OnSegment(startB, endA, endB)) return Intersection.Colinear;

            // General case
            if (o1 != o2 && o3 != o4)
            {
                return Intersection.Intersect;
            }

            return Intersection.None; // Doesn't fall in any of the above cases
        }

        public static INearestNeighbours<int> GetNearestNeighbourAccelerator(this Polygon polygon)
        {
            // if there are not enough points it is much faster to just iterate the array
            if (polygon.Count < 8)
            {
                return null;
            }

            return new KdTreeNeighbours(polygon);
        }

        public static QuadTree<int> GetPointQuadTree(this Polygon polygon, int splitCount = 5, long expandDist = 1)
        {
            var bounds = polygon.GetBoundsLong();
            bounds.Inflate(expandDist);
            var quadTree = new QuadTree<int>(splitCount, bounds.minX, bounds.minY, bounds.maxX, bounds.maxY);
            for (int i = 0; i < polygon.Count; i++)
            {
                quadTree.Insert(i, polygon[i].X - expandDist, polygon[i].Y - expandDist, polygon[i].X + expandDist, polygon[i].Y + expandDist);
            }

            return quadTree;
        }

        /// <summary>
        /// Return 1 if ccw -1 if cw
        /// </summary>
        /// <param name="polygon"></param>
        /// <returns></returns>
        public static int GetWindingDirection(this Polygon polygon)
        {
            var clipper = Clipper.Area(polygon);
            if (clipper > 0)
            {
                return 1;
            }
            else if (clipper < 0 - 1)
            {
                return -1;
            }

            return 0;
        }

        /// <summary>
        /// Return 1 if ccw -1 if cw
        /// </summary>
        /// <param name="polygon"></param>
        /// <returns></returns>
        public static int GetWindingDirectionOld(this Polygon polygon)
        {
            int pointCount = polygon.Count;
            double totalTurns = 0;
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                int prevIndex = (pointIndex + pointCount - 1) % pointCount;
                int nextIndex = (pointIndex + 1) % pointCount;
                IntPoint prevPoint = polygon[prevIndex];
                IntPoint currentPoint = polygon[pointIndex];
                IntPoint nextPoint = polygon[nextIndex];

                double turnAmount = currentPoint.GetTurnAmount(prevPoint, nextPoint);

                totalTurns += turnAmount;
            }

            return totalTurns > 0 ? 1 : -1;
        }

        public static Polygon MakeCloseSegmentsMergable(this Polygon polygonToSplit, long distanceNeedingAdd, bool pathIsClosed = true)
        {
            return MakeCloseSegmentsMergable(polygonToSplit, polygonToSplit, distanceNeedingAdd, pathIsClosed);
        }

        public static Polygon MakeCloseSegmentsMergable(this Polygon polygonToSplit, Polygon pointsToSplitOn, long distanceNeedingAdd, bool pathIsClosed = true)
        {
            List<Segment> segments = Segment.ConvertToSegments(polygonToSplit, pathIsClosed);

            var touchingEnumerator = new PolygonEdgeIterator(pointsToSplitOn, distanceNeedingAdd);

            // for every segment
            for (int segmentIndex = segments.Count - 1; segmentIndex >= 0; segmentIndex--)
            {
                List<Segment> newSegments = segments[segmentIndex].GetSplitSegmentForVertecies(touchingEnumerator);
                if (newSegments?.Count > 0)
                {
                    // remove the old segment
                    segments.RemoveAt(segmentIndex);
                    // add the new ones
                    segments.InsertRange(segmentIndex, newSegments);
                }
            }

            var segmentedPolygon = new Polygon(segments.Count);

            foreach (var segment in segments)
            {
                segmentedPolygon.Add(segment.Start);
            }

            if (!pathIsClosed && segments.Count > 0)
            {
                // add the last point
                segmentedPolygon.Add(segments[segments.Count - 1].End);
            }

            return segmentedPolygon;
        }

        public static Polygons MergePerimeterOverlaps(this Polygon perimeter, long overlapMergeAmount_um, bool pathIsClosed = true)
        {
            return MergePerimeterOverlaps(new Polygons { perimeter }, overlapMergeAmount_um, pathIsClosed);
        }

        public static Polygons MergePerimeterOverlaps(this Polygons perimetersIn, long overlapMergeAmount_um, bool pathIsClosed = true)
        {
            // if the path is wound CW
            var separatedPolygons = new Polygons();

            long cleanDistance_um = overlapMergeAmount_um / 40;

            var perimeters = Clipper.CleanPolygons(perimetersIn, cleanDistance_um);

            if (perimeters.Count != perimetersIn.Count
                || perimeters.Any(p => p.Count == 0))
            {
                perimeters = Clipper.CleanPolygons(perimetersIn);
            }

            if (perimeters.Count == 0)
            {
                return null;
            }

            bool pathWasOptimized = false;

            // Set all the paths to have the width we are starting with (as we will be changing some of them)
            foreach (var perimeter in perimeters)
            {
                for (int i = 0; i < perimeter.Count; i++)
                {
                    perimeter[i] = new IntPoint(perimeter[i]);
                }
            }

            perimeters = perimeters.MakeCloseSegmentsMergable(overlapMergeAmount_um * 3 / 4, pathIsClosed);

            // make a copy that has every point duplicated (so that we have them as segments).
            List<Segment> polySegments = Segment.ConvertToSegments(perimeters, pathIsClosed);

            var markedAltered = new Altered[polySegments.Count];

            var minimumLengthToCreateSquared = overlapMergeAmount_um;
            minimumLengthToCreateSquared *= minimumLengthToCreateSquared;

            var touchingEnumerator = new CloseSegmentsIterator(polySegments, overlapMergeAmount_um);
            int segmentCount = polySegments.Count;
            // now walk every segment and check if there is another segment that is similar enough to merge them together
            for (int firstSegmentIndex = 0; firstSegmentIndex < segmentCount; firstSegmentIndex++)
            {
                foreach (int checkSegmentIndex in touchingEnumerator.GetTouching(firstSegmentIndex, segmentCount))
                {
                    // The first point of start and the last point of check (the path will be coming back on itself).
                    long startDelta = (polySegments[firstSegmentIndex].Start - polySegments[checkSegmentIndex].End).Length();
                    // if the segments are similar enough
                    if (startDelta < overlapMergeAmount_um)
                    {
                        // The last point of start and the first point of check (the path will be coming back on itself).
                        long endDelta = (polySegments[firstSegmentIndex].End - polySegments[checkSegmentIndex].Start).Length();
                        if (endDelta < overlapMergeAmount_um)
                        {
                            // only consider the merge if the directions of the lines are towards each other
                            var firstSegmentDirection = polySegments[firstSegmentIndex].End - polySegments[firstSegmentIndex].Start;
                            var checkSegmentDirection = polySegments[checkSegmentIndex].End - polySegments[checkSegmentIndex].Start;
                            if (firstSegmentDirection.Dot(checkSegmentDirection) > 0)
                            {
                                continue;
                            }

                            // get the line width
                            long startEndWidth = (polySegments[firstSegmentIndex].Start - polySegments[checkSegmentIndex].End).Length();
                            long endStartWidth = (polySegments[firstSegmentIndex].End - polySegments[checkSegmentIndex].Start).Length();
                            long width = Math.Min(startEndWidth, endStartWidth) + overlapMergeAmount_um;

                            // check if we extrude enough to consider doing this merge
                            var segmentStart = (polySegments[firstSegmentIndex].Start + polySegments[checkSegmentIndex].End) / 2;
                            var segmentEnd = (polySegments[firstSegmentIndex].End + polySegments[checkSegmentIndex].Start) / 2;

                            if ((segmentStart - segmentEnd).LengthSquared() < minimumLengthToCreateSquared)
                            {
                                continue;
                            }

                            pathWasOptimized = true;
                            // move the first segments points to the average of the merge positions
                            polySegments[firstSegmentIndex].Start = segmentStart;
                            polySegments[firstSegmentIndex].End = segmentEnd;

                            markedAltered[firstSegmentIndex] = Altered.Merged;
                            // mark this segment for removal
                            markedAltered[checkSegmentIndex] = Altered.Remove;
                            // We only expect to find one match for each segment, so move on to the next segment
                            break;
                        }
                    }
                }
            }

            // remove the marked segments
            for (int segmentIndex = segmentCount - 1; segmentIndex >= 0; segmentIndex--)
            {
                if (markedAltered[segmentIndex] == Altered.Remove)
                {
                    polySegments.RemoveAt(segmentIndex);
                }
                else
                {
                    var nextSegment = (segmentIndex + 1) % segmentCount;
                    var prevSegment = (segmentIndex + segmentCount - 1) % segmentCount;
                    if (markedAltered[nextSegment] != 0
                        && markedAltered[prevSegment] != 0
                        && (polySegments[segmentIndex].End - polySegments[segmentIndex].Start).Length() < overlapMergeAmount_um)
                    {
                        polySegments.RemoveAt(segmentIndex);
                    }
                }
            }

            // go through the polySegments and create a new polygon for every connected set of segments
            var currentPolygon = new Polygon();
            separatedPolygons.Add(currentPolygon);
            // put in the first point
            for (int segmentIndex = 0; segmentIndex < polySegments.Count; segmentIndex++)
            {
                // add the start point
                currentPolygon.Add(polySegments[segmentIndex].Start);

                // if the next segment is not connected to this one
                if (segmentIndex < polySegments.Count - 1
                    && polySegments[segmentIndex].End != polySegments[segmentIndex + 1].Start)
                {
                    // add the end point
                    currentPolygon.Add(polySegments[segmentIndex].End);

                    // create a new polygon
                    currentPolygon = new Polygon();
                    separatedPolygons.Add(currentPolygon);
                }
            }

            if (polySegments.Count > 0)
            {
                // add the end point
                currentPolygon.Add(polySegments[polySegments.Count - 1].End);
            }

            if (pathWasOptimized
                && Math.Abs(perimeters.Length() - separatedPolygons.Length(false)) < overlapMergeAmount_um * 2)
            {
                return null;
            }

            return separatedPolygons;
        }

        public static bool OnSegment(IntPoint start, IntPoint testPosition, IntPoint end)
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

        //returns 0 if false, +1 if true, -1 if pt ON polygon boundary
        public static int PointIsInside(this Polygon polygon, IntPoint testPoint, INearestNeighbours<int> nearestNeighbours = null)
        {
            if (polygon.FindPoint(testPoint, nearestNeighbours) != -1)
            {
                return -1;
            }

            return Clipper.PointInPolygon(testPoint, polygon);
        }

        public static bool SegmentTouching(this Polygon polygon, IntPoint start, IntPoint end)
        {
            IntPoint segmentDelta = end - start;
            IntPoint edgeStart = polygon[0];
            for (int i = 0; i < polygon.Count; i++)
            {
                IntPoint edgeEnd = polygon[(i + 1) % polygon.Count];
                if (DoIntersect(start, end, edgeStart, edgeEnd))
                {
                    return true;
                }

                edgeStart = edgeEnd;
            }

            return false;
        }

        /// <summary>
        /// Split the given (closed) polygon at the specified index
        /// </summary>
        /// <param name="polygon">The polygon to split</param>
        /// <param name="index">The index to split at</param>
        /// <returns>A new poly line that starts at the split index and travels back around to it</returns>
        public static Polygon SplitAtIndex(this Polygon polygon, int index)
        {
            // break the polygon at the tracked position
            var count = polygon.Count;
            var splitAtIndex = new Polygon(count);
            // make sure we add the first point again (the less than or equal to)
            for (int j = 0; j <= polygon.Count; j++)
            {
                splitAtIndex.Add(polygon[(index + j) % count]);
            }

            return splitAtIndex;
        }

        public static bool TouchingEdge(this Polygon polygon, IntPoint testPosition, QuadTree<int> edgeQuadTree = null)
        {
            var edgeIterator = new PolygonEdgeIterator(polygon, 1, edgeQuadTree);
            foreach (var i in edgeIterator.GetTouching(new Quad(testPosition)))
            {
                IntPoint edgeStart = polygon[i];
                IntPoint edgeEnd = polygon[(i + 1) % polygon.Count];
                if (OnSegment(edgeStart, testPosition, edgeEnd))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Trim an amount off the end of the polygon
        /// </summary>
        /// <param name="inPolygon">The polygon to trim</param>
        /// <param name="amountToTrim">The distance to trim in polygon units</param>
        /// <param name="isClosed">Does the last point connect back to the first</param>
        /// <returns>A new polygon that has had the end trimmed</returns>
        public static Polygon TrimEnd(this Polygon inPolygon, long amountToTrim, bool isClosed = false)
        {
            return inPolygon.CutToLength((long)inPolygon.Length(isClosed) - amountToTrim);
        }

        public class KdTreeNeighbours : KdTree<long, int>, INearestNeighbours<int>
        {
            public KdTreeNeighbours(Polygon polygon)
                : base(2, new LongMath())
            {
                for (int i = 0; i < polygon.Count; i++)
                {
                    this.Add(new long[] { polygon[i].X, polygon[i].Y }, i);
                }
            }

            public int GetNearestNeighbour(IntPoint position)
            {
                foreach (var item in this.GetNearestNeighbours(new long[] { position.X, position.Y }, 1))
                {
                    return item.Value;
                }

                return -1;
            }
        }

        public class LongMath : TypeMath<long>
        {
            public LongMath()
            { }

            public override long MaxValue => long.MaxValue;

            public override long MinValue => long.MinValue;

            public override long NegativeInfinity => long.MinValue;

            public override long PositiveInfinity => long.MaxValue;

            public override long Zero => 0;

            public override long Add(long a, long b) => a + b;

            public override bool AreEqual(long a, long b) => a.Equals(b);

            public override int Compare(long a, long b) => a.CompareTo(b);

            public override long DistanceSquaredBetweenPoints(long[] a, long[] b)
            {
                long dist = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    dist += (a[i] - b[i]) * (a[i] - b[i]);
                }

                return dist;
            }

            public override long Multiply(long a, long b) => a * b;

            public override long Subtract(long a, long b) => a - b;
        }
    }
}