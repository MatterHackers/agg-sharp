using MatterHackers.Agg.SvgTools;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;


//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
using System.Collections.Generic;

namespace MatterHackers.Agg.VertexSource
{
    //---------------------------------------------------------------path_base
    // A container to store vertices with their flags.
    // A path consists of a number of contours separated with "move_to"
    // commands. The path storage can keep and maintain more than one
    // path.
    // To navigate to the beginning of a particular path, use rewind(path_id);
    // Where path_id is what start_new_path() returns. So, when you call
    // start_new_path() you need to store its return value somewhere else
    // to navigate to the path afterwards.
    //
    // See also: vertex_source concept
    //------------------------------------------------------------------------
    public class VertexStorage : IVertexSource, IVertexDest
    {
        private class VertexDataManager
        {
            private int allocatedVertices;
            private VertexData[] vertexData;
            private int numVertices;

            public VertexDataManager()
            {
            }

            public VertexData this[int index]
            {
                get => vertexData[index];
                set => vertexData[index] = value;
            }

            public void AddVertex(double x, double y, FlagsAndCommand CommandAndFlags, CommandHint hint = CommandHint.None)
            {
                int index = numVertices;
                AllocateIfRequired(numVertices);
                vertexData[index] = new VertexData(CommandAndFlags, x, y, hint);

                numVertices++;
            }

            public FlagsAndCommand Command(int index)
            {
                return vertexData[index].Command;
            }

            public void free_all()
            {
                vertexData = null;
                numVertices = 0;
            }

            public FlagsAndCommand LastCommand()
            {
                if (numVertices != 0)
                {
                    return Command(numVertices - 1);
                }

                return FlagsAndCommand.Stop;
            }

            public FlagsAndCommand LastVertex(out double x, out double y)
            {
                if (numVertices != 0)
                {
                    return Vertex(numVertices - 1, out x, out y);
                }

                x = 0;
                y = 0;

                return FlagsAndCommand.Stop;
            }

            public double last_x()
            {
                if (numVertices > 0)
                {
                    int index = numVertices - 1;
                    return vertexData[index].Position.X;
                }

                return 0;
            }

            public double LastY()
            {
                if (numVertices > 0)
                {
                    int index = numVertices - 1;
                    return vertexData[index].Position.Y;
                }

                return 0;
            }

            public void ModifyCommand(int index, FlagsAndCommand CommandAndFlags)
            {
                this.vertexData[index].Command = CommandAndFlags;
            }

            public void ModifyVertex(int index, double x, double y)
            {
                vertexData[index].Position = new Vector2(x, y);
            }

            public void ModifyVertex(int index, double x, double y, FlagsAndCommand CommandAndFlags)
            {
                vertexData[index].Position = new Vector2(x, y);
                vertexData[index].Command = CommandAndFlags;
            }

            public FlagsAndCommand PrevVertex(out double x, out double y)
            {
                if (numVertices > 1)
                {
                    return Vertex(numVertices - 2, out x, out y);
                }

                x = 0;
                y = 0;

                return FlagsAndCommand.Stop;
            }

            public void Clear()
            {
                numVertices = 0;
            }

            public void SwapVertices(int v1, int v2)
            {
                var hold = vertexData[v2];
                vertexData[v2] = vertexData[v1];
                vertexData[v1] = hold;
            }

            public int TotalVertices()
            {
                return numVertices;
            }

            public FlagsAndCommand Vertex(int index, out double x, out double y)
            {
                x = vertexData[index].Position.X;
                y = vertexData[index].Position.Y;
                return vertexData[index].Command;
            }

            private void AllocateIfRequired(int indexToAdd)
            {
                if (indexToAdd < numVertices)
                {
                    return;
                }

                while (indexToAdd >= allocatedVertices)
                {
                    int newSize = allocatedVertices + 256;
                    VertexData[] newVertexData = new VertexData[newSize];

                    if (vertexData != null)
                    {
                        for (int i = 0; i < numVertices; i++)
                        {
                            newVertexData[i] = vertexData[i];
                        }
                    }

                    vertexData = newVertexData;

                    allocatedVertices = newSize;
                }
            }

            /// <summary>
            /// Determines if the given point lies within, on, or outside the polygon defined by vertex data.
            /// Based on "The Point in Polygon Problem for Arbitrary Polygons" by Hormann & Agathos.
            /// </summary>
            /// <param name="pointToCheck">The point to check.</param>
            /// <returns>
            /// 0 if point is outside the polygon,
            /// +1 if point is inside the polygon,
            /// -1 if point is on the polygon boundary.
            /// </returns>
            public int CheckPointInPolygon(Vector2 pointToCheck)
            {
                int pointPosition = 0;
                var vertexCount = vertexData.Length;

                // If the polygon has less than 3 vertices, it's not valid, so the point is outside.
                if (vertexCount < 3)
                {
                    return 0;
                }

                var currentVertex = vertexData[0];
                for (int i = 1; i <= vertexCount; ++i)
                {
                    var nextVertex = (i == vertexCount ? vertexData[0] : vertexData[i]);

                    if (nextVertex.Y == pointToCheck.Y)
                    {
                        if ((nextVertex.X == pointToCheck.X) || (currentVertex.Y == pointToCheck.Y && ((nextVertex.X > pointToCheck.X) == (currentVertex.X < pointToCheck.X))))
                        {
                            return -1;
                        }
                    }

                    if ((currentVertex.Y < pointToCheck.Y) != (nextVertex.Y < pointToCheck.Y))
                    {
                        if (currentVertex.X >= pointToCheck.X)
                        {
                            if (nextVertex.X > pointToCheck.X)
                            {
                                pointPosition = 1 - pointPosition;
                            }
                            else
                            {
                                double d = (double)(currentVertex.X - pointToCheck.X) * (nextVertex.Y - pointToCheck.Y) - (double)(nextVertex.X - pointToCheck.X) * (currentVertex.Y - pointToCheck.Y);

                                if (d == 0)
                                {
                                    return -1;
                                }
                                else if ((d > 0) == (nextVertex.Y > currentVertex.Y))
                                {
                                    pointPosition = 1 - pointPosition;
                                }
                            }
                        }
                        else
                        {
                            if (nextVertex.X > pointToCheck.X)
                            {
                                double d = (double)(currentVertex.X - pointToCheck.X) * (nextVertex.Y - pointToCheck.Y) - (double)(nextVertex.X - pointToCheck.X) * (currentVertex.Y - pointToCheck.Y);

                                if (d == 0)
                                {
                                    return -1;
                                }
                                else if ((d > 0) == (nextVertex.Y > currentVertex.Y))
                                {
                                    pointPosition = 1 - pointPosition;
                                }
                            }
                        }
                    }

                    currentVertex = nextVertex;
                }

                return pointPosition;
            }

