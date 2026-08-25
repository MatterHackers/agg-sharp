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
using System.Globalization;
using System.IO;
using MatterHackers.Agg.Platform.Linux;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// The Linux/X11 <see cref="IOsInformationProvider"/>. Exists for the same reason
	/// <c>MacInformationProvider</c> does: the Windows one is built on
	/// <c>System.Windows.Forms.Screen</c> and <c>Microsoft.VisualBasic.Devices.ComputerInfo</c>, neither of
	/// which will even load off Windows.
	/// </summary>
	public class LinuxInformationProvider : IOsInformationProvider
	{
		/// <summary>
		/// What <see cref="DesktopSize"/> reports when there is no X server to ask. A headless test run and
		/// a CI container both land here, and every caller of DesktopSize is sizing or centring a window -
		/// so a plausible desktop keeps that arithmetic sane, where a 0x0 one turns into a zero-size window
		/// or a division by zero far away from here.
		/// </summary>
		private static readonly Point2D HeadlessDesktopSize = new Point2D(1920, 1080);

		/// <summary>The DPI X11 and every toolkit on it treat as unscaled.</summary>
		private const double BaselineDpi = 96.0;

		public LinuxInformationProvider()
		{
			// One connection for both reads, closed before the constructor returns. Holding a Display open
			// for the life of the provider would mean a second connection to the server alongside the
			// window host's, and a file descriptor that nothing ever closes.
			IntPtr display = TryOpenDisplay();
			try
			{
				this.DesktopSize = ReadDesktopSize(display);
				this.DisplayScale = ReadDisplayScale(display);
			}
			finally
			{
				if (display != IntPtr.Zero)
				{
					Xlib.XCloseDisplay(display);
				}
			}
		}

		public OSType OperatingSystem => OSType.X11;

		/// <summary>
		/// The screen size in <em>device pixels</em>, to match the space every other size in agg is
		/// expressed in. This is the whole screen, not a work area: X11 itself has no concept of one, and
		/// the <c>_NET_WORKAREA</c> that window managers publish is a convention, is often absent, and is
		/// meaningless on a multi-head setup. Read once at construction, like the mac provider's - a display
		/// change mid-run is not something the toolkit reacts to today.
		/// </summary>
		public Point2D DesktopSize { get; }

		/// <summary>
		/// The user's display scaling. X11 has no per-display scale factor of its own, so this is
		/// reconstructed from what the desktop environment wrote down: <c>Xft.dpi</c> over 96 first, then
		/// <c>GDK_SCALE</c>, then 1. Read once at construction, like <see cref="DesktopSize"/>.
		/// </summary>
		public double DisplayScale { get; }

		/// <summary>
		/// Total physical RAM in bytes, from <c>/proc/meminfo</c>'s <c>MemTotal</c> line - the only portable
		/// source on Linux (<c>sysconf</c> would need a P/Invoke to get the same number less legibly).
		/// </summary>
		public long PhysicalMemory
		{
			get
			{
				try
				{
					foreach (string line in File.ReadLines("/proc/meminfo"))
					{
						if (!line.StartsWith("MemTotal:", StringComparison.Ordinal))
						{
							continue;
						}

						// The line is "MemTotal:       16311476 kB" - the unit is always kB, and has been
						// since the file existed, but the whitespace run between the fields is not fixed.
						string[] fields = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
						if (fields.Length >= 2
							&& long.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long kilobytes))
						{
							return kilobytes * 1024;
						}

						break;
					}
				}
				catch (IOException)
				{
					// /proc is not mounted, which is possible inside a minimal container. Zero is what the
					// mac provider reports when its own query fails.
				}
				catch (UnauthorizedAccessException)
				{
				}

				return 0;
			}
		}

		/// <summary>
		/// Opens the default display, or returns zero when there is none. A missing libX11 is treated the
		/// same as a missing server: a machine with no X libraries installed can still legitimately run the
		/// non-UI parts of this stack, and this provider is constructed from wherever <c>AggContext</c> is
		/// first touched - which under a test runner is a thread pool worker with no display at all.
		/// </summary>
		private static IntPtr TryOpenDisplay()
		{
			try
			{
				return Xlib.XOpenDisplay(null);
			}
			catch (DllNotFoundException)
			{
				return IntPtr.Zero;
			}
			catch (EntryPointNotFoundException)
			{
				return IntPtr.Zero;
			}
		}

		private static Point2D ReadDesktopSize(IntPtr display)
		{
			if (display == IntPtr.Zero)
			{
				return HeadlessDesktopSize;
			}

			int screen = Xlib.XDefaultScreen(display);
			int width = Xlib.XDisplayWidth(display, screen);
			int height = Xlib.XDisplayHeight(display, screen);

			// A server that answers at all always has a positive screen size; the guard is here so a broken
			// answer degrades to the headless default rather than to a zero-size desktop.
			if (width <= 0 || height <= 0)
			{
				return HeadlessDesktopSize;
			}

			return new Point2D(width, height);
		}

		private static double ReadDisplayScale(IntPtr display)
		{
			// Xft.dpi is what every desktop environment writes when the user picks a scaling factor, and it
			// is the only one of these that can express a fractional scale.
			if (display != IntPtr.Zero
				&& Xlib.TryReadXftDpi(display, out double dpi)
				&& dpi > 0)
			{
				return dpi / BaselineDpi;
			}

			// GDK_SCALE is GTK's integer-only override, and is the one thing that is still set when an app
			// is launched from a scaled session with no resource database (a bare WM, or a login that never
			// ran xrdb). It is integral by definition, so it is parsed as an int and not a double.
			string gdkScale = Environment.GetEnvironmentVariable("GDK_SCALE");
			if (!string.IsNullOrWhiteSpace(gdkScale)
				&& int.TryParse(gdkScale.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int scale)
				&& scale > 0)
			{
				return scale;
			}

			return 1.0;
		}
	}
}
