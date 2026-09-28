//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2026 Lars Brubaker
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------

using System;

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// C++ <c>comp_op_adaptor_rgba&lt;rgba32, order_bgra&gt;</c>: the SVG compositing operators of
	/// agg_pixfmt_rgba.h for float (AGG_BGRA128) pixels - a straight source composited into a premultiplied float
	/// BGRA buffer. The float twin of <see cref="BlenderCompOpBGRA"/>, with the same two C++ bug fixes (SrcAtop's
	/// blue, ColorBurn under a black source), which the reference renderer's patched agg_pixfmt_rgba.h shares.
	/// </summary>
	/// <remarks>
	/// Every step keeps C++'s types: the premultiply and SrcOver's blender_rgba_pre are float arithmetic
	/// (<see cref="RgbaFloatMath"/>), the other operators work in doubles and narrow back to float.
	/// </remarks>
	public static class BlenderCompOpBGRAFloat
	{
		/// <summary>
		/// C++ <c>comp_op_adaptor_rgba::blend_pix</c> for rgba32: premultiplies the straight
		/// <paramref name="color"/> in float and composites it into the premultiplied float pixel at
		/// <paramref name="offset"/> with <paramref name="op"/>, scaled by <paramref name="cover"/> (0 to 255).
		/// </summary>
		public static void BlendPix(CompOp op, float[] buffer, int offset, ColorF color, int cover)
		{
			float a = color.alpha;
			BlendPremultiplied(op, buffer, offset, RgbaFloatMath.Multiply(color.red, a), RgbaFloatMath.Multiply(color.green, a), RgbaFloatMath.Multiply(color.blue, a), a, cover);
		}

		private static void BlendPremultiplied(CompOp op, float[] p, int offset, float r, float g, float b, float a, int cover)
		{
			int ri = offset + ImageBuffer.OrderR;
			int gi = offset + ImageBuffer.OrderG;
			int bi = offset + ImageBuffer.OrderB;
			int ai = offset + ImageBuffer.OrderA;

			switch (op)
			{
				case CompOp.Clear:
					if (cover >= 255)
					{
						p[ri] = p[gi] = p[bi] = p[ai] = 0;
					}
					else if (cover > 0)
					{
						Set(p, offset, GetPixel(p, offset, 255 - cover));
					}

					break;

				case CompOp.Src:
					if (cover >= 255)
					{
						p[ri] = r;
						p[gi] = g;
						p[bi] = b;
						p[ai] = a;
					}
					else
					{
						Rgba s = Get(r, g, b, a, cover);
						Rgba d = GetPixel(p, offset, 255 - cover);
						d.R += s.R;
						d.G += s.G;
						d.B += s.B;
						d.A += s.A;
						Set(p, offset, d);
					}

					break;

				case CompOp.Dst:
					break;

				case CompOp.SrcOver:
					{
						// C++ uses blender_rgba_pre here: rgba32 mult_cover, then prelerp, all in float.
						float ca = RgbaFloatMath.MultCover(a, cover);
						p[ri] = RgbaFloatMath.Prelerp(p[ri], RgbaFloatMath.MultCover(r, cover), ca);
						p[gi] = RgbaFloatMath.Prelerp(p[gi], RgbaFloatMath.MultCover(g, cover), ca);
						p[bi] = RgbaFloatMath.Prelerp(p[bi], RgbaFloatMath.MultCover(b, cover), ca);
						p[ai] = RgbaFloatMath.Prelerp(p[ai], ca, ca);
					}

					break;

				case CompOp.DstOver:
					{
						Rgba s = Get(r, g, b, a, cover);
						Rgba d = GetPixel(p, offset, 255);
						double d1a = 1 - d.A;
						d.R += s.R * d1a;
						d.G += s.G * d1a;
						d.B += s.B * d1a;
						d.A += s.A * d1a;
						Set(p, offset, d);
					}

					break;

				case CompOp.SrcIn:
					{
						double da = (double)p[ai];
						if (da > 0)
						{
							Rgba s = Get(r, g, b, a, cover);
							Rgba d = GetPixel(p, offset, 255 - cover);
							d.R += s.R * da;
							d.G += s.G * da;
							d.B += s.B * da;
							d.A += s.A * da;
							Set(p, offset, d);
						}
					}

					break;

				case CompOp.DstIn:
					{
						double sa = (double)a;
						Rgba d = GetPixel(p, offset, 255 - cover);
						Rgba d2 = GetPixel(p, offset, cover);
						d.R += d2.R * sa;
						d.G += d2.G * sa;
						d.B += d2.B * sa;
						d.A += d2.A * sa;
						Set(p, offset, d);
					}

					break;

				case CompOp.SrcOut:
					{
						Rgba s = Get(r, g, b, a, cover);
						Rgba d = GetPixel(p, offset, 255 - cover);
						double d1a = 1 - ((double)p[ai]);
						d.R += s.R * d1a;
						d.G += s.G * d1a;
						d.B += s.B * d1a;
						d.A += s.A * d1a;
						Set(p, offset, d);
					}

					break;

				case CompOp.DstOut:
					{
						Rgba d = GetPixel(p, offset, 255 - cover);
						Rgba dc = GetPixel(p, offset, cover);
						double s1a = 1 - (double)a;
						d.R += dc.R * s1a;
						d.G += dc.G * s1a;
						d.B += dc.B * s1a;
						d.A += dc.A * s1a;
						Set(p, offset, d);
					}

					break;

				case CompOp.SrcAtop:
					{
						Rgba s = Get(r, g, b, a, cover);
						Rgba d = GetPixel(p, offset, 255);
						double s1a = 1 - s.A;
						d.R = (s.R * d.A) + (d.R * s1a);
						d.G = (s.G * d.A) + (d.G * s1a);

						// C++ read the destination's green here (d.g * s1a), a typo fixed in both.
						d.B = (s.B * d.A) + (d.B * s1a);
						Set(p, offset, d);
					}

					break;

				case CompOp.DstAtop:
					{
						Rgba sc = Get(r, g, b, a, cover);
						Rgba dc = GetPixel(p, offset, cover);
						Rgba d = GetPixel(p, offset, 255 - cover);
						double sa = (double)a;
						double d1a = 1 - ((double)p[ai]);
						d.R += (dc.R * sa) + (sc.R * d1a);
						d.G += (dc.G * sa) + (sc.G * d1a);
						d.B += (dc.B * sa) + (sc.B * d1a);
						d.A += sc.A;
						Set(p, offset, d);
					}

					break;

				case CompOp.Xor:
					{
						Rgba s = Get(r, g, b, a, cover);
						Rgba d = GetPixel(p, offset, 255);
						double s1a = 1 - s.A;
						double d1a = 1 - ((double)p[ai]);
						d.R = (s.R * d1a) + (d.R * s1a);
						d.G = (s.G * d1a) + (d.G * s1a);
						d.B = (s.B * d1a) + (d.B * s1a);
						d.A = s.A + d.A - (2 * s.A * d.A);
						Set(p, offset, d);
					}

					break;

				case CompOp.Plus:
					{
						Rgba s = Get(r, g, b, a, cover);
						if (s.A > 0)
						{
							Rgba d = GetPixel(p, offset, 255);
							d.A = Math.Min(d.A + s.A, 1.0);
							d.R = Math.Min(d.R + s.R, d.A);
							d.G = Math.Min(d.G + s.G, d.A);
							d.B = Math.Min(d.B + s.B, d.A);
							Set(p, offset, Clip(d));
						}
					}

					break;

				case CompOp.Minus:
					{
						Rgba s = Get(r, g, b, a, cover);
						if (s.A > 0)
						{
							Rgba d = GetPixel(p, offset, 255);
							d.A += s.A - (s.A * d.A);
							d.R = Math.Max(d.R - s.R, 0.0);
							d.G = Math.Max(d.G - s.G, 0.0);
							d.B = Math.Max(d.B - s.B, 0.0);
							Set(p, offset, Clip(d));
						}
					}

					break;

				case CompOp.Multiply:
					{
						Rgba s = Get(r, g, b, a, cover);
						if (s.A > 0)
						{
							Rgba d = GetPixel(p, offset, 255);
							double s1a = 1 - s.A;
							double d1a = 1 - d.A;
							d.R = (s.R * d.R) + (s.R * d1a) + (d.R * s1a);
							d.G = (s.G * d.G) + (s.G * d1a) + (d.G * s1a);
							d.B = (s.B * d.B) + (s.B * d1a) + (d.B * s1a);
							d.A += s.A - (s.A * d.A);
							Set(p, offset, Clip(d));
						}
					}

					break;

				case CompOp.Screen:
					{
						Rgba s = Get(r, g, b, a, cover);
						if (s.A > 0)
						{
							Rgba d = GetPixel(p, offset, 255);
							d.R += s.R - (s.R * d.R);
							d.G += s.G - (s.G * d.G);
							d.B += s.B - (s.B * d.B);
							d.A += s.A - (s.A * d.A);
							Set(p, offset, Clip(d));
						}
					}

					break;

				case CompOp.Darken:
				case CompOp.Lighten:
					{
						Rgba s = Get(r, g, b, a, cover);
						if (s.A > 0)
						{
							Rgba d = GetPixel(p, offset, 255);
							double d1a = 1 - d.A;
							double s1a = 1 - s.A;
							Func<double, double, double> pick = op == CompOp.Darken ? Math.Min : Math.Max;
							d.R = pick(s.R * d.A, d.R * s.A) + (s.R * d1a) + (d.R * s1a);
							d.G = pick(s.G * d.A, d.G * s.A) + (s.G * d1a) + (d.G * s1a);
							d.B = pick(s.B * d.A, d.B * s.A) + (s.B * d1a) + (d.B * s1a);
							d.A += s.A - (s.A * d.A);
							Set(p, offset, Clip(d));
						}
					}

					break;

				case CompOp.Difference:
					{
						Rgba s = Get(r, g, b, a, cover);
						if (s.A > 0)
						{
							Rgba d = GetPixel(p, offset, 255);
							d.R += s.R - (2 * Math.Min(s.R * d.A, d.R * s.A));
							d.G += s.G - (2 * Math.Min(s.G * d.A, d.G * s.A));
							d.B += s.B - (2 * Math.Min(s.B * d.A, d.B * s.A));
							d.A += s.A - (s.A * d.A);
							Set(p, offset, Clip(d));
						}
					}

					break;

				case CompOp.Exclusion:
					{
						Rgba s = Get(r, g, b, a, cover);
						if (s.A > 0)
						{
							Rgba d = GetPixel(p, offset, 255);
							double d1a = 1 - d.A;
							double s1a = 1 - s.A;
							d.R = ((s.R * d.A) + (d.R * s.A) - (2 * s.R * d.R)) + (s.R * d1a) + (d.R * s1a);
							d.G = ((s.G * d.A) + (d.G * s.A) - (2 * s.G * d.G)) + (s.G * d1a) + (d.G * s1a);
							d.B = ((s.B * d.A) + (d.B * s.A) - (2 * s.B * d.B)) + (s.B * d1a) + (d.B * s1a);
							d.A += s.A - (s.A * d.A);
							Set(p, offset, Clip(d));
						}
					}

					break;

				case CompOp.Overlay:
				case CompOp.HardLight:
					{
						// Overlay's calc and hard light's share one formula, switching on the destination and the source.
						Rgba s = Get(r, g, b, a, cover);
						if (s.A > 0)
						{
							Rgba d = GetPixel(p, offset, 255);
							double d1a = 1 - d.A;
							double s1a = 1 - s.A;
							double sada = s.A * d.A;
							bool overlay = op == CompOp.Overlay;
							d.R = LightCalc(overlay, d.R, s.R, d.A, s.A, sada, d1a, s1a);
							d.G = LightCalc(overlay, d.G, s.G, d.A, s.A, sada, d1a, s1a);
							d.B = LightCalc(overlay, d.B, s.B, d.A, s.A, sada, d1a, s1a);
							d.A += s.A - sada;
							Set(p, offset, Clip(d));
						}
					}

					break;

				case CompOp.ColorDodge:
				case CompOp.ColorBurn:
				case CompOp.SoftLight:
					{
						Rgba s = Get(r, g, b, a, cover);
						if (s.A > 0)
						{
							Rgba d = GetPixel(p, offset, 255);
							if (d.A > 0)
							{
								double sada = s.A * d.A;
								double s1a = 1 - s.A;
								double d1a = 1 - d.A;
								Func<double, double, double, double, double, double, double, double> calc = op switch
								{
									CompOp.ColorDodge => ColorDodgeCalc,
									CompOp.ColorBurn => ColorBurnCalc,
									_ => SoftLightCalc,
								};
								d.R = calc(d.R, s.R, d.A, s.A, sada, d1a, s1a);
								d.G = calc(d.G, s.G, d.A, s.A, sada, d1a, s1a);
								d.B = calc(d.B, s.B, d.A, s.A, sada, d1a, s1a);
								d.A += s.A - sada;
								Set(p, offset, Clip(d));
							}
							else
							{
								Set(p, offset, s);
							}
						}
					}

					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(op), op, null);
			}
		}

		// Overlay: if 2.Dca <= Da, Dca' = 2.Sca.Dca + Sca.(1 - Da) + Dca.(1 - Sa); hard light the same on 2.Sca < Sa;
		// otherwise Dca' = Sa.Da - 2.(Da - Dca).(Sa - Sca) + Sca.(1 - Da) + Dca.(1 - Sa).
		private static double LightCalc(bool overlay, double dca, double sca, double da, double sa, double sada, double d1a, double s1a)
		{
			bool multiply = overlay ? 2 * dca <= da : 2 * sca < sa;
			return multiply
				? (2 * sca * dca) + (sca * d1a) + (dca * s1a)
				: sada - (2 * (da - dca) * (sa - sca)) + (sca * d1a) + (dca * s1a);
		}

		private static double ColorDodgeCalc(double dca, double sca, double da, double sa, double sada, double d1a, double s1a)
		{
			if (sca < sa)
			{
				return (sada * Math.Min(1.0, dca / da * sa / (sa - sca))) + (sca * d1a) + (dca * s1a);
			}

			if (dca > 0)
			{
				return sada + (sca * d1a) + (dca * s1a);
			}

			return sca * d1a;
		}

		private static double ColorBurnCalc(double dca, double sca, double da, double sa, double sada, double d1a, double s1a)
		{
			if (sca > 0)
			{
				return (sada * (1 - Math.Min(1.0, (1 - (dca / da)) * sa / sca))) + (sca * d1a) + (dca * s1a);
			}

			// The "Sca == 0 and Dca == Da" case: C++ tested dca > da, which premultiplied colors never reach.
			if (dca >= da)
			{
				return sada + (dca * s1a);
			}

			return dca * s1a;
		}

		private static double SoftLightCalc(double dca, double sca, double da, double sa, double sada, double d1a, double s1a)
		{
			double dcasa = dca * sa;
			if (2 * sca <= sa)
			{
				return dcasa - ((sada - (2 * sca * da)) * dcasa * (sada - dcasa)) + (sca * d1a) + (dca * s1a);
			}

			if (4 * dca <= da)
			{
				return dcasa + (((2 * sca * da) - sada) * (((((16 * dcasa) - 12) * dcasa + 4) * dca * da) - (dca * da))) + (sca * d1a) + (dca * s1a);
			}

			return dcasa + (((2 * sca * da) - sada) * (Math.Sqrt(dcasa) - dcasa)) + (sca * d1a) + (dca * s1a);
		}

		// C++ blender_base::get for rgba32: to_double is the float widened, scaled by cover / 255 below a full cover,
		// and no_color at cover 0.
		private static Rgba Get(float r, float g, float b, float a, int cover)
		{
			if (cover <= 0)
			{
				return default;
			}

			var c = new Rgba(r, g, b, a);
			if (cover < 255)
			{
				double x = cover / 255.0;
				c.R *= x;
				c.G *= x;
				c.B *= x;
				c.A *= x;
			}

			return c;
		}

		private static Rgba GetPixel(float[] p, int offset, int cover)
		{
			return Get(p[offset + ImageBuffer.OrderR], p[offset + ImageBuffer.OrderG], p[offset + ImageBuffer.OrderB], p[offset + ImageBuffer.OrderA], cover);
		}

		// C++ blender_base::set for rgba32: from_double is a plain narrowing to float.
		private static void Set(float[] p, int offset, Rgba c)
		{
			p[offset + ImageBuffer.OrderR] = (float)c.R;
			p[offset + ImageBuffer.OrderG] = (float)c.G;
			p[offset + ImageBuffer.OrderB] = (float)c.B;
			p[offset + ImageBuffer.OrderA] = (float)c.A;
		}

		// C++ clip(rgba&): alpha into 0..1, each color channel into 0..alpha.
		private static Rgba Clip(Rgba c)
		{
			c.A = c.A > 1 ? 1 : (c.A < 0 ? 0 : c.A);
			c.R = c.R > c.A ? c.A : (c.R < 0 ? 0 : c.R);
			c.G = c.G > c.A ? c.A : (c.G < 0 ? 0 : c.G);
			c.B = c.B > c.A ? c.A : (c.B < 0 ? 0 : c.B);
			return c;
		}

		private struct Rgba
		{
			public double R;
			public double G;
			public double B;
			public double A;

			public Rgba(double r, double g, double b, double a)
			{
				this.R = r;
				this.G = g;
				this.B = b;
				this.A = a;
			}
		}
	}

	/// <summary>
	/// C++ <c>rgba32</c>'s arithmetic (agg_color_rgba.h), float in and float out, so a float pixel format computes
	/// exactly what C++ AGG_BGRA128 does.
	/// </summary>
	public static class RgbaFloatMath
	{
		/// <summary>C++ rgba32::multiply: a * b in float.</summary>
		public static float Multiply(float a, float b) => a * b;

		/// <summary>C++ rgba32::mult_cover: a * cover / 255, in float.</summary>
		public static float MultCover(float a, int cover) => a * cover / 255;

		/// <summary>C++ rgba32::prelerp: (1 - a) * p + q, in float.</summary>
		public static float Prelerp(float p, float q, float a) => ((1 - a) * p) + q;

		/// <summary>C++ rgba32::lerp: (1 - a) * p + a * q, in float.</summary>
		public static float Lerp(float p, float q, float a) => ((1 - a) * p) + (a * q);
	}
}
