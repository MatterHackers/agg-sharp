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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

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

        public static readonly Color DarkBlue = new Color("#0000A0");
        public static readonly Color DarkGreen = new Color(0, 100, 0);
        public static readonly Color DarkMagenta = new Color(139, 0, 139);
        public static readonly Color DarkRed = new Color(139, 0, 0);
        public static readonly Color DarkYellow = new Color(139, 139, 0);
        public static readonly Color Green = new Color(0, 255, 0);
        public static readonly Color Magenta = new Color(255, 0, 255);
        public static readonly Color Red = new Color(255, 0, 0);
        public static readonly Color Yellow = new Color(255, 255, 0);
        public static readonly Color Black = new Color(0, 0, 0);
        public static readonly Color Blue = new Color("#0000FF");
        public static readonly Color Crimson = new Color("#DC143C");
        public static readonly Color Cyan = new Color(0, 255, 255);
        public static readonly Color DarkCyan = new Color(0, 139, 139);
        public static readonly Color DarkGray = new Color(85, 85, 85);
        public static readonly Color FireEngineRed = new Color("#F62817");
        public static readonly Color Gray = new Color(125, 125, 125);
        public static readonly Color Indigo = new Color(75, 0, 130);
        public static readonly Color LightBlue = new Color("#ADD8E6");
        public static readonly Color LightGray = new Color(225, 225, 225);
        public static readonly Color Orange = new Color(255, 127, 0);
        public static readonly Color Purple = new Color(128, 0, 128);
        public static readonly Color Pink = new Color(255, 192, 203);
        public static readonly Color Transparent = new Color(0, 0, 0, 0);
        public static readonly Color Violet = new Color(143, 0, 255);
        public static readonly Color White = new Color(255, 255, 255);
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
                switch (value.Trim().ToLower())
                {
                    case "blue":
                        this = Blue;
                        return;

                    case "green":
                        this = Green;
                        return;

                    case "red":
                        this = Red;
                        return;

                    case "black":
                        this = Black;
                        return;

                    case "gray":
                        this = Gray;
                        return;

                    case "orange":
                        this = Orange;
                        return;

                    case "purple":
                        this = Purple;
                        return;

                    case "yellow":
                        this = Yellow;
                        return;

                    default:
                        break;
                }

                switch (value.Length)
                {
                    case 4: // #CCC, single char rgb
                    case 5: // also has alpha
                        red = (byte)Convert.ToInt32(value.Substring(1, 1) + value.Substring(1, 1), 16);
                        green = (byte)Convert.ToInt32(value.Substring(2, 1) + value.Substring(2, 1), 16);
                        blue = (byte)Convert.ToInt32(value.Substring(3, 1) + value.Substring(3, 1), 16);
                        if (value.Length == 5)
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
            return Util.ComputeHash(new byte[] { red, green, blue, alpha }, hash);
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
            red = ((byte)Util.uround(r_ * (double)base_mask));
            green = ((byte)Util.uround(g_ * (double)base_mask));
            blue = ((byte)Util.uround(b_ * (double)base_mask));
            alpha = ((byte)Util.uround(a_ * (double)base_mask));
        }

        public Color(double r_, double g_, double b_)
        {
            red = ((byte)Util.uround(r_ * (double)base_mask));
            green = ((byte)Util.uround(g_ * (double)base_mask));
            blue = ((byte)Util.uround(b_ * (double)base_mask));
            alpha = (byte)base_mask;
        }

        public Color(ColorF c, double a_)
        {
            red = ((byte)Util.uround(c.red * (double)base_mask));
            green = ((byte)Util.uround(c.green * (double)base_mask));
            blue = ((byte)Util.uround(c.blue * (double)base_mask));
            alpha = ((byte)Util.uround(a_ * (double)base_mask));
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
            red = ((byte)Util.uround(c.red * (double)base_mask));
            green = ((byte)Util.uround(c.green * (double)base_mask));
            blue = ((byte)Util.uround(c.blue * (double)base_mask));
            alpha = ((byte)Util.uround(c.alpha * (double)base_mask));
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
            int ik = Util.uround(k * base_scale);
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

        public string ColorToName(bool simpleNames = true)
        {
            var colorDictionary = SmallColorDictionary;
            if(!simpleNames)
            {
                colorDictionary = DetailedColorDictionary;
            }

            string closestColorName = null;
            double closestColorDistance = double.MaxValue;

            foreach (var color in colorDictionary)
            {
                double distance = this.CalculateColorDistance(color.Value);
                if (distance < closestColorDistance)
                {
                    closestColorName = color.Key;
                    closestColorDistance = distance;
                }
            }

            return closestColorName;
        }

        private static readonly Dictionary<string, Color> SmallColorDictionary = new Dictionary<string, Color>
        {
            { "Red", new Color(255, 0, 0) },
            { "Green", new Color(0, 255, 0) },
            { "Blue", new Color(0, 0, 255) },
            { "Aqua", new Color(0, 255, 255) },
            { "Black", new Color(0, 0, 0) },
            { "Fuchsia", new Color(255, 0, 255) },
            { "Gray", new Color(128, 128, 128) },
            { "Lime", new Color(0, 255, 0) },
            { "Maroon", new Color(128, 0, 0) },
            { "Navy", new Color(0, 0, 128) },
            { "Olive", new Color(128, 128, 0) },
            { "Purple", new Color(128, 0, 128) },
            { "Silver", new Color(192, 192, 192) },
            { "Teal", new Color(0, 128, 128) },
            { "White", new Color(255, 255, 255) },
            { "Yellow", new Color(255, 255, 0) },
        };

        private static readonly Dictionary<string, Color> DetailedColorDictionary = new Dictionary<string, Color>
        {
            { "Red", new Color(255, 0, 0) },
            { "Green", new Color(0, 255, 0) },
            { "Blue", new Color(0, 0, 255) },
            { "Aqua", new Color(0, 255, 255) },
            { "Black", new Color(0, 0, 0) },
            { "Fuchsia", new Color(255, 0, 255) },
            { "Gray", new Color(128, 128, 128) },
            { "Lime", new Color(0, 255, 0) },
            { "Maroon", new Color(128, 0, 0) },
            { "Navy", new Color(0, 0, 128) },
            { "Olive", new Color(128, 128, 0) },
            { "Purple", new Color(128, 0, 128) },
            { "Silver", new Color(192, 192, 192) },
            { "Teal", new Color(0, 128, 128) },
            { "White", new Color(255, 255, 255) },
            { "Yellow", new Color(255, 255, 0) },
            { "Orange", new Color(255, 165, 0) },
            { "Pink", new Color(255, 192, 203) },
            { "Cyan", new Color(0, 255, 255) },
            { "Magenta", new Color(255, 0, 255) },
            { "Gold", new Color(255, 215, 0) },
            { "Chocolate", new Color(210, 105, 30) },
            { "Coral", new Color(255, 127, 80) },
            { "DarkBlue", new Color(0, 0, 139) },
            { "DarkCyan", new Color(0, 139, 139) },
            { "DarkGray", new Color(169, 169, 169) },
            { "DarkGreen", new Color(0, 100, 0) },
            { "DarkMagenta", new Color(139, 0, 139) },
            { "DarkOliveGreen", new Color(85, 107, 47) },
            { "DarkOrange", new Color(255, 140, 0) },
            { "DarkOrchid", new Color(153, 50, 204) },
            { "DarkRed", new Color(139, 0, 0) },
            { "DarkSalmon", new Color(233, 150, 122) },
            { "DarkSeaGreen", new Color(143, 188, 139) },
            { "DarkSlateBlue", new Color(72, 61, 139) },
            { "DarkSlateGray", new Color(47, 79, 79) },
            { "DarkTurquoise", new Color(0, 206, 209) },
            { "DarkViolet", new Color(148, 0, 211) },
            { "DeepPink", new Color(255, 20, 147) },
            { "DeepSkyBlue", new Color(0, 191, 255) },
            { "DimGray", new Color(105, 105, 105) },
            { "DodgerBlue", new Color(30, 144, 255) },
            { "Firebrick", new Color(178, 34, 34) },
            { "ForestGreen", new Color(34, 139, 34) },
            { "Gainsboro", new Color(220, 220, 220) },
            { "HotPink", new Color(255, 105, 180) },
            { "IndianRed", new Color(205, 92, 92) },
            { "Indigo", new Color(75, 0, 130) },
            { "Khaki", new Color(240, 230, 140) },
            { "Lavender", new Color(230, 230, 250) },
            { "LawnGreen", new Color(124, 252, 0) },
            { "LightBlue", new Color(173, 216, 230) },
            { "LightCoral", new Color(240, 128, 128) },
            { "LightCyan", new Color(224, 255, 255) },
            { "LightGoldenrodYellow", new Color(250, 250, 210) },
            { "LightGray", new Color(211, 211, 211) },
            { "LightGreen", new Color(144, 238, 144) },
            { "LightPink", new Color(255, 182, 193) },
            { "LightSalmon", new Color(255, 160, 122) },
            { "LightSeaGreen", new Color(32, 178, 170) },
            { "LightSkyBlue", new Color(135, 206, 250) },
            { "LightSlateGray", new Color(119, 136, 153) },
            { "LightSteelBlue", new Color(176, 196, 222) },
            { "LightYellow", new Color(255, 255, 224) },
            { "LimeGreen", new Color(50, 205, 50) },
        };


        /// <summary>
        /// Calculates the Euclidean distance between two colors in the LAB color space.
        /// </summary>
        /// <param name="color2">The second color to compare.</param>
        /// <returns>The distance between the two colors in the LAB color space.</returns>
        public double CalculateColorDistance(Color color2)
        {
            double[] lab1 = RgbToLab(this);
            double[] lab2 = RgbToLab(color2);

            double lDiff = lab1[0] - lab2[0];
            double aDiff = lab1[1] - lab2[1];
            double bDiff = lab1[2] - lab2[2];

            return Math.Sqrt(lDiff * lDiff + aDiff * aDiff + bDiff * bDiff);
        }

        /// <summary>
        /// Blends two colors in the HSL color space using the specified ratio.
        /// </summary>
        /// <param name="color2">The second color to blend with the current color.</param>
        /// <param name="ratioB">The ratio of the second color in the blended result (0.0 to 1.0).</param>
        /// <returns>A new Color object representing the blended color in the HSL color space.</returns>
        public Color BlendHsl(Color color2, double ratioOf2)
        {
            double aH, aS, aL;
            new ColorF(this).GetHSL(out aH, out aS, out aL);
            double bH, bS, bL;
            new ColorF(color2).GetHSL(out bH, out bS, out bL);

            return ColorF.FromHSL(
                aH * (1 - ratioOf2) + bH * ratioOf2,
                aS * (1 - ratioOf2) + bS * ratioOf2,
                aL * (1 - ratioOf2) + bL * ratioOf2).ToColor();
        }

        private static double[] XyzToRgb(double[] xyz)
        {
            // xyz is normalized to [0,1]
            var x = xyz[0] / 100;
            var y = xyz[1] / 100;
            var z = xyz[2] / 100;
            // xyz is multiplied by the reverse transformation matrix to linear rgb
            var invR = 3.2406254773200533 * x - 1.5372079722103187 * y -
              0.4986285986982479 * z;
            var invG = -0.9689307147293197 * x + 1.8757560608852415 * y +
              0.041517523842953964 * z;
            var invB = 0.055710120445510616 * x + -0.2040210505984867 * y +
              1.0569959422543882 * z;
            // Linear rgb must be gamma corrected to normalized srgb. Gamma correction
            // is linear for values <= 0.0031308 to avoid infinite log slope near zero
            double compand(double c)
            {
                return c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
            }

            var cR = compand(invR);
            var cG = compand(invG);
            var cB = compand(invB);
            // srgb is scaled to [0,255]
            // Add zero to prevent signed zeros (force 0 rather than -0)
            return new double[]
            {
                Math.Round(cR * 255) + 0,
                Math.Round(cG * 255) + 0,
                Math.Round(cB * 255) + 0
            };
        }

        private static double[] LabToXyz(double[] lab)
        {
            /** d65 standard illuminant in XYZ */
            double[] d65 = { 95.05, 100, 108.9 };

            var L = lab[0];
            var a = lab[1];
            var b = lab[2];
            var eps = 216 / 24389;
            var kap = 24389 / 27;
            var fY = (L + 16) / 116;
            var fZ = (fY - b / 200);
            var fX = a / 500 + fY;
            var xR = Math.Pow(fX, 3) > eps ? Math.Pow(fX, 3) : (116 * fX - 16) / kap;
            var yR = L > kap * eps ? Math.Pow((L + 16) / 116, 3) : L / kap;
            var zR = Math.Pow(fZ, 3) > eps ? Math.Pow(fZ, 3) : (116 * fZ - 16) / kap;
            // Add zero to prevent signed zeros (force 0 rather than -0)
            return new double[]
            {
                xR * d65[0] + 0,
                yR * d65[1] + 0,
                zR * d65[2] + 0
            };
        }

        private static Color LabToRgb(double[] lab)
        {
            var xyz = LabToXyz(lab);
            var rgb = XyzToRgb(xyz);
            return new Color((int)rgb[0], (int)rgb[1], (int)rgb[2]);
        }

        public static Color FromLab(double l, double a, double b)
        {
            return LabToRgb(new double[] { l, a, b });
        }

        public static double[] RgbToLab(Color color)
        {
            // First, convert RGB to XYZ
            double[] xyz = RgbToXyz(color);

            // Then, convert XYZ to LAB
            double[] lab = XyzToLab(xyz[0], xyz[1], xyz[2]);

            return lab;
        }

        public static double[] XyzToLab(double x, double y, double z)
        {
            double ref_X = 95.047;
            double ref_Y = 100.000;
            double ref_Z = 108.883;

            double var_X = x / ref_X;
            double var_Y = y / ref_Y;
            double var_Z = z / ref_Z;

            if (var_X > 0.008856) var_X = Math.Pow(var_X, (1.0 / 3.0));
            else var_X = (7.787 * var_X) + (16.0 / 116.0);
            if (var_Y > 0.008856) var_Y = Math.Pow(var_Y, (1.0 / 3.0));
            else var_Y = (7.787 * var_Y) + (16.0 / 116.0);
            if (var_Z > 0.008856) var_Z = Math.Pow(var_Z, (1.0 / 3.0));
            else var_Z = (7.787 * var_Z) + (16.0 / 116.0);

            double l = (116.0 * var_Y) - 16.0;
            double a = 500.0 * (var_X - var_Y);
            double b = 200.0 * (var_Y - var_Z);

            return new double[] { l, a, b };
        }

        private static double[] RgbToXyz(Color color)
        {
            double var_R = (color.red / 255.0);
            double var_G = (color.green / 255.0);
            double var_B = (color.blue / 255.0);

            if (var_R > 0.04045) var_R = Math.Pow((var_R + 0.055) / 1.055, 2.4);
            else var_R = var_R / 12.92;
            if (var_G > 0.04045) var_G = Math.Pow((var_G + 0.055) / 1.055, 2.4);
            else var_G = var_G / 12.92;
            if (var_B > 0.04045) var_B = Math.Pow((var_B + 0.055) / 1.055, 2.4);
            else var_B = var_B / 12.92;

            var_R = var_R * 100.0;
            var_G = var_G * 100.0;
            var_B = var_B * 100.0;

            double x = var_R * 0.4124 + var_G * 0.3576 + var_B * 0.1805;
            double y = var_R * 0.2126 + var_G * 0.7152 + var_B * 0.0722;
            double z = var_R * 0.0193 + var_G * 0.1192 + var_B * 0.9505;

            return new double[] { x, y, z };
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
            var overlaid = thisA;

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