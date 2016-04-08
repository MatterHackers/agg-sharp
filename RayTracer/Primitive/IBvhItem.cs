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
	public class CompareCentersOnAxis : IComparer<IBvhItem>
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

		public int Compare(IBvhItem a, IBvhItem b)
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

	public interface IBvhItem
	{
		/// <summary>
		/// The actual surface area of the surface that this bvh item is defining (a sphere, or a box, or a triangle, etc...)
		/// </summary>
		/// <returns></returns>
		double GetSurfaceArea();

		/// <summary>
		/// Return the bounds of all of the elements of this bvh item
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
		/// If this bvh item is a collection of other bvh items this will return the elements that are
		/// in the sub-region. If it is the actual element it will return itself (like a sphere or a box).
		/// </summary>
		/// <param name="results"></param>
		/// <param name="subRegion"></param>
		/// <returns></returns>
		bool GetContained(List<IBvhItem> results, AxisAlignedBoundingBox subRegion);

		/// <summary>
		/// Check if the give contains the item to check for as part of its collection or proxy
		/// </summary>
		/// <param name="itemToCheckFor"></param>
		/// <returns></returns>
		bool Contains(IBvhItem itemToCheckFor);
	}
}