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
	/*
	//===========================================span_interpolator_persp_exact
	//template<int SubpixelShift = 8>
	class span_interpolator_persp_exact
	{
	public:
		typedef trans_perspective trans_type;
		typedef trans_perspective::iterator_x iterator_type;
		enum subpixel_scale_e
		{
			subpixel_shift = SubpixelShift,
			subpixel_scale = 1 << subpixel_shift
		};

		//--------------------------------------------------------------------
		span_interpolator_persp_exact() {}

		//--------------------------------------------------------------------
		// Arbitrary quadrangle transformations
		span_interpolator_persp_exact(double[] src, double[] dst)
		{
			quad_to_quad(src, dst);
		}

		//--------------------------------------------------------------------
		// Direct transformations
		span_interpolator_persp_exact(double x1, double y1,
									  double x2, double y2,
									  double[] quad)
		{
			rect_to_quad(x1, y1, x2, y2, quad);
		}

		//--------------------------------------------------------------------
		// Reverse transformations
		span_interpolator_persp_exact(double[] quad,
									  double x1, double y1,
									  double x2, double y2)
		{
			quad_to_rect(quad, x1, y1, x2, y2);
		}

		//--------------------------------------------------------------------
		// Set the transformations using two arbitrary quadrangles.
		void quad_to_quad(double[] src, double[] dst)
		{
			m_trans_dir.quad_to_quad(src, dst);
			m_trans_inv.quad_to_quad(dst, src);
		}

		//--------------------------------------------------------------------
		// Set the direct transformations, i.e., rectangle -> quadrangle
		void rect_to_quad(double x1, double y1, double x2, double y2,
						  double[] quad)
		{
			double src[8];
			src[0] = src[6] = x1;
			src[2] = src[4] = x2;
			src[1] = src[3] = y1;
			src[5] = src[7] = y2;
			quad_to_quad(src, quad);
		}

		//--------------------------------------------------------------------
		// Set the reverse transformations, i.e., quadrangle -> rectangle
		void quad_to_rect(double[] quad,
						  double x1, double y1, double x2, double y2)
		{
			double dst[8];
			dst[0] = dst[6] = x1;
			dst[2] = dst[4] = x2;
			dst[1] = dst[3] = y1;
			dst[5] = dst[7] = y2;
			quad_to_quad(quad, dst);
		}

		//--------------------------------------------------------------------
		// Check if the equations were solved successfully
		bool is_valid() { return m_trans_dir.is_valid(); }

		//----------------------------------------------------------------
		void begin(double x, double y, int len)
		{
			m_iterator = m_trans_dir.begin(x, y, 1.0);
			double xt = m_iterator.x;
			double yt = m_iterator.y;

			double dx;
			double dy;
			double delta = 1/(double)subpixel_scale;
			dx = xt + delta;
			dy = yt;
			m_trans_inv.transform(&dx, &dy);
			dx -= x;
			dy -= y;
			int sx1 = agg_basics.uround(subpixel_scale/Math.Sqrt(dx*dx + dy*dy)) >> subpixel_shift;
			dx = xt;
			dy = yt + delta;
			m_trans_inv.transform(&dx, &dy);
			dx -= x;
			dy -= y;
			int sy1 = agg_basics.uround(subpixel_scale/Math.Sqrt(dx*dx + dy*dy)) >> subpixel_shift;

			x += len;
			xt = x;
			yt = y;
			m_trans_dir.transform(&xt, &yt);

			dx = xt + delta;
			dy = yt;
			m_trans_inv.transform(&dx, &dy);
			dx -= x;
			dy -= y;
			int sx2 = agg_basics.uround(subpixel_scale/Math.Sqrt(dx*dx + dy*dy)) >> subpixel_shift;
			dx = xt;
			dy = yt + delta;
			m_trans_inv.transform(&dx, &dy);
			dx -= x;
			dy -= y;
			int sy2 = agg_basics.uround(subpixel_scale/Math.Sqrt(dx*dx + dy*dy)) >> subpixel_shift;

			m_scale_x = dda2_line_interpolator(sx1, sx2, len);
			m_scale_y = dda2_line_interpolator(sy1, sy2, len);
		}

		//----------------------------------------------------------------
		void resynchronize(double xe, double ye, int len)
		{
			// Assume x1,y1 are equal to the ones at the previous end point
			int sx1 = m_scale_x.y();
			int sy1 = m_scale_y.y();

			// Calculate transformed coordinates at x2,y2
			double xt = xe;
			double yt = ye;
			m_trans_dir.transform(&xt, &yt);

			double delta = 1/(double)subpixel_scale;
			double dx;
			double dy;

			// Calculate scale by X at x2,y2
			dx = xt + delta;
			dy = yt;
			m_trans_inv.transform(&dx, &dy);
			dx -= xe;
			dy -= ye;
			int sx2 = agg_basics.uround(subpixel_scale/Math.Sqrt(dx*dx + dy*dy)) >> subpixel_shift;

			// Calculate scale by Y at x2,y2
			dx = xt;
			dy = yt + delta;
			m_trans_inv.transform(&dx, &dy);
			dx -= xe;
			dy -= ye;
			int sy2 = agg_basics.uround(subpixel_scale/Math.Sqrt(dx*dx + dy*dy)) >> subpixel_shift;

			// Initialize the interpolators
			m_scale_x = dda2_line_interpolator(sx1, sx2, len);
			m_scale_y = dda2_line_interpolator(sy1, sy2, len);
		}

		//----------------------------------------------------------------
		void operator++()
		{
			++m_iterator;
			++m_scale_x;
			++m_scale_y;
		}

		//----------------------------------------------------------------
		void coordinates(int* x, int* y)
		{
			*x = agg_basics.iround(m_iterator.x * subpixel_scale);
			*y = agg_basics.iround(m_iterator.y * subpixel_scale);
		}

		//----------------------------------------------------------------
		void local_scale(int* x, int* y)
		{
			*x = m_scale_x.y();
			*y = m_scale_y.y();
		}

		//----------------------------------------------------------------
		void transform(double[] x, double[] y)
		{
			m_trans_dir.transform(x, y);
		}

	private:
		trans_type             m_trans_dir;
		trans_type             m_trans_inv;
		iterator_type          m_iterator;
		dda2_line_interpolator m_scale_x;
		dda2_line_interpolator m_scale_y;
	};
	 */

	//============================================span_interpolator_persp_lerp
	//template<int SubpixelShift = 8>
	public class span_interpolator_persp_lerp : ISpanInterpolator
	{
		private Transform.Perspective m_trans_dir;
		private Transform.Perspective m_trans_inv;
		private dda2_line_interpolator m_coord_x;
		private dda2_line_interpolator m_coord_y;
		private dda2_line_interpolator m_scale_x;
		private dda2_line_interpolator m_scale_y;

		private const int subpixel_shift = 8;
		private const int subpixel_scale = 1 << subpixel_shift;

		//--------------------------------------------------------------------
		public span_interpolator_persp_lerp()
		{
			m_trans_dir = new Transform.Perspective();
			m_trans_inv = new Transform.Perspective();
		}

		//--------------------------------------------------------------------
		// Arbitrary quadrangle transformations
		public span_interpolator_persp_lerp(double[] src, double[] dst)
			: this()
		{
			quad_to_quad(src, dst);
		}

		//--------------------------------------------------------------------
		// Direct transformations
		public span_interpolator_persp_lerp(double x1, double y1,
									 double x2, double y2,
									 double[] quad)
			: this()
		{
			rect_to_quad(x1, y1, x2, y2, quad);
		}

		//--------------------------------------------------------------------
		// Reverse transformations
		public span_interpolator_persp_lerp(double[] quad,
									 double x1, double y1,
									 double x2, double y2)
			: this()
		{
			quad_to_rect(quad, x1, y1, x2, y2);
		}

		//--------------------------------------------------------------------
		// Set the transformations using two arbitrary quadrangles.
		public void quad_to_quad(double[] src, double[] dst)
		{
			m_trans_dir.quad_to_quad(src, dst);
			m_trans_inv.quad_to_quad(dst, src);
		}

		//--------------------------------------------------------------------
		// Set the direct transformations, i.e., rectangle -> quadrangle
		public void rect_to_quad(double x1, double y1, double x2, double y2, double[] quad)
		{
			double[] src = new double[8];
			src[0] = src[6] = x1;
			src[2] = src[4] = x2;
			src[1] = src[3] = y1;
			src[5] = src[7] = y2;
			quad_to_quad(src, quad);
		}

		//--------------------------------------------------------------------
		// Set the reverse transformations, i.e., quadrangle -> rectangle
		public void quad_to_rect(double[] quad,
						  double x1, double y1, double x2, double y2)
		{
			double[] dst = new double[8];
			dst[0] = dst[6] = x1;
			dst[2] = dst[4] = x2;
			dst[1] = dst[3] = y1;
			dst[5] = dst[7] = y2;
			quad_to_quad(quad, dst);
		}

		//--------------------------------------------------------------------
		// Check if the equations were solved successfully
		public bool is_valid()
		{
			return m_trans_dir.is_valid();
		}

		//----------------------------------------------------------------
		public void begin(double x, double y, int len)
		{
			// Calculate transformed coordinates at x1,y1
			double xt = x;
			double yt = y;
			m_trans_dir.transform(ref xt, ref yt);
			int x1 = agg_basics.iround(xt * subpixel_scale);
			int y1 = agg_basics.iround(yt * subpixel_scale);

			double dx;
			double dy;
			double delta = 1 / (double)subpixel_scale;

			// Calculate scale by X at x1,y1
			dx = xt + delta;
			dy = yt;
			m_trans_inv.transform(ref dx, ref dy);
			dx -= x;
			dy -= y;
			int sx1 = (int)agg_basics.uround(subpixel_scale / Math.Sqrt(dx * dx + dy * dy)) >> subpixel_shift;

			// Calculate scale by Y at x1,y1
			dx = xt;
			dy = yt + delta;
			m_trans_inv.transform(ref dx, ref dy);
			dx -= x;
			dy -= y;
			int sy1 = (int)agg_basics.uround(subpixel_scale / Math.Sqrt(dx * dx + dy * dy)) >> subpixel_shift;

			// Calculate transformed coordinates at x2,y2
			x += len;
			xt = x;
			yt = y;
			m_trans_dir.transform(ref xt, ref yt);
			int x2 = agg_basics.iround(xt * subpixel_scale);
			int y2 = agg_basics.iround(yt * subpixel_scale);

			// Calculate scale by X at x2,y2
			dx = xt + delta;
			dy = yt;
			m_trans_inv.transform(ref dx, ref dy);
			dx -= x;
			dy -= y;
			int sx2 = (int)agg_basics.uround(subpixel_scale / Math.Sqrt(dx * dx + dy * dy)) >> subpixel_shift;

			// Calculate scale by Y at x2,y2
			dx = xt;
			dy = yt + delta;
			m_trans_inv.transform(ref dx, ref dy);
			dx -= x;
			dy -= y;
			int sy2 = (int)agg_basics.uround(subpixel_scale / Math.Sqrt(dx * dx + dy * dy)) >> subpixel_shift;

			// Initialize the interpolators
			m_coord_x = new dda2_line_interpolator(x1, x2, (int)len);
			m_coord_y = new dda2_line_interpolator(y1, y2, (int)len);
			m_scale_x = new dda2_line_interpolator(sx1, sx2, (int)len);
			m_scale_y = new dda2_line_interpolator(sy1, sy2, (int)len);
		}

		//----------------------------------------------------------------
		public void resynchronize(double xe, double ye, int len)
		{
			// Assume x1,y1 are equal to the ones at the previous end point
			int x1 = m_coord_x.y();
			int y1 = m_coord_y.y();
			int sx1 = m_scale_x.y();
			int sy1 = m_scale_y.y();

			// Calculate transformed coordinates at x2,y2
			double xt = xe;
			double yt = ye;
			m_trans_dir.transform(ref xt, ref yt);
			int x2 = agg_basics.iround(xt * subpixel_scale);
			int y2 = agg_basics.iround(yt * subpixel_scale);

			double delta = 1 / (double)subpixel_scale;
			double dx;
			double dy;

			// Calculate scale by X at x2,y2
			dx = xt + delta;
			dy = yt;
			m_trans_inv.transform(ref dx, ref dy);
			dx -= xe;
			dy -= ye;
			int sx2 = (int)agg_basics.uround(subpixel_scale / Math.Sqrt(dx * dx + dy * dy)) >> subpixel_shift;

			// Calculate scale by Y at x2,y2
			dx = xt;
			dy = yt + delta;
			m_trans_inv.transform(ref dx, ref dy);
			dx -= xe;
			dy -= ye;
			int sy2 = (int)agg_basics.uround(subpixel_scale / Math.Sqrt(dx * dx + dy * dy)) >> subpixel_shift;

			// Initialize the interpolators
			m_coord_x = new dda2_line_interpolator(x1, x2, (int)len);
			m_coord_y = new dda2_line_interpolator(y1, y2, (int)len);
			m_scale_x = new dda2_line_interpolator(sx1, sx2, (int)len);
			m_scale_y = new dda2_line_interpolator(sy1, sy2, (int)len);
		}

		public Transform.ITransform transformer()
		{
			throw new System.NotImplementedException();
		}

		public void transformer(Transform.ITransform trans)
		{
			throw new System.NotImplementedException();
		}

		//----------------------------------------------------------------
		public void Next()
		{
			m_coord_x.Next();
			m_coord_y.Next();
			m_scale_x.Next();
			m_scale_y.Next();
		}

		//----------------------------------------------------------------
		public void coordinates(out int x, out int y)
		{
			x = m_coord_x.y();
			y = m_coord_y.y();
		}

		//----------------------------------------------------------------
		public void local_scale(out int x, out int y)
		{
			x = m_scale_x.y();
			y = m_scale_y.y();
		}

		//----------------------------------------------------------------
		public void transform(ref double x, ref double y)
		{
			m_trans_dir.transform(ref x, ref y);
		}
	};
}