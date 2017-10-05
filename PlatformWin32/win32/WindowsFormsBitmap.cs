//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
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
using System.Drawing;
using MatterHackers.Agg.Platform;

namespace MatterHackers.Agg.UI
{
	public class BitmapSystemWindow : WinformsSystemWindow
	{
		public BitmapSystemWindow(SystemWindow childSystemWindow)
		{
			this.AggSystemWindow = childSystemWindow;
		}

		public override void CopyBackBufferToScreen(Graphics displayGraphics)
		{
			RectangleInt intRect = new RectangleInt(0, 0, (int)AggSystemWindow.Width, (int)AggSystemWindow.Height);
			bitmapBackBuffer.UpdateHardwareSurface(intRect);

			if (AggContext.OperatingSystem != OSType.Windows)
			{
				//displayGraphics.DrawImage(aggBitmapAppWidget.bitmapBackBuffer.windowsBitmap, windowsRect, windowsRect, GraphicsUnit.Pixel);  // around 250 ms for full screen
				displayGraphics.DrawImageUnscaled(bitmapBackBuffer.windowsBitmap, 0, 0); // around 200 ms for full screen
			}
			else
			{
				using (Graphics bitmapGraphics = Graphics.FromImage(bitmapBackBuffer.windowsBitmap))
				{
					IntPtr displayHDC = displayGraphics.GetHdc();
					IntPtr bitmapHDC = bitmapGraphics.GetHdc();

					IntPtr hBitmap = bitmapBackBuffer.windowsBitmap.GetHbitmap();
					IntPtr hOldObject = NativeMethods.SelectObject(bitmapHDC, hBitmap);

					int result = NativeMethods.BitBlt(displayHDC, 0, 0, bitmapBackBuffer.windowsBitmap.Width, bitmapBackBuffer.windowsBitmap.Height, bitmapHDC, 0, 0, NativeMethods.SRCCOPY);

					NativeMethods.SelectObject(bitmapHDC, hOldObject);
					NativeMethods.DeleteObject(hBitmap);

					bitmapGraphics.ReleaseHdc(bitmapHDC);
					displayGraphics.ReleaseHdc(displayHDC);
				}
			}
		}

		internal WindowsFormsBitmapBackBuffer bitmapBackBuffer = new WindowsFormsBitmapBackBuffer();

		public override void BoundsChanged(EventArgs e)
		{
			if (AggSystemWindow != null)
			{
				System.Drawing.Imaging.PixelFormat format = System.Drawing.Imaging.PixelFormat.Undefined;
				switch (AggSystemWindow.BitDepth)
				{
					case 24:
						format = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
						break;

					case 32:
						format = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
						break;

					default:
						throw new NotImplementedException();
				}

				int bitDepth = System.Drawing.Image.GetPixelFormatSize(format);
				bitmapBackBuffer.Initialize((int)Width, (int)Height, bitDepth);
				NewGraphics2D().Clear(new RGBA_Floats(1, 1, 1, 1));
			}

			base.BoundsChanged(e);
		}

		public override Graphics2D NewGraphics2D()
		{
			Graphics2D graphics2D;
			if (bitmapBackBuffer.backingImageBufferByte != null)
			{
				graphics2D = bitmapBackBuffer.backingImageBufferByte.NewGraphics2D();
			}
			else
			{
				graphics2D = bitmapBackBuffer.backingImageBufferFloat.NewGraphics2D();
			}
			graphics2D.PushTransform();
			return graphics2D;
		}

		public void Init(SystemWindow childSystemWindow)
		{
			System.Drawing.Size clientSize = new System.Drawing.Size();
			clientSize.Width = (int)childSystemWindow.Width;
			clientSize.Height = (int)childSystemWindow.Height;
			this.ClientSize = clientSize;

			if (!childSystemWindow.Resizable)
			{
				this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
				this.MaximizeBox = false;
			}

			clientSize.Width = (int)childSystemWindow.Width;
			clientSize.Height = (int)childSystemWindow.Height;
			this.ClientSize = clientSize;

			// OnInitialize(); {{

			bitmapBackBuffer.Initialize((int)childSystemWindow.Width, (int)childSystemWindow.Height, childSystemWindow.BitDepth);

			NewGraphics2D().Clear(new RGBA_Floats(1, 1, 1, 1));

			// OnInitialize(); }}

		} 
	}
}
