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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static MatterHackers.Agg.Platform.Mac.ObjC;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The mac host under <see cref="IPlatformWindow.ShowWindowsInBackground"/> (on for this whole test
	/// process): a shown window draws and presents, but the process never becomes the active application,
	/// the window never becomes key, and a second window opens above the first rather than over the user.
	/// The WinForms counterpart is <see cref="WinformsShowInBackgroundTests"/>.
	/// </summary>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class MacShowInBackgroundTests
	{
		[Test]
		[Timeout(60_000)]
		public async Task AShownWindowDrawsWithoutActivatingTheAppOrBecomingKey(CancellationToken cancellationToken)
		{
			await WithShownWindow(cancellationToken, async (window, macWindow) =>
			{
				var state = MainThreadDispatcher.Invoke(() => ReadState(macWindow));
				Console.WriteLine($"MacShowInBackgroundTests: {state}");

				await Assert.That(state.AppIsActive).IsFalse()
					.Because("showing a test window must not take the user away from the app they are in");
				await Assert.That(state.FrontmostPid).IsNotEqualTo(Environment.ProcessId);
				await Assert.That(state.IsKey).IsFalse();
				await Assert.That(state.IsVisible).IsTrue()
					.Because("the window is still shown - it is never minimized, which would stop it drawing");
			});
		}

		[Test]
		[Timeout(60_000)]
		public async Task ASecondWindowOpensAboveTheFirstWithoutActivating(CancellationToken cancellationToken)
		{
			await WithShownWindow(cancellationToken, async (first, firstMac) =>
			{
				await WithShownWindow(cancellationToken, async (second, secondMac) =>
				{
					IntPtr frontmostOfOurs = MainThreadDispatcher.Invoke(() =>
					{
						IntPtr ordered = Send_r(NSApp, Sel("orderedWindows"));
						return Send_r_Q(ordered, Sel("objectAtIndex:"), 0);
					});
					var state = MainThreadDispatcher.Invoke(() => ReadState(secondMac));

					await Assert.That(frontmostOfOurs).IsEqualTo(secondMac.NativeWindow);
					await Assert.That(state.AppIsActive).IsFalse();
					await Assert.That(state.IsKey).IsFalse();
				});
			});
		}

		private static IntPtr NSApp => Send_r(Class("NSApplication"), Sel("sharedApplication"));

		private record WindowState(bool AppIsActive, bool IsKey, bool IsVisible, int FrontmostPid, ulong OcclusionState, int PresentedFrames);

		private static WindowState ReadState(MacSystemWindow macWindow)
		{
			IntPtr workspace = Send_r(Class("NSWorkspace"), Sel("sharedWorkspace"));
			IntPtr frontmost = Send_r(workspace, Sel("frontmostApplication"));
			int frontmostPid = frontmost == IntPtr.Zero ? -1 : (int)Send_q(frontmost, Sel("processIdentifier"));

			return new WindowState(
				Send_B(NSApp, Sel("isActive")) != NO,
				Send_B(macWindow.NativeWindow, Sel("isKeyWindow")) != NO,
				Send_B(macWindow.NativeWindow, Sel("isVisible")) != NO,
				frontmostPid,
				Send_Q(macWindow.NativeWindow, Sel("occlusionState")),
				macWindow.WebGpuLayer.PresentedFrameCount);
		}

		/// <summary>Shows a window from a worker (as every windowed test does), waits for it to draw, runs <paramref name="check"/>, then closes it.</summary>
		private static async Task WithShownWindow(CancellationToken cancellationToken, Func<SystemWindow, MacSystemWindow, Task> check)
		{
			// What is under test is what the host does on a show. If this process is already the active app -
			// the user clicked one of its windows - there is nothing the show could take away, and failing
			// would blame the host for the user's click.
			if (MainThreadDispatcher.Invoke(() => Send_B(NSApp, Sel("isActive")) != NO))
			{
				Skip.Test("The test process is already the active app (a window of it was clicked), so a show cannot be checked for taking the foreground.");
			}

			var window = new SystemWindow(300, 200) { BackgroundColor = Color.White };
			int draws = 0;
			window.AfterDraw += (s, e) => Interlocked.Increment(ref draws);

			// ShowAsSystemWindow blocks for the window's lifetime when it starts the loop, and returns at
			// once when a loop is already running - Task.Run covers both.
			var shown = Task.Run(() => window.ShowAsSystemWindow(), cancellationToken);
			try
			{
				await WaitFor(() => Volatile.Read(ref draws) > 0, TimeSpan.FromSeconds(20), cancellationToken);
				await Assert.That(Volatile.Read(ref draws)).IsGreaterThan(0)
					.Because("a window in the background still has to draw, or every test waiting on a frame stalls");

				await check(window, (MacSystemWindow)window.PlatformWindow);
			}
			finally
			{
				window.CloseOnIdle();
				await WaitFor(() => window.HasBeenClosed, TimeSpan.FromSeconds(20), cancellationToken);
			}
		}

		/// <summary>Polls a condition off the UI thread; the caller's assert reports a miss.</summary>
		private static async Task WaitFor(Func<bool> condition, TimeSpan limit, CancellationToken cancellationToken)
		{
			var watch = Stopwatch.StartNew();
			while (!condition() && watch.Elapsed < limit)
			{
				await Task.Delay(10, cancellationToken);
			}
		}
	}
}
