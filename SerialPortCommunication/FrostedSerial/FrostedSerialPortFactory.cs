/*
Copyright (c) 2014, Kevin Pope
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using MatterHackers.Agg;

namespace MatterHackers.SerialPortCommunication.FrostedSerial
{
	public class FrostedSerialPortFactory
	{
		[DllImport("SetSerial", SetLastError = true)]
		static extern int set_baud(string portName, int baud_rate);

		static bool allowPlugin = true;
		public static void DoNotAllowPlugin()
		{
			allowPlugin = false;
		}
		static FrostedSerialPortFactory instance = null;
		public static FrostedSerialPortFactory Instance
		{
			get
			{
				if(instance == null)
				{
					if (allowPlugin)
					{
						PluginFinder<FrostedSerialPortFactory> pluginFinder = new PluginFinder<FrostedSerialPortFactory>();

						foreach (FrostedSerialPortFactory plugin in pluginFinder.Plugins)
						{
							if (plugin.GetType() != typeof(FrostedSerialPortFactory))
							{
								instance = plugin;
							}
						}
					}
					
					if (instance == null)
					{
						instance = new FrostedSerialPortFactory();
					}
				}

				return instance;
			}
		}

		protected FrostedSerialPortFactory()
		{
		}

		public bool IsWindows
		{
			get
			{
				PlatformID id = Environment.OSVersion.Platform;
				return id == PlatformID.Win32Windows || id == PlatformID.Win32NT; // WinCE not supported
			}
		}

		public virtual IFrostedSerialPort Create(string serialPortName)
		{
#if __ANDROID__
			//Create an instance of a FrostedSerialPort
			IFrostedSerialPort newPort = null;
			newPort = new FrostedSerialPort(serialPortName);
			return newPort;
#else
			IFrostedSerialPort newPort = null;
			// if we can find a mac helper class (to get us 250k)
			string appBundle = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			if (File.Exists(Path.Combine(appBundle, "libFrostedSerialHelper.dylib")))
			{
				// use it
				newPort = new FrostedSerialPort(serialPortName);
			}
			else // use the c# native serial port
			{
#if USE_STANDARD_SERIAL
				newPort = new CSharpSerialPortWrapper(serialPortName);
#endif
			}

			return newPort;
#endif // ANDROID
		}

		public virtual IFrostedSerialPort CreateAndOpen(string serialPortName, int baudRate, bool DtrEnableOnConnect)
		{
#if __ANDROID__
			//Create an instance of a FrostedSerialPort and open it
			IFrostedSerialPort newPort = Create(serialPortName);

			newPort.BaudRate = baudRate;
			if (DtrEnableOnConnect)
			{
				newPort.DtrEnable = true;
			}

			// Set the read/write timeouts
			newPort.ReadTimeout = 500;
			newPort.WriteTimeout = 500;
			newPort.Open();

			return newPort;
#else
			IFrostedSerialPort newPort = Create(serialPortName);

			bool isLinux = !(newPort is FrostedSerialPort) && !IsWindows;

			// Skip BaudRate assignment on Linux to avoid Invalid Baud exception - defaults to 9600
			if (!isLinux)
			{
				newPort.BaudRate = baudRate;
			}

			if (DtrEnableOnConnect)
			{
				newPort.DtrEnable = true;
			}

			// Set the read/write timeouts
			newPort.ReadTimeout = 500;
			newPort.WriteTimeout = 500;

			newPort.Open();

			if (isLinux)
			{
				// Once mono has enforced its ANSI baud rate policy(in SerialPort.Open), reset the baud rate to the user specified 
				// value by calling set_baud in libSetSerial.so
				set_baud(serialPortName, baudRate);
			}

			return newPort;
#endif // ANDROID
		}
	}
}
