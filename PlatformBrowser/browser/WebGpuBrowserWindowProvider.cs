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
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.Platform.Browser;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Hands out <see cref="BrowserSystemWindow"/>s. Resolved by name as
	/// <c>"MatterHackers.Agg.UI.WebGpuBrowserWindowProvider, agg_platform_browser"</c>, which is what
	/// <c>AggContext.Config.ProviderTypes.SystemWindowProvider</c> defaults to under wasm.
	/// <para>
	/// The twin of <see cref="WebGpuMacWindowProvider"/> and <c>WebGpuX11WindowProvider</c>, and it lives
	/// here for the same reason they live in their platform layers: a provider's whole job is to construct
	/// its own host's window type.
	/// </para>
	/// <para>
	/// One agg window at a time, unlike those two - a page has one canvas and the newest window shown takes
	/// the animation frame loop over. That is enough for a demo, which shows one window and keeps it. An
	/// application shell that stacks dialogs uses its own <c>SingleWindowProvider</c> and sets
	/// <see cref="BrowserSystemWindow.SingleWindowMode"/>, exactly as the desktop heads do.
	/// </para>
	/// </summary>
	public class WebGpuBrowserWindowProvider : ISystemWindowProvider
	{
		private readonly List<SystemWindow> openWindows = new List<SystemWindow>();

		public IReadOnlyList<SystemWindow> OpenWindows => this.openWindows;

		public SystemWindow TopWindow => this.openWindows.LastOrDefault();

		/// <summary>Creates or reconnects a platform window for the given <see cref="SystemWindow"/>.</summary>
		/// <remarks>
		/// Note what this does NOT do that every desktop provider's caller relies on: block. A browser
		/// window's <c>ShowSystemWindow</c> starts the animation frame loop and returns, so a head has to
		/// keep the runtime alive itself (Blazor's <c>RunAsync</c> does). See
		/// <see cref="BrowserSystemWindow"/>'s class remarks.
		/// </remarks>
		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			IPlatformWindow platformWindow;

			if (systemWindow.PlatformWindow == null)
			{
				platformWindow = CreatePlatformWindow(systemWindow);
			}
			else
			{
				platformWindow = systemWindow.PlatformWindow;
			}

			if (platformWindow is BrowserSystemWindow browserWindow)
			{
				browserWindow.WindowProvider = this;
			}

			this.openWindows.Add(systemWindow);

			platformWindow.ShowSystemWindow(systemWindow);
		}

		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			systemWindow.PlatformWindow?.CloseSystemWindow(systemWindow);
			this.openWindows.Remove(systemWindow);
		}

		/// <summary>
		/// Builds the window that will bind the canvas.
		/// </summary>
		/// <remarks>
		/// Split out of <see cref="ShowSystemWindow"/> only so the browser-only construction sits behind one
		/// <c>IsBrowser()</c> guard. The provider itself carries no platform attribute because
		/// <c>AggContext</c> constructs it by <see cref="Activator"/> from a type string, and the throw is
		/// what makes a provider string pointed at this assembly on a desktop fail where the mistake is
		/// rather than with a null window three frames later.
		/// </remarks>
		private static IPlatformWindow CreatePlatformWindow(SystemWindow systemWindow)
		{
			if (!OperatingSystem.IsBrowser())
			{
				throw new PlatformNotSupportedException(
					"WebGpuBrowserWindowProvider can only show a window inside a browser - it binds a "
					+ "<canvas> through JS interop. On a desktop, point "
					+ "AggContext.Config.ProviderTypes.SystemWindowProvider at that OS's provider instead.");
			}

			BrowserSystemWindow platformWindow = BrowserSystemWindow.CreateForBrowser();

			platformWindow.Caption = systemWindow.Title;
			platformWindow.MinimumSize = systemWindow.MinimumSize;

			return platformWindow;
		}
	}
}
