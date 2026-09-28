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
using System;

namespace MatterHackers.Agg
{
	public interface ISpanInterpolator
	{
		void begin(double x, double y, int len);

		void coordinates(out int x, out int y);

		void Next();

		Transform.ITransform transformer();

		void transformer(Transform.ITransform trans);

		void resynchronize(double xe, double ye, int len);

		void local_scale(out int x, out int y);
	};

	//================================================span_interpolator_linear
	public sealed class span_interpolator_linear : ISpanInterpolator
	{
		private Transform.ITransform m_trans;
		private dda2_line_interpolator m_li_x;
		private dda2_line_interpolator m_li_y;

		public enum subpixel_scale_e
		{
			SubpixelShift = 8,
			subpixel_shift = SubpixelShift,
			subpixel_scale = 1 << subpixel_shift
		};

		//--------------------------------------------------------------------
		public span_interpolator_linear()
		{
		}

		public span_interpolator_linear(Transform.ITransform trans)
		{
			m_trans = trans;
		}

		public span_interpolator_linear(Transform.ITransform trans, double x, double y, int len)
		{
			m_trans = trans;
			begin(x, y, len);
		}

		//----------------------------------------------------------------
		public Transform.ITransform transformer()
		{
			return m_trans;
		}

		public void transformer(Transform.ITransform trans)
		{
			m_trans = trans;
		}

		public void local_scale(out int x, out int y)
		{
			throw new System.NotImplementedException();
		}

		//----------------------------------------------------------------
		public void begin(double x, double y, int len)
		{
			double tx;
			double ty;

			tx = x;
			ty = y;
			m_trans.Transform(ref tx, ref ty);
			int x1 = Util.iround(tx * (double)subpixel_scale_e.subpixel_scale);
			int y1 = Util.iround(ty * (double)subpixel_scale_e.subpixel_scale);

			tx = x + len;
			ty = y;
			m_trans.Transform(ref tx, ref ty);
			int x2 = Util.iround(tx * (double)subpixel_scale_e.subpixel_scale);
			int y2 = Util.iround(ty * (double)subpixel_scale_e.subpixel_scale);

			m_li_x = new dda2_line_interpolator(x1, x2, (int)len);
			m_li_y = new dda2_line_interpolator(y1, y2, (int)len);
		}

		//----------------------------------------------------------------
		public void resynchronize(double xe, double ye, int len)
		{
			m_trans.Transform(ref xe, ref ye);
			m_li_x = new dda2_line_interpolator(m_li_x.y(), Util.iround(xe * (double)subpixel_scale_e.subpixel_scale), (int)len);
			m_li_y = new dda2_line_interpolator(m_li_y.y(), Util.iround(ye * (double)subpixel_scale_e.subpixel_scale), (int)len);
		}

		//----------------------------------------------------------------
		//public void operator++()
		public void Next()
		{
			m_li_x.Next();
			m_li_y.Next();
		}

		//----------------------------------------------------------------
		public void coordinates(out int x, out int y)
		{
			x = m_li_x.y();
			y = m_li_y.y();
		}
	};

	public interface ISpanInterpolatorFloat
	{
		void begin(double x, double y, int len);

		void coordinates(out float x, out float y);

		void Next();

		Transform.ITransform transformer();

		void transformer(Transform.ITransform trans);

		void resynchronize(double xe, double ye, int len);

		void local_scale(out double x, out double y);
	};

	//================================================span_interpolator_linear
	public sealed class span_interpolator_linear_float : ISpanInterpolatorFloat
	{
		private Transform.ITransform m_trans;
		private float currentX;
		private float stepX;
		private float currentY;
		private float stepY;

		public span_interpolator_linear_float()
		{
		}

		public span_interpolator_linear_float(Transform.ITransform trans)
		{
			m_trans = trans;
		}

		public span_interpolator_linear_float(Transform.ITransform trans, double x, double y, int len)
		{
			m_trans = trans;
			begin(x, y, len);
		}

		//----------------------------------------------------------------
		public Transform.ITransform transformer()
		{
			return m_trans;
		}

		public void transformer(Transform.ITransform trans)
		{
			m_trans = trans;
		}

		public void local_scale(out double x, out double y)
		{
			throw new System.NotImplementedException();
		}

