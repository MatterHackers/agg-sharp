//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
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
using System;

namespace MatterHackers.Agg.Image
{
	//==================================================pixfmt_amask_adaptor
	public sealed class AlphaMaskAdaptor : ImageProxy
	{
		private IAlphaMask m_mask;
		private ArrayPOD<byte> m_span;

		private enum span_extra_tail_e { span_extra_tail = 256 };

		private static readonly byte cover_full = 255;

		private void realloc_span(int len)
		{
			if (len > m_span.Size())
			{
				m_span.Resize(len + (int)span_extra_tail_e.span_extra_tail);
			}
		}

		private void init_span(int len)
		{
			init_span(len, cover_full);
		}

		private void init_span(int len, byte cover)
		{
			realloc_span(len);
			agg_basics.memset(m_span.Array, 0, cover, len);
		}

		private void init_span(int len, byte[] covers, int coversIndex)
		{
			realloc_span(len);
			byte[] array = m_span.Array;
			for (int i = 0; i < (int)len; i++)
			{
				array[i] = covers[coversIndex + i];
			}
		}

		public AlphaMaskAdaptor(IImageByte image, IAlphaMask mask)
			: base(image)
		{
			linkedImage = image;
			m_mask = mask;
			m_span = new ArrayPOD<byte>(255);
		}

		public void AttachImage(IImageByte image)
		{
			linkedImage = image;
		}

		public void attach_alpha_mask(IAlphaMask mask)
		{
			m_mask = mask;
		}

		public void copy_pixel(int x, int y, RGBA_Bytes c)
		{
			linkedImage.BlendPixel(x, y, c, m_mask.pixel(x, y));
		}

		public override void copy_hline(int x, int y, int len, RGBA_Bytes c)
		{
			throw new NotImplementedException();
			/*
						realloc_span((int)len);
						unsafe
						{
							fixed (byte* pBuffer = m_span.Array)
							{
								m_mask.fill_hspan(x, y, pBuffer, (int)len);
								m_LinkedImage.blend_solid_hspan(x, y, len, c, pBuffer);
							}
						}
			 */
		}

		public override void blend_hline(int x1, int y, int x2, RGBA_Bytes c, byte cover)
		{
			int len = x2 - x1 + 1;
			if (cover == cover_full)
			{
				realloc_span(len);
				m_mask.combine_hspanFullCover(x1, y, m_span.Array, 0, (int)len);
				linkedImage.blend_solid_hspan(x1, y, (int)len, c, m_span.Array, 0);
			}
			else
			{
				init_span(len, cover);
				m_mask.combine_hspan(x1, y, m_span.Array, 0, (int)len);
				linkedImage.blend_solid_hspan(x1, y, (int)len, c, m_span.Array, 0);
			}
		}

		public override void copy_vline(int x, int y, int len, RGBA_Bytes c)
		{
			throw new NotImplementedException(); /*
            realloc_span((int)len);
            unsafe
            {
                fixed (byte* pBuffer = m_span.Array)
                {
                    m_mask.fill_vspan(x, y, pBuffer, (int)len);
                    m_LinkedImage.blend_solid_vspan(x, y, len, c, pBuffer);
                }
            }
                                                  */
		}

		public override void blend_vline(int x, int y1, int y2, RGBA_Bytes c, byte cover)
		{
			throw new NotImplementedException(); /*
            int len = y2 - y1 + 1;
            init_span(len, cover);
            unsafe
            {
                fixed (byte* pBuffer = m_span.Array)
                {
                    m_mask.combine_vspan(x, y1, pBuffer, len);
                    throw new System.NotImplementedException("blend_solid_vspan does not take a y2 yet");
                    //m_pixf.blend_solid_vspan(x, y1, y2, c, pBuffer);
                }
            }
                                                  */
		}

		public override void blend_solid_hspan(int x, int y, int len, RGBA_Bytes color, byte[] covers, int coversIndex)
		{
			byte[] buffer = m_span.Array;
			m_mask.combine_hspan(x, y, covers, coversIndex, len);
			linkedImage.blend_solid_hspan(x, y, len, color, covers, coversIndex);
		}

		public override void blend_solid_vspan(int x, int y, int len, RGBA_Bytes c, byte[] covers, int coversIndex)
		{
			throw new System.NotImplementedException();
#if false
            init_span((int)len, covers);
            unsafe
            {
                fixed (byte* pBuffer = m_span.Array)
                {
                    m_mask.combine_vspan(x, y, pBuffer, (int)len);
                    m_LinkedImage.blend_solid_vspan(x, y, len, c, pBuffer);
                }
            }
#endif
		}

		public override void copy_color_hspan(int x, int y, int len, RGBA_Bytes[] colors, int colorsIndex)
		{
			throw new System.NotImplementedException();
#if false
            realloc_span((int)len);
            unsafe
            {
                fixed (byte* pBuffer = m_span.GetArray())
                {
                    m_mask.fill_hspan(x, y, pBuffer, (int)len);
                    m_pixf.blend_color_hspan(x, y, len, colors, pBuffer, cover_full);
                }
            }
#endif
		}

		public override void copy_color_vspan(int x, int y, int len, RGBA_Bytes[] colors, int colorsIndex)
		{
			throw new System.NotImplementedException();
#if false
            realloc_span((int)len);
            unsafe
            {
                fixed (byte* pBuffer = m_span.GetArray())
                {
                    m_mask.fill_vspan(x, y, pBuffer, (int)len);
                    m_pixf.blend_color_vspan(x, y, len, colors, pBuffer, cover_full);
                }
            }
#endif
		}

		public override void blend_color_hspan(int x, int y, int len, RGBA_Bytes[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			throw new System.NotImplementedException();
#if false
            unsafe
            {
                fixed (byte* pBuffer = m_span.GetArray())
                {
                    if (covers != null)
                    {
                        init_span((int)len, covers);
                        m_mask.combine_hspan(x, y, pBuffer, (int)len);
                    }
                    else
                    {
                        realloc_span((int)len);
                        m_mask.fill_hspan(x, y, pBuffer, (int)len);
                    }
                    m_pixf.blend_color_hspan(x, y, len, colors, pBuffer, cover);
                }
            }
#endif
		}

		public override void blend_color_vspan(int x, int y, int len, RGBA_Bytes[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			throw new System.NotImplementedException();
#if false
            unsafe
            {
                fixed (byte* pBuffer = m_span.GetArray())
                {
                    if (covers != null)
                    {
                        init_span((int)len, covers);
                        m_mask.combine_vspan(x, y, pBuffer, (int)len);
                    }
                    else
                    {
                        realloc_span((int)len);
                        m_mask.fill_vspan(x, y, pBuffer, (int)len);
                    }
                    m_pixf.blend_color_vspan(x, y, len, colors, pBuffer, cover);
                }
            }
#endif
		}
	};
}