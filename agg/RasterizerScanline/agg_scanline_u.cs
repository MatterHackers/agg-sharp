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
// Adaptation for 32-bit screen coordinates (scanline32_u) has been sponsored by
// Liberty Technology Systems, Inc., visit http://lib-sys.com
//
// Liberty Technology Systems, Inc. is the provider of
// PostScript and PDF technology for software developers.
//
//----------------------------------------------------------------------------
namespace MatterHackers.Agg.RasterizerScanline
{
	//=============================================================scanline_u8
	//
	// Unpacked scanline container class
	//
	// This class is used to transfer data from a scanline rasterizer
	// to the rendering buffer. It's organized very simple. The class stores
	// information of horizontal spans to render it into a pixel-map buffer.
	// Each span has staring X, length, and an array of bytes that determine the
	// cover-values for each pixel.
	// Before using this class you should know the minimal and maximal pixel
	// coordinates of your scanline. The protocol of using is:
	// 1. reset(min_x, max_x)
	// 2. add_cell() / add_span() - accumulate scanline.
	//    When forming one scanline the next X coordinate must be always greater
	//    than the last stored one, i.e. it works only with ordered coordinates.
	// 3. Call finalize(y) and render the scanline.
	// 3. Call reset_spans() to prepare for the new scanline.
	//
	// 4. Rendering:
	//
	// Scanline provides an iterator class that allows you to extract
	// the spans and the cover values for each pixel. Be aware that clipping
	// has not been done yet, so you should perform it yourself.
	// Use scanline_u8::iterator to render spans:
	//-------------------------------------------------------------------------
	//
	// int y = sl.y();                    // Y-coordinate of the scanline
	//
	// ************************************
	// ...Perform vertical clipping here...
	// ************************************
	//
	// scanline_u8::const_iterator span = sl.begin();
	//
	// unsigned char* row = m_rbuf->row(y); // The address of the beginning
	//                                      // of the current row
	//
	// unsigned num_spans = sl.num_spans(); // Number of spans. It's guaranteed that
	//                                      // num_spans is always greater than 0.
	//
	// do
	// {
	//     const scanline_u8::cover_type* covers =
	//         span->covers;                     // The array of the cover values
	//
	//     int num_pix = span->len;              // Number of pixels of the span.
	//                                           // Always greater than 0, still it's
	//                                           // better to use "int" instead of
	//                                           // "unsigned" because it's more
	//                                           // convenient for clipping
	//     int x = span->x;
	//
	//     **************************************
	//     ...Perform horizontal clipping here...
	//     ...you have x, covers, and pix_count..
	//     **************************************
	//
	//     unsigned char* dst = row + x;  // Calculate the start address of the row.
	//                                    // In this case we assume a simple
	//                                    // grayscale image 1-byte per pixel.
	//     do
	//     {
	//         *dst++ = *covers++;        // Hypothetical rendering.
	//     }
	//     while(--num_pix);
	//
	//     ++span;
	// }
	// while(--num_spans);  // num_spans cannot be 0, so this loop is quite safe
	//------------------------------------------------------------------------
	//
	// The question is: why should we accumulate the whole scanline when we
	// could render just separate spans when they're ready?
	// That's because using the scanline is generally faster. When is consists
	// of more than one span the conditions for the processor cash system
	// are better, because switching between two different areas of memory
	// (that can be very large) occurs less frequently.
	//------------------------------------------------------------------------
	public sealed class scanline_unpacked_8 : IScanlineCache
	{
		private int m_min_x;
		private int m_last_x;
		private int m_y;
		private ArrayPOD<byte> m_covers;
		private ArrayPOD<ScanlineSpan> m_spans;
		private int m_span_index;
		private int m_interator_index;

		public ScanlineSpan GetNextScanlineSpan()
		{
			m_interator_index++;
			return m_spans.Array[m_interator_index - 1];
		}

		//--------------------------------------------------------------------
		public scanline_unpacked_8()
		{
			m_last_x = (0x7FFFFFF0);
			m_covers = new ArrayPOD<byte>(1000);
			m_spans = new ArrayPOD<ScanlineSpan>(1000);
		}

		//--------------------------------------------------------------------
		public void reset(int min_x, int max_x)
		{
			int max_len = max_x - min_x + 2;
			if (max_len > m_spans.Size())
			{
				m_spans.Resize(max_len);
				m_covers.Resize(max_len);
			}
			m_last_x = 0x7FFFFFF0;
			m_min_x = min_x;
			m_span_index = 0;
		}

		//--------------------------------------------------------------------
		public void add_cell(int x, int cover)
		{
			x -= m_min_x;
			m_covers.Array[x] = (byte)cover;
			if (x == m_last_x + 1)
			{
				m_spans.Array[m_span_index].len++;
			}
			else
			{
				m_span_index++;
				m_spans.Array[m_span_index].x = x + m_min_x;
				m_spans.Array[m_span_index].len = 1;
				m_spans.Array[m_span_index].cover_index = (int)x;
			}
			m_last_x = x;
		}

		/*
		//--------------------------------------------------------------------
		unsafe public void add_cells(int x, int len, byte* covers)
		{
			x -= m_min_x;
			for (int i = 0; i < len; i++)
			{
				m_covers.Array[x + i] = covers[i];
			}
			if (x == m_last_x + 1)
			{
				m_spans.Array[m_span_index].len += (int)len;
			}
			else
			{
				m_span_index++;
				m_spans.Array[m_span_index].x = x + m_min_x;
				m_spans.Array[m_span_index].len = (int)len;
				m_spans.Array[m_span_index].cover_index = x;
			}
			m_last_x = x + (int)len - 1;
		}
		 */

		//--------------------------------------------------------------------
		public void add_span(int x, int len, int cover)
		{
			x -= m_min_x;
			for (int i = 0; i < len; i++)
			{
				m_covers.Array[x + i] = (byte)cover;
			}

			if (x == m_last_x + 1)
			{
				m_spans.Array[m_span_index].len += (int)len;
			}
			else
			{
				m_span_index++;
				m_spans.Array[m_span_index].x = x + m_min_x;
				m_spans.Array[m_span_index].len = (int)len;
				m_spans.Array[m_span_index].cover_index = (int)x;
			}
			m_last_x = x + (int)len - 1;
		}

		//--------------------------------------------------------------------
		public void finalize(int y)
		{
			m_y = y;
		}

		//--------------------------------------------------------------------
		public void ResetSpans()
		{
			m_last_x = 0x7FFFFFF0;
			m_span_index = 0;
		}

		//--------------------------------------------------------------------
		public int y()
		{
			return m_y;
		}

		public int num_spans()
		{
			return (int)m_span_index;
		}

		public ScanlineSpan begin()
		{
			m_interator_index = 1;
			return GetNextScanlineSpan();
		}

		public byte[] GetCovers()
		{
			return m_covers.Array;
		}
	};
}