		//----------------------------------------------------------------
		public void begin(double x, double y, int len)
		{
			double tx;
			double ty;

			tx = x;
			ty = y;
			m_trans.Transform(ref tx, ref ty);
			currentX = (float)tx;
			currentY = (float)ty;

			tx = x + len;
			ty = y;
			m_trans.Transform(ref tx, ref ty);
			stepX = (float)((tx - currentX) / len);
			stepY = (float)((ty - currentY) / len);
		}

		//----------------------------------------------------------------
		public void resynchronize(double xe, double ye, int len)
		{
			throw new NotImplementedException();
			//m_trans.transform(ref xe, ref ye);
			//m_li_x = new dda2_line_interpolator(m_li_x.y(), agg_basics.iround(xe * (double)subpixel_scale_e.subpixel_scale), (int)len);
			//m_li_y = new dda2_line_interpolator(m_li_y.y(), agg_basics.iround(ye * (double)subpixel_scale_e.subpixel_scale), (int)len);
		}

		//----------------------------------------------------------------
		//public void operator++()
		public void Next()
		{
			currentX += stepX;
			currentY += stepY;
		}

		//----------------------------------------------------------------
		public void coordinates(out float x, out float y)
		{
			x = (float)currentX;
			y = (float)currentY;
		}
	};

	//=====================================span_interpolator_linear_subdiv
	/// <summary>
	/// C++ span_interpolator_linear_subdiv (SubpixelShift 8): span_interpolator_linear, but it transforms the exact
	/// point again every 2^subdiv_shift pixels (16 by default) and steps a dda line only between those, so a
	/// perspective transform stays close to exact along a long span at a fraction of the cost.
	/// </summary>
	public sealed class span_interpolator_linear_subdiv : ISpanInterpolator
	{
		private const int subpixel_shift = 8;
		private const int subpixel_scale = 1 << subpixel_shift;

		private Transform.ITransform m_trans;
		private int m_subdiv_shift;
		private int m_subdiv_size;
		private dda2_line_interpolator m_li_x;
		private dda2_line_interpolator m_li_y;
		private int m_src_x;
		private double m_src_y;
		private int m_pos;
		private int m_len;

		public span_interpolator_linear_subdiv(Transform.ITransform trans, int subdiv_shift = 4)
		{
			m_trans = trans;
			this.subdiv_shift(subdiv_shift);
		}

		public Transform.ITransform transformer() => m_trans;

		public void transformer(Transform.ITransform trans) => m_trans = trans;

		public int subdiv_shift() => m_subdiv_shift;

		public void subdiv_shift(int shift)
		{
			m_subdiv_shift = shift;
			m_subdiv_size = 1 << m_subdiv_shift;
		}

		public void local_scale(out int x, out int y)
		{
			throw new System.NotImplementedException();
		}

		public void resynchronize(double xe, double ye, int len)
		{
			throw new System.NotImplementedException();
		}

		public void begin(double x, double y, int len)
		{
			m_pos = 1;
			m_src_x = Util.iround(x * subpixel_scale) + subpixel_scale;
			m_src_y = y;
			m_len = len;

			if (len > m_subdiv_size)
			{
				len = m_subdiv_size;
			}

			double tx = x;
			double ty = y;
			m_trans.Transform(ref tx, ref ty);
			int x1 = Util.iround(tx * subpixel_scale);
			int y1 = Util.iround(ty * subpixel_scale);

			tx = x + len;
			ty = y;
			m_trans.Transform(ref tx, ref ty);

			m_li_x = new dda2_line_interpolator(x1, Util.iround(tx * subpixel_scale), len);
			m_li_y = new dda2_line_interpolator(y1, Util.iround(ty * subpixel_scale), len);
		}

		public void Next()
		{
			m_li_x.Next();
			m_li_y.Next();
			if (m_pos >= m_subdiv_size)
			{
				int len = m_len;
				if (len > m_subdiv_size)
				{
					len = m_subdiv_size;
				}

				double tx = (double)m_src_x / subpixel_scale + len;
				double ty = m_src_y;
				m_trans.Transform(ref tx, ref ty);
				m_li_x = new dda2_line_interpolator(m_li_x.y(), Util.iround(tx * subpixel_scale), len);
				m_li_y = new dda2_line_interpolator(m_li_y.y(), Util.iround(ty * subpixel_scale), len);
				m_pos = 0;
			}

			m_src_x += subpixel_scale;
			++m_pos;
			--m_len;
		}

		public void coordinates(out int x, out int y)
		{
			x = m_li_x.y();
			y = m_li_y.y();
		}
	}
}