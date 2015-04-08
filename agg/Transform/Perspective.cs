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
// Perspective 2D transformations
//
//----------------------------------------------------------------------------
using System;

namespace MatterHackers.Agg.Transform
{
	//=======================================================trans_perspective
	public sealed class Perspective : ITransform
	{
		static public readonly double affine_epsilon = 1e-14;
		public double sx, shy, w0, shx, sy, w1, tx, ty, w2;

		//-------------------------------------------------------ruction
		// Identity matrix
		public Perspective()
		{
			sx = (1); shy = (0); w0 = (0);
			shx = (0); sy = (1); w1 = (0);
			tx = (0); ty = (0); w2 = (1);
		}

		// Custom matrix
		public Perspective(double v0, double v1, double v2,
						  double v3, double v4, double v5,
						  double v6, double v7, double v8)
		{
			sx = (v0); shy = (v1); w0 = (v2);
			shx = (v3); sy = (v4); w1 = (v5);
			tx = (v6); ty = (v7); w2 = (v8);
		}

		// Custom matrix from m[9]
		public Perspective(double[] m)
		{
			sx = (m[0]); shy = (m[1]); w0 = (m[2]);
			shx = (m[3]); sy = (m[4]); w1 = (m[5]);
			tx = (m[6]); ty = (m[7]); w2 = (m[8]);
		}

		// From affine
		public Perspective(Affine a)
		{
			sx = (a.sx); shy = (a.shy); w0 = (0);
			shx = (a.shx); sy = (a.sy); w1 = (0);
			tx = (a.tx); ty = (a.ty); w2 = (1);
		}

		// From trans_perspective
		public Perspective(Perspective a)
		{
			sx = (a.sx); shy = (a.shy); w0 = a.w0;
			shx = (a.shx); sy = (a.sy); w1 = a.w1;
			tx = (a.tx); ty = (a.ty); w2 = a.w2;
		}

		// Rectangle to quadrilateral
		public Perspective(double x1, double y1, double x2, double y2, double[] quad)
		{
			rect_to_quad(x1, y1, x2, y2, quad);
		}

		// Quadrilateral to rectangle
		public Perspective(double[] quad, double x1, double y1, double x2, double y2)
		{
			quad_to_rect(quad, x1, y1, x2, y2);
		}

		// Arbitrary quadrilateral transformations
		public Perspective(double[] src, double[] dst)
		{
			quad_to_quad(src, dst);
		}

		public void Set(Perspective Other)
		{
			sx = Other.sx;
			shy = Other.shy;
			w0 = Other.w0;
			shx = Other.shx;
			sy = Other.sy;
			w1 = Other.w1;
			tx = Other.tx;
			ty = Other.ty;
			w2 = Other.w2;
		}

		//-------------------------------------- Quadrilateral transformations
		// The arguments are double[8] that are mapped to quadrilaterals:
		// x1,y1, x2,y2, x3,y3, x4,y4
		public bool quad_to_quad(double[] qs, double[] qd)
		{
			Perspective p = new Perspective();
			if (!quad_to_square(qs)) return false;
			if (!p.square_to_quad(qd)) return false;
			multiply(p);
			return true;
		}

		public bool rect_to_quad(double x1, double y1, double x2, double y2, double[] q)
		{
			double[] r = new double[8];
			r[0] = r[6] = x1;
			r[2] = r[4] = x2;
			r[1] = r[3] = y1;
			r[5] = r[7] = y2;
			return quad_to_quad(r, q);
		}

		public bool quad_to_rect(double[] q, double x1, double y1, double x2, double y2)
		{
			double[] r = new double[8];
			r[0] = r[6] = x1;
			r[2] = r[4] = x2;
			r[1] = r[3] = y1;
			r[5] = r[7] = y2;
			return quad_to_quad(q, r);
		}

