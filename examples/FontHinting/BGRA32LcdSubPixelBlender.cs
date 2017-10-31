using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.Font
{
	public sealed class BGRA32LcdSubPixelBlender : IRecieveBlenderByte
	{
		public int NumPixelBits { get { return 8; } }

		public const byte base_mask = 255;
		private const int base_shift = 8;

		public BGRA32LcdSubPixelBlender()
		{
		}

		public Color PixelToColorRGBA_Bytes(byte[] buffer, int bufferOffset)
		{
			return new Color(buffer[bufferOffset + ImageBuffer.OrderR], buffer[bufferOffset + ImageBuffer.OrderG], buffer[bufferOffset + ImageBuffer.OrderB], buffer[bufferOffset + ImageBuffer.OrderA]);
		}

		public void CopyPixels(byte[] buffer, int bufferOffset, Color sourceColor, int count)
		{
			do
			{
				buffer[bufferOffset++] = sourceColor.red;
			}
			while (--count != 0);
		}

		public void BlendPixel(byte[] buffer, int bufferOffset, Color sourceColor)
		{
			unchecked
			{
				if (sourceColor.alpha == 255)
				{
					buffer[bufferOffset] = (byte)(sourceColor.red);
				}
				else
				{
					int r = buffer[bufferOffset];
					buffer[bufferOffset] = (byte)(((sourceColor.red - r) * sourceColor.alpha + (r << (int)Color.base_shift)) >> (int)Color.base_shift);
				}
			}
		}

		public void BlendPixels(byte[] destBuffer, int bufferOffset,
			Color[] sourceColors, int sourceColorsOffset,
			byte[] covers, int coversIndex, bool firstCoverForAll, int count)
		{
			if (firstCoverForAll)
			{
				int cover = covers[coversIndex];
				if (cover == 255)
				{
					do
					{
						BlendPixel(destBuffer, bufferOffset, sourceColors[sourceColorsOffset++]);
						bufferOffset += 4;
					}
					while (--count != 0);
				}
				else
				{
					do
					{
						sourceColors[sourceColorsOffset].alpha = (byte)((sourceColors[sourceColorsOffset].alpha * cover + 255) >> 8);
						BlendPixel(destBuffer, bufferOffset, sourceColors[sourceColorsOffset]);
						bufferOffset += 4;
						++sourceColorsOffset;
					}
					while (--count != 0);
				}
			}
			else
			{
				do
				{
					int cover = covers[coversIndex++];
					if (cover == 255)
					{
						BlendPixel(destBuffer, bufferOffset, sourceColors[sourceColorsOffset]);
					}
					else
					{
						Color color = sourceColors[sourceColorsOffset];
						color.alpha = (byte)((color.alpha * (cover) + 255) >> 8);
						BlendPixel(destBuffer, bufferOffset, color);
					}
					bufferOffset += 4;
					++sourceColorsOffset;
				}
				while (--count != 0);
			}
		}
	}
}