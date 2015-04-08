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

namespace MatterHackers.Agg
{
	public interface IGradient
	{
		int calculate(int x, int y, int d);
	};

	public interface IColorFunction
	{
		int size();

		RGBA_Bytes this[int v]
		{
			get;
		}
	};

	//==========================================================span_gradient
	public class span_gradient : ISpanGenerator
	{
		public const int gradient_subpixel_shift = 4;                              //-----gradient_subpixel_shift
		public const int gradient_subpixel_scale = 1 << gradient_subpixel_shift;   //-----gradient_subpixel_scale
		public const int gradient_subpixel_mask = gradient_subpixel_scale - 1;    //-----gradient_subpixel_mask

		public const int subpixelShift = 8;

		public const int downscale_shift = subpixelShift - gradient_subpixel_shift;

		private ISpanInterpolator m_interpolator;
		private IGradient m_gradient_function;
		private IColorFunction m_color_function;
		private int m_d1;
		private int m_d2;

		//--------------------------------------------------------------------
		public span_gradient()
		{
		}

		//--------------------------------------------------------------------
		public span_gradient(ISpanInterpolator inter,
					  IGradient gradient_function,
					  IColorFunction color_function,
					  double d1, double d2)
		{
			m_interpolator = inter;
			m_gradient_function = gradient_function;
			m_color_function = color_function;
			m_d1 = (agg_basics.iround(d1 * gradient_subpixel_scale));
			m_d2 = (agg_basics.iround(d2 * gradient_subpixel_scale));
		}

		//--------------------------------------------------------------------
		public ISpanInterpolator interpolator()
		{
			return m_interpolator;
		}

		public IGradient gradient_function()
		{
			return m_gradient_function;
		}

		public IColorFunction color_function()
		{
			return m_color_function;
		}

		public double d1()
		{
			return (double)(m_d1) / gradient_subpixel_scale;
		}

		public double d2()
		{
			return (double)(m_d2) / gradient_subpixel_scale;
		}

		//--------------------------------------------------------------------
		public void interpolator(ISpanInterpolator i)
		{
			m_interpolator = i;
		}

		public void gradient_function(IGradient gf)
		{
			m_gradient_function = gf;
		}

		public void color_function(IColorFunction cf)
		{
			m_color_function = cf;
		}

		public void d1(double v)
		{
			m_d1 = agg_basics.iround(v * gradient_subpixel_scale);
		}

		public void d2(double v)
		{
			m_d2 = agg_basics.iround(v * gradient_subpixel_scale);
		}

		//--------------------------------------------------------------------
		public void prepare()
		{
		}

		//--------------------------------------------------------------------
		public void generate(RGBA_Bytes[] span, int spanIndex, int x, int y, int len)
		{
			int dd = m_d2 - m_d1;
			if (dd < 1) dd = 1;
			m_interpolator.begin(x + 0.5, y + 0.5, len);
			do
			{
				m_interpolator.coordinates(out x, out y);
				int d = m_gradient_function.calculate(x >> downscale_shift,
													   y >> downscale_shift, m_d2);
				d = ((d - m_d1) * (int)m_color_function.size()) / dd;
				if (d < 0) d = 0;
				if (d >= (int)m_color_function.size())
				{
					d = m_color_function.size() - 1;
				}

				span[spanIndex++] = m_color_function[d];
				m_interpolator.Next();
			}
			while (--len != 0);
		}
	};

	//=====================================================gradient_linear_color
	public struct gradient_linear_color : IColorFunction
	{
		private RGBA_Bytes m_c1;
		private RGBA_Bytes m_c2;
		private int m_size;

		public gradient_linear_color(RGBA_Bytes c1, RGBA_Bytes c2)
			: this(c1, c2, 256)
		{
		}

		public gradient_linear_color(RGBA_Bytes c1, RGBA_Bytes c2, int size)
		{
			m_c1 = c1;
			m_c2 = c2;
			m_size = size;
		}

		public int size()
		{
			return m_size;
		}

		public RGBA_Bytes this[int v]
		{
			get
			{
				return m_c1.gradient(m_c2, (double)(v) / (double)(m_size - 1));
			}
		}

		public void colors(RGBA_Bytes c1, RGBA_Bytes c2)
		{
			colors(c1, c2, 256);
		}

		public void colors(RGBA_Bytes c1, RGBA_Bytes c2, int size)
		{
			m_c1 = c1;
			m_c2 = c2;
			m_size = size;
		}
	};

	//==========================================================gradient_circle
	public class gradient_circle : IGradient
	{
		// Actually the same as radial. Just for compatibility
		public int calculate(int x, int y, int d)
		{
			return (int)(agg_math.fast_sqrt((int)(x * x + y * y)));
		}
	};

	//==========================================================gradient_radial
	public class gradient_radial : IGradient
	{
		public int calculate(int x, int y, int d)
		{
			return (int)(System.Math.Sqrt(x * x + y * y));
			//return (int)(agg_math.fast_sqrt((int)(x * x + y * y)));
		}
	}

	//========================================================gradient_radial_d
	public class gradient_radial_d : IGradient
	{
		public int calculate(int x, int y, int d)
		{
			return (int)agg_basics.uround(System.Math.Sqrt((double)(x) * (double)(x) + (double)(y) * (double)(y)));
		}
	};

	//====================================================gradient_radial_focus
	public class gradient_radial_focus : IGradient
	{
		private int m_r;
		private int m_fx;
		private int m_fy;
		private double m_r2;
		private double m_fx2;
		private double m_fy2;
		private double m_mul;

		//---------------------------------------------------------------------
		public gradient_radial_focus()
		{
			m_r = (100 * span_gradient.gradient_subpixel_scale);
			m_fx = (0);
			m_fy = (0);
			update_values();
		}