		// Map square (0,0,1,1) to the quadrilateral and vice versa
		public bool square_to_quad(double[] q)
		{
			double dx = q[0] - q[2] + q[4] - q[6];
			double dy = q[1] - q[3] + q[5] - q[7];
			if (dx == 0.0 && dy == 0.0)
			{
				// Affine case (parallelogram)
				//---------------
				sx = q[2] - q[0];
				shy = q[3] - q[1];
				w0 = 0.0;
				shx = q[4] - q[2];
				sy = q[5] - q[3];
				w1 = 0.0;
				tx = q[0];
				ty = q[1];
				w2 = 1.0;
			}
			else
			{
				double dx1 = q[2] - q[4];
				double dy1 = q[3] - q[5];
				double dx2 = q[6] - q[4];
				double dy2 = q[7] - q[5];
				double den = dx1 * dy2 - dx2 * dy1;
				if (den == 0.0)
				{
					// Singular case
					//---------------
					sx = shy = w0 = shx = sy = w1 = tx = ty = w2 = 0.0;
					return false;
				}
				// General case
				//---------------
				double u = (dx * dy2 - dy * dx2) / den;
				double v = (dy * dx1 - dx * dy1) / den;
				sx = q[2] - q[0] + u * q[2];
				shy = q[3] - q[1] + u * q[3];
				w0 = u;
				shx = q[6] - q[0] + v * q[6];
				sy = q[7] - q[1] + v * q[7];
				w1 = v;
				tx = q[0];
				ty = q[1];
				w2 = 1.0;
			}
			return true;
		}

		public bool quad_to_square(double[] q)
		{
			if (!square_to_quad(q)) return false;
			invert();
			return true;
		}

		//--------------------------------------------------------- Operations
		public Perspective from_affine(Affine a)
		{
			sx = a.sx; shy = a.shy; w0 = 0;
			shx = a.shx; sy = a.sy; w1 = 0;
			tx = a.tx; ty = a.ty; w2 = 1;
			return this;
		}

		// Reset - load an identity matrix
		public Perspective reset()
		{
			sx = 1; shy = 0; w0 = 0;
			shx = 0; sy = 1; w1 = 0;
			tx = 0; ty = 0; w2 = 1;
			return this;
		}

		// Invert matrix. Returns false in degenerate case
		public bool invert()
		{
			double d0 = sy * w2 - w1 * ty;
			double d1 = w0 * ty - shy * w2;
			double d2 = shy * w1 - w0 * sy;
			double d = sx * d0 + shx * d1 + tx * d2;
			if (d == 0.0)
			{
				sx = shy = w0 = shx = sy = w1 = tx = ty = w2 = 0.0;
				return false;
			}
			d = 1.0 / d;
			Perspective a = new Perspective(this);
			sx = d * d0;
			shy = d * d1;
			w0 = d * d2;
			shx = d * (a.w1 * a.tx - a.shx * a.w2);
			sy = d * (a.sx * a.w2 - a.w0 * a.tx);
			w1 = d * (a.w0 * a.shx - a.sx * a.w1);
			tx = d * (a.shx * a.ty - a.sy * a.tx);
			ty = d * (a.shy * a.tx - a.sx * a.ty);
			w2 = d * (a.sx * a.sy - a.shy * a.shx);
			return true;
		}

		// Direct transformations operations
		public Perspective translate(double x, double y)
		{
			tx += x;
			ty += y;
			return this;
		}

		public Perspective rotate(double a)
		{
			multiply(Affine.NewRotation(a));
			return this;
		}

		public Perspective scale(double s)
		{
			multiply(Affine.NewScaling(s));
			return this;
		}

		public Perspective scale(double x, double y)
		{
			multiply(Affine.NewScaling(x, y));
			return this;
		}

