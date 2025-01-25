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
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Csg
{
    public class SimilarPlaneFinder
	{
		public class VoxelIndex
		{
			public int X;
			public int Y;
			public int Z;

			public int SideX;
			public int SideY;
			public int SideZ;

			public VoxelIndex(Vector3 position, double normalErrorValue)
			{
				var voxelScale = 1 / normalErrorValue;
				var indexScale = voxelScale / 10;
				X = (int)Math.Round(position.X * indexScale);
				Y = (int)Math.Round(position.Y * indexScale);
				Z = (int)Math.Round(position.Z * indexScale);

				SideX = GetSide(position.X * voxelScale);
				SideY = GetSide(position.Y * voxelScale);
				SideZ = GetSide(position.Z * voxelScale);
			}

			int GetSide(double voxelScale)
			{
				var fraction = voxelScale - (int)voxelScale;
				if (fraction < .1)
				{
					return -1;
				}
				else if (fraction > .9)
				{
					return 1;
				}

				return 0;
			}
		}

		private readonly Dictionary<int, Dictionary<int, Dictionary<int, List<Plane>>>> comparer;

		public SimilarPlaneFinder(IEnumerable<Plane> inputPlanes, double normalErrorValue = .0001)
		{
			this.normalErrorValue = normalErrorValue;
			comparer = new Dictionary<int, Dictionary<int, Dictionary<int, List<Plane>>>>();
			foreach (var plane in inputPlanes)
            {
                AddPlaneToComparer(plane, normalErrorValue);
            }
        }

        public void AddPlaneToComparer(Plane plane, double normalErrorValue = .0001)
        {
            var voxelIndex = new VoxelIndex(plane.Normal, normalErrorValue);

            if (!comparer.ContainsKey(voxelIndex.X))
            {
                comparer[voxelIndex.X] = new Dictionary<int, Dictionary<int, List<Plane>>>();
            }
            if (!comparer[voxelIndex.X].ContainsKey(voxelIndex.Y))
            {
                comparer[voxelIndex.X][voxelIndex.Y] = new Dictionary<int, List<Plane>>();
            }
            if (!comparer[voxelIndex.X][voxelIndex.Y].ContainsKey(voxelIndex.Z))
            {
                comparer[voxelIndex.X][voxelIndex.Y][voxelIndex.Z] = new List<Plane>();
            }

            comparer[voxelIndex.X][voxelIndex.Y][voxelIndex.Z].Add(plane);
        }

        private IEnumerable<int> GetSearch(VoxelIndex voxelIndex, int axis)
		{
			if (axis == 0)
			{
				if (voxelIndex.SideX == -1)
				{
					yield return voxelIndex.X - 1;
					yield return voxelIndex.X;
				}
				else if (voxelIndex.SideX == 1)
				{
					yield return voxelIndex.X;
					yield return voxelIndex.X + 1;
				}
				else
				{
					yield return voxelIndex.X;
				}
			}
			else if (axis == 1)
			{
				if (voxelIndex.SideY == -1)
				{
					yield return voxelIndex.Y - 1;
					yield return voxelIndex.Y;
				}
				else if (voxelIndex.SideY == 1)
				{
					yield return voxelIndex.Y;
					yield return voxelIndex.Y + 1;
				}
				else
				{
					yield return voxelIndex.Y;
				}
			}
			else
			{
				if (voxelIndex.SideZ == -1)
				{
					yield return voxelIndex.Z - 1;
					yield return voxelIndex.Z;
				}
				else if (voxelIndex.SideZ == 1)
				{
					yield return voxelIndex.Z;
					yield return voxelIndex.Z + 1;
				}
				else
				{
					yield return voxelIndex.Z;
				}
			}
		}

		IEnumerable<Plane> FindPlanes(Vector3 normal)
        {
			var voxelIndex = new VoxelIndex(normal, normalErrorValue);

			foreach(var x in GetSearch(voxelIndex, 0))
            {
				if (comparer.ContainsKey(x))
				{
					foreach (var y in GetSearch(voxelIndex, 1))
					{
						if (comparer[x].ContainsKey(y))
						{
							foreach (var z in GetSearch(voxelIndex, 2))
							{
								if (comparer[x][y].ContainsKey(z))
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
			}
		}

        HashSet<Plane> firstFoundPlanes = new HashSet<Plane>();
        private double normalErrorValue;

        public Plane? FindPlane(Plane searchPlane,
			double distanceErrorValue = .01)
		{
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
			foreach (var plane in allPlanes)
            {
				if (plane.Equals(searchPlane, distanceErrorValue, normalErrorValue))
				{
					firstFoundPlanes.Add(plane);
					return plane;
				}
			}

			return null;
		}
	}
}