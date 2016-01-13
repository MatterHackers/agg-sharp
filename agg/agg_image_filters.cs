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
// Image transformation filters,
// Filtering classes (ImageFilterLookUpTable, image_filter),
// Basic filter shape classes
//----------------------------------------------------------------------------
using System;

namespace MatterHackers.Agg
{
	public interface IImageFilterFunction
	{
		double radius();

		double calc_weight(double x);
	}

	//-----------------------------------------------------ImageFilterLookUpTable
	public class ImageFilterLookUpTable
	{
		private double m_radius;
		private int m_diameter;
		private int m_start;
		private ArrayPOD<int> m_weight_array;

		public enum image_filter_scale_e
		{
			image_filter_shift = 14,                      //----image_filter_shift
			image_filter_scale = 1 << image_filter_shift, //----image_filter_scale
			image_filter_mask = image_filter_scale - 1   //----image_filter_mask
		}

		public enum image_subpixel_scale_e
		{
			image_subpixel_shift = 8,                         //----image_subpixel_shift
			image_subpixel_scale = 1 << image_subpixel_shift, //----image_subpixel_scale
			image_subpixel_mask = image_subpixel_scale - 1   //----image_subpixel_mask
		}

		public void calculate(IImageFilterFunction filter)
		{
			calculate(filter, true);
		}

		public void calculate(IImageFilterFunction filter, bool normalization)
		{
			double r = filter.radius();
			realloc_lut(r);
			int i;
			int pivot = diameter() << ((int)image_subpixel_scale_e.image_subpixel_shift - 1);
			for (i = 0; i < pivot; i++)
			{
				double x = (double)i / (double)image_subpixel_scale_e.image_subpixel_scale;
				double y = filter.calc_weight(x);
				m_weight_array.Array[pivot + i] =
				m_weight_array.Array[pivot - i] = agg_basics.iround(y * (int)image_filter_scale_e.image_filter_scale);
			}
			int end = (diameter() << (int)image_subpixel_scale_e.image_subpixel_shift) - 1;
			m_weight_array.Array[0] = m_weight_array.Array[end];
			if (normalization)
			{
				normalize();
			}
		}

		public ImageFilterLookUpTable()
		{
			m_weight_array = new ArrayPOD<int>(256);
			m_radius = (0);
			m_diameter = (0);
			m_start = (0);
		}

		public ImageFilterLookUpTable(IImageFilterFunction filter)
			: this(filter, true)
		{
		}

		public ImageFilterLookUpTable(IImageFilterFunction filter, bool normalization)
		{
			m_weight_array = new ArrayPOD<int>(256);
			calculate(filter, normalization);
		}

		public double radius()
		{
			return m_radius;
		}

		public int diameter()
		{
			return m_diameter;
		}

		public int start()
		{
			return m_start;
		}

		public int[] weight_array()
		{
			return m_weight_array.Array;
		}

		//--------------------------------------------------------------------
		// This function normalizes integer values and corrects the rounding
		// errors. It doesn't do anything with the source floating point values
		// (m_weight_array_dbl), it corrects only integers according to the rule
		// of 1.0 which means that any sum of pixel weights must be equal to 1.0.
		// So, the filter function must produce a graph of the proper shape.
		//--------------------------------------------------------------------
		public void normalize()
		{
			int i;
			int flip = 1;

			for (i = 0; i < (int)image_subpixel_scale_e.image_subpixel_scale; i++)
			{
				for (; ; )
				{
					int sum = 0;
					int j;
					for (j = 0; j < m_diameter; j++)
					{
						sum += m_weight_array.Array[j * (int)image_subpixel_scale_e.image_subpixel_scale + i];
					}

					if (sum == (int)image_filter_scale_e.image_filter_scale) break;

					double k = (double)((int)image_filter_scale_e.image_filter_scale) / (double)(sum);
					sum = 0;
					for (j = 0; j < m_diameter; j++)
					{
						sum += m_weight_array.Array[j * (int)image_subpixel_scale_e.image_subpixel_scale + i] =
							(int)agg_basics.iround(m_weight_array.Array[j * (int)image_subpixel_scale_e.image_subpixel_scale + i] * k);
					}

					sum -= (int)image_filter_scale_e.image_filter_scale;
					int inc = (sum > 0) ? -1 : 1;

					for (j = 0; j < m_diameter && sum != 0; j++)
					{
						flip ^= 1;
						int idx = flip != 0 ? m_diameter / 2 + j / 2 : m_diameter / 2 - j / 2;
						int v = m_weight_array.Array[idx * (int)image_subpixel_scale_e.image_subpixel_scale + i];
						if (v < (int)image_filter_scale_e.image_filter_scale)
						{
							m_weight_array.Array[idx * (int)image_subpixel_scale_e.image_subpixel_scale + i] += (int)inc;
							sum += inc;
						}
					}
				}
			}

			int pivot = m_diameter << ((int)image_subpixel_scale_e.image_subpixel_shift - 1);

			for (i = 0; i < pivot; i++)
			{
				m_weight_array.Array[pivot + i] = m_weight_array.Array[pivot - i];
			}
			int end = (diameter() << (int)image_subpixel_scale_e.image_subpixel_shift) - 1;
			m_weight_array.Array[0] = m_weight_array.Array[end];
		}

