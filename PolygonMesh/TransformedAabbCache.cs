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

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
	public class TransformedAabbCache
	{
		private object locker = new object();
		private Dictionary<Matrix4X4, AxisAlignedBoundingBox> cache = new Dictionary<Matrix4X4, AxisAlignedBoundingBox>();

		public void Changed()
		{
			lock (locker)
			{
				// set the aabbTransform to a bad value so we detect in needs to be recreated
				cache.Clear();
			}
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Mesh mesh, Matrix4X4 transform)
		{
			lock (locker)
			{
				var cacheCount = cache.Count();
				if (cacheCount > 100)
				{
					cache.Clear();
				}
				// if we already have the transform with exact bounds than return it
				AxisAlignedBoundingBox aabb;
				if (cache.TryGetValue(transform, out aabb))
				{
					// return, the fast cache for this transform is correct
					return aabb;
				}

				var positions = mesh.Vertices;

				var convexHull = mesh.GetConvexHull(true);
				if (convexHull != null)
				{
					positions = convexHull.Vertices;
				}

				CalculateBounds(positions, transform);

				return cache[transform];
			}
		}

		private void CalculateBounds(IEnumerable<Vector3Float> vertices, Matrix4X4 transform)
		{
			// calculate the aabb for the current transform
			Vector3 minXYZ = new Vector3(double.MaxValue, double.MaxValue, double.MaxValue);
			Vector3 maxXYZ = new Vector3(double.MinValue, double.MinValue, double.MinValue);

			foreach (var positionIn in vertices)
			{
				Vector3 position = new Vector3(positionIn).Transform(transform);

				minXYZ.X = Math.Min(minXYZ.X, position.X);
				minXYZ.Y = Math.Min(minXYZ.Y, position.Y);
				minXYZ.Z = Math.Min(minXYZ.Z, position.Z);

				maxXYZ.X = Math.Max(maxXYZ.X, position.X);
				maxXYZ.Y = Math.Max(maxXYZ.Y, position.Y);
				maxXYZ.Z = Math.Max(maxXYZ.Z, position.Z);
			}

			cache.Add(transform, new AxisAlignedBoundingBox(minXYZ, maxXYZ));
		}
	}
}