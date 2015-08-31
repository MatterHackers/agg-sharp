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
	public class Multiply
	{
		public static void DoMultiply(ImageBuffer result, ImageBuffer imageA, ImageBuffer imageB)
		{
			if (imageA.BitDepth != imageB.BitDepth || imageB.BitDepth != result.BitDepth)
			{
				throw new NotImplementedException("All the images have to be the same bit depth.");
			}
			if (imageA.Width != imageB.Width || imageA.Height != imageB.Height
				|| imageA.Width != result.Width || imageA.Height != result.Height)
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
						byte[] imageBBuffer = imageB.GetBuffer();
						for (int y = 0; y < height; y++)
						{
							int offsetA = imageA.GetBufferOffsetY(y);
							int offsetB = imageB.GetBufferOffsetY(y);
							int offsetResult = result.GetBufferOffsetY(y);

							for (int x = 0; x < width; x++)
							{
								resultBuffer[offsetResult++] = (byte)((imageABuffer[offsetA++] * imageBBuffer[offsetB++]) / 255);
								resultBuffer[offsetResult++] = (byte)((imageABuffer[offsetA++] * imageBBuffer[offsetB++]) / 255);
								resultBuffer[offsetResult++] = (byte)((imageABuffer[offsetA++] * imageBBuffer[offsetB++]) / 255);
								resultBuffer[offsetResult++] = 255; offsetA++; offsetB++;
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