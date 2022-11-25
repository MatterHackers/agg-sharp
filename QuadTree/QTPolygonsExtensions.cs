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

using System;
using System.Collections.Generic;
using ClipperLib;
using Polygon = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;

namespace MatterHackers.QuadTree
{
	[Flags]
	internal enum Altered
	{
		Remove = 1,
		Merged = 2
	}

	public static class QTPolygonsExtensions
	{
		public static IEnumerable<(int polyIndex, int pointIndex, IntPoint position)> FindCrossingPoints(this Polygons polygons,
			IntPoint start,
			IntPoint end,
			QuadTree<(int polyIndex, QuadTree<int> quadTree)> edgeQuadTrees)
		{
			edgeQuadTrees.SearchArea(new Quad(start, end));
			foreach (var item in edgeQuadTrees.QueryResults)
			// for (int polyIndex = 0; polyIndex < polygons.Count; polyIndex++)
			{
				foreach (var crossing in polygons[item.polyIndex].FindCrossingPoints(start,
					end,
					edgeQuadTrees == null ? null : item.quadTree))
				{
					yield return (item.polyIndex, crossing.pointIndex, crossing.position);
				}
			}
		}

		internal class PolygonGroups : INearestNeighbours<(int polygonIndex, int pointIndex)>
		{
			private QuadTree<int> quadTree;
			private List<INearestNeighbours<int>> polygonSearch = new List<INearestNeighbours<int>>();

			internal PolygonGroups(Polygons inPolygons)
			{
				var expandDist = 1;
				var allBounds = inPolygons.GetBoundsLong();
				allBounds.Inflate(expandDist);
				quadTree = new QuadTree<int>(5, allBounds.minX, allBounds.minY, allBounds.maxX, allBounds.maxY);
				for (int i = 0; i < inPolygons.Count; i++)
				{
					var polygon = inPolygons[i];
					var bounds = polygon.GetBoundsLong();

					quadTree.Insert(i, new Quad(bounds.minX, bounds.minY, bounds.maxX, bounds.maxY));

					polygonSearch.Add(polygon.GetNearestNeighbourAccelerator());
				}
			}

			public (int polygonIndex, int pointIndex) GetNearestNeighbour(IntPoint position)
			{
				quadTree.SearchPoint(position.X, position.Y);
				foreach (var index in quadTree.QueryResults)
				{
					polygonSearch[index]?.GetNearestNeighbour(position);
				}

				return (-1, -1);
			}
		}

		public static INearestNeighbours<(int polygonIndex, int pointIndex)> GetNearestNeighbourAccelerator(this Polygons inPolygons)
		{
			return new PolygonGroups(inPolygons);
		}

		public static Intersection FindIntersection(this Polygons polygons,
			IntPoint start,
			IntPoint end,
			QuadTree<(int polyIndex, QuadTree<int> quadTree)> edgeQuadTrees = null)
		{
			Intersection bestIntersection = Intersection.None;
			edgeQuadTrees.SearchArea(new Quad(start, end));
			foreach (var item in edgeQuadTrees.QueryResults)
			// for (int polyIndex = 0; polyIndex < polygons.Count; polyIndex++)
			{
				var result = polygons[item.polyIndex].FindIntersection(start, end, item.quadTree);
				if (result == Intersection.Intersect)
				{
					return Intersection.Intersect;
				}
				else if (result == Intersection.Colinear)
				{
					bestIntersection = Intersection.Colinear;
				}
			}

			return bestIntersection;
		}

		public static Tuple<int, int> FindPoint(this Polygons polygons, IntPoint position, List<INearestNeighbours<int>> nearestNeighbours = null)
		{
			if (nearestNeighbours != null)
			{
				for (int polyIndex = 0; polyIndex < polygons.Count; polyIndex++)
				{
					var pointIndex = polygons[polyIndex].FindPoint(position, nearestNeighbours[polyIndex]);
					if (pointIndex != -1)
					{
						return new Tuple<int, int>(polyIndex, pointIndex);
					}
				}
			}
			else
			{
				for (int polyIndex = 0; polyIndex < polygons.Count; polyIndex++)
				{
					int pointIndex = polygons[polyIndex].FindPoint(position);
					if (pointIndex != -1)
					{
						return new Tuple<int, int>(polyIndex, pointIndex);
					}
				}
			}

			return null;
		}

