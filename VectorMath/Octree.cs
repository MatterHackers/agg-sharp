﻿//The MIT License(MIT)

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
using System.Linq;

namespace MatterHackers.VectorMath
{
	/// <summary>
	/// Used by the Octree to represent a rectangular area.
	/// </summary>
	public struct Bounds
	{
		public double MaxX;
		public double MaxY;
		public double MaxZ;
		public double MinX;
		public double MinY;
		public double MinZ;

		public Bounds(AxisAlignedBoundingBox axisAlignedBoundingBox) : this()
		{
			this.MinX = axisAlignedBoundingBox.minXYZ.X;
			this.MinY = axisAlignedBoundingBox.minXYZ.Y;
			this.MinZ = axisAlignedBoundingBox.minXYZ.Z;

			this.MaxX = axisAlignedBoundingBox.maxXYZ.X;
			this.MaxY = axisAlignedBoundingBox.maxXYZ.Y;
			this.MaxZ = axisAlignedBoundingBox.maxXYZ.Z;
		}

		/// <summary>
		/// Construct a new Octree.
		/// </summary>
		/// <param name="minX">Minimum x.</param>
		/// <param name="minY">Minimum y.</param>
		/// <param name="maxX">Max x.</param>
		/// <param name="maxY">Max y.</param>
		public Bounds(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
		{
			MinX = minX;
			MinY = minY;
			MinZ = minZ;
			MaxX = maxX;
			MaxY = maxY;
			MaxZ = maxZ;
		}

		/// <summary>
		/// Check if this Octree can completely contain another.
		/// </summary>
		public bool Contains(Bounds other)
		{
			return other.MinX >= MinX
				&& other.MinY >= MinY
				&& other.MinZ >= MinZ
				&& other.MaxX <= MaxX
				&& other.MaxY <= MaxY
				&& other.MaxZ <= MaxZ;
		}

		/// <summary>
		/// Check if this Octree contains the point.
		/// </summary>
		public bool Contains(double x, double y, double z)
		{
			return x > MinX
				&& y > MinY
				&& z > MinZ
				&& x < MaxX
				&& y < MaxY
				&& z < MaxZ;
		}

		/// <summary>
		/// Check if this Octree intersects with another.
		/// </summary>
		public bool Intersects(Bounds other)
		{
			return MinX <= other.MaxX
				&& MinY <= other.MaxY
				&& MinZ <= other.MaxZ
				&& MaxX >= other.MinX
				&& MaxY >= other.MinY
				&& MaxZ >= other.MinZ;
		}

		/// <summary>
		/// Set the Octree's position.
		/// </summary>
		/// <param name="minX">Minimum x.</param>
		/// <param name="minY">Minimum y.</param>
		/// <param name="maxX">Max x.</param>
		/// <param name="maxY">Max y.</param>
		public void Set(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
		{
			MinX = minX;
			MinY = minY;
			MinZ = minZ;
			MaxX = maxX;
			MaxY = maxY;
			MaxZ = maxZ;
		}

		public void Expand(double amount)
		{
			MinX -= amount; MinY -= amount; MinZ -= amount;
			MaxX += amount; MaxY += amount; MaxZ += amount;
		}
	}

	/// <summary>
	/// A Octree tree where leaf nodes contain a Octree and a unique instance of T.
	/// For example, if you are developing a game, you might use Octree<GameObject>
	/// for collisions, or Octree<int> if you just want to populate it with IDs.
	/// </summary>
	public class Octree<T>
	{
		internal Dictionary<T, Leaf> leafLookup = new Dictionary<T, Leaf>();
		internal int splitCount;
		private Branch root;

		/// <summary>
		/// Creates a new Octree.
		/// </summary>
		/// <param name="splitCount">How many leaves a branch can hold before it splits into sub-branches.</param>
		/// <param name="region">The region that your Octree occupies, all inserted bounds should fit into this.</param>
		public Octree(int splitCount, Bounds region) 
		{
			this.splitCount = splitCount;
			root = CreateBranch(this, null, region);
		}

		/// <summary>
		/// Creates a new Octree.
		/// </summary>
		/// <param name="splitCount">How many leaves a branch can hold before it splits into sub-branches.</param>
		/// <param name="minX">X position of the region.</param>
		/// <param name="minY">Y position of the region.</param>
		/// <param name="maxX">xSize of the region.</param>
		/// <param name="maxY">ySize of the region.</param>
		public Octree(int splitCount, double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
			: this(splitCount, new Bounds(minX, minY, minZ, maxX, maxY, maxZ))
		{
		}

		public int Count { get; private set; }

		/// <summary>
		/// Clear the Octree. This will remove all leaves and branches. If you have a lot of moving objects,
		/// you probably want to call Clear() every frame, and re-insert every object. Branches and leaves are pooled.
		/// </summary>
		public void Clear()
		{
			root.Clear();
			root.Tree = this;
			leafLookup.Clear();
		}

		/// <summary>
		/// Count how many branches are in the Octree.
		/// </summary>
		public int CountBranches()
		{
			int count = CountBranches(root, 0);
			return count;
		}

		/// <summary>
		/// Find all other values whose areas are overlapping the specified value.
		/// </summary>
		/// <returns>True if any collisions were found.</returns>
		/// <param name="value">The value to check collisions against.</param>
		/// <param name="values">A list to populate with the results. If null, this function will create the list for you.</param>
		public IEnumerable<T> FindCollisions(T value)
		{
			Leaf leaf;
			if (leafLookup.TryGetValue(value, out leaf))
			{
				var branch = leaf.Branch;

				//Add the leaf's siblings (prevent it from colliding with itself)
				if (branch.Leaves.Count > 0)
				{
					for (int i = 0; i < branch.Leaves.Count; ++i)
					{
						if (leaf != branch.Leaves[i] && leaf.Bounds.Intersects(branch.Leaves[i].Bounds))
						{
							yield return branch.Leaves[i].Value;
						}
					}
				}

				//Add the branch's children
				if (branch.Split)
				{
					for (int i = 0; i < 8; ++i)
					{
						if (branch.Branches[i] != null)
						{
							foreach (var child in branch.Branches[i].SearchBounds(leaf.Bounds))
							{
								yield return child;
							}
						}
					}
				}

				//Add all leaves back to the root
				branch = branch.Parent;
				while (branch != null)
				{
					if (branch.Leaves.Count > 0)
					{
						for (int i = 0; i < branch.Leaves.Count; ++i)
						{
							if (leaf.Bounds.Intersects(branch.Leaves[i].Bounds))
							{
								yield return branch.Leaves[i].Value;
							}
						}
					}
					branch = branch.Parent;
				}
			}
		}

		public IEnumerable<T> AlongRay(Ray ray)
		{
			return root.AlongRay(ray);
		}

		public IEnumerable<T> All()
		{
			return root.All();
		}

		/// <summary>
		/// Insert a new leaf node into the Octree.
		/// </summary>
		/// <param name="value">The leaf value.</param>
		/// <param name="Bounds">The leaf size.</param>
		public void Insert(T value, Bounds bounds)
		{
			Leaf leaf;
			if (!leafLookup.TryGetValue(value, out leaf))
			{
				leaf = CreateLeaf(value, bounds);
				leafLookup.Add(value, leaf);
			}
			root.Insert(leaf);
			Count++;
		}

		/// <summary>
		/// Insert a new leaf node into the Octree.
		/// </summary>
		/// <param name="value">The leaf value.</param>
		/// <param name="x">X position of the leaf.</param>
		/// <param name="y">Y position of the leaf.</param>
		/// <param name="xSize">xSize of the leaf.</param>
		/// <param name="ySize">ySize of the leaf.</param>
		public void Insert(T value, double x, double y, double z, double xSize, double ySize, double zSize)
		{
			var bounds = new Bounds(x, y, z, x + xSize, y + ySize, z + zSize);
			Insert(value, bounds);
		}

		public void Remove(T value)
		{
			Leaf leaf;
			if (leafLookup.TryGetValue(value, out leaf))
			{
				root.Remove(leaf);
				leafLookup.Remove(value);
				Count--;
			}
		}

		public IEnumerable<T> SearchBounds(Bounds bounds)
		{
			return root.SearchBounds(bounds);
		}

		/// <summary>
		/// Find all values touching in the specified area.
		/// </summary>
		/// <returns>True if any values were found.</returns>
		/// <param name="x">X position to search.</param>
		/// <param name="y">Y position to search.</param>
		/// <param name="xSize">xSize of the search area.</param>
		/// <param name="ySize">ySize of the search area.</param>
		/// <param name="values">A list to populate with the results. If null, this function will create the list for you.</param>
		public IEnumerable<T> SearchBounds(double x, double y, double z, double xSize, double ySize, double zSize)
		{
			var bounds = new Bounds(x, y, z, x + xSize, y + ySize, z + zSize);
			return SearchBounds(bounds);
		}

		/// <summary>
		/// Find all values overlapping the specified point.
		/// </summary>
		/// <returns>True if any values were found.</returns>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		/// <param name="values">A list to populate with the results. If null, this function will create the list for you.</param>
		public IEnumerable<T> SearchPoint(double x, double y, double z)
		{
			return root.SearchPoint(x, y, z);
		}

		private static Branch CreateBranch(Octree<T> tree, Branch parent, Bounds bounds)
		{
			//       ____________
			//      /     /     /
			//     /  6  /  7  / |
			//    /_____/_____/  |
			//   /     /     /   |
			//  /  4  /  5  /    |
			// /_____/_____/     |
			// |     ____________|
			// |     /     /     /
			// |    /  2  /  3  /
			// |   /_____/_____/
			// |  /     /     /
			// | /  0  /  1  /
			//  /_____/_____/

			var branch = new Branch(bounds);
			branch.Tree = tree;
			branch.Parent = parent;
			branch.Split = false;
			double midX = bounds.MinX + (bounds.MaxX - bounds.MinX) / 2;
			double midY = bounds.MinY + (bounds.MaxY - bounds.MinY) / 2;
			double midZ = bounds.MinZ + (bounds.MaxZ - bounds.MinZ) / 2;

			double[] xPos = new double[] { bounds.MinX, midX, midX, bounds.MaxX };
			double[] yPos = new double[] { bounds.MinY, midY, midY, bounds.MaxY };
			double[] zPos = new double[] { bounds.MinZ, midZ, midZ, bounds.MaxZ };

			branch.ChildBounds[0].Set(xPos[0], yPos[0], zPos[0], xPos[1], yPos[1], zPos[1]);
			branch.ChildBounds[1].Set(xPos[2], yPos[0], zPos[0], xPos[3], yPos[1], zPos[1]);
			branch.ChildBounds[2].Set(xPos[0], yPos[2], zPos[0], xPos[1], yPos[3], zPos[1]);
			branch.ChildBounds[3].Set(xPos[2], yPos[2], zPos[0], xPos[3], yPos[3], zPos[1]);
			branch.ChildBounds[4].Set(xPos[0], yPos[0], zPos[2], xPos[1], yPos[1], zPos[3]);
			branch.ChildBounds[5].Set(xPos[2], yPos[0], zPos[2], xPos[3], yPos[1], zPos[3]);
			branch.ChildBounds[6].Set(xPos[0], yPos[2], zPos[2], xPos[1], yPos[3], zPos[3]);
			branch.ChildBounds[7].Set(xPos[2], yPos[2], zPos[2], xPos[3], yPos[3], zPos[3]);
			return branch;
		}

		private static Leaf CreateLeaf(T value, Bounds bounds)
		{
			var leaf = new Leaf();
			leaf.Value = value;
			leaf.Bounds = bounds;
			return leaf;
		}

		private int CountBranches(Branch branch, int count)
		{
			++count;
			if (branch.Split)
			{
				for (int i = 0; i < 8; ++i)
				{
					if (branch.Branches[i] != null)
					{
						CountBranches(branch.Branches[i], count);
					}
				}
			}

			return count;
		}

		internal class Branch
		{
			internal Bounds Bounds = new Bounds();
			internal Bounds[] ChildBounds = new Bounds[8];
			internal Branch[] Branches = new Branch[8];
			internal List<Leaf> Leaves = new List<Leaf>();
			internal Branch Parent;
			internal bool Split;
			internal Octree<T> Tree;

			internal Branch(Bounds bounds)
			{
				this.Bounds = bounds;
			}

			internal IEnumerable<T> AlongRay(Ray ray)
			{
				if (intersect(ray))
				{
					var items = new Stack<Branch>(new Branch[] { this });
					while (items.Any())
					{
						Branch item = items.Pop();

						if (item.Leaves.Count > 0)
						{
							for (int i = 0; i < item.Leaves.Count; ++i)
							{
								yield return item.Leaves[i].Value;
							}
						}

						for (int i = 0; i < 8; ++i)
						{
							if (item.Branches[i] != null)
							{
								items.Push(item.Branches[i]);
							}
						}
					}
				}
			}

			public Vector3 this[int index]
			{
				get
				{
					if (index == 0)
					{
						return new Vector3(Bounds.MinX, Bounds.MinY, Bounds.MinZ);
					}
					else if (index == 1)
					{
						return new Vector3(Bounds.MaxX, Bounds.MaxY, Bounds.MaxZ);
					}
					else
					{
						throw new IndexOutOfRangeException();
					}
				}
			}

			private bool intersect(Ray ray)
			{
				double minDistFound;
				double maxDistFound;

				// we calculate distance to the intersection with the x planes of the box
				minDistFound = (this[(int)ray.sign[0]].X - ray.origin.X) * ray.oneOverDirection.X;
				maxDistFound = (this[1 - (int)ray.sign[0]].X - ray.origin.X) * ray.oneOverDirection.X;

				// now find the distance to the y planes of the box
				double minDistToY = (this[(int)ray.sign[1]].Y - ray.origin.Y) * ray.oneOverDirection.Y;
				double maxDistToY = (this[1 - (int)ray.sign[1]].Y - ray.origin.Y) * ray.oneOverDirection.Y;

				if ((minDistFound > maxDistToY) || (minDistToY > maxDistFound))
				{
					return false;
				}

				if (minDistToY > minDistFound)
				{
					minDistFound = minDistToY;
				}

				if (maxDistToY < maxDistFound)
				{
					maxDistFound = maxDistToY;
				}

				// and finaly the z planes
				double minDistToZ = (this[(int)ray.sign[2]].Z - ray.origin.Z) * ray.oneOverDirection.Z;
				double maxDistToZ = (this[1 - (int)ray.sign[2]].Z - ray.origin.Z) * ray.oneOverDirection.Z;

				if ((minDistFound > maxDistToZ) || (minDistToZ > maxDistFound))
				{
					return false;
				}

				if (minDistToZ > minDistFound)
				{
					minDistFound = minDistToZ;
				}

				if (maxDistToZ < maxDistFound)
				{
					maxDistFound = maxDistToZ;
				}

				bool oneHitIsWithinLimits = (minDistFound < ray.maxDistanceToConsider && minDistFound > ray.minDistanceToConsider)
					|| (maxDistFound < ray.maxDistanceToConsider && maxDistFound > ray.minDistanceToConsider);

				return oneHitIsWithinLimits;
			}

			internal IEnumerable<T> All()
			{
				var items = new Stack<Branch>(new Branch[] { this });
				while (items.Any())
				{
					Branch item = items.Pop();

					if (item.Leaves.Count > 0)
					{
						for (int i = 0; i < item.Leaves.Count; ++i)
						{
							yield return item.Leaves[i].Value;
						}
					}

					for (int i = 0; i < 8; ++i)
					{
						if (item.Branches[i] != null)
						{
							items.Push(item.Branches[i]);
						}
					}
				}
			}

			internal void Clear()
			{
				Tree = null;
				Parent = null;
				Split = false;

				for (int i = 0; i < 8; ++i)
				{
					Branches[i] = null;
				}

				Leaves.Clear();
			}

			internal void Insert(Leaf leaf)
			{
				//If this branch is already split
				if (Split)
				{
					for (int i = 0; i < 8; ++i)
					{
						if (ChildBounds[i].Contains(leaf.Bounds))
						{
							if (Branches[i] == null)
							{
								Branches[i] = CreateBranch(Tree, this, ChildBounds[i]);
							}
							Branches[i].Insert(leaf);
							return;
						}
					}

					Leaves.Add(leaf);
					leaf.Branch = this;
				}
				else
				{
					//Add the leaf to this node
					Leaves.Add(leaf);
					leaf.Branch = this;

					//Once I have reached capacity, split the node
					if (Leaves.Count >= Tree.splitCount)
					{
						var temp = new List<Leaf>();
						temp.AddRange(Leaves);
						Leaves.Clear();
						Split = true;
						for (int i = 0; i < temp.Count; ++i)
						{
							Insert(temp[i]);
						}
					}
				}
			}

			internal void Remove(Leaf leaf)
			{
				if (Leaves.Contains(leaf))
				{
					Leaves.Remove(leaf);
				}
				else if (Split)
				{
					for (int i = 0; i < 8; ++i)
					{
						if (ChildBounds[i].Contains(leaf.Bounds)
							&& Branches[i] != null)
						{
							Branches[i].Remove(leaf);
						}
					}
				}
			}

			internal IEnumerable<T> SearchBounds(Bounds bounds)
			{
				var items = new Stack<Branch>(new Branch[] { this });
				while (items.Any())
				{
					Branch item = items.Pop();

					if (item.Leaves.Count > 0)
					{
						for (int i = 0; i < item.Leaves.Count; ++i)
						{
							if (bounds.Intersects(item.Leaves[i].Bounds))
							{
								yield return item.Leaves[i].Value;
							}
						}
					}

					for (int i = 0; i < 8; ++i)
					{
						if (item.Branches[i] != null)
						{
							items.Push(item.Branches[i]);
						}
					}
				}
			}

			internal IEnumerable<T> SearchPoint(double x, double y, double z)
			{
				if (Leaves.Count > 0)
				{
					for (int i = 0; i < Leaves.Count; ++i)
					{
						if (Leaves[i].Bounds.Contains(x, y, z))
						{
							yield return Leaves[i].Value;
						}
					}
				}

				for (int i = 0; i < 8; ++i)
				{
					if (Branches[i] != null)
					{
						foreach (var child in Branches[i].SearchPoint(x, y, z))
						{
							yield return child;
						}
					}
				}
			}
		}

		internal class Leaf
		{
			internal Bounds Bounds;
			internal Branch Branch;
			internal T Value;
		}
	}
}