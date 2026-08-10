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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Runtime.InteropServices;
using MatterHackers.Agg.LcdCoverage;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// Reads the Windows desktop's font smoothing configuration and the primary display's geometry, so
	/// <see cref="LcdDisplayDetection"/> can decide whether subpixel text suits this machine.
	/// </summary>
	/// <remarks>
	/// Read once, at startup, by whoever seeds the LCD setting - there is deliberately no WM_SETTINGCHANGE
	/// listener, so a user who changes ClearType while the app is running sees the change next launch (or
	/// immediately, by flipping the app's own toggle).
	/// </remarks>
	public class WindowsLcdDisplayEnvironmentProvider : ILcdDisplayEnvironmentProvider
	{
		private const uint SPI_GETFONTSMOOTHING = 0x004A;
		private const uint SPI_GETFONTSMOOTHINGTYPE = 0x200A;
		private const uint SPI_GETFONTSMOOTHINGORIENTATION = 0x2012;

		private const int SM_REMOTESESSION = 0x1000;

		private const int ENUM_CURRENT_SETTINGS = -1;

		private const int DMDO_90 = 1;
		private const int DMDO_270 = 3;

		/// <inheritdoc/>
		public bool TryGetEnvironment(out LcdDisplayEnvironment environment)
		{
			environment = default;

			if (!OperatingSystem.IsWindows())
			{
				return false;
			}

			try
			{
				// Smoothing on/off and its style are the two the answer really hinges on; if either read
				// fails we know nothing useful and say so rather than filling in a plausible value.
				if (!SystemParametersInfo(SPI_GETFONTSMOOTHING, 0, out int smoothingEnabled, 0)
					|| !SystemParametersInfo(SPI_GETFONTSMOOTHINGTYPE, 0, out int smoothingType, 0))
				{
					return false;
				}

				// Orientation is missing on some drivers. RGB is the overwhelmingly common panel layout and
				// is also what Windows itself assumes, so an unreadable orientation is treated as RGB rather
				// than as a reason to give up the whole detection.
				if (!SystemParametersInfo(SPI_GETFONTSMOOTHINGORIENTATION, 0, out int orientation, 0))
				{
					orientation = (int)LcdStripeOrder.Rgb;
				}

				environment = new LcdDisplayEnvironment(
					fontSmoothingEnabled: smoothingEnabled != 0,
					fontSmoothingStyle: (LcdFontSmoothingStyle)smoothingType,
					stripeOrder: (LcdStripeOrder)orientation,
					isRemoteSession: GetSystemMetrics(SM_REMOTESESSION) != 0,
					displayRotatedQuarterTurn: PrimaryDisplayIsRotatedQuarterTurn());

				return true;
			}
			catch (Exception)
			{
				// DllNotFoundException / EntryPointNotFoundException on a machine without a full user32 (a
				// service host, a stripped container). Unknown display, so grayscale.
				return false;
			}
		}

		/// <summary>
		/// Whether the primary display is turned on its side, which puts its colour stripes on the vertical
		/// axis. False when the mode cannot be read - the unrotated case is the overwhelming majority.
		/// </summary>
		private static bool PrimaryDisplayIsRotatedQuarterTurn()
		{
			var deviceMode = default(DEVMODE);
			deviceMode.dmSize = (ushort)Marshal.SizeOf<DEVMODE>();

			// A null device name asks for the display the calling thread is on, which at startup is the
			// primary display.
			if (!EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref deviceMode))
			{
				return false;
			}

			return deviceMode.dmDisplayOrientation == DMDO_90
				|| deviceMode.dmDisplayOrientation == DMDO_270;
		}

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out int pvParam, uint fWinIni);

		[DllImport("user32.dll")]
		private static extern int GetSystemMetrics(int nIndex);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplaySettingsW")]
		private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

		/// <summary>
		/// The display half of Win32's DEVMODE. The printer fields (dmOrientation..dmPrintQuality, eight
		/// shorts) share a union with the four display ints declared here, so this layout is byte compatible
		/// with the full structure while naming only what a display query returns.
		/// </summary>
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct DEVMODE
		{
			private const int CCHDEVICENAME = 32;
			private const int CCHFORMNAME = 32;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
			public string dmDeviceName;

			public ushort dmSpecVersion;
			public ushort dmDriverVersion;
			public ushort dmSize;
			public ushort dmDriverExtra;
			public uint dmFields;

			public int dmPositionX;
			public int dmPositionY;
			public int dmDisplayOrientation;
			public int dmDisplayFixedOutput;

			public short dmColor;
			public short dmDuplex;
			public short dmYResolution;
			public short dmTTOption;
			public short dmCollate;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
			public string dmFormName;

			public ushort dmLogPixels;
			public uint dmBitsPerPel;
			public uint dmPelsWidth;
			public uint dmPelsHeight;
			public uint dmDisplayFlags;
			public uint dmDisplayFrequency;
			public uint dmICMMethod;
			public uint dmICMIntent;
			public uint dmMediaType;
			public uint dmDitherType;
			public uint dmReserved1;
			public uint dmReserved2;
			public uint dmPanningWidth;
			public uint dmPanningHeight;
		}
	}
}
