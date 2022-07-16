/*
Copyright (c) 2022, John Lewin, Lars Brubaker
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

using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.UI
{
    public class ThemedHoverIconButton : ThemedIconButton
    {
        private ImageBuffer normalImage;

        private ImageBuffer hoverImage;

        // Single ImageBuffer constructor creates a grayscale copy for use as the normal image
        // and uses the original as the hover image
        public ThemedHoverIconButton(ImageBuffer icon, ThemeConfig theme)
            : this(MakeGrayscale(icon), icon, theme)
        {
        }

        public ThemedHoverIconButton(ImageBuffer icon, ImageBuffer hoverIcon, ThemeConfig theme)
            : base(icon, theme)
        {
            normalImage = icon;
            hoverImage = hoverIcon;

            HAnchor = HAnchor.Absolute;
            VAnchor = VAnchor.Absolute | VAnchor.Center;
            Height = theme.ButtonHeight;
            Width = theme.ButtonHeight;

            imageWidget = new ImageWidget(icon, listenForImageChanged: false)
            {
                HAnchor = HAnchor.Center,
                VAnchor = VAnchor.Center,
            };

            AddChild(imageWidget);
        }

        public static ImageBuffer MakeGrayscale(ImageBuffer image)
        {
            var sourceImage = new ImageBuffer(image);

            var buffer = sourceImage.GetBuffer();
            int destIndex = 0;
            for (int y = 0; y < sourceImage.Height; y++)
            {
                for (int x = 0; x < sourceImage.Width; x++)
                {
                    int b = buffer[destIndex + 0];
                    int g = buffer[destIndex + 1];
                    int r = buffer[destIndex + 2];

                    int c = r * 77 + g * 151 + b * 28;
                    byte gray = (byte)(c >> 8);

                    buffer[destIndex + 0] = gray;
                    buffer[destIndex + 1] = gray;
                    buffer[destIndex + 2] = gray;

                    destIndex += 4;
                }
            }

            return sourceImage;
        }

        public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
        {
            imageWidget.Image = hoverImage;

            base.OnMouseEnterBounds(mouseEvent);
            Invalidate();
        }

        public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
        {
            imageWidget.Image = normalImage;

            base.OnMouseLeaveBounds(mouseEvent);
            Invalidate();
        }
    }
}