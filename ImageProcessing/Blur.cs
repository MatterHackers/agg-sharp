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
	public class Blur
	{
		public static void DoYBlur(ImageBuffer sourceDest)
		{
			if (sourceDest.BitDepth != 8)
			{
				throw new NotImplementedException("We only work with 8 bit at the moment.");
			}

			int height = sourceDest.Height;
			int width = sourceDest.Width;
			byte[] buffer = sourceDest.GetBuffer();
			int strideInBytes = sourceDest.StrideInBytes();
			byte[] cache = new byte[height];

			for (int x = 0; x < width; x++)
			{
				int offset = x;
				for (int y = 0; y < height; y++)
				{
					cache[y] = buffer[offset];
					offset += strideInBytes;
				}

				offset = x;
				for (int y = 1; y < height - 1; y++)
				{
					int newValue = (cache[y - 1] + cache[y] * 2 + cache[y + 1] + 2) / 4; // the + 2 is so that we will round correctly
					buffer[offset] = (byte)newValue;
					offset += strideInBytes;
				}
			}
		}

		internal static void DoXBlur(ImageBuffer sourceDest)
		{
			if (sourceDest.BitDepth != 8)
			{
				throw new NotImplementedException("We only work with 8 bit at the moment.");
			}

			int height = sourceDest.Height;
			int width = sourceDest.Width;
			byte[] buffer = sourceDest.GetBuffer();
			byte[] cache = new byte[width];

			for (int y = 0; y < height; y++)
			{
				int offset = sourceDest.GetBufferOffsetY(y);
				for (int x = 0; x < width; x++)
				{
					cache[x] = buffer[offset + x];
				}

				for (int x = 1; x < width - 1; x++)
				{
					int newValue = (cache[x - 1] + cache[x] * 2 + cache[x + 1] + 2) / 4; // the + 2 is so that we will round correctly
					buffer[offset + x] = (byte)newValue;
				}
			}
		}
	}
}