		public Perspective multiply(Perspective a)
		{
			Perspective b = new Perspective(this);
			sx = a.sx * b.sx + a.shx * b.shy + a.tx * b.w0;
			shx = a.sx * b.shx + a.shx * b.sy + a.tx * b.w1;
			tx = a.sx * b.tx + a.shx * b.ty + a.tx * b.w2;
			shy = a.shy * b.sx + a.sy * b.shy + a.ty * b.w0;
			sy = a.shy * b.shx + a.sy * b.sy + a.ty * b.w1;
			ty = a.shy * b.tx + a.sy * b.ty + a.ty * b.w2;
			w0 = a.w0 * b.sx + a.w1 * b.shy + a.w2 * b.w0;
			w1 = a.w0 * b.shx + a.w1 * b.sy + a.w2 * b.w1;
			w2 = a.w0 * b.tx + a.w1 * b.ty + a.w2 * b.w2;
			return this;
		}

		//------------------------------------------------------------------------
		public Perspective multiply(Affine a)
		{
			Perspective b = new Perspective(this);
			sx = a.sx * b.sx + a.shx * b.shy + a.tx * b.w0;
			shx = a.sx * b.shx + a.shx * b.sy + a.tx * b.w1;
			tx = a.sx * b.tx + a.shx * b.ty + a.tx * b.w2;
			shy = a.shy * b.sx + a.sy * b.shy + a.ty * b.w0;
			sy = a.shy * b.shx + a.sy * b.sy + a.ty * b.w1;
			ty = a.shy * b.tx + a.sy * b.ty + a.ty * b.w2;
			return this;
		}

		//------------------------------------------------------------------------
		public Perspective premultiply(Perspective b)
		{
			Perspective a = new Perspective(this);
			sx = a.sx * b.sx + a.shx * b.shy + a.tx * b.w0;
			shx = a.sx * b.shx + a.shx * b.sy + a.tx * b.w1;
			tx = a.sx * b.tx + a.shx * b.ty + a.tx * b.w2;
			shy = a.shy * b.sx + a.sy * b.shy + a.ty * b.w0;
			sy = a.shy * b.shx + a.sy * b.sy + a.ty * b.w1;
			ty = a.shy * b.tx + a.sy * b.ty + a.ty * b.w2;
			w0 = a.w0 * b.sx + a.w1 * b.shy + a.w2 * b.w0;
			w1 = a.w0 * b.shx + a.w1 * b.sy + a.w2 * b.w1;
			w2 = a.w0 * b.tx + a.w1 * b.ty + a.w2 * b.w2;
			return this;
		}

		//------------------------------------------------------------------------
		public Perspective premultiply(Affine b)
		{
			Perspective a = new Perspective(this);
			sx = a.sx * b.sx + a.shx * b.shy;
			shx = a.sx * b.shx + a.shx * b.sy;
			tx = a.sx * b.tx + a.shx * b.ty + a.tx;
			shy = a.shy * b.sx + a.sy * b.shy;
			sy = a.shy * b.shx + a.sy * b.sy;
			ty = a.shy * b.tx + a.sy * b.ty + a.ty;
			w0 = a.w0 * b.sx + a.w1 * b.shy;
			w1 = a.w0 * b.shx + a.w1 * b.sy;
			w2 = a.w0 * b.tx + a.w1 * b.ty + a.w2;
			return this;
		}

		//------------------------------------------------------------------------
		public Perspective multiply_inv(Perspective m)
		{
			Perspective t = m;
			t.invert();
			return multiply(t);
		}

		//------------------------------------------------------------------------
		public Perspective trans_perspectivemultiply_inv(Affine m)
		{
			Affine t = m;
			t.invert();
			return multiply(t);
		}

		//------------------------------------------------------------------------
		public Perspective premultiply_inv(Perspective m)
		{
			Perspective t = m;
			t.invert();
			Set(t.multiply(this));
			return this;
		}

		// Multiply inverse of "m" by "this" and assign the result to "this"
		public Perspective premultiply_inv(Affine m)
		{
			Perspective t = new Perspective(m);
			t.invert();
			Set(t.multiply(this));
			return this;
		}

