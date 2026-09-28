/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.Svg
{
	/// <summary>A pixel rectangle of a filter canvas: columns Left..Right-1, rows Bottom..Top-1 (agg rows, y up).</summary>
	internal readonly struct SvgPixelRect
	{
		public SvgPixelRect(int left, int bottom, int right, int top)
		{
			this.Left = left;
			this.Bottom = bottom;
			this.Right = Math.Max(left, right);
			this.Top = Math.Max(bottom, top);
		}

		public int Left { get; }

		public int Bottom { get; }

		public int Right { get; }

		public int Top { get; }

		public bool IsEmpty => this.Right <= this.Left || this.Top <= this.Bottom;

		public SvgPixelRect Intersect(SvgPixelRect other) => new SvgPixelRect(
			Math.Max(this.Left, other.Left), Math.Max(this.Bottom, other.Bottom), Math.Min(this.Right, other.Right), Math.Min(this.Top, other.Top));
	}

	/// <summary>One filter primitive's result: canvas-sized premultiplied BGRA, in linearRGB or sRGB.</summary>
	internal sealed class SvgFilterImage
	{
		public SvgFilterImage(byte[] pixels, bool linear)
		{
			this.Pixels = pixels;
			this.Linear = linear;
		}

		public byte[] Pixels { get; }

		public bool Linear { get; }

		/// <summary>The pixels this result was made for (its primitive's subregion); feTile repeats them.</summary>
		public SvgPixelRect Region { get; set; }
	}

	/// <summary>
	/// The pixel work of SVG filter primitives on canvas-sized premultiplied BGRA buffers (4 bytes a pixel, rows
	/// <c>width</c> pixels long). Each returns a new buffer holding its result inside <c>region</c> (the primitive's
	/// subregion, in pixels) and transparent black everywhere else.
	/// </summary>
	internal static class SvgFilterPrimitives
	{
		private const int B = ImageBuffer.OrderB;
		private const int G = ImageBuffer.OrderG;
		private const int R = ImageBuffer.OrderR;
		private const int A = ImageBuffer.OrderA;

		private static readonly byte[] ToLinearTable = Table(c => c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4));

		private static readonly byte[] ToSrgbTable = Table(c => c <= .0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - .055);

		/// <summary>Visits every pixel's byte index in <paramref name="region"/>.</summary>
		private static IEnumerable<int> Indices(int width, SvgPixelRect region)
		{
			for (int y = region.Bottom; y < region.Top; y++)
			{
				for (int x = region.Left; x < region.Right; x++)
				{
					yield return (y * width + x) * 4;
				}
			}
		}

		/// <summary><paramref name="source"/> inside <paramref name="region"/>, transparent outside.</summary>
		public static byte[] Crop(byte[] source, int width, SvgPixelRect region)
		{
			var result = new byte[source.Length];
			for (int y = region.Bottom; y < region.Top; y++)
			{
				int start = (y * width + region.Left) * 4;
				Array.Copy(source, start, result, start, (region.Right - region.Left) * 4);
			}

			return result;
		}

		/// <summary>
		/// <paramref name="image"/> in linearRGB (<paramref name="linear"/>) or sRGB: each colour is divided out of
		/// its alpha, mapped through the transfer curve, and multiplied back, as filters' colour spaces are defined.
		/// </summary>
		public static SvgFilterImage InColorSpace(SvgFilterImage image, bool linear)
		{
			if (image.Linear == linear)
			{
				return image;
			}

			byte[] table = linear ? ToLinearTable : ToSrgbTable;
			byte[] source = image.Pixels;
			var result = new byte[source.Length];
			for (int i = 0; i + 3 < source.Length; i += 4)
			{
				int alpha = source[i + A];
				if (alpha == 0)
				{
					continue;
				}

				for (int c = 0; c < 3; c++)
				{
					int straight = Math.Min(255, (source[i + c] * 255 + alpha / 2) / alpha);
					result[i + c] = (byte)((table[straight] * alpha + 127) / 255);
				}

				result[i + A] = (byte)alpha;
			}

			return new SvgFilterImage(result, linear);
		}

		/// <summary>SourceAlpha: <paramref name="source"/>'s alpha with black colour.</summary>
		public static byte[] AlphaOnly(byte[] source)
		{
			var result = new byte[source.Length];
			for (int i = A; i < source.Length; i += 4)
			{
				result[i] = source[i];
			}

			return result;
		}

		/// <summary>feFlood: <paramref name="premultiplied"/> over the whole region.</summary>
		public static byte[] Flood(int width, int height, SvgPixelRect region, Color premultiplied)
		{
			var result = new byte[width * height * 4];
			foreach (int i in Indices(width, region))
			{
				result[i + R] = premultiplied.red;
				result[i + G] = premultiplied.green;
				result[i + B] = premultiplied.blue;
				result[i + A] = premultiplied.alpha;
			}

			return result;
		}

		/// <summary>feOffset: moved <paramref name="dx"/>, <paramref name="dy"/> whole pixels (agg rows, y up).</summary>
		public static byte[] Offset(byte[] source, int width, int height, SvgPixelRect region, int dx, int dy)
		{
			var result = new byte[source.Length];
			for (int y = region.Bottom; y < region.Top; y++)
			{
				int sourceY = y - dy;
				for (int x = region.Left; x < region.Right; x++)
				{
					int sourceX = x - dx;
					if (sourceX >= 0 && sourceX < width && sourceY >= 0 && sourceY < height)
					{
						Array.Copy(source, (sourceY * width + sourceX) * 4, result, (y * width + x) * 4, 4);
					}
				}
			}

			return result;
		}

		/// <summary>
		/// feGaussianBlur with standard deviations in pixels. Below 2 a true Gaussian kernel is used; from 2 up,
		/// three box blurs of the size SVG 1.1 (section 15.17) gives, as resvg does. Pixels outside the region
		/// count as transparent. A deviation under .05 leaves that axis alone.
		/// </summary>
		public static byte[] Blur(byte[] source, int width, SvgPixelRect region, double sigmaX, double sigmaY)
		{
			byte[] result = Crop(source, width, region);
			if (sigmaX >= .05)
			{
				BlurAxis(result, width, region, sigmaX, true);
			}

			if (sigmaY >= .05)
			{
				BlurAxis(result, width, region, sigmaY, false);
			}

			return result;
		}

		private static void BlurAxis(byte[] pixels, int width, SvgPixelRect region, double sigma, bool horizontal)
		{
			int length = horizontal ? region.Right - region.Left : region.Top - region.Bottom;
			int lines = horizontal ? region.Top - region.Bottom : region.Right - region.Left;
			var line = new double[length];
			var scratch = new double[length];
			double[] kernel = sigma < 2 ? GaussianKernel(sigma) : null;
			for (int l = 0; l < lines; l++)
			{
				for (int c = 0; c < 4; c++)
				{
					int Index(int p) => horizontal
						? ((region.Bottom + l) * width + region.Left + p) * 4 + c
						: ((region.Bottom + p) * width + region.Left + l) * 4 + c;

					for (int p = 0; p < length; p++)
					{
						line[p] = pixels[Index(p)];
					}

					if (kernel != null)
					{
						Convolve(line, scratch, kernel);
						Array.Copy(scratch, line, length);
					}
					else
					{
						// d = floor(s * 3 * sqrt(2 * pi) / 4 + .5); an even d takes two boxes off-centre either way and a
						// third of d + 1, centred, so the three together stay centred.
						int d = (int)Math.Floor(sigma * 3 * Math.Sqrt(2 * Math.PI) / 4 + .5);
						if (d % 2 == 1)
						{
							Box(line, scratch, d / 2, d / 2);
							Box(scratch, line, d / 2, d / 2);
							Box(line, scratch, d / 2, d / 2);
						}
						else
						{
							Box(line, scratch, d / 2, d / 2 - 1);
							Box(scratch, line, d / 2 - 1, d / 2);
							Box(line, scratch, d / 2, d / 2);
						}

						Array.Copy(scratch, line, length);
					}

					for (int p = 0; p < length; p++)
					{
						pixels[Index(p)] = (byte)Math.Max(0, Math.Min(255, Math.Round(line[p])));
					}
				}
			}
		}

		/// <summary>The mean of <paramref name="source"/>[i - before .. i + after] into each <paramref name="result"/>[i], zero past the ends.</summary>
		private static void Box(double[] source, double[] result, int before, int after)
		{
			int size = before + after + 1;
			double sum = 0;
			for (int i = 0; i < after && i < source.Length; i++)
			{
				sum += source[i];
			}

			for (int i = 0; i < source.Length; i++)
			{
				if (i + after < source.Length)
				{
					sum += source[i + after];
				}

				if (i - before - 1 >= 0)
				{
					sum -= source[i - before - 1];
				}

				result[i] = sum / size;
			}
		}

		private static double[] GaussianKernel(double sigma)
		{
			int radius = (int)Math.Ceiling(sigma * 3);
			var kernel = new double[radius * 2 + 1];
			double total = 0;
			for (int i = -radius; i <= radius; i++)
			{
				kernel[i + radius] = Math.Exp(-i * i / (2 * sigma * sigma));
				total += kernel[i + radius];
			}

			for (int i = 0; i < kernel.Length; i++)
			{
				kernel[i] /= total;
			}

			return kernel;
		}

		private static void Convolve(double[] source, double[] result, double[] kernel)
		{
			int radius = kernel.Length / 2;
			for (int i = 0; i < source.Length; i++)
			{
				double sum = 0;
				for (int k = -radius; k <= radius; k++)
				{
					int p = i + k;
					if (p >= 0 && p < source.Length)
					{
						sum += source[p] * kernel[k + radius];
					}
				}

				result[i] = sum;
			}
		}

		/// <summary>
		/// feComposite: <paramref name="top"/> (in) with <paramref name="bottom"/> (in2) by Porter-Duff
		/// <paramref name="op"/> (over, in, out, atop, xor), or arithmetic's k1*i1*i2 + k2*i1 + k3*i2 + k4.
		/// </summary>
		public static byte[] Composite(byte[] top, byte[] bottom, int width, SvgPixelRect region, string op, double k1, double k2, double k3, double k4)
		{
			var result = new byte[top.Length];
			foreach (int i in Indices(width, region))
			{
				if (op == "arithmetic")
				{
					double alpha = Clamp01(k1 * top[i + A] / 255 * bottom[i + A] / 255 + k2 * top[i + A] / 255 + k3 * bottom[i + A] / 255 + k4);
					for (int c = 0; c < 4; c++)
					{
						double value = c == A ? alpha
							: Math.Min(alpha, Clamp01(k1 * top[i + c] / 255 * bottom[i + c] / 255 + k2 * top[i + c] / 255 + k3 * bottom[i + c] / 255 + k4));
						result[i + c] = (byte)Math.Round(value * 255);
					}

					continue;
				}

				int topAlpha = top[i + A];
				int bottomAlpha = bottom[i + A];
				(int topFactor, int bottomFactor) = op switch
				{
					"in" => (bottomAlpha, 0),
					"out" => (255 - bottomAlpha, 0),
					"atop" => (bottomAlpha, 255 - topAlpha),
					"xor" => (255 - bottomAlpha, 255 - topAlpha),
					_ => (255, 255 - topAlpha),
				};
				for (int c = 0; c < 4; c++)
				{
					result[i + c] = (byte)Math.Min(255, (top[i + c] * topFactor + bottom[i + c] * bottomFactor + 127) / 255);
				}
			}

			return result;
		}

		/// <summary>feMerge: <paramref name="layers"/> composited source-over, first at the bottom.</summary>
		public static byte[] Merge(IReadOnlyList<byte[]> layers, int width, int height, SvgPixelRect region)
		{
			var result = new byte[width * height * 4];
			foreach (byte[] layer in layers)
			{
				result = Composite(layer, result, width, region, "over", 0, 0, 0, 0);
			}

			return result;
		}

		/// <summary>feColorMatrix: the 4x5 row-major <paramref name="matrix"/> applied to each straight (unpremultiplied) RGBA.</summary>
		public static byte[] ColorMatrix(byte[] source, int width, SvgPixelRect region, double[] matrix)
		{
			var result = new byte[source.Length];
			var straight = new double[4];
			var mapped = new double[4];
			foreach (int i in Indices(width, region))
			{
				double alpha = source[i + A] / 255.0;
				straight[0] = alpha > 0 ? source[i + R] / 255.0 / alpha : 0;
				straight[1] = alpha > 0 ? source[i + G] / 255.0 / alpha : 0;
				straight[2] = alpha > 0 ? source[i + B] / 255.0 / alpha : 0;
				straight[3] = alpha;
				for (int row = 0; row < 4; row++)
				{
					double value = matrix[row * 5 + 4];
					for (int column = 0; column < 4; column++)
					{
						value += matrix[row * 5 + column] * straight[column];
					}

					mapped[row] = Clamp01(value);
				}

				result[i + R] = (byte)Math.Round(mapped[0] * mapped[3] * 255);
				result[i + G] = (byte)Math.Round(mapped[1] * mapped[3] * 255);
				result[i + B] = (byte)Math.Round(mapped[2] * mapped[3] * 255);
				result[i + A] = (byte)Math.Round(mapped[3] * 255);
			}

			return result;
		}

		/// <summary>
		/// feBlend: <paramref name="top"/> (in) blended onto <paramref name="bottom"/> (in2) with a separable CSS
		/// blend mode or a non-separable one (hue, saturation, color, luminosity; see <see cref="SvgBlendModes"/>), normal when unknown.
		/// </summary>
		public static byte[] Blend(byte[] top, byte[] bottom, int width, SvgPixelRect region, string mode)
		{
			Func<double, double, double> blend = BlendFunction(mode);
			Func<double[], double[], double[]> nonSeparable = SvgBlendModes.NonSeparable(mode);
			var result = new byte[top.Length];
			int[] channels = { R, G, B };
			var backdrop = new double[3];
			var sourceColor = new double[3];
			foreach (int i in Indices(width, region))
			{
				double topAlpha = top[i + A] / 255.0;
				double bottomAlpha = bottom[i + A] / 255.0;
				double alpha = topAlpha + bottomAlpha - topAlpha * bottomAlpha;
				double[] mixedColor = null;
				if (nonSeparable != null && topAlpha > 0 && bottomAlpha > 0)
				{
					for (int c = 0; c < 3; c++)
					{
						backdrop[c] = bottom[i + channels[c]] / 255.0 / bottomAlpha;
						sourceColor[c] = top[i + channels[c]] / 255.0 / topAlpha;
					}

					mixedColor = nonSeparable(backdrop, sourceColor);
				}

				for (int n = 0; n < 3; n++)
				{
					int c = channels[n];
					double cs = top[i + c] / 255.0;
					double cb = bottom[i + c] / 255.0;
					double mixed = mixedColor != null ? mixedColor[n] : topAlpha > 0 && bottomAlpha > 0 ? blend(cb / bottomAlpha, cs / topAlpha) : 0;
					double value = (1 - bottomAlpha) * cs + (1 - topAlpha) * cb + topAlpha * bottomAlpha * mixed;
					result[i + c] = (byte)Math.Round(Math.Min(alpha, Clamp01(value)) * 255);
				}

				result[i + A] = (byte)Math.Round(alpha * 255);
			}

			return result;
		}

		/// <summary>B(backdrop, source) of a separable blend mode, on straight colours 0..1.</summary>
		private static Func<double, double, double> BlendFunction(string mode)
		{
			double HardLight(double b, double s) => s <= .5 ? b * 2 * s : Screen(b, 2 * s - 1);
			double Screen(double b, double s) => b + s - b * s;
			switch (mode)
			{
				case "multiply":
					return (b, s) => b * s;
				case "screen":
					return Screen;
				case "darken":
					return Math.Min;
				case "lighten":
					return Math.Max;
				case "overlay":
					return (b, s) => HardLight(s, b);
				case "color-dodge":
					return (b, s) => b == 0 ? 0 : s >= 1 ? 1 : Math.Min(1, b / (1 - s));
				case "color-burn":
					return (b, s) => b >= 1 ? 1 : s <= 0 ? 0 : 1 - Math.Min(1, (1 - b) / s);
				case "hard-light":
					return HardLight;
				case "soft-light":
					return (b, s) =>
					{
						double d = b <= .25 ? ((16 * b - 12) * b + 4) * b : Math.Sqrt(b);
						return s <= .5 ? b - (1 - 2 * s) * b * (1 - b) : b + (2 * s - 1) * (d - b);
					};
				case "difference":
					return (b, s) => Math.Abs(b - s);
				case "exclusion":
					return (b, s) => b + s - 2 * b * s;
				default:
					return (b, s) => s;
			}
		}

		private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));

		private static byte[] Table(Func<double, double> curve)
		{
			var table = new byte[256];
			for (int i = 0; i < 256; i++)
			{
				table[i] = (byte)Math.Round(Clamp01(curve(i / 255.0)) * 255);
			}

			return table;
		}
	}
}
