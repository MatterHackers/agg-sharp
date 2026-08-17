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
using System.Runtime.InteropServices;
using MatterHackers.Agg.UI;

using static MatterHackers.Agg.Platform.Mac.ObjC;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// The macOS <see cref="IOsInformationProvider"/>. Exists because the Windows one is built on
	/// <c>System.Windows.Forms.Screen</c> and <c>Microsoft.VisualBasic.Devices.ComputerInfo</c>, neither of
	/// which will even load off Windows.
	/// </summary>
	public class MacInformationProvider : IOsInformationProvider
	{
		/// <summary><c>sysctlbyname</c>, for <c>hw.memsize</c>. The only way to total physical RAM on macOS.</summary>
		[DllImport("libc", SetLastError = true)]
		private static extern int sysctlbyname(
			[MarshalAs(UnmanagedType.LPUTF8Str)] string name,
			out long value,
			ref nuint length,
			IntPtr newValue,
			nuint newLength);

		public MacInformationProvider()
		{
			this.DesktopSize = ReadDesktopSize();
			this.DisplayScale = ReadBackingScaleFactor();
		}

		public OSType OperatingSystem => OSType.Mac;

		/// <summary>
		/// The usable desktop area in <em>device pixels</em>, to match the space every other size in agg is
		/// expressed in. <c>visibleFrame</c> rather than <c>frame</c> so the menu bar and the Dock are
		/// excluded, which is what the Windows provider's <c>WorkingArea</c> means.
		/// </summary>
		public Point2D DesktopSize { get; }

		/// <summary>
		/// The main screen's <c>backingScaleFactor</c> - 2 on every Retina display, 1 on an external
		/// non-Retina monitor. Read once at construction, like <see cref="DesktopSize"/>: a display change
		/// mid-run is not something the toolkit reacts to today.
		/// </summary>
		public double DisplayScale { get; }

		public long PhysicalMemory
		{
			get
			{
				nuint length = sizeof(long);
				if (sysctlbyname("hw.memsize", out long memSize, ref length, IntPtr.Zero, 0) != 0)
				{
					return 0;
				}

				return memSize;
			}
		}

		/// <summary>
		/// Reads the screen on the main thread. NSScreen is AppKit, so it obeys AppKit's main-thread rule,
		/// and this provider is constructed from wherever <c>AggContext</c> is first touched - which under a
		/// test runner is a thread pool worker. <c>MainThreadDispatcher</c> runs it inline when nothing has
		/// claimed the main thread, so an ordinary application pays nothing for this.
		/// </summary>
		/// <summary>Reads the main screen's backing scale on the main thread. See <see cref="ReadDesktopSize"/>.</summary>
		private static double ReadBackingScaleFactor()
		{
			return MainThreadDispatcher.Invoke(() =>
			{
				EnsureFrameworksLoaded();

				IntPtr screen = Send_r(Class("NSScreen"), Sel("mainScreen"));
				if (screen == IntPtr.Zero)
				{
					return 1.0;
				}

				double scale = Send_d(screen, Sel("backingScaleFactor"));
				return scale > 0 ? scale : 1.0;
			});
		}

		private static Point2D ReadDesktopSize()
		{
			return MainThreadDispatcher.Invoke(() =>
			{
				EnsureFrameworksLoaded();

				IntPtr screen = Send_r(Class("NSScreen"), Sel("mainScreen"));
				if (screen == IntPtr.Zero)
				{
					return new Point2D(0, 0);
				}

				var visible = Send_R(screen, Sel("visibleFrame"));
				double scale = Send_d(screen, Sel("backingScaleFactor"));
				if (scale <= 0)
				{
					scale = 1;
				}

				return new Point2D(
					(int)Math.Round(visible.Size.Width * scale),
					(int)Math.Round(visible.Size.Height * scale));
			});
		}
	}
}
