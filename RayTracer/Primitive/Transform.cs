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

using MatterHackers.Agg;
using MatterHackers.VectorMath;
using System;
using System.Collections;
using System.Collections.Generic;

namespace MatterHackers.RayTracer.Traceable
{
	public class Transform : Axis3D, IPrimitive
	{
		public IPrimitive Child { get; }

		public Transform(IPrimitive root)
		{
			this.Child = root;
		}

		public Transform(IPrimitive root, Matrix4X4 transform)
		{
			this.Child = root;

			AxisToWorld = transform;
			WorldToAxis = Matrix4X4.Invert(AxisToWorld);
		}

		public bool Contains(IBvhItem itemToCheckFor)
		{
			if (this == itemToCheckFor || Child.Contains(itemToCheckFor))
			{
				return true;
			}

			return false;
		}

		public RGBA_Floats GetColor(IntersectInfo info)
		{
			return Child.GetColor(info);
		}

		public MaterialAbstract Material
		{
			get
			{
				return Child.Material;
			}
			set
			{
				Child.Material = value;
			}
		}

		public bool GetContained(List<IBvhItem> results, AxisAlignedBoundingBox subRegion)
		{
			Child.GetContained(results, subRegion);

			return true;
		}

		public IntersectInfo GetClosestIntersection(Ray ray)
		{
			if (Child != null)
			{
				Ray localRay = GetLocalSpaceRay(ray);
				IntersectInfo localIntersection = Child.GetClosestIntersection(localRay);
				IntersectInfo globalIntersection = GetGlobalSpaceInfo(localIntersection);
				return globalIntersection;
			}

			return null;
		}

		public int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom)
		{
			throw new NotImplementedException();
		}

		public void GetClosestIntersections(RayBundle rayBundle, int rayIndexToStartCheckingFrom, IntersectInfo[] intersectionsForBundle)
		{
			for (int i = 0; i < rayBundle.rayArray.Length; i++)
			{
				rayBundle.rayArray[i] = GetLocalSpaceRay(rayBundle.rayArray[i]);
			}
			Child.GetClosestIntersections(rayBundle, rayIndexToStartCheckingFrom, intersectionsForBundle);
			for (int i = 0; i < rayBundle.rayArray.Length; i++)
			{
				intersectionsForBundle[i] = GetGlobalSpaceInfo(intersectionsForBundle[i]);
			}
		}

		public IEnumerable IntersectionIterator(Ray ray)
		{
			Ray localRay = GetLocalSpaceRay(ray);
			foreach (IntersectInfo localInfo in Child.IntersectionIterator(localRay))
			{
				IntersectInfo globalIntersection = GetGlobalSpaceInfo(localInfo);
				yield return globalIntersection;
			}
		}

		public double GetSurfaceArea()
		{
			return Child.GetSurfaceArea();
		}

		public Vector3 GetCenter()
		{
			return GetAxisAlignedBoundingBox().GetCenter();
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			Vector3 localOrigin = Origin;
			AxisAlignedBoundingBox localBounds = Child.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox bounds = localBounds.NewTransformed(AxisToWorld);
			return bounds;
		}

		public double GetIntersectCost()
		{
			return Child.GetIntersectCost();
		}

		private Ray GetLocalSpaceRay(Ray ray)
		{
			// TODO: cache this.
			Matrix4X4 WorldToAxis = Matrix4X4.Invert(AxisToWorld);
			Vector3 transformedOrigin = Vector3.TransformPosition(ray.origin, WorldToAxis);
			Vector3 transformedDirecton = Vector3.TransformVector(ray.directionNormal, WorldToAxis);
			return new Ray(transformedOrigin, transformedDirecton, ray.minDistanceToConsider, ray.maxDistanceToConsider, ray.intersectionType);
		}

		private IntersectInfo GetGlobalSpaceInfo(IntersectInfo localInfo)
		{
			if (localInfo == null)
			{
				return null;
			}
			IntersectInfo globalInfo = new IntersectInfo(localInfo);
			globalInfo.hitPosition = Vector3.TransformPosition(localInfo.hitPosition, this.AxisToWorld);
			globalInfo.normalAtHit = Vector3.TransformVector(localInfo.normalAtHit, this.AxisToWorld);
			return globalInfo;
		}
	}
}