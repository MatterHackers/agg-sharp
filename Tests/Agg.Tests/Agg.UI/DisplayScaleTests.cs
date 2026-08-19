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
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Dragging a window between a Retina display and a standard one changes how many device pixels a point
	/// is worth, and the application has to rebuild its UI at the new scale to stay legible. The platform
	/// hosts push that factor in through <see cref="SystemWindow.SetDisplayScale"/>; these tests pin down
	/// what that method promises the application layer, including the deferral that keeps a full UI rebuild
	/// from running inside a native window-drag callback.
	/// </summary>
	// The deferred raise is drained by pumping UiThread, which is process wide. Pumping it from this test's
	// own thread would run other tests' queued UI work on the wrong thread, so this shares the key the
	// windowed automation tests use - nothing else may be driving UiThread meanwhile.
	[NotInParallel(nameof(MatterHackers.GuiAutomation.AutomationRunner.ShowWindowAndExecuteTests))]
	public class DisplayScaleTests
	{
		[Test]
		public async Task AWindowStartsAtOneToOne()
		{
			// Every window that no host ever tells otherwise (a headless test, a non-DPI-aware host) has to
			// behave exactly as it did before per-monitor scale existed.
			var systemWindow = new SystemWindow(100, 100);

			await Assert.That(systemWindow.DisplayScale).IsEqualTo(1);
		}

		[Test]
		public async Task TheScaleIsVisibleImmediately()
		{
			// The property is set synchronously even though the event is deferred: anything that reads the
			// scale while laying out (rather than reacting to the change) must never see the stale value.
			var systemWindow = new SystemWindow(100, 100);

			systemWindow.SetDisplayScale(2);

			await Assert.That(systemWindow.DisplayScale).IsEqualTo(2);
		}

		[Test]
		public async Task AnUnusableScaleFallsBackToOne()
		{
			// A zero or NaN scale would silently divide the whole UI to nothing. The hosts read theirs from
			// the OS, so this is defence against a monitor hot-plug reporting garbage mid-transition, not
			// against a caller mistake.
			var systemWindow = new SystemWindow(100, 100);

			systemWindow.SetDisplayScale(2);
			systemWindow.SetDisplayScale(double.NaN);
			await Assert.That(systemWindow.DisplayScale).IsEqualTo(1);

			systemWindow.SetDisplayScale(2);
			systemWindow.SetDisplayScale(0);
			await Assert.That(systemWindow.DisplayScale).IsEqualTo(1);

			systemWindow.SetDisplayScale(2);
			systemWindow.SetDisplayScale(-1.5);
			await Assert.That(systemWindow.DisplayScale).IsEqualTo(1);

			systemWindow.SetDisplayScale(2);
			systemWindow.SetDisplayScale(double.PositiveInfinity);
			await Assert.That(systemWindow.DisplayScale).IsEqualTo(1);
		}

		[Test]
		public async Task AChangeRaisesTheEventOncePumped()
		{
			var systemWindow = new SystemWindow(100, 100);

			int raised = 0;
			double scaleSeenByHandler = 0;
			systemWindow.DisplayScaleChanged += (s, e) =>
			{
				raised++;
				scaleSeenByHandler = systemWindow.DisplayScale;
			};

			systemWindow.SetDisplayScale(2);

			// Deferred on purpose - see SetDisplayScale. Nothing has been told yet.
			await Assert.That(raised).IsEqualTo(0);

			UiThread.InvokePendingActions();

			await Assert.That(raised).IsEqualTo(1);

			// The handler rebuilds the UI at the new scale, so the property must already be the new one.
			await Assert.That(scaleSeenByHandler).IsEqualTo(2);
		}

		[Test]
		public async Task TheFirstReportRaisesEvenWhenItMatchesTheDefault()
		{
			// The 1 a window starts at is an assumption, not a measurement, so a host saying "1" is news.
			// Without this, an application that computed its scale from the PRIMARY monitor (2, on a Retina
			// machine) and then restored its window onto a 1x second monitor would never be told: the report
			// equals the default, and every later return to that monitor is genuinely no change either.
			var systemWindow = new SystemWindow(100, 100);

			int raised = 0;
			systemWindow.DisplayScaleChanged += (s, e) => raised++;

			systemWindow.SetDisplayScale(1);
			UiThread.InvokePendingActions();

			await Assert.That(raised).IsEqualTo(1);

			// Only the first one is news - after that the ordinary coalescing takes over.
			systemWindow.SetDisplayScale(1);
			UiThread.InvokePendingActions();

			await Assert.That(raised).IsEqualTo(1);
		}

		[Test]
		public async Task AFirstReportAndASecondChangeBeforeAPumpStillRaiseOnce()
		{
			// The first-report rule must not escape the coalescing: a window that opens on a 1x monitor and is
			// dragged to a 2x one before the queue drains rebuilds once, for where it ended up.
			var systemWindow = new SystemWindow(100, 100);

			int raised = 0;
			double scaleSeenByHandler = 0;
			systemWindow.DisplayScaleChanged += (s, e) =>
			{
				raised++;
				scaleSeenByHandler = systemWindow.DisplayScale;
			};

			systemWindow.SetDisplayScale(1);
			systemWindow.SetDisplayScale(2);

			UiThread.InvokePendingActions();

			await Assert.That(raised).IsEqualTo(1);
			await Assert.That(scaleSeenByHandler).IsEqualTo(2);
		}

		[Test]
		public async Task SettingTheSameScaleAgainRaisesNothing()
		{
			// windowDidChangeScreen: fires for any screen change, most of which are between two displays of
			// the same scale. Rebuilding the UI for those would be a visible stall for no reason.
			var systemWindow = new SystemWindow(100, 100);

			int raised = 0;
			systemWindow.DisplayScaleChanged += (s, e) => raised++;

			systemWindow.SetDisplayScale(2);
			UiThread.InvokePendingActions();
			await Assert.That(raised).IsEqualTo(1);

			systemWindow.SetDisplayScale(2);
			systemWindow.SetDisplayScale(2);
			UiThread.InvokePendingActions();

			await Assert.That(raised).IsEqualTo(1);
		}

		[Test]
		public async Task SeveralChangesBeforeAPumpRaiseOnceWithTheFinalValue()
		{
			// Dragging a window across a display boundary can cross back and forth several times before the
			// idle queue is drained. The handler is a full UI rebuild, so it runs once, for where the window
			// ended up.
			var systemWindow = new SystemWindow(100, 100);

			int raised = 0;
			double scaleSeenByHandler = 0;
			systemWindow.DisplayScaleChanged += (s, e) =>
			{
				raised++;
				scaleSeenByHandler = systemWindow.DisplayScale;
			};

			systemWindow.SetDisplayScale(2);
			systemWindow.SetDisplayScale(1);
			systemWindow.SetDisplayScale(3);

			UiThread.InvokePendingActions();

			await Assert.That(raised).IsEqualTo(1);
			await Assert.That(scaleSeenByHandler).IsEqualTo(3);
		}

		[Test]
		public async Task ARoundTripWhileQueuedRaisesNothing()
		{
			// The window left the 2x display and came back before anyone was told. Nothing about the UI
			// needs to change, so nothing is said - the coalescing compares against the value the event was
			// last raised for, not against the value at the time the raise was queued.
			var systemWindow = new SystemWindow(100, 100);

			int raised = 0;
			systemWindow.DisplayScaleChanged += (s, e) => raised++;

			systemWindow.SetDisplayScale(2);
			UiThread.InvokePendingActions();
			await Assert.That(raised).IsEqualTo(1);

			systemWindow.SetDisplayScale(1);
			systemWindow.SetDisplayScale(2);
			UiThread.InvokePendingActions();

			await Assert.That(raised).IsEqualTo(1);
			await Assert.That(systemWindow.DisplayScale).IsEqualTo(2);
		}

		[Test]
		public async Task AWindowStartsWithNoUsableScreenSize()
		{
			// Zero means "no host has measured it", which is what an application checks before falling back
			// to whatever it can find out about the desktop on its own.
			var systemWindow = new SystemWindow(100, 100);

			await Assert.That(systemWindow.DisplayUsableSize).IsEqualTo(Vector2.Zero);
		}

		[Test]
		public async Task TheUsableScreenSizeIsVisibleImmediately()
		{
			// Read while recomputing a minimum size rather than reacted to, so it must never be stale.
			var systemWindow = new SystemWindow(100, 100);

			systemWindow.SetDisplayUsableSize(new Vector2(1440, 810));

			await Assert.That(systemWindow.DisplayUsableSize).IsEqualTo(new Vector2(1440, 810));
		}

		[Test]
		public async Task AnUnusableScreenSizeLeavesTheLastGoodOne()
		{
			// A window dragged off screen, or caught mid hot-plug, measures nothing. Taking that as the new
			// truth would throw away the only description of the monitor the window is on and send the
			// application back to guessing from the primary display - which is the bug this exists to fix.
			var systemWindow = new SystemWindow(100, 100);

			systemWindow.SetDisplayUsableSize(new Vector2(1440, 810));

			systemWindow.SetDisplayUsableSize(Vector2.Zero);
			await Assert.That(systemWindow.DisplayUsableSize).IsEqualTo(new Vector2(1440, 810));

			systemWindow.SetDisplayUsableSize(new Vector2(-100, 810));
			await Assert.That(systemWindow.DisplayUsableSize).IsEqualTo(new Vector2(1440, 810));

			systemWindow.SetDisplayUsableSize(new Vector2(1440, double.NaN));
			await Assert.That(systemWindow.DisplayUsableSize).IsEqualTo(new Vector2(1440, 810));

			systemWindow.SetDisplayUsableSize(new Vector2(double.PositiveInfinity, 810));
			await Assert.That(systemWindow.DisplayUsableSize).IsEqualTo(new Vector2(1440, 810));
		}
	}
}