            public void RemoveAt(int index)
            {
                // remove the vertex and compact the array
                for (int i = index; i < numVertices - 1; i++)
                {
                    vertexData[i] = vertexData[i + 1];
                }

                // remove the last vertex
                numVertices--;
            }

            /// <summary>
            /// Replaces the vertices in [start, endInclusive] with <paramref name="replacement"/>, shifting
            /// everything after them. An edit that changes the shape of a contour rarely changes its vertex
            /// count by one, so it needs a splice rather than a series of RemoveAt calls.
            /// </summary>
            public void ReplaceRange(int start, int endInclusive, IList<VertexData> replacement)
            {
                var tail = new VertexData[numVertices - endInclusive - 1];
                for (int i = 0; i < tail.Length; i++)
                {
                    tail[i] = vertexData[endInclusive + 1 + i];
                }

                // rewind to the start of the range and re-add everything that follows it
                numVertices = start;

                foreach (var vertex in replacement)
                {
                    AddVertex(vertex.Position.X, vertex.Position.Y, vertex.Command, vertex.Hint);
                }

                foreach (var vertex in tail)
                {
                    AddVertex(vertex.Position.X, vertex.Position.Y, vertex.Command, vertex.Hint);
                }
            }
        }

        private int iteratorIndex;
        private VertexDataManager vertexDataManager;

        public VertexStorage()
        {
            vertexDataManager = new VertexDataManager();
        }

        public VertexStorage(string svgDString)
            : this()
        {
            SvgDString = svgDString;
        }

        public VertexStorage(IVertexSource copyFrom)
            : this()
        {
            if (copyFrom != null)
            {
                this.AddRangeDroppingTrailingStops(copyFrom.Vertices());
            }
        }

        /// <summary>
        /// Appends vertices, discarding any run of Stops that reaches the end of the sequence.
        /// </summary>
        /// <remarks>
        /// Vertices() synthesizes a terminal Stop that was never a stored vertex, so anything that reads a path
        /// through it and stores what it is handed comes back one vertex longer than its source - and longer again
        /// on the next copy. A Stop with real geometry after it means something entirely different: it ends one
        /// contour and everything past it belongs to the next, so dropping that one would weld two contours into
        /// one. Holding Stops back and writing them out only when a real vertex follows keeps the separators and
        /// drops the terminator, which this storage's own Vertices() will invent again on demand.
        /// Sources that stored their own trailing Stop - CreateVertexStorage callers and OutlinePathObject3D both
        /// end that way - therefore copy one vertex shorter. That is deliberate: a trailing Stop carries no
        /// geometry, and leaving it out is what makes a copy of a copy stop growing.
        /// </remarks>
        /// <param name="vertices">The vertices to append. Must not be null.</param>
        public void AddRangeDroppingTrailingStops(IEnumerable<VertexData> vertices)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            var pendingStops = new List<VertexData>();
            foreach (var vertex in vertices)
            {
                if (vertex.IsStop)
                {
                    pendingStops.Add(vertex);
                    continue;
                }

                foreach (var stop in pendingStops)
                {
                    this.Add(stop.X, stop.Y, stop.Command, stop.Hint);
                }

                pendingStops.Clear();

                this.Add(vertex.X, vertex.Y, vertex.Command, vertex.Hint);
            }
        }

        [JsonIgnore]
        public int Count
        {
            get
            {
                return vertexDataManager.TotalVertices();
            }
        }

        public ulong GetLongHashCode(ulong hash = 14695981039346656037)
        {
            foreach (var vertex in this.Vertices())
            {
                hash = vertex.GetLongHashCode(hash);
            }

            return hash;
        }

        /// <summary>
        /// Determines if the given point lies within, on, or outside the polygon defined by vertex data.
        /// Based on "The Point in Polygon Problem for Arbitrary Polygons" by Hormann & Agathos.
        /// </summary>
        /// <param name="pointToCheck">The point to check.</param>
        /// <returns>
        /// 0 if point is outside the polygon,
        /// +1 if point is inside the polygon,
        /// -1 if point is on the polygon boundary.
        /// </returns>
        public int CheckPointInPolygon(Vector2 pointToCheck)
        {
            return vertexDataManager.CheckPointInPolygon(pointToCheck);
        }

