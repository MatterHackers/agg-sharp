/*
Copyright (c) 2015, Lars Brubaker
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
using System;

namespace MatterHackers.ImageProcessing
{
    public static class RemovePreMultipliedProcess
    {
        public static void RemovePreMultiplied(ImageBuffer destImage, ImageBuffer sourceImage)
        {
            if (sourceImage.BitDepth != destImage.BitDepth)
            {
                throw new NotImplementedException("All the images have to be the same bit depth.");
            }
            if (sourceImage.Width != destImage.Width || sourceImage.Height != destImage.Height)
            {
                throw new Exception("All images must be the same size.");
            }

            switch (sourceImage.BitDepth)
            {
                case 32:
                    {
                        int height = sourceImage.Height;
                        int width = sourceImage.Width;
                        byte[] destBuffer = destImage.GetBuffer();
                        byte[] sourceBuffer = sourceImage.GetBuffer();
                        for (int y = 0; y < height; y++)
                        {
                            int sourceOffsetY = sourceImage.GetBufferOffsetY(y);
                            int destOffsetY = destImage.GetBufferOffsetY(y);

                            for (int x = 0; x < width; x++)
                            {
                                byte blue = sourceBuffer[sourceOffsetY++];
                                byte green = sourceBuffer[sourceOffsetY++];
                                byte red = sourceBuffer[sourceOffsetY++];
                                byte alpha = sourceBuffer[sourceOffsetY++];

                                if (alpha == 0)
                                {
                                    destBuffer[destOffsetY++] = 0;
                                    destBuffer[destOffsetY++] = 0;
                                    destBuffer[destOffsetY++] = 0;
                                    destBuffer[destOffsetY++] = 0;
                                }
                                else
                                {
                                    destBuffer[destOffsetY++] = (byte)(Math.Min(blue * 255, 65025) / alpha);
                                    destBuffer[destOffsetY++] = (byte)(Math.Min(green * 255, 65025) / alpha);
                                    destBuffer[destOffsetY++] = (byte)(Math.Min(red * 255, 65025) / alpha);
                                    destBuffer[destOffsetY++] = alpha;
                                }
                            }
                        }
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        public static ImageBuffer RemovePreMultiplied(this ImageBuffer normalImage)
        {
            ImageBuffer destImage = new ImageBuffer(normalImage.Width, normalImage.Height);

            RemovePreMultiplied(destImage, normalImage);

            return destImage;
        }
    }
}