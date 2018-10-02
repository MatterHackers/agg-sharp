//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.Platform;

namespace MatterHackers.Agg.UI
{
	public class WinformsSystemWindowProvider : ISystemWindowProvider
	{
		private List<SystemWindow> _openWindows = new List<SystemWindow>();

		public IReadOnlyList<SystemWindow> openWindows => _openWindows;

		public SystemWindow topWindow => _openWindows.LastOrDefault();

		/// <summary>
		/// Creates or connects a PlatformWindow to the given SystemWindow
		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			IPlatformWindow platformWindow;

			if (systemWindow.PlatformWindow == null)
			{
				platformWindow = AggContext.CreateInstanceFrom<IPlatformWindow>(AggContext.Config.ProviderTypes.SystemWindow);
				platformWindow.Caption = systemWindow.Title;
				platformWindow.MinimumSize = systemWindow.MinimumSize;
			}
			else
			{
				platformWindow = systemWindow.PlatformWindow;
			}

			if (platformWindow is WinformsSystemWindow winforms)
			{
				winforms.WindowProvider = this;
			}

			_openWindows.Add(systemWindow);

			platformWindow.ShowSystemWindow(systemWindow);
		}

		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			_openWindows.Remove(systemWindow);
			systemWindow.PlatformWindow.CloseSystemWindow(systemWindow);
		}
	}
}
