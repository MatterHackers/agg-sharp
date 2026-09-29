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
	/// <summary>
	/// feComponentTransfer, feMorphology, feTile, feConvolveMatrix and feDisplacementMap on canvas-sized
	/// premultiplied BGRA buffers, like <see cref="SvgFilterPrimitives"/>: results are transparent outside
	/// <c>region</c>. Kernels and offsets given in SVG's y-down terms are turned to agg's y-up rows here.
	/// </summary>
	internal static class SvgFilterEffects
	{
		private const int B = ImageBuffer.OrderB;
		private const int G = ImageBuffer.OrderG;
		private const int R = ImageBuffer.OrderR;
		private const int A = ImageBuffer.OrderA;

		/// <summary>
		/// feComponentTransfer: each straight channel (R, G, B, A in that order in <paramref name="functions"/>)
		/// through its function of 0..1; null is identity.
		/// </summary>
		public static byte[] ComponentTransfer(byte[] source, int width, SvgPixelRect region, Func<double, double>[] functions)
		{
			var result = new byte[source.Length];
			int[] channels = { R, G, B };
			for (int y = region.Bottom; y < region.Top; y++)
			{
				for (int x = region.Left; x < region.Right; x++)
				{
					int i = (y * width + x) * 4;
					double alpha = source[i + A] / 255.0;
					double mappedAlpha = Clamp01(functions[3]?.Invoke(alpha) ?? alpha);
					for (int c = 0; c < 3; c++)
					{
						double straight = alpha > 0 ? source[i + channels[c]] / 255.0 / alpha : 0;
						double mapped = Clamp01(functions[c]?.Invoke(straight) ?? straight);
						result[i + channels[c]] = (byte)Math.Round(mapped * mappedAlpha * 255);
					}

					result[i + A] = (byte)Math.Round(mappedAlpha * 255);
				}
			}

			return result;
		}

		/// <summary>
		/// A transfer function for type (identity, table, discrete, linear, gamma) and its numbers, as the SVG spec
		/// defines them; null for identity, an unknown type, or a table with no values.
		/// </summary>
		public static Func<double, double> TransferFunction(string type, IReadOnlyList<double> table, double slope, double intercept, double amplitude, double exponent, double offset)
		{
			switch (type)
			{
				case "table" when table.Count > 0:
					return c =>
					{
						int n = table.Count - 1;
						if (n == 0)
						{
							return table[0];
						}

						int k = Math.Min(n - 1, (int)Math.Floor(c * n));
						return table[k] + (c - (double)k / n) * n * (table[k + 1] - table[k]);
					};
				case "discrete" when table.Count > 0:
					return c => table[Math.Min(table.Count - 1, (int)Math.Floor(c * table.Count))];
				case "linear":
					return c => slope * c + intercept;
				case "gamma":
					return c => amplitude * Math.Pow(c, exponent) + offset;
				default:
					return null;
			}
		}

		/// <summary>
		/// feMorphology: each channel's minimum (erode) or maximum (dilate) over the pixels within
		/// <paramref name="radiusX"/>, <paramref name="radiusY"/> of it; pixels outside the region count as transparent.
		/// </summary>
		public static byte[] Morphology(byte[] source, int width, SvgPixelRect region, bool dilate, int radiusX, int radiusY)
		{
			byte[] cropped = SvgFilterPrimitives.Crop(source, width, region);
			byte[] across = MorphologyAxis(cropped, width, region, dilate, radiusX, true);
			return MorphologyAxis(across, width, region, dilate, radiusY, false);
		}

		/// <summary>
		/// One pass of <see cref="Morphology"/> along rows or columns, van Herk/Gil-Werman style: running extremes
		/// over blocks of the window's width make each pixel two comparisons whatever the radius, so a huge radius
		/// (filters/feMorphology/huge-radius) costs what a small one does. A radius past the line's length is cut to
		/// it: its window already takes in the whole line and the transparency beyond it.
		/// </summary>
		private static byte[] MorphologyAxis(byte[] source, int width, SvgPixelRect region, bool dilate, int radius, bool horizontal)
		{
			var result = new byte[source.Length];
			int lines = horizontal ? region.Top - region.Bottom : region.Right - region.Left;
			int length = horizontal ? region.Right - region.Left : region.Top - region.Bottom;
			if (lines <= 0 || length <= 0)
			{
				return result;
			}

			radius = Math.Clamp(radius, 0, length);
			int window = 2 * radius + 1;
			int padded = length + 2 * radius;
			var line = new byte[padded];
			var forward = new byte[padded];
			var backward = new byte[padded];
			int step = horizontal ? 4 : width * 4;
			for (int l = 0; l < lines; l++)
			{
				int start = horizontal ? ((region.Bottom + l) * width + region.Left) * 4 : (region.Bottom * width + region.Left + l) * 4;
				for (int c = 0; c < 4; c++)
				{
					// Pixels outside the region count as transparent: the padding either side is zero.
					Array.Clear(line);
					for (int k = 0; k < length; k++)
					{
						line[radius + k] = source[start + k * step + c];
					}

					for (int k = 0; k < padded; k++)
					{
						forward[k] = k % window == 0 ? line[k] : Pick(forward[k - 1], line[k], dilate);
					}

					for (int k = padded - 1; k >= 0; k--)
					{
						backward[k] = k == padded - 1 || (k + 1) % window == 0 ? line[k] : Pick(backward[k + 1], line[k], dilate);
					}

					// The window for output k is padded k .. k + 2r: its tail of one block, its head of the next.
					for (int k = 0; k < length; k++)
					{
						result[start + k * step + c] = Pick(backward[k], forward[k + 2 * radius], dilate);
					}
				}
			}

			return result;
		}

		private static byte Pick(byte a, byte b, bool max) => max ? Math.Max(a, b) : Math.Min(a, b);

		/// <summary>feTile: <paramref name="tile"/> of <paramref name="source"/> (its input's subregion) repeated across the region.</summary>
		public static byte[] Tile(byte[] source, int width, SvgPixelRect region, SvgPixelRect tile)
		{
			var result = new byte[source.Length];
			int tileWidth = tile.Right - tile.Left;
			int tileHeight = tile.Top - tile.Bottom;
			if (tileWidth <= 0 || tileHeight <= 0)
			{
				return result;
			}

			for (int y = region.Bottom; y < region.Top; y++)
			{
				int sy = tile.Bottom + Mod(y - tile.Bottom, tileHeight);
				for (int x = region.Left; x < region.Right; x++)
				{
					int sx = tile.Left + Mod(x - tile.Left, tileWidth);
					Array.Copy(source, (sy * width + sx) * 4, result, (y * width + x) * 4, 4);
				}
			}

			return result;
		}

		/// <summary>
		/// feConvolveMatrix, as the SVG spec writes it: the kernel (row-major, rows going down, <paramref name="orderX"/>
		/// by <paramref name="orderY"/>) is turned half a turn and centred on (targetX, targetY); the sum is divided
		/// and biased. Premultiplied, unless <paramref name="preserveAlpha"/>, when straight colour is convolved and
		/// alpha kept. Samples past the region follow <paramref name="edgeMode"/> (duplicate, wrap or none).
		/// </summary>
		public static byte[] ConvolveMatrix(byte[] source, int width, SvgPixelRect region, int orderX, int orderY, double[] kernel, double divisor, double bias, int targetX, int targetY, string edgeMode, bool preserveAlpha)
		{
			var result = new byte[source.Length];
			int regionWidth = region.Right - region.Left;
			int regionHeight = region.Top - region.Bottom;
			var sums = new double[4];
			for (int y = region.Bottom; y < region.Top; y++)
			{
				for (int x = region.Left; x < region.Right; x++)
				{
					Array.Clear(sums, 0, 4);
					for (int row = 0; row < orderY; row++)
					{
						for (int column = 0; column < orderX; column++)
						{
							// SVG's Y - targetY + row counts down; agg rows count up.
							int sx = x - targetX + column;
							int sy = y + targetY - row;
							if (sx < region.Left || sx >= region.Right || sy < region.Bottom || sy >= region.Top)
							{
								if (edgeMode == "none")
								{
									continue;
								}

								if (edgeMode == "wrap")
								{
									sx = region.Left + Mod(sx - region.Left, regionWidth);
									sy = region.Bottom + Mod(sy - region.Bottom, regionHeight);
								}
								else
								{
									sx = Math.Max(region.Left, Math.Min(region.Right - 1, sx));
									sy = Math.Max(region.Bottom, Math.Min(region.Top - 1, sy));
								}
							}

							double weight = kernel[(orderY - row - 1) * orderX + orderX - column - 1];
							int s = (sy * width + sx) * 4;
							double alpha = source[s + A] / 255.0;
							for (int c = 0; c < 4; c++)
							{
								double value = source[s + c] / 255.0;
								if (preserveAlpha && c != A)
								{
									value = alpha > 0 ? value / alpha : 0;
								}

								sums[c] += value * weight;
							}
						}
					}

					int i = (y * width + x) * 4;
					double outAlpha = preserveAlpha ? source[i + A] / 255.0 : Clamp01(sums[A] / divisor + bias);
					for (int c = 0; c < 4; c++)
					{
						if (c == A)
						{
							continue;
						}

						double value = preserveAlpha
							? Clamp01(sums[c] / divisor + bias) * outAlpha
							: Math.Min(outAlpha, Clamp01(sums[c] / divisor + bias * outAlpha));
						result[i + c] = (byte)Math.Round(value * 255);
					}

					result[i + A] = (byte)Math.Round(outAlpha * 255);
				}
			}

			return result;
		}

		/// <summary>
		/// feDisplacementMap: each pixel takes <paramref name="source"/>'s pixel moved by scale * (channel - .5) of
		/// <paramref name="map"/>'s straight colour, x by <paramref name="xChannel"/> and y by <paramref name="yChannel"/>
		/// (R, G, B or A). The scale is given as the pixel vectors one unit of x and one unit of y displacement move.
		/// </summary>
		public static byte[] DisplacementMap(byte[] source, byte[] map, int width, SvgPixelRect region, (double X, double Y) unitX, (double X, double Y) unitY, char xChannel, char yChannel)
		{
			var result = new byte[source.Length];
			int Channel(char name) => name == 'R' ? R : name == 'G' ? G : name == 'B' ? B : A;
			int xc = Channel(xChannel), yc = Channel(yChannel);
			for (int y = region.Bottom; y < region.Top; y++)
			{
				for (int x = region.Left; x < region.Right; x++)
				{
					int i = (y * width + x) * 4;
					double alpha = map[i + A] / 255.0;
					double Straight(int c) => c == A ? alpha : alpha > 0 ? map[i + c] / 255.0 / alpha : 0;
					double dx = Straight(xc) - .5, dy = Straight(yc) - .5;
					int sx = (int)Math.Floor(x + .5 + dx * unitX.X + dy * unitY.X);
					int sy = (int)Math.Floor(y + .5 + dx * unitX.Y + dy * unitY.Y);
					if (sx >= region.Left && sx < region.Right && sy >= region.Bottom && sy < region.Top)
					{
						Array.Copy(source, (sy * width + sx) * 4, result, i, 4);
					}
				}
			}

			return result;
		}

		private static int Mod(int value, int size) => ((value % size) + size) % size;

		private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));
	}
}
