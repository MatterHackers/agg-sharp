// The MIT License(MIT)

// Copyright(c) 2015 ChevyRay, 2017 Lars Brubaker

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.Agg.QuadTree
{
	public class Branch<T>
	{
		public List<Leaf<T>> Leaves = new List<Leaf<T>>();
		internal static ConcurrentStack<List<Leaf<T>>> tempPool = new ConcurrentStack<List<Leaf<T>>>();

		internal Branch<T> Parent;
		internal bool Split;
		internal QuadTree<T> Tree;

		public Branch<T>[] Branches { get; private set; } = new Branch<T>[4];

		public Quad[] Quads { get; private set; } = new Quad[4];

		public Quad Bounds { get; private set; }

		public Branch(Quad bounds)
		{
			Bounds = bounds;
		}

		internal void Clear()
		{
			Tree = null;
			Parent = null;
			Split = false;

			for (int i = 0; i < 4; ++i)
			{
				if (Branches[i] != null)
				{
					QuadTree<T>.branchPool.Push(Branches[i]);
					Branches[i].Clear();
					Branches[i] = null;
				}
			}

			for (int i = 0; i < Leaves.Count; ++i)
			{
				QuadTree<T>.leafPool.Push(Leaves[i]);
				Leaves[i].ContainingBranch = null;
				Leaves[i].Value = default(T);
			}

			Leaves.Clear();
		}

		internal void Insert(Leaf<T> leaf)
		{
			// If this branch is already split
			if (Split)
			{
				for (int i = 0; i < 4; ++i)
				{
					if (Quads[i].Contains(ref leaf.Quad))
					{
						if (Branches[i] == null)
						{
							Branches[i] = QuadTree<T>.CreateBranch(Tree, this, ref Quads[i]);
						}

						Branches[i].Insert(leaf);
						return;
					}
				}

				Leaves.Add(leaf);
				leaf.ContainingBranch = this;
			}
			else
			{
				// Add the leaf to this node
				Leaves.Add(leaf);
				leaf.ContainingBranch = this;

				// Once I have reached capacity, split the node
				if (Leaves.Count >= Tree.splitCount)
				{
					if (Quads[0].MinX + 2 < Quads[0].MaxX
						&& Quads[0].MinY + 2 < Quads[0].MaxY)
					{
						List<Leaf<T>> temp;
						if (!tempPool.TryPop(out temp))
						{
							temp = new List<Leaf<T>>();
						}

						temp.AddRange(Leaves);
						Leaves.Clear();
						Split = true;
						for (int i = 0; i < temp.Count; ++i)
						{
							Insert(temp[i]);
						}

						temp.Clear();
						tempPool.Push(temp);
					}
				}
			}
		}

		internal void SearchPoint(long x, long y, List<T> output)
		{
			var nodes = new Stack<Branch<T>>(new[] { this });
			while (nodes.Any())
			{
				Branch<T> node = nodes.Pop();

				if (node.Leaves.Count > 0)
				{
					for (int i = 0; i < node.Leaves.Count; ++i)
					{
						if (node.Leaves[i].Quad.Contains(x, y))
						{
							output.Add(node.Leaves[i].Value);
						}
					}
				}

				for (int i = 0; i < 4; ++i)
				{
					if (node.Branches[i] != null
						&& node.Branches[i].Bounds.Contains(x, y))
					{
						nodes.Push(node.Branches[i]);
					}
				}
			}
		}

		internal void SearchQuad(Quad quad, List<T> output)
		{
			var nodes = new Stack<Branch<T>>(new[] { this });
			while (nodes.Any())
			{
				Branch<T> node = nodes.Pop();

				if (node.Leaves.Count > 0)
				{
					for (int i = 0; i < node.Leaves.Count; ++i)
					{
						if (quad.Intersects(node.Leaves[i].Quad))
						{
							output.Add(node.Leaves[i].Value);
						}
					}
				}

				for (int i = 0; i < 4; ++i)
				{
					if (node.Branches[i] != null
						&& quad.Intersects(node.Branches[i].Bounds))
					{
						nodes.Push(node.Branches[i]);
					}
				}
			}
		}

		private int CountParents()
		{
			int count = 0;
			var parent = Parent;
			while (parent != null)
			{
				count++;
				parent = parent.Parent;
			}

			return count;
		}
	}

	public class Leaf<T>
	{
		public Quad Quad;
		internal Branch<T> ContainingBranch;
		internal T Value;
	}
}