        public static bool OldEqualsNewStyle(IVertexSource control, IVertexSource test, double maxError = .0001)
        {
            control.Rewind(0);
            double controlX;
            double controlY;
            FlagsAndCommand controlFlagsAndCommand = control.Vertex(out controlX, out controlY);

            int index = 0;
            foreach (VertexData vertexData in test.Vertices())
            {
                if (controlFlagsAndCommand != vertexData.Command
                    || controlX < vertexData.Position.X - maxError || controlX > vertexData.Position.X + maxError
                    || controlY < vertexData.Position.Y - maxError || controlY > vertexData.Position.Y + maxError)
                {
                    return false;
                }
                controlFlagsAndCommand = control.Vertex(out controlX, out controlY);
                index++;
            }

            if (controlFlagsAndCommand == FlagsAndCommand.Stop)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// How close two anchors have to be to count as the same point. Coordinates survive a trip through
        /// SvgDString at three decimal places, so anything meant to be the same point comes back identical.
        /// </summary>
        private const double CoincidentAnchorEpsilon = 0.000001;

        /// <summary>
        /// Removes the vertex at <paramref name="index"/> and repairs the contour it belonged to.
        /// </summary>
        /// <remarks>
        /// A point is not always a single vertex. A bezier anchor is the third vertex of a Curve4 triple and
        /// owns the handles on either side of it, and a closed contour whose last segment curves back to the
        /// start has to repeat that start point as the closing anchor. So this works on an anchor/handle view
        /// of the contour rather than on raw indexes:
        /// <list type="bullet">
        /// <item>removing an anchor merges the segments on either side of it into one, keeping the previous
        /// anchor's outgoing handle and the next anchor's incoming handle (the removed anchor's own two
        /// handles go with it),</item>
        /// <item>removing a handle straightens that side of its segment,</item>
        /// <item>removing the close vertex opens the contour,</item>
        /// <item>removing the first point promotes the next anchor to the MoveTo.</item>
        /// </list>
        /// Only the contour containing <paramref name="index"/> is rewritten - every other vertex in the
        /// storage is left exactly as it was.
        /// </remarks>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= vertexDataManager.TotalVertices())
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            // the close vertex is not part of the anchor structure, dropping it just opens the contour
            if (ShapePath.is_end_poly(vertexDataManager.Command(index)))
            {
                vertexDataManager.RemoveAt(index);
                return;
            }

            GetContourRange(index, out var contourStart, out var contourEnd);

            var isClosed = ShapePath.is_end_poly(vertexDataManager.Command(contourEnd));
            var closeVertex = isClosed ? (VertexData?)vertexDataManager[contourEnd] : null;
            var lastAnchorVertex = isClosed ? contourEnd - 1 : contourEnd;

            var anchors = BuildContourAnchors(contourStart, lastAnchorVertex, isClosed);
            RemoveFromContourAnchors(anchors, index);
            vertexDataManager.ReplaceRange(contourStart, contourEnd, EmitContourAnchors(anchors, closeVertex));
        }

        /// <summary>
        /// One editable point of a contour: its position plus the bezier handles that shape the segments
        /// arriving at and leaving it. The source vertex indexes ride along so a delete can tell which part of
        /// the flat vertex array the caller asked to remove.
        /// </summary>
        private struct ContourAnchor
        {
            public Vector2 Position;

            /// <summary>The C4ControlToPoint of the segment arriving at this anchor, if that segment curves.</summary>
            public Vector2? InHandle;

            /// <summary>The C4ControlFromPrev of the segment leaving this anchor, if that segment curves.</summary>
            public Vector2? OutHandle;

            public int AnchorIndex;
            public int InHandleIndex;
            public int OutHandleIndex;

            /// <summary>
            /// The repeated vertex a closing curve ends on, when that curve returns to this anchor. It is the
            /// same point to the user, so a delete aimed at either index means this anchor.
            /// </summary>
            public int ClosingAnchorIndex;

            public static ContourAnchor Create(Vector2 position, int anchorIndex)
            {
                return new ContourAnchor()
                {
                    Position = position,
                    AnchorIndex = anchorIndex,
                    InHandleIndex = -1,
                    OutHandleIndex = -1,
                    ClosingAnchorIndex = -1,
                };
            }
        }

        /// <summary>
        /// Finds the vertex range of the contour containing <paramref name="index"/>. The range runs from the
        /// contour's MoveTo up to (and including) its close vertex, stopping before the next contour.
        /// </summary>
        private void GetContourRange(int index, out int contourStart, out int contourEnd)
        {
            contourStart = index;
            while (contourStart > 0
                && !ShapePath.IsMoveTo(vertexDataManager.Command(contourStart)))
            {
                contourStart--;
            }

            contourEnd = index;
            while (contourEnd + 1 < vertexDataManager.TotalVertices())
            {
                var nextCommand = vertexDataManager.Command(contourEnd + 1);
                if (ShapePath.IsMoveTo(nextCommand) || ShapePath.IsStop(nextCommand))
                {
                    break;
                }

                contourEnd++;
            }
        }

