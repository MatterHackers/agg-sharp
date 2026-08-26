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

using System.Threading;
using System.Threading.Tasks;
using MatterHackers.RenderCore.Testing;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.RenderCore
{
	/// <summary>
	/// The machine-wide gate that keeps two test processes off the GPU at once.
	/// </summary>
	/// <remarks>
	/// No GPU here: the gate is a named mutex, and everything worth asserting about it (exclusion,
	/// releasing from a thread other than the one that acquired, re-entry) is visible between two threads
	/// of one process. A second thread stands in for a second shard because the mutex is named - what
	/// serializes the threads is the same object that serializes the processes.
	/// </remarks>
	[NotInParallel]
	public class GpuTestGateTests
	{
		[Test]
		public async Task ASecondAcquireWaitsForTheFirstToRelease()
		{
			var first = GpuTestGate.Acquire("first");

			var secondAcquired = new ManualResetEventSlim(false);
			var second = StartIndependentAcquire("second", secondAcquired);

			// A generous window that still cannot pass by accident: if the gate let the second acquire
			// straight through, this fires almost immediately.
			await Assert.That(secondAcquired.Wait(500)).IsFalse()
				.Because("the gate must not let a second waiter in while the first still holds it");

			first.Dispose();

			await second;
			await Assert.That(secondAcquired.IsSet).IsTrue()
				.Because("releasing the gate has to let the waiter through");
		}

		/// <summary>
		/// Releasing on a different thread than the one that acquired is the normal case for an async test
		/// body, and a Mutex released off its owning thread throws - which is why the gate hands ownership to
		/// a thread of its own.
		/// </summary>
		[Test]
		public async Task ItCanBeReleasedFromAnotherThread()
		{
			var scope = GpuTestGate.Acquire("acquired here");

			await Task.Run(() => scope.Dispose());

			// If the release above had thrown or been skipped, this would block for the full timeout.
			using (GpuTestGate.Acquire("acquired again"))
			{
			}
		}

		/// <summary>
		/// A gated harness created inside an already-gated span must not wait on a gate its own flow holds -
		/// the mutex is owned by a helper thread, so nothing about this flow would count as re-entry.
		/// </summary>
		[Test]
		public async Task NestedAcquiresInOneFlowDoNotDeadlock()
		{
			using (await GpuTestGate.AcquireAsync("outer"))
			{
				using (GpuTestGate.Acquire("inner"))
				{
				}
			}

			// The nesting bookkeeping has to have unwound, or this acquire would be a no-op that leaves the
			// gate held for the rest of the run.
			var after = GpuTestGate.Acquire("after");
			var reacquired = new ManualResetEventSlim(false);
			var contender = StartIndependentAcquire("contender", reacquired);

			await Assert.That(reacquired.Wait(500)).IsFalse()
				.Because("after the nested scopes unwound, the gate must be really held again");

			after.Dispose();
			await contender;
		}

		/// <summary>
		/// Takes the gate on a thread that stands in for another shard: the execution context is deliberately
		/// not flowed, because the gate treats one logical flow's nested acquires as re-entry and an inherited
		/// context would make this contender look like the holder rather than a rival.
		/// </summary>
		/// <param name="label">The gate label to acquire under.</param>
		/// <param name="acquired">Set once the contender is inside the gate.</param>
		private static Task StartIndependentAcquire(string label, ManualResetEventSlim acquired)
		{
			using (ExecutionContext.SuppressFlow())
			{
				return Task.Run(() =>
				{
					using (GpuTestGate.Acquire(label))
					{
						acquired.Set();
					}
				});
			}
		}
	}
}
