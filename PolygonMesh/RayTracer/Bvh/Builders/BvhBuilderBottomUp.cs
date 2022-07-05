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
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.RayTracer
{
    public static class BvhBuilderBottomUp
    {
		public class SortingAccelerator
		{
			private int nextAxisForBigGroups = 2;

			public SortingAccelerator()
			{
			}

			public int NextAxis
			{
				get
				{
					nextAxisForBigGroups = (nextAxisForBigGroups + 1) % 3;
					return nextAxisForBigGroups;
				}
			}
		}

		public static ITraceable Create(List<ITraceable> traceableItems, int maxRecursion = int.MaxValue, int recursionDepth = 0, SortingAccelerator accelerator = null)
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
			var axisSorter = new CompareCentersOnAxis(0);

			if (recursionDepth < maxRecursion)
			{
				if (numItems > 50)
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
						var item = traceableItems[i];
						totalIntersectCost += item.GetIntersectCost();
					}

					// get the bounding box of all the items we are going to consider.
					AxisAlignedBoundingBox overallBox = traceableItems[0].GetAxisAlignedBoundingBox();
					for (int i = skipInterval; i < numItems; i += skipInterval)
					{
						overallBox += traceableItems[i].GetAxisAlignedBoundingBox();
					}

					double areaOfTotalBounds = overallBox.GetSurfaceArea();

					double bestCost = totalIntersectCost;

					var totalDeviationOnAxis = default(Vector3);
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
							double thisCost;
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
				var leftItems = new List<ITraceable>(bestIndexToSplitOn + 1);
				var rightItems = new List<ITraceable>(numItems - bestIndexToSplitOn + 1);
				if (numItems > 100)
				{
					// there are lots of items, lets find a sampled bounds and then choose a center
					var totalBounds = AxisAlignedBoundingBox.Empty();
					for (int i = 0; i < 50; i++)
					{
						totalBounds.ExpandToInclude(traceableItems[i * numItems / 50].GetCenter());
					}

					bestAxis = totalBounds.XSize > totalBounds.YSize ? 0 : 1;
					bestAxis = totalBounds.Size[bestAxis] > totalBounds.ZSize ? bestAxis : 2;
					var axisCenter = totalBounds.Center[bestAxis];
					for (int i = 0; i < numItems; i++)
					{
						if (traceableItems[i].GetAxisCenter(bestAxis) <= axisCenter)
						{
							leftItems.Add(traceableItems[i]);
						}
						else
						{
							rightItems.Add(traceableItems[i]);
						}
					}
				}
				else // sort them and find the center
				{
					axisSorter.WhichAxis = bestAxis;
					traceableItems.Sort(axisSorter);
					for (int i = 0; i <= bestIndexToSplitOn; i++)
					{
						leftItems.Add(traceableItems[i]);
					}

					for (int i = bestIndexToSplitOn + 1; i < numItems; i++)
					{
						rightItems.Add(traceableItems[i]);
					}
				}

				var leftGroup = Create(leftItems, maxRecursion, recursionDepth + 1, accelerator);
				var rightGroup = Create(rightItems, maxRecursion, recursionDepth + 1, accelerator);
				var newBVHNode = new BoundingVolumeHierarchy(leftGroup, rightGroup);
				return newBVHNode;
			}
		}
	}
}