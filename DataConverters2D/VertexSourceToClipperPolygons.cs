﻿/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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

using System.Collections.Generic;
using System.Linq;
using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.DataConverters2D
{
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public static class VertexSourceToClipperPolygons
	{
		public static VertexStorage CreateVertexStorage(this Polygons polygons, double scaling = 1000)
		{
			var output = new VertexStorage();

			foreach (Polygon polygon in polygons)
			{
				bool first = true;

				foreach (IntPoint point in polygon)
				{
					if (first)
					{
						output.Add(point.X / scaling, point.Y / scaling, FlagsAndCommand.MoveTo);
						first = false;
					}
					else
					{
						output.Add(point.X / scaling, point.Y / scaling, FlagsAndCommand.LineTo);
					}
				}

				output.ClosePolygon();
			}

			return output;
		}

		public static VertexStorage Offset(this IVertexSource a, double distance, JoinType joinType = JoinType.jtMiter, double scale = 1000)
		{
			var aPolys = a.CreatePolygons(scale);

			aPolys = aPolys.GetCorrectedWinding();

			var offseter = new ClipperOffset();
			offseter.AddPaths(aPolys, joinType, EndType.etClosedPolygon);
			var solution = new Polygons();
			offseter.Execute(ref solution, distance * scale);

			Clipper.CleanPolygons(solution);

			VertexStorage output = solution.CreateVertexStorage();

			output.Add(0, 0, FlagsAndCommand.Stop);

			return output;
		}

		public static Polygons GetCorrectedWinding(this Polygons polygonsToFix)
		{
			polygonsToFix = Clipper.CleanPolygons(polygonsToFix);
			var boundsPolygon = new Polygon();
			IntRect bounds = Clipper.GetBounds(polygonsToFix);
			bounds.left -= 10;
			bounds.top -= 10;
			bounds.bottom += 10;
			bounds.right += 10;

			boundsPolygon.Add(new IntPoint(bounds.left, bounds.top));
			boundsPolygon.Add(new IntPoint(bounds.right, bounds.top));
			boundsPolygon.Add(new IntPoint(bounds.right, bounds.bottom));
			boundsPolygon.Add(new IntPoint(bounds.left, bounds.bottom));

			var clipper = new Clipper();

			clipper.AddPaths(polygonsToFix, PolyType.ptSubject, true);
			clipper.AddPath(boundsPolygon, PolyType.ptClip, true);

			var intersectionResult = new PolyTree();
			clipper.Execute(ClipType.ctIntersection, intersectionResult);

			Polygons outputPolygons = Clipper.ClosedPathsFromPolyTree(intersectionResult);

			return outputPolygons;
		}

		public static Polygons CreatePolygons(this IVertexSource sourcePath, double scaling = 1000)
		{
            return CreatePolygons(new FlattenCurves(sourcePath)
            {
                ResolutionScale = scaling
            }.Vertices());
        }

		public static Polygons CreatePolygons(this IEnumerable<VertexData> vertices, double scaling = 1000)
		{
			var allPolys = new Polygons();
			Polygon currentPoly = null;

			foreach (VertexData vertexData in vertices)
			{
				if (vertexData.Command == FlagsAndCommand.MoveTo
					|| vertexData.IsLineTo)
				{
					// MoveTo always creates a new polygon
					if (vertexData.Command == FlagsAndCommand.MoveTo)
					{
						currentPoly = null;
					}

					// Construct current polygon if unset
					if (currentPoly == null)
					{
						currentPoly = new Polygon();
						allPolys.Add(currentPoly);
					}

					// Add polygon point for LineTo or MoveTo command
					currentPoly.Add(new IntPoint(vertexData.Position.X * scaling, vertexData.Position.Y * scaling));
				}
				else if (vertexData.Command != FlagsAndCommand.FlagNone)
				{
					// Clear active, reconstructed on first valid point
					currentPoly = null;
				}
			}

			return allPolys;
		}
	}
}