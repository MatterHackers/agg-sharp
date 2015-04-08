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