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

using System.Collections.Generic;
using ClipperLib;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Csg
{
	public static class SliceLayer
	{
		public static List<List<IntPoint>> GetPolygonXYLoopsAt0(this Mesh mesh, Matrix4X4 matrix, double outputScale = 1000)
		{
			var slicePlane = new Plane(Vector3.UnitZ, 0);

			// transform our plane to the mesh
			var toMeshMatrix = matrix.Inverted;
			var planeInMeshSpace = new Plane(
				Vector3Ex.TransformNormal(slicePlane.Normal, toMeshMatrix),
				Vector3Ex.Transform(slicePlane.Normal * slicePlane.DistanceFromOrigin, toMeshMatrix));

			return CreateSlice(mesh, planeInMeshSpace);
		}

		public static List<List<IntPoint>> CreateSlice(Mesh mesh, Plane plane, int outputScale = 1000)
		{
			var unorderedSegments = GetUnorderdSegments(mesh, plane, outputScale);

			// connect all the segments together into polygons
			var closedPolygons = FindClosedPolygons(unorderedSegments);

			return UnionClosedPolygons(closedPolygons);
		}

		public static List<List<IntPoint>> UnionClosedPolygons(List<List<IntPoint>> closedPolygons)
		{
			if (closedPolygons.Count > 1)
			{
				return closedPolygons.GetRange(0, 1).Union(closedPolygons.GetRange(1, closedPolygons.Count - 1), PolyFillType.pftNonZero);
			}

			return closedPolygons;
		}

		public static List<Segment> GetUnorderdSegments(Mesh mesh, Plane plane, int outputScale = 1000)
		{
			var rotation = new Quaternion(plane.Normal, Vector3.UnitZ);
			var flattenedMatrix = Matrix4X4.CreateRotation(rotation);
			flattenedMatrix *= Matrix4X4.CreateTranslation(0, 0, -plane.DistanceFromOrigin);

			// collect all the segments this plane intersects and record them in unordered segments in z 0 space
			var meshTo0Plane = flattenedMatrix * Matrix4X4.CreateScale(outputScale);

			var unorderedSegments = new List<Segment>();
			foreach (var face in mesh.Faces)
			{
				var start = Vector3.Zero;
				var end = Vector3.Zero;
				if (face.GetCutLine(mesh.Vertices, plane, ref start, ref end))
				{
					var startAtZ0 = Vector3Ex.Transform(start, meshTo0Plane);
					var endAtZ0 = Vector3Ex.Transform(end, meshTo0Plane);
					unorderedSegments.Add(
						new Segment(
							new IntPoint(startAtZ0.X, startAtZ0.Y),
							new IntPoint(endAtZ0.X, endAtZ0.Y)));
				}
			}

			return unorderedSegments;
		}

		public static List<List<IntPoint>> FindClosedPolygons(List<Segment> UnorderedSegments)
		{
			var startIndexes = CreateFastIndexLookup(UnorderedSegments);

			var segmentHasBeenAdded = new bool[UnorderedSegments.Count];

			var openPolygonList = new List<List<IntPoint>>();
			var closedPolygons = new List<List<IntPoint>>();

			for (int startingSegmentIndex = 0; startingSegmentIndex < UnorderedSegments.Count; startingSegmentIndex++)
			{
				if (segmentHasBeenAdded[startingSegmentIndex])
				{
					continue;
				}

				var poly = new List<IntPoint>();
				// We start by adding the start, as we will add ends from now on.
				var polygonStartPosition = UnorderedSegments[startingSegmentIndex].Start;
				poly.Add(polygonStartPosition);

				int segmentIndexBeingAdded = startingSegmentIndex;
				bool canClose;

				while (true)
				{
					canClose = false;
					segmentHasBeenAdded[segmentIndexBeingAdded] = true;
					var addedSegmentEndPoint = UnorderedSegments[segmentIndexBeingAdded].End;

					poly.Add(addedSegmentEndPoint);
					segmentIndexBeingAdded = GetTouchingSegmentIndex(UnorderedSegments, startIndexes, segmentHasBeenAdded, addedSegmentEndPoint);
					if (segmentIndexBeingAdded == -1)
					{
						// if we have looped back around to where we started
						if (addedSegmentEndPoint == polygonStartPosition)
						{
							canClose = true;
						}

						break;
					}
					else
					{
						var foundSegmentStart = UnorderedSegments[segmentIndexBeingAdded].Start;
						if (addedSegmentEndPoint == foundSegmentStart)
						{
							// if we have looped back around to where we started
							if (addedSegmentEndPoint == polygonStartPosition)
							{
								canClose = true;
							}
						}
					}
				}

				if (canClose)
				{
					closedPolygons.Add(poly);
				}
				else
				{
					openPolygonList.Add(poly);
				}
			}

			// Remove all polygons from the open polygon list that have 0 points
			for (int i = openPolygonList.Count - 1; i >= 0; i--)
			{
				// add in the position of the last point
				if (openPolygonList[i].Count == 0)
				{
					openPolygonList.RemoveAt(i);
				}
				else // check if every point is the same
				{
					bool allSame = true;
					var first = openPolygonList[i][0];
					for (int j = 1; j < openPolygonList[i].Count; j++)
					{
						if (openPolygonList[i][j] != first)
						{
							allSame = false;
							break;
						}
					}

					if (allSame)
					{
						openPolygonList.RemoveAt(i);
					}
				}
			}

			var startSorter = new SortedIntPoint();
			for (int i = 0; i < openPolygonList.Count; i++)
			{
				startSorter.Add(i, openPolygonList[i][0]);
			}

			startSorter.Sort();

			var endSorter = new SortedIntPoint();
			for (int i = 0; i < openPolygonList.Count; i++)
			{
				endSorter.Add(i, openPolygonList[i][openPolygonList[i].Count - 1]);
			}

			endSorter.Sort();

			// Link up all the missing ends, closing up the smallest gaps first. This is an inefficient implementation which can run in O(n*n*n) time.
			while (true)
			{
				double bestScore = double.MaxValue;
				int bestA = -1;
				int bestB = -1;
				bool reversed = false;
				for (int polygonAIndex = 0; polygonAIndex < openPolygonList.Count; polygonAIndex++)
				{
					if (openPolygonList[polygonAIndex].Count < 1)
					{
						continue;
					}

					var aEndPosition = openPolygonList[polygonAIndex][openPolygonList[polygonAIndex].Count - 1];
					// find the closestStartFromEnd
					int bStartIndex = startSorter.FindClosetIndex(aEndPosition, out double distanceToStartSqrd);
					if (distanceToStartSqrd < bestScore)
					{
						bestScore = distanceToStartSqrd;
						bestA = polygonAIndex;
						bestB = bStartIndex;
						reversed = false;

						if (bestScore == 0)
						{
							// found a perfect match stop looking
							break;
						}
					}

					// find the closestStartFromStart
					int bEndIndex = endSorter.FindClosetIndex(aEndPosition, out double distanceToEndSqrd, polygonAIndex);
					if (distanceToEndSqrd < bestScore)
					{
						bestScore = distanceToEndSqrd;
						bestA = polygonAIndex;
						bestB = bEndIndex;
						reversed = true;

						if (bestScore == 0)
						{
							// found a perfect match stop looking
							break;
						}
					}

					if (bestScore == 0)
					{
						// found a perfect match stop looking
						break;
					}
				}

				if (bestScore >= double.MaxValue)
				{
					// we could not find any points to connect this to
					break;
				}

				if (bestA == bestB) // This loop connects to itself, close the polygon.
				{
					closedPolygons.Add(new List<IntPoint>(openPolygonList[bestA]));
					openPolygonList[bestA].Clear(); // B is cleared as it is A
					endSorter.Remove(bestA);
					startSorter.Remove(bestA);
				}
				else
				{
					if (reversed)
					{
						if (openPolygonList[bestA].Count > openPolygonList[bestB].Count)
						{
							for (int indexB = openPolygonList[bestB].Count - 1; indexB >= 0; indexB--)
							{
								openPolygonList[bestA].Add(openPolygonList[bestB][indexB]);
							}

							openPolygonList[bestB].Clear();
							endSorter.Remove(bestB);
							startSorter.Remove(bestB);
						}
						else
						{
							for (int indexA = openPolygonList[bestA].Count - 1; indexA >= 0; indexA--)
							{
								openPolygonList[bestB].Add(openPolygonList[bestA][indexA]);
							}

							openPolygonList[bestA].Clear();
							endSorter.Remove(bestA);
							startSorter.Remove(bestA);
						}
					}
					else
					{
						openPolygonList[bestA].AddRange(openPolygonList[bestB]);
						openPolygonList[bestB].Clear();
						endSorter.Remove(bestB);
						startSorter.Remove(bestB);
					}
				}
			}

			double minimumPerimeter = .01;
			for (int polygonIndex = 0; polygonIndex < closedPolygons.Count; polygonIndex++)
			{
				double perimeterLength = 0;

				for (int intPointIndex = 1; intPointIndex < closedPolygons[polygonIndex].Count; intPointIndex++)
				{
					perimeterLength += (closedPolygons[polygonIndex][intPointIndex] - closedPolygons[polygonIndex][intPointIndex - 1]).Length();
					if (perimeterLength > minimumPerimeter)
					{
						break;
					}
				}
				if (perimeterLength < minimumPerimeter)
				{
					closedPolygons.RemoveAt(polygonIndex);
					polygonIndex--;
				}
			}

			return Clipper.CleanPolygons(closedPolygons, 10);
		}

		private static Dictionary<(long, long), List<int>> CreateFastIndexLookup(List<Segment> UnorderedSegments)
		{
			var startIndexes = new Dictionary<(long, long), List<int>>();

			for (int startingSegmentIndex = 0; startingSegmentIndex < UnorderedSegments.Count; startingSegmentIndex++)
			{
				var position = UnorderedSegments[startingSegmentIndex].Start;
				var positionKey = (position.X, position.Y);
				if (!startIndexes.ContainsKey(positionKey))
				{
					startIndexes.Add(positionKey, new List<int>());
				}

				startIndexes[positionKey].Add(startingSegmentIndex);
			}

			return startIndexes;
		}

		private static int GetTouchingSegmentIndex(List<Segment> UnorderedSegments,
			Dictionary<(long, long), List<int>> startIndexes,
			bool[] segmentHasBeenAdded,
			IntPoint addedSegmentEndPoint)
		{
			int lookupSegmentIndex = -1;
			var positionKey = (addedSegmentEndPoint.X, addedSegmentEndPoint.Y);
			if (startIndexes.ContainsKey(positionKey))
			{
				foreach (int index in startIndexes[positionKey])
				{
					if (!segmentHasBeenAdded[index])
					{
						if (UnorderedSegments[index].Start == addedSegmentEndPoint)
						{
							lookupSegmentIndex = index;
						}
					}
				}
			}

			return lookupSegmentIndex;
		}
	}

	public static class IntPointPolygonsExtensions
	{
		public static IEnumerable<VertexData> Vertices(this List<List<IntPoint>> polygons, double outputScale = 1000)
		{
			foreach (var polygon in polygons)
			{
				if (polygon.Count > 2)
				{
					foreach (var vertex in polygon.Vertices(outputScale))
					{
						yield return vertex;
					}
				}
			}
		}
	}

	public static class IntPointPolygonExtensions
	{
		public static IEnumerable<VertexData> Vertices(this List<IntPoint> polygon, double outputScale = 1000)
		{
			// start at the last point
			yield return new VertexData(Agg.ShapePath.FlagsAndCommand.MoveTo,
				new Vector2(polygon[polygon.Count - 1].X / outputScale, polygon[polygon.Count - 1].Y / outputScale));

			for (int i = 0; i < polygon.Count; i++)
			{
				yield return new VertexData(Agg.ShapePath.FlagsAndCommand.LineTo,
					new Vector2(polygon[i].X / outputScale, polygon[i].Y / outputScale));
			}
		}

		public static double Area(this List<IntPoint> polygon)
		{
			var count = polygon.Count;

			if (count < 3)
			{
				return 0;
			}

			double a = 0;
			var lastPoint = count - 1;
			for (int i = 0; i < count; i++)
			{
				a += ((double)polygon[lastPoint].X + polygon[i].X) * ((double)polygon[lastPoint].Y - polygon[i].Y);
				lastPoint = i;
			}

			return -a * 0.5;
		}
	}
}