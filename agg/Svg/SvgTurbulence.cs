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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// feTurbulence: the SVG 1.1 specification's reference Perlin noise (section 15.23), ported as written - the
	/// seeded lattice and gradients, stitching and the fractalNoise / turbulence sums - so a seed gives the same
	/// pattern as other renderers.
	/// </summary>
	internal sealed class SvgTurbulence
	{
		private const int BSize = 0x100;
		private const int BM = 0xff;
		private const int PerlinN = 0x1000;
		private const long RandM = 2147483647;
		private const long RandA = 16807;
		private const long RandQ = 127773;
		private const long RandR = 2836;

		private readonly int[] latticeSelector = new int[BSize + BSize + 2];
		private readonly double[,,] gradient = new double[4, BSize + BSize + 2, 2];

		public SvgTurbulence(long seed)
		{
			seed = SetupSeed(seed);
			int i, j, k;
			for (k = 0; k < 4; k++)
			{
				for (i = 0; i < BSize; i++)
				{
					this.latticeSelector[i] = i;
					for (j = 0; j < 2; j++)
					{
						this.gradient[k, i, j] = (double)(((seed = Random(seed)) % (BSize + BSize)) - BSize) / BSize;
					}

					double s = Math.Sqrt(this.gradient[k, i, 0] * this.gradient[k, i, 0] + this.gradient[k, i, 1] * this.gradient[k, i, 1]);
					this.gradient[k, i, 0] /= s;
					this.gradient[k, i, 1] /= s;
				}
			}

			for (i = BSize - 1; i > 0; i--)
			{
				k = this.latticeSelector[i];
				this.latticeSelector[i] = this.latticeSelector[j = (int)((seed = Random(seed)) % BSize)];
				this.latticeSelector[j] = k;
			}

			for (i = 0; i < BSize + 2; i++)
			{
				this.latticeSelector[BSize + i] = this.latticeSelector[i];
				for (k = 0; k < 4; k++)
				{
					for (j = 0; j < 2; j++)
					{
						this.gradient[k, BSize + i, j] = this.gradient[k, i, j];
					}
				}
			}
		}

		/// <summary>
		/// The primitive's image: each pixel's top-left corner taken back to user space and run through
		/// <see cref="Turbulence"/> per channel, straight RGBA then premultiplied. <paramref name="tile"/> is the
		/// primitive subregion in user space, which stitching fits the frequencies to.
		/// </summary>
		public static byte[] Render(int width, int height, SvgPixelRect region, Affine userToPixels, double baseFrequencyX, double baseFrequencyY, int octaves, long seed, bool fractalNoise, bool stitch, RectangleDouble tile)
		{
			var result = new byte[width * height * 4];
			var noise = new SvgTurbulence(seed);
			Affine pixelsToUser = userToPixels;
			pixelsToUser.invert();
			int[] channels = { ImageBuffer.OrderR, ImageBuffer.OrderG, ImageBuffer.OrderB, ImageBuffer.OrderA };
			var values = new double[4];
			for (int y = region.Bottom; y < region.Top; y++)
			{
				for (int x = region.Left; x < region.Right; x++)
				{
					// The pixel's top-left corner in SVG's y-down device space is its agg corner (x, y + 1).
					double ux = x, uy = y + 1;
					pixelsToUser.Transform(ref ux, ref uy);
					for (int c = 0; c < 4; c++)
					{
						double sum = noise.Turbulence(c, ux, uy, baseFrequencyX, baseFrequencyY, octaves, fractalNoise, stitch, tile.Left, tile.Bottom, tile.Width, tile.Height);
						values[c] = Math.Max(0, Math.Min(1, fractalNoise ? (sum + 1) / 2 : sum));
					}

					int i = (y * width + x) * 4;
					int alpha = (int)Math.Round(values[3] * 255);
					for (int c = 0; c < 3; c++)
					{
						result[i + channels[c]] = (byte)((int)Math.Round(values[c] * 255) * alpha / 255);
					}

					result[i + channels[3]] = (byte)alpha;
				}
			}

			return result;
		}

		private static long SetupSeed(long seed)
		{
			if (seed <= 0)
			{
				seed = -(seed % (RandM - 1)) + 1;
			}

			return seed > RandM - 1 ? RandM - 1 : seed;
		}

		private static long Random(long seed)
		{
			long result = RandA * (seed % RandQ) - RandR * (seed / RandQ);
			return result <= 0 ? result + RandM : result;
		}

		private static double SCurve(double t) => t * t * (3 - 2 * t);

		private static double Lerp(double t, double a, double b) => a + t * (b - a);

		private double Noise2(int channel, double vx, double vy, bool stitching, int width, int height, int wrapX, int wrapY)
		{
			double t = vx + PerlinN;
			int bx0 = (int)t & BM;
			int bx1 = (bx0 + 1) & BM;
			double rx0 = t - (int)t;
			double rx1 = rx0 - 1;
			t = vy + PerlinN;
			int by0 = (int)t & BM;
			int by1 = (by0 + 1) & BM;
			double ry0 = t - (int)t;
			double ry1 = ry0 - 1;
			if (stitching)
			{
				bx0 = bx0 >= wrapX ? bx0 - width : bx0;
				bx1 = bx1 >= wrapX ? bx1 - width : bx1;
				by0 = by0 >= wrapY ? by0 - height : by0;
				by1 = by1 >= wrapY ? by1 - height : by1;
			}

			bx0 &= BM;
			bx1 &= BM;
			by0 &= BM;
			by1 &= BM;
			int i = this.latticeSelector[bx0];
			int j = this.latticeSelector[bx1];
			int b00 = this.latticeSelector[i + by0];
			int b10 = this.latticeSelector[j + by0];
			int b01 = this.latticeSelector[i + by1];
			int b11 = this.latticeSelector[j + by1];
			double sx = SCurve(rx0);
			double sy = SCurve(ry0);
			double u = rx0 * this.gradient[channel, b00, 0] + ry0 * this.gradient[channel, b00, 1];
			double v = rx1 * this.gradient[channel, b10, 0] + ry0 * this.gradient[channel, b10, 1];
			double a = Lerp(sx, u, v);
			u = rx0 * this.gradient[channel, b01, 0] + ry1 * this.gradient[channel, b01, 1];
			v = rx1 * this.gradient[channel, b11, 0] + ry1 * this.gradient[channel, b11, 1];
			double b = Lerp(sx, u, v);
			return Lerp(sy, a, b);
		}

		/// <summary>The spec's turbulence(): octaves of noise at doubling frequency, halving weight.</summary>
		private double Turbulence(int channel, double x, double y, double baseFrequencyX, double baseFrequencyY, int octaves, bool fractalSum, bool stitching, double tileX, double tileY, double tileWidth, double tileHeight)
		{
			int width = 0, height = 0, wrapX = 0, wrapY = 0;
			if (stitching)
			{
				// Frequencies are nudged to fit a whole number of lattice cells into the tile.
				if (baseFrequencyX != 0)
				{
					double low = Math.Floor(tileWidth * baseFrequencyX) / tileWidth;
					double high = Math.Ceiling(tileWidth * baseFrequencyX) / tileWidth;
					baseFrequencyX = baseFrequencyX / low < high / baseFrequencyX ? low : high;
				}

				if (baseFrequencyY != 0)
				{
					double low = Math.Floor(tileHeight * baseFrequencyY) / tileHeight;
					double high = Math.Ceiling(tileHeight * baseFrequencyY) / tileHeight;
					baseFrequencyY = baseFrequencyY / low < high / baseFrequencyY ? low : high;
				}

				width = (int)(tileWidth * baseFrequencyX + .5);
				wrapX = (int)(tileX * baseFrequencyX + PerlinN + width);
				height = (int)(tileHeight * baseFrequencyY + .5);
				wrapY = (int)(tileY * baseFrequencyY + PerlinN + height);
			}

			double sum = 0;
			double vx = x * baseFrequencyX;
			double vy = y * baseFrequencyY;
			double ratio = 1;
			for (int octave = 0; octave < octaves; octave++)
			{
				double n = this.Noise2(channel, vx, vy, stitching, width, height, wrapX, wrapY);
				sum += (fractalSum ? n : Math.Abs(n)) / ratio;
				vx *= 2;
				vy *= 2;
				ratio *= 2;
				if (stitching)
				{
					width += width;
					wrapX = 2 * wrapX - PerlinN;
					height += height;
					wrapY = 2 * wrapY - PerlinN;
				}
			}

			return sum;
		}
	}
}
