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

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// C++ AGG's slight_blur: a Gaussian blur small enough to reach only the adjacent pixels, for smoothing
	/// pixel-scale detail such as single-pixel lines. The curve has a standard deviation of r/2 (as in the HTML/CSS
	/// spec), sampled at 0 and at 1 pixel; the radius can go to about 1.33 before the cut-off matters. Every byte of
	/// a pixel is blurred alike, so use it on images without alpha or with premultiplied alpha.
	/// </summary>
	public class SlightBlur
	{
		// The center pixel's and each neighbour's weight; they sum to 1.
		private double g0;
		private double g1;

		public SlightBlur(double radius = 1.33)
		{
			this.Radius = radius;
		}

		/// <summary>The center pixel's weight, as the GPU's slight blur takes it.</summary>
		public double CenterWeight => this.g0;

		/// <summary>Each neighbour's weight, as the GPU's slight blur takes it.</summary>
		public double NeighborWeight => this.g1;

		/// <summary>The blur radius in pixels. 0 or less leaves the image as it is.</summary>
		public double Radius
		{
			set
			{
				if (value > 0)
				{
					// C++ uses this truncated pi; it is kept so the weights match to the last bit.
					double pi = 3.14159;
					double n = 2 / value;
					this.g0 = 1 / Math.Sqrt(2 * pi);
					this.g1 = this.g0 * Math.Exp(-n * n);

					double sum = this.g0 + (2 * this.g1);
					this.g0 /= sum;
					this.g1 /= sum;
				}
				else
				{
					this.g0 = 1;
					this.g1 = 0;
				}
			}
		}

		/// <summary>C++ <c>apply_slight_blur</c>: blurs the whole of <paramref name="image"/> when the radius is above 0.</summary>
		public static void Apply(IImageByte image, double radius)
		{
			if (radius > 0)
			{
				new SlightBlur(radius).Blur(image, new RectangleInt(0, 0, image.Width - 1, image.Height - 1));
			}
		}

		/// <summary>
		/// Blurs the inclusive pixel box <paramref name="bounds"/> (clipped to the image) in place: each row across,
		/// then the rows down, the box's edge pixels standing in for those beyond it. A box under 3 pixels wide or
		/// high is left alone. Channels round to the nearest level where C++ truncates (see SlightBlurTests).
		/// </summary>
		public void Blur(IImageByte image, RectangleInt bounds)
		{
			int x1 = Math.Max(bounds.Left, 0);
			int y1 = Math.Max(bounds.Bottom, 0);
			int x2 = Math.Min(bounds.Right, image.Width - 1);
			int y2 = Math.Min(bounds.Top, image.Height - 1);

			int w = x2 - x1 + 1;
			int h = y2 - y1 + 1;
			if (w < 3 || h < 3)
			{
				return;
			}

			byte[] buffer = image.GetBuffer();
			int channels = image.BitDepth / 8;
			int step = image.GetBytesBetweenPixelsInclusive();
			int rowLength = w * channels;

			// The rows above, at and below the one being written, each already blurred across; the top row stands
			// in for the one above it.
			var above = new byte[rowLength];
			var current = new byte[rowLength];
			var below = new byte[rowLength];
			this.BlurRow(buffer, image.GetBufferOffsetXY(x1, y1), w, channels, step, above);
			Array.Copy(above, current, rowLength);

			for (int y = 0; y < h; y++)
			{
				if (y + 1 < h)
				{
					this.BlurRow(buffer, image.GetBufferOffsetXY(x1, y1 + y + 1), w, channels, step, below);
				}
				else
				{
					Array.Copy(current, below, rowLength);
				}

				int destination = image.GetBufferOffsetXY(x1, y1 + y);
				for (int x = 0; x < w; x++)
				{
					for (int c = 0; c < channels; c++)
					{
						int i = (x * channels) + c;
						buffer[destination + c] = this.Weigh(above[i], current[i], below[i]);
					}

					destination += step;
				}

				(above, current, below) = (current, below, above);
			}
		}

		// One row blurred across into row, the end pixels standing in for their missing neighbours.
		private void BlurRow(byte[] buffer, int offset, int w, int channels, int step, byte[] row)
		{
			for (int x = 0; x < w; x++)
			{
				int center = offset + (x * step);
				int left = x > 0 ? center - step : center;
				int right = x < w - 1 ? center + step : center;
				for (int c = 0; c < channels; c++)
				{
					row[(x * channels) + c] = this.Weigh(buffer[left + c], buffer[center + c], buffer[right + c]);
				}
			}
		}

		// C++ calc_value, in the same order of operations so the sums match to the bit.
		private byte Weigh(byte v1, byte v2, byte v3)
		{
			return (byte)Util.uround((this.g1 * v1) + (this.g0 * v2) + (this.g1 * v3));
		}
	}
}