		/// <summary>
		/// Create the list of polygon segments (not closed) that represent the parts of the source polygons that are close (almost touching).
		/// </summary>
		/// <param name="polygons">The polygons to search for thin lines</param>
		/// <param name="overlapMergeAmount">If edges under consideration, are this distance or less apart (but greater than minimumRequiredWidth) they will generate edges</param>
		/// <param name="minimumRequiredWidth">If the distance between edges is less this they will not be generated. This lets us avoid considering very thin lines.</param>
		/// <param name="onlyMergeLines">The output segments that are calculated</param>
		/// <param name="pathIsClosed">Is the source path closed (does not contain the last edge but assumes it).</param>
		/// <returns>If thin lines were detected</returns>
		public static bool FindThinLines(this Polygons polygons, long overlapMergeAmount, long minimumRequiredWidth, out Polygons onlyMergeLines, bool pathIsClosed = true)
		{
			polygons = Clipper.CleanPolygons(polygons, overlapMergeAmount / 8);
			bool pathHasMergeLines = false;

			polygons = MakeCloseSegmentsMergable(polygons, overlapMergeAmount, pathIsClosed);

			// make a copy that has every point duplicated (so that we have them as segments).
			List<Segment> polySegments = Segment.ConvertToSegments(polygons);

			var markedAltered = new Altered[polySegments.Count];

			var touchingEnumerator = new CloseSegmentsIterator(polySegments, overlapMergeAmount);
			int segmentCount = polySegments.Count;
			// now walk every segment and check if there is another segment that is similar enough to merge them together
			for (int firstSegmentIndex = 0; firstSegmentIndex < segmentCount; firstSegmentIndex++)
			{
				foreach (int checkSegmentIndex in touchingEnumerator.GetTouching(firstSegmentIndex, segmentCount))
				{
					// The first point of start and the last point of check (the path will be coming back on itself).
					long startDelta = (polySegments[firstSegmentIndex].Start - polySegments[checkSegmentIndex].End).Length();
					// if the segments are similar enough
					if (startDelta < overlapMergeAmount)
					{
						// The last point of start and the first point of check (the path will be coming back on itself).
						long endDelta = (polySegments[firstSegmentIndex].End - polySegments[checkSegmentIndex].Start).Length();
						if (endDelta < overlapMergeAmount)
						{
							// move the first segments points to the average of the merge positions
							long startEndWidth = Math.Abs((polySegments[firstSegmentIndex].Start - polySegments[checkSegmentIndex].End).Length());
							long endStartWidth = Math.Abs((polySegments[firstSegmentIndex].End - polySegments[checkSegmentIndex].Start).Length());
							long width = Math.Min(startEndWidth, endStartWidth);

							if (width > minimumRequiredWidth)
							{
								// We need to check if the new start position is on the inside of the curve. We can only add thin lines on the insides of our existing curves.
								IntPoint newStartPosition = (polySegments[firstSegmentIndex].Start + polySegments[checkSegmentIndex].End) / 2; // the start;
								IntPoint newStartDirection = newStartPosition - polySegments[firstSegmentIndex].Start;
								IntPoint normalLeft = (polySegments[firstSegmentIndex].End - polySegments[firstSegmentIndex].Start).GetPerpendicularLeft();
								long dotProduct = normalLeft.Dot(newStartDirection);
								if (dotProduct > 0)
								{
									pathHasMergeLines = true;

									polySegments[firstSegmentIndex].Start = newStartPosition;
									polySegments[firstSegmentIndex].End = (polySegments[firstSegmentIndex].End + polySegments[checkSegmentIndex].Start) / 2; // the end

									markedAltered[firstSegmentIndex] = Altered.Merged;
									// mark this segment for removal
									markedAltered[checkSegmentIndex] = Altered.Remove;
									// We only expect to find one match for each segment, so move on to the next segment
									break;
								}
							}
						}
					}
				}
			}

			// remove the marked segments
			for (int segmentIndex = segmentCount - 1; segmentIndex >= 0; segmentIndex--)
			{
				// remove every segment that has not been merged
				if (markedAltered[segmentIndex] != Altered.Merged)
				{
					polySegments.RemoveAt(segmentIndex);
				}
			}

			// go through the polySegments and create a new polygon for every connected set of segments
			onlyMergeLines = new Polygons();
			var currentPolygon = new Polygon();
			onlyMergeLines.Add(currentPolygon);
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
					// check if the polygon we are adding is big enough
					if (currentPolygon.Length() <= overlapMergeAmount * 2)
					{
						// remove it as it is too small
						onlyMergeLines.RemoveAt(onlyMergeLines.Count - 1);
					}

					// create a new polygon
					currentPolygon = new Polygon();
					onlyMergeLines.Add(currentPolygon);
				}
			}

			// add the end point
			if (polySegments.Count > 0)
			{
				currentPolygon.Add(polySegments[polySegments.Count - 1].End);

				// now check if it is long enough
				if (currentPolygon.Length() <= overlapMergeAmount * 2)
				{
					// remove it as it is too small
					onlyMergeLines.RemoveAt(onlyMergeLines.Count - 1);
				}
			}

			long cleanDistance = overlapMergeAmount / 40;
			Clipper.CleanPolygons(onlyMergeLines, cleanDistance);

