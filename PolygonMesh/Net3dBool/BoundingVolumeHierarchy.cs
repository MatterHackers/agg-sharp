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

using MatterHackers.VectorMath;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Net3dBool
{
	public class UnboundCollection : IPrimitive
	{
		internal List<IPrimitive> items;

		public UnboundCollection(IList<IPrimitive> traceableItems)
		{
			items = new List<IPrimitive>(traceableItems.Count);
			foreach (IPrimitive traceable in traceableItems)
			{
				items.Add(traceable);
			}
		}

		public IntersectInfo GetClosestIntersection(Ray ray)
		{
			IntersectInfo bestInfo = null;
			foreach (IPrimitive item in items)
			{
				IntersectInfo info = item.GetClosestIntersection(ray);
				if (info != null && info.hitType != IntersectionType.None && info.distanceToHit >= 0)
				{
					if (bestInfo == null || info.distanceToHit < bestInfo.distanceToHit)
					{
						bestInfo = info;
					}
				}
			}

			return bestInfo;
		}

		public bool GetContained(List<IPrimitive> results, AxisAlignedBoundingBox subRegion)
		{
			bool foundItem = false;
			foreach (IPrimitive item in items)
			{
				foundItem |= item.GetContained(results, subRegion);
			}

			return foundItem;
		}

		public IEnumerable IntersectionIterator(Ray ray)
		{
			foreach (IPrimitive item in items)
			{
				foreach (IntersectInfo info in item.IntersectionIterator(ray))
				{
					yield return info;
				}
			}
		}

		public double GetSurfaceArea()
		{
			double totalSurfaceArea = 0;
			foreach (IPrimitive item in items)
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
		/// Attempt to give it in average CPU cycles for the intersecton.
		/// It really does not need to be a member variable as it is fixed to a given
		/// type of object.  But it needs to be virtual so we can get to the value
		/// for a given class. (If only there were class virtual functions :) ).
		/// </summary>
		/// <returns></returns>
		public double GetIntersectCost()
		{
			double totalIntersectCost = 0;
			foreach (IPrimitive item in items)
			{
				totalIntersectCost += item.GetIntersectCost();
			}

			return totalIntersectCost;
		}
	}

	public class BoundingVolumeHierarchy : IPrimitive
	{
		internal AxisAlignedBoundingBox Aabb;
		private IPrimitive nodeA;
		private IPrimitive nodeB;
		private int splitingPlane;

		public BoundingVolumeHierarchy()
		{
		}

		public BoundingVolumeHierarchy(IPrimitive nodeA, IPrimitive nodeB, int splitingPlane)
		{
			this.splitingPlane = splitingPlane;
			this.nodeA = nodeA;
			this.nodeB = nodeB;
			this.Aabb = nodeA.GetAxisAlignedBoundingBox() + nodeB.GetAxisAlignedBoundingBox(); // we can cache this because it is not allowed to change.
		}

		public bool GetContained(List<IPrimitive> results, AxisAlignedBoundingBox subRegion)
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

		public IntersectInfo GetClosestIntersection(Ray ray)
		{
			if (ray.Intersection(Aabb))
			{
				IPrimitive checkFirst = nodeA;
				IPrimitive checkSecond = nodeB;
				if (ray.directionNormal[splitingPlane] < 0)
				{
					checkFirst = nodeB;
					checkSecond = nodeA;
				}

				IntersectInfo infoFirst = checkFirst.GetClosestIntersection(ray);
				if (infoFirst != null && infoFirst.hitType != IntersectionType.None)
				{
					if (ray.isShadowRay)
					{
						return infoFirst;
					}
					else
					{
						ray.maxDistanceToConsider = infoFirst.distanceToHit;
					}
				}
				if (checkSecond != null)
				{
					IntersectInfo infoSecond = checkSecond.GetClosestIntersection(ray);
					if (infoSecond != null && infoSecond.hitType != IntersectionType.None)
					{
						if (ray.isShadowRay)
						{
							return infoSecond;
						}
						else
						{
							ray.maxDistanceToConsider = infoSecond.distanceToHit;
						}
					}
					if (infoFirst != null && infoFirst.hitType != IntersectionType.None && infoFirst.distanceToHit >= 0)
					{
						if (infoSecond != null && infoSecond.hitType != IntersectionType.None && infoSecond.distanceToHit < infoFirst.distanceToHit && infoSecond.distanceToHit >= 0)
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

		public IEnumerable IntersectionIterator(Ray ray)
		{
			if (ray.Intersection(Aabb))
			{
				IPrimitive checkFirst = nodeA;
				IPrimitive checkSecond = nodeB;
				if (ray.directionNormal[splitingPlane] < 0)
				{
					checkFirst = nodeB;
					checkSecond = nodeA;
				}

				foreach (IntersectInfo info in checkFirst.IntersectionIterator(ray))
				{
					if (info != null && info.hitType != IntersectionType.None)
					{
						yield return info;
					}
				}

				if (checkSecond != null)
				{
					foreach (IntersectInfo info in checkSecond.IntersectionIterator(ray))
					{
						if (info != null && info.hitType != IntersectionType.None)
						{
							yield return info;
						}
					}
				}
			}
		}

		public double GetSurfaceArea()
		{
			return Aabb.GetSurfaceArea();
		}

		public Vector3 GetCenter()
		{
			return GetAxisAlignedBoundingBox().GetCenter();
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			return Aabb;
		}

		public class SortingAccelerator
		{
			public int nextAxisForBigGroups = 2;

			public int NextAxis
			{
				get
				{
					nextAxisForBigGroups = (nextAxisForBigGroups + 1) % 3;
					return nextAxisForBigGroups;
				}
			}

			public SortingAccelerator()
			{
			}
		}

		public static IPrimitive CreateNewHierachy(List<IPrimitive> traceableItems, int maxRecursion = int.MaxValue, int recursionDepth = 0, SortingAccelerator accelerator = null)
		{
			if (accelerator == null)
			{
				accelerator = new SortingAccelerator();
			}

			int numItems = traceableItems.Count;

			if (numItems == 0)
			{
				return null;
			}

			if (numItems == 1)
			{
				return traceableItems[0];
			}

			int bestAxis = -1;
			int bestIndexToSplitOn = -1;
			CompareCentersOnAxis axisSorter = new CompareCentersOnAxis(0);

			if (recursionDepth < maxRecursion)
			{
				if (numItems > 5000)
				{
					bestAxis = accelerator.NextAxis;
					bestIndexToSplitOn = numItems / 2;
				}
				else
				{
					double totalIntersectCost = 0;
					int skipInterval = 1;
					for (int i = 0; i < numItems; i += skipInterval)
					{
						IPrimitive item = traceableItems[i];
						totalIntersectCost += item.GetIntersectCost();
					}

					// get the bounding box of all the items we are going to consider.
					AxisAlignedBoundingBox OverallBox = traceableItems[0].GetAxisAlignedBoundingBox();
					for (int i = skipInterval; i < numItems; i += skipInterval)
					{
						OverallBox += traceableItems[i].GetAxisAlignedBoundingBox();
					}
					double areaOfTotalBounds = OverallBox.GetSurfaceArea();

					double bestCost = totalIntersectCost;

					Vector3 totalDeviationOnAxis = new Vector3();
					double[] surfaceArreaOfItem = new double[numItems - 1];
					double[] rightBoundsAtItem = new double[numItems - 1];

					for (int axis = 0; axis < 3; axis++)
					{
						double intersectCostOnLeft = 0;

						axisSorter.WhichAxis = axis;
						traceableItems.Sort(axisSorter);

						// Get all left bounds
						AxisAlignedBoundingBox currentLeftBounds = traceableItems[0].GetAxisAlignedBoundingBox();
						surfaceArreaOfItem[0] = currentLeftBounds.GetSurfaceArea();
						for (int itemIndex = 1; itemIndex < numItems - 1; itemIndex += skipInterval)
						{
							currentLeftBounds += traceableItems[itemIndex].GetAxisAlignedBoundingBox();
							surfaceArreaOfItem[itemIndex] = currentLeftBounds.GetSurfaceArea();

							totalDeviationOnAxis[axis] += Math.Abs(traceableItems[itemIndex].GetCenter()[axis] - traceableItems[itemIndex - 1].GetCenter()[axis]);
						}

						// Get all right bounds
						if (numItems > 1)
						{
							AxisAlignedBoundingBox currentRightBounds = traceableItems[numItems - 1].GetAxisAlignedBoundingBox();
							rightBoundsAtItem[numItems - 2] = currentRightBounds.GetSurfaceArea();
							for (int itemIndex = numItems - 1; itemIndex > 1; itemIndex -= skipInterval)
							{
								currentRightBounds += traceableItems[itemIndex - 1].GetAxisAlignedBoundingBox();
								rightBoundsAtItem[itemIndex - 2] = currentRightBounds.GetSurfaceArea();
							}
						}

						// Sweep from left
						for (int itemIndex = 0; itemIndex < numItems - 1; itemIndex += skipInterval)
						{
							double thisCost = 0;

							{
								// Evaluate Surface Cost Equation
								double costOfTwoAABB = 2 * AxisAlignedBoundingBox.GetIntersectCost(); // the cost of the two children AABB tests

								// do the left cost
								intersectCostOnLeft += traceableItems[itemIndex].GetIntersectCost();
								double leftCost = (surfaceArreaOfItem[itemIndex] / areaOfTotalBounds) * intersectCostOnLeft;

								// do the right cost
								double intersectCostOnRight = totalIntersectCost - intersectCostOnLeft;
								double rightCost = (rightBoundsAtItem[itemIndex] / areaOfTotalBounds) * intersectCostOnRight;

								thisCost = costOfTwoAABB + leftCost + rightCost;
							}

							if (thisCost < bestCost + .000000001) // if it is less within some tiny error
							{
								if (thisCost > bestCost - .000000001)
								{
									// they are the same within the error
									if (axis > 0 && bestAxis != axis) // we have changed axis since last best and we need to decide if this is better than the last axis best
									{
										if (totalDeviationOnAxis[axis] > totalDeviationOnAxis[axis - 1])
										{
											// this new axis is better and we'll switch to it.  Otherwise don't switch.
											bestCost = thisCost;
											bestIndexToSplitOn = itemIndex;
											bestAxis = axis;
										}
									}
								}
								else // this is just better
								{
									bestCost = thisCost;
									bestIndexToSplitOn = itemIndex;
									bestAxis = axis;
								}
							}
						}
					}
				}
			}

			if (bestAxis == -1)
			{
				// No better partition found
				return new UnboundCollection(traceableItems);
			}
			else
			{
				axisSorter.WhichAxis = bestAxis;
				traceableItems.Sort(axisSorter);
				List<IPrimitive> leftItems = new List<IPrimitive>(bestIndexToSplitOn + 1);
				List<IPrimitive> rightItems = new List<IPrimitive>(numItems - bestIndexToSplitOn + 1);
				for (int i = 0; i <= bestIndexToSplitOn; i++)
				{
					leftItems.Add(traceableItems[i]);
				}
				for (int i = bestIndexToSplitOn + 1; i < numItems; i++)
				{
					rightItems.Add(traceableItems[i]);
				}
				IPrimitive leftGroup = CreateNewHierachy(leftItems, maxRecursion, recursionDepth + 1, accelerator);
				IPrimitive rightGroup = CreateNewHierachy(rightItems, maxRecursion, recursionDepth + 1, accelerator);
				BoundingVolumeHierarchy newBVHNode = new BoundingVolumeHierarchy(leftGroup, rightGroup, bestAxis);
				return newBVHNode;
			}
		}
	}
}