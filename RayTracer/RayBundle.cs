// Copyright 2006 Herre Kuijpers - <herre@xs4all.nl>
//
// This source file(s) may be redistributed, altered and customized
// by any means PROVIDING the authors name and all copyright
// notices remain intact.
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED. USE IT AT YOUR OWN RISK. THE AUTHOR ACCEPTS NO
// LIABILITY FOR ANY DATA DAMAGE/LOSS THAT THIS PRODUCT MAY CAUSE.
//-----------------------------------------------------------------------
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
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

namespace MatterHackers.RayTracer
{
    abstract public class RayBundle
    {
        public Ray[] rayArray;

        public RayBundle(int rayCount)
        {
            rayArray = new Ray[rayCount];
        }

        public abstract bool CheckIfBundleHitsAabb(AxisAlignedBoundingBox aabbToCheck);
    }

    public class FrustumRayBundle : RayBundle
    {
        public Frustum frustumForRays = new Frustum();

        public FrustumRayBundle(int rayCount)
            : base(rayCount)
        {
        }

        public override bool CheckIfBundleHitsAabb(AxisAlignedBoundingBox aabbToCheck)
        {
            if (frustumForRays.GetIntersect(aabbToCheck) == FrustumIntersection.Outside)
            {
                return false;
            }

            return true;
        }

        public void CalculateFrustum(int width, int height, Vector3 origin)
        {
            frustumForRays.plane = new Plane[4];

            Vector3 cornerRay0 = rayArray[0].direction;
            Vector3 cornerRay1 = rayArray[width-1].direction;
            Vector3 cornerRay2 = rayArray[(height - 1) * width].direction;
            Vector3 cornerRay3 = rayArray[(height - 1) * width + (width - 1)].direction;
            {
                Vector3 normal = Vector3.Cross(cornerRay0, cornerRay1).GetNormal();
                frustumForRays.plane[0] = new Plane(normal, Vector3.Dot(normal, origin));
            }
            {
                Vector3 normal = Vector3.Cross(cornerRay1, cornerRay2).GetNormal();
                frustumForRays.plane[1] = new Plane(normal, Vector3.Dot(normal, origin));
            }
            {
                Vector3 normal = Vector3.Cross(cornerRay2, cornerRay3).GetNormal();
                frustumForRays.plane[2] = new Plane(normal, Vector3.Dot(normal, origin));
            }
            {
                Vector3 normal = Vector3.Cross(cornerRay3, cornerRay0).GetNormal();
                frustumForRays.plane[3] = new Plane(normal, Vector3.Dot(normal, origin));
            }
        }
    }
}
