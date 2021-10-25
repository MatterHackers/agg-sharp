//The MIT License(MIT)

//Copyright(c) 2015 ChevyRay

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using System;
using System.Collections.Generic;

namespace MatterHackers.VectorMath
{
	public static class TradeOffBvhConstructor<T>
	{
		public static BvhTree<T> CreateNewHierachy(List<BvhTreeItemData<T>> itemsToAdd,
			int maxRecursion = int.MaxValue,
			int recursionDepth = 0,
			SortingAccelerator accelerator = null,
			int DoSimpleSortSize = 5000)
		{
			if (accelerator == null)
			{
				accelerator = new SortingAccelerator();
			}

			int numItems = itemsToAdd.Count;

			if (numItems == 0)
			{
				return null;
			}

			if (numItems == 1)
			{
				return new BvhTree<T>(itemsToAdd);
			}

			int bestAxis = -1;
			int bestIndexToSplitOn = -1;
			var axisSorter = new AxisSorter(0);

			if (recursionDepth < maxRecursion)
			{
				if (numItems > DoSimpleSortSize)
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
						var item = itemsToAdd[i];
						if (item.Item is IIntersectable intersectable)
						{
							totalIntersectCost += intersectable.GetIntersectCost();
						}
						else
						{
							totalIntersectCost += AxisAlignedBoundingBox.GetIntersectCost();
						}
					}

					// get the bounding box of all the items we are going to consider.
					AxisAlignedBoundingBox OverallBox = itemsToAdd[0].Aabb;
					for (int i = skipInterval; i < numItems; i += skipInterval)
					{
						OverallBox += itemsToAdd[i].Aabb;
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
						itemsToAdd.Sort(axisSorter);

						// Get all left bounds
						AxisAlignedBoundingBox currentLeftBounds = itemsToAdd[0].Aabb;
						surfaceArreaOfItem[0] = currentLeftBounds.GetSurfaceArea();
						for (int itemIndex = 1; itemIndex < numItems - 1; itemIndex += skipInterval)
						{
							currentLeftBounds += itemsToAdd[itemIndex].Aabb;
							surfaceArreaOfItem[itemIndex] = currentLeftBounds.GetSurfaceArea();

							totalDeviationOnAxis[axis] += Math.Abs(itemsToAdd[itemIndex].Center[axis] - itemsToAdd[itemIndex - 1].Center[axis]);
						}

						// Get all right bounds
						if (numItems > 1)
						{
							AxisAlignedBoundingBox currentRightBounds = itemsToAdd[numItems - 1].Aabb;
							rightBoundsAtItem[numItems - 2] = currentRightBounds.GetSurfaceArea();
							for (int itemIndex = numItems - 1; itemIndex > 1; itemIndex -= skipInterval)
							{
								currentRightBounds += itemsToAdd[itemIndex - 1].Aabb;
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
								if (itemsToAdd[itemIndex].Item is IIntersectable intersectable)
								{
									intersectCostOnLeft += intersectable.GetIntersectCost();
								}
								else
								{
									intersectCostOnLeft += AxisAlignedBoundingBox.GetIntersectCost();
								}
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
				return new BvhTree<T>(itemsToAdd);
			}
			else
			{
				axisSorter.WhichAxis = bestAxis;
				itemsToAdd.Sort(axisSorter);
				var leftItems = new List<BvhTreeItemData<T>>(bestIndexToSplitOn + 1);
				var rightItems = new List<BvhTreeItemData<T>>(numItems - bestIndexToSplitOn + 1);
				for (int i = 0; i <= bestIndexToSplitOn; i++)
				{
					leftItems.Add(itemsToAdd[i]);
				}
				for (int i = bestIndexToSplitOn + 1; i < numItems; i++)
				{
					rightItems.Add(itemsToAdd[i]);
				}
				var leftGroup = CreateNewHierachy(leftItems, maxRecursion, recursionDepth + 1, accelerator);
				var rightGroup = CreateNewHierachy(rightItems, maxRecursion, recursionDepth + 1, accelerator);
				var newBVHNode = new BvhTree<T>(leftGroup, rightGroup, bestAxis);
				return newBVHNode;
			}
		}

		public class AxisSorter : IComparer<BvhTreeItemData<T>>
		{
			private int whichAxis;

			public AxisSorter(int whichAxis)
			{
				this.whichAxis = whichAxis % 3;
			}

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

			public int Compare(BvhTreeItemData<T> a, BvhTreeItemData<T> b)
			{
				if (a == null || b == null)
				{
					throw new Exception();
				}

				double axisCenterA = a.Center[whichAxis];
				double axisCenterB = b.Center[whichAxis];

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

		public class SortingAccelerator
		{
			public int nextAxisForBigGroups = 2;

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
	}
}