		private void realloc_lut(double radius)
		{
			m_radius = radius;
			m_diameter = agg_basics.uceil(radius) * 2;
			m_start = -(int)(m_diameter / 2 - 1);
			int size = (int)m_diameter << (int)image_subpixel_scale_e.image_subpixel_shift;
			if (size > m_weight_array.Size())
			{
				m_weight_array.Resize(size);
			}
		}
	}

	/*

	//--------------------------------------------------------image_filter
	public class image_filter : ImageFilterLookUpTable
	{
		public image_filter()
		{
			calculate(m_filter_function);
		}

		private IImageFilter m_filter_function;
	};
	 */

	//-----------------------------------------------image_filter_bilinear
	public struct image_filter_bilinear : IImageFilterFunction
	{
		public double radius()
		{
			return 1.0;
		}

		public double calc_weight(double x)
		{
			if (Math.Abs(x) < 1)
			{
				if (x < 0)
				{
					return 1.0 + x;
				}
				else
				{
					return 1.0 - x;
				}
			}

			return 0;
		}
	};

	//-----------------------------------------------image_filter_hanning
	public struct image_filter_hanning : IImageFilterFunction
	{
		public double radius()
		{
			return 1.0;
		}

		public double calc_weight(double x)
		{
			return 0.5 + 0.5 * Math.Cos(Math.PI * x);
		}
	};

	//-----------------------------------------------image_filter_hamming
	public struct image_filter_hamming : IImageFilterFunction
	{
		public double radius()
		{
			return 1.0;
		}

		public double calc_weight(double x)
		{
			return 0.54 + 0.46 * Math.Cos(Math.PI * x);
		}
	};

	//-----------------------------------------------image_filter_hermite
	public struct image_filter_hermite : IImageFilterFunction
	{
		public double radius()
		{
			return 1.0;
		}

		public double calc_weight(double x)
		{
			return (2.0 * x - 3.0) * x * x + 1.0;
		}
	};

	//------------------------------------------------image_filter_quadric
	public struct image_filter_quadric : IImageFilterFunction
	{
		public double radius()
		{
			return 1.5;
		}

		public double calc_weight(double x)
		{
			double t;
			if (x < 0.5) return 0.75 - x * x;
			if (x < 1.5) { t = x - 1.5; return 0.5 * t * t; }
			return 0.0;
		}
	};

	//------------------------------------------------image_filter_bicubic
	public class image_filter_bicubic : IImageFilterFunction
	{
		private static double pow3(double x)
		{
			return (x <= 0.0) ? 0.0 : x * x * x;
		}

		public double radius()
		{
			return 2.0;
		}

		public double calc_weight(double x)
		{
			return
				(1.0 / 6.0) *
				(pow3(x + 2) - 4 * pow3(x + 1) + 6 * pow3(x) - 4 * pow3(x - 1));
		}
	};

	//-------------------------------------------------image_filter_kaiser
	public class image_filter_kaiser : IImageFilterFunction
	{
		private double a;
		private double i0a;
		private double epsilon;

		public image_filter_kaiser()
			: this(6.33)
		{
		}

		public image_filter_kaiser(double b)
		{
			a = (b);
			epsilon = (1e-12);
			i0a = 1.0 / bessel_i0(b);
		}

		public double radius()
		{
			return 1.0;
		}

		public double calc_weight(double x)
		{
			return bessel_i0(a * Math.Sqrt(1.0 - x * x)) * i0a;
		}

		private double bessel_i0(double x)
		{
			int i;
			double sum, y, t;

			sum = 1.0;
			y = x * x / 4.0;
			t = y;

			for (i = 2; t > epsilon; i++)
			{
				sum += t;
				t *= (double)y / (i * i);
			}
			return sum;
		}
	};

	//----------------------------------------------image_filter_catrom
	public struct image_filter_catrom : IImageFilterFunction
	{
		public double radius()
		{
			return 2.0;
		}

		public double calc_weight(double x)
		{
			if (x < 1.0) return 0.5 * (2.0 + x * x * (-5.0 + x * 3.0));
			if (x < 2.0) return 0.5 * (4.0 + x * (-8.0 + x * (5.0 - x)));
			return 0.0;
		}
	};

	//---------------------------------------------image_filter_mitchell
	public class image_filter_mitchell : IImageFilterFunction
	{
		private double p0, p2, p3;
		private double q0, q1, q2, q3;

