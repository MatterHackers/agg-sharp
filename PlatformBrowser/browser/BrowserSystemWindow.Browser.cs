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

using System.Runtime.Versioning;

namespace MatterHackers.Agg.Platform.Browser
{
	public partial class BrowserSystemWindow
	{
		/// <summary>
		/// A window wired to the real DOM. What a window provider calls; the constructor stays open for a
		/// test's own seams.
		/// </summary>
		/// <remarks>
		/// The one browser-only member of this class, and the reason it is one: constructing
		/// <see cref="BrowserWindowInterop"/> and <see cref="BrowserFrameLoop"/> is browser-only API use, and
		/// doing it from a parameterless constructor would make the whole window browser-only - which would
		/// take the tick, the resize handling and the screenshot contract out of the desktop test suite.
		/// A method may declare narrower platform support than its class; a constructor cannot usefully.
		/// <para/>
		/// <see cref="BrowserHostBootstrap.InitializeAsync"/> must have completed first: the modules these two
		/// import from have to be in place before the window binds a canvas or starts a loop.
		/// </remarks>
		[SupportedOSPlatform("browser")]
		public static BrowserSystemWindow CreateForBrowser()
			=> new BrowserSystemWindow(new BrowserWindowInterop(), new BrowserFrameLoop());
	}
}
