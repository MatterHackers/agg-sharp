/*
Copyright (c) 2025, Lars Brubaker
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
using ClipperLib;
using MatterHackers.DataConverters2D;
using MatterHackers.Agg.QuadTree;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Processors
{
	using Polygon = List<ClipperLib.IntPoint>;
	using Polygons = List<List<ClipperLib.IntPoint>>;

	public static class PathStitcher
	{
		public static Mesh Stitch(Polygons bottomLoop, double bottomHeight, Polygons topLoop, double topHeight, double scaling = 1000)
		{
			// only a bottom
			if (bottomLoop?.Count > 0
				&& (topLoop == null || topLoop.Count == 0))
			{
				// if there is no top than we need to create a top
				return CreateTop(bottomLoop, bottomHeight, scaling);
			}

			// only a top
			if ((bottomLoop == null || bottomLoop.Count == 0)
				&& topLoop?.Count > 0)
			{
				// if there is no bottom than we need to create  bottom
				return CreateBottom(topLoop, topHeight, scaling);
			}

			// simple bottom and top
			if (bottomLoop != null
				&& topLoop != null
				&& bottomLoop.Count == topLoop.Count)
			{
				var mesh = new Mesh();
				for (int i = 0; i < bottomLoop.Count; i++)
				{
					if (bottomLoop[i].Count == topLoop[i].Count)
					{
						mesh.CopyAllFaces(CreateSimpleWall(bottomLoop[i], bottomHeight * 1000, topLoop[i], topHeight * 1000), Matrix4X4.Identity);
					}
					else
					{
						mesh.CopyAllFaces(Stitch2SingleWalls(bottomLoop[i], bottomHeight * 1000, topLoop[i], topHeight * 1000), Matrix4X4.Identity);
					}
				}

				mesh.Transform(Matrix4X4.CreateScale(1 / scaling));
				return mesh;
			}

			return null;
		}

		private static Mesh CreateTop(Polygons path, double topHeight, double scaling)
		{
			return path.CreateVertexStorage(scaling).TriangulateFaces(zHeight: topHeight);
		}

		private static Mesh CreateBottom(Polygons path, double bottomHeight, double scaling)
		{
			var mesh = path.CreateVertexStorage(scaling).TriangulateFaces(zHeight: bottomHeight);
			mesh.ReverseFaces();
			return mesh;
		}

		public static (int indexA, int indexB) BestStartIndices(Polygon loopA, Polygon loopB)
		{
			var bestDistance = double.MaxValue;
			var bestIndexA = 0;
			var bestIndexB = 0;
			for (var indexA = 0; indexA < loopA.Count; indexA++)
			{
				for (var indexB = 0; indexB < loopB.Count; indexB++)
				{
					var distance = (loopA[indexA] - loopB[indexB]).LengthSquared();
					if (distance < bestDistance)
					{
						bestDistance = distance;
						bestIndexA = indexA;
						bestIndexB = indexB;
					}
				}
			}

			return (bestIndexA, bestIndexB);
		}

		private static Mesh Stitch2SingleWalls(Polygon loopA, double heightA, Polygon loopB, double heightB)
		{
			var mesh = new Mesh();

			var (startIndexA, startIndexB) = BestStartIndices(loopA, loopB);

			var curIndexA = startIndexA;
			var curIndexB = startIndexB;
			var loopedA = false;
			var loopedB = false;
			do
			{
				var nextIndexA = (curIndexA + 1) % loopA.Count;
				var nextIndexB = (curIndexB + 1) % loopB.Count;

				var segmentCurAToNextB = new Polygon() { loopA[curIndexA], loopB[nextIndexB] };
				var lengthCurAToNextB = segmentCurAToNextB.LengthSquared(false);
                // make sure this segments does not intersect either loop
                var intersectsWithA = loopA.FindIntersection(loopA[curIndexA], loopB[nextIndexB]) == Agg.QuadTree.Intersection.Intersect;
                
                var segmentCurBToNextA = new Polygon() { loopB[curIndexB], loopA[nextIndexA] };
				var lengthCurBToNextA = segmentCurBToNextA.LengthSquared();
				// make sure this segments does not intersect either loop

				if ((lengthCurAToNextB > lengthCurBToNextA && !loopedA && intersectsWithA)
					|| loopedB)
				{
					mesh.CreateFace(
						new Vector3(loopA[curIndexA].X, loopA[curIndexA].Y, heightA),
						new Vector3(loopA[nextIndexA].X, loopA[nextIndexA].Y, heightA),
						new Vector3(loopB[curIndexB].X, loopB[curIndexB].Y, heightB));

					curIndexA = nextIndexA;
					loopedA = curIndexA == startIndexA;
				}
				else
				{
					mesh.CreateFace(
						new Vector3(loopA[curIndexA].X, loopA[curIndexA].Y, heightA),
						new Vector3(loopB[nextIndexB].X, loopB[nextIndexB].Y, heightB),
						new Vector3(loopB[curIndexB].X, loopB[curIndexB].Y, heightB));

					curIndexB = nextIndexB;
					loopedB = curIndexB == startIndexB;
				}
			} while (curIndexA != startIndexA || curIndexB != startIndexB);


			return mesh;
		}

		private static Mesh CreateSimpleWall(Polygon bottomLoop, double bottomHeight, Polygon topLoop, double topHeight)
		{
			var mesh = new Mesh();
			for (int i=0; i<bottomLoop.Count; i++)
			{
				var next = (i + 1) % bottomLoop.Count;
				mesh.CreateFace(
					new Vector3(bottomLoop[i].X, bottomLoop[i].Y, bottomHeight),
					new Vector3(bottomLoop[next].X, bottomLoop[next].Y, bottomHeight),
					new Vector3(topLoop[i].X, topLoop[i].Y, topHeight));
				mesh.CreateFace(
					new Vector3(bottomLoop[next].X, bottomLoop[next].Y, bottomHeight),
					new Vector3(topLoop[next].X, topLoop[next].Y, topHeight),
					new Vector3(topLoop[i].X, topLoop[i].Y, topHeight));
			}

			return mesh;
		}

		public static int GetPolygonToAdvance(Polygon outerLoop, int outerIndex, Polygon innerLoop, int innerIndex)
		{
			var outerStart = outerLoop[outerIndex];
			var outerNextIndex = (outerIndex + 1) % outerLoop.Count;
			var outerNext = outerLoop[outerNextIndex];

			var innerStart = innerLoop[innerIndex];
			var innerNextIndex = (innerIndex + 1) % innerLoop.Count;
			var innerNext = innerLoop[innerNextIndex];

			var distanceToInnerNext = (innerNext - outerStart).LengthSquared();
			var distanceToOuterNext = (innerStart - outerNext).LengthSquared();

			var innerAdvanceCrosses = SegmentCrossesPolygon(innerLoop, outerStart, innerNext);
			var outerAdvanceCrosses = SegmentCrossesPolygon(innerLoop, innerStart, outerNext);

			if (!innerAdvanceCrosses && (outerAdvanceCrosses || distanceToInnerNext <= distanceToOuterNext))
			{
				return 1;
			}

			return 0;
		}

		private static bool SegmentCrossesPolygon(Polygon polygon, IntPoint start, IntPoint end)
		{
			for (int i = 0; i < polygon.Count; i++)
			{
				var edgeStart = polygon[i];
				var edgeEnd = polygon[(i + 1) % polygon.Count];

				if (start == edgeStart || start == edgeEnd || end == edgeStart || end == edgeEnd)
				{
					continue;
				}

				if (QTPolygonExtensions.GetIntersection(start, end, edgeStart, edgeEnd) != MatterHackers.Agg.QuadTree.Intersection.None)
				{
					return true;
				}
			}

			return false;
		}
	}
}
