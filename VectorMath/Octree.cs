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
			this.MinX = axisAlignedBoundingBox.minXYZ.x;
			this.MinY = axisAlignedBoundingBox.minXYZ.y;
			this.MinZ = axisAlignedBoundingBox.minXYZ.z;

			this.MaxX = axisAlignedBoundingBox.maxXYZ.x;
			this.MaxY = axisAlignedBoundingBox.maxXYZ.y;
			this.MaxZ = axisAlignedBoundingBox.maxXYZ.z;
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
			int count = 0;
			CountBranches(root, count);
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

		public IEnumerable<T> AllObjects()
		{
			return root.AllObjects();
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

			var branch = new Branch();
			branch.Tree = tree;
			branch.Parent = parent;
			branch.Split = false;
			double midX = bounds.MinX + (bounds.MaxX - bounds.MinX) / 2;
			double midY = bounds.MinY + (bounds.MaxY - bounds.MinY) / 2;
			double midZ = bounds.MinZ + (bounds.MaxZ - bounds.MinZ) / 2;

			double[] xPos = new double[] { bounds.MinX, midX, midX, bounds.MaxX };
			double[] yPos = new double[] { bounds.MinY, midY, midY, bounds.MaxY };
			double[] zPos = new double[] { bounds.MinZ, midZ, midZ, bounds.MaxZ };

			branch.Bounds[0].Set(xPos[0], yPos[0], zPos[0], xPos[1], yPos[1], zPos[1]);
			branch.Bounds[1].Set(xPos[2], yPos[0], zPos[0], xPos[3], yPos[1], zPos[1]);
			branch.Bounds[2].Set(xPos[0], yPos[2], zPos[0], xPos[1], yPos[3], zPos[1]);
			branch.Bounds[3].Set(xPos[2], yPos[2], zPos[0], xPos[3], yPos[3], zPos[1]);
			branch.Bounds[4].Set(xPos[0], yPos[0], zPos[2], xPos[1], yPos[1], zPos[3]);
			branch.Bounds[5].Set(xPos[2], yPos[0], zPos[2], xPos[3], yPos[1], zPos[3]);
			branch.Bounds[6].Set(xPos[0], yPos[2], zPos[2], xPos[1], yPos[3], zPos[3]);
			branch.Bounds[7].Set(xPos[2], yPos[2], zPos[2], xPos[3], yPos[3], zPos[3]);
			return branch;
		}

		private static Leaf CreateLeaf(T value, Bounds bounds)
		{
			var leaf = new Leaf();
			leaf.Value = value;
			leaf.Bounds = bounds;
			return leaf;
		}

		private void CountBranches(Branch branch, int count)
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
		}

		internal class Branch
		{
			internal Bounds[] Bounds = new Bounds[8];
			internal Branch[] Branches = new Branch[8];
			internal List<Leaf> Leaves = new List<Leaf>();
			internal Branch Parent;
			internal bool Split;
			internal Octree<T> Tree;

			internal IEnumerable<T> AllObjects()
			{
				if (Leaves.Count > 0)
				{
					for (int i = 0; i < Leaves.Count; ++i)
					{
						yield return Leaves[i].Value;
					}
				}

				for (int i = 0; i < 8; ++i)
				{
					if (Branches[i] != null)
					{
						foreach (var children in Branches[i].AllObjects())
						{
							yield return children;
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
						if (Bounds[i].Contains(leaf.Bounds))
						{
							if (Branches[i] == null)
							{
								Branches[i] = CreateBranch(Tree, this, Bounds[i]);
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
						if (Bounds[i].Contains(leaf.Bounds)
							&& Branches[i] != null)
						{
							Branches[i].Remove(leaf);
						}
					}
				}
			}

			internal IEnumerable<T> SearchBounds(Bounds bounds)
			{
				if (Leaves.Count > 0)
				{
					for (int i = 0; i < Leaves.Count; ++i)
					{
						if (bounds.Intersects(Leaves[i].Bounds))
						{
							yield return Leaves[i].Value;
						}
					}
				}

				for (int i = 0; i < 8; ++i)
				{
					if (Branches[i] != null)
					{
						foreach (var child in Branches[i].SearchBounds(bounds))
						{
							yield return child;
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