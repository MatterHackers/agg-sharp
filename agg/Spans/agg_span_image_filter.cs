//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007, 2026 Lars Brubaker
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
// Image transformations with filtering. Span generator base class
//
//----------------------------------------------------------------------------
using image_subpixel_scale_e = MatterHackers.Agg.ImageFilterLookUpTable.image_subpixel_scale_e;

namespace MatterHackers.Agg
{
	public interface ISpanGenerator
	{
		void prepare();

		void generate(Color[] span, int spanIndex, int x, int y, int len);
	};

	public abstract class span_image_filter : ISpanGenerator
	{
		private IImageBufferAccessor imageBufferAccessor;
		protected ISpanInterpolator m_interpolator;
		protected ImageFilterLookUpTable m_filter;
		private double m_dx_dbl;
		private double m_dy_dbl;
		private int m_dx_int;
		private int m_dy_int;

		public span_image_filter()
		{
		}

		public span_image_filter(IImageBufferAccessor src,
			ISpanInterpolator interpolator)
			: this(src, interpolator, null)
		{
		}

		public span_image_filter(IImageBufferAccessor src,
			ISpanInterpolator interpolator, ImageFilterLookUpTable filter)
		{
			imageBufferAccessor = src;
			m_interpolator = interpolator;
			m_filter = (filter);
			m_dx_dbl = (0.5);
			m_dy_dbl = (0.5);
			m_dx_int = ((int)image_subpixel_scale_e.image_subpixel_scale / 2);
			m_dy_int = ((int)image_subpixel_scale_e.image_subpixel_scale / 2);
		}

		public void attach(IImageBufferAccessor v)
		{
			imageBufferAccessor = v;
		}

		public abstract void generate(Color[] span, int spanIndex, int x, int y, int len);

		public IImageBufferAccessor GetImageBufferAccessor()
		{
			return imageBufferAccessor;
		}

		public ImageFilterLookUpTable filter()
		{
			return m_filter;
		}

		public int filter_dx_int()
		{
			return (int)m_dx_int;
		}

		public int filter_dy_int()
		{
			return (int)m_dy_int;
		}

		public double filter_dx_dbl()
		{
			return m_dx_dbl;
		}

		public double filter_dy_dbl()
		{
			return m_dy_dbl;
		}

		public void interpolator(ISpanInterpolator v)
		{
			m_interpolator = v;
		}

		public void filter(ImageFilterLookUpTable v)
		{
			m_filter = v;
		}

		public void filter_offset(double dx, double dy)
		{
			m_dx_dbl = dx;
			m_dy_dbl = dy;
			m_dx_int = (int)Util.iround(dx * (int)image_subpixel_scale_e.image_subpixel_scale);
			m_dy_int = (int)Util.iround(dy * (int)image_subpixel_scale_e.image_subpixel_scale);
		}

		public void filter_offset(double d)
		{
			filter_offset(d, d);
		}

		public ISpanInterpolator interpolator()
		{
			return m_interpolator;
		}

		public virtual void prepare()
		{
		}
	}

	public interface ISpanGeneratorFloat
	{
		void prepare();

		void generate(ColorF[] span, int spanIndex, int x, int y, int len);
	};

	public abstract class span_image_filter_float : ISpanGeneratorFloat
	{
		private IImageBufferAccessorFloat m_ImageBufferAccessor;
		protected ISpanInterpolatorFloat m_interpolator;
		protected IImageFilterFunction m_filterFunction;
		private float m_dx_dbl;
		private float m_dy_dbl;

		public span_image_filter_float()
		{
		}

		public span_image_filter_float(IImageBufferAccessorFloat src,
			ISpanInterpolatorFloat interpolator)
			: this(src, interpolator, null)
		{
		}

		public span_image_filter_float(IImageBufferAccessorFloat src,
			ISpanInterpolatorFloat interpolator, IImageFilterFunction filterFunction)
		{
			m_ImageBufferAccessor = src;
			m_interpolator = interpolator;
			m_filterFunction = filterFunction;
			m_dx_dbl = (0.5f);
			m_dy_dbl = (0.5f);
		}

		public void attach(IImageBufferAccessorFloat v)
		{
			m_ImageBufferAccessor = v;
		}

		public abstract void generate(ColorF[] span, int spanIndex, int x, int y, int len);

		public IImageBufferAccessorFloat source()
		{
			return m_ImageBufferAccessor;
		}

		public IImageFilterFunction filterFunction()
		{
			return m_filterFunction;
		}

		public float filter_dx_dbl()
		{
			return m_dx_dbl;
		}

		public float filter_dy_dbl()
		{
			return m_dy_dbl;
		}

		public void interpolator(ISpanInterpolatorFloat v)
		{
			m_interpolator = v;
		}

		public void filterFunction(IImageFilterFunction v)
		{
			m_filterFunction = v;
		}

		public void filter_offset(float dx, float dy)
		{
			m_dx_dbl = dx;
			m_dy_dbl = dy;
		}

		public void filter_offset(float d)
		{
			filter_offset(d, d);
		}

		public ISpanInterpolatorFloat interpolator()
		{
			return m_interpolator;
		}

		public void prepare()
		{
		}
	}

	//==============================================span_image_resample_affine
	/// <summary>
	/// C++ span_image_resample_affine: the base of the resampling span generators over an affine transform. The
	/// filter's footprint is sized once per render, in <see cref="prepare"/>, from the transform's scale (so a
	/// shrunk image averages every source pixel it covers instead of skipping some), times the blur. The
	/// interpolator's transformer must be an <see cref="Transform.Affine"/>, as C++'s
	/// span_interpolator_linear&lt;trans_affine&gt; is.
	/// </summary>
	public abstract class span_image_resample_affine : span_image_filter
	{
		protected int m_rx;
		protected int m_ry;
		protected int m_rx_inv;
		protected int m_ry_inv;

