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
    public class UnboundCollection : ITraceable
	{
		private AxisAlignedBoundingBox cachedAABB = new AxisAlignedBoundingBox(Vector3.NegativeInfinity, Vector3.NegativeInfinity);

		public UnboundCollection(IList<ITraceable> traceableItems)
		{
			Items = new List<ITraceable>(traceableItems.Count);
			foreach (var traceable in traceableItems)
			{
				Items.Add(traceable);
			}
		}

		public List<ITraceable> Items { get; }

		public MaterialAbstract Material
		{
			get
			{
				throw new Exception("You should not get a material from an UnboundCollection.");
			}

			set
			{
				throw new Exception("You can't set a material on an UnboundCollection.");
			}
		}

        public IEnumerable<IBvhItem> Children => Items;

        public Matrix4X4 AxisToWorld => Matrix4X4.Identity;

        public bool Contains(Vector3 position)
		{
			if (this.GetAxisAlignedBoundingBox().Contains(position))
			{
				foreach (var item in Items)
				{
					if (item.Contains(position))
					{
						return true;
					}
				}
			}

			return false;
		}

		public int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom)
		{
			throw new NotImplementedException();
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			if (cachedAABB.MinXYZ.X == double.NegativeInfinity
				&& Items.Count > 0)
			{
				cachedAABB = Items[0].GetAxisAlignedBoundingBox();
				for (int i = 1; i < Items.Count; i++)
				{
					cachedAABB += Items[i].GetAxisAlignedBoundingBox();
				}
			}

			return cachedAABB;
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
			IntersectInfo bestInfo = null;
			foreach (var item in Items)
			{
				IntersectInfo info = item.GetClosestIntersection(ray);
				if (info != null && info.HitType != IntersectionType.None && info.DistanceToHit >= 0)
				{
					if (bestInfo == null || info.DistanceToHit < bestInfo.DistanceToHit)
					{
						bestInfo = info;
					}
				}
			}

			return bestInfo;
		}

		public void GetClosestIntersections(RayBundle rayBundle, int rayIndexToStartCheckingFrom, IntersectInfo[] intersectionsForBundle)
		{
			var intersection = GetClosestIntersection(rayBundle.rayArray[rayIndexToStartCheckingFrom]);
			if (intersection == null)
			{
				intersectionsForBundle[rayIndexToStartCheckingFrom].HitType = IntersectionType.None;
			}
		}

		public ColorF GetColor(IntersectInfo info)
		{
			throw new NotImplementedException("You should not get a color directly from a BoundingVolumeHierarchy.");
		}

		public bool GetContained(List<IBvhItem> results, AxisAlignedBoundingBox subRegion)
		{
			bool foundItem = false;
			foreach (var item in Items)
			{
				foundItem |= item.GetContained(results, subRegion);
			}

			return foundItem;
		}

        public IEnumerable<IBvhItem> GetCrossing(Plane plane)
        {
			var bounds = this.GetAxisAlignedBoundingBox();
			if (plane.CrossedBy(bounds))
			{
				foreach (var item in Items)
				{
					bounds = item.GetAxisAlignedBoundingBox();
					if (plane.CrossedBy(bounds))
					{
						yield return item;
					}
				}
			}
		}

		/// <summary>
		/// This is the computation cost of doing an intersection with the given type.
		/// Attempt to give it in average CPU cycles for the intersection.
		/// It really does not need to be a member variable as it is fixed to a given
		/// type of object.  But it needs to be virtual so we can get to the value
		/// for a given class. (If only there were class virtual functions :) ).
		/// </summary>
		/// <returns>The relative cost of an intersection test</returns>
		public double GetIntersectCost()
		{
			double totalIntersectCost = 0;
			foreach (var item in Items)
			{
				totalIntersectCost += item.GetIntersectCost();
			}

			return totalIntersectCost;
		}

		public double GetSurfaceArea()
		{
			double totalSurfaceArea = 0;
			foreach (var item in Items)
			{
				totalSurfaceArea += item.GetSurfaceArea();
			}

			return totalSurfaceArea;
		}

        public IEnumerable<IBvhItem> GetTouching(Vector3 position, double error)
        {
			var bounds = this.GetAxisAlignedBoundingBox();
			if (bounds.Contains(position, error))
			{
				foreach (var item in Items)
				{
					bounds = item.GetAxisAlignedBoundingBox();
					if (bounds.Contains(position, error))
					{
						yield return item;
					}
				}
			}
		}

		public IEnumerable IntersectionIterator(Ray ray)
		{
			foreach (var item in Items)
			{
				foreach (IntersectInfo info in item.IntersectionIterator(ray))
				{
					yield return info;
				}
			}
		}
	}
}