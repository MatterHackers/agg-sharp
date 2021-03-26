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
using Newtonsoft.Json;

namespace MatterHackers.Agg
{
	public struct ColorF : IColorType
	{
		private const int base_shift = 8;
		private const int base_scale = (int)(1 << base_shift);
		private const int base_mask = base_scale - 1;

		public float red;
		public float green;
		public float blue;
		public float alpha;

		[JsonIgnore]
		public int Red0To255 { get { return (int)agg_basics.uround(Math.Max(0, Math.Min(255, red)) * (float)base_mask); } set { red = (float)value / (float)base_mask; } }

		[JsonIgnore]
		public int Green0To255 { get { return (int)agg_basics.uround(Math.Max(0, Math.Min(255, green)) * (float)base_mask); } set { green = (float)value / (float)base_mask; } }

		[JsonIgnore]
		public int Blue0To255 { get { return (int)agg_basics.uround(Math.Max(0, Math.Min(255, blue)) * (float)base_mask); } set { blue = (float)value / (float)base_mask; } }

		[JsonIgnore]
		public int Alpha0To255 { get { return (int)agg_basics.uround(Math.Max(0, Math.Min(255, alpha)) * (float)base_mask); } set { alpha = (float)value / (float)base_mask; } }

		[JsonIgnore]
		public float Red0To1 { get { return red; } set { red = value; } }

		[JsonIgnore]
		public float Green0To1 { get { return green; } set { green = value; } }

		[JsonIgnore]
		public float Blue0To1 { get { return blue; } set { blue = value; } }

		[JsonIgnore]
		public float Alpha0To1 { get { return alpha; } set { alpha = value; } }

		#region Defined Colors

		public static readonly ColorF White = new ColorF(1, 1, 1, 1);
		public static readonly ColorF Black = new ColorF(0, 0, 0, 1);
		public static readonly ColorF Red = new ColorF(1, 0, 0, 1);
		public static readonly ColorF Green = new ColorF(0, 1, 0, 1);
		public static readonly ColorF Blue = new ColorF(0, 0, 1, 1);
		public static readonly ColorF Cyan = new ColorF(0, 1, 1, 1);
		public static readonly ColorF Magenta = new ColorF(1, 0, 1, 1);
		public static readonly ColorF Yellow = new ColorF(1, 1, 0, 1);

		#endregion Defined Colors

		#region Constructors

		public ColorF(double r_, double g_, double b_)
			: this(r_, g_, b_, 1.0)
		{
		}

		public ColorF(double r_, double g_, double b_, double a_)
		{
			red = (float)r_;
			green = (float)g_;
			blue = (float)b_;
			alpha = (float)a_;
		}

		public ColorF(float r_, float g_, float b_)
			: this(r_, g_, b_, 1.0f)
		{
		}

		public ColorF(float r_, float g_, float b_, float a_)
		{
			red = r_;
			green = g_;
			blue = b_;
			alpha = a_;
		}

		public ColorF(ColorF c)
			: this(c, c.alpha)
		{
		}

		public ColorF(ColorF c, float a_)
		{
			red = c.red;
			green = c.green;
			blue = c.blue;
			alpha = a_;
		}

		public ColorF(float wavelen)
			: this(wavelen, 1.0f)
		{
		}

		public ColorF(float wavelen, float gamma)
		{
			this = from_wavelength(wavelen, gamma);
		}

		public ColorF(Color color)
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
		public static ColorF FromHSL(double hue0To1, double saturation0To1, double lightness0To1, double alpha = 1)
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

			return new ColorF(r, g, b, alpha);
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
		#endregion HSL

