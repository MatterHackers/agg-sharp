using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

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
//
// class gamma_spline
//
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	//------------------------------------------------------------------------
	// Class-helper for calculation gamma-correction arrays. A gamma-correction
	// array is an array of 256 unsigned chars that determine the actual values
	// of Anti-Aliasing for each pixel coverage value from 0 to 255. If all the
	// values in the array are equal to its index, i.e. 0,1,2,3,... there's
	// no gamma-correction. Class agg::polyfill allows you to use custom
	// gamma-correction arrays. You can calculate it using any approach, and
	// class gamma_spline allows you to calculate almost any reasonable shape
	// of the gamma-curve with using only 4 values - kx1, ky1, kx2, ky2.
	//
	//                                      kx2
	//        +----------------------------------+
	//        |                 |        |    .  |
	//        |                 |        | .     | ky2
	//        |                 |       .  ------|
	//        |                 |    .           |
	//        |                 | .              |
	//        |----------------.|----------------|
	//        |             .   |                |
	//        |          .      |                |
	//        |-------.         |                |
	//    ky1 |    .   |        |                |
	//        | .      |        |                |
	//        +----------------------------------+
	//            kx1
	//
	// Each value can be in range [0...2]. Value 1.0 means one quarter of the
	// bounding rectangle. Function values() calculates the curve by these
	// 4 values. After calling it one can get the gamma-array with call gamma().
	// Class also supports the vertex source interface, i.e rewind() and
	// vertex(). It's made for convinience and used in class gamma_ctrl.
	// Before calling rewind/vertex one must set the bounding box
	// box() using pixel coordinates.
	//------------------------------------------------------------------------

	public class gamma_spline : SimpleVertexSourceWidget
	{
		private byte[] m_gamma = new byte[256];
		private double[] m_x = new double[4];
		private double[] m_y = new double[4];
		private bspline m_spline = new bspline();

		//double        m_x1;
		//double        m_y1;
		//double        m_x2;
		//double        m_y2;
		private double m_cur_x;

		public gamma_spline()
			: base(new Vector2())
		{
			m_cur_x = (0.0);
			values(1.0, 1.0, 1.0, 1.0);
		}

		public override int num_paths()
		{
			throw new System.Exception("The method or operation is not implemented.");
		}

		public void values(double kx1, double ky1, double kx2, double ky2)
		{
			if (kx1 < 0.001) kx1 = 0.001;
			if (kx1 > 1.999) kx1 = 1.999;
			if (ky1 < 0.001) ky1 = 0.001;
			if (ky1 > 1.999) ky1 = 1.999;
			if (kx2 < 0.001) kx2 = 0.001;
			if (kx2 > 1.999) kx2 = 1.999;
			if (ky2 < 0.001) ky2 = 0.001;
			if (ky2 > 1.999) ky2 = 1.999;

			m_x[0] = 0.0;
			m_y[0] = 0.0;
			m_x[1] = kx1 * 0.25;
			m_y[1] = ky1 * 0.25;
			m_x[2] = 1.0 - kx2 * 0.25;
			m_y[2] = 1.0 - ky2 * 0.25;
			m_x[3] = 1.0;
			m_y[3] = 1.0;

			m_spline.init(4, m_x, m_y);

			int i;
			for (i = 0; i < 256; i++)
			{
				m_gamma[i] = (byte)(y((double)(i) / 255.0) * 255.0);
			}
		}

		public byte[] gamma()
		{
			return m_gamma;
		}

		public double y(double x)
		{
			if (x < 0.0) x = 0.0;
			if (x > 1.0) x = 1.0;
			double val = m_spline.get(x);
			if (val < 0.0) val = 0.0;
			if (val > 1.0) val = 1.0;
			return val;
		}

		public void values(out double kx1, out double ky1, out double kx2, out double ky2)
		{
			kx1 = m_x[1] * 4.0;
			ky1 = m_y[1] * 4.0;
			kx2 = (1.0 - m_x[2]) * 4.0;
			ky2 = (1.0 - m_y[2]) * 4.0;
		}

		public void box(double x1, double y1, double x2, double y2)
		{
			//BoundsRelativeToParent = new rect_d(x1, y1, x2, y2);
		}

		public override IEnumerable<VertexData> Vertices()
		{
			throw new NotImplementedException();
		}

		public override void rewind(int idx)
		{
			m_cur_x = 0.0;
		}

		public override ShapePath.FlagsAndCommand vertex(out double ox, out double oy)
		{
			RectangleDouble localBounds = new RectangleDouble(10, 10, 100, 100);

			ox = 0;
			oy = 0;
			if (m_cur_x == 0.0)
			{
				ox = localBounds.Left;
				oy = localBounds.Bottom;
				m_cur_x += 1.0 / (localBounds.Right - localBounds.Left);
				return ShapePath.FlagsAndCommand.CommandMoveTo;
			}

			if (m_cur_x > 1.0)
			{
				return ShapePath.FlagsAndCommand.CommandStop;
			}

			ox = localBounds.Left + m_cur_x * (localBounds.Right - localBounds.Left);
			oy = localBounds.Bottom + y(m_cur_x) * (localBounds.Top - localBounds.Bottom);

			m_cur_x += 1.0 / (localBounds.Right - localBounds.Left);
			return ShapePath.FlagsAndCommand.CommandLineTo;
		}
	};
}