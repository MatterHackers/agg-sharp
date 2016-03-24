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
	public class CompareCentersOnAxis : IComparer<IPrimitive>
	{
		private int whichAxis;

		public int WhichAxis
		{
			get
			{
				return whichAxis;
			}
			set
			{
				whichAxis = value % 3;
			}
		}

		public CompareCentersOnAxis(int whichAxis)
		{
			this.whichAxis = whichAxis % 3;
		}

		public int Compare(IPrimitive a, IPrimitive b)
		{
			if (a == null || b == null)
			{
				throw new Exception();
			}

			double axisCenterA = a.GetCenter()[whichAxis];
			double axisCenterB = b.GetCenter()[whichAxis];

			if (axisCenterA > axisCenterB)
			{
				return 1;
			}
			else if (axisCenterA < axisCenterB)
			{
				return -1;
			}
			return 0;
		}
	}

	/// <summary>
	/// element in a scene
	/// </summary>
	public interface IPrimitive
	{
		/// <summary>
		/// Get the color for a primitive at the given info.
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		RGBA_Floats GetColor(IntersectInfo info);

		/// <summary>
		/// Specifies the ambient and diffuse color of the element.
		/// </summary>
		MaterialAbstract Material { get; set; }

		/// <summary>
		/// If this primitive is a collection of other primitives this will return the elements that are
		/// in the sub-region. If it is the actual element of it will return itself (like a sphere or a box).
		/// </summary>
		/// <param name="results"></param>
		/// <param name="subRegion"></param>
		/// <returns></returns>
		bool GetContained(List<IPrimitive> results, AxisAlignedBoundingBox subRegion);

		/// <summary>
		/// This method is to be implemented by each element separately. This is the core
		/// function of each element, to determine the intersection with a ray.
		/// </summary>
		/// <param name="ray">the ray that intersects with the element</param>
		/// <returns></returns>
		IntersectInfo GetClosestIntersection(Ray ray);

		int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom);

		void GetClosestIntersections(RayBundle rayBundle, int rayIndexToStartCheckingFrom, IntersectInfo[] intersectionsForBundle);

		/// <summary>
		/// This is used for things like view space csg. All intersections along the ray
		/// are returned in order.
		/// </summary>
		/// <param name="ray"></param>
		/// <returns></returns>
		IEnumerable IntersectionIterator(Ray ray);

		/// <summary>
		/// The actual surface area of the surface that this primitive is defining (a sphere, or a box, or a triangle, etc...)
		/// </summary>
		/// <returns></returns>
		double GetSurfaceArea();

		/// <summary>
		/// Return the bounds of all of the elements of this primitive
		/// </summary>
		/// <returns></returns>
		AxisAlignedBoundingBox GetAxisAlignedBoundingBox();

		/// <summary>
		/// The center of the axis aligned bounds. Represented as a separate function
		/// for possible optimization depending on the underlying data.
		/// </summary>
		/// <returns></returns>
		Vector3 GetCenter();

		/// <summary>
		/// This is the computation cost of doing an intersection with the given type.
		/// This number is the number of milliseconds it takes to do some number of intersections.
		/// It just needs to be the same number for every type as they only need to
		/// be relative to each other.
		/// It really does not need to be a member variable as it is fixed to a given
		/// type of object.  But it needs to be virtual so we can get to the value
		/// for a given class. (If only there were class virtual functions :) ).
		/// </summary>
		/// <returns></returns>
		double GetIntersectCost();
	}
}