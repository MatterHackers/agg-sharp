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
//
// Adaptation for high precision colors has been sponsored by
// Liberty Technology Systems, Inc., visit http://lib-sys.com
//
// Liberty Technology Systems, Inc. is the provider of
// PostScript and PDF technology for software developers.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
using System;

namespace MatterHackers.Agg
{
	// Supported byte orders for RGB and RGBA pixel formats
	//=======================================================================
	internal struct order_rgb { private enum rgb_e { R = 0, G = 1, B = 2, rgb_tag }; };       //----order_rgb

	internal struct order_bgr { private enum bgr_e { B = 0, G = 1, R = 2, rgb_tag }; };       //----order_bgr

	internal struct order_rgba { private enum rgba_e { R = 0, G = 1, B = 2, A = 3, rgba_tag }; }; //----order_rgba

	internal struct order_argb { private enum argb_e { A = 0, R = 1, G = 2, B = 3, rgba_tag }; }; //----order_argb

	internal struct order_abgr { private enum abgr_e { A = 0, B = 1, G = 2, R = 3, rgba_tag }; }; //----order_abgr

	internal struct order_bgra { private enum bgra_e { B = 0, G = 1, R = 2, A = 3, rgba_tag }; }; //----order_bgra

	public struct RGBA_Floats : IColorType
	{
		private const int base_shift = 8;
		private const int base_scale = (int)(1 << base_shift);
		private const int base_mask = base_scale - 1;

		public float red;
		public float green;
		public float blue;
		public float alpha;

		public int Red0To255 { get { return (int)agg_basics.uround(red * (float)base_mask); } set { red = (float)value / (float)base_mask; } }

		public int Green0To255 { get { return (int)agg_basics.uround(green * (float)base_mask); } set { green = (float)value / (float)base_mask; } }

		public int Blue0To255 { get { return (int)agg_basics.uround(blue * (float)base_mask); } set { blue = (float)value / (float)base_mask; } }

		public int Alpha0To255 { get { return (int)agg_basics.uround(alpha * (float)base_mask); } set { alpha = (float)value / (float)base_mask; } }

		public float Red0To1 { get { return red; } set { red = value; } }

		public float Green0To1 { get { return green; } set { green = value; } }

		public float Blue0To1 { get { return blue; } set { blue = value; } }

		public float Alpha0To1 { get { return alpha; } set { alpha = value; } }

		#region Defined Colors

		public static readonly RGBA_Floats White = new RGBA_Floats(1, 1, 1, 1);
		public static readonly RGBA_Floats Black = new RGBA_Floats(0, 0, 0, 1);
		public static readonly RGBA_Floats Red = new RGBA_Floats(1, 0, 0, 1);
		public static readonly RGBA_Floats Green = new RGBA_Floats(0, 1, 0, 1);
		public static readonly RGBA_Floats Blue = new RGBA_Floats(0, 0, 1, 1);
		public static readonly RGBA_Floats Cyan = new RGBA_Floats(0, 1, 1, 1);
		public static readonly RGBA_Floats Magenta = new RGBA_Floats(1, 0, 1, 1);
		public static readonly RGBA_Floats Yellow = new RGBA_Floats(1, 1, 0, 1);

		#endregion Defined Colors

		#region Constructors

		public RGBA_Floats(double r_, double g_, double b_)
			: this(r_, g_, b_, 1.0)
		{
		}

		public RGBA_Floats(double r_, double g_, double b_, double a_)
		{
			red = (float)r_;
			green = (float)g_;
			blue = (float)b_;
			alpha = (float)a_;
		}

		public RGBA_Floats(float r_, float g_, float b_)
			: this(r_, g_, b_, 1.0f)
		{
		}

		public RGBA_Floats(float r_, float g_, float b_, float a_)
		{
			red = r_;
			green = g_;
			blue = b_;
			alpha = a_;
		}

