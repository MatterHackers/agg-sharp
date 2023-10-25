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

								if (s < .01)
								{
									if (destIntensity == DestIntensity.FromColor)
									{
										destBuffer[destOffsetY++] = (byte)(color.blue); sourceOffsetY++;
										destBuffer[destOffsetY++] = (byte)(color.green); sourceOffsetY++;
										destBuffer[destOffsetY++] = (byte)(color.red); sourceOffsetY++;
										destBuffer[destOffsetY++] = sourceBuffer[sourceOffsetY++];
									}
									else
									{
										byte intensity = sourceBuffer[sourceOffsetY];
										destBuffer[destOffsetY++] = (byte)(color.blue * intensity / 255); sourceOffsetY++;
										destBuffer[destOffsetY++] = (byte)(color.green * intensity / 255); sourceOffsetY++;
										destBuffer[destOffsetY++] = (byte)(color.red * intensity / 255); sourceOffsetY++;
										destBuffer[destOffsetY++] = sourceBuffer[sourceOffsetY++];
									}
								}
								else
								{
                                    destBuffer[destOffsetY++] = sourceBuffer[sourceOffsetY++];
									destBuffer[destOffsetY++] = sourceBuffer[sourceOffsetY++];
									destBuffer[destOffsetY++] = sourceBuffer[sourceOffsetY++];
                                    destBuffer[destOffsetY++] = sourceBuffer[sourceOffsetY++];                                    
                                }
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