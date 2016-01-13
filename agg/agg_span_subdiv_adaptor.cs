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

namespace MatterHackers.Agg
{
	//=================================================span_subdiv_adaptor
	public class span_subdiv_adaptor : ISpanInterpolator
	{
		private int m_subdiv_shift;
		private int m_subdiv_size;
		private int m_subdiv_mask;
		private ISpanInterpolator m_interpolator;
		private int m_src_x;
		private double m_src_y;
		private int m_pos;
		private int m_len;

		private const int subpixel_shift = 8;
		private const int subpixel_scale = 1 << subpixel_shift;

		//----------------------------------------------------------------
		public span_subdiv_adaptor(ISpanInterpolator interpolator)
			: this(interpolator, 4)
		{
		}

		public span_subdiv_adaptor(ISpanInterpolator interpolator, int subdiv_shift)
		{
			m_subdiv_shift = subdiv_shift;
			m_subdiv_size = 1 << m_subdiv_shift;
			m_subdiv_mask = m_subdiv_size - 1;
			m_interpolator = interpolator;
		}

		public span_subdiv_adaptor(ISpanInterpolator interpolator,
							 double x, double y, int len,
							 int subdiv_shift)
			: this(interpolator, subdiv_shift)
		{
			begin(x, y, len);
		}

		public void resynchronize(double xe, double ye, int len)
		{
			throw new System.NotImplementedException();
		}

		//----------------------------------------------------------------
		public ISpanInterpolator interpolator()
		{
			return m_interpolator;
		}

		public void interpolator(ISpanInterpolator intr)
		{
			m_interpolator = intr;
		}

		//----------------------------------------------------------------
		public Transform.ITransform transformer()
		{
			return m_interpolator.transformer();
		}

		public void transformer(Transform.ITransform trans)
		{
			m_interpolator.transformer(trans);
		}

		//----------------------------------------------------------------
		public int subdiv_shift()
		{
			return m_subdiv_shift;
		}

		public void subdiv_shift(int shift)
		{
			m_subdiv_shift = shift;
			m_subdiv_size = 1 << m_subdiv_shift;
			m_subdiv_mask = m_subdiv_size - 1;
		}

		//----------------------------------------------------------------
		public void begin(double x, double y, int len)
		{
			m_pos = 1;
			m_src_x = agg_basics.iround(x * subpixel_scale) + subpixel_scale;
			m_src_y = y;
			m_len = len;
			if (len > m_subdiv_size) len = (int)m_subdiv_size;
			m_interpolator.begin(x, y, len);
		}

		//----------------------------------------------------------------
		public void Next()
		{
			m_interpolator.Next();
			if (m_pos >= m_subdiv_size)
			{
				int len = m_len;
				if (len > m_subdiv_size) len = (int)m_subdiv_size;
				m_interpolator.resynchronize((double)m_src_x / (double)subpixel_scale + len,
											  m_src_y,
											  len);
				m_pos = 0;
			}
			m_src_x += subpixel_scale;
			++m_pos;
			--m_len;
		}

		//----------------------------------------------------------------
		public void coordinates(out int x, out int y)
		{
			m_interpolator.coordinates(out x, out y);
		}

		//----------------------------------------------------------------
		public void local_scale(out int x, out int y)
		{
			m_interpolator.local_scale(out x, out y);
		}
	};
}