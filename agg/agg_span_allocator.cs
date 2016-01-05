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
	//----------------------------------------------------------span_allocator
	public class span_allocator
	{
		private ArrayPOD<RGBA_Bytes> m_span;

		public span_allocator()
		{
			m_span = new ArrayPOD<RGBA_Bytes>(255);
		}

		//--------------------------------------------------------------------
		public ArrayPOD<RGBA_Bytes> allocate(int span_len)
		{
			if (span_len > m_span.Size())
			{
				// To reduce the number of reallocs we align the
				// span_len to 256 color elements.
				// Well, I just like this number and it looks reasonable.
				//-----------------------
				m_span.Resize((((int)span_len + 255) >> 8) << 8);
			}
			return m_span;
		}

		public ArrayPOD<RGBA_Bytes> span()
		{
			return m_span;
		}

		public int max_span_len()
		{
			return m_span.Size();
		}
	};
}