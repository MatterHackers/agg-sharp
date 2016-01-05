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
// Adaptation for high precision colors has been sponsored by
// Liberty Technology Systems, Inc., visit http://lib-sys.com
//
// Liberty Technology Systems, Inc. is the provider of
// PostScript and PDF technology for software developers.
//
//----------------------------------------------------------------------------
//
// color types gray8, gray16
//
//----------------------------------------------------------------------------
namespace MatterHackers.Agg
{
	//===================================================================gray8
	public struct gray8
	{
		private const uint base_mask = base_scale - 1;
		private const uint base_scale = (uint)(1 << base_shift);
		private const int base_shift = 8;
		private byte a;
		private byte v;
		//--------------------------------------------------------------------
		public gray8(uint v_)
			: this(v_, (uint)base_mask)
		{
		}

		public gray8(uint v_, uint a_)
		{
			v = (byte)(v_);
			a = (byte)(a_);
		}

		//--------------------------------------------------------------------
		public gray8(RGBA_Floats c)
		{
			v = ((byte)agg_basics.uround((0.299 * c.Red0To255 + 0.587 * c.Green0To255 + 0.114 * c.Blue0To255) * (double)(base_mask)));
			a = ((byte)agg_basics.uround(c.Alpha0To255 * (double)(base_mask)));
		}

		//--------------------------------------------------------------------
		public gray8(RGBA_Floats c, double a_)
		{
			v = ((byte)agg_basics.uround((0.299 * c.Red0To255 + 0.587 * c.Green0To255 + 0.114 * c.Blue0To255) * (double)(base_mask)));
			a = ((byte)agg_basics.uround(a_ * (double)(base_mask)));
		}

		//--------------------------------------------------------------------
		public gray8(RGBA_Bytes c)
		{
			v = (byte)((c.Red0To255 * 77 + c.Green0To255 * 150 + c.Blue0To255 * 29) >> 8);
			a = (byte)(c.Alpha0To255);
		}

		//--------------------------------------------------------------------
		public gray8(RGBA_Bytes c, int a_)
		{
			v = (byte)((c.Red0To255 * 77 + c.Green0To255 * 150 + c.Blue0To255 * 29) >> 8);
			a = (byte)(a_);
		}

		//--------------------------------------------------------------------
		private gray8(gray8 c, uint a_)
		{
			v = (c.v);
			a = (byte)(a_);
		}
		//--------------------------------------------------------------------
		public void clear()
		{
			v = a = 0;
		}

		//--------------------------------------------------------------------
		public gray8 demultiply()
		{
			if (a == (int)base_mask) return this;
			if (a == 0)
			{
				v = 0;
				return this;
			}
			int v_ = ((int)(v) * (int)base_mask) / a;
			v = (byte)((v_ > (int)base_mask) ? (byte)base_mask : v_);
			return this;
		}

		//--------------------------------------------------------------------
		public gray8 gradient(gray8 c, double k)
		{
			gray8 ret;
			int ik = agg_basics.uround(k * (int)base_scale);
			ret.v = (byte)((int)(v) + ((((int)(c.v) - v) * ik) >> base_shift));
			ret.a = (byte)((int)(a) + ((((int)(c.a) - a) * ik) >> base_shift));
			return ret;
		}

		//--------------------------------------------------------------------
		public void opacity(double a_)
		{
			if (a_ < 0.0) a_ = 0.0;
			if (a_ > 1.0) a_ = 1.0;
			a = (byte)agg_basics.uround(a_ * (double)(base_mask));
		}

		//--------------------------------------------------------------------
		public double opacity()
		{
			return (double)(a) / (double)(base_mask);
		}

		//--------------------------------------------------------------------
		public gray8 premultiply()
		{
			if (a == (byte)base_mask) return this;
			if (a == 0)
			{
				v = 0;
				return this;
			}
			v = (byte)(((int)(v) * a) >> base_shift);
			return this;
		}

		//--------------------------------------------------------------------
		public gray8 premultiply(int a_)
		{
			if (a == (int)base_mask && a_ >= (int)base_mask) return this;
			if (a == 0 || a_ == 0)
			{
				v = a = 0;
				return this;
			}
			int v_ = ((int)(v) * a_) / a;
			v = (byte)((v_ > a_) ? a_ : v_);
			a = (byte)(a_);
			return this;
		}

		//--------------------------------------------------------------------
		public gray8 transparent()
		{
			a = 0;
			return this;
		}
		/*
		//--------------------------------------------------------------------
		void add(gray8 c, int cover)
		{
			int cv, ca;
			if(cover == cover_mask)
			{
				if (c.a == base_mask)
				{
					*this = c;
				}
				else
				{
					cv = v + c.v; v = (cv > (int)(base_mask)) ? (int)(base_mask) : cv;
					ca = a + c.a; a = (ca > (int)(base_mask)) ? (int)(base_mask) : ca;
				}
			}
			else
			{
				cv = v + ((c.v * cover + cover_mask/2) >> cover_shift);
				ca = a + ((c.a * cover + cover_mask/2) >> cover_shift);
				v = (cv > (int)(base_mask)) ? (int)(base_mask) : cv;
				a = (ca > (int)(base_mask)) ? (int)(base_mask) : ca;
			}
		}
		 */

		//--------------------------------------------------------------------
		//static gray8 no_color() { return gray8(0,0); }

		/*
		static gray8 gray8_pre(int v, int a = gray8.base_mask)
		{
			return gray8(v,a).premultiply();
		}

		static gray8 gray8_pre(gray8 c, uint a)
		{
			return gray8(c,a).premultiply();
		}

		static gray8 gray8_pre(rgba& c)
		{
			return gray8(c).premultiply();
		}

		static gray8 gray8_pre(rgba& c, double a)
		{
			return gray8(c,a).premultiply();
		}

		static gray8 gray8_pre(rgba8& c)
		{
			return gray8(c).premultiply();
		}

		static gray8 gray8_pre(rgba8& c, uint a)
		{
			return gray8(c,a).premultiply();
		}
		 */
	}
}