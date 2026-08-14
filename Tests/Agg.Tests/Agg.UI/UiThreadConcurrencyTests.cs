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
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// UiThread is process wide static state. A real app has one message loop, but tests (and any
	/// host that pumps a nested loop) can end up calling InvokePendingActions from more than one
	/// thread at a time. Pumping must never throw, lose a queued action, or run one twice.
	/// </summary>
	// Pumping from this test's own threads would run other tests' queued UI work on the wrong thread, so it
	// shares the key the windowed automation tests use - nothing else may be driving UiThread meanwhile.
	[NotInParallel(nameof(MatterHackers.GuiAutomation.AutomationRunner.ShowWindowAndExecuteTests))]
	public class UiThreadConcurrencyTests
	{
		[Test]
		[Timeout(60_000)]
		public async Task ConcurrentPumpsDoNotThrowOrLoseActions(CancellationToken cancellationToken)
		{
			const int actionCount = 20000;

			var ranCount = 0;
			var pumpFailures = new List<Exception>();
			var stopPumping = false;

			void Pump()
			{
				try
				{
					while (!Volatile.Read(ref stopPumping))
					{
						UiThread.InvokePendingActions();
						Thread.Yield();
					}

					// Drain whatever the producer queued just before the stop flag was set
					UiThread.InvokePendingActions();
				}
				catch (Exception pumpException)
				{
					lock (pumpFailures)
					{
						pumpFailures.Add(pumpException);
					}
				}
			}

			var pumps = new List<Thread>();
			for (int i = 0; i < 3; i++)
			{
				var pump = new Thread(Pump)
				{
					IsBackground = true,
					Name = "UiThread test pump " + i
				};

				pumps.Add(pump);
				pump.Start();
			}

			for (int i = 0; i < actionCount; i++)
			{
				UiThread.RunOnIdle(() => Interlocked.Increment(ref ranCount));
			}

			// Let the pumps get through the queue before asking them to stop. The deadline is only so a
			// dropped action fails the assert below instead of hanging the run.
			var deadline = System.Diagnostics.Stopwatch.StartNew();
			while (Volatile.Read(ref ranCount) < actionCount
				&& deadline.Elapsed < TimeSpan.FromSeconds(10))
			{
				await Task.Delay(1, cancellationToken);
			}

			Volatile.Write(ref stopPumping, true);

			foreach (var pump in pumps)
			{
				pump.Join();
			}

			// The pumps latched UiThread's ui thread id onto threads that are now gone, and may have left
			// intervals behind - hand the next test a clean queue.
			UiThread.ResetForTests();

			await Assert.That(pumpFailures).IsEmpty()
				.Because("pumping the idle queue from two threads must not tear the queue's internal lists");

			await Assert.That(ranCount).IsEqualTo(actionCount)
				.Because("every queued action must run exactly once, no matter which pump picks it up");
		}
	}
}
