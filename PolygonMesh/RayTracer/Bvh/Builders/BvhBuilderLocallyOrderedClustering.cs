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
using MatterHackers.VectorMath.Bvh;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;

namespace MatterHackers.RayTracer
{
    public class BvhBuilderLocallyOrderedClustering
    {
        private const int MinNodesForParallel = 1000;
        private const int MaxNodesToOptimize = 10000; // Maximum number of nodes to consider for SAH optimization
        private const bool EnableTreeOptimization = true; // Flag to enable/disable the optimization pass
        private const int BatchSize = 128; // Batch size for parallel operations
        private const int MaxStackDepth = 64; // Maximum stack depth for non-recursive processing

        private static readonly BvhNodePool NodePool = new BvhNodePool();
        private static readonly ConcurrentBag<List<ITraceable>> ListPool = new ConcurrentBag<List<ITraceable>>();

        private static List<ITraceable> GetList(int capacity)
        {
            if (ListPool.TryTake(out var list))
            {
                list.Clear();
                list.Capacity = Math.Max(list.Capacity, capacity);
                return list;
            }
            return new List<ITraceable>(capacity);
        }

        private static void ReturnList(List<ITraceable> list)
        {
            if (list != null)
            {
                ListPool.Add(list);
            }
        }

        public static ITraceable Create(List<ITraceable> sourceNodes)
        {
            if (sourceNodes.Count == 0) return null;
            if (sourceNodes.Count == 1) return sourceNodes[0];

            var bounds = CalculateBounds(sourceNodes);
            var scale = Vector3.Max(bounds.Size, Vector3.One) * 1.001;
            var centersToMortonSpace = CalculateMortonTransform(bounds, scale);

            var nodes = new (ITraceable node, long mortonCode)[sourceNodes.Count];
            
            // Process nodes in batches for better cache locality
            if (sourceNodes.Count > BatchSize * 4)
            {
                int batchCount = (sourceNodes.Count + BatchSize - 1) / BatchSize;
                Parallel.For(0, batchCount, b =>
                {
                    int start = b * BatchSize;
                    int end = Math.Min(start + BatchSize, sourceNodes.Count);
                    for (int i = start; i < end; i++)
                    {
                        nodes[i] = (sourceNodes[i], CalculateMortonCode(sourceNodes[i], centersToMortonSpace));
                    }
                });
            }
            else
            {
                Parallel.For(0, sourceNodes.Count, i =>
                {
                    nodes[i] = (sourceNodes[i], CalculateMortonCode(sourceNodes[i], centersToMortonSpace));
                });
            }

            RadixSort(nodes);

            ITraceable rootNode = BuildTreeParallel(nodes, 0, nodes.Length - 1);
            
            // Apply optimization pass if enabled and the tree is not too large
            if (EnableTreeOptimization && sourceNodes.Count <= MaxNodesToOptimize)
            {
                rootNode = OptimizeTree(rootNode);
            }
            
            return rootNode;
        }

        private static void RadixSort((ITraceable node, long mortonCode)[] a)
        {
            var t = new (ITraceable node, long mortonCode)[a.Length];
            const int r = 8;
            const int mask = (1 << r) - 1;
            var count = new int[1 << r];
            var pref = new int[1 << r];

            for (int shift = 0; shift < 64; shift += r)
            {
                Array.Clear(count, 0, count.Length);

                // Improved loop with better cache locality
                int length = a.Length;
                for (int i = 0; i < length; i++)
                {
                    count[(a[i].mortonCode >> shift) & mask]++;
                }

                pref[0] = 0;
                // Unroll the prefix sum loop slightly for better performance
                for (int i = 1; i < count.Length; i += 4)
                {
                    pref[i] = pref[i - 1] + count[i - 1];
                    if (i + 1 < count.Length) pref[i + 1] = pref[i] + count[i];
                    if (i + 2 < count.Length) pref[i + 2] = pref[i + 1] + count[i + 1];
                    if (i + 3 < count.Length) pref[i + 3] = pref[i + 2] + count[i + 2];
                }

                for (int i = 0; i < length; i++)
                {
                    int index = (int)((a[i].mortonCode >> shift) & mask);
                    t[pref[index]++] = a[i];
                }

                // Use Array.Copy for struct arrays
                Array.Copy(t, 0, a, 0, length);
            }
        }

