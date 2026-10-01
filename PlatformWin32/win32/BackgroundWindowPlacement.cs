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
using System.Text;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The Win32 half of <see cref="IPlatformWindow.ShowWindowsInBackground"/>: how a WinForms window opens
	/// without taking the foreground from the window the user is working in.
	/// </summary>
	internal static class BackgroundWindowPlacement
	{
		internal const int WS_EX_TOPMOST = 0x00000008;

		private const uint SWP_NOSIZE = 0x0001;

		private const uint SWP_NOMOVE = 0x0002;

		private const uint SWP_NOACTIVATE = 0x0010;

		private const int GWL_EXSTYLE = -20;

		private const uint GW_OWNER = 4;

		/// <summary>
		/// The desktop and taskbar: "foreground" when the user has clicked the desktop, but inserting under
		/// one of them would bury the window beneath the wallpaper.
		/// </summary>
		private static readonly string[] ShellWindowClasses = { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };

		/// <summary>
		/// Where the window the user is working in comes from. A seam so a test can stand a window of its own
		/// in for it rather than depend on whatever the machine running the test has in front.
		/// </summary>
		internal static Func<IntPtr> ForegroundWindow { get; set; } = GetForegroundWindow;

		/// <summary>
		/// The extended style a window is created with: in the background never WS_EX_TOPMOST, which would
		/// float it over everything the user has open.
		/// </summary>
		/// <remarks>
		/// Deliberately not WS_EX_NOACTIVATE: the user clicking a test window to watch the run should raise it
		/// as usual, and NOACTIVATE would also drop its taskbar button. Not taking the foreground on its own is
		/// ShowWithoutActivation's job plus never calling Activate/BringToFront.
		/// </remarks>
		internal static int ExtendedStyle(int exStyle, bool inBackground)
			=> inBackground ? exStyle & ~WS_EX_TOPMOST : exStyle;

		/// <summary>
		/// Moves a window - before it is shown, so it never flashes on top - directly beneath the window the
		/// user is working in, without activating it.
		/// </summary>
		/// <remarks>
		/// The insert-after window is the foreground's root owner: inserting after an owned dialog would wedge
		/// this window between the dialog and the window that owns it. Left alone (so the window opens on top,
		/// still without taking focus) when there is no usable foreground: nothing, this window, an invisible
		/// window, the desktop or taskbar - under those the window would be lost below the wallpaper - or a
		/// topmost window, which a normal window is already below and inserting after which would make this
		/// one topmost too.
		/// </remarks>
		internal static void PlaceBehindForeground(IntPtr handle)
		{
			IntPtr foreground = ForegroundWindow();
			if (foreground == IntPtr.Zero)
			{
				return;
			}

			// GW_OWNER rather than GetAncestor(GA_ROOTOWNER): that walks GetParent, which only reports the
			// owner of a WS_POPUP window, and an owned WinForms form (and most dialogs) is WS_OVERLAPPED.
			for (IntPtr owner = GetWindow(foreground, GW_OWNER); owner != IntPtr.Zero; owner = GetWindow(foreground, GW_OWNER))
			{
				foreground = owner;
			}

			if (foreground == handle
				|| !IsWindowVisible(foreground)
				|| IsShellWindow(foreground)
				|| (GetWindowLong(foreground, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0)
			{
				return;
			}

			SetWindowPos(handle, foreground, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
		}

		private static bool IsShellWindow(IntPtr window)
		{
			var className = new StringBuilder(64);
			GetClassName(window, className, className.Capacity);
			return Array.IndexOf(ShellWindowClasses, className.ToString()) >= 0;
		}

		[DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		private static extern IntPtr GetWindow(IntPtr hWnd, uint command);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool IsWindowVisible(IntPtr hWnd);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

		[DllImport("user32.dll")]
		private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
	}
}
