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
	/// The SVG compositing operators of C++ AGG's agg_pixfmt_rgba.h (see http://www.w3.org/TR/SVGCompositing/),
	/// numbered as C++ <c>comp_op_e</c> so an index from the compositing demos' list maps straight across.
	/// Dca/Da are the destination's premultiplied color and alpha, Sca/Sa the source's.
	/// </summary>
	public enum CompOp
	{
		/// <summary>Dca' = 0, Da' = 0.</summary>
		Clear,

		/// <summary>Dca' = Sca, Da' = Sa.</summary>
		Src,

		/// <summary>Dca' = Dca, Da' = Da: the destination is left alone.</summary>
		Dst,

		/// <summary>Dca' = Sca + Dca.(1 - Sa): ordinary alpha blending.</summary>
		SrcOver,

		/// <summary>Dca' = Dca + Sca.(1 - Da).</summary>
		DstOver,

		/// <summary>Dca' = Sca.Da, Da' = Sa.Da.</summary>
		SrcIn,

		/// <summary>Dca' = Dca.Sa, Da' = Sa.Da.</summary>
		DstIn,

		/// <summary>Dca' = Sca.(1 - Da), Da' = Sa.(1 - Da).</summary>
		SrcOut,

		/// <summary>Dca' = Dca.(1 - Sa), Da' = Da.(1 - Sa).</summary>
		DstOut,

		/// <summary>Dca' = Sca.Da + Dca.(1 - Sa), Da' = Da.</summary>
		SrcAtop,

		/// <summary>Dca' = Dca.Sa + Sca.(1 - Da), Da' = Sa.</summary>
		DstAtop,

		/// <summary>Dca' = Sca.(1 - Da) + Dca.(1 - Sa), Da' = Sa + Da - 2.Sa.Da.</summary>
		Xor,

		/// <summary>Dca' = Sca + Dca, Da' = Sa + Da, clipped.</summary>
		Plus,

		/// <summary>Dca' = Sca.Dca + Sca.(1 - Da) + Dca.(1 - Sa).</summary>
		Multiply,

		/// <summary>Dca' = Sca + Dca - Sca.Dca.</summary>
		Screen,

		/// <summary>Multiply or screen, chosen by the destination.</summary>
		Overlay,

		/// <summary>Dca' = min(Sca.Da, Dca.Sa) + Sca.(1 - Da) + Dca.(1 - Sa).</summary>
		Darken,

		/// <summary>Dca' = max(Sca.Da, Dca.Sa) + Sca.(1 - Da) + Dca.(1 - Sa).</summary>
		Lighten,

		/// <summary>Brightens the destination to reflect the source.</summary>
		ColorDodge,

		/// <summary>Darkens the destination to reflect the source.</summary>
		ColorBurn,

		/// <summary>Multiply or screen, chosen by the source.</summary>
		HardLight,

		/// <summary>Darkens or lightens, chosen by the source; a softer hard light.</summary>
		SoftLight,

		/// <summary>Dca' = Sca + Dca - 2.min(Sca.Da, Dca.Sa).</summary>
		Difference,

		/// <summary>Dca' = Sca.Da + Dca.Sa - 2.Sca.Dca + Sca.(1 - Da) + Dca.(1 - Sa).</summary>
		Exclusion,

		/// <summary>
		/// Dca' = Dca - Sca, Da' = Sa + Da - Sa.Da. Not in the SVG spec, and left out of C++ <c>comp_op_e</c> (its
		/// comp_op_rgba_minus is commented out of the table), so it comes after the C++ operators here.
		/// </summary>
		Minus,
	}

	/// <summary>
	/// Marks a blender whose every solid pixel must go through its BlendPixels, as C++ pixfmt_custom_blend_rgba
	/// calls blend_pix for each: ImageBuffer's solid blends then skip their copy-when-opaque and
	/// skip-when-transparent shortcuts, which a compositing operator such as Multiply or Src does not obey.
	/// </summary>
	internal interface IBlendsEveryPixel
	{
	}

	/// <summary>
	/// C++ <c>pixfmt_custom_blend_rgba&lt;comp_op_adaptor_rgba&lt;rgba8, order_bgra&gt;&gt;</c>: blends straight
	/// (non-premultiplied) colors into a premultiplied BGRA buffer through one of the SVG compositing operators,
	/// chosen by <see cref="Operator"/>. Every pixel goes through the operator, opaque or not, as in C++.
	/// </summary>
	/// <remarks>
	/// Byte-identical to C++ AGG (the operators work in doubles and round back with <c>uround</c>), except for two
	/// C++ bugs fixed here and in the reference renderer's patched agg_pixfmt_rgba.h: <see cref="CompOp.SrcAtop"/>
	/// blends blue with the destination's blue (C++ used its green), and <see cref="CompOp.ColorBurn"/> keeps a
	/// full destination channel under a black source (C++ tested dca &gt; da, which premultiplied colors never
	/// reach). C++'s contrast, invert and invert_rgb are disabled (#if 0) in AGG and not ported.
	/// </remarks>
	public sealed class BlenderCompOpBGRA : BlenderBase8888, IRecieveBlenderByte, IBlendsEveryPixel
	{
		public BlenderCompOpBGRA(CompOp compOp = CompOp.SrcOver)
		{
			this.Operator = compOp;
		}

		/// <summary>The operator every blend uses - C++ <c>pixfmt_custom_blend_rgba::comp_op</c>.</summary>
		public CompOp Operator { get; set; }

		public Color PixelToColor(byte[] buffer, int bufferOffset)
		{
			return new Color(buffer[bufferOffset + ImageBuffer.OrderR], buffer[bufferOffset + ImageBuffer.OrderG], buffer[bufferOffset + ImageBuffer.OrderB], buffer[bufferOffset + ImageBuffer.OrderA]);
		}

		/// <summary>C++ copy_hline: the color's bytes, stored as they are.</summary>
		public void CopyPixels(byte[] buffer, int bufferOffset, Color sourceColor, int count)
		{
			for (int i = 0; i < count; i++)
			{
				buffer[bufferOffset + ImageBuffer.OrderR] = sourceColor.red;
				buffer[bufferOffset + ImageBuffer.OrderG] = sourceColor.green;
				buffer[bufferOffset + ImageBuffer.OrderB] = sourceColor.blue;
				buffer[bufferOffset + ImageBuffer.OrderA] = sourceColor.alpha;
				bufferOffset += 4;
			}
		}

		public void BlendPixel(byte[] buffer, int bufferOffset, Color sourceColor)
		{
			BlendPix(this.Operator, buffer, bufferOffset, sourceColor, 255);
		}

		public void BlendPixels(byte[] buffer, int bufferOffset, Color[] sourceColors, int sourceColorsOffset, byte[] sourceCovers, int sourceCoversOffset, bool firstCoverForAll, int count)
		{
			for (int i = 0; i < count; i++)
			{
				BlendPix(this.Operator, buffer, bufferOffset, sourceColors[sourceColorsOffset + i], sourceCovers[firstCoverForAll ? sourceCoversOffset : sourceCoversOffset + i]);
				bufferOffset += 4;
			}
		}

		/// <summary>
		/// C++ <c>comp_op_adaptor_rgba::blend_pix</c>: premultiplies the straight <paramref name="color"/> (rgba8
		/// multiply) and composites it into the premultiplied pixel at <paramref name="offset"/> with
		/// <paramref name="op"/>, scaled by <paramref name="cover"/> (0 to 255).
		/// </summary>
		public static void BlendPix(CompOp op, byte[] buffer, int offset, Color color, int cover)
		{
			int a = color.alpha;
			BlendPremultiplied(op, buffer, offset, Rgba8Math.Multiply(color.red, a), Rgba8Math.Multiply(color.green, a), Rgba8Math.Multiply(color.blue, a), a, cover);
		}

		private static void BlendPremultiplied(CompOp op, byte[] p, int offset, int r, int g, int b, int a, int cover)
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
						p[ri] = (byte)r;
						p[gi] = (byte)g;
						p[bi] = (byte)b;
						p[ai] = (byte)a;
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
						// C++ uses blender_rgba_pre here: integer mult_cover, then prelerp.
						int ca = Rgba8Math.Multiply(a, cover);
						p[ri] = (byte)Rgba8Math.Prelerp(p[ri], Rgba8Math.Multiply(r, cover), ca);
						p[gi] = (byte)Rgba8Math.Prelerp(p[gi], Rgba8Math.Multiply(g, cover), ca);
						p[bi] = (byte)Rgba8Math.Prelerp(p[bi], Rgba8Math.Multiply(b, cover), ca);
						p[ai] = (byte)Rgba8Math.Prelerp(p[ai], ca, ca);
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
						double da = p[ai] / 255.0;
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
						double sa = a / 255.0;
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
						double d1a = 1 - (p[ai] / 255.0);
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
						double s1a = 1 - (a / 255.0);
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
						double sa = a / 255.0;
						double d1a = 1 - (p[ai] / 255.0);
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
						double d1a = 1 - (p[ai] / 255.0);
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

		// C++ blender_base::get: a color as doubles (value / 255), scaled by cover / 255 below a full cover, and
		// no_color at cover 0.
		private static Rgba Get(int r, int g, int b, int a, int cover)
		{
			if (cover <= 0)
			{
				return default;
			}

			var c = new Rgba(r / 255.0, g / 255.0, b / 255.0, a / 255.0);
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

		private static Rgba GetPixel(byte[] p, int offset, int cover)
		{
			return Get(p[offset + ImageBuffer.OrderR], p[offset + ImageBuffer.OrderG], p[offset + ImageBuffer.OrderB], p[offset + ImageBuffer.OrderA], cover);
		}

		// C++ blender_base::set: each channel rgba8::from_double, value_type(uround(v * 255)) - unsigned(v + 0.5)
		// truncated to a byte (.NET's double-to-uint saturates as arm64's fcvtzu does).
		private static void Set(byte[] p, int offset, Rgba c)
		{
			p[offset + ImageBuffer.OrderR] = FromDouble(c.R);
			p[offset + ImageBuffer.OrderG] = FromDouble(c.G);
			p[offset + ImageBuffer.OrderB] = FromDouble(c.B);
			p[offset + ImageBuffer.OrderA] = FromDouble(c.A);

			static byte FromDouble(double v) => unchecked((byte)(uint)((v * 255) + 0.5));
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
}
