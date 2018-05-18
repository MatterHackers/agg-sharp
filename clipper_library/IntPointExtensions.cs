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

using ClipperLib;
using System;
using System.Collections.Generic;

namespace ClipperLib
{
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
	}
}