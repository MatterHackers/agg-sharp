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

		public static readonly Color Black = new Color(0, 0, 0);
		public static readonly Color Blue = new Color("#0000FF");
		public static readonly Color Crimson = new Color("#DC143C");
		public static readonly Color Cyan = new Color(0, 255, 255);
		public static readonly Color DarkBlue = new Color("#0000A0");
		public static readonly Color DarkGray = new Color(85, 85, 85);
		public static readonly Color FireEngineRed = new Color("#F62817");
		public static readonly Color Gray = new Color(125, 125, 125);
		public static readonly Color Green = new Color(0, 255, 0);
		public static readonly Color Indigo = new Color(75, 0, 130);
		public static readonly Color LightBlue = new Color("#ADD8E6");
		public static readonly Color LightGray = new Color(225, 225, 225);
		public static readonly Color Magenta = new Color(255, 0, 255);
		public static readonly Color Orange = new Color(255, 127, 0);
		public static readonly Color Pink = new Color(255, 192, 203);
		public static readonly Color Red = new Color(255, 0, 0);
		public static readonly Color Transparent = new Color(0, 0, 0, 0);
		public static readonly Color Violet = new Color(143, 0, 255);
		public static readonly Color White = new Color(255, 255, 255);
		public static readonly Color Yellow = new Color(255, 255, 0);
		public static readonly Color YellowGreen = new Color(154, 205, 50);

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

		public ulong GetLongHashCode(ulong hash = 14695981039346656037)
		{
			return agg_basics.ComputeHash(new byte[] { red, green, blue, alpha}, hash);
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
			alpha = (byte)Math.Max(0, Math.Min(255, a_));
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
		public static IColorType WithContrast(this IColorType colorToAdjust, IColorType fixedColor, double minimumRequiredContrast = 3)
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

		public static Color WithAlpha(this Color color, int alpha)
		{
			return new Color(color, alpha);
		}

		public static Color WithAlpha(this Color color, double alpha)
		{
			return new Color(color, (int)Math.Round(255 * alpha));
		}

		public static ColorF AdjustSaturation(this IColorType original, double saturationMultiplier)
		{
			ColorF colorF = original.ToColorF();

			colorF.GetHSL(out double hue0To1, out double saturation0To1, out double lightness0To1);
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

			// If we’re here, it means we have a semi-transparent background
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

		public static ColorF WithLightness(this IColorType original, double lightness)
		{
			ColorF colorF = original.ToColorF();

			colorF.GetHSL(out double hue0To1, out double saturation0To1, out _);

			return ColorF.FromHSL(hue0To1, saturation0To1, lightness);
		}

		public static ColorF AdjustLightness(this IColorType original, double lightnessMultiplier)
		{
			ColorF colorF = original.ToColorF();

			colorF.GetHSL(out double hue0To1, out double saturation0To1, out double lightness0To1);
			lightness0To1 *= lightnessMultiplier;

			return ColorF.FromHSL(hue0To1, saturation0To1, lightness0To1);
		}
	}
}