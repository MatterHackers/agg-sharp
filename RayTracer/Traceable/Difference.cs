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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace MatterHackers.RayTracer.Traceable
{
    public class Difference : IRayTraceable
    {
        IRayTraceable primary;
        IRayTraceable subtract;

        public Difference(IRayTraceable primary, IRayTraceable subtract)
        {
            this.primary = primary;
            this.subtract = subtract;
        }

        public IRayTraceable Primary { get { return primary; } }
        public IRayTraceable Subtract { get { return subtract; } }

        public bool GetContained(List<IRayTraceable> results, AxisAlignedBoundingBox subRegion)
        {
            throw new NotImplementedException();
        }

        public IntersectInfo GetClosestIntersection(Ray ray)
        {
            List<IntersectInfo> allPrimary = new List<IntersectInfo>();
            Ray checkFrontAndBacks = new Ray(ray);
            checkFrontAndBacks.intersectionType = IntersectionType.Both;
            foreach (IntersectInfo info in primary.IntersectionIterator(checkFrontAndBacks))
            {
                allPrimary.Add(info);
            }

            if (allPrimary.Count == 0)
            {
                // We did not hit the primary object.  We are done. The subtract object does not mater.
                return null;
            }

            allPrimary.Sort(new CompareIntersectInfoOnDistance());

            // we hit the primary object, did we hit the subtract object before (within error) hitting the primary.
            List<IntersectInfo> allSubtract = new List<IntersectInfo>();
            foreach (IntersectInfo info in subtract.IntersectionIterator(checkFrontAndBacks))
            {
                allSubtract.Add(info);
            }

            if (allSubtract.Count == 0)
            {
                // we did not hit the subtract so return the first primary
                return allPrimary[0];
            }

            allSubtract.Sort(new CompareIntersectInfoOnDistance());

            List<IntersectInfo> result = new List<IntersectInfo>();
            IntersectInfo.Subtract(allPrimary, allSubtract, result);

            if (result.Count > 0)
            {
                return result[0];
            }

            return null;
        }

        public int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom)
        {
            throw new NotImplementedException();
        }

        public void GetClosestIntersections(RayBundle ray, int rayIndexToStartCheckingFrom, IntersectInfo[] intersectionsForBundle)
        {
            throw new NotImplementedException();
        }

        public IEnumerable IntersectionIterator(Ray ray)
        {
            List<IntersectInfo> allPrimary = new List<IntersectInfo>();
            Ray checkFrontAndBacks = new Ray(ray);
            checkFrontAndBacks.intersectionType = IntersectionType.Both;
            foreach (IntersectInfo info in primary.IntersectionIterator(checkFrontAndBacks))
            {
                allPrimary.Add(info);
            }

            if (allPrimary.Count == 0)
            {
                // We did not hit the primary object.  We are done. The subtract object does not mater.
                //yield break;
            }

            allPrimary.Sort(new CompareIntersectInfoOnDistance());

            // we hit the primary object, did we hit the subtract object before (within error) hitting the primary.
            List<IntersectInfo> allSubtract = new List<IntersectInfo>();
            foreach (IntersectInfo info in subtract.IntersectionIterator(checkFrontAndBacks))
            {
                allSubtract.Add(info);
            }

            if (allSubtract.Count == 0)
            {
                // we did not hit the subtract so return the primary
                foreach (IntersectInfo primaryInfo in allPrimary)
                {
                    yield return primaryInfo;
                }

                yield break;
            }

            allSubtract.Sort(new CompareIntersectInfoOnDistance());

            List<IntersectInfo> results = new List<IntersectInfo>();
            IntersectInfo.Subtract(allPrimary, allSubtract, results);

            foreach (IntersectInfo resultInfo in results)
            {
                yield return resultInfo;
            }
        }

        public RGBA_Floats GetColor(IntersectInfo info)
        {
            throw new NotImplementedException("You should not get a color directly from a Difference.");
        }

        public IMaterial Material
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        private IntersectInfo FindNextIntersections(IRayTraceable element, Ray ray, IntersectInfo info, IntersectionType intersectionType)
        {
            // get all the intersection for the object
            Ray currentRayCheckBackfaces = new Ray(ray);
            currentRayCheckBackfaces.intersectionType = intersectionType;
            currentRayCheckBackfaces.minDistanceToConsider = ((info.hitPosition + ray.direction * Ray.sameSurfaceOffset) - ray.origin).Length;
            currentRayCheckBackfaces.maxDistanceToConsider = double.PositiveInfinity;

            return element.GetClosestIntersection(currentRayCheckBackfaces);
        }

        public double GetSurfaceArea()
        {
            return primary.GetSurfaceArea();
        }

        public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
        {
            return primary.GetAxisAlignedBoundingBox();
        }

        public double GetIntersectCost()
        {
            return primary.GetIntersectCost() + subtract.GetIntersectCost() / 2;
        }
    }
}