		public RGBA_Floats(RGBA_Floats c)
			: this(c, c.alpha)
		{
		}

		public RGBA_Floats(RGBA_Floats c, float a_)
		{
			red = c.red;
			green = c.green;
			blue = c.blue;
			alpha = a_;
		}

		public RGBA_Floats(float wavelen)
			: this(wavelen, 1.0f)
		{
		}

		public RGBA_Floats(float wavelen, float gamma)
		{
			this = from_wavelength(wavelen, gamma);
		}

		public RGBA_Floats(RGBA_Bytes color)
		{
			red = color.Red0To1;
			green = color.Green0To1;
			blue = color.Blue0To1;
			alpha = color.Alpha0To1;
		}

		#endregion Constructors

		#region HSL

		// Given H,S,L,A in range of 0-1
		// Returns a Color (RGB struct) in range of 0-255
		public static RGBA_Floats FromHSL(double hue0To1, double saturation0To1, double lightness0To1, double alpha = 1)
		{
			double v;
			double r, g, b;
			if (alpha > 1.0)
			{
				alpha = 1.0;
			}

			r = lightness0To1;   // default to gray
			g = lightness0To1;
			b = lightness0To1;
			v = lightness0To1 + saturation0To1 - lightness0To1 * saturation0To1;
			if (lightness0To1 <= 0.5)
			{
				v = lightness0To1 * (1.0 + saturation0To1);
			}

			if (v > 0)
			{
				double m;
				double sv;
				int sextant;
				double fract, vsf, mid1, mid2;

				m = lightness0To1 + lightness0To1 - v;
				sv = (v - m) / v;
				hue0To1 *= 6.0;
				sextant = (int)hue0To1;
				fract = hue0To1 - sextant;
				vsf = v * sv * fract;
				mid1 = m + vsf;
				mid2 = v - vsf;
				switch (sextant)
				{
					case 0:
						r = v;
						g = mid1;
						b = m;
						break;

					case 1:
						r = mid2;
						g = v;
						b = m;
						break;

					case 2:
						r = m;
						g = v;
						b = mid1;
						break;

					case 3:
						r = m;
						g = mid2;
						b = v;
						break;

					case 4:
						r = mid1;
						g = m;
						b = v;
						break;

					case 5:
						r = v;
						g = m;
						b = mid2;
						break;

					case 6:
						goto case 0;
				}
			}

			return new RGBA_Floats(r, g, b, alpha);
		}

		public void GetHSL(out double hue0To1, out double saturation0To1, out double lightness0To1)
		{
			double maxRGB = Math.Max(red, Math.Max(green, blue));
			double minRGB = Math.Min(red, Math.Min(green, blue));
			double deltaMaxToMin = maxRGB - minRGB;
			double r2, g2, b2;

			hue0To1 = 0; // default to black
			saturation0To1 = 0;
			lightness0To1 = 0;
			lightness0To1 = (minRGB + maxRGB) / 2.0;
			if (lightness0To1 <= 0.0)
			{
				return;
			}
			saturation0To1 = deltaMaxToMin;
			if (saturation0To1 > 0.0)
			{
				saturation0To1 /= (lightness0To1 <= 0.5) ? (maxRGB + minRGB) : (2.0 - maxRGB - minRGB);
			}
			else
			{
				return;
			}
			r2 = (maxRGB - red) / deltaMaxToMin;
			g2 = (maxRGB - green) / deltaMaxToMin;
			b2 = (maxRGB - blue) / deltaMaxToMin;
			if (red == maxRGB)
			{
				if (green == minRGB)
				{
					hue0To1 = 5.0 + b2;
				}
				else
				{
					hue0To1 = 1.0 - g2;
				}
			}
			else if (green == maxRGB)
			{
				if (blue == minRGB)
				{
					hue0To1 = 1.0 + r2;
				}
				else
				{
					hue0To1 = 3.0 - b2;
				}
			}
			else
			{
				if (red == minRGB)
				{
					hue0To1 = 3.0 + g2;
				}
				else
				{
					hue0To1 = 5.0 - r2;
				}
			}
			hue0To1 /= 6.0;
		}

