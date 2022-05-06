/*
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

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Csg
{
    public class SimilarPlaneFinder
	{
		private readonly PlaneNormalComparer xComparer;
		private readonly PlaneNormalComparer yComparer;
		private readonly PlaneNormalComparer zComparer;

		public SimilarPlaneFinder(IEnumerable<Plane> inputPlanes)
		{
			xComparer = new PlaneNormalComparer(inputPlanes, 0);
			yComparer = new PlaneNormalComparer(inputPlanes, 1);
			zComparer = new PlaneNormalComparer(inputPlanes, 2);
		}

		HashSet<Plane> foundPlanes = new HashSet<Plane>();

		public Plane? FindPlane(Plane searchPlane,
			double distanceErrorValue = .01,
			double normalErrorValue = .0001)
		{
			var allAxis = xComparer.FindPlanes(searchPlane, normalErrorValue)
				.Union(yComparer.FindPlanes(searchPlane, normalErrorValue))
				.Union(zComparer.FindPlanes(searchPlane, normalErrorValue));

			foreach (var planeAndDelta in allAxis.OrderBy(pad => pad.delta))
			{
				if (foundPlanes.Contains(planeAndDelta.plane))
				{
					if (planeAndDelta.plane.Equals(searchPlane, distanceErrorValue, normalErrorValue))
					{
						return planeAndDelta.plane;
					}
				}
			}

			var doIt = true;
			if (doIt)
			{
				foreach (var plane in foundPlanes)
				{
					if (plane.Equals(searchPlane, distanceErrorValue, normalErrorValue))
					{
						return plane;
					}
				}
            }

			foreach (var planeAndDelta in allAxis.OrderBy(pad => pad.delta))
            {
				if (planeAndDelta.plane.Equals(searchPlane, distanceErrorValue, normalErrorValue))
				{
					foundPlanes.Add(planeAndDelta.plane);
					return planeAndDelta.plane;
				}
			}

			return null;
		}
	}

	public class PlaneNormalComparer : IComparer<Plane>
	{
        private readonly int axis;
        private readonly List<Plane> planes;

		public PlaneNormalComparer(IEnumerable<Plane> inputPlanes, int axis)
		{
			this.axis = axis;
			planes = new List<Plane>(inputPlanes);
			planes.Sort(this);
		}

		public int Compare(Plane a, Plane b)
		{
			return a.Normal[axis].CompareTo(b.Normal[axis]);
		}

		public IEnumerable<(Plane plane, double delta)> FindPlanes(Plane searchPlane, double normalErrorValue)
		{
			if (Math.Abs(searchPlane[axis]) > .2)
			{
				Plane testPlane = searchPlane;
				int index = planes.BinarySearch(testPlane, this);
				if (index < 0)
				{
					index = ~index;
				}

				// we have the starting index now get all the vertices that are close enough starting from here
				var downOffset = 1;
				for (int i = index; i < planes.Count; i++)
				{
					var foundOne = false;
					var component = planes[i].Normal[axis];
					var normalDelta = Math.Abs(component - searchPlane.Normal[axis]);
					var distanceDelta = planes[i].DistanceFromOrigin - searchPlane.DistanceFromOrigin;
					if (normalDelta <= normalErrorValue)
					{
						foundOne = true;
						yield return (planes[i], normalDelta + distanceDelta);
					}

					var downIndex = index - downOffset++;
					if (downIndex >= 0)
					{
						component = planes[downIndex].Normal[axis];
						normalDelta = Math.Abs(component - searchPlane.Normal[axis]);
						distanceDelta = planes[downIndex].DistanceFromOrigin - searchPlane.DistanceFromOrigin;
						if (normalDelta <= normalErrorValue)
						{
							foundOne = true;
							yield return (planes[i], normalDelta + distanceDelta);
						}
					}

					if (!foundOne)
					{
						break;
					}
				}
			}
		}
	}
}