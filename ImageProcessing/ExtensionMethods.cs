/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;

namespace MatterHackers.ImageProcessing
{
	public static class ExtensionMethods
	{
		/// <summary>
		/// Multiply all colors by the given color
		/// </summary>
		/// <param name="sourceImage">The image to act on</param>
		/// <param name="color">The color to use</param>
		/// <returns>A new modified image</returns>
		public static ImageBuffer Multiply(this ImageBuffer sourceImage, RGBA_Bytes color)
		{
			var outputImage = new ImageBuffer(sourceImage);

			switch (outputImage.BitDepth)
			{
				case 32:
					int height = outputImage.Height;
					int width = outputImage.Width;
					byte[] imageABuffer = outputImage.GetBuffer();
					for (int y = 0; y < height; y++)
					{
						int offsetA = outputImage.GetBufferOffsetY(y);

						for (int x = 0; x < width; x++)
						{
							imageABuffer[offsetA + 0] = (byte)((imageABuffer[offsetA + 0] * color.blue) / 255);
							imageABuffer[offsetA + 1] = (byte)((imageABuffer[offsetA + 1] * color.green) / 255);
							imageABuffer[offsetA + 2] = (byte)((imageABuffer[offsetA + 2] * color.red) / 255);
							imageABuffer[offsetA + 3] = (byte)((imageABuffer[offsetA + 3] * color.alpha) / 255);
							offsetA += 4;
						}
					}

					break;

				default:
					throw new NotImplementedException();
			}

			return outputImage;
		}

		public static ImageBuffer AjustAlpha(this ImageBuffer sourceImage, double factor)
		{
			var outputImage = new ImageBuffer(sourceImage);

			switch (outputImage.BitDepth)
			{
				case 32:
					{
						int height = outputImage.Height;
						int width = outputImage.Width;
						byte[] imageABuffer = outputImage.GetBuffer();

						for (int y = 0; y < height; y++)
						{
							int offsetA = outputImage.GetBufferOffsetY(y);

							for (int x = 0; x < width; x++)
							{
								var alpha = imageABuffer[offsetA + 3];
								if (alpha > 0)
								{
									imageABuffer[offsetA + 3] = (byte) (alpha * factor);
								}

								offsetA += 4;
							}
						}

						outputImage.SetRecieveBlender(new BlenderPreMultBGRA());
					}
					break;

				default:
					throw new NotImplementedException();
			}

			return outputImage;
		}

		public static ImageBuffer ReplaceColor(this ImageBuffer sourceImage, RGBA_Bytes existingColor, RGBA_Bytes newColor, bool keepExistingAlpha = true)
		{
			var outputImage = new ImageBuffer(sourceImage);

			switch (outputImage.BitDepth)
			{
				case 32:
					{
						int height = outputImage.Height;
						int width = outputImage.Width;
						byte[] imageABuffer = outputImage.GetBuffer();

						for (int y = 0; y < height; y++)
						{
							int offsetA = outputImage.GetBufferOffsetY(y);

							for (int x = 0; x < width; x++)
							{
								if (imageABuffer[offsetA + 0] == existingColor.blue
									&& imageABuffer[offsetA + 1] == existingColor.green
									&& imageABuffer[offsetA + 2] == existingColor.red
									&& imageABuffer[offsetA + 3] == existingColor.alpha)
								{
									// Set transparent colors
									imageABuffer[offsetA + 0] = newColor.blue;
									imageABuffer[offsetA + 1] = newColor.green;
									imageABuffer[offsetA + 2] = newColor.red;

									if (!keepExistingAlpha)
									{
										imageABuffer[offsetA + 3] = newColor.alpha;
									}
								}

								offsetA += 4;
							}
						}

						outputImage.SetRecieveBlender(new BlenderPreMultBGRA());
					}
					break;

				default:
					throw new NotImplementedException();
			}

			return outputImage;
		}

		/// <summary>
		/// Change all colors to White if not alpha 0 or set to RGBA_Bytes.Transparent
		/// </summary>
		/// <param name="sourceImage">The source image to act on</param>
		/// <returns>A new modified image</returns>
		public static ImageBuffer AnyAlphaToColor(this ImageBuffer sourceImage, RGBA_Bytes color)
		{
			return AnyAlphaToColor(sourceImage, color, RGBA_Bytes.Transparent);
		}

		public static ImageBuffer AnyAlphaToColor(this ImageBuffer sourceImage, RGBA_Bytes color, RGBA_Bytes transparency)
		{
			var outputImage = new ImageBuffer(sourceImage);

			switch (outputImage.BitDepth)
			{
				case 32:
					{
						int height = outputImage.Height;
						int width = outputImage.Width;
						byte[] imageABuffer = outputImage.GetBuffer();

						for (int y = 0; y < height; y++)
						{
							int offsetA = outputImage.GetBufferOffsetY(y);

							for (int x = 0; x < width; x++)
							{
								int alpha = imageABuffer[offsetA + 3];
								if (alpha > 0)
								{
									// Set semi-transparent colors
									imageABuffer[offsetA + 0] = color.blue;
									imageABuffer[offsetA + 1] = color.green;
									imageABuffer[offsetA + 2] = color.red;
									//imageABuffer[offsetA + 3] = (byte) (alpha == 255 ? 255 : 255 - alpha);
									imageABuffer[offsetA + 3] = (byte) alpha;
								}
								else
								{
									// Set transparent colors
									imageABuffer[offsetA + 0] = transparency.blue;
									imageABuffer[offsetA + 1] = transparency.green;
									imageABuffer[offsetA + 2] = transparency.red;
									imageABuffer[offsetA + 3] = transparency.alpha;
								}

								offsetA += 4;
							}
						}

						//outputImage.SetRecieveBlender(new BlenderPreMultBGRA());
					}
					break;

				default:
					throw new NotImplementedException();
			}

			return outputImage;
		}

		public static ImageBuffer AllWhite(this ImageBuffer sourceImage)
		{
			var destImage = new ImageBuffer(sourceImage);

			switch (destImage.BitDepth)
			{
				case 32:
					{
						int height = destImage.Height;
						int width = destImage.Width;
						byte[] resultBuffer = sourceImage.GetBuffer();
						byte[] imageABuffer = destImage.GetBuffer();
						for (int y = 0; y < height; y++)
						{
							int offsetA = destImage.GetBufferOffsetY(y);
							int offsetResult = sourceImage.GetBufferOffsetY(y);

							for (int x = 0; x < width; x++)
							{
								int alpha = imageABuffer[offsetA + 3];
								if (alpha > 0)
								{
									resultBuffer[offsetResult++] = (byte)255; offsetA++;
									resultBuffer[offsetResult++] = (byte)255; offsetA++;
									resultBuffer[offsetResult++] = (byte)255; offsetA++;
									resultBuffer[offsetResult++] = (byte)alpha; offsetA++;
								}
								else
								{
									resultBuffer[offsetResult++] = (byte)0; offsetA++;
									resultBuffer[offsetResult++] = (byte)0; offsetA++;
									resultBuffer[offsetResult++] = (byte)0; offsetA++;
									resultBuffer[offsetResult++] = (byte)0; offsetA++;
								}
							}
						}

						destImage.SetRecieveBlender(new BlenderPreMultBGRA());
					}
					break;

				default:
					throw new NotImplementedException();
			}

			return destImage;
		}

	}
}