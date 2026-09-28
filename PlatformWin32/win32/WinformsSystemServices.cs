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
using System.ComponentModel;
using System.Diagnostics;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The Windows <see cref="UrlLauncher"/> provider, installed by
	/// <see cref="WinformsSystemWindowProvider{T}"/> when it shows a window. There is no Windows
	/// <see cref="SystemAppearance"/> provider yet; apps that ask get null and use their own fallback.
	/// </summary>
	public static class WinformsSystemServices
	{
		/// <summary>Installs the Windows providers wherever an app has not already installed its own.</summary>
		public static void InstallDefaults()
		{
			UrlLauncher.Provider ??= OpenUrl;
		}

		/// <summary>
		/// Hands the url to the shell, which opens the user's default browser. UseShellExecute is required:
		/// .NET Core defaults it to false, and a url is not an executable.
		/// </summary>
		public static void OpenUrl(string url)
		{
			try
			{
				using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
			}
			catch (Win32Exception e)
			{
				// No handler registered for the scheme (no browser, or no mail client for mailto). A link
				// click must not take the app down; saying so is all there is left to do.
				Console.WriteLine($"Could not open {url}: {e.Message}");
			}
		}
	}
}
