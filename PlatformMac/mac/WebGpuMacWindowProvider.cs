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
	/// Hands out <see cref="MacSystemWindow"/>s. Resolved by name as
	/// <c>"MatterHackers.Agg.UI.WebGpuMacWindowProvider, agg_platform_mac"</c>, which is what
	/// <c>AggContext.Config.ProviderTypes.SystemWindowProvider</c> defaults to on macOS and what
	/// <c>AGG_WINDOW_PROVIDER=mac</c> selects.
	/// </summary>
	public class WebGpuMacWindowProvider : ISystemWindowProvider
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
				platformWindow = new MacSystemWindow
				{
					Caption = systemWindow.Title,
					MinimumSize = systemWindow.MinimumSize,
				};
			}
			else
			{
				platformWindow = systemWindow.PlatformWindow;
			}

			if (platformWindow is MacSystemWindow macWindow)
			{
				macWindow.WindowProvider = this;
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
