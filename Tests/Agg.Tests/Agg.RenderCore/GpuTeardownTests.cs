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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.RenderCore;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.RenderCore
{
	/// <summary>
	/// The time budget a window close puts around the GPU drain.
	/// </summary>
	/// <remarks>
	/// No GPU here, and deliberately so: the thing under test is the budget, and the slow native wait it
	/// exists for (wgpu waiting on the device fence before it can release a swapchain) is only slow on a
	/// software rasterizer - GitHub's WARP runners, where it measured minutes. A blocking delegate stands in
	/// for it, which is the whole reason the drain is passed in as an <see cref="Action"/>.
	/// </remarks>
	public class GpuTeardownTests
	{
		[Test]
		public async Task AQuickDrainRunsToCompletion()
		{
			bool drained = false;
			var reports = new ConcurrentQueue<string>();

			bool finished = GpuTeardown.DrainWithinBudget(
				() => drained = true,
				"quick",
				TimeSpan.FromSeconds(30),
				backgroundThreadAvailable: true,
				report: reports.Enqueue);

			await Assert.That(finished).IsTrue()
				.Because("true is the caller's permission to go on and release");
			await Assert.That(drained).IsTrue();
			await Assert.That(reports).IsEmpty()
				.Because("a drain that finished inside its budget is not news");
		}

		/// <summary>
		/// The reason this class exists: a drain that outlasts its budget must hand the calling thread back,
		/// not hold it for as long as the driver feels like.
		/// </summary>
		[Test]
		public async Task ADrainThatOutlastsItsBudgetIsAbandoned()
		{
			using var releaseDrain = new ManualResetEventSlim(false);
			var drainFinished = new ManualResetEventSlim(false);
			var reports = new ConcurrentQueue<string>();

			var elapsed = Stopwatch.StartNew();
			bool finished = GpuTeardown.DrainWithinBudget(
				() =>
				{
					// Stands in for wgpu waiting on a queue of frames a software rasterizer has not finished.
					releaseDrain.Wait();
					drainFinished.Set();
				},
				"slow device",
				TimeSpan.FromMilliseconds(200),
				backgroundThreadAvailable: true,
				report: reports.Enqueue);

			elapsed.Stop();

			await Assert.That(finished).IsFalse()
				.Because("false is what stops the caller from releasing a surface the drain is still inside");
			await Assert.That(elapsed.Elapsed).IsLessThan(TimeSpan.FromSeconds(10))
				.Because("the caller waits the budget, not however long the GPU takes");
			await Assert.That(reports.Any(message => message.Contains("slow device"))).IsTrue()
				.Because("the message has to name which device was abandoned");

			// The abandoned drain is still running: nothing was released, so it cannot fault, and letting it
			// finish here keeps the test from leaving a live thread behind.
			releaseDrain.Set();
			await Assert.That(drainFinished.Wait(TimeSpan.FromSeconds(10))).IsTrue();
		}

		/// <summary>
		/// A drain that throws is a bug in the drain, and it has to surface on the thread that asked for it -
		/// the same place it would have surfaced without a budget. Swallowing it on the helper thread instead
		/// would take the process down with an unhandled exception.
		/// </summary>
		[Test]
		public async Task AnExceptionFromTheDrainReachesTheCaller()
		{
			var thrown = Assert.Throws<InvalidOperationException>(() => GpuTeardown.DrainWithinBudget(
				() => throw new InvalidOperationException("drain blew up"),
				"throwing",
				TimeSpan.FromSeconds(30),
				backgroundThreadAvailable: true,
				report: null));

			await Assert.That(thrown.Message).IsEqualTo("drain blew up");
		}

		/// <summary>
		/// The scary one: the drain fails <em>after</em> the caller has given up on it, so there is no longer
		/// anyone to hand the exception to. An exception left to escape that thread would take the whole
		/// process down - the test surviving to its asserts is half of what it proves - and the failure has
		/// to be reported rather than silently dropped, because nothing else will ever mention it.
		/// </summary>
		[Test]
		public async Task ADrainThatFailsAfterBeingAbandonedIsReportedAndSurvived()
		{
			using var failTheDrain = new ManualResetEventSlim(false);
			var reports = new ConcurrentQueue<string>();

			bool finished = GpuTeardown.DrainWithinBudget(
				() =>
				{
					failTheDrain.Wait();
					throw new InvalidOperationException("late drain failure");
				},
				"late failure",
				TimeSpan.FromMilliseconds(200),
				backgroundThreadAvailable: true,
				report: reports.Enqueue);

			await Assert.That(finished).IsFalse();

			// Only now does the abandoned drain fail, with nobody joining it.
			failTheDrain.Set();

			var deadline = Stopwatch.StartNew();
			while (!reports.Any(message => message.Contains("late drain failure"))
				&& deadline.Elapsed < TimeSpan.FromSeconds(10))
			{
				await Task.Delay(10);
			}

			var failureReport = reports.FirstOrDefault(message => message.Contains("late drain failure"));

			await Assert.That(failureReport).IsNotNull()
				.Because("a failure nobody is waiting for is a failure nobody would ever hear about");
			await Assert.That(failureReport).Contains("late failure")
				.Because("the report has to name the device it belongs to");
			await Assert.That(failureReport).Contains(nameof(GpuTeardownTests))
				.Because("the stack is what makes a report from a detached thread actionable");
		}

		/// <summary>
		/// The browser leg. wasm is single threaded, so there is no thread to detach a slow drain onto and
		/// nothing to bound it with; the call has to run inline rather than throw out of
		/// <c>new Thread(...)</c>. Runnable on the desktop because the platform is a parameter.
		/// </summary>
		[Test]
		public async Task WithNoBackgroundThreadTheDrainRunsInline()
		{
			int drainThread = 0;

			bool finished = GpuTeardown.DrainWithinBudget(
				() => drainThread = Environment.CurrentManagedThreadId,
				"browser",
				TimeSpan.FromMilliseconds(1),
				backgroundThreadAvailable: false,
				report: null);

			await Assert.That(finished).IsTrue();
			await Assert.That(drainThread).IsEqualTo(Environment.CurrentManagedThreadId);
		}
	}
}
