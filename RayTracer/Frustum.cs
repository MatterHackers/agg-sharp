/*
Copyright (c) 2013, Lars Brubaker
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
    public class Frustum
    {
        public enum FrustumIntersectState { Inside, Outside, Intersect };

        public Plane[] planes = new Plane[6];

        public FrustumIntersectState IntersectFrustum(AxisAlignedBoundingBox boundingBox)
        {
            Frustum frustum = this;
            FrustumIntersectState returnValue = FrustumIntersectState.Inside;
            Vector3 vmin, vmax;

            for (int i = 0; i < 6; ++i)
            {
                // X axis 
                if (frustum.planes[i].planeNormal.x > 0)
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
                if (frustum.planes[i].planeNormal.y > 0)
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
                if (frustum.planes[i].planeNormal.z > 0)
                {
                    vmin.z = boundingBox.minXYZ.z;
                    vmax.z = boundingBox.maxXYZ.z;
                }
                else
                {
                    vmin.z = boundingBox.maxXYZ.z;
                    vmax.z = boundingBox.minXYZ.z;
                }

                if (Vector3.Dot(frustum.planes[i].planeNormal, vmin) + frustum.planes[i].distanceToPlaneFromOrigin > 0)
                {
                    return FrustumIntersectState.Outside;
                }

                if (Vector3.Dot(frustum.planes[i].planeNormal, vmax) + frustum.planes[i].distanceToPlaneFromOrigin >= 0)
                {
                    return FrustumIntersectState.Intersect;
                }
            }

            return returnValue;
        }
    }
}
