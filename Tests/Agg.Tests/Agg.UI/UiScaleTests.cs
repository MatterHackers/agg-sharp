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
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// <see cref="UiScale"/> is the policy between a host's reported display scale and
	/// <see cref="GuiWidget.DeviceScale"/>: compose it at startup, and rebuild once - only once, only when it
	/// really changed, and only when the app can - as a window moves between monitors.
	/// </summary>
	public class UiScaleTests
	{
		[Test]
		[NotInParallel] // writes the process-wide GuiWidget.DeviceScale and pumps UiThread
		public async Task StartupComposesTheMultiplierWithTheDisplay()
		{
			await WithCleanState(async () =>
			{
				UiScale.DisplayScaleOverride = 2;

				UiScale.ApplyAtStartup(() => 1.5);

				await Assert.That(GuiWidget.DeviceScale).IsEqualTo(3.0);
				await Assert.That(UiScale.CurrentDisplayScale).IsEqualTo(2.0);

				UiScale.ApplyAtStartup();
				await Assert.That(GuiWidget.DeviceScale).IsEqualTo(2.0).Because("no multiplier is a multiplier of 1");
			});
		}

		[Test]
		[NotInParallel] // writes the process-wide GuiWidget.DeviceScale
		public async Task UnusableScalesFallBackToOne()
		{
			await WithCleanState(async () =>
			{
				foreach (double unusable in new[] { 0, -2, double.NaN, double.PositiveInfinity })
				{
					UiScale.UpdateForDisplayScale(2);
					UiScale.UpdateForDisplayScale(unusable);

					await Assert.That(GuiWidget.DeviceScale).IsEqualTo(1.0).Because($"a display scale of {unusable} is a 1x display");
				}
			});
		}

		[Test]
		[NotInParallel] // writes the process-wide GuiWidget.DeviceScale and pumps UiThread
		public async Task TheStartupEchoDoesNotRebuild()
		{
			await WithCleanState(async () =>
			{
				UiScale.DisplayScaleOverride = 2;
				UiScale.ApplyAtStartup();
				UiScale.DisplayScaleOverride = null;

				var window = new SystemWindow(100, 100);
				int rebuilds = 0;
				UiScale.Follow(window, () => rebuilds++);

				// The window's first report always raises, carrying the display startup already composed in -
				// give or take a rounding error from a different route to the same monitor.
				Report(window, 2.00000001);

				await Assert.That(rebuilds).IsEqualTo(0);
				await Assert.That(GuiWidget.DeviceScale).IsEqualTo(2.0);
			});
		}

		[Test]
		[NotInParallel] // writes the process-wide GuiWidget.DeviceScale and pumps UiThread
		public async Task AChangeRebuildsOnceAtTheNewScale()
		{
			await WithCleanState(async () =>
			{
				UiScale.DisplayScaleOverride = 1;
				UiScale.ApplyAtStartup(() => 1.25);
				UiScale.DisplayScaleOverride = null;

				var window = new SystemWindow(100, 100);
				var scalesAtRebuild = new System.Collections.Generic.List<double>();
				double scaleBeforeRescale = 0;
				UiScale.Follow(window, () => scalesAtRebuild.Add(GuiWidget.DeviceScale), beforeRescale: () => scaleBeforeRescale = GuiWidget.DeviceScale);

				Report(window, 2);

				await Assert.That(scalesAtRebuild.Count).IsEqualTo(1).Because("one change is one rebuild");
				await Assert.That(scalesAtRebuild[0]).IsEqualTo(2.5).Because("the rebuild runs at multiplier x new display");
				await Assert.That(scaleBeforeRescale).IsEqualTo(1.25).Because("beforeRescale sees the old scale");
				await Assert.That(UiScale.CurrentDisplayScale).IsEqualTo(2.0);
			});
		}

		[Test]
		[NotInParallel] // writes the process-wide GuiWidget.DeviceScale and pumps UiThread
		public async Task ChangesWhileBusyCoalesceIntoOneRebuildWhenFree()
		{
			await WithCleanState(async () =>
			{
				UiScale.RebuildRetrySeconds = 0;
				UiScale.UpdateForDisplayScale(1);

				var window = new SystemWindow(100, 100);
				int rebuilds = 0;
				bool busy = true;
				UiScale.Follow(window, () => rebuilds++, canRebuildNow: () => !busy);

				Report(window, 2);
				Report(window, 1.5);
				UiThread.InvokePendingActions();

				await Assert.That(rebuilds).IsEqualTo(0).Because("nothing rebuilds while the app is busy");
				await Assert.That(GuiWidget.DeviceScale).IsEqualTo(1.0).Because("the scale waits for the rebuild");

				busy = false;
				UiThread.InvokePendingActions();
				UiThread.InvokePendingActions();

				await Assert.That(rebuilds).IsEqualTo(1).Because("one pending retry covers every change");
				await Assert.That(GuiWidget.DeviceScale).IsEqualTo(1.5).Because("the retry reads where the window ended up");
			});
		}

		[Test]
		[NotInParallel] // writes the process-wide GuiWidget.DeviceScale and pumps UiThread
		public async Task ADeferredRescaleIsDroppedWhenTheWindowComesBack()
		{
			await WithCleanState(async () =>
			{
				UiScale.RebuildRetrySeconds = 0;
				UiScale.UpdateForDisplayScale(1);

				var window = new SystemWindow(100, 100);
				int rebuilds = 0;
				bool busy = true;
				UiScale.Follow(window, () => rebuilds++, canRebuildNow: () => !busy);

				Report(window, 2);
				Report(window, 1);
				busy = false;
				UiThread.InvokePendingActions();

				await Assert.That(rebuilds).IsEqualTo(0).Because("the window is back on the display the UI was built for");
			});
		}

		[Test]
		[NotInParallel] // writes the process-wide GuiWidget.DeviceScale and pumps UiThread
		public async Task ClosingTheWindowStopsFollowing()
		{
			await WithCleanState(async () =>
			{
				UiScale.UpdateForDisplayScale(1);

				var window = new SystemWindow(100, 100);
				int rebuilds = 0;
				UiScale.Follow(window, () => rebuilds++);

				window.Close();

				// A closed window never raises the event, so what is checked is the subscription itself.
				await Assert.That(DisplayScaleChangedSubscriberCount(window)).IsEqualTo(0);
				await Assert.That(rebuilds).IsEqualTo(0);
			});
		}

		[Test]
		[NotInParallel] // writes the process-wide GuiWidget.DeviceScale and pumps UiThread
		public async Task DisposingStopsFollowing()
		{
			await WithCleanState(async () =>
			{
				UiScale.UpdateForDisplayScale(1);

				var window = new SystemWindow(100, 100);
				int rebuilds = 0;
				UiScale.Follow(window, () => rebuilds++).Dispose();

				Report(window, 2);

				await Assert.That(rebuilds).IsEqualTo(0);
				await Assert.That(GuiWidget.DeviceScale).IsEqualTo(1.0);
			});
		}

		[Test]
		[NotInParallel] // writes the process-wide GuiWidget.DeviceScale and pumps UiThread
		public async Task RescaledIsRaisedForEveryScaleChangeBeforeTheRebuild()
		{
			await WithCleanState(async () =>
			{
				var log = new System.Collections.Generic.List<string>();
				EventHandler onRescaled = (s, e) => log.Add($"rescaled {GuiWidget.DeviceScale}");
				UiScale.Rescaled += onRescaled;
				try
				{
					UiScale.DisplayScaleOverride = 2;
					UiScale.ApplyAtStartup();
					UiScale.DisplayScaleOverride = null;

					var window = new SystemWindow(100, 100);
					UiScale.Follow(window, () => log.Add("rebuild"));
					Report(window, 1.5);

					await Assert.That(string.Join(", ", log)).IsEqualTo("rescaled 2, rescaled 1.5, rebuild")
						.Because("app-derived sizes follow every scale change, and are ready before the rebuild reads them");
				}
				finally
				{
					UiScale.Rescaled -= onRescaled;
				}
			});
		}

		[Test]
		[NotInParallel] // writes the process-wide GuiWidget.DeviceScale and pumps UiThread
		public async Task OnlyOneRetryIsPendingHoweverManyChangesArrive()
		{
			await WithCleanState(async () =>
			{
				UiScale.RebuildRetrySeconds = 0;
				UiScale.UpdateForDisplayScale(1);

				var window = new SystemWindow(100, 100);
				int asks = 0;
				UiScale.Follow(window, () => { }, canRebuildNow: () =>
				{
					asks++;
					return false;
				});

				Report(window, 2);
				Report(window, 1.5);

				asks = 0;
				UiThread.InvokePendingActions();

				await Assert.That(asks).IsEqualTo(1).Because("a second change while busy must not start a second retry chain");
			});
		}

		[Test]
		[NotInParallel] // writes the process-wide GuiWidget.DeviceScale and pumps UiThread
		public async Task APendingRetryDoesNothingAfterTheWindowCloses()
		{
			await WithCleanState(async () =>
			{
				UiScale.RebuildRetrySeconds = 0;
				UiScale.UpdateForDisplayScale(1);

				var window = new SystemWindow(100, 100);
				int rebuilds = 0;
				bool busy = true;
				UiScale.Follow(window, () => rebuilds++, canRebuildNow: () => !busy);

				Report(window, 2);
				window.Close();
				busy = false;
				UiThread.InvokePendingActions();

				await Assert.That(rebuilds).IsEqualTo(0);
				await Assert.That(GuiWidget.DeviceScale).IsEqualTo(1.0);
			});
		}

		[Test]
		public async Task TheWindowSizeIsScaledAndClampedToTheDesktop()
		{
			await Assert.That(UiScale.ScaledWindowSize(1200, 800, 2, new Point2D(3000, 2000))).IsEqualTo((2400, 1600));
			await Assert.That(UiScale.ScaledWindowSize(1200, 800, 1.5, new Point2D(1920, 1040))).IsEqualTo((1800, 1040));
			await Assert.That(UiScale.ScaledWindowSize(1200, 800, 2, new Point2D(0, 0))).IsEqualTo((2400, 1600))
				.Because("a desktop the host could not measure is not clamped to");
		}

		/// <summary>A host reporting the window is now on a display of <paramref name="scale"/>, delivered.</summary>
		private static void Report(SystemWindow window, double scale)
		{
			// Held for the host too, so a real host's late report cannot contradict the simulated monitor.
			SystemWindow.SimulatedDisplayScale = scale;
			window.SetDisplayScale(scale);
			UiThread.InvokePendingActions();
		}

		private static async Task WithCleanState(Func<Task> test)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			UiScale.ResetForTests();
			try
			{
				await test();
			}
			finally
			{
				SystemWindow.SimulatedDisplayScale = null;
				UiScale.ResetForTests();
				GuiWidget.DeviceScale = savedDeviceScale;
				UiThread.ResetForTests();
			}
		}

		/// <summary>DisplayScaleChanged is a field-like event, so its handlers are only readable through the
		/// compiler's backing field of the same name.</summary>
		private static int DisplayScaleChangedSubscriberCount(SystemWindow window)
		{
			var field = typeof(SystemWindow).GetField(nameof(SystemWindow.DisplayScaleChanged), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			return (field.GetValue(window) as Delegate)?.GetInvocationList().Length ?? 0;
		}
	}
}
