﻿/*
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
using System.Collections.Generic;

namespace ClipperLib
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public static class PolygonsExtensions
    {
        public static Polygons CombinePolygons(this Polygons aPolys, Polygons bPolys, ClipType clipType, PolyFillType fillType = PolyFillType.pftEvenOdd)
        {
            var clipper = new Clipper();
            clipper.AddPaths(aPolys, PolyType.ptSubject, true);
            clipper.AddPaths(bPolys, PolyType.ptClip, true);

            var outputPolys = new Polygons();
            clipper.Execute(clipType, outputPolys, fillType);
            return outputPolys;
        }

        public static Polygons CreateFromString(string polygonsPackedString, double scale = 1)
        {
            Polygon SinglePolygon(string polygonString)
            {
                var poly = new Polygon();
                string[] intPointData = polygonString.Split(',');
                int increment = 2;
                for (int i = 0; i < intPointData.Length - 1; i += increment)
                {
                    var nextIntPoint = new IntPoint(GetDouble(intPointData[i], scale), GetDouble(intPointData[i + 1], scale));
                    poly.Add(nextIntPoint);
                }

                return poly;
            }

            Polygons output = new Polygons();
            string[] polygons = polygonsPackedString.Split('|');
            foreach (string polygonString in polygons)
            {
                Polygon nextPoly = SinglePolygon(polygonString);
                if (nextPoly.Count > 0)
                {
                    output.Add(nextPoly);
                }
            }

            return output;
        }

        public static RectangleDouble GetBounds(this Polygons polys)
        {
            RectangleDouble bounds = RectangleDouble.ZeroIntersection;
            foreach (var poly in polys)
            {
                bounds.ExpandToInclude(poly.GetBounds());
            }

            return bounds;
        }

        public static RectangleLong GetBoundsLong(this Polygons polys)
        {
            RectangleLong bounds = RectangleLong.ZeroIntersection;
            foreach (var poly in polys)
            {
                bounds.ExpandToInclude(poly.GetBoundsLong());
            }

            return bounds;
        }

        public static double Length(this Polygons polygons, bool areClosed = true)
        {
            double length = 0;
            for (int i = 0; i < polygons.Count; i++)
            {
                length += polygons[i].Length(areClosed);
            }

            return length;
        }

        public static Polygons Offset(this Polygons polygons, double distance, JoinType joinType)
        {
            var offseter = new ClipperOffset();
            offseter.AddPaths(polygons, joinType, EndType.etClosedPolygon);
            var solution = new Polygons();
            offseter.Execute(ref solution, distance);

            return solution;
        }

        public static void PolyTreeToPolygonSets(this Polygons inputPolygons, PolyNode node, List<Polygons> ret)
        {
            for (int n = 0; n < node.ChildCount; n++)
            {
                PolyNode child = node.Childs[n];
                var outputPolygons = new Polygons();
                outputPolygons.Add(child.Contour);
                for (int i = 0; i < child.ChildCount; i++)
                {
                    outputPolygons.Add(child.Childs[i].Contour);
                    inputPolygons.PolyTreeToPolygonSets(child.Childs[i], ret);
                }

                ret.Add(outputPolygons);
            }
        }

        public static Polygons Rotate(this Polygons polys, double radians)
        {
            var output = new Polygons(polys.Count);
            foreach (var poly in polys)
            {
                output.Add(poly.Rotate(radians));
            }

            return output;
        }

        public static Polygons Scale(this Polygons polys, double scaleX, double scaleY)
        {
            var output = new Polygons(polys.Count);
            foreach (var poly in polys)
            {
                output.Add(poly.Scale(scaleX, scaleY));
            }

            return output;
        }

        public static List<Polygons> SeparatePolygonGroups(this Polygons polygons)
        {
            var ret = new List<Polygons>();
            var clipper = new Clipper();
            var polyTree = new PolyTree();
            clipper.AddPaths(polygons, PolyType.ptSubject, true);
            clipper.Execute(ClipType.ctUnion, polyTree);

            polygons.PolyTreeToPolygonSets(polyTree, ret);
            return ret;
        }

        public static void PolyTreeToOutlinesAndContainedHoles(PolyNode polyTree, List<Polygons> outlinesAndHoles)
        {
            var closed = !polyTree.IsOpen;

            // if this is not a hole
            if (polyTree.m_polygon.Count > 0
                && closed
                && !polyTree.IsHole)
            {
                // create a new shape for the polygon data
                var outlineAndHoles = new Polygons
                {
                    new Polygon(polyTree.m_polygon)
                };

                outlinesAndHoles.Add(outlineAndHoles);

                // add all the children that are holes as holes
                foreach (var child in polyTree.Childs)
                {
                    if (child.m_polygon.Count > 0
                        && !child.IsOpen
                        && child.IsHole)
                    {
                        outlineAndHoles.Add(child.m_polygon);
                    }
                }
            }

            foreach (var child in polyTree.Childs)
            {
                PolyTreeToOutlinesAndContainedHoles(child, outlinesAndHoles);
            }
        }

        /// <summary>
        /// This will separate the polygons into outlines and holes. The outlines will be the first polygon in each set and the holes will be the rest.
        /// </summary>
        /// <param name="polygons"></param>
        /// <returns></returns>
        public static List<Polygons> SeparateIntoOutlinesAndContainedHoles(this Polygons polygons)
        {
            var ret = new List<Polygons>();
            var clipper = new Clipper();
            var polyTree = new PolyTree();
            clipper.AddPaths(polygons, PolyType.ptSubject, true);
            clipper.Execute(ClipType.ctUnion, polyTree);

            PolyTreeToOutlinesAndContainedHoles(polyTree, ret);
            return ret;
        }

        public static Polygons Subtract(this Polygons polygons, Polygons other)
        {
            return polygons.CombinePolygons(other, ClipType.ctDifference);
        }

        public static Polygons Subtract(this Polygons polygons, Polygon other)
        {
            return polygons.CombinePolygons(new Polygons() { other }, ClipType.ctDifference);
        }

        public static Polygons Translate(this Polygons polys, double x, double y, double scale = 1)
        {
            var output = new Polygons(polys.Count);
            foreach (var poly in polys)
            {
                output.Add(poly.Translate(x, y, scale));
            }

            return output;
        }

        public static Polygons Union(this Polygons polygons, Polygons other, PolyFillType fillType = PolyFillType.pftEvenOdd)
        {
            return polygons.CombinePolygons(other, ClipType.ctUnion, fillType);
        }

        public static Polygons Union(this Polygons polygons, Polygon other)
        {
            return polygons.CombinePolygons(new Polygons() { other }, ClipType.ctUnion);
        }

        public static Polygons Intersect(this Polygons polygons, Polygons other, PolyFillType fillType = PolyFillType.pftEvenOdd)
        {
            return polygons.CombinePolygons(other, ClipType.ctIntersection, fillType);
        }

        private static double GetDouble(string doubleString, double scale)
        {
            // strip leading characters up to and including ':'
            int colonIndex = doubleString.IndexOf(':');
            if (colonIndex != -1)
            {
                doubleString = doubleString.Substring(colonIndex + 1);
            }

            return double.Parse(doubleString) * scale;
        }
    }
}