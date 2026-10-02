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
	/// The window-independent half of an unattended <c>AGG_SMOKE_FRAMES</c> run of the WinForms host: reading
	/// the frame target and making sure the process exits once the run has closed its window.
	/// </summary>
	internal static class WinformsSmokeRun
	{
		/// <summary>
		/// Guarantees a smoke run terminates. Closing the window ends the message loop, but a teardown that
		/// throws part way (leaving the platform window up) or a demo that left a foreground thread running
		/// would keep the process alive forever, and an unattended run that never returns is
		/// indistinguishable from a hang in the renderer.
		/// </summary>
		/// <remarks>
		/// Firing is itself a failure and is reported as one. It used to exit with whatever code the run had
		/// earned, so a shutdown bug that only the watchdog caught scrolled past as a green run - which is
		/// exactly how a teardown exception in ListBox.RemoveChild went unnoticed.
		/// </remarks>
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
