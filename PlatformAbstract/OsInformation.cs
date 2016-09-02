/*
Copyright (c) 2014, Lars Brubaker
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
using System.IO;

namespace MatterHackers.Agg.PlatformAbstract
{
	public enum OSType { Unknown, Windows, Mac, X11, Other, Android };

	public class OsInformation
	{
		private static OSType operatingSystem = OSType.Unknown;
		private static Point2D desktopSize = new Point2D();

		public static OSType OperatingSystem
		{
			get
			{
				if (operatingSystem == OSType.Unknown)
				{
					string pluginPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
					PluginFinder<OsInformationPlugin> osInformationPlugins = new PluginFinder<OsInformationPlugin>(pluginPath);
					if (osInformationPlugins.Plugins.Count != 1)
					{
						throw new Exception(string.Format("Did not find any OsInformationPlugins in Plugin path ({0}.", pluginPath));
					}

					operatingSystem = osInformationPlugins.Plugins[0].GetOSType();
				}

				return operatingSystem;
			}
		}

		public static Point2D DesktopSize
		{
			get
			{
				if (desktopSize.GetLength() == 0)
				{
					string pluginPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
					PluginFinder<OsInformationPlugin> osInformationPlugins = new PluginFinder<OsInformationPlugin>(pluginPath);
					if (osInformationPlugins.Plugins.Count != 1)
					{
						throw new Exception(string.Format("Did not find any OsInformationPlugins in Plugin path ({0}.", pluginPath));
					}

					desktopSize = osInformationPlugins.Plugins[0].GetDesktopSize();
				}

				return desktopSize;
			}
		}
	}

	public class OsInformationPlugin
	{
		public virtual Point2D GetDesktopSize()
		{
			return new Point2D();
		}

		public virtual OSType GetOSType()
		{
			throw new Exception("You must implement this in an inherited class.");
		}
	}
}