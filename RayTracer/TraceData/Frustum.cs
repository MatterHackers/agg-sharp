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

using MatterHackers.VectorMath;
using System;

namespace MatterHackers.RayTracer
{
	public enum FrustumIntersection { Inside, Outside, Intersect };

	public class Frustum
	{
		// Plane normals should point out
		public Plane[] Planes { get; set; } = new Plane[6];

		public Frustum()
		{
			Planes = new Plane[6];
		}

		// The front plan is at 0 (the intersection of the 4 side planes). Plane normals should point out.
		public Frustum(Vector3 leftNormal, Vector3 rightNormal,
			Vector3 bottomNormal, Vector3 topNormal,
			Vector3 backNormal, double distanceToBack)
		{
			Planes = new Plane[5];
			Planes[0] = new Plane(leftNormal.GetNormal(), 0);
			Planes[1] = new Plane(rightNormal.GetNormal(), 0);
			Planes[2] = new Plane(bottomNormal.GetNormal(), 0);
			Planes[3] = new Plane(topNormal.GetNormal(), 0);
			Planes[4] = new Plane(backNormal.GetNormal(), distanceToBack);
		}

		// Plane normals should point out.
		public Frustum(Plane left, Plane right, Plane bottom, Plane top, Plane front, Plane back)
		{
			Planes = new Plane[6];

			Planes[0] = left;
			Planes[1] = right;
			Planes[2] = bottom;
			Planes[3] = top;
			Planes[4] = front;
			Planes[5] = back;
		}

		static public Frustum Transform(Frustum frustum, Matrix4X4 matrix)
		{
			Frustum transformedFrustum = new Frustum();
			transformedFrustum.Planes = new Plane[frustum.Planes.Length];
			for (int i = 0; i < frustum.Planes.Length; ++i)
			{
				transformedFrustum.Planes[i] = Plane.Transform(frustum.Planes[i], matrix);
			}

			return transformedFrustum;
		}

		public Frustum(Plane[] sixPlanes)
		{
			Planes = new Plane[6];

			if (sixPlanes.Length != 6)
			{
				throw new Exception("Must create with six planes");
			}

			for (int i = 0; i < 6; i++)
			{
				Planes[i] = sixPlanes[i];
			}
		}

		public FrustumIntersection GetIntersect(AxisAlignedBoundingBox boundingBox)
		{
			FrustumIntersection returnValue = FrustumIntersection.Inside;
			Vector3 vmin, vmax;

			for (int i = 0; i < Planes.Length; ++i)
			{
				// X axis
				if (Planes[i].PlaneNormal.x > 0)
				{
					vmin.x = boundingBox.minXYZ.x;
					vmax.x = boundingBox.maxXYZ.x;
				}
				else
				{
					vmin.x = boundingBox.maxXYZ.x;
					vmax.x = boundingBox.minXYZ.x;
				}

				// Y axis
				if (Planes[i].PlaneNormal.y > 0)
				{
					vmin.y = boundingBox.minXYZ.y;
					vmax.y = boundingBox.maxXYZ.y;
				}
				else
				{
					vmin.y = boundingBox.maxXYZ.y;
					vmax.y = boundingBox.minXYZ.y;
				}

				// Z axis
				if (Planes[i].PlaneNormal.z > 0)
				{
					vmin.z = boundingBox.minXYZ.z;
					vmax.z = boundingBox.maxXYZ.z;
				}
				else
				{
					vmin.z = boundingBox.maxXYZ.z;
					vmax.z = boundingBox.minXYZ.z;
				}

				if (Vector3.Dot(Planes[i].PlaneNormal, vmin) - Planes[i].DistanceToPlaneFromOrigin > 0)
				{
					if (Vector3.Dot(Planes[i].PlaneNormal, vmax) - Planes[i].DistanceToPlaneFromOrigin >= 0)
					{
						return FrustumIntersection.Outside;
					}
				}

				if (Vector3.Dot(Planes[i].PlaneNormal, vmax) - Planes[i].DistanceToPlaneFromOrigin >= 0)
				{
					returnValue = FrustumIntersection.Intersect;
				}
			}

			return returnValue;
		}
	}
}