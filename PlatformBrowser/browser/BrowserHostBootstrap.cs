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

using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// Loads the host's JS modules. A browser head awaits this once, before it starts the application.
	/// </summary>
	/// <remarks>
	/// <para>Separate from showing a window, and asynchronous, because an ES module import is a promise and
	/// <c>IPlatformWindow.ShowSystemWindow</c> is not allowed to be: it is called from application startup on
	/// every platform and returns void. A window that imported its modules lazily would either block the one
	/// thread waiting for a promise - which is a deadlock, since the promise can only settle by returning to
	/// the event loop - or run its first frames against modules that had not arrived. So the head awaits this,
	/// then calls into agg exactly as a desktop <c>Main</c> does.</para>
	/// <para>Idempotent, and shares one in-flight import: a head that is unsure whether it already
	/// initialized (a Razor component re-rendering, a re-entrant boot path) can simply await it again.</para>
	/// </remarks>
	[SupportedOSPlatform("browser")]
	public static class BrowserHostBootstrap
	{
		/// <summary>
		/// Where the modules are served from, relative to the runtime's own <c>_framework/</c> folder -
		/// <see cref="JSHost.ImportAsync"/> resolves against that, so app-root files need the <c>../</c> hop.
		/// Settable for a head that serves them from somewhere else.
		/// </summary>
		public static string ModuleBasePath { get; set; } = "../";

		/// <summary>The one import, kept so a second call awaits the first rather than importing again.</summary>
		private static Task initialization;

		/// <summary>
		/// Imports <c>frameLoop.js</c>, <c>input.js</c>, <c>peripherals.js</c> and <c>storageMirror.js</c>.
		/// Await this before showing an agg window, installing any of the browser providers, or constructing a
		/// <see cref="BrowserStorageMirrorInterop"/>.
		/// </summary>
		/// <remarks>
		/// All four unconditionally, including for a head that persists nothing: an ES module import is a
		/// fetch of a few hundred bytes from a URL the page has already opened a connection to, and a
		/// conditional import would mean a second boot-order contract for a head to get wrong. The mirror
		/// module does nothing at all until someone calls <c>openStore</c>.
		/// </remarks>
		public static Task InitializeAsync()
		{
			// No lock: wasm has one thread, so there is no race to protect against here (see
			// BrowserSystemWindow's class remarks).
			return initialization ??= ImportModulesAsync();
		}

		private static async Task ImportModulesAsync()
		{
			await JSHost.ImportAsync(BrowserFrameLoop.ModuleName, ModuleBasePath + "frameLoop.js");
			await JSHost.ImportAsync(BrowserWindowInterop.ModuleName, ModuleBasePath + "input.js");
			await JSHost.ImportAsync(BrowserPeripherals.ModuleName, ModuleBasePath + "peripherals.js");
			await JSHost.ImportAsync(BrowserStorageMirrorInterop.ModuleName, ModuleBasePath + "storageMirror.js");

			// Immediately after its own import, and before any provider can call into it: the module has no
			// other way to report a failure that happens inside one of its promises.
			BrowserPeripherals.InstallFaultReporter();
		}
	}
}
