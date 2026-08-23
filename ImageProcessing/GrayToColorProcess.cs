/*
Copyright (c) 2026, Lars Brubaker
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
    public enum DestIntensity
    {
        FromColor,
        FromSource
    }

    /// <summary>
    /// Set all gray pixels to a given color (including setting of black and white)
    /// </summary>
    public static class GrayToColorProcess
    {
        public static void GrayToColor(ImageBuffer destImage, ImageBuffer sourceImage, Color color, DestIntensity destIntensity)
        {
            if (sourceImage.BitDepth != destImage.BitDepth)
            {
                throw new NotImplementedException("All the images have to be the same bit depth.");
            }
            if (sourceImage.Width != destImage.Width || sourceImage.Height != destImage.Height)
            {
                throw new Exception("All images must be the same size.");
            }

            // A premultiplied destination expects the color already scaled by alpha, and a fully
            // transparent pixel must be completely clear either way - its blender adds the source
            // color outright, so any ink left in an invisible pixel paints at full strength.
            bool destIsPreMultiplied = destImage.GetRecieveBlender() is BlenderPreMultBGRA;

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
                                sourceImage.GetPixel(x, y).ToColorF().GetHSL(out double _, out double s, out double _);

                                byte blue;
                                byte green;
                                byte red;
                                byte alpha = sourceBuffer[sourceOffsetY + 3];

                                if (s < .01)
                                {
                                    if (destIntensity == DestIntensity.FromColor)
                                    {
                                        blue = color.blue;
                                        green = color.green;
                                        red = color.red;
                                    }
                                    else
                                    {
                                        byte intensity = sourceBuffer[sourceOffsetY];
                                        blue = (byte)(color.blue * intensity / 255);
                                        green = (byte)(color.green * intensity / 255);
                                        red = (byte)(color.red * intensity / 255);
                                    }
                                }
                                else
                                {
                                    blue = sourceBuffer[sourceOffsetY + 0];
                                    green = sourceBuffer[sourceOffsetY + 1];
                                    red = sourceBuffer[sourceOffsetY + 2];
                                }

                                if (alpha == 0)
                                {
                                    blue = 0;
                                    green = 0;
                                    red = 0;
                                }
                                else if (destIsPreMultiplied && alpha < 255)
                                {
                                    blue = (byte)(blue * alpha / 255);
                                    green = (byte)(green * alpha / 255);
                                    red = (byte)(red * alpha / 255);
                                }

                                destBuffer[destOffsetY + 0] = blue;
                                destBuffer[destOffsetY + 1] = green;
                                destBuffer[destOffsetY + 2] = red;
                                destBuffer[destOffsetY + 3] = alpha;

                                sourceOffsetY += 4;
                                destOffsetY += 4;
                            }
                        }
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        public static ImageBuffer GrayToColor(this ImageBuffer sourceImage, Color color, DestIntensity destIntensity = DestIntensity.FromColor)
        {
            ImageBuffer destImage = new ImageBuffer(sourceImage.Width, sourceImage.Height);

            GrayToColor(destImage, sourceImage, color, destIntensity);

            return destImage;
        }
    }
}