		public static RGBA_Floats AdjustSaturation(RGBA_Floats original, double saturationMultiplier)
		{
			double hue0To1;
			double saturation0To1;
			double lightness0To1;
			original.GetHSL(out hue0To1, out saturation0To1, out lightness0To1);
			saturation0To1 *= saturationMultiplier;

			return FromHSL(hue0To1, saturation0To1, lightness0To1);
		}

		public static RGBA_Floats AdjustLightness(RGBA_Floats original, double lightnessMultiplier)
		{
			double hue0To1;
			double saturation0To1;
			double lightness0To1;
			original.GetHSL(out hue0To1, out saturation0To1, out lightness0To1);
			lightness0To1 *= lightnessMultiplier;

			return FromHSL(hue0To1, saturation0To1, lightness0To1);
		}

		#endregion HSL

		public static bool operator ==(RGBA_Floats a, RGBA_Floats b)
		{
			if (a.red == b.red && a.green == b.green && a.blue == b.blue && a.alpha == b.alpha)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(RGBA_Floats a, RGBA_Floats b)
		{
			if (a.red != b.red || a.green != b.green || a.blue != b.blue || a.alpha != b.alpha)
			{
				return true;
			}

			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj.GetType() == typeof(RGBA_Floats))
			{
				return this == (RGBA_Floats)obj;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return new { blue, green, red, alpha }.GetHashCode();
		}

		public RGBA_Bytes GetAsRGBA_Bytes()
		{
			return new RGBA_Bytes(Red0To255, Green0To255, Blue0To255, Alpha0To255);
		}

		public RGBA_Floats GetAsRGBA_Floats()
		{
			return this;
		}

		static public RGBA_Floats operator +(RGBA_Floats A, RGBA_Floats B)
		{
			RGBA_Floats temp = new RGBA_Floats();
			temp.red = A.red + B.red;
			temp.green = A.green + B.green;
			temp.blue = A.blue + B.blue;
			temp.alpha = A.alpha + B.alpha;
			return temp;
		}

		static public RGBA_Floats operator -(RGBA_Floats A, RGBA_Floats B)
		{
			RGBA_Floats temp = new RGBA_Floats();
			temp.red = A.red - B.red;
			temp.green = A.green - B.green;
			temp.blue = A.blue - B.blue;
			temp.alpha = A.alpha - B.alpha;
			return temp;
		}

		static public RGBA_Floats operator *(RGBA_Floats A, RGBA_Floats B)
		{
			RGBA_Floats temp = new RGBA_Floats();
			temp.red = A.red * B.red;
			temp.green = A.green * B.green;
			temp.blue = A.blue * B.blue;
			temp.alpha = A.alpha * B.alpha;
			return temp;
		}

		static public RGBA_Floats operator /(RGBA_Floats A, RGBA_Floats B)
		{
			RGBA_Floats temp = new RGBA_Floats();
			temp.red = A.red / B.red;
			temp.green = A.green / B.green;
			temp.blue = A.blue / B.blue;
			temp.alpha = A.alpha / B.alpha;
			return temp;
		}

		static public RGBA_Floats operator /(RGBA_Floats A, float B)
		{
			RGBA_Floats temp = new RGBA_Floats();
			temp.red = A.red / B;
			temp.green = A.green / B;
			temp.blue = A.blue / B;
			temp.alpha = A.alpha / B;
			return temp;
		}

		static public RGBA_Floats operator /(RGBA_Floats A, double doubleB)
		{
			float B = (float)doubleB;
			RGBA_Floats temp = new RGBA_Floats();
			temp.red = A.red / B;
			temp.green = A.green / B;
			temp.blue = A.blue / B;
			temp.alpha = A.alpha / B;
			return temp;
		}

		static public RGBA_Floats operator *(RGBA_Floats A, float B)
		{
			RGBA_Floats temp = new RGBA_Floats();
			temp.red = A.red * B;
			temp.green = A.green * B;
			temp.blue = A.blue * B;
			temp.alpha = A.alpha * B;
			return temp;
		}

		static public RGBA_Floats operator *(RGBA_Floats A, double doubleB)
		{
			float B = (float)doubleB;
			RGBA_Floats temp = new RGBA_Floats();
			temp.red = A.red * B;
			temp.green = A.green * B;
			temp.blue = A.blue * B;
			temp.alpha = A.alpha * B;
			return temp;
		}

		public void clear()
		{
			red = green = blue = alpha = 0;
		}

		public RGBA_Floats transparent()
		{
			alpha = 0.0f;
			return this;
		}

		public RGBA_Floats opacity(float a_)
		{
			if (a_ < 0.0) a_ = 0.0f;
			if (a_ > 1.0) a_ = 1.0f;
			alpha = a_;
			return this;
		}

		public float opacity()
		{
			return alpha;
		}

		public RGBA_Floats premultiply()
		{
			red *= alpha;
			green *= alpha;
			blue *= alpha;
			return this;
		}

		public RGBA_Floats premultiply(float a_)
		{
			if (alpha <= 0.0 || a_ <= 0.0)
			{
				red = green = blue = alpha = 0.0f;
				return this;
			}
			a_ /= alpha;
			red *= a_;
			green *= a_;
			blue *= a_;
			alpha = a_;
			return this;
		}

		public static RGBA_Floats ComponentMax(RGBA_Floats a, RGBA_Floats b)
		{
			RGBA_Floats result = a;
			if (result.red < b.red) result.red = b.red;
			if (result.green < b.green) result.green = b.green;
			if (result.blue < b.blue) result.blue = b.blue;
			if (result.alpha < b.alpha) result.alpha = b.alpha;

			return result;
		}

		public RGBA_Floats demultiply()
		{
			if (alpha == 0)
			{
				red = green = blue = 0;
				return this;
			}
			float a_ = 1.0f / alpha;
			red *= a_;
			green *= a_;
			blue *= a_;
			return this;
		}

		public RGBA_Bytes gradient(RGBA_Bytes c_8, double k)
		{
			RGBA_Floats c = c_8.GetAsRGBA_Floats();
			RGBA_Floats ret;
			ret.red = (float)(red + (c.red - red) * k);
			ret.green = (float)(green + (c.green - green) * k);
			ret.blue = (float)(blue + (c.blue - blue) * k);
			ret.alpha = (float)(alpha + (c.alpha - alpha) * k);
			return ret.GetAsRGBA_Bytes();
		}

		public static IColorType no_color()
		{
			return (IColorType)new RGBA_Floats(0, 0, 0, 0);
		}

		public static RGBA_Floats from_wavelength(float wl)
		{
			return from_wavelength(wl, 1.0f);
		}

		public static RGBA_Floats from_wavelength(float wl, float gamma)
		{
			RGBA_Floats t = new RGBA_Floats(0.0f, 0.0f, 0.0f);

			if (wl >= 380.0 && wl <= 440.0)
			{
				t.red = (float)(-1.0 * (wl - 440.0) / (440.0 - 380.0));
				t.blue = 1.0f;
			}
			else if (wl >= 440.0 && wl <= 490.0)
			{
				t.green = (float)((wl - 440.0) / (490.0 - 440.0));
				t.blue = 1.0f;
			}
			else if (wl >= 490.0 && wl <= 510.0)
			{
				t.green = 1.0f;
				t.blue = (float)(-1.0 * (wl - 510.0) / (510.0 - 490.0));
			}
			else if (wl >= 510.0 && wl <= 580.0)
			{
				t.red = (float)((wl - 510.0) / (580.0 - 510.0));
				t.green = 1.0f;
			}
			else if (wl >= 580.0 && wl <= 645.0)
			{
				t.red = 1.0f;
				t.green = (float)(-1.0 * (wl - 645.0) / (645.0 - 580.0));
			}
			else if (wl >= 645.0 && wl <= 780.0)
			{
				t.red = 1.0f;
			}

			float s = 1.0f;
			if (wl > 700.0) s = (float)(0.3 + 0.7 * (780.0 - wl) / (780.0 - 700.0));
			else if (wl < 420.0) s = (float)(0.3 + 0.7 * (wl - 380.0) / (420.0 - 380.0));

			t.red = (float)Math.Pow(t.red * s, gamma);
			t.green = (float)Math.Pow(t.green * s, gamma);
			t.blue = (float)Math.Pow(t.blue * s, gamma);

			return t;
		}

		public static RGBA_Floats rgba_pre(double r, double g, double b)
		{
			return rgba_pre((float)r, (float)g, (float)b, 1.0f);
		}

		public static RGBA_Floats rgba_pre(float r, float g, float b)
		{
			return rgba_pre(r, g, b, 1.0f);
		}

		public static RGBA_Floats rgba_pre(float r, float g, float b, float a)
		{
			return new RGBA_Floats(r, g, b, a).premultiply();
		}

		public static RGBA_Floats rgba_pre(double r, double g, double b, double a)
		{
			return new RGBA_Floats((float)r, (float)g, (float)b, (float)a).premultiply();
		}

		public static RGBA_Floats rgba_pre(RGBA_Floats c)
		{
			return new RGBA_Floats(c).premultiply();
		}

		public static RGBA_Floats rgba_pre(RGBA_Floats c, float a)
		{
			return new RGBA_Floats(c, a).premultiply();
		}

		public static RGBA_Floats GetTweenColor(RGBA_Floats Color1, RGBA_Floats Color2, double RatioOf2)
		{
			if (RatioOf2 <= 0)
			{
				return new RGBA_Floats(Color1);
			}

			if (RatioOf2 >= 1.0)
			{
				return new RGBA_Floats(Color2);
			}

			// figure out how much of each color we should be.
			double RatioOf1 = 1.0 - RatioOf2;
			return new RGBA_Floats(
				Color1.red * RatioOf1 + Color2.red * RatioOf2,
				Color1.green * RatioOf1 + Color2.green * RatioOf2,
				Color1.blue * RatioOf1 + Color2.blue * RatioOf2);
		}

		public RGBA_Floats Blend(RGBA_Floats other, double weight)
		{
			RGBA_Floats result = new RGBA_Floats(this);
			result = this * (1 - weight) + other * weight;
			return result;
		}

		public double SumOfDistances(RGBA_Floats other)
		{
			double dist = Math.Abs(red - other.red) + Math.Abs(green - other.green) + Math.Abs(blue - other.blue);
			return dist;
		}

		private void Clamp0To1(ref float value)
		{
			if (value < 0)
			{
				value = 0;
			}
			else if (value > 1)
			{
				value = 1;
			}
		}

		public void Clamp0To1()
		{
			Clamp0To1(ref red);
			Clamp0To1(ref green);
			Clamp0To1(ref blue);
			Clamp0To1(ref alpha);
		}
	}

