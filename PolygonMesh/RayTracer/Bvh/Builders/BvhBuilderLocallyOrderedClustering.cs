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

namespace MatterHackers.RayTracer
{
    public static class BvhBuilderLocallyOrderedClustering
    {

    }

    public class RadixSort
    {
#if false
        const int bits_per_iteration = 10;

    /// Performs the sort. Must be called from a parallel region.
    template<typename Key, typename Value>
    void sort_in_parallel(
        Key* bvh_restrict& keys,
        Key* bvh_restrict& keys_copy,
        Value* bvh_restrict& values,
        Value* bvh_restrict& values_copy,
        size_t count, size_t bit_count)
        {
            bvh::assert_in_parallel();

            static constexpr size_t bucket_count = 1 << bits_per_iteration;
            static constexpr Key mask = (Key(1) << bits_per_iteration) - 1;

            size_t thread_count = bvh::get_thread_count();
            size_t thread_id = bvh::get_thread_id();

            // Allocate temporary storage
#pragma omp single
            {
                size_t data_size = (thread_count + 1) * bucket_count;
                if (per_thread_data_size < data_size)
                {
                    per_thread_buckets = std::make_unique<size_t[]>(data_size);
                    per_thread_data_size = data_size;
                }
            }

            for (size_t bit = 0; bit < bit_count; bit += BitsPerIteration)
            {
                auto buckets = &per_thread_buckets[thread_id * bucket_count];
                std::fill(buckets, buckets + bucket_count, 0);

#pragma omp for schedule(static)
                for (size_t i = 0; i < count; ++i)
                    buckets[(keys[i] >> bit) & mask]++;

#pragma omp for
                for (size_t i = 0; i < bucket_count; i++)
                {
                    // Do a prefix sum of the elements in one bucket over all threads
                    size_t sum = 0;
                    for (size_t j = 0; j < thread_count; ++j)
                    {
                        size_t old_sum = sum;
                        sum += per_thread_buckets[j * bucket_count + i];
                        per_thread_buckets[j * bucket_count + i] = old_sum;
                    }
                    per_thread_buckets[thread_count * bucket_count + i] = sum;
                }

                for (size_t i = 0, sum = 0; i < bucket_count; ++i)
                {
                    size_t old_sum = sum;
                    sum += per_thread_buckets[thread_count * bucket_count + i];
                    buckets[i] += old_sum;
                }

#pragma omp for schedule(static)
                for (size_t i = 0; i < count; ++i)
                {
                    size_t j = buckets[(keys[i] >> bit) & mask]++;
                    keys_copy[j] = keys[i];
                    values_copy[j] = values[i];
                }

#pragma omp single
                {
                    std::swap(keys_copy, keys);
                    std::swap(values_copy, values);
                }
            }
        }

        /// Creates a radix sort key from a floating point value.
        template<typename T, std::enable_if_t<std::is_floating_point<T>::value, int> = 0>
    static typename SizedIntegerType<sizeof(T) * CHAR_BIT>::Unsigned make_key(T x)
        {
            using U = typename SizedIntegerType < sizeof(T) * CHAR_BIT >::Unsigned;
            auto mask = U(1) << (sizeof(T) * CHAR_BIT - 1);
            auto y = as< U > (x);
            return (y & mask ? (-y) ^ mask : y) ^ mask;
        }

        private:
    std::unique_ptr<size_t[]> per_thread_buckets;
        size_t per_thread_data_size = 0;
#endif
    }
}