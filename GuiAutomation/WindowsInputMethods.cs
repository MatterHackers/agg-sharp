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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using System;
using System.Drawing;
#if !__ANDROID__
using System.Drawing.Imaging;
#endif

namespace MatterHackers.GuiAutomation
{
#if !__ANDROID__
	public class WindowsInputMethods : IInputMethod
	{
		public bool LeftButtonDown { get; private set; }

		public void CreateMouseEvent(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo)
		{
			this.LeftButtonDown = (dwFlags == NativeMethods.MOUSEEVENTF_LEFTDOWN);

			NativeMethods.mouse_event(dwFlags, dx, dy, cButtons, dwExtraInfo);
		}

		public void SetCursorPosition(int x, int y)
		{
			NativeMethods.SetCursorPos(x, y);
		}

		public Point2D CurrentMousePosition()
		{
			Point2D mousePos = new Point2D(System.Windows.Forms.Control.MousePosition.X, System.Windows.Forms.Control.MousePosition.Y);
			return mousePos;
		}

		public void Dispose()
		{
		}

		public int GetCurrentScreenHeight()
		{
			Size sz = new Size();
			return sz.Height;
		}

		public ImageBuffer GetCurrentScreen()
		{
			ImageBuffer screenCapture = new ImageBuffer();

			Size sz = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Size;
			IntPtr hDesk = NativeMethods.GetDesktopWindow();
			IntPtr hSrce = NativeMethods.GetWindowDC(hDesk);
			IntPtr hDest = NativeMethods.CreateCompatibleDC(hSrce);
			IntPtr hBmp = NativeMethods.CreateCompatibleBitmap(hSrce, sz.Width, sz.Height);
			IntPtr hOldBmp = NativeMethods.SelectObject(hDest, hBmp);
			bool b = NativeMethods.BitBlt(hDest, 0, 0, sz.Width, sz.Height, hSrce, 0, 0, CopyPixelOperation.SourceCopy | CopyPixelOperation.CaptureBlt);
			Bitmap bmpScreenCapture = Bitmap.FromHbitmap(hBmp);
			NativeMethods.SelectObject(hDest, hOldBmp);
			NativeMethods.DeleteObject(hBmp);
			NativeMethods.DeleteDC(hDest);
			NativeMethods.ReleaseDC(hDesk, hSrce);

			//bmpScreenCapture.Save("bitmapsave.png");

			screenCapture = new ImageBuffer(bmpScreenCapture.Width, bmpScreenCapture.Height);
			BitmapData bitmapData = bmpScreenCapture.LockBits(new Rectangle(0, 0, bmpScreenCapture.Width, bmpScreenCapture.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, bmpScreenCapture.PixelFormat);

			int offset;
			byte[] buffer = screenCapture.GetBuffer(out offset);
			int bitmapDataStride = bitmapData.Stride;
			int backBufferStrideInBytes = screenCapture.StrideInBytes();
			int backBufferHeight = screenCapture.Height;
			int backBufferHeightMinusOne = backBufferHeight - 1;

			unsafe
			{
				byte* bitmapDataScan0 = (byte*)bitmapData.Scan0;
				fixed (byte* pSourceFixed = &buffer[offset])
				{
					byte* pSource = bitmapDataScan0 + bitmapDataStride * backBufferHeightMinusOne;
					byte* pDestBuffer = pSourceFixed;
					for (int y = 0; y < screenCapture.Height; y++)
					{
						int* pSourceInt = (int*)pSource;
						pSourceInt -= (bitmapDataStride * y / 4);

						int* pDestBufferInt = (int*)pDestBuffer;
						pDestBufferInt += (backBufferStrideInBytes * y / 4);

						for (int x = 0; x < screenCapture.Width; x++)
						{
							pDestBufferInt[x] = pSourceInt[x];
						}
					}
				}
			}

			bmpScreenCapture.UnlockBits(bitmapData);

			bmpScreenCapture.Dispose();

			return screenCapture;
		}

		public void Type(string textToType)
		{
			System.Windows.Forms.SendKeys.SendWait(textToType);
		}
	}
#endif
}