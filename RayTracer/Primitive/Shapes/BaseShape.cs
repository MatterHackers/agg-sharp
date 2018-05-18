using MatterHackers.Agg;
using MatterHackers.VectorMath;

// Copyright 2006 Herre Kuijpers - <herre@xs4all.nl>
//
// This source file(s) may be redistributed, altered and customized
// by any means PROVIDING the authors name and all copyright
// notices remain intact.
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED. USE IT AT YOUR OWN RISK. THE AUTHOR ACCEPTS NO
// LIABILITY FOR ANY DATA DAMAGE/LOSS THAT THIS PRODUCT MAY CAUSE.
//-----------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;

namespace MatterHackers.RayTracer
{
	public abstract class BaseShape : IPrimitive
	{
		public ColorF GetColor(IntersectInfo info)
		{
			if (Material.HasTexture)
			{
				var uv = GetUv(info);
				return Material.GetColor(uv.u, uv.v);
			}
			else
			{
				return Material.GetColor(0, 0);
			}
		}

		public abstract (double u, double v) GetUv(IntersectInfo info);

		public MaterialAbstract Material
		{
			get;
			set;
		}

		public bool GetContained(List<IBvhItem> results, AxisAlignedBoundingBox subRegion)
		{
			AxisAlignedBoundingBox bounds = GetAxisAlignedBoundingBox();
			if (bounds.Contains(subRegion))
			{
				results.Add(this);
				return true;
			}

			return false;
		}

		public virtual bool Contains(Vector3 position)
		{
			if (this.GetAxisAlignedBoundingBox().Contains(position))
			{
				return true;
			}

			return false;
		}

		public BaseShape()
		{
			Material = new SolidMaterial(new ColorF(1, 0, 1), 0, 0, 0);
		}

		public abstract IntersectInfo GetClosestIntersection(Ray ray);

		public abstract int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom);

		public virtual void GetClosestIntersections(RayBundle rayBundle, int rayIndexToStartCheckingFrom, IntersectInfo[] intersectionsForBundle)
		{
			throw new NotImplementedException("Implement this for the class you want.");
		}

		public abstract IEnumerable IntersectionIterator(Ray ray);

		public abstract double GetSurfaceArea();

		public virtual Vector3 GetCenter()
		{
			return GetAxisAlignedBoundingBox().GetCenter();
		}

		public abstract AxisAlignedBoundingBox GetAxisAlignedBoundingBox();

		public abstract double GetIntersectCost();
	}
}