			return pathHasMergeLines && onlyMergeLines.Count > 0;
		}

		public static QuadTree<int> GetQuadTree(this Polygons polygons, int splitCount = 5)
		{
			var expandDist = 1;
			var allBounds = polygons.GetBoundsLong();
			allBounds.Inflate(expandDist);
			var quadTree = new QuadTree<int>(5, allBounds.minX, allBounds.minY, allBounds.maxX, allBounds.maxY);
			for (int i = 0; i < polygons.Count; i++)
			{
				var polygon = polygons[i];
				var bounds = polygon.GetBoundsLong();

				quadTree.Insert(i, new Quad(bounds.minX, bounds.minY, bounds.maxX, bounds.maxY));
			}

			return quadTree;
		}

		public static QuadTree<(int polyIndex, QuadTree<int> quadTree)> GetEdgeQuadTrees(this Polygons polygons, int splitCount = 5, long expandDist = 1)
		{
			var bounds = polygons.GetBoundsLong();
			bounds.Inflate(expandDist);
			var quadTrees = new QuadTree<(int polyIndex, QuadTree<int> quadTree)>(5, new Quad(bounds.minX, bounds.minY, bounds.maxX, bounds.maxY));
			for (var i = 0; i < polygons.Count; i++)
			{
				var polygon = polygons[i];
				var bounds2 = polygon.GetBoundsLong();
				bounds2.Inflate(expandDist);
				var quad = new Quad(bounds2.minX, bounds2.minY, bounds2.maxX, bounds2.maxY);
				quadTrees.Insert((i, polygon.GetEdgeQuadTree(splitCount, expandDist)), quad);
			}

			return quadTrees;
		}

		public static IntPoint Center(this Polygons polygons)
		{
			var center = default(IntPoint);
			int count = 0;
			foreach (var polygon in polygons)
			{
				for (int positionIndex = 0; positionIndex < polygon.Count; positionIndex++)
				{
					center += polygon[positionIndex];
					count++;
				}
			}

			if (count > 0)
			{
				center /= count;
			}

			return center;
		}

		public static Polygons MakeCloseSegmentsMergable(this Polygons polygonsToSplit, long distanceNeedingAdd, bool pathsAreClosed = true)
		{
			var polygonAccelerator = polygonsToSplit.GetQuadTree();
			var splitPolygons = new Polygons();
			for (int i = 0; i < polygonsToSplit.Count; i++)
			{
				Polygon accumulatedSplits = polygonsToSplit[i];

				var bounds = accumulatedSplits.GetBoundsLong();
				bounds.Inflate(distanceNeedingAdd);
				polygonAccelerator.SearchArea(new Quad(bounds.minX, bounds.minY, bounds.maxX, bounds.maxY));

				foreach (var j in polygonAccelerator.QueryResults)
				{
					accumulatedSplits = QTPolygonExtensions.MakeCloseSegmentsMergable(accumulatedSplits, polygonsToSplit[j], distanceNeedingAdd, pathsAreClosed);
				}

				splitPolygons.Add(accumulatedSplits);
			}

			return splitPolygons;
		}

		public static (int polyIndex, int pointIndex, IntPoint position) FindClosestPoint(this Polygons boundaryPolygons,
			IntPoint position,
			Func<int, Polygon, bool> considerPolygon = null,
			Func<int, IntPoint, bool> considerPoint = null)
		{
			var polyPointPosition = (-1, -1, default(IntPoint));

			long bestDist = long.MaxValue;
			for (int polygonIndex = 0; polygonIndex < boundaryPolygons.Count; polygonIndex++)
			{
				if (considerPolygon == null || considerPolygon(polygonIndex, boundaryPolygons[polygonIndex]))
				{
					var closestToPoly = boundaryPolygons[polygonIndex].FindClosestPoint(position, considerPoint);
					if (closestToPoly.index != -1)
					{
						long length = (closestToPoly.position - position).Length();
						if (length < bestDist)
						{
							bestDist = length;
							polyPointPosition = (polygonIndex, closestToPoly.index, closestToPoly.position);
						}
					}
				}
			}

			return polyPointPosition;
		}

		public static void MovePointInsideBoundary(this Polygons boundaryPolygons,
			IntPoint startPosition,
			out (int polyIndex, int pointIndex, IntPoint position) polyPointPosition,
			QuadTree<(int polyIndex, QuadTree<int> quadTree)> edgeQuadTrees = null,
			INearestNeighbours<(int polygonIndex, int pointIndex)> nearestNeighbours = null,
			Func<IntPoint, InsideState> fastInsideCheck = null)
		{
			var bestPolyPointPosition = (0, 0, startPosition);

			if (boundaryPolygons.PointIsInside(startPosition, edgeQuadTrees, nearestNeighbours, fastInsideCheck))
			{
				// already inside
				polyPointPosition = (-1, -1, default(IntPoint));
				return;
			}

			long bestDist = long.MaxValue;
			for (int polygonIndex = 0; polygonIndex < boundaryPolygons.Count; polygonIndex++)
			{
				var boundaryPolygon = boundaryPolygons[polygonIndex];
				if (boundaryPolygon.Count < 3)
				{
					continue;
				}

				for (int pointIndex = 0; pointIndex < boundaryPolygon.Count; pointIndex++)
				{
					IntPoint segmentStart = boundaryPolygon[pointIndex];

					IntPoint pointRelStart = startPosition - segmentStart;
					long distFromStart = pointRelStart.Length();
					if (distFromStart < bestDist)
					{
						bestDist = distFromStart;
						bestPolyPointPosition = (polygonIndex, pointIndex, segmentStart);
					}

					IntPoint segmentEnd = boundaryPolygon[(pointIndex + 1) % boundaryPolygon.Count];

					IntPoint segmentDelta = segmentEnd - segmentStart;
					long segmentLength = segmentDelta.Length();
					IntPoint segmentLeft = segmentDelta.GetPerpendicularLeft();
					long segmentLeftLength = segmentLeft.Length();

					if (segmentLength != 0)
					{
						long distanceFromStart = segmentDelta.Dot(pointRelStart) / segmentLength;

						if (distanceFromStart >= 0 && distanceFromStart <= segmentDelta.Length())
						{
							long distToBoundarySegment = segmentLeft.Dot(pointRelStart) / segmentLeftLength;

							if (Math.Abs(distToBoundarySegment) < bestDist)
							{
								IntPoint pointAlongCurrentSegment = startPosition;
								if (distToBoundarySegment != 0)
								{
									pointAlongCurrentSegment = startPosition - segmentLeft * distToBoundarySegment / segmentLeftLength;
								}

								bestDist = Math.Abs(distToBoundarySegment);
								bestPolyPointPosition = (polygonIndex, pointIndex, pointAlongCurrentSegment);
							}
						}
					}
				}
			}

			polyPointPosition = bestPolyPointPosition;
		}

		public enum InsideState
		{
			Inside,
			Outside,
			Unknown
		}

		public static bool PointIsInside(this Polygons polygons,
			IntPoint position,
			QuadTree<(int polyIndex, QuadTree<int> quadTree)> edgeQuadTrees = null,
			INearestNeighbours<(int polygonIndex, int pointIndex)> nearestNeighbours = null,
			Func<IntPoint, InsideState> fastInsideCheck = null)
		{
			if (polygons.TouchingEdge(position, edgeQuadTrees))
			{
				return true;
			}

			if (nearestNeighbours != null)
			{
				var index = nearestNeighbours.GetNearestNeighbour(position);
				if (index.pointIndex != -1 && position == polygons[index.polygonIndex][index.pointIndex])
				{
					return true;
				}
			}

			int insideCount = 0;
			for (int i = 0; i < polygons.Count; i++)
			{
				var polygon = polygons[i];
				if (fastInsideCheck != null)
				{
					switch (fastInsideCheck(position))
					{
						case InsideState.Inside:
							return true;
						case InsideState.Outside:
							return false;
						case InsideState.Unknown:
							if (polygon.PointIsInside(position) != 0)
							{
								insideCount++;
							}

							break;
					}
				}
				else if (polygon.PointIsInside(position) != 0)
				{
					insideCount++;
				}
			}

			return insideCount % 2 == 1;
		}

		public static IEnumerable<(int polyIndex, int pointIndex, IntPoint position)> SkipSame(this IEnumerable<(int polyIndex, int pointIndex, IntPoint position)> source)
		{
			var lastItem = (-1, -1, new IntPoint(long.MaxValue, long.MaxValue));
			foreach (var item in source)
			{
				if (item.polyIndex != -1)
				{
					if (item.position != lastItem.Item3)
					{
						yield return item;
					}

					lastItem = item;
				}
			}
		}

		public static bool TouchingEdge(this Polygons polygons, IntPoint testPosition,
			QuadTree<(int polyIndex, QuadTree<int> quadTree)> edgeQuadTrees = null)
		{
			if (edgeQuadTrees == null)
			{
				for (int i = 0; i < polygons.Count; i++)
				{
					if (polygons[i].TouchingEdge(testPosition))
					{
						return true;
					}
				}
			}
			else
			{
				edgeQuadTrees.SearchArea(new Quad(testPosition));
				foreach (var item in edgeQuadTrees.QueryResults)
				{
					if (polygons[item.polyIndex].TouchingEdge(testPosition, item.quadTree))
					{
						return true;
					}
				}
			}

			return false;
		}
	}
}