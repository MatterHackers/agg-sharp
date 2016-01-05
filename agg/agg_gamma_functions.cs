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
	public interface IGammaFunction
	{
		double GetGamma(double x);
	}

	public struct gamma_none : IGammaFunction
	{
		public double GetGamma(double x)
		{
			return x;
		}
	}

	//==============================================================gamma_power
	public class gamma_power : IGammaFunction
	{
		public gamma_power()
		{
			m_gamma = 1.0;
		}

		public gamma_power(double g)
		{
			m_gamma = g;
		}

		public void gamma(double g)
		{
			m_gamma = g;
		}

		public double gamma()
		{
			return m_gamma;
		}

		public double GetGamma(double x)
		{
			return Math.Pow(x, m_gamma);
		}

		private double m_gamma;
	}

	//==========================================================gamma_threshold
	public class gamma_threshold : IGammaFunction
	{
		public gamma_threshold()
		{
			m_threshold = 0.5;
		}

		public gamma_threshold(double t)
		{
			m_threshold = t;
		}

		public void threshold(double t)
		{
			m_threshold = t;
		}

		public double threshold()
		{
			return m_threshold;
		}

		public double GetGamma(double x)
		{
			return (x < m_threshold) ? 0.0 : 1.0;
		}

		private double m_threshold;
	}

	//============================================================gamma_linear
	public class gamma_linear : IGammaFunction
	{
		public gamma_linear()
		{
			m_start = (0.0);
			m_end = (1.0);
		}

		public gamma_linear(double s, double e)
		{
			m_start = (s);
			m_end = (e);
		}

		public void set(double s, double e)
		{
			m_start = s; m_end = e;
		}

		public void start(double s)
		{
			m_start = s;
		}

		public void end(double e)
		{
			m_end = e;
		}

		public double start()
		{
			return m_start;
		}

		public double end()
		{
			return m_end;
		}

		public double GetGamma(double x)
		{
			if (x < m_start) return 0.0;
			if (x > m_end) return 1.0;
			double EndMinusStart = m_end - m_start;
			if (EndMinusStart != 0)
				return (x - m_start) / EndMinusStart;
			else
				return 0.0;
		}

		private double m_start;
		private double m_end;
	}

	//==========================================================gamma_multiply
	public class gamma_multiply : IGammaFunction
	{
		public gamma_multiply()
		{
			m_mul = (1.0);
		}

		public gamma_multiply(double v)
		{
			m_mul = (v);
		}

		public void value(double v)
		{
			m_mul = v;
		}

		public double value()
		{
			return m_mul;
		}

		public double GetGamma(double x)
		{
			double y = x * m_mul;
			if (y > 1.0) y = 1.0;
			return y;
		}

		private double m_mul;
	}
}