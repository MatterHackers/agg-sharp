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
	public partial class BvhTree<T> : IIntersectable
	{
		// any of the items that are in this node
		public List<BvhTreeItemData<T>> Items = new List<BvhTreeItemData<T>>();

		private readonly BvhTree<T> nodeA;

		private readonly BvhTree<T> nodeB;

		private readonly int splittingPlane;

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

		public BvhTree(BvhTree<T> nodeA, BvhTree<T> nodeB, int splittingPlane)
		{
			this.splittingPlane = splittingPlane;
			this.nodeA = nodeA;
			this.nodeB = nodeB;
			this.Aabb = nodeA.Aabb + nodeB.Aabb; // we can cache this because it is not allowed to change.
			this.Center = Aabb.Center;
		}

		public AxisAlignedBoundingBox Aabb { get; private set; }

		public Vector3 Center { get; private set; }

		public int Count { get; private set; }

		public void All(List<T> results)
		{
			// check all the items that are part of this object
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
				// check all the items that are part of this object
				foreach (var item in Items)
				{
					if (item is IIntersectable intersectable)
					{
						RayHitInfo info = intersectable.GetIntersection(ray);
						if (info != null && info.HitType != IntersectionType.None && info.DistanceToHit >= 0)
						{
							results.Add(item.Item);
						}
					}
					else if (ray.Intersection(item.Aabb))
					{
						results.Add(item.Item);
					}
				}

				nodeA?.AlongRay(ray, results);
				nodeB?.AlongRay(ray, results);
			}
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
				// check all the items that are part of this object
				foreach (var item in Items)
				{
					if (item is IIntersectable intersectable)
					{
						RayHitInfo info = intersectable.GetIntersection(ray);
						if (info != null && info.HitType != IntersectionType.None && info.DistanceToHit >= 0)
						{
							if (ray.isShadowRay)
							{
								return info;
							}
							else if (bestIntersect == null || info.DistanceToHit < bestIntersect.DistanceToHit)
							{
								bestIntersect = info;
								ray.maxDistanceToConsider = bestIntersect.DistanceToHit;
							}
						}
					}
					// we will just be hitting the bounding box of the type
					else
					{
						RayHitInfo info = ray.GetClosestIntersection(item.Aabb);
						if (info != null && info.HitType != IntersectionType.None && info.DistanceToHit >= 0)
						{
							info.ClosestHitObject = item.Item;
							if (ray.isShadowRay)
							{
								return info;
							}
							else if (bestIntersect == null || info.DistanceToHit < bestIntersect.DistanceToHit)
							{
								bestIntersect = info;
								ray.maxDistanceToConsider = bestIntersect.DistanceToHit;
							}
						}
					}
				}

				var checkFirst = nodeA;
				var checkSecond = nodeB;
				if (ray.directionNormal[splittingPlane] < 0)
				{
					checkFirst = nodeB;
					checkSecond = nodeA;
				}

				if (checkFirst != null)
				{
					RayHitInfo firstIntersect = checkFirst.GetClosestIntersection(ray);
					if (firstIntersect != null && firstIntersect.HitType != IntersectionType.None)
					{
						if (ray.isShadowRay)
						{
							return firstIntersect;
						}
						else if (bestIntersect == null || firstIntersect.DistanceToHit < bestIntersect.DistanceToHit)
						{
							bestIntersect = firstIntersect;
							ray.maxDistanceToConsider = bestIntersect.DistanceToHit;
						}
					}
				}
				if (checkSecond != null)
				{
					RayHitInfo secondIntersect = checkSecond.GetClosestIntersection(ray);
					if (secondIntersect != null && secondIntersect.HitType != IntersectionType.None)
					{
						if (ray.isShadowRay)
						{
							return secondIntersect;
						}
						else if (bestIntersect == null || secondIntersect.DistanceToHit < bestIntersect.DistanceToHit)
						{
							bestIntersect = secondIntersect;
							ray.maxDistanceToConsider = bestIntersect.DistanceToHit;
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

		public void SearchBounds(AxisAlignedBoundingBox bounds, List<T> results)
		{
			if (bounds.Intersects(Aabb))
			{
				// check all the items that are part of this object
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
				// check all the items that are part of this object
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
	}
}