		public static bool operator ==(ColorF a, ColorF b)
		{
			if (a.red == b.red && a.green == b.green && a.blue == b.blue && a.alpha == b.alpha)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(ColorF a, ColorF b)
		{
			if (a.red != b.red || a.green != b.green || a.blue != b.blue || a.alpha != b.alpha)
			{
				return true;
			}

			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj.GetType() == typeof(ColorF))
			{
				return this == (ColorF)obj;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return new { blue, green, red, alpha }.GetHashCode();
		}

		public Color ToColor()
		{
			return new Color(Red0To255, Green0To255, Blue0To255, Alpha0To255);
		}

		public ColorF ToColorF()
		{
			return this;
		}

		static public ColorF operator +(ColorF A, ColorF B)
		{
			ColorF temp = new ColorF();
			temp.red = A.red + B.red;
			temp.green = A.green + B.green;
			temp.blue = A.blue + B.blue;
			temp.alpha = A.alpha + B.alpha;
			return temp;
		}

		static public ColorF operator -(ColorF A, ColorF B)
		{
			ColorF temp = new ColorF();
			temp.red = A.red - B.red;
			temp.green = A.green - B.green;
			temp.blue = A.blue - B.blue;
			temp.alpha = A.alpha - B.alpha;
			return temp;
		}

		static public ColorF operator *(ColorF A, ColorF B)
		{
			ColorF temp = new ColorF();
			temp.red = A.red * B.red;
			temp.green = A.green * B.green;
			temp.blue = A.blue * B.blue;
			temp.alpha = A.alpha * B.alpha;
			return temp;
		}

		static public ColorF operator /(ColorF A, ColorF B)
		{
			ColorF temp = new ColorF();
			temp.red = A.red / B.red;
			temp.green = A.green / B.green;
			temp.blue = A.blue / B.blue;
			temp.alpha = A.alpha / B.alpha;
			return temp;
		}

		static public ColorF operator /(ColorF A, float B)
		{
			ColorF temp = new ColorF();
			temp.red = A.red / B;
			temp.green = A.green / B;
			temp.blue = A.blue / B;
			temp.alpha = A.alpha / B;
			return temp;
		}

		static public ColorF operator /(ColorF A, double doubleB)
		{
			float B = (float)doubleB;
			ColorF temp = new ColorF();
			temp.red = A.red / B;
			temp.green = A.green / B;
			temp.blue = A.blue / B;
			temp.alpha = A.alpha / B;
			return temp;
		}

		static public ColorF operator *(ColorF A, float B)
		{
			ColorF temp = new ColorF();
			temp.red = A.red * B;
			temp.green = A.green * B;
			temp.blue = A.blue * B;
			temp.alpha = A.alpha * B;
			return temp;
		}

		static public ColorF operator *(ColorF A, double doubleB)
		{
			float B = (float)doubleB;
			ColorF temp = new ColorF();
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

		public ColorF transparent()
		{
			alpha = 0.0f;
			return this;
		}

		public ColorF opacity(float a_)
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

		public ColorF premultiply()
		{
			red *= alpha;
			green *= alpha;
			blue *= alpha;
			return this;
		}

		public ColorF premultiply(float a_)
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

		public static ColorF ComponentMax(ColorF a, ColorF b)
		{
			ColorF result = a;
			if (result.red < b.red) result.red = b.red;
			if (result.green < b.green) result.green = b.green;
			if (result.blue < b.blue) result.blue = b.blue;
			if (result.alpha < b.alpha) result.alpha = b.alpha;

			return result;
		}

		public ColorF demultiply()
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

		public Color gradient(Color c_8, double k)
		{
			ColorF c = c_8.ToColorF();
			ColorF ret;
			ret.red = (float)(red + (c.red - red) * k);
			ret.green = (float)(green + (c.green - green) * k);
			ret.blue = (float)(blue + (c.blue - blue) * k);
			ret.alpha = (float)(alpha + (c.alpha - alpha) * k);
			return ret.ToColor();
		}

		public static IColorType no_color()
		{
			return (IColorType)new ColorF(0, 0, 0, 0);
		}

		public static ColorF from_wavelength(float wl)
		{
			return from_wavelength(wl, 1.0f);
		}

		public static ColorF from_wavelength(float wl, float gamma)
		{
			ColorF t = new ColorF(0.0f, 0.0f, 0.0f);

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

		public static ColorF rgba_pre(double r, double g, double b)
		{
			return rgba_pre((float)r, (float)g, (float)b, 1.0f);
		}

		public static ColorF rgba_pre(float r, float g, float b)
		{
			return rgba_pre(r, g, b, 1.0f);
		}

		public static ColorF rgba_pre(float r, float g, float b, float a)
		{
			return new ColorF(r, g, b, a).premultiply();
		}

		public static ColorF rgba_pre(double r, double g, double b, double a)
		{
			return new ColorF((float)r, (float)g, (float)b, (float)a).premultiply();
		}

		public static ColorF rgba_pre(ColorF c)
		{
			return new ColorF(c).premultiply();
		}

		public static ColorF rgba_pre(ColorF c, float a)
		{
			return new ColorF(c, a).premultiply();
		}

		public static ColorF GetTweenColor(ColorF Color1, ColorF Color2, double RatioOf2)
		{
			if (RatioOf2 <= 0)
			{
				return new ColorF(Color1);
			}

			if (RatioOf2 >= 1.0)
			{
				return new ColorF(Color2);
			}

			// figure out how much of each color we should be.
			double RatioOf1 = 1.0 - RatioOf2;
			return new ColorF(
				Color1.red * RatioOf1 + Color2.red * RatioOf2,
				Color1.green * RatioOf1 + Color2.green * RatioOf2,
				Color1.blue * RatioOf1 + Color2.blue * RatioOf2);
		}

		public ColorF Blend(ColorF other, double weight)
		{
			ColorF result = new ColorF(this);
			result = this * (1 - weight) + other * weight;
			return result;
		}

		public double SumOfDistances(ColorF other)
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
}