        private List<ContourAnchor> BuildContourAnchors(int contourStart, int lastAnchorVertex, bool isClosed)
        {
            if (!ShapePath.IsMoveTo(vertexDataManager.Command(contourStart)))
            {
                throw new NotImplementedException("RemoveAt requires the contour to start with a MoveTo.");
            }

            var anchors = new List<ContourAnchor>();
            anchors.Add(ContourAnchor.Create(vertexDataManager[contourStart].Position, contourStart));

            var i = contourStart + 1;
            while (i <= lastAnchorVertex)
            {
                switch (vertexDataManager.Command(i) & FlagsAndCommand.CommandsMask)
                {
                    case FlagsAndCommand.LineTo:
                        anchors.Add(ContourAnchor.Create(vertexDataManager[i].Position, i));
                        i++;
                        break;

                    case FlagsAndCommand.Curve4:
                        if (i + 2 > lastAnchorVertex)
                        {
                            throw new NotImplementedException("RemoveAt found an incomplete Curve4 segment.");
                        }

                        var previous = anchors[anchors.Count - 1];
                        previous.OutHandle = vertexDataManager[i].Position;
                        previous.OutHandleIndex = i;
                        anchors[anchors.Count - 1] = previous;

                        var anchor = ContourAnchor.Create(vertexDataManager[i + 2].Position, i + 2);
                        anchor.InHandle = vertexDataManager[i + 1].Position;
                        anchor.InHandleIndex = i + 1;
                        anchors.Add(anchor);
                        i += 3;
                        break;

                    default:
                        throw new NotImplementedException($"RemoveAt does not handle {vertexDataManager.Command(i)} yet.");
                }
            }

            FoldClosingAnchor(anchors, isClosed);

            return anchors;
        }

        /// <summary>
        /// Folds a closing vertex that sits on the contour's start point back into the first anchor. A closing
        /// curve has to end on an anchor, so it always repeats the start point, and a contour can also run a
        /// plain line back to it. Left as its own entry the same user point would be two anchors, and deleting
        /// either one would strand the other.
        /// </summary>
        private static void FoldClosingAnchor(List<ContourAnchor> anchors, bool isClosed)
        {
            if (!isClosed || anchors.Count < 2)
            {
                return;
            }

            var last = anchors[anchors.Count - 1];
            var first = anchors[0];

            // coincidence alone decides, not whether the last segment curves - a contour that runs a line all
            // the way back to its start stores that point twice as well, and an edit that treats the two
            // vertices as separate points re-roots the same shape instead of changing it
            if ((last.Position - first.Position).Length > CoincidentAnchorEpsilon)
            {
                return;
            }

            first.InHandle = last.InHandle;
            first.InHandleIndex = last.InHandleIndex;
            first.ClosingAnchorIndex = last.AnchorIndex;
            anchors[0] = first;
            anchors.RemoveAt(anchors.Count - 1);
        }

        private static void RemoveFromContourAnchors(List<ContourAnchor> anchors, int index)
        {
            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];

                if (anchor.AnchorIndex == index || anchor.ClosingAnchorIndex == index)
                {
                    anchors.RemoveAt(i);
                    return;
                }

                if (anchor.InHandleIndex == index)
                {
                    anchor.InHandle = null;
                    anchor.InHandleIndex = -1;
                    anchors[i] = anchor;
                    return;
                }