	public struct RGBA_Bytes : IColorType
	{
		public const int cover_shift = 8;
		public const int cover_size = 1 << cover_shift;  //----cover_size
		public const int cover_mask = cover_size - 1;    //----cover_mask
		//public const int cover_none  = 0,                 //----cover_none
		//public const int cover_full  = cover_mask         //----cover_full

		public const int base_shift = 8;
		public const int base_scale = (int)(1 << base_shift);
		public const int base_mask = base_scale - 1;

		public byte blue;
		public byte green;
		public byte red;
		public byte alpha;

		public static readonly RGBA_Bytes Transparent = new RGBA_Bytes(255, 255, 255, 0);
		public static readonly RGBA_Bytes White = new RGBA_Bytes(255, 255, 255, 255);
		public static readonly RGBA_Bytes LightGray = new RGBA_Bytes(225, 225, 225, 255);
		public static readonly RGBA_Bytes Gray = new RGBA_Bytes(125, 125, 125, 235);
		public static readonly RGBA_Bytes DarkGray = new RGBA_Bytes(85, 85, 85, 255);
		public static readonly RGBA_Bytes Black = new RGBA_Bytes(0, 0, 0, 255);
		public static readonly RGBA_Bytes Red = new RGBA_Bytes(255, 0, 0, 255);
		public static readonly RGBA_Bytes Orange = new RGBA_Bytes(255, 127, 0, 255);
		public static readonly RGBA_Bytes Pink = new RGBA_Bytes(255, 192, 203, 255);
		public static readonly RGBA_Bytes Green = new RGBA_Bytes(0, 255, 0, 255);
		public static readonly RGBA_Bytes Blue = new RGBA_Bytes(0, 0, 255, 255);
		public static readonly RGBA_Bytes Indigo = new RGBA_Bytes(75, 0, 130, 255);
		public static readonly RGBA_Bytes Violet = new RGBA_Bytes(143, 0, 255, 255);
		public static readonly RGBA_Bytes Cyan = new RGBA_Bytes(0, 255, 255, 255);
		public static readonly RGBA_Bytes Magenta = new RGBA_Bytes(255, 0, 255, 255);
		public static readonly RGBA_Bytes Yellow = new RGBA_Bytes(255, 255, 0, 255);
		public static readonly RGBA_Bytes YellowGreen = new RGBA_Bytes(154, 205, 50, 255);

