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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using System;

namespace MatterHackers.ImageProcessing
{
    public static class WhiteToColorProcess
    {
        public static void ConvertWhiteToAlpha(ImageBuffer destImage, ImageBuffer sourceImage)
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
                                byte sourceRed = sourceBuffer[sourceOffsetY];
                                byte sourceGreen = sourceBuffer[sourceOffsetY + 1];
                                byte sourceBlue = sourceBuffer[sourceOffsetY + 2];
                                byte sourceAlpha = sourceBuffer[sourceOffsetY + 3];

                                // Check if the pixel is close to white and mostly non-transparent
                                if (sourceRed > 240 && sourceGreen > 240 && sourceBlue > 240 && sourceAlpha > 200)
                                {
                                    // Set alpha to 0 (transparent)
                                    destBuffer[destOffsetY++] = 255; // R
                                    destBuffer[destOffsetY++] = 255; // G
                                    destBuffer[destOffsetY++] = 255; // B
                                    destBuffer[destOffsetY++] = 0;   // A (transparent)
                                }
                                else
                                {
                                    // Copy the original pixel if it's not white
                                    destBuffer[destOffsetY++] = sourceRed;
                                    destBuffer[destOffsetY++] = sourceGreen;
                                    destBuffer[destOffsetY++] = sourceBlue;
                                    destBuffer[destOffsetY++] = sourceAlpha;
                                }

                                sourceOffsetY += 4;
                            }
                        }
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        public static ImageBuffer WhiteToAlpha(this ImageBuffer sourceImage)
        {
            ImageBuffer destImage = new ImageBuffer(sourceImage.Width, sourceImage.Height);

            ConvertWhiteToAlpha(destImage, sourceImage);

            return destImage;
        }

        public static (ImageBuffer, string) WhiteToAlpha_GreyToColor(this ImageBuffer image, Color color)
        {
            var key = "WhiteToAlpha_GreyToColor" + color.GetLongHashCode().ToString();

            if (image == null)
            {
                return (null, key);
            }

            var newImage = image.WhiteToAlpha().GrayToColor(color);
            return (newImage, key);
        }
    }
}