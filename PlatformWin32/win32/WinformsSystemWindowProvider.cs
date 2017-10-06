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
using MatterHackers.Agg.Platform;

namespace MatterHackers.Agg.UI
{
	public class WinformsSystemWindowProvider : ISystemWindowProvider
	{
		private static IPlatformWindow platformWindow = null;

		/// <summary>
		/// Creates or connects a PlatformWindow to the given SystemWindow
		/// </summary>
		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			bool singleWindowMode = WinformsSystemWindow.SingleWindowMode;
			bool isFirstWindow = platformWindow == null;
			if ((singleWindowMode && platformWindow == null)
				|| !singleWindowMode)
			{
				platformWindow = AggContext.CreateInstanceFrom<IPlatformWindow>(AggContext.Config.ProviderTypes.SystemWindow);
			}

			if ((singleWindowMode && isFirstWindow)
				|| !singleWindowMode)
			{
				platformWindow.Caption = systemWindow.Title;
				platformWindow.MinimumSize = systemWindow.MinimumSize;
			}

			systemWindow.PlatformWindow = platformWindow;
			platformWindow.ShowSystemWindow(systemWindow);
		}

		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			platformWindow.CloseSystemWindow(systemWindow);
		}
	}
}
