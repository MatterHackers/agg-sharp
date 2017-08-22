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
using System.Runtime.InteropServices;
using MatterHackers.Agg;

namespace MatterHackers.Agg.Platform
{
	public class WinformsInformationProvider : IOsInformationProvider
	{
		//From Managed.Windows.Forms/XplatUI
		[DllImport("libc")]
		private static extern int uname(IntPtr buf);

		private static bool IsRunningOnMac()
		{
			IntPtr buf = IntPtr.Zero;
			try
			{
				buf = Marshal.AllocHGlobal(8192);
				// This is a hacktastic way of getting sysname from uname ()
				if (uname(buf) == 0)
				{
					string os = Marshal.PtrToStringAnsi(buf);
					if (os == "Darwin")
					{
						return true;
					}
				}
			}
			catch
			{
			}
			finally
			{
				if (buf != IntPtr.Zero)
				{
					Marshal.FreeHGlobal(buf);
				}
			}
			return false;
		}

		public WinformsInformationProvider()
		{
			this.OperatingSystem = GetOSType();

			var size = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Size;
			this.DesktopSize = new Point2D(size.Width, size.Height);
		}

		public OSType OperatingSystem { get; }
		public Point2D DesktopSize { get; }

		private OSType GetOSType()
		{
			if (Path.DirectorySeparatorChar == '\\')
			{
				return OSType.Windows;
			}
			else if (IsRunningOnMac())
			{
				return OSType.Mac;
			}
			else if (Environment.OSVersion.Platform == PlatformID.Unix)
			{
				return OSType.X11;
			}
			else
			{
				return OSType.Other;
			}
		}
	}
}
