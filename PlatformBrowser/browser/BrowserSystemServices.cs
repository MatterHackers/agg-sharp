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

using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// The browser's <see cref="UrlLauncher"/> and <see cref="SystemAppearance"/> providers, installed by
	/// <see cref="WebGpuBrowserWindowProvider"/> when it shows a window. Both are plain window functions, so
	/// they import straight off <c>globalThis</c> and need no module of their own.
	/// </summary>
	[SupportedOSPlatform("browser")]
	public static partial class BrowserSystemServices
	{
		/// <summary>Installs the browser providers wherever an app has not already installed its own.</summary>
		public static void InstallDefaults()
		{
			UrlLauncher.Provider ??= OpenUrl;
			SystemAppearance.Provider ??= PrefersDark;
		}

		/// <summary>
		/// <c>window.open(url, "_blank", "noopener")</c>: a new tab that cannot reach back into this page.
		/// It runs from a menu click, which is inside the user gesture popup blockers require.
		/// </summary>
		public static void OpenUrl(string url)
		{
			// With noopener the browser hands back null rather than the new window; dispose whatever comes back
			using JSObject opened = Open(url, "_blank", "noopener");
		}

		/// <summary><c>matchMedia("(prefers-color-scheme: dark)").matches</c>, read afresh on every call.</summary>
		public static bool PrefersDark()
		{
			using JSObject query = MatchMedia("(prefers-color-scheme: dark)");
			return query?.GetPropertyAsBoolean("matches") == true;
		}

		[JSImport("globalThis.open")]
		private static partial JSObject Open(string url, string target, string features);

		[JSImport("globalThis.matchMedia")]
		private static partial JSObject MatchMedia(string query);
	}
}