                if (anchor.OutHandleIndex == index)
                {
                    anchor.OutHandle = null;
                    anchor.OutHandleIndex = -1;
                    anchors[i] = anchor;
                    return;
                }
            }

            throw new NotImplementedException($"RemoveAt could not place vertex {index} in its contour.");
        }

        private static List<VertexData> EmitContourAnchors(List<ContourAnchor> anchors, VertexData? closeVertex)
        {
            var vertices = new List<VertexData>();
            if (anchors.Count == 0)
            {
                // the last point of the contour is gone, so the close goes with it
                return vertices;
            }

            vertices.Add(new VertexData(FlagsAndCommand.MoveTo, anchors[0].Position));

            for (int i = 1; i < anchors.Count; i++)
            {
                EmitContourSegment(vertices, anchors[i - 1], anchors[i]);
            }

            if (closeVertex.HasValue)
            {
                var last = anchors[anchors.Count - 1];
                if (anchors.Count > 1
                    && (EffectiveHandle(last.OutHandle, last.Position).HasValue
                        || EffectiveHandle(anchors[0].InHandle, anchors[0].Position).HasValue))
                {
                    // the closing segment curves, so it needs its own anchor back on the start point
                    EmitContourSegment(vertices, last, anchors[0]);
                }

                vertices.Add(closeVertex.Value);
            }

            return vertices;
        }

        /// <summary>
        /// Writes the segment between two anchors. It stays a line only while neither end shapes it; a single
        /// surviving handle still bends the segment, with the other control point resting on its own anchor.
        /// </summary>
        private static void EmitContourSegment(List<VertexData> vertices, ContourAnchor from, ContourAnchor to)
        {
            var outHandle = EffectiveHandle(from.OutHandle, from.Position);
            var inHandle = EffectiveHandle(to.InHandle, to.Position);

            if (outHandle.HasValue || inHandle.HasValue)
            {
                vertices.Add(new VertexData(FlagsAndCommand.Curve4, outHandle ?? from.Position, CommandHint.C4ControlFromPrev));
                vertices.Add(new VertexData(FlagsAndCommand.Curve4, inHandle ?? to.Position, CommandHint.C4ControlToPoint));
                vertices.Add(new VertexData(FlagsAndCommand.Curve4, to.Position, CommandHint.C4Point));
            }
            else
            {
                vertices.Add(new VertexData(FlagsAndCommand.LineTo, to.Position));
            }
        }

        /// <summary>
        /// Gets the handle to use for one end of a segment, treating a control point that rests on its own
        /// anchor as no handle at all. Parking the control point on the anchor is how the storage has to spell
        /// "this end is straight" while the other end still curves, so a rebuild must not read it back as a
        /// real handle - otherwise deleting the second handle of a segment could never straighten it.
        /// </summary>
        private static Vector2? EffectiveHandle(Vector2? handle, Vector2 anchorPosition)
        {
            if (handle.HasValue
                && (handle.Value - anchorPosition).Length > CoincidentAnchorEpsilon)
            {
                return handle;
            }

            return null;
        }

        public static bool OldEqualsOldStyle(IVertexSource control, IVertexSource test, double maxError = .0001)
        {
            control.Rewind(0);
            double controlX;
            double controlY;
            FlagsAndCommand controlFlagsAndCommand = control.Vertex(out controlX, out controlY);

            test.Rewind(0);
            double testX;
            double testY;
            FlagsAndCommand otherFlagsAndCommand = test.Vertex(out testX, out testY);

            int index = 1;
            if (controlFlagsAndCommand == otherFlagsAndCommand && controlX == testX && Util.is_equal_eps(controlY, testY, .000000001))
            {
                while (controlFlagsAndCommand != FlagsAndCommand.Stop)
                {
                    controlFlagsAndCommand = control.Vertex(out controlX, out controlY);
                    otherFlagsAndCommand = test.Vertex(out testX, out testY);
                    if (controlFlagsAndCommand != otherFlagsAndCommand
                        || controlX < testX - maxError || controlX > testX + maxError
                        || controlY < testY - maxError || controlY > testY + maxError)
                    {
                        return false;
                    }

                    index++;
                }

                return true;
            }

            return false;
        }

        public void Add(Vector2 vertex)
        {
            throw new System.NotImplementedException();
        }

        public void Add(double x, double y, FlagsAndCommand flagsAndCommand, CommandHint hint = CommandHint.None)
        {
            vertexDataManager.AddVertex(x, y, flagsAndCommand, hint);
        }

        public int ArrangeOrientations(int start, FlagsAndCommand orientation)
        {
            if (orientation != FlagsAndCommand.FlagNone)
            {
                while (start < vertexDataManager.TotalVertices())
                {
                    start = ArrangePolygonOrientation(start, orientation);
                    if (ShapePath.IsStop(vertexDataManager.Command(start)))
                    {
                        ++start;
                        break;
                    }
                }
            }

            return start;
        }

        public void ArrangeOrientationsAllPaths(FlagsAndCommand orientation)
        {
            if (orientation != FlagsAndCommand.FlagNone)
            {
                int start = 0;
                while (start < vertexDataManager.TotalVertices())
                {
                    start = ArrangeOrientations(start, orientation);
                }
            }
        }

        // Arrange the orientation of a polygon, all polygons in a path,
        // or in all paths. After calling arrange_orientations() or
        // arrange_orientations_all_paths(), all the polygons will have
        // the same orientation, i.e. path_flags_cw or path_flags_ccw
        //--------------------------------------------------------------------
        public int ArrangePolygonOrientation(int start, FlagsAndCommand orientation)
        {
            if (orientation == FlagsAndCommand.FlagNone) return start;

            // Skip all non-vertices at the beginning
            while (start < vertexDataManager.TotalVertices() &&
                  !ShapePath.IsVertex(vertexDataManager.Command(start))) ++start;

            // Skip all insignificant move_to
            while (start + 1 < vertexDataManager.TotalVertices() &&
                  ShapePath.IsMoveTo(vertexDataManager.Command(start)) &&
                  ShapePath.IsMoveTo(vertexDataManager.Command(start + 1))) ++start;

            // Find the last vertex
            int end = start + 1;
            while (end < vertexDataManager.TotalVertices() &&
                  !ShapePath.IsNextPoly(vertexDataManager.Command(end))) ++end;

            if (end - start > 2)
            {
                if (PerceivePolygonOrientation(start, end) != orientation)
                {
                    // Invert polygon, set orientation flag, and skip all end_poly
                    InvertPolygon(start, end);
                    FlagsAndCommand PathAndFlags;
                    while (end < vertexDataManager.TotalVertices() &&
                          ShapePath.is_end_poly(PathAndFlags = vertexDataManager.Command(end)))
                    {
                        vertexDataManager.ModifyCommand(end++, PathAndFlags | orientation);// Path.set_orientation(cmd, orientation));
                    }
                }
            }

            return end;
        }

        public void ClosePolygon(FlagsAndCommand flags)
        {
            EndPoly(FlagsAndCommand.FlagClose | flags);
        }

        public void ClosePolygon()
        {
            ClosePolygon(FlagsAndCommand.FlagNone);
        }

        public FlagsAndCommand Command(int index)
        {
            return vertexDataManager.Command(index);
        }

        // Concatenate path. The path is added as is.
        public void ConcatPath(IVertexSource vs)
        {
            ConcatPath(vs, 0);
        }

        public void ConcatPath(IVertexSource vs, int path_id)
        {
            double x, y;
            FlagsAndCommand PathAndFlags;
            vs.Rewind(path_id);
            while (!ShapePath.IsStop(PathAndFlags = vs.Vertex(out x, out y)))
            {
                vertexDataManager.AddVertex(x, y, PathAndFlags);
            }
        }

        /// <summary>
        /// Draws a quadratic Bezier curve from the current point to the target point using the supplied control point.
        /// </summary>
        /// <param name="controlPoint">The control point</param>
        /// <param name="point">The new target point</param>
        public void Curve3(Vector2 controlPoint, Vector2 point)
        {
            Curve3(controlPoint.X, controlPoint.Y, point.X, point.Y);
        }

        /// <summary>
        /// Draws a quadratic Bezier curve from the current point to (x,y) using (xControl,yControl) as the control point.
        /// </summary>
        /// <param name="xControl"></param>
        /// <param name="yControl"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void Curve3(double xControl, double yControl, double x, double y)
        {
            vertexDataManager.AddVertex(xControl, yControl, FlagsAndCommand.Curve3);
            vertexDataManager.AddVertex(x, y, FlagsAndCommand.Curve3);
        }

        /// <summary>
        /// <para>Draws a quadratic Bezier curve from the current point to (x,y).</para>
        /// <para>The control point is assumed to be the reflection of the control point on the previous command relative to the current point.</para>
        /// <para>(If there is no previous command or if the previous command was not a curve, assume the control point is coincident with the current point.)</para>
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void Curve3(double x, double y)
        {
            double x0;
            double y0;
            if (ShapePath.IsVertex(vertexDataManager.LastVertex(out x0, out y0)))
            {
                double x_ctrl;
                double y_ctrl;
                FlagsAndCommand cmd = vertexDataManager.PrevVertex(out x_ctrl, out y_ctrl);
                if (ShapePath.IsCurve(cmd))
                {
                    x_ctrl = x0 + x0 - x_ctrl;
                    y_ctrl = y0 + y0 - y_ctrl;
                }
                else
                {
                    x_ctrl = x0;
                    y_ctrl = y0;
                }
                Curve3(x_ctrl, y_ctrl, x, y);
            }
        }

        /// <summary>
        /// Draws a quadratic Bezier curve from the current point to (x,y) using (xControl,yControl) as the control point.
        /// </summary>
        /// <param name="xControl"></param>
        /// <param name="yControl"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void Curve3Rel(double dx_ctrl, double dy_ctrl, double dx_to, double dy_to)
        {
            RelToAbs(ref dx_ctrl, ref dy_ctrl);
            RelToAbs(ref dx_to, ref dy_to);
            vertexDataManager.AddVertex(dx_ctrl, dy_ctrl, FlagsAndCommand.Curve3);
            vertexDataManager.AddVertex(dx_to, dy_to, FlagsAndCommand.Curve3);
        }

        /// <summary>
        /// <para>Draws a quadratic Bezier curve from the current point to (x,y).</para>
        /// <para>The control point is assumed to be the reflection of the control point on the previous command relative to the current point.</para>
        /// <para>(If there is no previous command or if the previous command was not a curve, assume the control point is coincident with the current point.)</para>
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void Curve3Rel(double dx_to, double dy_to)
        {
            RelToAbs(ref dx_to, ref dy_to);
            Curve3(dx_to, dy_to);
        }

        public void Curve4(double controlFromPrevX, double controlFromPrevY,
                                   double controlToPointX, double controlToPointY,
                                   double pointX, double pointY)
        {
            vertexDataManager.AddVertex(controlFromPrevX, controlFromPrevY, FlagsAndCommand.Curve4, CommandHint.C4ControlFromPrev);
            vertexDataManager.AddVertex(controlToPointX, controlToPointY, FlagsAndCommand.Curve4, CommandHint.C4ControlToPoint);
            vertexDataManager.AddVertex(pointX, pointY, FlagsAndCommand.Curve4, CommandHint.C4Point);
        }

        public void Curve4(Vector2 controlFromPrev, Vector2 controlToPoint, Vector2 point)
        {
            Curve4(controlFromPrev.X, controlFromPrev.Y, controlToPoint.X, controlToPoint.Y, point.X, point.Y);
        }

        public void Curve4(double controlToPointX, double controlToPointY, double pointX, double pointY)
        {
            double lastX;
            double lastY;
            if (ShapePath.IsVertex(LastVertex(out lastX, out lastY)))
            {
                double controlFromPrevX;
                double controlFromPrevY;
                FlagsAndCommand prevVertex = PrevVertex(out controlFromPrevX, out controlFromPrevY);
                if (ShapePath.IsCurve(prevVertex))
                {
                    controlFromPrevX = lastX + lastX - controlFromPrevX;
                    controlFromPrevY = lastY + lastY - controlFromPrevY;
                }
                else
                {
                    controlFromPrevX = lastX;
                    controlFromPrevY = lastY;
                }
                Curve4(controlFromPrevX, controlFromPrevY, controlToPointX, controlToPointY, pointX, pointY);
            }
        }

        public void Curve4(Vector2 controlToPoint, Vector2 point)
        {
            Curve4(controlToPoint.X, controlToPoint.Y, point.X, point.Y);
        }

        public void Curve4Rel(double dx_ctrl1, double dy_ctrl1,
                                       double dx_ctrl2, double dy_ctrl2,
                                       double dx_to, double dy_to)
        {
            RelToAbs(ref dx_ctrl1, ref dy_ctrl1);
            RelToAbs(ref dx_ctrl2, ref dy_ctrl2);
            RelToAbs(ref dx_to, ref dy_to);
            vertexDataManager.AddVertex(dx_ctrl1, dy_ctrl1, FlagsAndCommand.Curve4);
            vertexDataManager.AddVertex(dx_ctrl2, dy_ctrl2, FlagsAndCommand.Curve4);
            vertexDataManager.AddVertex(dx_to, dy_to, FlagsAndCommand.Curve4);
        }

        public void Curve4Rel(double dx_ctrl2, double dy_ctrl2,
                                       double dx_to, double dy_to)
        {
            RelToAbs(ref dx_ctrl2, ref dy_ctrl2);
            RelToAbs(ref dx_to, ref dy_to);
            Curve4(dx_ctrl2, dy_ctrl2, dx_to, dy_to);
        }

        public void EndPoly()
        {
            ClosePolygon(FlagsAndCommand.FlagClose);
        }

        public void EndPoly(FlagsAndCommand flags)
        {
            if (ShapePath.IsVertex(vertexDataManager.LastCommand()))
            {
                vertexDataManager.AddVertex(0.0, 0.0, FlagsAndCommand.EndPoly | flags);
            }
        }

        public bool Equals(IVertexSource other, double maxError = .0001, bool oldStyle = true)
        {
            if (oldStyle)
            {
                return OldEqualsOldStyle(this, other, maxError);
            }
            else
            {
                return OldEqualsNewStyle(this, other, maxError);
            }
        }

        // Flip all vertices horizontally or vertically,
        // between x1 and x2, or between y1 and y2 respectively
        //--------------------------------------------------------------------
        public void FlipX(double x1, double x2)
        {
            for (int i = 0; i < vertexDataManager.TotalVertices(); i++)
            {
                FlagsAndCommand PathAndFlags = vertexDataManager.Vertex(i, out double x, out double y);
                if (ShapePath.IsVertex(PathAndFlags))
                {
                    vertexDataManager.ModifyVertex(i, x2 - x + x1, y);
                }
            }
        }

        public void FlipY(double y1, double y2)
        {
            for (int i = 0; i < vertexDataManager.TotalVertices(); i++)
            {
                FlagsAndCommand PathAndFlags = vertexDataManager.Vertex(i, out double x, out double y);
                if (ShapePath.IsVertex(PathAndFlags))
                {
                    vertexDataManager.ModifyVertex(i, x, y2 - y + y1);
                }
            }
        }

        public void FreeAll()
        {
            vertexDataManager.free_all(); iteratorIndex = 0;
        }

        public double GetLastX()
        {
            return vertexDataManager.last_x();
        }

        public double GetLastY()
        {
            return vertexDataManager.LastY();
        }

        public void HorizontalLineTo(double x)
        {
            vertexDataManager.AddVertex(x, GetLastY(), FlagsAndCommand.LineTo);
        }

        public void InvertPolygon(int start)
        {
            // Skip all non-vertices at the beginning
            while (start < vertexDataManager.TotalVertices() &&
                  !ShapePath.IsVertex(vertexDataManager.Command(start))) ++start;

            // Skip all insignificant move_to
            while (start + 1 < vertexDataManager.TotalVertices() &&
                  ShapePath.IsMoveTo(vertexDataManager.Command(start)) &&
                  ShapePath.IsMoveTo(vertexDataManager.Command(start + 1))) ++start;

            // Find the last vertex
            int end = start + 1;
            while (end < vertexDataManager.TotalVertices() &&
                  !ShapePath.IsNextPoly(vertexDataManager.Command(end))) ++end;

            InvertPolygon(start, end);
        }

        //--------------------------------------------------------------------
        // Join path. The path is joined with the existing one, that is,
        // it behaves as if the pen of a plotter was always down (drawing)
        //template<class VertexSource>
        public void JoinPath(VertexStorage vs)
        {
            JoinPath(vs, 0);
        }

        public void JoinPath(VertexStorage vs, int path_id)
        {
            double x, y;
            vs.Rewind(path_id);
            FlagsAndCommand PathAndFlags = vs.Vertex(out x, out y);
            if (!ShapePath.IsStop(PathAndFlags))
            {
                if (ShapePath.IsVertex(PathAndFlags))
                {
                    double x0, y0;
                    FlagsAndCommand PathAndFlags0 = LastVertex(out x0, out y0);
                    if (ShapePath.IsVertex(PathAndFlags0))
                    {
                        if (agg_math.CalcDistance(x, y, x0, y0) > agg_math.vertex_dist_epsilon)
                        {
                            if (ShapePath.IsMoveTo(PathAndFlags)) PathAndFlags = FlagsAndCommand.LineTo;
                            vertexDataManager.AddVertex(x, y, PathAndFlags);
                        }
                    }
                    else
                    {
                        if (ShapePath.IsStop(PathAndFlags0))
                        {
                            PathAndFlags = FlagsAndCommand.MoveTo;
                        }
                        else
                        {
                            if (ShapePath.IsMoveTo(PathAndFlags)) PathAndFlags = FlagsAndCommand.LineTo;
                        }
                        vertexDataManager.AddVertex(x, y, PathAndFlags);
                    }
                }
                while (!ShapePath.IsStop(PathAndFlags = vs.Vertex(out x, out y)))
                {
                    vertexDataManager.AddVertex(x, y, ShapePath.IsMoveTo(PathAndFlags) ?
                                                    FlagsAndCommand.LineTo :
                                                    PathAndFlags);
                }
            }
        }

        public FlagsAndCommand LastVertex(out double x, out double y)
        {
            return vertexDataManager.LastVertex(out x, out y);
        }

        public void LineTo(Point2D position)
        {
            LineTo(position.x, position.y);
        }

        public void LineTo(Vector2 position)
        {
            LineTo(position.X, position.Y);
        }

        public void LineTo(double x, double y)
        {
            vertexDataManager.AddVertex(x, y, FlagsAndCommand.LineTo);
        }

        public void ModifyCommand(int index, FlagsAndCommand PathAndFlags)
        {
            vertexDataManager.ModifyCommand(index, PathAndFlags);
        }

        public void ModifyVertex(int index, double x, double y)
        {
            vertexDataManager.ModifyVertex(index, x, y);
        }

        public void ModifyVertex(int index, double x, double y, FlagsAndCommand PathAndFlags)
        {
            vertexDataManager.ModifyVertex(index, x, y, PathAndFlags);
        }

        public void MoveTo(Vector2 position)
        {
            MoveTo(position.X, position.Y);
        }

        public void MoveTo(double x, double y)
        {
            vertexDataManager.AddVertex(x, y, FlagsAndCommand.MoveTo);
        }

        public string SvgDString
        {
            get
            {
                return this.SvgDString();
            }

            set
            {
                this.ParseSvgDString(value);
            }
        }

        public FlagsAndCommand PrevVertex(out double x, out double y)
        {
            return vertexDataManager.PrevVertex(out x, out y);
        }

        public void RelToAbs(ref double x, ref double y)
        {
            if (vertexDataManager.TotalVertices() != 0)
            {
                double x2;
                double y2;
                if (ShapePath.IsVertex(vertexDataManager.LastVertex(out x2, out y2)))
                {
                    x += x2;
                    y += y2;
                }
            }
        }

        public void Clear()
        {
            vertexDataManager.Clear();
            iteratorIndex = 0;
        }

        public virtual void Rewind(int pathId)
        {
            iteratorIndex = pathId;
        }

        public void ShareVertexData(VertexStorage pathStorageToShareFrom)
        {
            vertexDataManager = pathStorageToShareFrom.vertexDataManager;
        }

        // Make path functions
        //--------------------------------------------------------------------
        public int StartNewPath()
        {
            if (!ShapePath.IsStop(vertexDataManager.LastCommand()))
            {
                vertexDataManager.AddVertex(0.0, 0.0, FlagsAndCommand.Stop);
            }
            return vertexDataManager.TotalVertices();
        }

        public int TotalVertices()
        {
            return vertexDataManager.TotalVertices();
        }

        //--------------------------------------------------------------------
        public void Transform(Transform.Affine trans)
        {
            Transform(trans, 0);
        }

        public void Transform(Transform.Affine trans, int path_id)
        {
            int num_ver = vertexDataManager.TotalVertices();
            for (; path_id < num_ver; path_id++)
            {
                double x, y;
                FlagsAndCommand PathAndFlags = vertexDataManager.Vertex(path_id, out x, out y);
                if (ShapePath.IsStop(PathAndFlags)) break;
                if (ShapePath.IsVertex(PathAndFlags))
                {
                    trans.Transform(ref x, ref y);
                    vertexDataManager.ModifyVertex(path_id, x, y);
                }
            }
        }

        //--------------------------------------------------------------------
        public void TransformAllPaths(Transform.Affine trans)
        {
            int index;
            int num_ver = vertexDataManager.TotalVertices();
            for (index = 0; index < num_ver; index++)
            {
                double x, y;
                if (ShapePath.IsVertex(vertexDataManager.Vertex(index, out x, out y)))
                {
                    trans.Transform(ref x, ref y);
                    vertexDataManager.ModifyVertex(index, x, y);
                }
            }
        }

        //--------------------------------------------------------------------
        public void Translate(double dx, double dy)
        {
            Translate(dx, dy, 0);
        }

        public void Translate(double dx, double dy, int path_id)
        {
            int num_ver = vertexDataManager.TotalVertices();
            for (; path_id < num_ver; path_id++)
            {
                double x, y;
                FlagsAndCommand PathAndFlags = vertexDataManager.Vertex(path_id, out x, out y);
                if (ShapePath.IsStop(PathAndFlags)) break;
                if (ShapePath.IsVertex(PathAndFlags))
                {
                    x += dx;
                    y += dy;
                    vertexDataManager.ModifyVertex(path_id, x, y);
                }
            }
        }

        public void TranslateAllPaths(double dx, double dy)
        {
            int index;
            int num_ver = vertexDataManager.TotalVertices();
            for (index = 0; index < num_ver; index++)
            {
                double x, y;
                if (ShapePath.IsVertex(vertexDataManager.Vertex(index, out x, out y)))
                {
                    x += dx;
                    y += dy;
                    vertexDataManager.ModifyVertex(index, x, y);
                }
            }
        }

        public VertexData this[int index]
        {
            get => vertexDataManager[index];
            set => vertexDataManager[index] = value;
        }

        public FlagsAndCommand Vertex(int index, out double x, out double y)
        {
            return vertexDataManager.Vertex(index, out x, out y);
        }

        public FlagsAndCommand Vertex(out double x, out double y)
        {
            if (iteratorIndex >= vertexDataManager.TotalVertices())
            {
                x = 0;
                y = 0;
                return FlagsAndCommand.Stop;
            }

            return vertexDataManager.Vertex(iteratorIndex++, out x, out y);
        }

        public void VerticalLineTo(double y)
        {
            vertexDataManager.AddVertex(GetLastX(), y, FlagsAndCommand.LineTo);
        }

        public IEnumerable<VertexData> Vertices()
        {
            int count = vertexDataManager.TotalVertices();
            for (int i = 0; i < count; i++)
            {
                yield return vertexDataManager[i];
            }

            yield return new VertexData(FlagsAndCommand.Stop, new Vector2(0, 0));
        }

        private void InvertPolygon(int start, int end)
        {
            int i;
            FlagsAndCommand tmp_PathAndFlags = vertexDataManager.Command(start);

            --end; // Make "end" inclusive

            // Shift all commands to one position
            for (i = start; i < end; i++)
            {
                vertexDataManager.ModifyCommand(i, vertexDataManager.Command(i + 1));
            }

            // Assign starting command to the ending command
            vertexDataManager.ModifyCommand(end, tmp_PathAndFlags);

            // Reverse the polygon
            while (end > start)
            {
                vertexDataManager.SwapVertices(start++, end--);
            }
        }

        private FlagsAndCommand PerceivePolygonOrientation(int start, int end)
        {
            // Calculate signed area (double area to be exact)
            //---------------------
            int np = end - start;
            double area = 0.0;
            int i;
            for (i = 0; i < np; i++)
            {
                double x1, y1, x2, y2;
                vertexDataManager.Vertex(start + i, out x1, out y1);
                vertexDataManager.Vertex(start + (i + 1) % np, out x2, out y2);
                area += x1 * y2 - y1 * x2;
            }
            return (area < 0.0) ? FlagsAndCommand.FlagCW : FlagsAndCommand.FlagCCW;
        }
    }
}