		public int Red0To255 { get { return (int)red; } set { red = (byte)value; } }

		public int Green0To255 { get { return (int)green; } set { green = (byte)value; } }

		public int Blue0To255 { get { return (int)blue; } set { blue = (byte)value; } }

		public int Alpha0To255 { get { return (int)alpha; } set { alpha = (byte)value; } }

		public float Red0To1 { get { return red / 255.0f; } set { red = (byte)Math.Max(0, Math.Min((int)(value * 255), 255)); } }

		public float Green0To1 { get { return green / 255.0f; } set { green = (byte)Math.Max(0, Math.Min((int)(value * 255), 255)); } }

		public float Blue0To1 { get { return blue / 255.0f; } set { blue = (byte)Math.Max(0, Math.Min((int)(value * 255), 255)); } }

		public float Alpha0To1 { get { return alpha / 255.0f; } set { alpha = (byte)Math.Max(0, Math.Min((int)(value * 255), 255)); } }

		public RGBA_Bytes(int r_, int g_, int b_)
			: this(r_, g_, b_, base_mask)
		{ }

		public RGBA_Bytes(int r_, int g_, int b_, int a_)
		{
			red = (byte)Math.Min(Math.Max(r_, 0), 255);
			green = (byte)Math.Min(Math.Max(g_, 0), 255);
			blue = (byte)Math.Min(Math.Max(b_, 0), 255);
			alpha = (byte)Math.Min(Math.Max(a_, 0), 255);
		}