		//---------------------------------------------------------------------
		public gradient_radial_focus(double r, double fx, double fy)
		{
			m_r = (agg_basics.iround(r * span_gradient.gradient_subpixel_scale));
			m_fx = (agg_basics.iround(fx * span_gradient.gradient_subpixel_scale));
			m_fy = (agg_basics.iround(fy * span_gradient.gradient_subpixel_scale));
			update_values();
		}

		//---------------------------------------------------------------------
		public void init(double r, double fx, double fy)
		{
			m_r = agg_basics.iround(r * span_gradient.gradient_subpixel_scale);
			m_fx = agg_basics.iround(fx * span_gradient.gradient_subpixel_scale);
			m_fy = agg_basics.iround(fy * span_gradient.gradient_subpixel_scale);
			update_values();
		}

		//---------------------------------------------------------------------
		public double radius()
		{
			return (double)(m_r) / span_gradient.gradient_subpixel_scale;
		}

		public double focus_x()
		{
			return (double)(m_fx) / span_gradient.gradient_subpixel_scale;
		}

		public double focus_y()
		{
			return (double)(m_fy) / span_gradient.gradient_subpixel_scale;
		}

		//---------------------------------------------------------------------
		public int calculate(int x, int y, int d)
		{
			double dx = x - m_fx;
			double dy = y - m_fy;
			double d2 = dx * m_fy - dy * m_fx;
			double d3 = m_r2 * (dx * dx + dy * dy) - d2 * d2;
			return agg_basics.iround((dx * m_fx + dy * m_fy + System.Math.Sqrt(System.Math.Abs(d3))) * m_mul);
		}

		//---------------------------------------------------------------------
		private void update_values()
		{
			// Calculate the invariant values. In case the focal center
			// lies exactly on the gradient circle the divisor degenerates
			// into zero. In this case we just move the focal center by
			// one subpixel unit possibly in the direction to the origin (0,0)
			// and calculate the values again.
			//-------------------------
			m_r2 = (double)(m_r) * (double)(m_r);
			m_fx2 = (double)(m_fx) * (double)(m_fx);
			m_fy2 = (double)(m_fy) * (double)(m_fy);
			double d = (m_r2 - (m_fx2 + m_fy2));
			if (d == 0)
			{
				if (m_fx != 0)
				{
					if (m_fx < 0) ++m_fx; else --m_fx;
				}

				if (m_fy != 0)
				{
					if (m_fy < 0) ++m_fy; else --m_fy;
				}

				m_fx2 = (double)(m_fx) * (double)(m_fx);
				m_fy2 = (double)(m_fy) * (double)(m_fy);
				d = (m_r2 - (m_fx2 + m_fy2));
			}
			m_mul = m_r / d;
		}
	};

	//==============================================================gradient_x
	public class gradient_x : IGradient
	{
		public int calculate(int x, int y, int d)
		{
			return x;
		}
	};

	//==============================================================gradient_y
	public class gradient_y : IGradient
	{
		public int calculate(int x, int y, int d)
		{
			return y;
		}
	};

	//========================================================gradient_diamond
	public class gradient_diamond : IGradient
	{
		public int calculate(int x, int y, int d)
		{
			int ax = System.Math.Abs(x);
			int ay = System.Math.Abs(y);
			return ax > ay ? ax : ay;
		}
	};

	//=============================================================gradient_xy
	public class gradient_xy : IGradient
	{
		public int calculate(int x, int y, int d)
		{
			return System.Math.Abs(x) * System.Math.Abs(y) / d;
		}
	};

	//========================================================gradient_sqrt_xy
	public class gradient_sqrt_xy : IGradient
	{
		public int calculate(int x, int y, int d)
		{
			//return (int)System.Math.Sqrt((int)(System.Math.Abs(x) * System.Math.Abs(y)));
			return (int)agg_math.fast_sqrt((int)(System.Math.Abs(x) * System.Math.Abs(y)));
		}
	};

	//==========================================================gradient_conic
	public class gradient_conic : IGradient
	{
		public int calculate(int x, int y, int d)
		{
			return (int)agg_basics.uround(System.Math.Abs(System.Math.Atan2((double)(y), (double)(x))) * (double)(d) / System.Math.PI);
		}
	};

	//=================================================gradient_repeat_adaptor
	public class gradient_repeat_adaptor : IGradient
	{
		private IGradient m_gradient;

		public gradient_repeat_adaptor(IGradient gradient)
		{
			m_gradient = gradient;
		}

		public int calculate(int x, int y, int d)
		{
			int ret = m_gradient.calculate(x, y, d) % d;
			if (ret < 0) ret += d;
			return ret;
		}
	};

	//================================================gradient_reflect_adaptor
	public class gradient_reflect_adaptor : IGradient
	{
		private IGradient m_gradient;

		public gradient_reflect_adaptor(IGradient gradient)
		{
			m_gradient = gradient;
		}

		public int calculate(int x, int y, int d)
		{
			int d2 = d << 1;
			int ret = m_gradient.calculate(x, y, d) % d2;
			if (ret < 0) ret += d2;
			if (ret >= d) ret = d2 - ret;
			return ret;
		}
	};

	public class gradient_clamp_adaptor : IGradient
	{
		private IGradient m_gradient;

		public gradient_clamp_adaptor(IGradient gradient)
		{
			m_gradient = gradient;
		}

		public int calculate(int x, int y, int d)
		{
			int ret = m_gradient.calculate(x, y, d);
			if (ret < 0) ret = 0;
			if (ret > d) ret = d;
			return ret;
		}
	};
}