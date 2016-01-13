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

namespace MatterHackers.Agg.Collections
{
	public class UnboundCollection : IBoundedItem
	{
		internal List<IBoundedItem> items;

		public UnboundCollection(IList<IBoundedItem> traceableItems)
		{
			items = new List<IBoundedItem>(traceableItems.Count);
			foreach (IBoundedItem traceable in traceableItems)
			{
				items.Add(traceable);
			}
		}

		public bool GetContained(List<IBoundedItem> results, AxisAlignedBoundingBox subRegion)
		{
			bool foundItem = false;
			foreach (IBoundedItem item in items)
			{
				foundItem |= item.GetContained(results, subRegion);
			}

			return foundItem;
		}

		public double GetSurfaceArea()
		{
			double totalSurfaceArea = 0;
			foreach (IBoundedItem item in items)
			{
				totalSurfaceArea += item.GetSurfaceArea();
			}

			return totalSurfaceArea;
		}

		public Vector3 GetCenter()
		{
			return GetAxisAlignedBoundingBox().GetCenter();
		}

		private AxisAlignedBoundingBox cachedAABB = new AxisAlignedBoundingBox(Vector3.NegativeInfinity, Vector3.NegativeInfinity);

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			if (cachedAABB.minXYZ.x == double.NegativeInfinity)
			{
				cachedAABB = items[0].GetAxisAlignedBoundingBox();
				for (int i = 1; i < items.Count; i++)
				{
					cachedAABB += items[i].GetAxisAlignedBoundingBox();
				}
			}

			return cachedAABB;
		}

		/// <summary>
		/// This is the computation cost of doing an intersection with the given type.
		/// Attempt to give it in average CPU cycles for the intersection.
		/// It really does not need to be a member variable as it is fixed to a given
		/// type of object.  But it needs to be virtual so we can get to the value
		/// for a given class. (If only there were class virtual functions :) ).
		/// </summary>
		/// <returns></returns>
		public double GetIntersectCost()
		{
			double totalIntersectCost = 0;
			foreach (IBoundedItem item in items)
			{
				totalIntersectCost += item.GetIntersectCost();
			}

			return totalIntersectCost;
		}
	}
}
