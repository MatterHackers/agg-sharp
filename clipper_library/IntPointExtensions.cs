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
	using Polygons = List<List<IntPoint>>;
	using Polygon = List<IntPoint>;

	public static class IntPointExtensions
	{
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

		/// <summary>
		/// Return 1 if ccw -1 if cw
		/// </summary>
		/// <param name="polygon"></param>
		/// <returns></returns>
		public static int GetWindingDirection(this List<IntPoint> polygon)
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

		public static long Dot(this IntPoint point1, IntPoint point2)
		{
			return point1.X * point2.X + point1.Y * point2.Y;
		}

		public static long Cross(this IntPoint left, IntPoint right)
		{
			return left.X * right.Y - left.Y * right.X;
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
		public static Polygons Translate(this Polygons polys, double x, double y, double scale = 1)
		{
			var output = new Polygons(polys.Count);
			foreach (var poly in polys)
			{
				output.Add(poly.Translate(x, y, scale));
			}

			return output;
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

		public static Polygons Rotate(this Polygons polys, double radians)
		{
			var output = new Polygons(polys.Count);
			foreach (var poly in polys)
			{
				output.Add(poly.Rotate(radians));
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
		public static Polygons Scale(this Polygons polys, double scaleX, double scaleY)
		{
			var output = new Polygons(polys.Count);
			foreach (var poly in polys)
			{
				output.Add(poly.Scale(scaleX, scaleY));
			}

			return output;
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

		public static RectangleDouble GetBounds(this Polygons polys)
		{
			RectangleDouble bounds = RectangleDouble.ZeroIntersection;
			foreach (var poly in polys)
			{
				bounds.ExpandToInclude(poly.GetBounds());
			}

			return bounds;
		}

		public static Polygons Offset(this Polygons polygons, double distance)
		{
			var offseter = new ClipperOffset();
			offseter.AddPaths(polygons, JoinType.jtRound, EndType.etClosedPolygon);

			var solution = new Polygons();
			offseter.Execute(ref solution, distance);

			return solution;
		}

		public static Polygons Offset(this Polygon polygon, double distance)
		{
			var offseter = new ClipperOffset();
			offseter.AddPath(polygon, JoinType.jtRound, EndType.etClosedPolygon);

			var solution = new Polygons();
			offseter.Execute(ref solution, distance);

			return solution;
		}

		private static Polygons CombinePolygons(this Polygons aPolys, Polygons bPolys, ClipType clipType, PolyFillType fillType = PolyFillType.pftEvenOdd)
		{
			var clipper = new Clipper();
			clipper.AddPaths(aPolys, PolyType.ptSubject, true);
			clipper.AddPaths(bPolys, PolyType.ptClip, true);

			var outputPolys = new Polygons();
			clipper.Execute(clipType, outputPolys, fillType);
			return outputPolys;
		}

		public static Polygons Union(this Polygons polygons, Polygons other, PolyFillType fillType = PolyFillType.pftEvenOdd)
		{
			return polygons.CombinePolygons(other, ClipType.ctUnion, fillType);
		}

		public static Polygons Union(this Polygons polygons, Polygon other)
		{
			return polygons.CombinePolygons(new Polygons() { other }, ClipType.ctUnion);
		}

		public static Polygons Subtract(this Polygons polygons, Polygons other)
		{
			return polygons.CombinePolygons(other, ClipType.ctDifference);
		}

		public static Polygons Subtract(this Polygons polygons, Polygon other)
		{
			return polygons.CombinePolygons(new Polygons() { other }, ClipType.ctDifference);
		}

		public static Polygons Subtract(this Polygon polygon, Polygons other)
		{
			return new Polygons() { polygon }.CombinePolygons(other, ClipType.ctDifference);
		}

		public static Polygons Subtract(this Polygon polygon, Polygon other)
		{
			return new Polygons() { polygon }.CombinePolygons(new Polygons() { other }, ClipType.ctDifference);
		}
	}
}