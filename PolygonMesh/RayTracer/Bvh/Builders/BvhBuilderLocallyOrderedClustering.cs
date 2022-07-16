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

using MatterHackers.Agg;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.RayTracer
{
    public class BvhBuilderLocallyOrderedClustering
    {
        public static void RadixSort((ITraceable node, long mortonCode)[] a)
        {
            // our helper array 
            var t = new (ITraceable node, long mortonCode)[a.Length];

            // number of bits our group will be long 
            int r = 4; // try to set this also to 2, 8 or 16 to see if it is 
                       // quicker or not 

            // number of bits of a C# int 
            int b = 32;

            // counting and prefix arrays
            // (note dimensions 2^r which is the number of all possible values of a 
            // r-bit number) 
            int[] count = new int[1 << r];
            int[] pref = new int[1 << r];

            // number of groups 
            int groups = (int)Math.Ceiling((double)b / (double)r);

            // the mask to identify groups 
            int mask = (1 << r) - 1;

            // the algorithm: 
            for (int c = 0, shift = 0; c < groups; c++, shift += r)
            {
                // reset count array 
                for (int j = 0; j < count.Length; j++)
                {
                    count[j] = 0;
                }

                // counting elements of the c-th group 
                for (int i = 0; i < a.Length; i++)
                {
                    count[(a[i].mortonCode >> shift) & mask]++;
                }

                // calculating prefixes 
                pref[0] = 0;
                for (int i = 1; i < count.Length; i++)
                {
                    pref[i] = pref[i - 1] + count[i - 1];
                }

                // from a[] to t[] elements ordered by c-th group 
                for (int i = 0; i < a.Length; i++)
                {
                    t[pref[(a[i].mortonCode >> shift) & mask]++] = a[i];
                }

                // a[]=t[] and start again until the last group 
                t.CopyTo(a, 0);
            }
            // a is sorted 
        }
        
        /// <summary>
        /// Create a balanced BvhTree from the nodes
        /// </summary>
        /// <param name="nodes">The input nodes</param>
        /// <param name="checkDistance">The distance before and after a given morton index to look for nearest matches</param>
        /// <returns>The top of a new balanced tree</returns>
        public static ITraceable Create(List<ITraceable> sourceNodes, int checkDistance = 6)
        {
            if (sourceNodes.Count == 0)
            {
                return null;
            }

            if (sourceNodes.Count == 1)
            {
                return sourceNodes[0];
            }

            Parallel.Sequential = false;
 
            // get the bounds of all the nodes
            var bounds = AxisAlignedBoundingBox.Empty();
            foreach (var node in sourceNodes)
            {
                bounds.ExpandToInclude(node.GetCenter());
            }

            var scale = Vector3.ComponentMax(bounds.Size) + 2;
            // translate to all positive numbers
            var centersToMortonSpace = Matrix4X4.CreateTranslation(-bounds.MinXYZ);
            // scale to the morton space size
            centersToMortonSpace *= Matrix4X4.CreateScale(MortonCodes.Size / scale);

            var inputNodes = new List<(ITraceable node, long mortonCode)>(sourceNodes.Count);
            var outputNodes = new List<(ITraceable node, long mortonCode)>(sourceNodes.Count);

            long GetMortonCode(ITraceable node)
            {
                var mortonSpace = node.GetCenter().Transform(centersToMortonSpace);
                return MortonCodes.Encode3(mortonSpace);
            }

            // add all the does after creating their morton codes for the center position
            for (int i = 0; i < sourceNodes.Count; i++)
            {
                inputNodes.Add((sourceNodes[i], GetMortonCode(sourceNodes[i])));
            }

            // sort them
            var inputNodesSorted = inputNodes.ToArray();
            RadixSort(inputNodesSorted);
            inputNodes = inputNodesSorted.ToList();

            var bestNodeToMerge = new List<int>(new int[inputNodes.Count]);

            // we will need to keep track of nodes for removal
            var markedForRemoval = new List<bool>(new bool[inputNodes.Count]);

            while (inputNodes.Count > 1)
            {
                // find the nerest point for every point minimizing the surface area of what would be the enclosing aabb
                // Parallel.For(0, inputNodes.Count, (index) =>
                for(var index = 0; index < inputNodes.Count; index++)
                {
                    var minSurfaceArea = double.MaxValue;
                    var minIndex = -1;

                    var node = inputNodes[index].node;
                    var nodeBounds = node.GetAxisAlignedBoundingBox();

                    void TestForMinSurfaceArea(int i)
                    {
                        var testNode = inputNodes[i].node;
                        var testBounds = testNode.GetAxisAlignedBoundingBox() + nodeBounds;
                        var surfaceArea = testBounds.GetSurfaceArea();
                        if (surfaceArea < minSurfaceArea)
                        {
                            minSurfaceArea = surfaceArea;
                            minIndex = i;
                        }
                    }

                    // check the checkDistance behind
                    var start = Math.Max(0, index - checkDistance);
                    for (int i = start; i < index; i++)
                    {
                        TestForMinSurfaceArea(i);
                    }

                    // check the checkDistance in front
                    var end = Math.Min(inputNodes.Count, index + checkDistance);
                    for (int i = index + 1; i < end; i++)
                    {
                        TestForMinSurfaceArea(i);
                    }

                    // save the best index
                    bestNodeToMerge[index] = minIndex;
                }// );

                // clear the markedForRemoval
                for (int i = 0; i < inputNodes.Count; i++)
                {
                    markedForRemoval[i] = false;
                }

                // find all the nodes that agree on merging with eachother
                for (int i = 0; i < inputNodes.Count; i++)
                {
                    if (markedForRemoval[i])
                    {
                        continue;
                    }

                    var nodeToMergeWith = bestNodeToMerge[i];
                    // if the node we want to merge with wants to merge with us
                    if (bestNodeToMerge[nodeToMergeWith] == i)
                    {
                        // create a new node that is the merge
                        var newNode = new BoundingVolumeHierarchy(inputNodes[i].node, inputNodes[nodeToMergeWith].node);

                        // replace the first node with the new node
                        // nodesToAdd.Add((newNode, GetMortonCode(newNode)));
                        inputNodes[i] = (newNode, inputNodes[i].mortonCode);

                        // remember the nodes needs to be removed
                        markedForRemoval[nodeToMergeWith] = true;
                    }
                }

                // iterate over the inputNodes moving all the un-marked nodes to the outputNodes
                outputNodes.Clear();
                for (int i = 0; i < inputNodes.Count; i++)
                {
                    if (!markedForRemoval[i])
                    {
                        outputNodes.Add(inputNodes[i]);
                    }
                }

                // swap the input and output Nodes
                var temp = inputNodes;
                inputNodes = outputNodes;
                outputNodes = temp;
                checkDistance = 20;
                
                // continue until all nodes have been merged
            }

            return inputNodes[0].node;
        }
    }
}