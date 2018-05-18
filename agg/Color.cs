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
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

	[TypeConverter(typeof(ColorTypeConverter))]
	[JsonConverter(typeof(ColorJsonConverter))]
	public struct Color : IColorType
	{
		public const int cover_shift = 8;
		public const int cover_size = 1 << cover_shift;  //----cover_size
		public const int cover_mask = cover_size - 1;    //----cover_mask
		//public const int cover_none  = 0,                 //----cover_none
		//public const int cover_full  = cover_mask         //----cover_full

		public const int base_shift = 8;
		public const int base_scale = (int)(1 << base_shift);
		public const int base_mask = base_scale - 1;

		[JsonIgnore]
		public byte blue;
		[JsonIgnore]
		public byte green;
		[JsonIgnore]
		public byte red;
		[JsonIgnore]
		public byte alpha;

		public static readonly Color Transparent = new Color(0, 0, 0, 0);
		public static readonly Color White = new Color(255, 255, 255, 255);
		public static readonly Color LightGray = new Color(225, 225, 225, 255);
		public static readonly Color Gray = new Color(125, 125, 125, 255);
		public static readonly Color DarkGray = new Color(85, 85, 85, 255);
		public static readonly Color Black = new Color(0, 0, 0, 255);
		public static readonly Color Red = new Color(255, 0, 0, 255);
		public static readonly Color FireEngineRed = new Color("#F62817");
		public static readonly Color Orange = new Color(255, 127, 0, 255);
		public static readonly Color Pink = new Color(255, 192, 203, 255);
		public static readonly Color Green = new Color(0, 255, 0, 255);
		public static readonly Color Blue = new Color("#0000FF");
		public static readonly Color DargBlue = new Color("#0000A0");
		public static readonly Color LightBlue = new Color("#ADD8E6");
		public static readonly Color Indigo = new Color(75, 0, 130, 255);
		public static readonly Color Violet = new Color(143, 0, 255, 255);
		public static readonly Color Cyan = new Color(0, 255, 255, 255);
		public static readonly Color Magenta = new Color(255, 0, 255, 255);
		public static readonly Color Yellow = new Color(255, 255, 0, 255);
		public static readonly Color YellowGreen = new Color(154, 205, 50, 255);

		[JsonIgnore]
		public int Red0To255 { get { return (int)red; } set { red = (byte)value; } }

		[JsonIgnore]
		public int Green0To255 { get { return (int)green; } set { green = (byte)value; } }

		[JsonIgnore]
		public int Blue0To255 { get { return (int)blue; } set { blue = (byte)value; } }

		[JsonIgnore]
		public int Alpha0To255 { get { return (int)alpha; } set { alpha = (byte)value; } }

		[JsonIgnore]
		public float Red0To1 { get { return red / 255.0f; } set { red = (byte)Math.Max(0, Math.Min((int)(value * 255), 255)); } }

		[JsonIgnore]
		public float Green0To1 { get { return green / 255.0f; } set { green = (byte)Math.Max(0, Math.Min((int)(value * 255), 255)); } }

		[JsonIgnore]
		public float Blue0To1 { get { return blue / 255.0f; } set { blue = (byte)Math.Max(0, Math.Min((int)(value * 255), 255)); } }

		[JsonIgnore]
		public float Alpha0To1 { get { return alpha / 255.0f; } set { alpha = (byte)Math.Max(0, Math.Min((int)(value * 255), 255)); } }

		// serialize
		public string Html
		{
			get
			{
				return $"#{red:X2}{green:X2}{blue:X2}{alpha:X2}";
			}

			set
			{
				switch(value.Length)
				{
					case 4: // #CCC, single char rgb
					case 5: // also has alpha
						red = (byte)Convert.ToInt32(value.Substring(1, 1) + value.Substring(1, 1), 16);
						green = (byte)Convert.ToInt32(value.Substring(2, 1) + value.Substring(2, 1), 16);
						blue = (byte)Convert.ToInt32(value.Substring(3,1) + value.Substring(3, 1), 16);
						if(value.Length == 5)
						{
							alpha = (byte)Convert.ToInt32(value.Substring(4, 1) + value.Substring(4, 1), 16);
						}
						else
						{
							alpha = 255;
						}
						break;

					case 7: // #ACACAC, two char rgb
					case 9: // also has alpha
						red = (byte)Convert.ToInt32(value.Substring(1, 2), 16);
						green = (byte)Convert.ToInt32(value.Substring(3, 2), 16);
						blue = (byte)Convert.ToInt32(value.Substring(5, 2), 16);
						if (value.Length == 9)
						{
							alpha = (byte)Convert.ToInt32(value.Substring(7, 2), 16);
						}
						else
						{
							alpha = 255;
						}
						break;

					default:
						break; // don't know what it is, do nothing
				}
			}
		}

		public Color(string htmlString)
			: this()
		{
			Html = htmlString;
		}

		public Color(int r_, int g_, int b_)
			: this(r_, g_, b_, base_mask)
		{ }

		public Color(int r_, int g_, int b_, int a_)
		{
			red = (byte)Math.Min(Math.Max(r_, 0), 255);
			green = (byte)Math.Min(Math.Max(g_, 0), 255);
			blue = (byte)Math.Min(Math.Max(b_, 0), 255);
			alpha = (byte)Math.Min(Math.Max(a_, 0), 255);
		}

		public Color(double r_, double g_, double b_, double a_)
		{
			red = ((byte)agg_basics.uround(r_ * (double)base_mask));
			green = ((byte)agg_basics.uround(g_ * (double)base_mask));
			blue = ((byte)agg_basics.uround(b_ * (double)base_mask));
			alpha = ((byte)agg_basics.uround(a_ * (double)base_mask));
		}

		public Color(double r_, double g_, double b_)
		{
			red = ((byte)agg_basics.uround(r_ * (double)base_mask));
			green = ((byte)agg_basics.uround(g_ * (double)base_mask));
			blue = ((byte)agg_basics.uround(b_ * (double)base_mask));
			alpha = (byte)base_mask;
		}

		public Color(ColorF c, double a_)
		{
			red = ((byte)agg_basics.uround(c.red * (double)base_mask));
			green = ((byte)agg_basics.uround(c.green * (double)base_mask));
			blue = ((byte)agg_basics.uround(c.blue * (double)base_mask));
			alpha = ((byte)agg_basics.uround(a_ * (double)base_mask));
		}

		public Color(Color c)
			: this(c, c.alpha)
		{
		}

		public Color(Color c, int a_)
		{
			red = (byte)c.red;
			green = (byte)c.green;
			blue = (byte)c.blue;
			alpha = (byte)a_;
		}

		public Color(uint fourByteColor)
		{
			red = (byte)((fourByteColor >> 16) & 0xFF);
			green = (byte)((fourByteColor >> 8) & 0xFF);
			blue = (byte)((fourByteColor >> 0) & 0xFF);
			alpha = (byte)((fourByteColor >> 24) & 0xFF);
		}

		public Color(ColorF c)
		{
			red = ((byte)agg_basics.uround(c.red * (double)base_mask));
			green = ((byte)agg_basics.uround(c.green * (double)base_mask));
			blue = ((byte)agg_basics.uround(c.blue * (double)base_mask));
			alpha = ((byte)agg_basics.uround(c.alpha * (double)base_mask));
		}

		public static bool operator ==(Color a, Color b)
		{
			if (a.red == b.red && a.green == b.green && a.blue == b.blue && a.alpha == b.alpha)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(Color a, Color b)
		{
			if (a.red != b.red || a.green != b.green || a.blue != b.blue || a.alpha != b.alpha)
			{
				return true;
			}

			return false;
		}

		public static Color FromHSL(double hue0To1, double saturation0To1, double lightness0To1, double alpha = 1)
		{
			return ColorF.FromHSL(hue0To1, saturation0To1, lightness0To1, alpha).ToColor();
		}

		public override string ToString()
		{
			return GetAsHTMLString();
		}

		public override bool Equals(object obj)
		{
			if (obj.GetType() == typeof(Color))
			{
				return this == (Color)obj;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return new { blue, green, red, alpha }.GetHashCode();
		}

		public ColorF ToColorF()
		{
			return new ColorF((float)red / (float)base_mask, (float)green / (float)base_mask, (float)blue / (float)base_mask, (float)alpha / (float)base_mask);
		}

		public Color ToColor()
		{
			return this;
		}

		public string GetAsHTMLString()
		{
			if (alpha == 255)
			{
				return $"#{red:X2}{green:X2}{blue:X2}";
			}
			else
			{
				return $"#{red:X2}{green:X2}{blue:X2}{alpha:X2}";
			}
		}

		private void clear()
		{
			red = green = blue = alpha = 0;
		}

		public Color gradient(Color c, double k)
		{
			Color ret = new Color();
			int ik = agg_basics.uround(k * base_scale);
			ret.Red0To255 = (byte)((int)(Red0To255) + ((((int)(c.Red0To255) - Red0To255) * ik) >> base_shift));
			ret.Green0To255 = (byte)((int)(Green0To255) + ((((int)(c.Green0To255) - Green0To255) * ik) >> base_shift));
			ret.Blue0To255 = (byte)((int)(Blue0To255) + ((((int)(c.Blue0To255) - Blue0To255) * ik) >> base_shift));
			ret.Alpha0To255 = (byte)((int)(Alpha0To255) + ((((int)(c.Alpha0To255) - Alpha0To255) * ik) >> base_shift));
			return ret;
		}

		static public Color operator +(Color A, Color B)
		{
			Color temp = new Color();
			temp.red = (byte)((A.red + B.red) > 255 ? 255 : (A.red + B.red));
			temp.green = (byte)((A.green + B.green) > 255 ? 255 : (A.green + B.green));
			temp.blue = (byte)((A.blue + B.blue) > 255 ? 255 : (A.blue + B.blue));
			temp.alpha = (byte)((A.alpha + B.alpha) > 255 ? 255 : (A.alpha + B.alpha));
			return temp;
		}

		static public Color operator -(Color A, Color B)
		{
			Color temp = new Color();
			temp.red = (byte)((A.red - B.red) < 0 ? 0 : (A.red - B.red));
			temp.green = (byte)((A.green - B.green) < 0 ? 0 : (A.green - B.green));
			temp.blue = (byte)((A.blue - B.blue) < 0 ? 0 : (A.blue - B.blue));
			temp.alpha = 255;// (byte)((A.m_A - B.m_A) < 0 ? 0 : (A.m_A - B.m_A));
			return temp;
		}

		static public Color operator *(Color A, double doubleB)
		{
			float B = (float)doubleB;
			ColorF temp = new ColorF();
			temp.red = A.red / 255.0f * B;
			temp.green = A.green / 255.0f * B;
			temp.blue = A.blue / 255.0f * B;
			temp.alpha = A.alpha / 255.0f * B;
			return new Color(temp);
		}

		public void add(Color c, int cover)
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
			return new Color(0, 0, 0, 0);
		}

		//-------------------------------------------------------------rgb8_packed
		static public Color rgb8_packed(int v)
		{
			return new Color((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
		}

		public Color Blend(Color other, double weight)
		{
			Color result = new Color(this);
			result = this * (1 - weight) + other * weight;
			return result;
		}

		public Color BlendHsl(Color b, double rationB)
		{
			double aH, aS, aL;
			new ColorF(this).GetHSL(out aH, out aS, out aL);
			double bH, bS, bL;
			new ColorF(b).GetHSL(out bH, out bS, out bL);

			return ColorF.FromHSL(
				aH * (1 - rationB) + bH * rationB,
				aS * (1 - rationB) + bS * rationB,
				aL * (1 - rationB) + bL * rationB).ToColor();
		}
	}

	public class ColorTypeConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			string stringValue = value as string;

			if (string.IsNullOrEmpty(stringValue))
			{
				return Color.Transparent;
			}
			else if (stringValue.Contains("#"))
			{
				return new Color(stringValue);
			}

			return base.ConvertFrom(context, culture, value);
		}
	}

	public class ColorJsonConverter : JsonConverter
	{
		public override bool CanWrite => false;

		public override bool CanRead => true;

		public override bool CanConvert(Type objectType)
		{
			return true;
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			// Use raw value if string and starts with #
			if (reader.Value is string itemValue
				&& itemValue.StartsWith("#"))
			{
				return new Color(itemValue);
			}

			// Use .html if applicable
			if (JObject.Load(reader)?["Html"] is JToken jtoken
				&& jtoken.Value<string>() is string html)
			{
				return new Color(html);
			}

			return Color.Transparent;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			// Should never get invoked due to CanWrite => false
			throw new NotImplementedException();
		}
	}

	public static class ColorExtensionMethods
	{
		public static IColorType AdjustContrast(this IColorType colorToAdjust, IColorType fixedColor, double minimumRequiredContrast = 3)
		{
			var contrast = colorToAdjust.Contrast(fixedColor);
			int tries = 0;
			while (contrast < minimumRequiredContrast
				&& tries++ < 30)
			{
				if (fixedColor.Luminance0To1() < .5)
				{
					colorToAdjust = colorToAdjust.AdjustLightness(1.05).ToColor();
				}
				else
				{
					colorToAdjust = colorToAdjust.AdjustLightness(.95).ToColor();
				}
				contrast = colorToAdjust.Contrast(fixedColor);
			}

			return colorToAdjust;
		}

		public static IColorType AdjustSaturation(this IColorType original, double saturationMultiplier)
		{
			double hue0To1;
			double saturation0To1;
			double lightness0To1;

			ColorF colorF = original is ColorF ? (ColorF)original : original.ToColorF();

			colorF.GetHSL(out hue0To1, out saturation0To1, out lightness0To1);
			saturation0To1 *= saturationMultiplier;

			return ColorF.FromHSL(hue0To1, saturation0To1, lightness0To1);
		}

		public static double Luminance0To1(this IColorType color)
		{
			double R = 0, G = 0, B = 0;
			if (color.Red0To1 <= 0.03928)
			{
				R = color.Red0To1 / 12.92;
			}
			else
			{
				R = Math.Pow(((color.Red0To1 + 0.055) / 1.055), 2.4);
			}

			if (color.Green0To1 <= 0.03928)
			{
				G = color.Green0To1 / 12.92;
			}
			else
			{
				G = Math.Pow(((color.Green0To1 + 0.055) / 1.055), 2.4);
			}

			if (color.Blue0To1 <= 0.03928)
			{
				B = color.Blue0To1 / 12.92;
			}
			else
			{
				B = Math.Pow(((color.Blue0To1 + 0.055) / 1.055), 2.4);
			}
			return 0.2126 * R + 0.7152 * G + 0.0722 * B;
		}

		// Overlay a color over another
		public static IColorType OverlayOn(this IColorType thisA, IColorType color)
		{
			var overlaid =  thisA;

			var alpha = thisA.Alpha0To1;

			if (alpha >= 1)
			{
				return overlaid;
			}

			overlaid.Red0To1 = overlaid.Red0To1 * alpha + color.Red0To1 * color.Alpha0To1 * (1 - alpha);
			overlaid.Green0To1 = overlaid.Green0To1 * alpha + color.Green0To1 * color.Alpha0To1 * (1 - alpha);
			overlaid.Blue0To1 = overlaid.Blue0To1 * alpha + color.Blue0To1 * color.Alpha0To1 * (1 - alpha);

			overlaid.Alpha0To1 = alpha + color.Alpha0To1 * (1 - alpha);

			return overlaid;
		}

		// Contrast ratios can range from 1 to 21 (commonly written 1:1 to 21:1).
		public static double Contrast(this IColorType thisA, IColorType color)
		{
			// Formula: http://www.w3.org/TR/2008/REC-WCAG20-20081211/#contrast-ratiodef
			var alpha = thisA.Alpha0To1;

			if (alpha >= 1)
			{
				if (color.Alpha0To1 < 1)
				{
					color = color.OverlayOn(thisA);
				}

				var l1 = thisA.Luminance0To1() + .05;
				var l2 = color.Luminance0To1() + .05;
				var ratio = l1 / l2;

				if (l2 > l1)
				{
					ratio = 1 / ratio;
				}

				ratio = Math.Round(ratio, 1);

				return ratio;
			}

			// If we�re here, it means we have a semi-transparent background
			// The text color may or may not be semi-transparent, but that doesn't matter
			var onBlack = thisA.OverlayOn(Color.Black);
			var onWhite = thisA.OverlayOn(Color.White);
			var contrastOnBlack = onBlack.Contrast(color);
			var contrastOnWhite = onWhite.Contrast(color);

			var max = Math.Max(contrastOnBlack, contrastOnWhite);

			var min = 1.0;
			if (onBlack.Luminance0To1() > color.Luminance0To1())
			{
				min = contrastOnBlack;
			}
			else if (onWhite.Luminance0To1() < color.Luminance0To1())
			{
				min = contrastOnWhite;
			}
			var error = Math.Round((max - min) / 2, 2);
			var farthest = contrastOnWhite == max ? Color.White : Color.Black;

			return Math.Round((min + max) / 2, 2);
		}

		public static IColorType SetLightness(this IColorType original, double lightness)
		{
			double hue0To1;
			double saturation0To1;
			double lightness0To1;

			ColorF colorF = original is ColorF ? (ColorF)original : original.ToColorF();

			colorF.GetHSL(out hue0To1, out saturation0To1, out lightness0To1);

			return ColorF.FromHSL(hue0To1, saturation0To1, lightness);
		}

		public static IColorType AdjustLightness(this IColorType original, double lightnessMultiplier)
		{
			double hue0To1;
			double saturation0To1;
			double lightness0To1;

			ColorF colorF = original is ColorF ? (ColorF)original : original.ToColorF();

			colorF.GetHSL(out hue0To1, out saturation0To1, out lightness0To1);
			lightness0To1 *= lightnessMultiplier;

			return ColorF.FromHSL(hue0To1, saturation0To1, lightness0To1);
		}
	}
}