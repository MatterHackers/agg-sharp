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

using System;
using System.Collections.Generic;
using Node = MatterHackers.RayTracerNS.ITraceable;
using Scalar = System.Double;

namespace MatterHackers.RayTracerNS
{
#if false
    public class BvhBuilderLocallyOrderedClustering
    {
        /// Parameter of the algorithm. The larger the search radius,
        /// the longer the search for neighboring nodes lasts.
        int search_radius = 14;

        (int, int) search_range(int i, int begin, int end)
        {
            return (i > begin + search_radius ? i - search_radius : begin, Math.Min(i + search_radius + 1, end));
        }

        (int, int) cluster(List<ITraceable> input,
            Node output,
            int neighbors,
            int merged_index,
            int begin,
            int end,
            int previous_end)
        {
            int next_begin = 0;
            int next_end = 0;

            {
                int thread_count = 1;// bvh::get_thread_count();
                int thread_id = 0; // bvh::get_thread_id();
                int chunk_size = (end - begin) / thread_count;
                int chunk_begin = begin + thread_id * chunk_size;
                int chunk_end = thread_id != thread_count - 1 ? chunk_begin + chunk_size : end;

                var distances = new Scalar[((search_radius + 1) * search_radius)];
                var distance_matrix = new List<Scalar[]>(search_radius + 1);
                for (int i = 0; i <= search_radius; ++i)
                {
                    distance_matrix[i] = distances[i * search_radius];
                }

                // Initialize the distance matrix, which caches the distances between
                // neighboring nodes in the array. A brute force approach that recomputes the
                // distances for every neighbor can be implemented without a distance matrix,
                // but would be slower for larger search radii.
                for (int i = search_range(chunk_begin, begin, end).Item1; i < chunk_begin; ++i)
                {
                    int search_end = search_range(i, begin, end).Item2;
                    for (int j = i + 1; j < search_end; ++j)
                    {
                        distance_matrix[chunk_begin - i][j - i - 1] = input[i]
                            .bounding_box_proxy()
                            .to_bounding_box()
                            .extend(input[j].bounding_box_proxy())
                            .half_area();
                    }
                }

                // Nearest neighbor search
                for (int i = chunk_begin; i < chunk_end; i++)
                {
                    int[search_begin, search_end] = search_range(i, begin, end);
                    Scalar best_distance = std::numeric_limits < Scalar >::max();
                    int best_neighbor = -1;

                    // Backward search (using the previously-computed distances stored in the distance matrix)
                    for (int j = search_begin; j < i; ++j)
                    {
                        int distance = distance_matrix[i - j][i - j - 1];
                        if (distance < best_distance)
                        {
                            best_distance = distance;
                            best_neighbor = j;
                        }
                    }

                    // Forward search (caching computed distances in the distance matrix)
                    for (int j = i + 1; j < search_end; ++j)
                    {
                        int distance = input[i]
            .bounding_box_proxy()
            .to_bounding_box()
            .extend(input[j].bounding_box_proxy())
            .half_area();
                        distance_matrix[0][j - i - 1] = distance;
                        if (distance < best_distance)
                        {
                            best_distance = distance;
                            best_neighbor = j;
                        }
                    }

                    assert(best_neighbor != int(-1));
                    neighbors[i] = best_neighbor;

                    // Rotate the distance matrix columns
                    int last = distance_matrix[search_radius];
                    std::move_backward(
                        distance_matrix.get(),
                        distance_matrix.get() + search_radius,
                        distance_matrix.get() + search_radius + 1);
                    distance_matrix[0] = last;
                }

                // Mark nodes that are the closest as merged, but keep
                // the one with lowest index to act as the parent
                for (int i = begin; i < end; ++i)
                {
                    int j = neighbors[i];
                    bool is_mergeable = neighbors[j] == i;
                    merged_index[i] = i < j && is_mergeable ? 1 : 0;
                }

                // Perform a prefix sum to compute the insertion indices
                prefix_sum.sum_in_parallel(merged_index + begin, merged_index + begin, end - begin);
                int merged_count = merged_index[end - 1];
                int unmerged_count = end - begin - merged_count;
                int children_count = merged_count * 2;
                int children_begin = end - children_count;
                int unmerged_begin = end - (children_count + unmerged_count);

                {
                    next_begin = unmerged_begin;
                    next_end = children_begin;
                }

                // Finally, merge nodes that are marked for merging and create
                // their parents using the indices computed previously.
                for (int i = begin; i < end; ++i)
                {
                    int j = neighbors[i];
                    if (neighbors[j] == i)
                    {
                        if (i < j)
                        {
                            int &unmerged_node = output[unmerged_begin + j - begin - merged_index[j]];
                            int first_child = children_begin + (merged_index[i] - 1) * 2;
                            unmerged_node.bounding_box_proxy() = input[j]
                                .bounding_box_proxy()
                                .to_bounding_box()
                                .extend(input[i].bounding_box_proxy());
                            unmerged_node.primitive_count = 0;
                            unmerged_node.first_child_or_primitive = first_child;
                            output[first_child + 0] = input[i];
                            output[first_child + 1] = input[j];
                        }
                    }
                    else
                    {
                        output[unmerged_begin + i - begin - merged_index[i]] = input[i];
                    }
                }

                // Copy the nodes of the previous level into the current array of nodes.
                for (int i = end; i < previous_end; ++i)
                    output[i] = input[i];
            }

            return (next_begin, next_end);
        }
    }

    public class RadixSort<Key, Value>
    {
        // A function to do counting sort of arr[] according to
        // the digit represented by exp.
        public static void CountSort(int[] arr, int n, int exp)
        {
            int[] output = new int[n]; // output array
            int i;
            int[] count = new int[10];

            // initializing all elements of count to 0
            for (i = 0; i < 10; i++)
            {
                count[i] = 0;
            }

            // Store count of occurrences in count[]
            for (i = 0; i < n; i++)
            {
                count[(arr[i] / exp) % 10]++;
            }

            // Change count[i] so that count[i] now contains
            // actual
            //  position of this digit in output[]
            for (i = 1; i < 10; i++)
            {
                count[i] += count[i - 1];
            }

            // Build the output array
            for (i = n - 1; i >= 0; i--)
            {
                output[count[(arr[i] / exp) % 10] - 1] = arr[i];
                count[(arr[i] / exp) % 10]--;
            }

            // Copy the output array to arr[], so that arr[] now
            // contains sorted numbers according to current
            // digit
            for (i = 0; i < n; i++)
            {
                arr[i] = output[i];
            }
        }

        public static int GetMax(int[] arr, int n)
        {
            int mx = arr[0];
            for (int i = 1; i < n; i++)
            {
                if (arr[i] > mx)
                {
                    mx = arr[i];
                }
            }

            return mx;
        }

        // The main function to that sorts arr[] of size n using
        // Radix Sort
        public static void Radixsort(int[] arr, int n)
        {
            // Find the maximum number to know number of digits
            int m = GetMax(arr, n);

            // Do counting sort for every digit. Note that
            // instead of passing digit number, exp is passed.
            // exp is 10^i where i is current digit number
            for (int exp = 1; m / exp > 0; exp *= 10)
            {
                CountSort(arr, n, exp);
            }
        }
    }
#endif
}