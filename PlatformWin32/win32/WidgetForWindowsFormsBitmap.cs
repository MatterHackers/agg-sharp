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
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MatterHackers.Agg.UI
{
    public class WidgetForWindowsFormsBitmap : WidgetForWindowsFormsAbstract
    {
        internal WindowsFormsBitmapBackBuffer bitmapBackBuffer = new WindowsFormsBitmapBackBuffer();

        public static Stopwatch copyTime = new Stopwatch();

        public WidgetForWindowsFormsBitmap(SystemWindow childSystemWindow)
            : base(childSystemWindow)
        {
            WindowsFormsWindow = new WindowsFormBitmap(this, childSystemWindow);
        }

        public override void OnBoundsChanged(EventArgs e)
        {
            if (childSystemWindow != null)
            {
                System.Drawing.Imaging.PixelFormat format = System.Drawing.Imaging.PixelFormat.Undefined;
                switch (childSystemWindow.BitDepth)
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
            base.OnBoundsChanged(e);
        }

        public override void Invalidate(RectangleDouble rectToInvalidate)
        {
            base.Invalidate(rectToInvalidate);
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
            WindowsFormsWindow.ClientSize = clientSize;

            if (!childSystemWindow.Resizable)
            {
                WindowsFormsWindow.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
                WindowsFormsWindow.MaximizeBox = false;
            }

            clientSize.Width = (int)childSystemWindow.Width;
            clientSize.Height = (int)childSystemWindow.Height;
            WindowsFormsWindow.ClientSize = clientSize;

            OnInitialize();
        }

        public override void OnInitialize()
        {
            bitmapBackBuffer.Initialize((int)childSystemWindow.Width, (int)childSystemWindow.Height, childSystemWindow.BitDepth);

            NewGraphics2D().Clear(new RGBA_Floats(1, 1, 1, 1));

            base.OnInitialize();
        }
    }
}