		//--------------------------------------------------------- Load/Store
		public void store_to(double[] m)
		{
			m[0] = sx; m[1] = shy; m[2] = w0;
			m[3] = shx; m[4] = sy; m[5] = w1;
			m[6] = tx; m[7] = ty; m[8] = w2;
		}

		//------------------------------------------------------------------------
		public Perspective load_from(double[] m)
		{
			sx = m[0]; shy = m[1]; w0 = m[2];
			shx = m[3]; sy = m[4]; w1 = m[5];
			tx = m[6]; ty = m[7]; w2 = m[8];
			return this;
		}

		//---------------------------------------------------------- Operators
		// Multiply the matrix by another one and return the result in a separate matrix.
		public static Perspective operator *(Perspective a, Perspective b)
		{
			Perspective temp = a;
			temp.multiply(b);

			return temp;
		}

		// Multiply the matrix by another one and return the result in a separate matrix.
		public static Perspective operator *(Perspective a, Affine b)
		{
			Perspective temp = a;
			temp.multiply(b);

			return temp;
		}

		// Multiply the matrix by inverse of another one and return the result in a separate matrix.
		public static Perspective operator /(Perspective a, Perspective b)
		{
			Perspective temp = a;
			temp.multiply_inv(b);

			return temp;
		}

		// Calculate and return the inverse matrix
		public static Perspective operator ~(Perspective b)
		{
			Perspective ret = b;
			ret.invert();
			return ret;
		}

		// Equal operator with default epsilon
		public static bool operator ==(Perspective a, Perspective b)
		{
			return a.is_equal(b, affine_epsilon);
		}

		// Not Equal operator with default epsilon
		public static bool operator !=(Perspective a, Perspective b)
		{
			return !a.is_equal(b, affine_epsilon);
		}

		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		//---------------------------------------------------- Transformations
		// Direct transformation of x and y
		public void transform(ref double px, ref double py)
		{
			double x = px;
			double y = py;
			double m = 1.0 / (x * w0 + y * w1 + w2);
			px = m * (x * sx + y * shx + tx);
			py = m * (x * shy + y * sy + ty);
		}

		// Direct transformation of x and y, affine part only
		public void transform_affine(ref double x, ref double y)
		{
			double tmp = x;
			x = tmp * sx + y * shx + tx;
			y = tmp * shy + y * sy + ty;
		}

		// Direct transformation of x and y, 2x2 matrix only, no translation
		public void transform_2x2(ref double x, ref double y)
		{
			double tmp = x;
			x = tmp * sx + y * shx;
			y = tmp * shy + y * sy;
		}

		// Inverse transformation of x and y. It works slow because
		// it explicitly inverts the matrix on every call. For massive
		// operations it's better to invert() the matrix and then use
		// direct transformations.
		public void inverse_transform(ref double x, ref double y)
		{
			Perspective t = new Perspective(this);
			if (t.invert()) t.transform(ref x, ref y);
		}

		//---------------------------------------------------------- Auxiliary
		public double determinant()
		{
			return sx * (sy * w2 - ty * w1) +
				   shx * (ty * w0 - shy * w2) +
				   tx * (shy * w1 - sy * w0);
		}

		public double determinant_reciprocal()
		{
			return 1.0 / determinant();
		}

		public bool is_valid()
		{
			return is_valid(affine_epsilon);
		}

		public bool is_valid(double epsilon)
		{
			return Math.Abs(sx) > epsilon && Math.Abs(sy) > epsilon && Math.Abs(w2) > epsilon;
		}

		public bool is_identity()
		{
			return is_identity(affine_epsilon);
		}

