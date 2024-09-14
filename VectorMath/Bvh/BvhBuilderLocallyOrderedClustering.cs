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
using System.Threading.Tasks;

namespace MatterHackers.VectorMath.Bvh
{
    public class BvhBuilderLocallyOrderedClustering
    {
        private const int MinNodesForParallel = 1000;

        public class BvhNode
        {
            public int Left { get; set; }
            public int Right { get; set; }
            public bool IsLeaf => Left == Right;
        }

        private struct NodeInfo
        {
            public Vector3 Center;
            public long MortonCode;
            public int OriginalIndex;
        }

        public static BvhNode[] BuildBvh(List<AxisAlignedBoundingBox> aabbs)
        {
            if (aabbs.Count == 0) return Array.Empty<BvhNode>();
            if (aabbs.Count == 1) return new[] { new BvhNode { Left = 0, Right = 0 } };

            var bounds = CalculateBounds(aabbs);
            var scale = Vector3.Max(bounds.Size, Vector3.One) * 1.001;
            var centersToMortonSpace = CalculateMortonTransform(bounds, scale);

            var nodeInfos = new NodeInfo[aabbs.Count];
            Parallel.For(0, aabbs.Count, i =>
            {
                var center = aabbs[i].Center;
                nodeInfos[i] = new NodeInfo
                {
                    Center = center,
                    MortonCode = CalculateMortonCode(center, centersToMortonSpace),
                    OriginalIndex = i
                };
            });

            Array.Sort(nodeInfos, (a, b) => a.MortonCode.CompareTo(b.MortonCode));

            var nodes = new BvhNode[aabbs.Count * 2 - 1];
            BuildTreeParallel(nodeInfos, 0, nodeInfos.Length - 1, nodes, 0);

            return nodes;
        }

        private static int BuildTreeParallel(NodeInfo[] nodeInfos, int start, int end, BvhNode[] nodes, int nodeIndex)
        {
            if (start == end)
            {
                nodes[nodeIndex] = new BvhNode { Left = nodeInfos[start].OriginalIndex, Right = nodeInfos[start].OriginalIndex };
                return nodeIndex;
            }

            if (end - start < MinNodesForParallel)
            {
                return BuildTreeRecursive(nodeInfos, start, end, nodes, nodeIndex);
            }

            int split = FindBestSplit(nodeInfos, start, end);

            int leftChild = nodeIndex + 1;
            int rightChild;

            Parallel.Invoke(
                () => BuildTreeParallel(nodeInfos, start, split, nodes, leftChild),
                () => rightChild = BuildTreeParallel(nodeInfos, split + 1, end, nodes, nodeIndex + 2 * (split - start + 1))
            );

            rightChild = nodeIndex + 2 * (split - start + 1);
            nodes[nodeIndex] = new BvhNode { Left = leftChild, Right = rightChild };

            return nodeIndex;
        }

        private static int BuildTreeRecursive(NodeInfo[] nodeInfos, int start, int end, BvhNode[] nodes, int nodeIndex)
        {
            if (start == end)
            {
                nodes[nodeIndex] = new BvhNode { Left = nodeInfos[start].OriginalIndex, Right = nodeInfos[start].OriginalIndex };
                return nodeIndex;
            }

            int split = FindBestSplit(nodeInfos, start, end);

            int leftChild = BuildTreeRecursive(nodeInfos, start, split, nodes, nodeIndex + 1);
            int rightChild = BuildTreeRecursive(nodeInfos, split + 1, end, nodes, nodeIndex + 2 * (split - start + 1));

            nodes[nodeIndex] = new BvhNode { Left = leftChild, Right = rightChild };

            return nodeIndex;
        }

        private static int FindBestSplit(NodeInfo[] nodeInfos, int start, int end)
        {
            if (end - start <= 1) return start;

            int commonPrefix = System.Numerics.BitOperations.LeadingZeroCount((ulong)(nodeInfos[start].MortonCode ^ nodeInfos[end].MortonCode)) - 64;

            int split = start;
            int step = end - start;
            do
            {
                step = (step + 1) >> 1;
                int newSplit = split + step;
                if (newSplit < end)
                {
                    int splitPrefix = System.Numerics.BitOperations.LeadingZeroCount((ulong)(nodeInfos[start].MortonCode ^ nodeInfos[newSplit].MortonCode)) - 64;
                    if (splitPrefix > commonPrefix)
                        split = newSplit;
                }
            } while (step > 1);

            return split;
        }

        private static AxisAlignedBoundingBox CalculateBounds(List<AxisAlignedBoundingBox> aabbs)
        {
            var bounds = aabbs[0];
            for (int i = 1; i < aabbs.Count; i++)
            {
                bounds.ExpandToInclude(aabbs[i]);
            }
            return bounds;
        }

        private static Matrix4X4 CalculateMortonTransform(AxisAlignedBoundingBox bounds, Vector3 scale)
        {
            var translation = Matrix4X4.CreateTranslation(-bounds.MinXYZ);
            var scaleMatrix = Matrix4X4.CreateScale(MortonCodes.Size / scale);
            return Matrix4X4.Mult(translation, scaleMatrix);
        }

        private static long CalculateMortonCode(Vector3 center, Matrix4X4 transform)
        {
            var mortonSpace = center.Transform(transform);
            return MortonCodes.Encode3(mortonSpace);
        }
    }
}