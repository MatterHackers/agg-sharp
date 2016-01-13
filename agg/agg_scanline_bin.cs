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
// Class scanline_bin - binary scanline.
//
//----------------------------------------------------------------------------
//
// Adaptation for 32-bit screen coordinates (scanline32_bin) has been sponsored by
// Liberty Technology Systems, Inc., visit http://lib-sys.com
//
// Liberty Technology Systems, Inc. is the provider of
// PostScript and PDF technology for software developers.
//
//----------------------------------------------------------------------------

namespace MatterHackers.Agg
{
	//=============================================================scanline_bin
	//
	// This is binary scanline container which supports the interface
	// used in the rasterizer::render(). See description of agg_scanline_u8
	// for details.
	//
	//------------------------------------------------------------------------
	public sealed class scanline_bin : IScanlineCache
	{
		private int m_last_x;
		private int m_y;
		private ArrayPOD<ScanlineSpan> m_spans;
		private int m_span_index;
		private int m_interator_index;

		public ScanlineSpan GetNextScanlineSpan()
		{
			m_interator_index++;
			return m_spans.Array[m_interator_index - 1];
		}

		//--------------------------------------------------------------------
		public scanline_bin()
		{
			m_last_x = (0x7FFFFFF0);
			m_spans = new ArrayPOD<ScanlineSpan>(1000);
			m_span_index = 0;
		}

		//--------------------------------------------------------------------
		public void reset(int min_x, int max_x)
		{
			int max_len = max_x - min_x + 3;
			if (max_len > m_spans.Size())
			{
				m_spans.Resize(max_len);
			}
			m_last_x = 0x7FFFFFF0;
			m_span_index = 0;
		}

		//--------------------------------------------------------------------
		public void add_cell(int x, int cover)
		{
			if (x == m_last_x + 1)
			{
				m_spans.Array[m_span_index].len++;
			}
			else
			{
				m_span_index++;
				m_spans.Array[m_span_index].x = (int)x;
				m_spans.Array[m_span_index].len = 1;
			}
			m_last_x = x;
		}

		//--------------------------------------------------------------------
		public void add_span(int x, int len, int cover)
		{
			if (x == m_last_x + 1)
			{
				m_spans.Array[m_span_index].len += (int)len;
			}
			else
			{
				m_span_index++;
				m_spans.Array[m_span_index].x = x;
				m_spans.Array[m_span_index].len = (int)len;
			}
			m_last_x = x + len - 1;
		}

		/*
		//--------------------------------------------------------------------
		public void add_cells(int x, int len, void*)
		{
			add_span(x, len, 0);
		}
		 */

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
			return null;
		}
	};

	/*
//===========================================================scanline32_bin
class scanline32_bin
{
public:
	typedef int32 coord_type;

	//--------------------------------------------------------------------
	struct span
	{
		span() {}
		span(coord_type x_, coord_type len_) : x(x_), len(len_) {}

		coord_type x;
		coord_type len;
	};
	typedef pod_bvector<span, 4> span_array_type;

	//--------------------------------------------------------------------
	class_iterator
	{
	public:
	   _iterator(span_array_type& spans) :
			m_spans(spans),
			m_span_idx(0)
		{}

		span& operator*()  { return m_spans[m_span_idx];  }
		span* operator->() { return &m_spans[m_span_idx]; }

		void operator ++ () { ++m_span_idx; }

	private:
		span_array_type& m_spans;
		int               m_span_idx;
	};

	//--------------------------------------------------------------------
	scanline32_bin() : m_max_len(0), m_last_x(0x7FFFFFF0) {}

	//--------------------------------------------------------------------
	void reset(int min_x, int max_x)
	{
		m_last_x = 0x7FFFFFF0;
		m_spans.remove_all();
	}

	//--------------------------------------------------------------------
	void add_cell(int x, int)
	{
		if(x == m_last_x+1)
		{
			m_spans.last().len++;
		}
		else
		{
			m_spans.add(span(coord_type(x), 1));
		}
		m_last_x = x;
	}

	//--------------------------------------------------------------------
	void add_span(int x, int len, int)
	{
		if(x == m_last_x+1)
		{
			m_spans.last().len += coord_type(len);
		}
		else
		{
			m_spans.add(span(coord_type(x), coord_type(len)));
		}
		m_last_x = x + len - 1;
	}

	//--------------------------------------------------------------------
	void add_cells(int x, int len, void*)
	{
		add_span(x, len, 0);
	}

	//--------------------------------------------------------------------
	void finalize(int y)
	{
		m_y = y;
	}

	//--------------------------------------------------------------------
	void reset_spans()
	{
		m_last_x = 0x7FFFFFF0;
		m_spans.remove_all();
	}

	//--------------------------------------------------------------------
	int            y()         { return m_y; }
	int       num_spans() { return m_spans.size(); }
   _iterator begin()     { return_iterator(m_spans); }

private:
	scanline32_bin(scanline32_bin&);
	scanline32_bin operator = (scanline32_bin&);

	int        m_max_len;
	int             m_last_x;
	int             m_y;
	span_array_type m_spans;
};
	 */
}