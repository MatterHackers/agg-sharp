//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
//
// Adaptation for high precision colors has been sponsored by
// Liberty Technology Systems, Inc., visit http://lib-sys.com
//
// Liberty Technology Systems, Inc. is the provider of
// PostScript and PDF technology for software developers.
//
//----------------------------------------------------------------------------
using System;

namespace MatterHackers.Agg.Image
{
	public class blender_gray : IRecieveBlenderByte
	{
		public int NumPixelBits { get { return 8; } }

		public const byte base_mask = 255;
		private const int base_shift = 8;

		private static int[] m_Saturate9BitToByte = new int[1 << 9];

		private int bytesBetweenPixelsInclusive;

		public blender_gray(int bytesBetweenPixelsInclusive)
		{
			this.bytesBetweenPixelsInclusive = bytesBetweenPixelsInclusive;

			if (m_Saturate9BitToByte[2] == 0)
			{
				for (int i = 0; i < m_Saturate9BitToByte.Length; i++)
				{
					m_Saturate9BitToByte[i] = Math.Min(i, 255);
				}
			}
		}

		public RGBA_Bytes PixelToColorRGBA_Bytes(byte[] buffer, int bufferOffset)
		{
			int value = buffer[bufferOffset];
			return new RGBA_Bytes(value, value, value, 255);
		}

		public void CopyPixels(byte[] pDestBuffer, int bufferOffset, RGBA_Bytes sourceColor, int count)
		{
			do
			{
				int y = (sourceColor.red * 77) + (sourceColor.green * 151) + (sourceColor.blue * 28);
				int gray = (y >> 8);
				pDestBuffer[bufferOffset] = (byte)gray;
				bufferOffset += bytesBetweenPixelsInclusive;
			}
			while (--count != 0);
		}

		public void BlendPixel(byte[] pDestBuffer, int bufferOffset, RGBA_Bytes sourceColor)
		{
			int OneOverAlpha = base_mask - sourceColor.alpha;
			unchecked
			{
				int y = (sourceColor.red * 77) + (sourceColor.green * 151) + (sourceColor.blue * 28);
				int gray = (y >> 8);
				gray = (byte)((((gray - (int)(pDestBuffer[bufferOffset])) * sourceColor.alpha) + ((int)(pDestBuffer[bufferOffset]) << base_shift)) >> base_shift);
				pDestBuffer[bufferOffset] = (byte)gray;
			}
		}

