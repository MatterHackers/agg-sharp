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
	/// The end of an unattended <c>AGG_SMOKE_FRAMES</c> run of a <see cref="WinformsSystemWindow"/>, once its
	/// frames are drawn: print the verdict, close the application's shell window and arm the exit watchdog.
	/// </summary>
	internal static class WinformsSmokeRunClose
	{
		/// <summary>
		/// Reports how the run went (failing it if the renderer has an error to report) and closes the window
		/// that ends the application. Runs after the paint that reached the frame target, never inside it.
		/// </summary>
		public static void ReportAndClose(WinformsSystemWindow window, int drawCount)
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

			// The exit code was already right, but a run that failed used to print "ok" anyway, and the
			// console line is what a human (and every log scraper) reads first.
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
			WinformsSmokeRun.StartExitWatchdog();

			try
			{
				// Closing the agg window is what tears the platform window down with it; the form's own
				// Close is only the fallback for a window that was never attached to one.
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
				// The close raced its own teardown, or a demo threw on the way out. Worth saying out loud
				// - it is the difference between "closed cleanly" and "the watchdog had to shoot it" - but
				// not worth failing the run over, since the frames all rendered.
				Console.Error.WriteLine($"AGG_SMOKE: close threw {ex.GetType().Name}: {ex}");
			}
		}

		/// <summary>
		/// The agg window whose close ends the application: the shell, not whatever is currently on top.
		/// </summary>
		/// <remarks>
		/// In single window mode <see cref="WinformsSystemWindow.AggSystemWindow"/> is the window being drawn and given the
		/// events, which the provider re-points at every dialog that opens. Closing that only dismisses the
		/// dialog - the shell stays up, the message loop keeps running, and the process never exits. The
		/// provider keeps the shell first in <see cref="ISystemWindowProvider.OpenWindows"/> and takes the
		/// dialogs above it down with it, so closing that one window is the whole application closing.
		/// See <see cref="PlatformCloseArbitration.ShellWindowForClose"/>, which every host shares.
		/// </remarks>
		private static SystemWindow ShellAggWindow(WinformsSystemWindow window)
		{
			return PlatformCloseArbitration.ShellWindowForClose(
				WinformsSystemWindow.SingleWindowMode,
				window.WindowProvider,
				window.AggSystemWindow);
		}
	}
}