		public bool is_identity(double epsilon)
		{
			return agg_basics.is_equal_eps(sx, 1.0, epsilon) &&
				   agg_basics.is_equal_eps(shy, 0.0, epsilon) &&
				   agg_basics.is_equal_eps(w0, 0.0, epsilon) &&
				   agg_basics.is_equal_eps(shx, 0.0, epsilon) &&
				   agg_basics.is_equal_eps(sy, 1.0, epsilon) &&
				   agg_basics.is_equal_eps(w1, 0.0, epsilon) &&
				   agg_basics.is_equal_eps(tx, 0.0, epsilon) &&
				   agg_basics.is_equal_eps(ty, 0.0, epsilon) &&
				   agg_basics.is_equal_eps(w2, 1.0, epsilon);
		}

		public bool is_equal(Perspective m)
		{
			return is_equal(m, affine_epsilon);
		}

		public bool is_equal(Perspective m, double epsilon)
		{
			return agg_basics.is_equal_eps(sx, m.sx, epsilon) &&
				   agg_basics.is_equal_eps(shy, m.shy, epsilon) &&
				   agg_basics.is_equal_eps(w0, m.w0, epsilon) &&
				   agg_basics.is_equal_eps(shx, m.shx, epsilon) &&
				   agg_basics.is_equal_eps(sy, m.sy, epsilon) &&
				   agg_basics.is_equal_eps(w1, m.w1, epsilon) &&
				   agg_basics.is_equal_eps(tx, m.tx, epsilon) &&
				   agg_basics.is_equal_eps(ty, m.ty, epsilon) &&
				   agg_basics.is_equal_eps(w2, m.w2, epsilon);
		}

		// Determine the major affine parameters. Use with caution
		// considering possible degenerate cases.
		public double scale()
		{
			double x = 0.707106781 * sx + 0.707106781 * shx;
			double y = 0.707106781 * shy + 0.707106781 * sy;
			return Math.Sqrt(x * x + y * y);
		}

		public double rotation()
		{
			double x1 = 0.0;
			double y1 = 0.0;
			double x2 = 1.0;
			double y2 = 0.0;
			transform(ref x1, ref y1);
			transform(ref x2, ref y2);
			return Math.Atan2(y2 - y1, x2 - x1);
		}

		public void translation(out double dx, out double dy)
		{
			dx = tx;
			dy = ty;
		}

		public void scaling(out double x, out double y)
		{
			double x1 = 0.0;
			double y1 = 0.0;
			double x2 = 1.0;
			double y2 = 1.0;
			Perspective t = new Perspective(this);
			t *= Affine.NewRotation(-rotation());
			t.transform(ref x1, ref y1);
			t.transform(ref x2, ref y2);
			x = x2 - x1;
			y = y2 - y1;
		}

		public void scaling_abs(out double x, out double y)
		{
			x = Math.Sqrt(sx * sx + shx * shx);
			y = Math.Sqrt(shy * shy + sy * sy);
		}

		//--------------------------------------------------------------------
		public sealed class iterator_x
		{
			private double den;
			private double den_step;
			private double nom_x;
			private double nom_x_step;
			private double nom_y;
			private double nom_y_step;

			public double x;
			public double y;

			public iterator_x()
			{
			}

			public iterator_x(double px, double py, double step, Perspective m)
			{
				den = (px * m.w0 + py * m.w1 + m.w2);
				den_step = (m.w0 * step);
				nom_x = (px * m.sx + py * m.shx + m.tx);
				nom_x_step = (step * m.sx);
				nom_y = (px * m.shy + py * m.sy + m.ty);
				nom_y_step = (step * m.shy);
				x = (nom_x / den);
				y = (nom_y / den);
			}

			public static iterator_x operator ++(iterator_x a)
			{
				a.den += a.den_step;
				a.nom_x += a.nom_x_step;
				a.nom_y += a.nom_y_step;
				double d = 1.0 / a.den;
				a.x = a.nom_x * d;
				a.y = a.nom_y * d;

				return a;
			}
		};

		//--------------------------------------------------------------------
		public iterator_x begin(double x, double y, double step)
		{
			return new iterator_x(x, y, step, this);
		}
	};
}