		public image_filter_mitchell()
			: this(1.0 / 3.0, 1.0 / 3.0)
		{
		}

		public image_filter_mitchell(double b, double c)
		{
			p0 = ((6.0 - 2.0 * b) / 6.0);
			p2 = ((-18.0 + 12.0 * b + 6.0 * c) / 6.0);
			p3 = ((12.0 - 9.0 * b - 6.0 * c) / 6.0);
			q0 = ((8.0 * b + 24.0 * c) / 6.0);
			q1 = ((-12.0 * b - 48.0 * c) / 6.0);
			q2 = ((6.0 * b + 30.0 * c) / 6.0);
			q3 = ((-b - 6.0 * c) / 6.0);
		}

		public double radius()
		{
			return 2.0;
		}

		public double calc_weight(double x)
		{
			if (x < 1.0) return p0 + x * x * (p2 + x * p3);
			if (x < 2.0) return q0 + x * (q1 + x * (q2 + x * q3));
			return 0.0;
		}
	};

	//----------------------------------------------image_filter_spline16
	public struct image_filter_spline16 : IImageFilterFunction
	{
		public double radius()
		{
			return 2.0;
		}

		public double calc_weight(double x)
		{
			if (x < 1.0)
			{
				return ((x - 9.0 / 5.0) * x - 1.0 / 5.0) * x + 1.0;
			}
			return ((-1.0 / 3.0 * (x - 1) + 4.0 / 5.0) * (x - 1) - 7.0 / 15.0) * (x - 1);
		}
	};

	//---------------------------------------------image_filter_spline36
	public struct image_filter_spline36 : IImageFilterFunction
	{
		public double radius()
		{
			return 3.0;
		}

		public double calc_weight(double x)
		{
			if (x < 1.0)
			{
				return ((13.0 / 11.0 * x - 453.0 / 209.0) * x - 3.0 / 209.0) * x + 1.0;
			}
			if (x < 2.0)
			{
				return ((-6.0 / 11.0 * (x - 1) + 270.0 / 209.0) * (x - 1) - 156.0 / 209.0) * (x - 1);
			}
			return ((1.0 / 11.0 * (x - 2) - 45.0 / 209.0) * (x - 2) + 26.0 / 209.0) * (x - 2);
		}
	};

	//----------------------------------------------image_filter_gaussian
	public struct image_filter_gaussian : IImageFilterFunction
	{
		public double radius()
		{
			return 2.0;
		}

		public double calc_weight(double x)
		{
			return Math.Exp(-2.0 * x * x) * Math.Sqrt(2.0 / Math.PI);
		}
	};

	//------------------------------------------------image_filter_bessel
	public struct image_filter_bessel : IImageFilterFunction
	{
		public double radius()
		{
			return 3.2383;
		}

		public double calc_weight(double x)
		{
			return (x == 0.0) ? Math.PI / 4.0 : agg_math.besj(Math.PI * x, 1) / (2.0 * x);
		}
	};

	//-------------------------------------------------image_filter_sinc
	public class image_filter_sinc : IImageFilterFunction
	{
		public image_filter_sinc(double r)
		{
			m_radius = (r < 2.0 ? 2.0 : r);
		}

		public double radius()
		{
			return m_radius;
		}

		public double calc_weight(double x)
		{
			if (x == 0.0) return 1.0;
			x *= Math.PI;
			return Math.Sin(x) / x;
		}

		private double m_radius;
	};

	//-----------------------------------------------image_filter_lanczos
	public class image_filter_lanczos : IImageFilterFunction
	{
		public image_filter_lanczos(double r)
		{
			m_radius = (r < 2.0 ? 2.0 : r);
		}

		public double radius()
		{
			return m_radius;
		}

		public double calc_weight(double x)
		{
			if (x == 0.0) return 1.0;
			if (x > m_radius) return 0.0;
			x *= Math.PI;
			double xr = x / m_radius;
			return (Math.Sin(x) / x) * (Math.Sin(xr) / xr);
		}

		private double m_radius;
	};

	//----------------------------------------------image_filter_blackman
	public class image_filter_blackman : IImageFilterFunction
	{
		public image_filter_blackman(double r)
		{
			m_radius = (r < 2.0 ? 2.0 : r);
		}

		public double radius()
		{
			return m_radius;
		}

		public double calc_weight(double x)
		{
			if (x == 0.0)
			{
				return 1.0;
			}

			if (x > m_radius)
			{
				return 0.0;
			}

			x *= Math.PI;
			double xr = x / m_radius;
			return (Math.Sin(x) / x) * (0.42 + 0.5 * Math.Cos(xr) + 0.08 * Math.Cos(2 * xr));
		}

		private double m_radius;
	};
}