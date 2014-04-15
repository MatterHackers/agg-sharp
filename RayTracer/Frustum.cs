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
using System.Text;

using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace MatterHackers.RayTracer
{
    public enum FrustumIntersection { Inside, Outside, Intersect };

    public class Frustum
    {
        // Plane normals should point out
        public Plane[] plane = null;

        public Frustum()
        {
            plane = new Plane[6];
        }

        // The front plan is at 0 (the intersection of the 4 side planes). Plane normals should point out.
        public Frustum(Vector3 leftNormal, Vector3 rightNormal, 
            Vector3 bottomNormal, Vector3 topNormal, 
            Vector3 backNormal, double distanceToBack)
        {
            plane = new Plane[5];
            plane[0] = new Plane(leftNormal.GetNormal(), 0);
            plane[1] = new Plane(rightNormal.GetNormal(), 0);
            plane[2] = new Plane(bottomNormal.GetNormal(), 0);
            plane[3] = new Plane(topNormal.GetNormal(), 0);
            plane[4] = new Plane(backNormal.GetNormal(), distanceToBack);
        }

        // Plane normals should point out.
        public Frustum(Plane left, Plane right, Plane bottom, Plane top, Plane front, Plane back)
        {
            plane = new Plane[6];

            plane[0] = left;
            plane[1] = right;
            plane[2] = bottom;
            plane[3] = top;
            plane[4] = front;
            plane[5] = back;
        }

        static public Frustum Transform(Frustum frustum, Matrix4X4 matrix)
        {
            Frustum transformedFrustum = new Frustum();
            transformedFrustum.plane = new Plane[frustum.plane.Length];
            for (int i = 0; i < frustum.plane.Length; ++i)
            {
                Vector3 planeNormal = frustum.plane[i].planeNormal;
                double distanceToPlane = frustum.plane[i].distanceToPlaneFromOrigin;
                transformedFrustum.plane[i].planeNormal = Vector3.TransformNormal(planeNormal, matrix);
                Vector3 pointOnPlane = planeNormal * distanceToPlane;
                Vector3 pointOnTransformedPlane = Vector3.TransformNormal(pointOnPlane, matrix);
                transformedFrustum.plane[i].distanceToPlaneFromOrigin = Vector3.Dot(transformedFrustum.plane[i].planeNormal, pointOnTransformedPlane);
            }

            return transformedFrustum;
        }

        public Frustum(Plane[] sixPlanes)
        {
            plane = new Plane[6];

            if (sixPlanes.Length != 6)
            {
                throw new Exception("Must create with six planes");
            }

            for (int i = 0; i < 6; i++)
            {
                plane[i] = sixPlanes[i];
            }
        }

        public FrustumIntersection GetIntersect(AxisAlignedBoundingBox boundingBox)
        {
            FrustumIntersection returnValue = FrustumIntersection.Inside;
            Vector3 vmin, vmax;

            for (int i = 0; i < plane.Length; ++i)
            {
                // X axis 
                if (plane[i].planeNormal.x > 0)
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
                if (plane[i].planeNormal.y > 0)
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
                if (plane[i].planeNormal.z > 0)
                {
                    vmin.z = boundingBox.minXYZ.z;
                    vmax.z = boundingBox.maxXYZ.z;
                }
                else
                {
                    vmin.z = boundingBox.maxXYZ.z;
                    vmax.z = boundingBox.minXYZ.z;
                }

                if (Vector3.Dot(plane[i].planeNormal, vmin) - plane[i].distanceToPlaneFromOrigin > 0)
                {
                    if (Vector3.Dot(plane[i].planeNormal, vmax) - plane[i].distanceToPlaneFromOrigin >= 0)
                    {
                        return FrustumIntersection.Outside;
                    }
                }
                
                if (Vector3.Dot(plane[i].planeNormal, vmax) - plane[i].distanceToPlaneFromOrigin >= 0)
                {
                    returnValue = FrustumIntersection.Intersect;
                }
            }

            return returnValue;
        }
    }
}
