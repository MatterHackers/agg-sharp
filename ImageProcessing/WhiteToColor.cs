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
	public class WhiteToColor
	{
		public static void DoWhiteToColor(ImageBuffer sourceImageAndDest, Color color)
		{
			DoWhiteToColor(sourceImageAndDest, sourceImageAndDest, color);
		}

		public static void DoWhiteToColor(ImageBuffer result, ImageBuffer imageA, Color color)
		{
			if (imageA.BitDepth != result.BitDepth)
			{
				throw new NotImplementedException("All the images have to be the same bit depth.");
			}
			if (imageA.Width != result.Width || imageA.Height != result.Height)
			{
				throw new Exception("All images must be the same size.");
			}

			switch (imageA.BitDepth)
			{
				case 32:
					{
						int height = imageA.Height;
						int width = imageA.Width;
						byte[] resultBuffer = result.GetBuffer();
						byte[] imageABuffer = imageA.GetBuffer();
						for (int y = 0; y < height; y++)
						{
							int offsetA = imageA.GetBufferOffsetY(y);
							int offsetResult = result.GetBufferOffsetY(y);

							for (int x = 0; x < width; x++)
							{
								byte amountOfWhite = imageABuffer[offsetA];
								resultBuffer[offsetResult++] = (byte)(color.blue * amountOfWhite / 255); offsetA++;
								resultBuffer[offsetResult++] = (byte)(color.green * amountOfWhite / 255); offsetA++;
								resultBuffer[offsetResult++] = (byte)(color.red * amountOfWhite / 255); offsetA++;
								resultBuffer[offsetResult++] = imageABuffer[offsetA++];
							}
						}
					}
					break;

				default:
					throw new NotImplementedException();
			}
		}

		public static ImageBuffer CreateWhiteToColor(ImageBuffer normalImage, Color color)
		{
			ImageBuffer destImage = new ImageBuffer(normalImage.Width, normalImage.Height);

			DoWhiteToColor(destImage, normalImage, color);

			return destImage;
		}
	}

	public static class ImageSetColorExtensions
	{
		/// <summary>
		/// Set all gray pixels to a given color (including setting of black and white)
		/// </summary>
		public static ImageBuffer SetToColor(this ImageBuffer source, Color color)
		{
			var dest = new ImageBuffer(source);
			SetGrayToColor.DoSetGrayToColor(dest, source, color);
			return dest;
		}
	}

	/// <summary>
	/// Set all gray pixels to a given color (including setting of black and white)
	/// </summary>
	public class SetGrayToColor
	{
		public static void DoSetGrayToColor(ImageBuffer sourceImageAndDest, Color color)
		{
			DoSetGrayToColor(sourceImageAndDest, sourceImageAndDest, color);
		}

		public static void DoSetGrayToColor(ImageBuffer result, ImageBuffer imageA, Color color)
		{
			if (imageA.BitDepth != result.BitDepth)
			{
				throw new NotImplementedException("All the images have to be the same bit depth.");
			}
			if (imageA.Width != result.Width || imageA.Height != result.Height)
			{
				throw new Exception("All images must be the same size.");
			}

			switch (imageA.BitDepth)
			{
				case 32:
					{
						int height = imageA.Height;
						int width = imageA.Width;
						byte[] resultBuffer = result.GetBuffer();
						byte[] imageABuffer = imageA.GetBuffer();
						for (int y = 0; y < height; y++)
						{
							int offsetA = imageA.GetBufferOffsetY(y);
							int offsetResult = result.GetBufferOffsetY(y);

							for (int x = 0; x < width; x++)
							{
								imageA.GetPixel(x, y).ToColorF().GetHSL(out double _, out double s, out double _);

								if (s < .1)
								{
									resultBuffer[offsetResult++] = (byte)(color.blue); offsetA++;
									resultBuffer[offsetResult++] = (byte)(color.green); offsetA++;
									resultBuffer[offsetResult++] = (byte)(color.red); offsetA++;
									resultBuffer[offsetResult++] = imageABuffer[offsetA++];
								}
								else
								{
									offsetResult += 4;
									offsetA += 4;
								}
							}
						}
					}
					break;

				default:
					throw new NotImplementedException();
			}
		}

		public static ImageBuffer CreateSetToColor(ImageBuffer normalImage, Color color)
		{
			ImageBuffer destImage = new ImageBuffer(normalImage.Width, normalImage.Height);

			DoSetGrayToColor(destImage, normalImage, color);

			return destImage;
		}
	}
}