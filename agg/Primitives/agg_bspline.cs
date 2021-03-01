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
// class bspline
//
//----------------------------------------------------------------------------

namespace MatterHackers.Agg
{
	//----------------------------------------------------------------bspline
	// A very simple class of Bi-cubic Spline interpolation.
	// First call init(num, x[], y[]) where num - number of source points,
	// x, y - arrays of X and Y values respectively. Here Y must be a function
	// of X. It means that all the X-coordinates must be arranged in the ascending
	// order.
	// Then call get(x) that calculates a value Y for the respective X.
	// The class supports extrapolation, i.e. you can call get(x) where x is
	// outside the given with init() X-range. Extrapolation is a simple linear
	// function.
	//------------------------------------------------------------------------
	public sealed class bspline
	{
		private int m_max;
		private int m_num;
		private int m_xOffset;
		private int m_yOffset;
		private ArrayPOD<double> m_am = new ArrayPOD<double>(16);
		private int m_last_idx;

		//------------------------------------------------------------------------
		public bspline()
		{
			m_max = (0);
			m_num = (0);
			m_xOffset = (0);
			m_yOffset = (0);
			m_last_idx = -1;
		}

		//------------------------------------------------------------------------
		public bspline(int num)
		{
			m_max = (0);
			m_num = (0);
			m_xOffset = (0);
			m_yOffset = (0);
			m_last_idx = -1;

			init(num);
		}

		//------------------------------------------------------------------------
		public bspline(int num, double[] x, double[] y)
		{
			m_max = (0);
			m_num = (0);
			m_xOffset = (0);
			m_yOffset = (0);
			m_last_idx = -1;
			init(num, x, y);
		}

		//------------------------------------------------------------------------
		public void init(int max)
		{
			if (max > 2 && max > m_max)
			{
				m_am.Resize(max * 3);
				m_max = max;
				m_xOffset = m_max;
				m_yOffset = m_max * 2;
			}
			m_num = 0;
			m_last_idx = -1;
		}

		//------------------------------------------------------------------------
		public void add_point(double x, double y)
		{
			if (m_num < m_max)
			{
				m_am[m_xOffset + m_num] = x;
				m_am[m_yOffset + m_num] = y;
				++m_num;
			}
		}

		//------------------------------------------------------------------------
		public void prepare()
		{
			if (m_num > 2)
			{
				int i, k;
				int r;
				int s;
				double h, p, d, f, e;

				for (k = 0; k < m_num; k++)
				{
					m_am[k] = 0.0;
				}

				int n1 = 3 * m_num;

				ArrayPOD<double> al = new ArrayPOD<double>(n1);

				for (k = 0; k < n1; k++)
				{
					al[k] = 0.0;
				}

				r = m_num;
				s = m_num * 2;

				n1 = m_num - 1;
				d = m_am[m_xOffset + 1] - m_am[m_xOffset + 0];
				e = (m_am[m_yOffset + 1] - m_am[m_yOffset + 0]) / d;

				for (k = 1; k < n1; k++)
				{
					h = d;
					d = m_am[m_xOffset + k + 1] - m_am[m_xOffset + k];
					f = e;
					e = (m_am[m_yOffset + k + 1] - m_am[m_yOffset + k]) / d;
					al[k] = d / (d + h);
					al[r + k] = 1.0 - al[k];
					al[s + k] = 6.0 * (e - f) / (h + d);
				}

				for (k = 1; k < n1; k++)
				{
					p = 1.0 / (al[r + k] * al[k - 1] + 2.0);
					al[k] *= -p;
					al[s + k] = (al[s + k] - al[r + k] * al[s + k - 1]) * p;
				}

				m_am[n1] = 0.0;
				al[n1 - 1] = al[s + n1 - 1];
				m_am[n1 - 1] = al[n1 - 1];

				for (k = n1 - 2, i = 0; i < m_num - 2; i++, k--)
				{
					al[k] = al[k] * al[k + 1] + al[s + k];
					m_am[k] = al[k];
				}
			}
			m_last_idx = -1;
		}

