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

using MatterHackers.VectorMath;
using System;

namespace MatterHackers.VectorMath
{
	public enum FrustumIntersection { Inside, Outside, Intersect };

	public class Frustum
	{
		// Plane normals should point out
		public Plane[] Planes { get; set; } = new Plane[6];

		public Plane Left 
		{
			get { return Planes[0]; } 
			set { Planes[0] = value; }
		}

		public Frustum()
		{
			Planes = new Plane[6];
		}

		public static Frustum FrustumFromProjectionMatrix(Matrix4X4 projectionMatrix)
		{
			Frustum createdFrustum = new Frustum();
			// left
			for (int i = 4; i-- > 0;) createdFrustum.Planes[0][i] = projectionMatrix[i, 3] + projectionMatrix[i, 0];
			// right
			for (int i = 4; i-- > 0;) createdFrustum.Planes[1][i] = projectionMatrix[i,3] - projectionMatrix[i,0];
			// bottom
			for (int i = 4; i-- > 0;) createdFrustum.Planes[2][i] = projectionMatrix[i, 3] + projectionMatrix[i, 1];
			// top
			for (int i = 4; i-- > 0;) createdFrustum.Planes[3][i] = projectionMatrix[i, 3] - projectionMatrix[i, 1];
			// front
			for (int i = 4; i-- > 0;) createdFrustum.Planes[4][i] = projectionMatrix[i, 3] + projectionMatrix[i, 2];
			// back
			for (int i = 4; i-- > 0;) createdFrustum.Planes[5][i] = projectionMatrix[i, 3] - projectionMatrix[i, 2];

			for(int i=0; i<6; i++)
			{
				createdFrustum.Planes[i].Normalize();
				createdFrustum.Planes[i].DistanceFromOrigin = -createdFrustum.Planes[i].DistanceFromOrigin;
			}

			return createdFrustum;
		}

		/// <summary>  
		/// Modify the start and end points so they fall within the view frustum.
		/// </summary>
		/// <param name="startPoint"></param>
		/// <param name="endPoint"></param>
		/// <returns>Returns true if any part of the line is in the frustum else false.</returns>
		public bool ClipLine(ref Vector3 startPoint, ref Vector3 endPoint)
		{
			foreach(Plane plane in Planes)
			{
				if(!plane.ClipLine(ref startPoint, ref endPoint))
				{
					// It is entirely behind the plane so
					return false;
				}
			}

			// It has been clipped to all planes and there is still some left.
			return true;
		}

		// The front plan is at 0 (the intersection of the 4 side planes). Plane normals should point out.
		public Frustum(Vector3 leftNormal, Vector3 rightNormal,
			Vector3 bottomNormal, Vector3 topNormal,
			Vector3 backNormal, double distanceToBack)
		{
			Planes = new Plane[6];
			Planes[0] = new Plane(leftNormal.GetNormal(), 0);
			Planes[1] = new Plane(rightNormal.GetNormal(), 0);
			Planes[2] = new Plane(bottomNormal.GetNormal(), 0);
			Planes[3] = new Plane(topNormal.GetNormal(), 0);
			Planes[4] = new Plane(backNormal.GetNormal(), distanceToBack);
            Planes[5] = new Plane(-backNormal.GetNormal(), 0);
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
            Vector3[] corners = boundingBox.GetCorners(); // Assuming this method exists and correctly gets all 8 corners
            bool allInside = true;

            for (int i = 0; i < Planes.Length; ++i)
            {
				var plane = Planes[i];
                bool anyInside = false;
                for (int c = 0; c < corners.Length; ++c)
                {
					var corner = corners[c];
					var distFromPlane = plane.GetDistanceFromPlane(corner);

                    if (distFromPlane < 0)
                    {
                        anyInside = true; // At least one corner is inside or on this plane
                    }
                    else
                    {
                        allInside = false; // If any corner is outside this plane, it cannot be completely inside the frustum
                    }
                }

                if (!anyInside)
                {
                    return FrustumIntersection.Outside; // If no corners are inside this plane, the box is outside
                }
            }

            return allInside ? FrustumIntersection.Inside : FrustumIntersection.Intersect;
        }
    }
}