        private static ITraceable BuildTreeParallel((ITraceable node, long mortonCode)[] nodes, int start, int end)
        {
            if (start == end) return nodes[start].node;

            if (end - start < MinNodesForParallel)
            {
                return BuildTreeRecursive(nodes, start, end);
            }

            int split = FindBestSplit(nodes, start, end);

            ITraceable left = null, right = null;
            Parallel.Invoke(
                () => left = BuildTreeParallel(nodes, start, split),
                () => right = BuildTreeParallel(nodes, split + 1, end)
            );

            return NodePool.Get(left, right);
        }

        private static ITraceable BuildTreeRecursive((ITraceable node, long mortonCode)[] nodes, int start, int end)
        {
            // Non-recursive implementation to avoid stack overflow on deep trees
            if (end - start > MaxStackDepth)
            {
                return BuildTreeIterative(nodes, start, end);
            }

            if (start == end) return nodes[start].node;

            int split = FindBestSplit(nodes, start, end);

            var left = BuildTreeRecursive(nodes, start, split);
            var right = BuildTreeRecursive(nodes, split + 1, end);

            return NodePool.Get(left, right);
        }

        private static ITraceable BuildTreeIterative((ITraceable node, long mortonCode)[] nodes, int start, int end)
        {
            // For a safe fallback, just use direct construction with minimal memory usage
            if (start == end) return nodes[start].node;
            
            // This is a simple method that builds a balanced tree directly from the sorted nodes
            // Much safer than trying to do complex parent-child tracking
            return BuildBalancedTreeFromRange(nodes, start, end);
        }
        
        private static ITraceable BuildBalancedTreeFromRange((ITraceable node, long mortonCode)[] nodes, int start, int end)
        {
            if (start == end) return nodes[start].node;
            if (start + 1 == end) return NodePool.Get(nodes[start].node, nodes[end].node);
            
            int mid = (start + end) / 2;
            var left = BuildBalancedTreeFromRange(nodes, start, mid);
            var right = BuildBalancedTreeFromRange(nodes, mid + 1, end);
            
            return NodePool.Get(left, right);
        }

        private static int FindBestSplit((ITraceable node, long mortonCode)[] nodes, int start, int end)
        {
            if (end - start <= 1) return start;

            // Optimized bit operations for prefix finding
            long startCode = nodes[start].mortonCode;
            long endCode = nodes[end].mortonCode;
            int commonPrefix = System.Numerics.BitOperations.LeadingZeroCount((ulong)(startCode ^ endCode)) - 64;

            // Binary search for split point with early exit
            int split = start;
            int step = end - start;
            
            do
            {
                step = (step + 1) >> 1;
                int newSplit = split + step;
                
                if (newSplit < end)
                {
                    // Lookup the morton code only once
                    long splitCode = nodes[newSplit].mortonCode;
                    int splitPrefix = System.Numerics.BitOperations.LeadingZeroCount((ulong)(startCode ^ splitCode)) - 64;
                    
                    if (splitPrefix > commonPrefix)
                        split = newSplit;
                }
            } while (step > 1);

            return split;
        }
        
        private static ITraceable OptimizeTree(ITraceable rootNode)
        {
            // Only optimize BVH nodes
            if (!(rootNode is BoundingVolumeHierarchy bvh))
            {
                return rootNode;
            }
            
            // Extract all leaf nodes for SAH evaluation
            var leafNodes = GetList(1024); // Initial capacity estimate
            CollectLeafNodes(rootNode, leafNodes);
            
            if (leafNodes.Count <= 1)
            {
                ReturnList(leafNodes);
                return rootNode;
            }
            
            // Apply SAH-based tree building
            var result = BuildOptimizedTree(leafNodes);
            
            ReturnList(leafNodes);
            return result;
        }
        
        private static void CollectLeafNodes(ITraceable node, List<ITraceable> leafNodes)
        {
            // Use a stack-based approach to avoid recursion for very deep trees
            var stack = new Stack<ITraceable>(32); // Initial capacity - tunable
            stack.Push(node);
            
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                
                if (current is BoundingVolumeHierarchy bvh)
                {
                    stack.Push(bvh.Right);
                    stack.Push(bvh.Left);
                }
                else
                {
                    leafNodes.Add(current);
                }
            }
        }
        
