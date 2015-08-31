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
	static public class Dilate
	{
		public static void DoDilate3x3Binary(ImageBuffer sourceAndDest, int threshold)
		{
			ImageBuffer temp = new ImageBuffer(sourceAndDest);
			DoDilate3x3Binary(temp, sourceAndDest, threshold);
		}

		public static void DoDilate3x3Binary(ImageBuffer source, ImageBuffer dest, int threshold)
		{
			if (source.BitDepth != 32 || dest.BitDepth != 32)
			{
				throw new NotImplementedException("We only work with 32 bit at the moment.");
			}

			if (source.Width != dest.Width || source.Height != dest.Height)
			{
				throw new NotImplementedException("Source and Dest have to be the same size");
			}

			int height = source.Height;
			int width = source.Width;
			int sourceStrideInBytes = source.StrideInBytes();
			int destStrideInBytes = dest.StrideInBytes();
			byte[] sourceBuffer = source.GetBuffer();
			byte[] destBuffer = dest.GetBuffer();

			for (int testY = 1; testY < height - 1; testY++)
			{
				for (int testX = 1; testX < width - 1; testX++)
				{
					for (int sourceY = -1; sourceY <= 1; sourceY++)
					{
						for (int sourceX = -1; sourceX <= 1; sourceX++)
						{
							int sourceOffset = source.GetBufferOffsetXY(testX + sourceX, testY + sourceY);
							if (sourceBuffer[sourceOffset] > threshold)
							{
								int destOffset = dest.GetBufferOffsetXY(testX, testY);
								destBuffer[destOffset++] = 255;
								destBuffer[destOffset++] = 255;
								destBuffer[destOffset++] = 255;
								destBuffer[destOffset++] = 255;
							}
						}
					}
				}
			}
		}

		public static void DoDilate3x3MaxValue(ImageBuffer sourceAndDest)
		{
			ImageBuffer temp = new ImageBuffer(sourceAndDest);
			DoDilate3x3MaxValue(temp, sourceAndDest);
		}

		public static void DoDilate3x3MaxValue(ImageBuffer source, ImageBuffer dest)
		{
			if (source.BitDepth != 32 || dest.BitDepth != 32)
			{
				throw new NotImplementedException("We only work with 32 bit at the moment.");
			}

			if (source.Width != dest.Width || source.Height != dest.Height)
			{
				throw new NotImplementedException("Source and Dest have to be the same size");
			}

			int height = source.Height;
			int width = source.Width;
			int sourceStrideInBytes = source.StrideInBytes();
			int destStrideInBytes = dest.StrideInBytes();
			byte[] sourceBuffer = source.GetBuffer();
			byte[] destBuffer = dest.GetBuffer();

			for (int testY = 1; testY < height - 1; testY++)
			{
				for (int testX = 1; testX < width - 1; testX++)
				{
					RGBA_Bytes maxColor = RGBA_Bytes.Black;
					int sourceOffset = source.GetBufferOffsetXY(testX, testY - 1);

					// x-1, y-1
					//maxColor = MaxColor(sourceBuffer, maxColor, sourceOffset - 4);
					// x0, y-1
					maxColor = MaxColor(sourceBuffer, maxColor, sourceOffset + 0);
					// x1, y-1
					//maxColor = MaxColor(sourceBuffer, maxColor, sourceOffset + 4);

					// x-1, y0
					maxColor = MaxColor(sourceBuffer, maxColor, sourceOffset + sourceStrideInBytes - 4);
					// x0, y0
					maxColor = MaxColor(sourceBuffer, maxColor, sourceOffset + sourceStrideInBytes + 0);
					// x+1, y0
					maxColor = MaxColor(sourceBuffer, maxColor, sourceOffset + sourceStrideInBytes + 4);

					// x-1, y+1
					//maxColor = MaxColor(sourceBuffer, maxColor, sourceOffset + sourceStrideInBytes * 2 - 4);
					// x0, y+1
					maxColor = MaxColor(sourceBuffer, maxColor, sourceOffset + sourceStrideInBytes * 2 + 0);
					// x+1, y+1
					//maxColor = MaxColor(sourceBuffer, maxColor, sourceOffset + sourceStrideInBytes * 2 + 4);

					int destOffset = dest.GetBufferOffsetXY(testX, testY);
					destBuffer[destOffset + 2] = maxColor.red;
					destBuffer[destOffset + 1] = maxColor.green;
					destBuffer[destOffset + 0] = maxColor.blue;
					destBuffer[destOffset + 3] = 255;
				}
			}
		}

		private static RGBA_Bytes MaxColor(byte[] sourceBuffer, RGBA_Bytes maxColor, int sourceOffset)
		{
			maxColor.red = Math.Max(maxColor.red, sourceBuffer[sourceOffset + 2]);
			maxColor.green = Math.Max(maxColor.green, sourceBuffer[sourceOffset + 1]);
			maxColor.blue = Math.Max(maxColor.blue, sourceBuffer[sourceOffset + 0]);
			return maxColor;
		}
	}
}