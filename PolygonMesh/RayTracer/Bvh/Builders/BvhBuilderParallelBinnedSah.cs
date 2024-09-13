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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.RayTracer
{
    public static class BvhBuilderParallelBinnedSah
    {
        private const int MinBins = 8;
        private const int MaxBins = 64;
        private const int MaxPrimitivesPerLeaf = 4;
        private const int MaxParallelDepth = 4;

        public static ITraceable Create(List<ITraceable> traceableItems)
        {
            try
            {
                AnalyzeInputData(traceableItems);

                //Console.WriteLine($"Starting BVH creation with {traceableItems.Count} items");

                if (traceableItems == null || traceableItems.Count == 0)
                {
                    //Console.WriteLine("No items to process");
                    return null;
                }

                var items = traceableItems.ToArray();
                var result = BuildBvh(items, 0, items.Length, 0);

                //Console.WriteLine("BVH creation completed successfully");
                return result;
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Error during BVH creation: {ex.Message}");
                //Console.WriteLine(ex.StackTrace);
                throw;
            }
        }

        private static void AnalyzeInputData(List<ITraceable> items)
        {
            var bounds = new AxisAlignedBoundingBox();
            foreach (var item in items)
            {
                bounds += item.GetAxisAlignedBoundingBox();
            }

            //Console.WriteLine($"Total bounds: Min={bounds.MinXYZ}, Max={bounds.MaxXYZ}");
            //Console.WriteLine($"Extent: {bounds.MaxXYZ - bounds.MinXYZ}");

            // Check for degenerate triangles
            int degenerateCount = items.Count(item => item.GetAxisAlignedBoundingBox().GetVolume() < 1e-10);
            //Console.WriteLine($"Degenerate triangles: {degenerateCount}");
        }

        private static ITraceable BuildBvh(ITraceable[] items, int start, int end, int depth)
        {
            int numItems = end - start;

            //Console.WriteLine($"BuildBvh: depth={depth}, items={numItems}");

            if (depth > 1000 || numItems <= MaxPrimitivesPerLeaf)
            {
                return new UnboundCollection(items.Skip(start).Take(numItems).ToList());
            }

            // Base cases
            if (numItems <= 0)
            {
                return null;
            }

            if (numItems <= MaxPrimitivesPerLeaf)
            {
                // Create a leaf node with the remaining primitives
                var leafItems = new List<ITraceable>();
                for (int i = start; i < end; i++)
                {
                    leafItems.Add(items[i]);
                }
                return new UnboundCollection(leafItems);
            }

            // Compute bounding box for this node
            AxisAlignedBoundingBox nodeBounds = ComputeBounds(items, start, end);

            // Choose split axis based on bounding box extent
            int splitAxis = nodeBounds.GetLargestAxis();

            // Use binned SAH to find the best split position
            double splitPosition = FindSplitPosition(items, start, end, splitAxis, nodeBounds);

            // Partition the items in place based on the split position
            int mid = Partition(items, start, end, splitAxis, splitPosition);

            // Recursively build left and right subtrees
            ITraceable leftGroup = null;
            ITraceable rightGroup = null;

            if (depth < MaxParallelDepth)
            {
                object lockObject = new object();
                leftGroup = null;
                rightGroup = null;

                Parallel.Invoke(
                    () => {
                        var left = BuildBvh(items, start, mid, depth + 1);
                        lock (lockObject) { leftGroup = left; }
                    },
                    () => {
                        var right = BuildBvh(items, mid, end, depth + 1);
                        lock (lockObject) { rightGroup = right; }
                    }
                );

                // Ensure both groups are assigned
                if (leftGroup == null || rightGroup == null)
                {
                    throw new InvalidOperationException("Parallel BVH construction failed to assign both child nodes.");
                }
            }
            else
            {
                // Sequential execution for deeper levels
                leftGroup = BuildBvh(items, start, mid, depth + 1);
                rightGroup = BuildBvh(items, mid, end, depth + 1);
            }

            //Console.WriteLine($"Split at axis {splitAxis}, position {splitPosition}");
            //Console.WriteLine($"Left child: {start}-{mid}, Right child: {mid}-{end}");

            // Create and return the BVH node
            return new BoundingVolumeHierarchy(leftGroup, rightGroup);
        }

        private static AxisAlignedBoundingBox ComputeBounds(ITraceable[] items, int start, int end)
        {
            AxisAlignedBoundingBox bounds = items[start].GetAxisAlignedBoundingBox();
            for (int i = start + 1; i < end; i++)
            {
                bounds += items[i].GetAxisAlignedBoundingBox();
            }
            return bounds;
        }

        private static double FindSplitPosition(ITraceable[] items, int start, int end, int axis, AxisAlignedBoundingBox bounds)
        {
            int binCount = GetAdaptiveBinCount(end - start);
            var bins = new Bin[binCount];
            for (int i = 0; i < binCount; i++)
            {
                bins[i] = new Bin();
            }

            double minBound = bounds.MinXYZ[axis];
            double maxBound = bounds.MaxXYZ[axis];
            double binSize = (maxBound - minBound) / binCount;

            if (binSize <= 0)
            {
                return minBound + (maxBound - minBound) * 0.5;
            }

            for (int i = start; i < end; i++)
            {
                double center = items[i].GetCenter()[axis];
                int binIndex = (int)((center - minBound) / binSize);
                binIndex = Math.Min(binIndex, binCount - 1);
                bins[binIndex].Add(items[i]);
            }

            var leftBounds = new AxisAlignedBoundingBox[binCount];
            var rightBounds = new AxisAlignedBoundingBox[binCount];
            int[] leftCounts = new int[binCount];
            int[] rightCounts = new int[binCount];

            ComputePrefixSums(bins, leftBounds, rightBounds, leftCounts, rightCounts);

            double minCost = double.MaxValue;
            int minCostSplit = -1;
            double totalSurfaceArea = bounds.GetSurfaceArea();
            double traversalCost = EstimateTraversalCost();
            double intersectionCost = EstimateIntersectionCost();

            for (int i = 0; i < binCount - 1; i++)
            {
                int countLeft = leftCounts[i];
                int countRight = rightCounts[i + 1];
                if (countLeft == 0 || countRight == 0)
                {
                    continue;
                }

                double leftArea = leftBounds[i].GetSurfaceArea();
                double rightArea = rightBounds[i + 1].GetSurfaceArea();

                double cost = traversalCost +
                              (leftArea / totalSurfaceArea) * countLeft * intersectionCost +
                              (rightArea / totalSurfaceArea) * countRight * intersectionCost;

                if (cost < minCost)
                {
                    minCost = cost;
                    minCostSplit = i;
                }
            }

            if (minCostSplit == -1 || leftCounts[minCostSplit] == 0 || rightCounts[minCostSplit + 1] == 0)
            {
                return MedianSplit(items, start, end, axis);
            }

            return minBound + binSize * (minCostSplit + 1);
        }

        private static int GetAdaptiveBinCount(int primitiveCount)
        {
            int binCount = (int)(Math.Log(primitiveCount) / Math.Log(2)) * 2;
            return Math.Max(MinBins, Math.Min(MaxBins, binCount));
        }

        private static void ComputePrefixSums(Bin[] bins, AxisAlignedBoundingBox[] leftBounds, AxisAlignedBoundingBox[] rightBounds, int[] leftCounts, int[] rightCounts)
        {
            AxisAlignedBoundingBox leftAccumBounds = new AxisAlignedBoundingBox();
            int leftAccumCount = 0;
            for (int i = 0; i < bins.Length; i++)
            {
                if (bins[i].Count > 0)
                {
                    foreach (var item in bins[i].Items)
                    {
                        leftAccumBounds += item.GetAxisAlignedBoundingBox();
                    }
                    leftAccumCount += bins[i].Count;
                }
                leftBounds[i] = leftAccumBounds;
                leftCounts[i] = leftAccumCount;
            }

            AxisAlignedBoundingBox rightAccumBounds = new AxisAlignedBoundingBox();
            int rightAccumCount = 0;
            for (int i = bins.Length - 1; i >= 0; i--)
            {
                if (bins[i].Count > 0)
                {
                    foreach (var item in bins[i].Items)
                    {
                        rightAccumBounds += item.GetAxisAlignedBoundingBox();
                    }
                    rightAccumCount += bins[i].Count;
                }
                rightBounds[i] = rightAccumBounds;
                rightCounts[i] = rightAccumCount;
            }
        }

        private static double EstimateTraversalCost()
        {
            // This value might need tuning based on your specific hardware and scene complexity
            return 1.0;
        }

        private static double EstimateIntersectionCost()
        {
            // This value might need tuning based on your specific hardware and scene complexity
            return 1.5;
        }

        private static double MedianSplit(ITraceable[] items, int start, int end, int axis)
        {
            int count = end - start;
            if (count % 2 == 0)
            {
                return (items[start + count / 2 - 1].GetCenter()[axis] + items[start + count / 2].GetCenter()[axis]) / 2;
            }
            else
            {
                return items[start + count / 2].GetCenter()[axis];
            }
        }

        private static int Partition(ITraceable[] items, int start, int end, int axis, double splitPosition)
        {
            int initialStart = start;
            int i = start;
            int j = end - 1;

            while (i <= j)
            {
                while (i <= j && items[i].GetCenter()[axis] <= splitPosition) i++;
                while (i <= j && items[j].GetCenter()[axis] > splitPosition) j--;
                if (i < j)
                {
                    var temp = items[i];
                    items[i] = items[j];
                    items[j] = temp;
                    i++;
                    j--;
                }
            }

            // Debug output
            //Console.WriteLine($"Partition result: Left={i - initialStart}, Right={end - i}");

            // Ensure at least one item is on each side
            if (i == initialStart) i = start + 1;
            if (i == end) i = end - 1;

            return i;
        }

        // Helper class for bins
        private class Bin
        {
            public List<ITraceable> Items { get; private set; }
            public int Count => Items.Count;

            public Bin()
            {
                Items = new List<ITraceable>();
            }

            public void Add(ITraceable item)
            {
                Items.Add(item);
            }
        }
    }
}