		public RGBA_Bytes(double r_, double g_, double b_, double a_)
		{
			red = ((byte)agg_basics.uround(r_ * (double)base_mask));
			green = ((byte)agg_basics.uround(g_ * (double)base_mask));
			blue = ((byte)agg_basics.uround(b_ * (double)base_mask));
			alpha = ((byte)agg_basics.uround(a_ * (double)base_mask));
		}

		public RGBA_Bytes(double r_, double g_, double b_)
		{
			red = ((byte)agg_basics.uround(r_ * (double)base_mask));
			green = ((byte)agg_basics.uround(g_ * (double)base_mask));
			blue = ((byte)agg_basics.uround(b_ * (double)base_mask));
			alpha = (byte)base_mask;
		}

		public RGBA_Bytes(RGBA_Floats c, double a_)
		{
			red = ((byte)agg_basics.uround(c.red * (double)base_mask));
			green = ((byte)agg_basics.uround(c.green * (double)base_mask));
			blue = ((byte)agg_basics.uround(c.blue * (double)base_mask));
			alpha = ((byte)agg_basics.uround(a_ * (double)base_mask));
		}

		public RGBA_Bytes(RGBA_Bytes c)
			: this(c, c.alpha)
		{
		}

		public RGBA_Bytes(RGBA_Bytes c, int a_)
		{
			red = (byte)c.red;
			green = (byte)c.green;
			blue = (byte)c.blue;
			alpha = (byte)a_;
		}