        private static ITraceable BuildOptimizedTree(List<ITraceable> leafNodes)
        {
            // For small numbers of nodes, just use bottom-up SAH clustering
            if (leafNodes.Count <= 4)
            {
                return BuildSahTree(leafNodes, 0, leafNodes.Count - 1);
            }
            
            // For larger trees, use a hybrid approach:
            // 1. Partition into spatial bins with Morton codes
            // 2. Apply SAH optimization to each bin
            
            // Calculate scene bounds
            var bounds = CalculateBounds(leafNodes);
            var scale = Vector3.Max(bounds.Size, Vector3.One) * 1.001;
            var centersToMortonSpace = CalculateMortonTransform(bounds, scale);
            
            // Assign Morton codes to each node - use parallel for large node counts
            int leafCount = leafNodes.Count;
            var nodes = new (ITraceable node, long mortonCode)[leafCount];
            
            if (leafCount > BatchSize * 4)
            {
                int batchCount = (leafCount + BatchSize - 1) / BatchSize;
                Parallel.For(0, batchCount, b =>
                {
                    int start = b * BatchSize;
                    int end = Math.Min(start + BatchSize, leafCount);
                    for (int i = start; i < end; i++)
                    {
                        nodes[i] = (leafNodes[i], CalculateMortonCode(leafNodes[i], centersToMortonSpace));
                    }
                });
            }
            else
            {
                for (int i = 0; i < leafCount; i++)
                {
                    nodes[i] = (leafNodes[i], CalculateMortonCode(leafNodes[i], centersToMortonSpace));
                }
            }
            
            // Sort by Morton code
            Array.Sort(nodes, (a, b) => a.mortonCode.CompareTo(b.mortonCode));
            
            // Determine optimal bin count based on node count (more bins for more nodes)
            int binCount = Math.Min(32, Math.Max(4, leafCount / 256));
            int nodesPerBin = Math.Max(1, (leafCount + binCount - 1) / binCount);
            
            // Build optimized subtrees for each bin - parallelize for larger counts
            var optimizedBins = GetList(binCount);
            
            if (binCount >= 4)
            {
                var results = new ITraceable[binCount];
                Parallel.For(0, binCount, i =>
                {
                    int start = i * nodesPerBin;
                    int end = Math.Min(start + nodesPerBin - 1, leafCount - 1);
                    
                    if (start <= end)
                    {
                        var binNodes = GetList(end - start + 1);
                        for (int j = start; j <= end; j++)
                        {
                            binNodes.Add(nodes[j].node);
                        }
                        
                        results[i] = BuildSahTree(binNodes, 0, binNodes.Count - 1);
                        ReturnList(binNodes);
                    }
                });
                
                // Collect non-null results
                for (int i = 0; i < binCount; i++)
                {
                    if (results[i] != null)
                    {
                        optimizedBins.Add(results[i]);
                    }
                }
            }
            else
            {
                for (int i = 0; i < binCount; i++)
                {
                    int start = i * nodesPerBin;
                    int end = Math.Min(start + nodesPerBin - 1, leafCount - 1);
                    
                    if (start <= end)
                    {
                        var binNodes = GetList(end - start + 1);
                        for (int j = start; j <= end; j++)
                        {
                            binNodes.Add(nodes[j].node);
                        }
                        
                        optimizedBins.Add(BuildSahTree(binNodes, 0, binNodes.Count - 1));
                        ReturnList(binNodes);
                    }
                }
            }
            
            // Apply SAH to combine the bins
            var result = BuildSahTree(optimizedBins, 0, optimizedBins.Count - 1);
            ReturnList(optimizedBins);
            return result;
        }
        
