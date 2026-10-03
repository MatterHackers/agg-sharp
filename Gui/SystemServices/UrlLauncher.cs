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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Opens a web address in the user's browser. The platform layer that owns the window installs the
	/// <see cref="Provider"/> (NSWorkspace on the mac, window.open in the browser, the shell on Windows);
	/// widgets only call <see cref="Open"/>, so they stay free of platform code.
	/// </summary>
	public static class UrlLauncher
	{
		/// <summary>
		/// Gets or sets what actually opens a url. Null until a platform layer installs one, and a platform
		/// layer only installs one while it is still null, so an app (or a test) that set its own keeps it.
		/// </summary>
		public static Action<string> Provider { get; set; }

		/// <summary>Opens <paramref name="url"/> with the installed <see cref="Provider"/>.</summary>
		/// <returns>False when there is no provider (a headless test, a host with no browser) or the url is not
		/// one <see cref="CanOpen"/> allows, so the caller can show the address instead.</returns>
		public static bool Open(string url)
		{
			var provider = Provider;
			if (provider == null
				|| !TryParse(url, out Uri uri))
			{
				return false;
			}

			// Escaped, never the link text as written: the shell provider puts the url on the browser's command
			// line, where a raw quote or space would end it and let the rest add browser switches. AbsoluteUri
			// keeps escapes the author already wrote (%20 stays %20).
			provider(uri.AbsoluteUri);
			return true;
		}

		/// <summary>
		/// Whether <paramref name="url"/> is an absolute http, https or mailto address - the only kinds a link
		/// may hand to the OS. The providers pass the string to the shell (or NSWorkspace), which would just as
		/// happily run a file: path, a local program or a custom protocol handler, so link text that came from
		/// a document must not be able to reach them with anything else.
		/// </summary>
		public static bool CanOpen(string url) => TryParse(url, out _);

		private static bool TryParse(string url, out Uri uri)
		{
			uri = null;
			return url != null
				&& Uri.TryCreate(url.Trim(), UriKind.Absolute, out uri)
				&& (uri.Scheme == Uri.UriSchemeHttp
					|| uri.Scheme == Uri.UriSchemeHttps
					|| uri.Scheme == Uri.UriSchemeMailto);
		}
	}
}
