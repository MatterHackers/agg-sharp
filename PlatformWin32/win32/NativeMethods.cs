using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
	}
}