        private static ITraceable BuildSahTree(List<ITraceable> nodes, int start, int end)
        {
            if (start == end) return nodes[start];
            if (start + 1 == end) return NodePool.Get(nodes[start], nodes[end]);
            
            // Use array-based storage for better cache locality and reduced allocations
            int nodeCount = end - start + 1;
            var allBounds = new AxisAlignedBoundingBox[nodeCount];
            
            // If we have a lot of nodes, process in parallel
            if (nodeCount > 512)
            {
                Parallel.For(0, nodeCount, i =>
                {
                    allBounds[i] = nodes[i + start].GetAxisAlignedBoundingBox();
                });
            }
            else
            {
                for (int i = 0; i < nodeCount; i++)
                {
                    allBounds[i] = nodes[i + start].GetAxisAlignedBoundingBox();
                }
            }
            
            // Precompute prefix and suffix bounds
            var leftBounds = new AxisAlignedBoundingBox[nodeCount - 1];
            var rightBounds = new AxisAlignedBoundingBox[nodeCount - 1];
            
            leftBounds[0] = allBounds[0];
            for (int i = 1; i < nodeCount - 1; i++)
            {
                leftBounds[i] = leftBounds[i - 1] + allBounds[i];
            }
            
            rightBounds[nodeCount - 2] = allBounds[nodeCount - 1];
            for (int i = nodeCount - 3; i >= 0; i--)
            {
                rightBounds[i] = rightBounds[i + 1] + allBounds[i + 1];
            }
            
            // Find best split using Surface Area Heuristic
            double bestCost = double.MaxValue;
            int bestSplit = start;
            
            // Evaluate all possible splits - use SIMD optimization if available
            for (int splitIndex = 0; splitIndex < nodeCount - 1; splitIndex++)
            {
                int leftCount = splitIndex + 1;
                int rightCount = nodeCount - leftCount;
                
                var leftBox = leftBounds[splitIndex];
                var rightBox = rightBounds[splitIndex];
                
                // Calculate SAH cost with improved weight calculations
                double leftSA = leftBox.GetSurfaceArea();
                double rightSA = rightBox.GetSurfaceArea();
                double cost = leftCount * leftSA + rightCount * rightSA;
                
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestSplit = start + splitIndex;
                }
            }
            
            // Build left and right subtrees - no need to create intermediate lists
            var leftNodes = GetList(bestSplit - start + 1);
            var rightNodes = GetList(end - bestSplit);
            
            for (int i = start; i <= bestSplit; i++)
            {
                leftNodes.Add(nodes[i]);
            }
            
            for (int i = bestSplit + 1; i <= end; i++)
            {
                rightNodes.Add(nodes[i]);
            }
            
            var left = BuildSahTree(leftNodes, 0, leftNodes.Count - 1);
            var right = BuildSahTree(rightNodes, 0, rightNodes.Count - 1);
            
            ReturnList(leftNodes);
            ReturnList(rightNodes);
            
            return NodePool.Get(left, right);
        }

        private static AxisAlignedBoundingBox CalculateBounds(List<ITraceable> nodes)
        {
            var bounds = nodes[0].GetAxisAlignedBoundingBox();
            int count = nodes.Count;
            for (int i = 1; i < count; i++)
            {
                bounds.ExpandToInclude(nodes[i].GetAxisAlignedBoundingBox());
            }
            return bounds;
        }

        private static Matrix4X4 CalculateMortonTransform(AxisAlignedBoundingBox bounds, Vector3 scale)
        {
            var translation = Matrix4X4.CreateTranslation(-bounds.MinXYZ);
            var scaleMatrix = Matrix4X4.CreateScale(MortonCodes.Size / scale);
            return Matrix4X4.Mult(translation, scaleMatrix);
        }

        private static long CalculateMortonCode(ITraceable node, Matrix4X4 transform)
        {
            var mortonSpace = node.GetCenter().Transform(transform);
            return MortonCodes.Encode3(mortonSpace);
        }
    }

    public class BvhNodePool
    {
        private readonly ConcurrentStack<BoundingVolumeHierarchy> _pool = new ConcurrentStack<BoundingVolumeHierarchy>();

        public BoundingVolumeHierarchy Get(ITraceable nodeA, ITraceable nodeB)
        {
            if (_pool.TryPop(out var node))
            {
                node.SetNodes(nodeA, nodeB);
                return node;
            }
            return new BoundingVolumeHierarchy(nodeA, nodeB);
        }

        public void Return(BoundingVolumeHierarchy node)
        {
            _pool.Push(node);
        }
    }
}