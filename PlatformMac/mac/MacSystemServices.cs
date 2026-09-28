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
using MatterHackers.Agg.UI;
using static MatterHackers.Agg.Platform.Mac.ObjC;

namespace MatterHackers.Agg.Platform.Mac
{
	/// <summary>
	/// The mac's <see cref="UrlLauncher"/> and <see cref="SystemAppearance"/> providers, installed by
	/// <see cref="WebGpuMacWindowProvider"/> when it shows its first window. Both go through
	/// <see cref="MainThreadDispatcher"/>: AppKit objects are main thread only.
	/// </summary>
	public static class MacSystemServices
	{
		/// <summary>Installs the mac providers wherever an app has not already installed its own.</summary>
		public static void InstallDefaults()
		{
			UrlLauncher.Provider ??= OpenUrl;
			SystemAppearance.Provider ??= PrefersDark;
		}

		/// <summary><c>[[NSWorkspace sharedWorkspace] openURL:[NSURL URLWithString:url]]</c>.</summary>
		public static void OpenUrl(string url)
		{
			MainThreadDispatcher.Invoke(() => WithAutoreleasePool(() =>
			{
				IntPtr nsUrl = Send_r_r(Class("NSURL"), Sel("URLWithString:"), NSString(url));

				// URLWithString: returns nil for a string that is not a url; there is nothing to open then
				if (nsUrl != IntPtr.Zero)
				{
					IntPtr workspace = Send_r(Class("NSWorkspace"), Sel("sharedWorkspace"));
					Send_B_r(workspace, Sel("openURL:"), nsUrl);
				}

				return true;
			}));
		}

		/// <summary>
		/// Whether <c>[NSApp effectiveAppearance]</c> is a dark one. Its name is NSAppearanceNameDarkAqua
		/// (or the accessibility high contrast dark variant), so "Dark" in the name is the test AppKit's own
		/// docs suggest short of bestMatchFromAppearancesWithNames:. An app that never set an appearance
		/// inherits the system's, which is the point.
		/// </summary>
		public static bool PrefersDark()
		{
			return MainThreadDispatcher.Invoke(() => WithAutoreleasePool(() =>
			{
				IntPtr app = Send_r(Class("NSApplication"), Sel("sharedApplication"));
				IntPtr appearance = Send_r(app, Sel("effectiveAppearance"));
				string name = appearance == IntPtr.Zero ? null : FromNSString(Send_r(appearance, Sel("name")));
				return name?.Contains("Dark", StringComparison.Ordinal) == true;
			}));
		}

		/// <summary>
		/// Runs <paramref name="work"/> inside its own NSAutoreleasePool. These calls can arrive outside the
		/// window's event pump, whose pool would otherwise catch the NSURL, NSString and appearance-name
		/// temporaries, so without one they would leak for the life of the process.
		/// </summary>
		private static T WithAutoreleasePool<T>(Func<T> work)
		{
			IntPtr pool = New("NSAutoreleasePool");
			try
			{
				return work();
			}
			finally
			{
				Send_v(pool, Sel("drain"));
			}
		}
	}
}
