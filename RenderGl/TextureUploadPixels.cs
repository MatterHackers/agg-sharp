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
using MatterHackers.Agg.Image;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// The RGBA bytes <see cref="ImageTexturePlugin"/> uploads: the image's own pixels, as the software renderer reads
	/// them, never blended through its receive blender (which premultiplied a straight-alpha image, and shifted one
	/// with an OriginOffset - the texture quad applies that offset).
	/// </summary>
	internal static class TextureUploadPixels
	{
		/// <summary>
		/// The image's pixels as RGBA rows, row 0 first, padded with transparent black to the hardware size. A
		/// straight-alpha (<see cref="BlenderBGRA"/>) image's fully transparent pixels lose their colour, so a filtered
		/// sample beside one does not pick up a colour that was never visible.
		/// </summary>
		public static byte[] FromImage(ImageBuffer image, int hardwareWidth, int hardwareHeight)
		{
			var rgba = new byte[hardwareWidth * hardwareHeight * 4];
			byte[] source = image.GetBuffer();
			bool clearTransparentColor = image.GetRecieveBlender() is BlenderBGRA;
			int bytesPerPixel = image.BitDepth / 8;
			for (int y = 0; y < image.Height; y++)
			{
				int to = y * hardwareWidth * 4;
				for (int x = 0; x < image.Width; x++, to += 4)
				{
					int from = image.GetBufferOffsetXY(x, y);
					switch (bytesPerPixel)
					{
						case 4:
							rgba[to + 0] = source[from + ImageBuffer.OrderR];
							rgba[to + 1] = source[from + ImageBuffer.OrderG];
							rgba[to + 2] = source[from + ImageBuffer.OrderB];
							rgba[to + 3] = source[from + ImageBuffer.OrderA];
							if (clearTransparentColor && rgba[to + 3] == 0)
							{
								rgba[to + 0] = rgba[to + 1] = rgba[to + 2] = 0;
							}

							break;

						case 3:
							rgba[to + 0] = source[from + ImageBuffer.OrderR];
							rgba[to + 1] = source[from + ImageBuffer.OrderG];
							rgba[to + 2] = source[from + ImageBuffer.OrderB];
							rgba[to + 3] = 255;
							break;

						case 1:
							rgba[to + 0] = rgba[to + 1] = rgba[to + 2] = source[from];
							rgba[to + 3] = 255;
							break;

						default:
							throw new NotImplementedException($"No texture upload for {image.BitDepth} bit images.");
					}
				}
			}

			return rgba;
		}

		/// <summary>
		/// The next mip level down: each texel the 2 by 2 block above it, its alpha the block's mean and its colour the
		/// alpha-weighted mean of the block's straight colours - so a level stays straight alpha like level 0, and a
		/// transparent texel adds no colour. An odd last row or column is reused by the block that runs past it.
		/// </summary>
		public static byte[] DownsampleStraightAlpha(byte[] rgba, int width, int height, out int halfWidth, out int halfHeight)
		{
			halfWidth = Math.Max(1, width / 2);
			halfHeight = Math.Max(1, height / 2);
			var half = new byte[halfWidth * halfHeight * 4];
			for (int y = 0; y < halfHeight; y++)
			{
				for (int x = 0; x < halfWidth; x++)
				{
					int alphaSum = 0;
					int redSum = 0, greenSum = 0, blueSum = 0;
					for (int dy = 0; dy < 2; dy++)
					{
						int sourceY = Math.Min(height - 1, (y * 2) + dy);
						for (int dx = 0; dx < 2; dx++)
						{
							int sourceX = Math.Min(width - 1, (x * 2) + dx);
							int at = ((sourceY * width) + sourceX) * 4;
							int alpha = rgba[at + 3];
							alphaSum += alpha;
							redSum += rgba[at + 0] * alpha;
							greenSum += rgba[at + 1] * alpha;
							blueSum += rgba[at + 2] * alpha;
						}
					}

					int to = ((y * halfWidth) + x) * 4;
					if (alphaSum > 0)
					{
						half[to + 0] = (byte)((redSum + (alphaSum / 2)) / alphaSum);
						half[to + 1] = (byte)((greenSum + (alphaSum / 2)) / alphaSum);
						half[to + 2] = (byte)((blueSum + (alphaSum / 2)) / alphaSum);
					}

					half[to + 3] = (byte)((alphaSum + 2) / 4);
				}
			}

			return half;
		}
	}
}
