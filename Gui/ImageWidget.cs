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

using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.UI
{
    public class ImageWidget : GuiWidget
    {
        ImageBuffer image;

        public bool ForcePixelAlignment { get; set; }

        public ImageWidget(int width, int height)
        {
            ForcePixelAlignment = true;
            LocalBounds = new RectangleDouble(0, 0, width, height);
        }

        public ImageWidget(ImageBuffer initialImage)
            : this(initialImage.Width, initialImage.Height)
        {
            Image = initialImage;
        }

        public ImageBuffer Image
        {
            get
            {
                return image;
            }

            set
            {
                image = value;
                LocalBounds = new RectangleDouble(0, 0, image.Width, image.Height);
            }
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            if (image != null)
            {
                RectangleDouble screenBounds = TransformRectangleToScreenSpace(LocalBounds);
                double pixelAlignXAdjust = 0;
                double pixelAlignYAdjust = 0;
                if (ForcePixelAlignment)
                {
                    pixelAlignXAdjust = screenBounds.Left - (int)screenBounds.Left;
                    pixelAlignYAdjust = screenBounds.Bottom - (int)screenBounds.Bottom;
                }
                graphics2D.Render(image, -pixelAlignXAdjust, -pixelAlignYAdjust);
            }
            base.OnDraw(graphics2D);
        }
    }
}