		public RGBA_Bytes(uint fourByteColor)
		{
			red = (byte)((fourByteColor >> 16) & 0xFF);
			green = (byte)((fourByteColor >> 8) & 0xFF);
			blue = (byte)((fourByteColor >> 0) & 0xFF);
			alpha = (byte)((fourByteColor >> 24) & 0xFF);
		}

		public RGBA_Bytes(RGBA_Floats c)
		{
			red = ((byte)agg_basics.uround(c.red * (double)base_mask));
			green = ((byte)agg_basics.uround(c.green * (double)base_mask));
			blue = ((byte)agg_basics.uround(c.blue * (double)base_mask));
			alpha = ((byte)agg_basics.uround(c.alpha * (double)base_mask));
		}

		public static bool operator ==(RGBA_Bytes a, RGBA_Bytes b)
		{
			if (a.red == b.red && a.green == b.green && a.blue == b.blue && a.alpha == b.alpha)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(RGBA_Bytes a, RGBA_Bytes b)
		{
			if (a.red != b.red || a.green != b.green || a.blue != b.blue || a.alpha != b.alpha)
			{
				return true;
			}

			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj.GetType() == typeof(RGBA_Bytes))
			{
				return this == (RGBA_Bytes)obj;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return new { blue, green, red, alpha }.GetHashCode();
		}

		public RGBA_Floats GetAsRGBA_Floats()
		{
			return new RGBA_Floats((float)red / (float)base_mask, (float)green / (float)base_mask, (float)blue / (float)base_mask, (float)alpha / (float)base_mask);
		}

		public RGBA_Bytes GetAsRGBA_Bytes()
		{
			return this;
		}

		public string GetAsHTMLString()
		{
			string html = string.Format("#{0:X2}{1:X2}{2:X2}", red, green, blue);
			return html;
		}

		private void clear()
		{
			red = green = blue = alpha = 0;
		}

		public RGBA_Bytes gradient(RGBA_Bytes c, double k)
		{
			RGBA_Bytes ret = new RGBA_Bytes();
			int ik = agg_basics.uround(k * base_scale);
			ret.Red0To255 = (byte)((int)(Red0To255) + ((((int)(c.Red0To255) - Red0To255) * ik) >> base_shift));
			ret.Green0To255 = (byte)((int)(Green0To255) + ((((int)(c.Green0To255) - Green0To255) * ik) >> base_shift));
			ret.Blue0To255 = (byte)((int)(Blue0To255) + ((((int)(c.Blue0To255) - Blue0To255) * ik) >> base_shift));
			ret.Alpha0To255 = (byte)((int)(Alpha0To255) + ((((int)(c.Alpha0To255) - Alpha0To255) * ik) >> base_shift));
			return ret;
		}

		static public RGBA_Bytes operator +(RGBA_Bytes A, RGBA_Bytes B)
		{
			RGBA_Bytes temp = new RGBA_Bytes();
			temp.red = (byte)((A.red + B.red) > 255 ? 255 : (A.red + B.red));
			temp.green = (byte)((A.green + B.green) > 255 ? 255 : (A.green + B.green));
			temp.blue = (byte)((A.blue + B.blue) > 255 ? 255 : (A.blue + B.blue));
			temp.alpha = (byte)((A.alpha + B.alpha) > 255 ? 255 : (A.alpha + B.alpha));
			return temp;
		}

		static public RGBA_Bytes operator -(RGBA_Bytes A, RGBA_Bytes B)
		{
			RGBA_Bytes temp = new RGBA_Bytes();
			temp.red = (byte)((A.red - B.red) < 0 ? 0 : (A.red - B.red));
			temp.green = (byte)((A.green - B.green) < 0 ? 0 : (A.green - B.green));
			temp.blue = (byte)((A.blue - B.blue) < 0 ? 0 : (A.blue - B.blue));
			temp.alpha = 255;// (byte)((A.m_A - B.m_A) < 0 ? 0 : (A.m_A - B.m_A));
			return temp;
		}

		static public RGBA_Bytes operator *(RGBA_Bytes A, double doubleB)
		{
			float B = (float)doubleB;
			RGBA_Floats temp = new RGBA_Floats();
			temp.red = A.red / 255.0f * B;
			temp.green = A.green / 255.0f * B;
			temp.blue = A.blue / 255.0f * B;
			temp.alpha = A.alpha / 255.0f * B;
			return new RGBA_Bytes(temp);
		}

		public void add(RGBA_Bytes c, int cover)
		{
			int cr, cg, cb, ca;
			if (cover == cover_mask)
			{
				if (c.Alpha0To255 == base_mask)
				{
					this = c;
				}
				else
				{
					cr = Red0To255 + c.Red0To255; Red0To255 = (cr > (int)(base_mask)) ? (int)(base_mask) : cr;
					cg = Green0To255 + c.Green0To255; Green0To255 = (cg > (int)(base_mask)) ? (int)(base_mask) : cg;
					cb = Blue0To255 + c.Blue0To255; Blue0To255 = (cb > (int)(base_mask)) ? (int)(base_mask) : cb;
					ca = Alpha0To255 + c.Alpha0To255; Alpha0To255 = (ca > (int)(base_mask)) ? (int)(base_mask) : ca;
				}
			}
			else
			{
				cr = Red0To255 + ((c.Red0To255 * cover + cover_mask / 2) >> cover_shift);
				cg = Green0To255 + ((c.Green0To255 * cover + cover_mask / 2) >> cover_shift);
				cb = Blue0To255 + ((c.Blue0To255 * cover + cover_mask / 2) >> cover_shift);
				ca = Alpha0To255 + ((c.Alpha0To255 * cover + cover_mask / 2) >> cover_shift);
				Red0To255 = (cr > (int)(base_mask)) ? (int)(base_mask) : cr;
				Green0To255 = (cg > (int)(base_mask)) ? (int)(base_mask) : cg;
				Blue0To255 = (cb > (int)(base_mask)) ? (int)(base_mask) : cb;
				Alpha0To255 = (ca > (int)(base_mask)) ? (int)(base_mask) : ca;
			}
		}

		public void apply_gamma_dir(GammaLookUpTable gamma)
		{
			Red0To255 = gamma.dir((byte)Red0To255);
			Green0To255 = gamma.dir((byte)Green0To255);
			Blue0To255 = gamma.dir((byte)Blue0To255);
		}

		public static IColorType no_color()
		{
			return new RGBA_Bytes(0, 0, 0, 0);
		}

		//-------------------------------------------------------------rgb8_packed
		static public RGBA_Bytes rgb8_packed(int v)
		{
			return new RGBA_Bytes((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
		}

		public RGBA_Bytes Blend(RGBA_Bytes other, double weight)
		{
			RGBA_Bytes result = new RGBA_Bytes(this);
			result = this * (1 - weight) + other * weight;
			return result;
		}
	}
}