		//------------------------------------------------------------------------
		public void init(int num, double[] x, double[] y)
		{
			if (num > 2)
			{
				init(num);
				int i;
				for (i = 0; i < num; i++)
				{
					add_point(x[i], y[i]);
				}
				prepare();
			}
			m_last_idx = -1;
		}

		//------------------------------------------------------------------------
		private void bsearch(int n, int xOffset, double x0, out int i)
		{
			int j = n - 1;
			int k;

			for (i = 0; (j - i) > 1; )
			{
				k = (i + j) >> 1;
				if (x0 < m_am[xOffset + k]) j = k;
				else i = k;
			}
		}

		//------------------------------------------------------------------------
		private double interpolation(double x, int i)
		{
			int j = i + 1;
			double d = m_am[m_xOffset + i] - m_am[m_xOffset + j];
			double h = x - m_am[m_xOffset + j];
			double r = m_am[m_xOffset + i] - x;
			double p = d * d / 6.0;
			return (m_am[j] * r * r * r + m_am[i] * h * h * h) / 6.0 / d +
				   ((m_am[m_yOffset + j] - m_am[j] * p) * r + (m_am[m_yOffset + i] - m_am[i] * p) * h) / d;
		}

		//------------------------------------------------------------------------
		private double extrapolation_left(double x)
		{
			double d = m_am[m_xOffset + 1] - m_am[m_xOffset + 0];
			return (-d * m_am[1] / 6 + (m_am[m_yOffset + 1] - m_am[m_yOffset + 0]) / d) *
				   (x - m_am[m_xOffset + 0]) +
				   m_am[m_yOffset + 0];
		}

		//------------------------------------------------------------------------
		private double extrapolation_right(double x)
		{
			double d = m_am[m_xOffset + m_num - 1] - m_am[m_xOffset + m_num - 2];
			return (d * m_am[m_num - 2] / 6 + (m_am[m_yOffset + m_num - 1] - m_am[m_yOffset + m_num - 2]) / d) *
				   (x - m_am[m_xOffset + m_num - 1]) +
				   m_am[m_yOffset + m_num - 1];
		}

		//------------------------------------------------------------------------
		public double get(double x)
		{
			if (m_num > 2)
			{
				int i;

				// Extrapolation on the left
				if (x < m_am[m_xOffset + 0]) return extrapolation_left(x);

				// Extrapolation on the right
				if (x >= m_am[m_xOffset + m_num - 1]) return extrapolation_right(x);

				// Interpolation
				bsearch(m_num, m_xOffset, x, out i);
				return interpolation(x, i);
			}
			return 0.0;
		}

		//------------------------------------------------------------------------
		public double get_stateful(double x)
		{
			if (m_num > 2)
			{
				// Extrapolation on the left
				if (x < m_am[m_xOffset + 0]) return extrapolation_left(x);

				// Extrapolation on the right
				if (x >= m_am[m_xOffset + m_num - 1]) return extrapolation_right(x);

				if (m_last_idx >= 0)
				{
					// Check if x is not in current range
					if (x < m_am[m_xOffset + m_last_idx] || x > m_am[m_xOffset + m_last_idx + 1])
					{
						// Check if x between next points (most probably)
						if (m_last_idx < m_num - 2 &&
						   x >= m_am[m_xOffset + m_last_idx + 1] &&
						   x <= m_am[m_xOffset + m_last_idx + 2])
						{
							++m_last_idx;
						}
						else
							if (m_last_idx > 0 &&
							   x >= m_am[m_xOffset + m_last_idx - 1] &&
							   x <= m_am[m_xOffset + m_last_idx])
							{
								// x is between previous points
								--m_last_idx;
							}
							else
							{
								// Else perform full search
								bsearch(m_num, m_xOffset, x, out m_last_idx);
							}
					}
					return interpolation(x, m_last_idx);
				}
				else
				{
					// Interpolation
					bsearch(m_num, m_xOffset, x, out m_last_idx);
					return interpolation(x, m_last_idx);
				}
			}
			return 0.0;
		}
	}
}