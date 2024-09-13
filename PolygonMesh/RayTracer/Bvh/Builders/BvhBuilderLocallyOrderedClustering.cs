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
using System.Threading.Tasks;

namespace MatterHackers.RayTracer
{
    public class BvhBuilderLocallyOrderedClustering
    {
        private const int MinNodesForParallel = 1000;

        private static readonly BvhNodePool NodePool = new BvhNodePool();

        public static ITraceable Create(List<ITraceable> sourceNodes)
        {
            if (sourceNodes.Count == 0) return null;
            if (sourceNodes.Count == 1) return sourceNodes[0];

            var bounds = CalculateBounds(sourceNodes);
            var scale = Vector3.Max(bounds.Size, Vector3.One) * 1.001;
            var centersToMortonSpace = CalculateMortonTransform(bounds, scale);

            var nodes = new (ITraceable node, long mortonCode)[sourceNodes.Count];
            Parallel.For(0, sourceNodes.Count, i =>
            {
                nodes[i] = (sourceNodes[i], CalculateMortonCode(sourceNodes[i], centersToMortonSpace));
            });

            RadixSort(nodes);

            return BuildTreeParallel(nodes, 0, nodes.Length - 1);
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

                for (int i = 0; i < a.Length; i++)
                {
                    count[(a[i].mortonCode >> shift) & mask]++;
                }

                pref[0] = 0;
                for (int i = 1; i < count.Length; i++)
                {
                    pref[i] = pref[i - 1] + count[i - 1];
                }

                for (int i = 0; i < a.Length; i++)
                {
                    int index = (int)((a[i].mortonCode >> shift) & mask);
                    t[pref[index]++] = a[i];
                }

                Array.Copy(t, a, a.Length);
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
            if (start == end) return nodes[start].node;

            int split = FindBestSplit(nodes, start, end);

            var left = BuildTreeRecursive(nodes, start, split);
            var right = BuildTreeRecursive(nodes, split + 1, end);

            return NodePool.Get(left, right);
        }

        private static int FindBestSplit((ITraceable node, long mortonCode)[] nodes, int start, int end)
        {
            if (end - start <= 1) return start;

            int commonPrefix = System.Numerics.BitOperations.LeadingZeroCount((ulong)(nodes[start].mortonCode ^ nodes[end].mortonCode)) - 64;

            int split = start;
            int step = end - start;
            do
            {
                step = (step + 1) >> 1;
                int newSplit = split + step;
                if (newSplit < end)
                {
                    int splitPrefix = System.Numerics.BitOperations.LeadingZeroCount((ulong)(nodes[start].mortonCode ^ nodes[newSplit].mortonCode)) - 64;
                    if (splitPrefix > commonPrefix)
                        split = newSplit;
                }
            } while (step > 1);

            return split;
        }

        private static AxisAlignedBoundingBox CalculateBounds(List<ITraceable> nodes)
        {
            var bounds = nodes[0].GetAxisAlignedBoundingBox();
            for (int i = 1; i < nodes.Count; i++)
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
        private readonly Stack<BoundingVolumeHierarchy> _pool = new Stack<BoundingVolumeHierarchy>();

        public BoundingVolumeHierarchy Get(ITraceable nodeA, ITraceable nodeB)
        {
            if (_pool.Count > 0)
            {
                var node = _pool.Pop();
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