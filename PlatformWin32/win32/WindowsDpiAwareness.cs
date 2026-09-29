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
using System.Windows.Forms;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// Opts the process into per-monitor (V2) DPI awareness, so Windows hands agg real device pixels and
	/// real scale reports instead of bitmap-stretching a 96 DPI window.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The WinForms host's job, not each application's: an unaware process is told 96 DPI by
	/// <c>GetDpiForSystem</c> (so <see cref="WinformsInformationProvider.DisplayScale"/> is always 1) and
	/// never gets <c>DpiChanged</c>, so <c>UiScale</c> would have nothing true to follow. V2 rather than V1:
	/// V1 leaves the title bar and frame at the DPI the process started on when the window changes monitor.
	/// </para>
	/// <para>
	/// Windows locks the awareness at the process's first top-level window, so this runs from the two places
	/// that can come first - <see cref="WinformsInformationProvider"/>'s constructor, which reads the system
	/// DPI, and <see cref="UI.WinformsSystemWindow"/>'s static constructor. Idempotent: after the first call,
	/// and after an application that already set its own awareness (MatterCAD's <c>ConfigureHighDpi</c>),
	/// <see cref="Application.SetHighDpiMode"/> just returns false. It never throws: a failure is logged, so
	/// neither caller can be broken by it.
	/// </para>
	/// </remarks>
	public static class WindowsDpiAwareness
	{
		private static readonly object Gate = new object();

		private static bool attempted;

		/// <summary>
		/// Asks for per-monitor V2 awareness once; later calls do nothing. Public for an application that opens
		/// a plain WinForms window (a splash screen) before agg's host is touched - it calls this first.
		/// </summary>
		public static void EnsurePerMonitorV2()
		{
			lock (Gate)
			{
				if (attempted)
				{
					return;
				}

				attempted = true;
			}

			// False when a window already exists or the awareness was already set; either way nothing more can
			// be done from here, and the app still runs (bitmap-scaled at worst).
			try
			{
				Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
			}
			catch (Exception e)
			{
				// Never let this escape: one caller is WinformsSystemWindow's static constructor, where an
				// exception becomes a TypeInitializationException that makes every later window fail too. A
				// DPI-unaware app is blurry; one that cannot open a window is broken.
				Console.Error.WriteLine($"agg: could not make the process per-monitor DPI aware ({e.GetType().Name}: {e.Message}); Windows will bitmap-scale its windows.");
			}
		}
	}
}
