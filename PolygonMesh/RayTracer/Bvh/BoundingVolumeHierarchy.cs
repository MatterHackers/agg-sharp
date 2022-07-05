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
using MatterHackers.Agg;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.RayTracer
{
	public enum BvhCreationOptions
	{
		LegacyFastConstructionSlowTracing,
		LegacySlowConstructionFastTracing,
        LocFastContructionFastTracing
    }

	public class BoundingVolumeHierarchy : ITraceable
	{
		internal AxisAlignedBoundingBox Aabb;
		private readonly ITraceable nodeA;
		private readonly ITraceable nodeB;
		private int splittingPlane;

		public BoundingVolumeHierarchy()
		{
		}

		public BoundingVolumeHierarchy(ITraceable nodeA, ITraceable nodeB)
        {
			this.nodeA = nodeA;
			this.nodeB = nodeB;
			Aabb = nodeA.GetAxisAlignedBoundingBox() + nodeB.GetAxisAlignedBoundingBox();
			splittingPlane = Aabb.XSize > Aabb.YSize ? 0 : 1;
			splittingPlane = Aabb.Size[splittingPlane] > Aabb.ZSize ? splittingPlane : 2;
		}

		public MaterialAbstract Material
		{
			get
			{
				throw new Exception("You should not get a material from a BoundingVolumeHierarchy.");
			}

			set
			{
				throw new Exception("You can't set a material on a BoundingVolumeHierarchy.");
			}
		}

        public IEnumerable<IBvhItem> Children
        {
			get
			{
				yield return nodeA;
				yield return nodeB;
			}
        }

        public Matrix4X4 AxisToWorld => Matrix4X4.Identity;

		public bool Contains(Vector3 position)
		{
			if (this.GetAxisAlignedBoundingBox().Contains(position))
			{
				if (nodeA.Contains(position)
					|| nodeB.Contains(position))
				{
					return true;
				}
			}

			return false;
		}

		public int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom)
		{
			// check if first ray hits bounding box
			if (rayBundle.rayArray[rayIndexToStartCheckingFrom].Intersection(Aabb))
			{
				return rayIndexToStartCheckingFrom;
			}

			int count = rayBundle.rayArray.Length;
			// check if all bundle misses
			if (!rayBundle.CheckIfBundleHitsAabb(Aabb))
			{
				return -1;
			}

			// check each ray until one hits or all miss
			for (int i = rayIndexToStartCheckingFrom + 1; i < count; i++)
			{
				if (rayBundle.rayArray[i].Intersection(Aabb))
				{
					return i;
				}
			}

			return -1;
		}

        public static ITraceable CreateNewHierachy(List<ITraceable> tracePrimitives, BvhCreationOptions bvhCreationOptions = BvhCreationOptions.LegacySlowConstructionFastTracing)
        {
			ITraceable output = null;
			using (new QuickTimer("LocalOrderClustering", 1))
			{
                //output = BvhBuilderLocallyOrderedClustering.Create(tracePrimitives);
				//return output;
			}

			switch (bvhCreationOptions)
            {
				case BvhCreationOptions.LegacyFastConstructionSlowTracing:
					using (new QuickTimer("LegacyFastConstructionSlowTracing", 1))
					{
						output = BvhBuilderBottomUp.Create(tracePrimitives, 0);
					}
					break;

				case BvhCreationOptions.LegacySlowConstructionFastTracing:
					using (new QuickTimer("LegacySlowConstructionFastTracing", 1))
					{
						output = BvhBuilderBottomUp.Create(tracePrimitives);
					}
					break;

				case BvhCreationOptions.LocFastContructionFastTracing:
					using (new QuickTimer("LocFastContructionFastTracing", 1))
					{
						output = BvhBuilderLocallyOrderedClustering.Create(tracePrimitives);
					}
					break;

				default:
					output = BvhBuilderBottomUp.Create(tracePrimitives);
					break;
			}

			return output;
		}

        public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			return Aabb;
		}

		public double GetAxisCenter(int axis)
		{
			return GetCenter()[axis];
		}

		public Vector3 GetCenter()
		{
			return GetAxisAlignedBoundingBox().GetCenter();
		}

		public IntersectInfo GetClosestIntersection(Ray ray)
		{
			if (ray.Intersection(Aabb))
			{
				var checkFirst = nodeA;
				var checkSecond = nodeB;
				if (ray.directionNormal[splittingPlane] < 0)
				{
					checkFirst = nodeB;
					checkSecond = nodeA;
				}

				IntersectInfo infoFirst = checkFirst.GetClosestIntersection(ray);
				if (infoFirst != null && infoFirst.HitType != IntersectionType.None)
				{
					if (ray.isShadowRay)
					{
						return infoFirst;
					}
					else
					{
						ray.maxDistanceToConsider = infoFirst.DistanceToHit;
					}
				}

				if (checkSecond != null)
				{
					IntersectInfo infoSecond = checkSecond.GetClosestIntersection(ray);
					if (infoSecond != null && infoSecond.HitType != IntersectionType.None)
					{
						if (ray.isShadowRay)
						{
							return infoSecond;
						}
						else
						{
							ray.maxDistanceToConsider = infoSecond.DistanceToHit;
						}
					}

					if (infoFirst != null && infoFirst.HitType != IntersectionType.None && infoFirst.DistanceToHit >= 0)
					{
						if (infoSecond != null && infoSecond.HitType != IntersectionType.None && infoSecond.DistanceToHit < infoFirst.DistanceToHit && infoSecond.DistanceToHit >= 0)
						{
							return infoSecond;
						}
						else
						{
							return infoFirst;
						}
					}

					return infoSecond; // we don't have to test it because it didn't hit.
				}

				return infoFirst;
			}

			return null;
		}

		public void GetClosestIntersections(RayBundle rayBundle, int rayIndexToStartCheckingFrom, IntersectInfo[] intersectionsForBundle)
		{
			int startRayIndex = FindFirstRay(rayBundle, rayIndexToStartCheckingFrom);
			if (startRayIndex != -1)
			{
				var checkFirst = nodeA;
				var checkSecond = nodeB;
				if (rayBundle.rayArray[startRayIndex].directionNormal[splittingPlane] < 0)
				{
					checkFirst = nodeB;
					checkSecond = nodeA;
				}

				checkFirst.GetClosestIntersections(rayBundle, startRayIndex, intersectionsForBundle);
				if (checkSecond != null)
				{
					checkSecond.GetClosestIntersections(rayBundle, startRayIndex, intersectionsForBundle);
				}
			}
		}

		public ColorF GetColor(IntersectInfo info)
		{
			throw new NotImplementedException("You should not get a color directly from a BoundingVolumeHierarchy.");
		}

		public bool GetContained(List<IBvhItem> results, AxisAlignedBoundingBox subRegion)
		{
			AxisAlignedBoundingBox bounds = GetAxisAlignedBoundingBox();
			if (bounds.Contains(subRegion))
			{
				bool resultA = this.nodeA.GetContained(results, subRegion);
				bool resultB = this.nodeB.GetContained(results, subRegion);
				return resultA | resultB;
			}

			return false;
		}

		public double GetIntersectCost()
		{
			return AxisAlignedBoundingBox.GetIntersectCost();
		}

		public double GetSurfaceArea()
		{
			return Aabb.GetSurfaceArea();
		}

		public IEnumerable IntersectionIterator(Ray ray)
		{
			if (ray.Intersection(Aabb))
			{
				var checkFirst = nodeA;
				var checkSecond = nodeB;
				if (ray.directionNormal[splittingPlane] < 0)
				{
					checkFirst = nodeB;
					checkSecond = nodeA;
				}

				foreach (IntersectInfo info in checkFirst.IntersectionIterator(ray))
				{
					if (info != null && info.HitType != IntersectionType.None)
					{
						yield return info;
					}
				}

				if (checkSecond != null)
				{
					foreach (IntersectInfo info in checkSecond.IntersectionIterator(ray))
					{
						if (info != null && info.HitType != IntersectionType.None)
						{
							yield return info;
						}
					}
				}
			}
		}

        public IEnumerable<IBvhItem> GetCrossing(Plane plane)
        {
			AxisAlignedBoundingBox bounds = GetAxisAlignedBoundingBox();
			if (plane.CrossedBy(bounds))
			{
				foreach(var item in this.nodeA.GetCrossing(plane))
                {
					yield return item;
                }
				foreach (var item in this.nodeB.GetCrossing(plane))
				{
					yield return item;
				}
			}
		}

        public IEnumerable<IBvhItem> GetTouching(Vector3 position, double error)
        {
			AxisAlignedBoundingBox bounds = GetAxisAlignedBoundingBox();
			if (bounds.Contains(position, error))
			{
				foreach (var item in this.nodeA.GetTouching(position, error))
				{
					yield return item;
				}
				foreach (var item in this.nodeB.GetTouching(position, error))
				{
					yield return item;
				}
			}
		}
	}
}