		private double m_scale_limit;
		private double m_blur_x;
		private double m_blur_y;

		public span_image_resample_affine(IImageBufferAccessor src, ISpanInterpolator inter, ImageFilterLookUpTable filter)
			: base(src, inter, filter)
		{
			m_scale_limit = 200.0;
			m_blur_x = 1.0;
			m_blur_y = 1.0;
		}

		/// <summary>C++ <c>m_rx</c>: the filter's horizontal footprint, in subpixels per source pixel (256 at scale 1).</summary>
		public int ScaleX => m_rx;

		/// <summary>C++ <c>m_ry</c>: the filter's vertical footprint, in subpixels per source pixel.</summary>
		public int ScaleY => m_ry;

		public int scale_limit() => Util.uround(m_scale_limit);

		public void scale_limit(int v) => m_scale_limit = v;

		public double blur_x() => m_blur_x;

		public double blur_y() => m_blur_y;

		public void blur_x(double v) => m_blur_x = v;

		public void blur_y(double v) => m_blur_y = v;

		public void blur(double v) => m_blur_x = m_blur_y = v;

		public override void prepare()
		{
			if (!(interpolator().transformer() is Transform.Affine affine))
			{
				throw new System.InvalidOperationException("span_image_resample_affine needs an interpolator over an Affine transform.");
			}

			affine.scaling_abs(out double scale_x, out double scale_y);

			double scale_xy = scale_x * scale_y;
			if (scale_xy > m_scale_limit)
			{
				scale_x = scale_x * m_scale_limit / scale_xy;
				scale_y = scale_y * m_scale_limit / scale_xy;
			}

			if (scale_x < 1) scale_x = 1;
			if (scale_y < 1) scale_y = 1;

			if (scale_x > m_scale_limit) scale_x = m_scale_limit;
			if (scale_y > m_scale_limit) scale_y = m_scale_limit;

			scale_x *= m_blur_x;
			scale_y *= m_blur_y;

			if (scale_x < 1) scale_x = 1;
			if (scale_y < 1) scale_y = 1;

			const double subpixelScale = (int)image_subpixel_scale_e.image_subpixel_scale;
			m_rx = Util.uround(scale_x * subpixelScale);
			m_rx_inv = Util.uround(1.0 / scale_x * subpixelScale);

			m_ry = Util.uround(scale_y * subpixelScale);
			m_ry_inv = Util.uround(1.0 / scale_y * subpixelScale);
		}
	}

	//=====================================================span_image_resample
	public abstract class span_image_resample
		: span_image_filter
	{
		public span_image_resample(IImageBufferAccessor src,
							ISpanInterpolator inter,
							ImageFilterLookUpTable filter)
			: base(src, inter, filter)
		{
			m_scale_limit = (20);
			m_blur_x = ((int)image_subpixel_scale_e.image_subpixel_scale);
			m_blur_y = ((int)image_subpixel_scale_e.image_subpixel_scale);
		}

		//public abstract void prepare();
		//public abstract unsafe void generate(rgba8* span, int x, int y, int len);

		//--------------------------------------------------------------------
		public int scale_limit()
		{
			return m_scale_limit;
		}

		public void scale_limit(int v)
		{
			m_scale_limit = v;
		}

		//--------------------------------------------------------------------
		public double blur_x()
		{
			return (double)(m_blur_x) / (double)((int)image_subpixel_scale_e.image_subpixel_scale);
		}

		public double blur_y()
		{
			return (double)(m_blur_y) / (double)((int)image_subpixel_scale_e.image_subpixel_scale);
		}

		public void blur_x(double v)
		{
			m_blur_x = (int)Util.uround(v * (double)((int)image_subpixel_scale_e.image_subpixel_scale));
		}

		public void blur_y(double v)
		{
			m_blur_y = (int)Util.uround(v * (double)((int)image_subpixel_scale_e.image_subpixel_scale));
		}

		public void blur(double v)
		{
			m_blur_x = m_blur_y = (int)Util.uround(v * (double)((int)image_subpixel_scale_e.image_subpixel_scale));
		}

		protected void adjust_scale(ref int rx, ref int ry)
		{
			if (rx < (int)image_subpixel_scale_e.image_subpixel_scale) rx = (int)image_subpixel_scale_e.image_subpixel_scale;
			if (ry < (int)image_subpixel_scale_e.image_subpixel_scale) ry = (int)image_subpixel_scale_e.image_subpixel_scale;
			if (rx > (int)image_subpixel_scale_e.image_subpixel_scale * m_scale_limit)
			{
				rx = (int)image_subpixel_scale_e.image_subpixel_scale * m_scale_limit;
			}
			if (ry > (int)image_subpixel_scale_e.image_subpixel_scale * m_scale_limit)
			{
				ry = (int)image_subpixel_scale_e.image_subpixel_scale * m_scale_limit;
			}
			rx = (rx * m_blur_x) >> (int)image_subpixel_scale_e.image_subpixel_shift;
			ry = (ry * m_blur_y) >> (int)image_subpixel_scale_e.image_subpixel_shift;
			if (rx < (int)image_subpixel_scale_e.image_subpixel_scale) rx = (int)image_subpixel_scale_e.image_subpixel_scale;
			if (ry < (int)image_subpixel_scale_e.image_subpixel_scale) ry = (int)image_subpixel_scale_e.image_subpixel_scale;
		}

		private int m_scale_limit;
		private int m_blur_x;
		private int m_blur_y;
	}
}