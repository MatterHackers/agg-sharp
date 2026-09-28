//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2026 Lars Brubaker
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

namespace MatterHackers.Agg
{
	/// <summary>
	/// C++ AGG's span_interpolator_trans: transforms every pixel center of the span exactly, with no linear
	/// interpolation between the ends - slower than span_interpolator_linear, but right for non-linear
	/// transforms such as a perspective one.
	/// </summary>
	public sealed class span_interpolator_trans : ISpanInterpolator
	{
		private const int subpixel_scale = 1 << 8;

		private Transform.ITransform m_trans;

		private double m_x;

		private double m_y;

		private int m_ix;

		private int m_iy;

		public span_interpolator_trans(Transform.ITransform trans)
		{
			m_trans = trans;
		}

		public Transform.ITransform transformer()
		{
			return m_trans;
		}

		public void transformer(Transform.ITransform trans)
		{
			m_trans = trans;
		}

		public void begin(double x, double y, int len)
		{
			m_x = x;
			m_y = y;
			Update();
		}

		public void Next()
		{
			m_x += 1.0;
			Update();
		}

		public void coordinates(out int x, out int y)
		{
			x = m_ix;
			y = m_iy;
		}

		// Every pixel is transformed exactly, so there is nothing to resynchronize.
		public void resynchronize(double xe, double ye, int len)
		{
		}

		public void local_scale(out int x, out int y)
		{
			throw new NotImplementedException();
		}

		private void Update()
		{
			double x = m_x;
			double y = m_y;
			m_trans.Transform(ref x, ref y);
			m_ix = Util.iround(x * subpixel_scale);
			m_iy = Util.iround(y * subpixel_scale);
		}
	}
}
