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
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg
{
	public interface IImageBufferAccessor
	{
		byte[] span(int x, int y, int len, out int bufferIndex);

		byte[] next_x(out int bufferByteOffset);

		byte[] next_y(out int bufferByteOffset);

		IImageByte SourceImage
		{
			get;
		}
	};

	public class ImageBufferAccessorCommon : IImageBufferAccessor
	{
		protected IImageByte m_SourceImage;
		protected int m_x, m_x0, m_y, m_DistanceBetweenPixelsInclusive;
		protected byte[] m_Buffer;
		protected int m_CurrentBufferOffset = -1;
		private int m_Width;

		public ImageBufferAccessorCommon(IImageByte pixf)
		{
			attach(pixf);
		}

		private void attach(IImageByte pixf)
		{
			m_SourceImage = pixf;
			m_Buffer = m_SourceImage.GetBuffer();
			m_Width = m_SourceImage.Width;
			m_DistanceBetweenPixelsInclusive = m_SourceImage.GetBytesBetweenPixelsInclusive();
		}

		public IImageByte SourceImage
		{
			get
			{
				return m_SourceImage;
			}
		}

		private byte[] pixel(out int bufferByteOffset)
		{
			int x = m_x;
			int y = m_y;
			unchecked
			{
				if ((uint)x >= (uint)m_SourceImage.Width)
				{
					if (x < 0)
					{
						x = 0;
					}
					else
					{
						x = (int)m_SourceImage.Width - 1;
					}
				}

				if ((uint)y >= (uint)m_SourceImage.Height)
				{
					if (y < 0)
					{
						y = 0;
					}
					else
					{
						y = (int)m_SourceImage.Height - 1;
					}
				}
			}

			bufferByteOffset = m_SourceImage.GetBufferOffsetXY(x, y);
			return m_SourceImage.GetBuffer();
		}

		public byte[] span(int x, int y, int len, out int bufferOffset)
		{
			m_x = m_x0 = x;
			m_y = y;
			unchecked
			{
				if ((uint)y < (uint)m_SourceImage.Height
					&& x >= 0 && x + len <= (int)m_SourceImage.Width)
				{
					bufferOffset = m_SourceImage.GetBufferOffsetXY(x, y);
					m_Buffer = m_SourceImage.GetBuffer();
					m_CurrentBufferOffset = bufferOffset;
					return m_Buffer;
				}
			}

			m_CurrentBufferOffset = -1;
			return pixel(out bufferOffset);
		}

		public byte[] next_x(out int bufferOffset)
		{
			// this is the code (managed) that the original agg used.
			// It looks like it doesn't check x but, It should be a bit faster and is valid
			// because "span" checked the whole length for good x.
			if (m_CurrentBufferOffset != -1)
			{
				m_CurrentBufferOffset += m_DistanceBetweenPixelsInclusive;
				bufferOffset = m_CurrentBufferOffset;
				return m_Buffer;
			}
			++m_x;
			return pixel(out bufferOffset);
		}

		public byte[] next_y(out int bufferOffset)
		{
			++m_y;
			m_x = m_x0;
			if (m_CurrentBufferOffset != -1
				&& (uint)m_y < (uint)m_SourceImage.Height)
			{
				m_CurrentBufferOffset = m_SourceImage.GetBufferOffsetXY(m_x, m_y);
				m_SourceImage.GetBuffer();
				bufferOffset = m_CurrentBufferOffset; ;
				return m_Buffer;
			}

			m_CurrentBufferOffset = -1;
			return pixel(out bufferOffset);
		}
	};

	public sealed class ImageBufferAccessorClip : ImageBufferAccessorCommon
	{
		private byte[] m_OutsideBufferColor;

		public ImageBufferAccessorClip(IImageByte sourceImage, RGBA_Bytes bk)
			: base(sourceImage)
		{
			m_OutsideBufferColor = new byte[4];
			m_OutsideBufferColor[0] = bk.red;
			m_OutsideBufferColor[1] = bk.green;
			m_OutsideBufferColor[2] = bk.blue;
			m_OutsideBufferColor[3] = bk.alpha;
		}

		private byte[] pixel(out int bufferByteOffset)
		{
			unchecked
			{
				if (((uint)m_x < (uint)m_SourceImage.Width)
					&& ((uint)m_y < (uint)m_SourceImage.Height))
				{
					bufferByteOffset = m_SourceImage.GetBufferOffsetXY(m_x, m_y);
					return m_SourceImage.GetBuffer();
				}
			}

			bufferByteOffset = 0;
			return m_OutsideBufferColor;
		}
	};

	/*
		//--------------------------------------------------image_accessor_no_clip
		template<class PixFmt> class image_accessor_no_clip
		{
		public:
			typedef PixFmt   pixfmt_type;
			typedef typename pixfmt_type::color_type color_type;
			typedef typename pixfmt_type::order_type order_type;
			typedef typename pixfmt_type::value_type value_type;
			enum pix_width_e { pix_width = pixfmt_type::pix_width };

			image_accessor_no_clip() {}
			explicit image_accessor_no_clip(pixfmt_type& pixf) :
				m_pixf(&pixf)
			{}

			void attach(pixfmt_type& pixf)
			{
				m_pixf = &pixf;
			}

			byte* span(int x, int y, int)
			{
				m_x = x;
				m_y = y;
				return m_pix_ptr = m_pixf->pix_ptr(x, y);
			}

			byte* next_x()
			{
				return m_pix_ptr += pix_width;
			}

			byte* next_y()
			{
				++m_y;
				return m_pix_ptr = m_pixf->pix_ptr(m_x, m_y);
			}

		private:
			pixfmt_type* m_pixf;
			int                m_x, m_y;
			byte*       m_pix_ptr;
		};
	 */

	public sealed class ImageBufferAccessorClamp : ImageBufferAccessorCommon
	{
		public ImageBufferAccessorClamp(IImageByte pixf)
			: base(pixf)
		{
		}

		private byte[] pixel(out int bufferByteOffset)
		{
			int x = m_x;
			int y = m_y;
			unchecked
			{
				if ((uint)x >= (uint)m_SourceImage.Width)
				{
					if (x < 0)
					{
						x = 0;
					}
					else
					{
						x = (int)m_SourceImage.Width - 1;
					}
				}

				if ((uint)y >= (uint)m_SourceImage.Height)
				{
					if (y < 0)
					{
						y = 0;
					}
					else
					{
						y = (int)m_SourceImage.Height - 1;
					}
				}
			}

			bufferByteOffset = m_SourceImage.GetBufferOffsetXY(x, y);
			return m_SourceImage.GetBuffer();
		}
	};

	/*

		//-----------------------------------------------------image_accessor_wrap
		template<class PixFmt, class WrapX, class WrapY> class image_accessor_wrap
		{
		public:
			typedef PixFmt   pixfmt_type;
			typedef typename pixfmt_type::color_type color_type;
			typedef typename pixfmt_type::order_type order_type;
			typedef typename pixfmt_type::value_type value_type;
			enum pix_width_e { pix_width = pixfmt_type::pix_width };

			image_accessor_wrap() {}
			explicit image_accessor_wrap(pixfmt_type& pixf) :
				m_pixf(&pixf),
				m_wrap_x(pixf.Width),
				m_wrap_y(pixf.Height)
			{}

			void attach(pixfmt_type& pixf)
			{
				m_pixf = &pixf;
			}

			byte* span(int x, int y, int)
			{
				m_x = x;
				m_row_ptr = m_pixf->row_ptr(m_wrap_y(y));
				return m_row_ptr + m_wrap_x(x) * pix_width;
			}

			byte* next_x()
			{
				int x = ++m_wrap_x;
				return m_row_ptr + x * pix_width;
			}

			byte* next_y()
			{
				m_row_ptr = m_pixf->row_ptr(++m_wrap_y);
				return m_row_ptr + m_wrap_x(m_x) * pix_width;
			}

		private:
			pixfmt_type* m_pixf;
			byte*       m_row_ptr;
			int                m_x;
			WrapX              m_wrap_x;
			WrapY              m_wrap_y;
		};

		//--------------------------------------------------------wrap_mode_repeat
		class wrap_mode_repeat
		{
		public:
			wrap_mode_repeat() {}
			wrap_mode_repeat(int size) :
				m_size(size),
				m_add(size * (0x3FFFFFFF / size)),
				m_value(0)
			{}

			int operator() (int v)
			{
				return m_value = (int(v) + m_add) % m_size;
			}

			int operator++ ()
			{
				++m_value;
				if(m_value >= m_size) m_value = 0;
				return m_value;
			}
		private:
			int m_size;
			int m_add;
			int m_value;
		};

		//---------------------------------------------------wrap_mode_repeat_pow2
		class wrap_mode_repeat_pow2
		{
		public:
			wrap_mode_repeat_pow2() {}
			wrap_mode_repeat_pow2(int size) : m_value(0)
			{
				m_mask = 1;
				while(m_mask < size) m_mask = (m_mask << 1) | 1;
				m_mask >>= 1;
			}
			int operator() (int v)
			{
				return m_value = int(v) & m_mask;
			}
			int operator++ ()
			{
				++m_value;
				if(m_value > m_mask) m_value = 0;
				return m_value;
			}
		private:
			int m_mask;
			int m_value;
		};

		//----------------------------------------------wrap_mode_repeat_auto_pow2
		class wrap_mode_repeat_auto_pow2
		{
		public:
			wrap_mode_repeat_auto_pow2() {}
			wrap_mode_repeat_auto_pow2(int size) :
				m_size(size),
				m_add(size * (0x3FFFFFFF / size)),
				m_mask((m_size & (m_size-1)) ? 0 : m_size-1),
				m_value(0)
			{}

			int operator() (int v)
			{
				if(m_mask) return m_value = int(v) & m_mask;
				return m_value = (int(v) + m_add) % m_size;
			}
			int operator++ ()
			{
				++m_value;
				if(m_value >= m_size) m_value = 0;
				return m_value;
			}

		private:
			int m_size;
			int m_add;
			int m_mask;
			int m_value;
		};

		//-------------------------------------------------------wrap_mode_reflect
		class wrap_mode_reflect
		{
		public:
			wrap_mode_reflect() {}
			wrap_mode_reflect(int size) :
				m_size(size),
				m_size2(size * 2),
				m_add(m_size2 * (0x3FFFFFFF / m_size2)),
				m_value(0)
			{}

			int operator() (int v)
			{
				m_value = (int(v) + m_add) % m_size2;
				if(m_value >= m_size) return m_size2 - m_value - 1;
				return m_value;
			}

			int operator++ ()
			{
				++m_value;
				if(m_value >= m_size2) m_value = 0;
				if(m_value >= m_size) return m_size2 - m_value - 1;
				return m_value;
			}
		private:
			int m_size;
			int m_size2;
			int m_add;
			int m_value;
		};

		//--------------------------------------------------wrap_mode_reflect_pow2
		class wrap_mode_reflect_pow2
		{
		public:
			wrap_mode_reflect_pow2() {}
			wrap_mode_reflect_pow2(int size) : m_value(0)
			{
				m_mask = 1;
				m_size = 1;
				while(m_mask < size)
				{
					m_mask = (m_mask << 1) | 1;
					m_size <<= 1;
				}
			}
			int operator() (int v)
			{
				m_value = int(v) & m_mask;
				if(m_value >= m_size) return m_mask - m_value;
				return m_value;
			}
			int operator++ ()
			{
				++m_value;
				m_value &= m_mask;
				if(m_value >= m_size) return m_mask - m_value;
				return m_value;
			}
		private:
			int m_size;
			int m_mask;
			int m_value;
		};

		//---------------------------------------------wrap_mode_reflect_auto_pow2
		class wrap_mode_reflect_auto_pow2
		{
		public:
			wrap_mode_reflect_auto_pow2() {}
			wrap_mode_reflect_auto_pow2(int size) :
				m_size(size),
				m_size2(size * 2),
				m_add(m_size2 * (0x3FFFFFFF / m_size2)),
				m_mask((m_size2 & (m_size2-1)) ? 0 : m_size2-1),
				m_value(0)
			{}

			int operator() (int v)
			{
				m_value = m_mask ? int(v) & m_mask :
								  (int(v) + m_add) % m_size2;
				if(m_value >= m_size) return m_size2 - m_value - 1;
				return m_value;
			}
			int operator++ ()
			{
				++m_value;
				if(m_value >= m_size2) m_value = 0;
				if(m_value >= m_size) return m_size2 - m_value - 1;
				return m_value;
			}

		private:
			int m_size;
			int m_size2;
			int m_add;
			int m_mask;
			int m_value;
		};
	 */

	public interface IImageBufferAccessorFloat
	{
		float[] span(int x, int y, int len, out int bufferIndex);

		float[] next_x(out int bufferFloatOffset);

		float[] next_y(out int bufferFloatOffset);

		IImageFloat SourceImage
		{
			get;
		}
	};

	public class ImageBufferAccessorCommonFloat : IImageBufferAccessorFloat
	{
		protected IImageFloat m_SourceImage;
		protected int m_x, m_x0, m_y, m_DistanceBetweenPixelsInclusive;
		protected float[] m_Buffer;
		protected int m_CurrentBufferOffset = -1;
		private int m_Width;

		public ImageBufferAccessorCommonFloat(IImageFloat pixf)
		{
			attach(pixf);
		}

		private void attach(IImageFloat pixf)
		{
			m_SourceImage = pixf;
			m_Buffer = m_SourceImage.GetBuffer();
			m_Width = m_SourceImage.Width;
			m_DistanceBetweenPixelsInclusive = m_SourceImage.GetFloatsBetweenPixelsInclusive();
		}

		public IImageFloat SourceImage
		{
			get
			{
				return m_SourceImage;
			}
		}

		private float[] pixel(out int bufferFloatOffset)
		{
			int x = m_x;
			int y = m_y;
			unchecked
			{
				if ((uint)x >= (uint)m_SourceImage.Width)
				{
					if (x < 0)
					{
						x = 0;
					}
					else
					{
						x = (int)m_SourceImage.Width - 1;
					}
				}

				if ((uint)y >= (uint)m_SourceImage.Height)
				{
					if (y < 0)
					{
						y = 0;
					}
					else
					{
						y = (int)m_SourceImage.Height - 1;
					}
				}
			}

			bufferFloatOffset = m_SourceImage.GetBufferOffsetXY(x, y);
			return m_SourceImage.GetBuffer();
		}

		public float[] span(int x, int y, int len, out int bufferOffset)
		{
			m_x = m_x0 = x;
			m_y = y;
			unchecked
			{
				if ((uint)y < (uint)m_SourceImage.Height
					&& x >= 0 && x + len <= (int)m_SourceImage.Width)
				{
					bufferOffset = m_SourceImage.GetBufferOffsetXY(x, y);
					m_Buffer = m_SourceImage.GetBuffer();
					m_CurrentBufferOffset = bufferOffset;
					return m_Buffer;
				}
			}

			m_CurrentBufferOffset = -1;
			return pixel(out bufferOffset);
		}

		public float[] next_x(out int bufferOffset)
		{
			// this is the code (managed) that the original agg used.
			// It looks like it doesn't check x but, It should be a bit faster and is valid
			// because "span" checked the whole length for good x.
			if (m_CurrentBufferOffset != -1)
			{
				m_CurrentBufferOffset += m_DistanceBetweenPixelsInclusive;
				bufferOffset = m_CurrentBufferOffset;
				return m_Buffer;
			}
			++m_x;
			return pixel(out bufferOffset);
		}

		public float[] next_y(out int bufferOffset)
		{
			++m_y;
			m_x = m_x0;
			if (m_CurrentBufferOffset != -1
				&& (uint)m_y < (uint)m_SourceImage.Height)
			{
				m_CurrentBufferOffset = m_SourceImage.GetBufferOffsetXY(m_x, m_y);
				bufferOffset = m_CurrentBufferOffset;
				return m_Buffer;
			}

			m_CurrentBufferOffset = -1;
			return pixel(out bufferOffset);
		}
	};

	public sealed class ImageBufferAccessorClipFloat : ImageBufferAccessorCommonFloat
	{
		private float[] m_OutsideBufferColor;

		public ImageBufferAccessorClipFloat(IImageFloat sourceImage, RGBA_Floats bk)
			: base(sourceImage)
		{
			m_OutsideBufferColor = new float[4];
			m_OutsideBufferColor[0] = bk.red;
			m_OutsideBufferColor[1] = bk.green;
			m_OutsideBufferColor[2] = bk.blue;
			m_OutsideBufferColor[3] = bk.alpha;
		}

		private float[] pixel(out int bufferFloatOffset)
		{
			unchecked
			{
				if (((uint)m_x < (uint)m_SourceImage.Width)
					&& ((uint)m_y < (uint)m_SourceImage.Height))
				{
					bufferFloatOffset = m_SourceImage.GetBufferOffsetXY(m_x, m_y);
					return m_SourceImage.GetBuffer();
				}
			}

			bufferFloatOffset = 0;
			return m_OutsideBufferColor;
		}

		//public void background_color(IColorType bk)
		//{
		//  m_pixf.make_pix(m_pBackBufferColor, bk);
		//}
	};
}