		public void BlendPixels(byte[] destBuffer, int bufferOffset,
			RGBA_Bytes[] sourceColors, int sourceColorsOffset,
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
						bufferOffset += bytesBetweenPixelsInclusive;
					}
					while (--count != 0);
				}
				else
				{
					do
					{
						sourceColors[sourceColorsOffset].alpha = (byte)((sourceColors[sourceColorsOffset].alpha * cover + 255) >> 8);
						BlendPixel(destBuffer, bufferOffset, sourceColors[sourceColorsOffset]);
						bufferOffset += bytesBetweenPixelsInclusive;
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
						RGBA_Bytes color = sourceColors[sourceColorsOffset];
						color.alpha = (byte)((color.alpha * (cover) + 255) >> 8);
						BlendPixel(destBuffer, bufferOffset, color);
					}
					bufferOffset += bytesBetweenPixelsInclusive;
					++sourceColorsOffset;
				}
				while (--count != 0);
			}
		}
	}

	public class blenderGrayFromRed : IRecieveBlenderByte
	{
		public int NumPixelBits { get { return 8; } }

		public const byte base_mask = 255;
		private const int base_shift = 8;

		private static int[] m_Saturate9BitToByte = new int[1 << 9];

		private int bytesBetweenPixelsInclusive;

		public blenderGrayFromRed(int bytesBetweenPixelsInclusive)
		{
			this.bytesBetweenPixelsInclusive = bytesBetweenPixelsInclusive;

			if (m_Saturate9BitToByte[2] == 0)
			{
				for (int i = 0; i < m_Saturate9BitToByte.Length; i++)
				{
					m_Saturate9BitToByte[i] = Math.Min(i, 255);
				}
			}
		}

		public RGBA_Bytes PixelToColorRGBA_Bytes(byte[] buffer, int bufferOffset)
		{
			int value = buffer[bufferOffset];
			return new RGBA_Bytes(value, value, value, 255);
		}

		public void CopyPixels(byte[] pDestBuffer, int bufferOffset, RGBA_Bytes sourceColor, int count)
		{
			do
			{
				pDestBuffer[bufferOffset] = sourceColor.red;
				bufferOffset += bytesBetweenPixelsInclusive;
			}
			while (--count != 0);
		}

		public void BlendPixel(byte[] pDestBuffer, int bufferOffset, RGBA_Bytes sourceColor)
		{
			int OneOverAlpha = base_mask - sourceColor.alpha;
			unchecked
			{
				byte gray = (byte)((((sourceColor.red - (int)(pDestBuffer[bufferOffset])) * sourceColor.alpha) + ((int)(pDestBuffer[bufferOffset]) << base_shift)) >> base_shift);
				pDestBuffer[bufferOffset] = (byte)gray;
			}
		}

		public void BlendPixels(byte[] destBuffer, int bufferOffset,
			RGBA_Bytes[] sourceColors, int sourceColorsOffset,
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
						bufferOffset += bytesBetweenPixelsInclusive;
					}
					while (--count != 0);
				}
				else
				{
					do
					{
						sourceColors[sourceColorsOffset].alpha = (byte)((sourceColors[sourceColorsOffset].alpha * cover + 255) >> 8);
						BlendPixel(destBuffer, bufferOffset, sourceColors[sourceColorsOffset]);
						bufferOffset += bytesBetweenPixelsInclusive;
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
						RGBA_Bytes color = sourceColors[sourceColorsOffset];
						color.alpha = (byte)((color.alpha * (cover) + 255) >> 8);
						BlendPixel(destBuffer, bufferOffset, color);
					}
					bufferOffset += bytesBetweenPixelsInclusive;
					++sourceColorsOffset;
				}
				while (--count != 0);
			}
		}
	}

	public class blenderGrayClampedMax : IRecieveBlenderByte
	{
		public int NumPixelBits { get { return 8; } }

		public const byte base_mask = 255;
		private const int base_shift = 8;

		private static int[] m_Saturate9BitToByte = new int[1 << 9];

		private int bytesBetweenPixelsInclusive;

		public blenderGrayClampedMax(int bytesBetweenPixelsInclusive)
		{
			this.bytesBetweenPixelsInclusive = bytesBetweenPixelsInclusive;

			if (m_Saturate9BitToByte[2] == 0)
			{
				for (int i = 0; i < m_Saturate9BitToByte.Length; i++)
				{
					m_Saturate9BitToByte[i] = Math.Min(i, 255);
				}
			}
		}

		public RGBA_Bytes PixelToColorRGBA_Bytes(byte[] buffer, int bufferOffset)
		{
			int value = buffer[bufferOffset];
			return new RGBA_Bytes(value, value, value, 255);
		}

		public void CopyPixels(byte[] pDestBuffer, int bufferOffset, RGBA_Bytes sourceColor, int count)
		{
			do
			{
				byte clampedMax = Math.Min(Math.Max(sourceColor.red, Math.Max(sourceColor.green, sourceColor.blue)), (byte)255);
				pDestBuffer[bufferOffset] = clampedMax;
				bufferOffset += bytesBetweenPixelsInclusive;
			}
			while (--count != 0);
		}

		public void BlendPixel(byte[] pDestBuffer, int bufferOffset, RGBA_Bytes sourceColor)
		{
			int OneOverAlpha = base_mask - sourceColor.alpha;
			unchecked
			{
				byte clampedMax = Math.Min(Math.Max(sourceColor.red, Math.Max(sourceColor.green, sourceColor.blue)), (byte)255);
				byte gray = (byte)((((clampedMax - (int)(pDestBuffer[bufferOffset])) * sourceColor.alpha) + ((int)(pDestBuffer[bufferOffset]) << base_shift)) >> base_shift);
				pDestBuffer[bufferOffset] = (byte)gray;
			}
		}

		public void BlendPixels(byte[] destBuffer, int bufferOffset,
			RGBA_Bytes[] sourceColors, int sourceColorsOffset,
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
						bufferOffset += bytesBetweenPixelsInclusive;
					}
					while (--count != 0);
				}
				else
				{
					do
					{
						sourceColors[sourceColorsOffset].alpha = (byte)((sourceColors[sourceColorsOffset].alpha * cover + 255) >> 8);
						BlendPixel(destBuffer, bufferOffset, sourceColors[sourceColorsOffset]);
						bufferOffset += bytesBetweenPixelsInclusive;
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
						RGBA_Bytes color = sourceColors[sourceColorsOffset];
						color.alpha = (byte)((color.alpha * (cover) + 255) >> 8);
						BlendPixel(destBuffer, bufferOffset, color);
					}
					bufferOffset += bytesBetweenPixelsInclusive;
					++sourceColorsOffset;
				}
				while (--count != 0);
			}
		}
	}

	/*

	//======================================================blender_gray_pre
	//template<class ColorT>
	struct blender_gray_pre
	{
		typedef ColorT color_type;
		typedef typename color_type::value_type value_type;
		typedef typename color_type::calc_type calc_type;
		enum base_scale_e { base_shift = color_type::base_shift };

		static void blend_pix(value_type* p, int cv,
										 int alpha, int cover)
		{
			alpha = color_type::base_mask - alpha;
			cover = (cover + 1) << (base_shift - 8);
			*p = (value_type)((*p * alpha + cv * cover) >> base_shift);
		}

		static void blend_pix(value_type* p, int cv,
										 int alpha)
		{
			*p = (value_type)(((*p * (color_type::base_mask - alpha)) >> base_shift) + cv);
		}
	};

	//=====================================================apply_gamma_dir_gray
	//template<class ColorT, class GammaLut>
	class apply_gamma_dir_gray
	{
	public:
		typedef typename ColorT::value_type value_type;

		apply_gamma_dir_gray(GammaLut& gamma) : m_gamma(gamma) {}

		void operator () (byte* p)
		{
			*p = m_gamma.dir(*p);
		}

	private:
		GammaLut& m_gamma;
	};

	//=====================================================apply_gamma_inv_gray
	//template<class ColorT, class GammaLut>
	class apply_gamma_inv_gray
	{
	public:
		typedef typename ColorT::value_type value_type;

		apply_gamma_inv_gray(GammaLut& gamma) : m_gamma(gamma) {}

		void operator () (byte* p)
		{
			*p = m_gamma.inv(*p);
		}

	private:
		GammaLut& m_gamma;
	};

	 */
}