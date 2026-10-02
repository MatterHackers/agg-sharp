/*
Copyright (c) 2026, Lars Brubaker
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
    /// <summary>
    /// Thin wrapper over <see cref="System.Threading.Tasks.Parallel"/> that can be globally
    /// forced onto a plain sequential path. Hosts with no thread pool (single threaded wasm)
    /// set <see cref="Sequential"/> once at startup; everything routed through here then runs
    /// in-line. Lives in VectorMath rather than Agg so the lowest-level projects
    /// (VectorMath, geometry3Sharp) can share the one flag.
    /// </summary>
    public static class Parallel
    {
        [ThreadStatic]
        private static bool runInlineOnThisThread;

        public static bool Sequential { get; set; }

        /// <summary>
        /// True when parallel calls made from the calling thread run entirely on that thread. Set by
        /// <see cref="RunInlineOnCurrentThread"/>; per thread, and stays set for the thread's lifetime.
        /// </summary>
        public static bool CurrentThreadRunsInline => runInlineOnThisThread;

        /// <summary>
        /// Marks the calling thread so every loop routed through this class from it runs on it alone,
        /// queueing nothing to the thread pool. Gui marks the UI thread (UiThread.MarkCurrentThreadAsUiThread)
        /// so every agg app gets this.
        /// </summary>
        /// <remarks>
        /// Why: System.Threading.Tasks.Parallel queues loop replicas to the pool's global queue and the calling
        /// thread waits for them. While a long operation (an exact Dilate) keeps every pool worker busy with its
        /// own nested parallel work, those replicas sit unscheduled for minutes - and a UI thread that built a
        /// tiny handle mesh's trace data with a parallel loop froze the whole window that long. Work small enough
        /// to do on the UI thread at all is small enough to do on it alone; large work already goes to a Task.
        /// </remarks>
        public static void RunInlineOnCurrentThread()
        {
            runInlineOnThisThread = true;
        }

        // MaxDegreeOfParallelism = 1 makes System.Threading.Tasks.Parallel run its one replica synchronously on
        // the calling thread and spawn no others, so exception shape and ParallelLoopState semantics stay the
        // same as the parallel path. The scheduler is pinned to Default because the replica is started with
        // RunSynchronously on the options' scheduler, and a custom TaskScheduler.Current that declines inline
        // execution would queue it and leave this thread waiting again.
        private static readonly ParallelOptions InlineOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 1,
            TaskScheduler = TaskScheduler.Default,
        };

        private static readonly ParallelOptions DefaultOptions = new ParallelOptions();

        private static ParallelOptions Options => runInlineOnThisThread ? InlineOptions : DefaultOptions;

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
                System.Threading.Tasks.Parallel.ForEach<T>(source, Options, action);
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
                System.Threading.Tasks.Parallel.For(startInclusive, endExclusive, Options, action);
            }
        }

        /// <summary>
        /// Runs the actions, in parallel unless <see cref="Sequential"/> is set. Exception shape differs
        /// under Sequential: the first throwing action propagates raw and later actions never run, whereas
        /// the parallel path runs all actions and aggregates failures into an AggregateException.
        /// </summary>
        public static void Invoke(params Action[] actions)
        {
            if (Sequential)
            {
                foreach (var action in actions)
                {
                    action();
                }
            }
            else
            {
                System.Threading.Tasks.Parallel.Invoke(Options, actions);
            }
        }

        // No Sequential path: ParallelLoopState has no public constructor, so there is nothing
        // faithful to hand the body. Convert call sites to a plain accumulating loop if this
        // ever needs to run on a single threaded host.
        public static void For<T>(int startInclusive, int endExclusive, Func<T> action, Func<int, ParallelLoopState, T, T> p2, Action<T> p3)
        {
            System.Threading.Tasks.Parallel.For(startInclusive, endExclusive, Options, action, p2, p3);
        }
    }
}
