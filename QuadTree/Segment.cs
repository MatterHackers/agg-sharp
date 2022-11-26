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
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.QuadTree
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public class Segment
    {
        public IntPoint End;

        public IntPoint Start;

        public Segment()
        {
        }

        public Segment(IntPoint start, IntPoint end)
        {
            this.Start = start;
            this.End = end;
        }

        public long Bottom
        {
            get
            {
                return Math.Min(Start.Y, End.Y);
            }
        }

        public long Left
        {
            get
            {
                return Math.Min(Start.X, End.X);
            }
        }

        public long Right
        {
            get
            {
                return Math.Max(Start.X, End.X);
            }
        }

        public long Top
        {
            get
            {
                return Math.Max(Start.Y, End.Y);
            }
        }

        public static List<Segment> ConvertPathToSegments(IList<IntPoint> path, bool pathIsClosed = true)
        {
            List<Segment> polySegments = new List<Segment>(path.Count);
            int endIndex = pathIsClosed ? path.Count : path.Count - 1;
            for (int i = 0; i < endIndex; i++)
            {
                IntPoint point = new IntPoint(path[i]);
                int nextIndex = (i + 1) % path.Count;
                IntPoint nextPoint = new IntPoint(path[nextIndex]);

                polySegments.Add(new Segment()
                {
                    Start = point,
                    End = nextPoint,
                });
            }

            return polySegments;
        }

        public static List<Segment> ConvertToSegments(Polygons polygons, bool pathsAreClosed = true)
        {
            List<Segment> polySegments = new List<Segment>();
            foreach (var polygon in polygons)
            {
                polySegments.AddRange(ConvertToSegments(polygon, pathsAreClosed));
            }

            return polySegments;
        }

        public static List<Segment> ConvertToSegments(Polygon polygon, bool pathIsClosed = true)
        {
            List<Segment> polySegments = new List<Segment>(polygon.Count);
            int endIndex = pathIsClosed ? polygon.Count : polygon.Count - 1;
            for (int i = 0; i < endIndex; i++)
            {
                IntPoint point = polygon[i];
                IntPoint nextPoint = polygon[(i + 1) % polygon.Count];

                polySegments.Add(new Segment()
                {
                    Start = point,
                    End = nextPoint,
                });
            }

            return polySegments;
        }

        public static bool operator !=(Segment p0, Segment p1)
        {
            return p0.Start != p1.Start || p0.End != p1.End;
        }

        public static bool operator ==(Segment p0, Segment p1)
        {
            return p0.Start == p1.Start && p0.End == p1.End;
        }

        public override bool Equals(System.Object obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }

            // If parameter cannot be cast to Point return false.
            Segment p = (Segment)obj;
            if ((System.Object)p == null)
            {
                return false;
            }

            // Return true if the fields match:
            return (End == p.End) && (Start == p.Start);
        }

        public override int GetHashCode()
        {
            return new IntPoint[] { End, Start }.GetHashCode();
        }

        public List<Segment> GetSplitSegmentForVertecies(PolygonEdgeIterator touchingEnumerator)
        {
            IntPoint start2D = new IntPoint(Start);
            IntPoint end2D = new IntPoint(End);

            SortedList<long, IntPoint> requiredSplits2D = new SortedList<long, IntPoint>();

            // get some data we will need for the operations
            IntPoint direction = (end2D - start2D);
            long length = direction.Length();
            long lengthSquared = length * length;
            IntPoint rightDirection = direction.GetPerpendicularRight();
            long maxDistanceNormalized = touchingEnumerator.OverlapAmount * length;

            // for every vertex
            foreach (int touchingPoint in touchingEnumerator.GetTouching(new Quad(this.Left, this.Bottom, this.Right, this.Top)))
            //for (int splintIndex = 0; splintIndex < splitPoints.Count; splintIndex++)
            {
                IntPoint vertex = new IntPoint(touchingEnumerator.SourcePoints[touchingPoint]) - start2D;
                // if the vertex is close enough to the segment
                long dotProduct = rightDirection.Dot(vertex);
                if (Math.Abs(dotProduct) < maxDistanceNormalized)
                {
                    long dotProduct2 = direction.Dot(vertex);
                    if (dotProduct2 > 0 && dotProduct2 < lengthSquared)
                    {
                        long distance = dotProduct2 / length;
                        // don't add if there is already a point at this position
                        if (!requiredSplits2D.ContainsKey(distance))
                        {
                            // we are close enough to the line split it
                            requiredSplits2D.Add(distance, start2D + direction * distance / length);
                        }
                    }
                }
            }

            if (requiredSplits2D.Count > 0)
            {
                // add in the start and end
                if (!requiredSplits2D.ContainsKey(0))
                {
                    requiredSplits2D.Add(0, start2D);
                }
                if (!requiredSplits2D.ContainsKey(length))
                {
                    requiredSplits2D.Add(length, end2D);
                }
                // convert to a segment list
                List<Segment> newSegments = Segment.ConvertPathToSegments(requiredSplits2D.Values, false);
                // return them;
                return newSegments;
            }

            return null;
        }

        public long LengthSquared()
        {
            return (this.End - this.Start).LengthSquared();
        }
    }
}