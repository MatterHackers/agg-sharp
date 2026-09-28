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
*/

using System;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>AGG's spheres image (examples/spheres.bmp), the source picture of the image demos.</summary>
	public static class SpheresImage
	{
		/// <summary>
		/// A fresh 32-bit copy of the picture as a C++ flip_y = true example sees it after <c>load_img</c>:
		/// row 0 is the bitmap's first stored row, the picture's bottom, so it shows upright in a y-up demo.
		/// </summary>
		public static ImageBuffer Load()
		{
			const string resourceName = "MatterHackers.AggSharpDemo.Images.spheres.bmp";
			using var stream = typeof(SpheresImage).Assembly.GetManifestResourceStream(resourceName)
				?? throw new InvalidOperationException(
					$"The image resource '{resourceName}' is missing; AggSharpDemo.csproj embeds it from examples/spheres.bmp.");
			using var memory = new MemoryStream();
			stream.CopyTo(memory);
			return FromBmp(memory.ToArray());
		}

		// Reads only what spheres.bmp is: an uncompressed 24-bit bottom-up BMP.
		private static ImageBuffer FromBmp(byte[] bmp)
		{
			int offset = BitConverter.ToInt32(bmp, 10);
			int width = BitConverter.ToInt32(bmp, 18);
			int height = BitConverter.ToInt32(bmp, 22);
			int bitsPerPixel = BitConverter.ToUInt16(bmp, 28);
			if (bmp[0] != 'B' || bmp[1] != 'M' || bitsPerPixel != 24 || height <= 0)
			{
				throw new InvalidDataException("spheres.bmp is expected to be a 24-bit bottom-up BMP.");
			}

			int rowStride = ((width * 3) + 3) / 4 * 4;
			var image = new ImageBuffer(width, height);
			for (int y = 0; y < height; y++)
			{
				int row = offset + (y * rowStride);
				for (int x = 0; x < width; x++)
				{
					int i = row + (x * 3);
					image.SetPixel(x, y, new Color(bmp[i + 2], bmp[i + 1], bmp[i], 255));
				}
			}

			return image;
		}
	}
}
