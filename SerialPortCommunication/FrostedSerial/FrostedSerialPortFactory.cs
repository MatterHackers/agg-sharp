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

		internal string GetDefaultPortName()
		{
			string[] ports = FrostedSerialPortFactory.Instance.GetPortNames();
			if (ports.Length > 0)
			{
				return ports[0];
			}
			else
			{
				int p = (int)Environment.OSVersion.Platform;
				if (p == 4 || p == 128 || p == 6)
					return "ttyS0"; // Default for Unix
				else
					return "COM1"; // Default for Windows
			}
		}

		// On non-Android platforms simply return true as port access validation isn't applicable
		public virtual bool EnsureDeviceAccess()
		{
			return true;
		}

		public string[] GetPortNames(bool filter = false)
		{
			int p = (int)Environment.OSVersion.Platform;
			List<string> serial_ports = new List<string>();

			// Are we on Unix?
			if (p == 4 || p == 128 || p == 6)
			{
				string[] ttys = Directory.GetFiles("/dev/", "tty*");

				// If filtering was not requested, return the raw listing of /dev/tty* - (subsequent filtering happens in client code)
				if (!filter)
				{
					return ttys;
				}

				// Probe for Linux-styled devices: /dev/ttyS* or /dev/ttyUSB*
				foreach (string dev in ttys)
				{
					if (dev.StartsWith("/dev/ttyS") || dev.StartsWith("/dev/ttyUSB") || dev.StartsWith("/dev/ttyACM"))
					{
						serial_ports.Add(dev);
					}
					else if (dev != "/dev/tty" && dev.StartsWith("/dev/tty") && !dev.StartsWith("/dev/ttyC"))
					{
						serial_ports.Add(dev);
					}
				}
			}
			else
			{
#if USE_STANDARD_SERIAL
				using (RegistryKey subkey = Registry.LocalMachine.OpenSubKey("HARDWARE\\DEVICEMAP\\SERIALCOMM"))
				{
					if (subkey != null)
					{
						string[] names = subkey.GetValueNames();
						foreach (string value in names)
						{
							string port = subkey.GetValue(value, "").ToString();
							if (port != "")
								serial_ports.Add(port);
						}
					}
				}
#endif
			}
			return serial_ports.ToArray();
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
		}

		public virtual IFrostedSerialPort CreateAndOpen(string serialPortName, int baudRate, bool DtrEnableOnConnect)
		{
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
		}
	}
}
