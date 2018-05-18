﻿/*
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
using System.Threading.Tasks;
using MatterHackers.VectorMath;
using MIConvexHull;

namespace MatterHackers.PolygonMesh
{
	public class TransformedAabbCache
	{
		private bool calculatingHull;
		private object locker = new object();
		private Matrix4X4 aabbTransform { get; set; } = Matrix4X4.Identity;
		private AxisAlignedBoundingBox cachedAabb { get; set; }

		public void Changed()
		{
			lock (locker)
			{
				aabbTransform = Matrix4X4.Identity;
				var current = aabbTransform;
				current[0, 0] = double.MinValue;
				aabbTransform = current;
			}
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Mesh mesh, AxisAlignedBoundingBox verticesBounds, Matrix4X4 transform)
		{
			// if we already have the transform with exact bounds than return it
			if (aabbTransform == transform && cachedAabb != null)
			{
				// return, the fast cache for this transform is correct
				return cachedAabb;
			}

			IEnumerable<Vector3> positions = mesh.Vertices.Select((v) => v.Position);

			lock (locker)
			{
				// build the convex hull for faster bounding calculations
				// we have a mesh so don't recurse into children
				object objectData;
				mesh.PropertyBag.TryGetValue("ConvexHullData", out objectData);
				if (objectData is ConvexHull<CHVertex, CHFace> convexHullData)
				{
					positions = convexHullData.Points.Select((p) => new Vector3(p.Position[0], p.Position[1], p.Position[2]));
				}
				else if (mesh.Vertices.Count > 1000 && !calculatingHull)
				{
					calculatingHull = true;
					Task.Run(() =>
					{
						// Get the convex hull for the mesh
						var cHVertexList = new List<CHVertex>();
						foreach (var vertex in mesh.Vertices)
						{
							cHVertexList.Add(new CHVertex(vertex.Position));
						}
						var convexHull = ConvexHull<CHVertex, CHFace>.Create(cHVertexList, .01);
						if (convexHull != null)
						{
							try
							{
								mesh.PropertyBag.Add("ConvexHullData", convexHull);
							}
							catch
							{
							}
						}
						calculatingHull = false;
					});
				}
			}

			CalculateBounds(positions, transform);
			return cachedAabb;
		}

		private void CalculateBounds(IEnumerable<Vector3> vertices, Matrix4X4 transform)
		{
			// calculate the aabb for the current transform
			Vector3 minXYZ = new Vector3(double.MaxValue, double.MaxValue, double.MaxValue);
			Vector3 maxXYZ = new Vector3(double.MinValue, double.MinValue, double.MinValue);

			foreach (var positionIn in vertices)
			{
				Vector3 position = Vector3.Transform(positionIn, transform);

				minXYZ.X = Math.Min(minXYZ.X, position.X);
				minXYZ.Y = Math.Min(minXYZ.Y, position.Y);
				minXYZ.Z = Math.Min(minXYZ.Z, position.Z);

				maxXYZ.X = Math.Max(maxXYZ.X, position.X);
				maxXYZ.Y = Math.Max(maxXYZ.Y, position.Y);
				maxXYZ.Z = Math.Max(maxXYZ.Z, position.Z);
			}

			lock (locker)
			{
				cachedAabb = new AxisAlignedBoundingBox(minXYZ, maxXYZ);
				aabbTransform = transform;
			}
		}
	}

	internal class CHFace : ConvexFace<CHVertex, CHFace>
	{
	}

	internal class CHVertex : MIConvexHull.IVertex
	{
		private double[] position;

		internal CHVertex(Vector3 position)
		{
			this.position = position.ToArray();
		}

		public double[] Position => position;
	}
}