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
			m_trans.transform(ref tx, ref ty);
			int x1 = agg_basics.iround(tx * (double)subpixel_scale_e.subpixel_scale);
			int y1 = agg_basics.iround(ty * (double)subpixel_scale_e.subpixel_scale);

			tx = x + len;
			ty = y;
			m_trans.transform(ref tx, ref ty);
			int x2 = agg_basics.iround(tx * (double)subpixel_scale_e.subpixel_scale);
			int y2 = agg_basics.iround(ty * (double)subpixel_scale_e.subpixel_scale);

			m_li_x = new dda2_line_interpolator(x1, x2, (int)len);
			m_li_y = new dda2_line_interpolator(y1, y2, (int)len);
		}

		//----------------------------------------------------------------
		public void resynchronize(double xe, double ye, int len)
		{
			m_trans.transform(ref xe, ref ye);
			m_li_x = new dda2_line_interpolator(m_li_x.y(), agg_basics.iround(xe * (double)subpixel_scale_e.subpixel_scale), (int)len);
			m_li_y = new dda2_line_interpolator(m_li_y.y(), agg_basics.iround(ye * (double)subpixel_scale_e.subpixel_scale), (int)len);
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
			m_trans.transform(ref tx, ref ty);
			currentX = (float)tx;
			currentY = (float)ty;

			tx = x + len;
			ty = y;
			m_trans.transform(ref tx, ref ty);
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

	/*
		//=====================================span_interpolator_linear_subdiv
		template<class Transformer = ITransformer, int SubpixelShift = 8>
		class span_interpolator_linear_subdiv
		{
		public:
			typedef Transformer trans_type;

			enum subpixel_scale_e
			{
				subpixel_shift = SubpixelShift,
				subpixel_scale = 1 << subpixel_shift
			};

			//----------------------------------------------------------------
			span_interpolator_linear_subdiv() :
				m_subdiv_shift(4),
				m_subdiv_size(1 << m_subdiv_shift),
				m_subdiv_mask(m_subdiv_size - 1) {}

			span_interpolator_linear_subdiv(const trans_type& trans,
											int subdiv_shift = 4) :
				m_subdiv_shift(subdiv_shift),
				m_subdiv_size(1 << m_subdiv_shift),
				m_subdiv_mask(m_subdiv_size - 1),
				m_trans(&trans) {}

			span_interpolator_linear_subdiv(const trans_type& trans,
											double x, double y, int len,
											int subdiv_shift = 4) :
				m_subdiv_shift(subdiv_shift),
				m_subdiv_size(1 << m_subdiv_shift),
				m_subdiv_mask(m_subdiv_size - 1),
				m_trans(&trans)
			{
				begin(x, y, len);
			}

			//----------------------------------------------------------------
			const trans_type& transformer() const { return *m_trans; }
			void transformer(const trans_type& trans) { m_trans = &trans; }

			//----------------------------------------------------------------
			int subdiv_shift() const { return m_subdiv_shift; }
			void subdiv_shift(int shift)
			{
				m_subdiv_shift = shift;
				m_subdiv_size = 1 << m_subdiv_shift;
				m_subdiv_mask = m_subdiv_size - 1;
			}

			//----------------------------------------------------------------
			void begin(double x, double y, int len)
			{
				double tx;
				double ty;
				m_pos   = 1;
				m_src_x = iround(x * subpixel_scale) + subpixel_scale;
				m_src_y = y;
				m_len   = len;

				if(len > m_subdiv_size) len = m_subdiv_size;
				tx = x;
				ty = y;
				m_trans->transform(&tx, &ty);
				int x1 = iround(tx * subpixel_scale);
				int y1 = iround(ty * subpixel_scale);

				tx = x + len;
				ty = y;
				m_trans->transform(&tx, &ty);

				m_li_x = dda2_line_interpolator(x1, iround(tx * subpixel_scale), len);
				m_li_y = dda2_line_interpolator(y1, iround(ty * subpixel_scale), len);
			}

			//----------------------------------------------------------------
			void operator++()
			{
				++m_li_x;
				++m_li_y;
				if(m_pos >= m_subdiv_size)
				{
					int len = m_len;
					if(len > m_subdiv_size) len = m_subdiv_size;
					double tx = double(m_src_x) / double(subpixel_scale) + len;
					double ty = m_src_y;
					m_trans->transform(&tx, &ty);
					m_li_x = dda2_line_interpolator(m_li_x.y(), iround(tx * subpixel_scale), len);
					m_li_y = dda2_line_interpolator(m_li_y.y(), iround(ty * subpixel_scale), len);
					m_pos = 0;
				}
				m_src_x += subpixel_scale;
				++m_pos;
				--m_len;
			}

			//----------------------------------------------------------------
			void coordinates(int* x, int* y) const
			{
				*x = m_li_x.y();
				*y = m_li_y.y();
			}

		private:
			int m_subdiv_shift;
			int m_subdiv_size;
			int m_subdiv_mask;
			const trans_type* m_trans;
			dda2_line_interpolator m_li_x;
			dda2_line_interpolator m_li_y;
			int      m_src_x;
			double   m_src_y;
			int m_pos;
			int m_len;
		};

	 */
}