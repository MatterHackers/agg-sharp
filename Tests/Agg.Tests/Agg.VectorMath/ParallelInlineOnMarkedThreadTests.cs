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
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using AggParallel = MatterHackers.Agg.Parallel;

namespace MatterHackers.VectorMath.Tests
{
	/// <summary>
	/// A thread marked with <see cref="AggParallel.RunInlineOnCurrentThread"/> (the UI thread) runs every
	/// loop routed through agg's Parallel on itself and queues nothing to the thread pool. The bug this pins:
	/// the UI thread built a handle mesh's BVH with a Parallel.For while an exact Dilate kept every pool worker
	/// busy, and waited minutes for loop replicas the pool never got to - a frozen window.
	/// </summary>
	/// <remarks>
	/// Each test runs on a fresh dedicated thread so the per-thread mark never lands on a shared pool thread.
	/// Bodies spin briefly so the unmarked parallel path would really spread the work across pool threads
	/// rather than the caller finishing a trivial loop before any replica starts.
	/// </remarks>
	public class ParallelInlineOnMarkedThreadTests
	{
		private const int Iterations = 1000;

		[Test]
		public async Task ForRunsEveryIterationOnTheMarkedThread()
		{
			var (callerId, ranOn) = RunOnMarkedThread(ids =>
				AggParallel.For(0, Iterations, i => Record(ids)));

			await AssertAllOn(callerId, ranOn, Iterations);
		}

		[Test]
		public async Task ForEachRunsEveryItemOnTheMarkedThread()
		{
			var (callerId, ranOn) = RunOnMarkedThread(ids =>
				AggParallel.ForEach(Count(Iterations), i => Record(ids)));

			await AssertAllOn(callerId, ranOn, Iterations);
		}

		[Test]
		public async Task InvokeRunsEveryActionOnTheMarkedThread()
		{
			var (callerId, ranOn) = RunOnMarkedThread(ids =>
				AggParallel.Invoke(
					() => Record(ids, 200_000),
					() => Record(ids, 200_000),
					() => Record(ids, 200_000),
					() => Record(ids, 200_000),
					() => Record(ids, 200_000),
					() => Record(ids, 200_000),
					() => Record(ids, 200_000),
					() => Record(ids, 200_000)));

			await AssertAllOn(callerId, ranOn, 8);
		}

		/// <summary>
		/// The localInit/body/localFinally overload, which has no Sequential path - the inline path must still
		/// hand the body a real ParallelLoopState and fold every local into the result.
		/// </summary>
		[Test]
		public async Task ForWithLoopStateRunsEveryIterationOnTheMarkedThread()
		{
			long sum = 0;
			bool sawLoopState = true;
			var (callerId, ranOn) = RunOnMarkedThread(ids =>
				AggParallel.For<long>(
					0,
					Iterations,
					() =>
					{
						Record(ids, 0);
						return 0L;
					},
					(i, state, local) =>
					{
						Record(ids);
						sawLoopState &= state != null;
						return local + i;
					},
					local =>
					{
						Record(ids, 0);
						Interlocked.Add(ref sum, local);
					}));

			// One localInit and one localFinally on the single replica, plus every body.
			await AssertAllOn(callerId, ranOn, Iterations + 2);
			await Assert.That(sawLoopState).IsTrue();
			await Assert.That(sum).IsEqualTo((long)Iterations * (Iterations - 1) / 2);
		}

		private static (int callerId, ConcurrentBag<int> ranOn) RunOnMarkedThread(Action<ConcurrentBag<int>> work)
		{
			var ranOn = new ConcurrentBag<int>();
			int callerId = 0;
			Exception failure = null;

			var thread = new Thread(() =>
			{
				try
				{
					callerId = Environment.CurrentManagedThreadId;
					AggParallel.RunInlineOnCurrentThread();
					work(ranOn);
				}
				catch (Exception e)
				{
					failure = e;
				}
			})
			{
				IsBackground = true,
				Name = "Parallel inline test thread",
			};

			thread.Start();
			thread.Join();

			if (failure != null)
			{
				throw failure;
			}

			return (callerId, ranOn);
		}

		private static void Record(ConcurrentBag<int> ids, int spin = 2000)
		{
			Thread.SpinWait(spin);
			ids.Add(Environment.CurrentManagedThreadId);
		}

		private static IEnumerable<int> Count(int count)
		{
			for (int i = 0; i < count; i++)
			{
				yield return i;
			}
		}

		private static async Task AssertAllOn(int callerId, ConcurrentBag<int> ranOn, int expectedCount)
		{
			await Assert.That(ranOn.Count).IsEqualTo(expectedCount);

			int elsewhere = 0;
			foreach (var id in ranOn)
			{
				if (id != callerId)
				{
					elsewhere++;
				}
			}

			await Assert.That(elsewhere).IsEqualTo(0)
				.Because("a marked thread must not hand loop work to the thread pool, where a busy pool can leave it waiting for minutes");
		}
	}
}
