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

namespace MatterHackers.Agg.ImageProcessing
{
	public static class Threshold
	{
		public delegate bool TestThreshold(byte[] buffer, int offset, int threshold);

		public static bool MaxRGB32(byte[] buffer, int offset, int threshold)
		{
			if (buffer[offset + 0] > threshold || buffer[offset + 0] > threshold || buffer[offset + 0] > threshold)
			{
				return true;
			}

			return false;
		}

		public static void DoThreshold(this ImageBuffer sourceImageAndDest, int threshold)
		{
			DoThreshold(sourceImageAndDest, sourceImageAndDest, threshold, MaxRGB32);
		}

		public static void DoThreshold(ImageBuffer sourceImageAndDest, int threshold, TestThreshold testFunction)
		{
			DoThreshold(sourceImageAndDest, sourceImageAndDest, threshold, testFunction);
		}

		public static void DoThreshold(ImageBuffer result, ImageBuffer sourceImage, int threshold, TestThreshold testFunction)
		{
			if (sourceImage.BitDepth != result.BitDepth)
			{
				throw new NotImplementedException("All the images have to be the same bit depth.");
			}
			if (sourceImage.Width != result.Width || sourceImage.Height != result.Height)
			{
				throw new Exception("All images must be the same size.");
			}

			switch (sourceImage.BitDepth)
			{
				case 8:
					{
						int height = sourceImage.Height;
						int width = sourceImage.Width;
						byte[] resultBuffer = result.GetBuffer();
						byte[] sourceBuffer = sourceImage.GetBuffer();
						for (int y = 0; y < height; y++)
						{
							int offset = sourceImage.GetBufferOffsetY(y);

							for (int x = 0; x < width; x++)
							{
								if (testFunction(sourceBuffer, offset, threshold))
								{
									resultBuffer[offset] = (byte)255;
								}
								else
								{
									resultBuffer[offset] = (byte)0;
								}
								offset += 1;
							}
						}
					}
					break;

				case 32:
					{
						int height = sourceImage.Height;
						int width = sourceImage.Width;
						byte[] resultBuffer = result.GetBuffer();
						byte[] sourceBuffer = sourceImage.GetBuffer();
						for (int y = 0; y < height; y++)
						{
							int offset = sourceImage.GetBufferOffsetY(y);

							for (int x = 0; x < width; x++)
							{
								if (testFunction(sourceBuffer, offset, threshold))
								{
									resultBuffer[offset + 0] = (byte)255;
									resultBuffer[offset + 1] = (byte)255;
									resultBuffer[offset + 2] = (byte)255;
									resultBuffer[offset + 3] = (byte)255;
								}
								else
								{
									resultBuffer[offset + 0] = (byte)0;
									resultBuffer[offset + 1] = (byte)0;
									resultBuffer[offset + 2] = (byte)0;
									resultBuffer[offset + 3] = (byte)0;
								}
								offset += 4;
							}
						}
					}
					break;

				default:
					throw new NotImplementedException();
			}
		}
	}
}