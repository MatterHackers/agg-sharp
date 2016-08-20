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
using System.Drawing;
using System.Runtime.InteropServices;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;

namespace MatterHackers.GuiAutomation
{
	public static class NativeMethods
	{
		public const int MOUSEEVENTF_LEFTDOWN = 0x02;
		public const int MOUSEEVENTF_LEFTUP = 0x04;
		public const int MOUSEEVENTF_RIGHTDOWN = 0x08;
		public const int MOUSEEVENTF_RIGHTUP = 0x10;
		public const int MOUSEEVENTF_MIDDLEDOWN = 0x20;
		public const int MOUSEEVENTF_MIDDLEUP = 0x40;

#if !__ANDROID__
		// P/Invoke declarations
		[DllImport("gdi32.dll")]
		internal static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSource, int xSrc, int ySrc, CopyPixelOperation rop);

		[DllImport("user32.dll")]
		internal static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDc);

		[DllImport("gdi32.dll")]
		internal static extern IntPtr DeleteDC(IntPtr hDc);

		[DllImport("gdi32.dll")]
		internal static extern IntPtr DeleteObject(IntPtr hDc);

		[DllImport("gdi32.dll")]
		internal static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

		[DllImport("gdi32.dll")]
		internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);

		[DllImport("gdi32.dll")]
		internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr bmp);

		[DllImport("user32.dll")]
		internal static extern IntPtr GetDesktopWindow();

		[DllImport("user32.dll")]
		internal static extern IntPtr GetWindowDC(IntPtr ptr);

		[DllImport("User32.Dll")]
		internal static extern long SetCursorPos(int x, int y);

		[DllImport("user32.dll")]
		internal static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);
#endif
	}
}