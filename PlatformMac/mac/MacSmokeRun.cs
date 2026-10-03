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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The end of an unattended <c>AGG_SMOKE_FRAMES</c> run of a <see cref="MacSystemWindow"/>: reading the
	/// frame target, and once the frames are drawn printing the verdict, closing the application's shell
	/// window and making sure the process exits. The Mac twin of <c>WinformsSmokeRun</c>.
	/// </summary>
	internal static class MacSmokeRun
	{
		/// <summary>
		/// Reports how the run went (failing it if the renderer has an error to report) and closes the window
		/// that ends the application. Runs after the paint that reached the frame target, never inside it.
		/// </summary>
		public static void ReportAndClose(MacSystemWindow window, int drawCount)
		{
			string report = window.RenderErrorReport;
			if (!string.IsNullOrEmpty(report))
			{
				Console.Error.WriteLine($"AGG_SMOKE render error: {report}");
				Environment.ExitCode = 1;
			}

			string status = window.RenderStatusReport;
			string detail = $"{drawCount} frames on {window.GetType().Name}"
				+ (string.IsNullOrEmpty(status) ? string.Empty : $" [{status}]");

			if (Environment.ExitCode != 0)
			{
				Console.WriteLine($"AGG_SMOKE FAILED: {detail}");
			}
			else
			{
				Console.WriteLine($"AGG_SMOKE ok: {detail}");
			}

			// Armed before the close, not after: a close that throws or blocks is exactly the case the
			// watchdog exists for.
			StartExitWatchdog();

			try
			{
				// Closing the agg window is what tears the platform window down with it; the platform's own
				// close is only the fallback for a window that was never attached to one.
				var windowToClose = ShellAggWindow(window);

				if (windowToClose != null)
				{
					windowToClose.Close();
				}
				else
				{
					window.Close();
				}
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"AGG_SMOKE: close threw {ex.GetType().Name}: {ex}");
			}
		}

		/// <summary>
		/// The agg window whose close ends the application: the shell, not whatever is currently on top.
		/// See <see cref="PlatformCloseArbitration.ShellWindowForClose"/> for why
		/// <see cref="MacSystemWindow.AggSystemWindow"/> is not that window in single window mode.
		/// </summary>
		private static SystemWindow ShellAggWindow(MacSystemWindow window)
		{
			return PlatformCloseArbitration.ShellWindowForClose(MacSystemWindow.SingleWindowMode, window.WindowProvider, window.AggSystemWindow);
		}

		/// <summary>
		/// Guarantees a smoke run terminates. Closing the window ends the event loop, but a teardown that
		/// throws part way or a demo that left a foreground thread running would keep the process alive
		/// forever, and an unattended run that never returns is indistinguishable from a hang in the
		/// renderer. Firing is itself a failure and is reported as one.
		/// </summary>
		public static void StartExitWatchdog()
		{
			var watchdog = new System.Threading.Timer(
				_ =>
				{
					Console.Error.WriteLine("AGG_SMOKE: the process did not exit on its own after closing; forcing exit.");
					Console.WriteLine("AGG_SMOKE FAILED: the exit watchdog had to force the process down.");
					Environment.Exit(Environment.ExitCode != 0 ? Environment.ExitCode : 1);
				},
				null,
				TimeSpan.FromSeconds(5),
				System.Threading.Timeout.InfiniteTimeSpan);

			// Nothing else holds this; keeping the reference alive is the only thing standing between the
			// timer and the collector.
			exitWatchdog = watchdog;
		}

		private static System.Threading.Timer exitWatchdog;

		/// <summary>The <c>AGG_SMOKE_FRAMES</c> frame target, or 0 when the variable is unset or not a positive count.</summary>
		public static int ParseFrames()
		{
			return int.TryParse(Environment.GetEnvironmentVariable("AGG_SMOKE_FRAMES"), out int frames) && frames > 0
				? frames
				: 0;
		}
	}
}
