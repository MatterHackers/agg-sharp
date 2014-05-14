//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# Port port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software 
// is granted provided this copyright notice appears in all copies. 
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
// OS Type detection code author:
//       Jonathan Pobst <monkey@jpobst.com>
// 
// Copyright (c) 2010 Jonathan Pobst
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

using MatterHackers.Agg;
using MatterHackers.RenderOpenGl;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;

namespace MatterHackers.Agg.UI
{
    public class WindowsFormBitmap : WindowsFormsAbstract
    {
        public WindowsFormBitmap(AbstractOsMappingWidget app, SystemWindow childSystemWindow)
        {
            SetUpFormsWindow(app, childSystemWindow);

            HookWindowsInputAndSendToWidget communication = new HookWindowsInputAndSendToWidget(this, aggAppWidget);
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        
        [System.Runtime.InteropServices.DllImportAttribute("gdi32.dll")]
        public static extern System.IntPtr SelectObject(System.IntPtr hdc, System.IntPtr h);

        [System.Runtime.InteropServices.DllImportAttribute("gdi32.dll")]
        private static extern int BitBlt(
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
        
        public override void CopyBackBufferToScreen(Graphics displayGraphics)
        {
            WidgetForWindowsFormsBitmap aggBitmapAppWidget = ((WidgetForWindowsFormsBitmap)aggAppWidget);

            RectangleInt intRect = new RectangleInt(0, 0, (int)aggAppWidget.Width, (int)aggAppWidget.Height);
            aggBitmapAppWidget.bitmapBackBuffer.UpdateHardwareSurface(intRect);

            WidgetForWindowsFormsBitmap.copyTime.Restart();

			if(GetOSType() != OSType.Windows)
			{
	            //displayGraphics.DrawImage(aggBitmapAppWidget.bitmapBackBuffer.windowsBitmap, windowsRect, windowsRect, GraphicsUnit.Pixel);  // around 250 ms for full screen
	            displayGraphics.DrawImageUnscaled(aggBitmapAppWidget.bitmapBackBuffer.windowsBitmap, 0, 0); // around 200 ms for full screnn
			}
			else
			{
	            // or the code below which calls BitBlt directly running at 17 ms for full screnn.
	            const int SRCCOPY = 0xcc0020;

	            using (Graphics bitmapGraphics = Graphics.FromImage(aggBitmapAppWidget.bitmapBackBuffer.windowsBitmap))
	            {
	                IntPtr displayHDC = displayGraphics.GetHdc();
	                IntPtr bitmapHDC = bitmapGraphics.GetHdc();

	                IntPtr hBitmap = aggBitmapAppWidget.bitmapBackBuffer.windowsBitmap.GetHbitmap();
	                IntPtr hOldObject = SelectObject(bitmapHDC, hBitmap);

	                int result = BitBlt(displayHDC, 0, 0, aggBitmapAppWidget.bitmapBackBuffer.windowsBitmap.Width, aggBitmapAppWidget.bitmapBackBuffer.windowsBitmap.Height, bitmapHDC, 0, 0, SRCCOPY);

	                SelectObject(bitmapHDC, hOldObject);
	                DeleteObject(hBitmap);

	                bitmapGraphics.ReleaseHdc(bitmapHDC);
	                displayGraphics.ReleaseHdc(displayHDC);
	            }
			}
            WidgetForWindowsFormsBitmap.copyTime.Stop();
        }
    }
}