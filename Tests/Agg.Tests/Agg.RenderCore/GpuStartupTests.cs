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
	/// The time budget a window puts around building its render device.
	/// </summary>
	/// <remarks>
	/// No GPU here: the stall this bounds is a synchronous native call - DXGI enumeration, then opening a
	/// D3D12 device - that only misbehaves on a loaded software rasterizer. A blocking factory stands in for
	/// it, which is why the device is built through a delegate rather than constructed in place.
	/// </remarks>
	public class GpuStartupTests
	{
		/// <summary>A stand-in for a render device; identity is all these tests need.</summary>
		private sealed class FakeDevice
		{
		}

		[Test]
		public async Task ADeviceThatArrivesInTimeIsReturned()
		{
			var device = new FakeDevice();
			var reports = new ConcurrentQueue<string>();

			var built = GpuStartup.CreateWithinBudget(
				() => device,
				"quick",
				TimeSpan.FromSeconds(30),
				backgroundThreadAvailable: true,
				report: reports.Enqueue);

			await Assert.That(built).IsSameReferenceAs(device);
			await Assert.That(reports).IsEmpty();
		}

		/// <summary>
		/// The failure this exists for: the acquisition does not come back, and the caller has to be handed
		/// its thread again so the window can open and say it has no device - rather than sitting inside
		/// Show() forever with nothing to report.
		/// </summary>
		[Test]
		public async Task AnAcquisitionThatOutlastsItsBudgetGivesUpAndSaysSo()
		{
			using var releaseBuild = new ManualResetEventSlim(false);
			var reports = new ConcurrentQueue<string>();

			var elapsed = Stopwatch.StartNew();
			var built = GpuStartup.CreateWithinBudget<FakeDevice>(
				() =>
				{
					releaseBuild.Wait();
					return new FakeDevice();
				},
				"stuck adapter",
				TimeSpan.FromMilliseconds(200),
				backgroundThreadAvailable: true,
				report: reports.Enqueue);

			elapsed.Stop();

			await Assert.That(built).IsNull()
				.Because("null is what tells the host to open the window without a device rather than wait on");
			await Assert.That(elapsed.Elapsed).IsLessThan(TimeSpan.FromSeconds(10))
				.Because("the caller waits the budget, not however long the driver takes");
			await Assert.That(reports.Any(message => message.Contains("stuck adapter"))).IsTrue();

			releaseBuild.Set();
		}

		/// <summary>
		/// A device that turns up after the window gave up is leaked rather than released - releasing it
		/// would tear down a swapchain next to a window that may already be gone. It has to say so, because
		/// a silent leak is indistinguishable from a bug, and it must not take the process down.
		/// </summary>
		[Test]
		public async Task ADeviceThatArrivesAfterTheBudgetIsReportedAndLeaked()
		{
			using var releaseBuild = new ManualResetEventSlim(false);
			var reports = new ConcurrentQueue<string>();

			var built = GpuStartup.CreateWithinBudget<FakeDevice>(
				() =>
				{
					releaseBuild.Wait();
					return new FakeDevice();
				},
				"late device",
				TimeSpan.FromMilliseconds(200),
				backgroundThreadAvailable: true,
				report: reports.Enqueue);

			await Assert.That(built).IsNull();

			releaseBuild.Set();

			var deadline = Stopwatch.StartNew();
			while (!reports.Any(message => message.Contains("leaked"))
				&& deadline.Elapsed < TimeSpan.FromSeconds(10))
			{
				await Task.Delay(10);
			}

			await Assert.That(reports.Any(message => message.Contains("leaked"))).IsTrue()
				.Because("a device nobody asked for any more still has to be accounted for out loud");
		}

		/// <summary>
		/// A build that fails is a real error - a machine with no usable adapter, a refused limit - and it
		/// has to reach the caller, who is the only one who can turn it into a window that explains itself.
		/// </summary>
		[Test]
		public async Task AFailureInsideTheBuildReachesTheCaller()
		{
			var thrown = Assert.Throws<InvalidOperationException>(() => GpuStartup.CreateWithinBudget<FakeDevice>(
				() => throw new InvalidOperationException("no adapter"),
				"throwing",
				TimeSpan.FromSeconds(30),
				backgroundThreadAvailable: true,
				report: null));

			await Assert.That(thrown.Message).IsEqualTo("no adapter");
		}

		/// <summary>
		/// The browser leg: one thread, so there is nothing to build on and nothing to bound it with. Built
		/// inline, and runnable on the desktop because the platform is a parameter.
		/// </summary>
		[Test]
		public async Task WithNoBackgroundThreadTheDeviceIsBuiltInline()
		{
			int buildThread = 0;

			var built = GpuStartup.CreateWithinBudget(
				() =>
				{
					buildThread = Environment.CurrentManagedThreadId;
					return new FakeDevice();
				},
				"browser",
				TimeSpan.FromMilliseconds(1),
				backgroundThreadAvailable: false,
				report: null);

			await Assert.That(built).IsNotNull();
			await Assert.That(buildThread).IsEqualTo(Environment.CurrentManagedThreadId);
		}
	}
}
