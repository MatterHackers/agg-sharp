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
	/// The operating system's own light/dark setting, for apps that offer a "follow the system" theme. The
	/// platform layer that owns the window installs the <see cref="Provider"/> (NSApp's effective appearance
	/// on the mac, the prefers-color-scheme media query in the browser).
	/// </summary>
	public static class SystemAppearance
	{
		/// <summary>
		/// Gets or sets the test for whether the system is in dark mode. It is asked afresh on every read of
		/// <see cref="PrefersDark"/>, so a change made in the OS settings shows the next time an app looks.
		/// Null until a platform layer installs one; a platform layer only installs one while it is still null.
		/// </summary>
		public static Func<bool> Provider { get; set; }

		/// <summary>
		/// Gets whether the system prefers a dark appearance, or null when there is no provider to ask - the
		/// caller picks its own fallback.
		/// </summary>
		public static bool? PrefersDark => Provider?.Invoke();
	}
}
