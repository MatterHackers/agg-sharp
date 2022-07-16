/*
Copyright (c) 2020, Lars Brubaker
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
using System.Threading.Tasks;

namespace MatterHackers.Agg
{
    public static class Parallel
    {
        public static bool Sequential { get; set; }

        public static void ForEach<T>(IEnumerable<T> source, Action<T> action)
        {
            if (Sequential)
            {
                foreach (T v in source)
                {
                    action(v);
                }
            }
            else
            {
                System.Threading.Tasks.Parallel.ForEach<T>(source, action);
            }
        }

        public static void For(int startInclusive, int endExclusive, Action<int> action)
        {
            if (Sequential)
            {
                for (int i = startInclusive; i < endExclusive; i++)
                {
                    action(i);
                }
            }
            else
            {
                System.Threading.Tasks.Parallel.For(startInclusive, endExclusive, action);
            }
        }

        public static void For<T>(int startInclusive, int endExclusive, Func<T> action, Func<int, ParallelLoopState, T, T> p2, Action<T> p3)
        {
            System.Threading.Tasks.Parallel.For(startInclusive, endExclusive, action, p2, p3);
        }
    }
}
