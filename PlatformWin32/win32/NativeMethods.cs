using System;
using System.Runtime.InteropServices;

namespace MatterHackers.Agg.UI
{
	public static class NativeMethods
	{
		// or the code below which calls BitBlt directly running at 17 ms for full screen.
		public const int SRCCOPY = 0xcc0020;

		[DllImport("gdi32.dll")]
		public static extern bool DeleteObject(IntPtr hObject);

		[DllImport("gdi32.dll")]
		public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

		[DllImport("gdi32.dll")]
		public static extern int BitBlt(
			IntPtr hdcDest,     // handle to destination DC (device context)
			int nXDest,         // x-coord of destination upper-left corner
			int nYDest,         // y-coord of destination upper-left corner
			int nWidth,         // width of destination rectangle
			int nHeight,        // height of destination rectangle
			IntPtr hdcSrc,      // handle to source DC
			int nXSrc,          // x-coordinate of source upper-left corner
			int nYSrc,          // y-coordinate of source upper-left corner
			System.Int32 dwRop  // raster operation code
			);

		[DllImport("user32.dll")]
		internal static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDc);

		[DllImport("gdi32.dll")]
		internal static extern IntPtr DeleteDC(IntPtr hDc);

		[DllImport("gdi32.dll")]
		internal static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

		[DllImport("gdi32.dll")]
		internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);

		[DllImport("user32.dll")]
		internal static extern IntPtr GetDesktopWindow();

		[DllImport("user32.dll")]
		internal static extern IntPtr GetWindowDC(IntPtr ptr);

		[DllImport("User32.Dll")]
		internal static extern long SetCursorPos(int x, int y);

		[DllImport("user32.dll")]
		internal static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);
	}
}
