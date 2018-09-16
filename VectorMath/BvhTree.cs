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
	public interface IIntersectable
	{
		/// <summary>
		/// This is the computation cost of doing an intersection with the given type.
		/// It is relative to the coust of doing a simple BvhTree bounds calculation which is 1.
		/// </summary>
		/// <returns></returns>
		double GetIntersectCost();

		/// <summary>
		/// Calculate the RayHitInfo for this object
		/// </summary>
		/// <param name="ray"></param>
		/// <returns></returns>
		RayHitInfo GetIntersection(Ray ray);
	}

	public class BvhTree<T> : IIntersectable
	{
		// any of the items that are in this node
		public List<BvhTreeItemData<T>> Items = new List<BvhTreeItemData<T>>();

		private BvhTree<T> nodeA;

		private BvhTree<T> nodeB;

		private int splitingPlane;

		public BvhTree()
		{
		}

		public BvhTree(IEnumerable<BvhTreeItemData<T>> items)
		{
			Aabb = AxisAlignedBoundingBox.Empty();
			foreach (var item in items)
			{
				Items.Add(item);
				Aabb += item.Aabb;
			}
		}

		public BvhTree(BvhTree<T> nodeA, BvhTree<T> nodeB, int splitingPlane)
		{
			this.splitingPlane = splitingPlane;
			this.nodeA = nodeA;
			this.nodeB = nodeB;
			this.Aabb = nodeA.Aabb + nodeB.Aabb; // we can cache this because it is not allowed to change.
			this.Center = Aabb.Center;
		}

		public AxisAlignedBoundingBox Aabb { get; private set; }

		public Vector3 Center { get; private set; }

		public int Count { get; private set; }

		public static BvhTree<T> CreateNewHierachy(List<BvhTreeItemData<T>> itemsToAdd,
			int maxRecursion = int.MaxValue,
			int recursionDepth = 0,
			SortingAccelerator accelerator = null)
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

		public void All(List<T> results)
		{
			// check all the itmes that are part of this object
			foreach (var item in Items)
			{
				results.Add(item.Item);
			}

			nodeA.All(results);
			nodeB.All(results);
		}

		public void AlongRay(Ray ray, List<T> results)
		{
			if (ray.Intersection(Aabb))
			{
				// check all the itmes that are part of this object
				foreach (var item in Items)
				{
					if (item is IIntersectable intersectable)
					{
						RayHitInfo info = intersectable.GetIntersection(ray);
						if (info != null && info.hitType != IntersectionType.None && info.distanceToHit >= 0)
						{
							results.Add(item.Item);
						}
					}
					else
					{
						results.Add(item.Item);
					}
				}

				nodeA?.AlongRay(ray, results);
				nodeB?.AlongRay(ray, results);
			}
		}

		public void Clear()
		{
			throw new NotImplementedException();
		}

		public int CountBranches()
		{
			int count = 1;
			if (nodeA != null)
			{
				count += nodeA.CountBranches();
			}

			if (nodeB != null)
			{
				count += nodeB.CountBranches();
			}
			return count;
		}

		public RayHitInfo GetClosestIntersection(Ray ray)
		{
			RayHitInfo bestIntersect = null;

			if (ray.Intersection(Aabb))
			{
				// check all the itmes that are part of this object
				foreach (var item in Items)
				{
					if (item is IIntersectable intersectable)
					{
						RayHitInfo info = intersectable.GetIntersection(ray);
						if (info != null && info.hitType != IntersectionType.None && info.distanceToHit >= 0)
						{
							if (ray.isShadowRay)
							{
								return info;
							}
							else if (bestIntersect == null || info.distanceToHit < bestIntersect.distanceToHit)
							{
								bestIntersect = info;
								ray.maxDistanceToConsider = bestIntersect.distanceToHit;
							}
						}
					}
				}

				var checkFirst = nodeA;
				var checkSecond = nodeB;
				if (ray.directionNormal[splitingPlane] < 0)
				{
					checkFirst = nodeB;
					checkSecond = nodeA;
				}

				RayHitInfo firstIntersect = checkFirst.GetClosestIntersection(ray);
				if (firstIntersect != null && firstIntersect.hitType != IntersectionType.None)
				{
					if (ray.isShadowRay)
					{
						return firstIntersect;
					}
					else if (bestIntersect == null || firstIntersect.distanceToHit < bestIntersect.distanceToHit)
					{
						bestIntersect = firstIntersect;
						ray.maxDistanceToConsider = bestIntersect.distanceToHit;
					}
				}
				if (checkSecond != null)
				{
					RayHitInfo secondIntersect = checkSecond.GetClosestIntersection(ray);
					if (secondIntersect != null && secondIntersect.hitType != IntersectionType.None)
					{
						if (ray.isShadowRay)
						{
							return secondIntersect;
						}
						else if (bestIntersect == null || secondIntersect.distanceToHit < bestIntersect.distanceToHit)
						{
							bestIntersect = secondIntersect;
							ray.maxDistanceToConsider = bestIntersect.distanceToHit;
						}
					}
				}
			}

			return bestIntersect;
		}

		public double GetIntersectCost()
		{
			return AxisAlignedBoundingBox.GetIntersectCost();
		}

		public RayHitInfo GetIntersection(Ray ray)
		{
			return GetClosestIntersection(ray);
		}

		public void Insert(T value, AxisAlignedBoundingBox bounds)
		{
			throw new NotImplementedException();
		}

		public void Insert(T value, double x, double y, double z, double xSize, double ySize, double zSize)
		{
			throw new NotImplementedException();
		}

		public void Remove(T value)
		{
			throw new NotImplementedException();
		}

		public void SearchBounds(AxisAlignedBoundingBox bounds, List<T> results)
		{
			if (bounds.Intersects(Aabb))
			{
				// check all the itmes that are part of this object
				foreach (var item in Items)
				{
					if (item.Aabb.Intersects(bounds))
					{
						results.Add(item.Item);
					}
				}

				nodeA?.SearchBounds(bounds, results);
				nodeB?.SearchBounds(bounds, results);
			}
		}

		public void SearchBounds(double x, double y, double z,
			double xSize, double ySize, double zSize,
			List<T> results)
		{
			SearchBounds(new AxisAlignedBoundingBox(
				new Vector3(x, y, z),
				new Vector3(x + xSize, y + ySize, z + zSize)),
				results);
		}

		public void SearchPoint(Vector3 position, List<T> results)
		{
			if (Aabb.Contains(position))
			{
				// check all the itmes that are part of this object
				foreach (var item in Items)
				{
					if (item.Aabb.Contains(position))
					{
						results.Add(item.Item);
					}
				}

				nodeA?.SearchPoint(position, results);
				nodeB?.SearchPoint(position, results);
			}
		}

		public void SearchPoint(double x, double y, double z, List<T> results)
		{
			SearchPoint(new Vector3(x, y, z), results);
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

	public class BvhTreeItemData<T>
	{
		public BvhTreeItemData(T item, AxisAlignedBoundingBox bounds)
		{
			this.Item = item;
			Aabb = bounds;
		}

		public AxisAlignedBoundingBox Aabb { get; set; }
		public Vector3 Center { get; set; }
		public T Item { get; set; }
	}

	public class RayHitInfo
	{
		public object closestHitObject;
		public double distanceToHit;
		public IntersectionType hitType;
		public Vector3 normalAtHit;

		public RayHitInfo()
		{
			distanceToHit = double.MaxValue;
		}

		public RayHitInfo(RayHitInfo copyInfo)
		{
			this.hitType = copyInfo.hitType;
			this.closestHitObject = copyInfo.closestHitObject;
			this.HitPosition = copyInfo.HitPosition;
			this.normalAtHit = copyInfo.normalAtHit;
			this.distanceToHit = copyInfo.distanceToHit;
		}

		public Vector3 HitPosition { get; set; }
	}
}