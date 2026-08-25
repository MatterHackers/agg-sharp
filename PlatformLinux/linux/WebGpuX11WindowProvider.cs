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

using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Hands out <see cref="X11SystemWindow"/>s. Resolved by name as
	/// <c>"MatterHackers.Agg.UI.WebGpuX11WindowProvider, agg_platform_linux"</c>, which is what
	/// <c>AggContext.Config.ProviderTypes.SystemWindowProvider</c> defaults to on Linux and what
	/// <c>AGG_WINDOW_PROVIDER=x11</c> selects.
	/// <para>
	/// One native window per agg window, which is the arrangement a demo wants. An application shell uses
	/// <c>SingleWindowProvider</c> instead and sets <see cref="X11SystemWindow.SingleWindowMode"/>.
	/// </para>
	/// </summary>
	public class WebGpuX11WindowProvider : ISystemWindowProvider
	{
		private readonly List<SystemWindow> openWindows = new List<SystemWindow>();

		public IReadOnlyList<SystemWindow> OpenWindows => this.openWindows;

		public SystemWindow TopWindow => this.openWindows.LastOrDefault();

		/// <summary>Creates or reconnects a platform window for the given <see cref="SystemWindow"/>.</summary>
		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			IPlatformWindow platformWindow;

			if (systemWindow.PlatformWindow == null)
			{
				platformWindow = new X11SystemWindow
				{
					Caption = systemWindow.Title,
					MinimumSize = systemWindow.MinimumSize,
				};
			}
			else
			{
				platformWindow = systemWindow.PlatformWindow;
			}

			if (platformWindow is X11SystemWindow x11Window)
			{
				x11Window.WindowProvider = this;
			}

			this.openWindows.Add(systemWindow);

			platformWindow.ShowSystemWindow(systemWindow);
		}

		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			systemWindow.PlatformWindow?.CloseSystemWindow(systemWindow);
			this.openWindows.Remove(systemWindow);
		}
	}
}
