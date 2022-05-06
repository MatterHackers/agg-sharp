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
using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Csg
{
	public class Vector3Int
    {
		public int X;
		public int Y;
		public int Z;

		public Vector3Int(Vector3 position, int scale)
        {
			X = (int)Math.Round(position.X * scale);
			Y = (int)Math.Round(position.Y * scale);
			Z = (int)Math.Round(position.Z * scale);
        }
	}
    public class SimilarPlaneFinder
	{
		private readonly Dictionary<int, Dictionary<int, Dictionary<int, List<Plane>>>> comparer;

		private Vector3Int GetIndex(Vector3 normal)
        {
			return new Vector3Int(normal, (int)(1 / normalErrorValue / 10));
		}

		public SimilarPlaneFinder(IEnumerable<Plane> inputPlanes, double normalErrorValue = .0001)
		{
			this.normalErrorValue = normalErrorValue;
			comparer = new Dictionary<int, Dictionary<int, Dictionary<int, List<Plane>>>>();
			foreach (var plane in inputPlanes)
            {
				var index = GetIndex(plane.Normal);

				if (!comparer.ContainsKey(index.X))
				{
					comparer[index.X] = new Dictionary<int, Dictionary<int, List<Plane>>>();
				}
				if (!comparer[index.X].ContainsKey(index.Y))
				{
					comparer[index.X][index.Y] = new Dictionary<int, List<Plane>>();
				}
				if (!comparer[index.X][index.Y].ContainsKey(index.Z))
				{
					comparer[index.X][index.Y][index.Z] = new List<Plane>();
				}

				comparer[index.X][index.Y][index.Z].Add(plane);
			}
		}

		IEnumerable<int> GetSearch(int position, double component)
        {
			var scaled = 1 / normalErrorValue * component;
			var fraction = scaled - (int)scaled;
			if (fraction < .1)
            {
				yield return position - 1;
				yield return position;
            }
			else if (fraction > .9)
            {
				yield return position;
				yield return position+1;
            }

			yield return position;
		}

		IEnumerable<Plane> FindPlanes(Vector3 normal)
        {
			var index = GetIndex(normal);
			foreach(var x in GetSearch(index.X, normal.X))
            {
				foreach (var y in GetSearch(index.Y, normal.Y))
				{
					foreach (var z in GetSearch(index.Z, normal.Z))
					{
						if (comparer.ContainsKey(x)
							&& comparer[x].ContainsKey(y)
							&& comparer[x][y].ContainsKey(z))
						{
							foreach (var plane in comparer[x][y][z])
							{
								yield return plane;
							}
						}
					}
				}
			}
		}

		HashSet<Plane> firstFoundPlanes = new HashSet<Plane>();
        private double normalErrorValue;

        public Plane? FindPlane(Plane searchPlane,
			double distanceErrorValue = .01)
		{
			var position = GetIndex(searchPlane.Normal);

			var allPlanes = FindPlanes(searchPlane.Normal);

			// first check if we have already found a plane that can match
			foreach (var plane in allPlanes)
			{
				if (firstFoundPlanes.Contains(plane))
				{
					if (plane.Equals(searchPlane, distanceErrorValue, normalErrorValue))
					{
						return plane;
					}
				}
			}

			var doIt = false;
			if (doIt)
			{
				foreach (var plane in firstFoundPlanes)
				{
					if (plane.Equals(searchPlane, distanceErrorValue, normalErrorValue))
					{
						return plane;
					}
				}
            }

			// 
			foreach (var planeAndDelta in allPlanes.OrderBy(pad => pad.delta))
            {
				if (planeAndDelta.plane.Equals(searchPlane, distanceErrorValue, normalErrorValue))
				{
					firstFoundPlanes.Add(planeAndDelta.plane);
					return planeAndDelta.plane;
				}
			}

			return null;
		}
	}
}