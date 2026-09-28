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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>How a render measured against its reference.png: whether it passes, and the share of pixels that did not.</summary>
	public readonly record struct SvgCompareResult(bool Pass, double Ratio, int MismatchedPixels, int MaxDelta);

	/// <summary>
	/// agg-gui's svg/compare.rs: the one rule for "close enough to reference.png". Opaque colour must match exactly,
	/// alpha may be off by one level (antialiased coverage), translucent colour by two premultiplied levels, and
	/// every pixel composited over white must sit within 5 RGB units; a render passes when at most 0.1% of its
	/// pixels break any of those.
	/// </summary>
	public static class SvgCompare
	{
		public const int OpaqueRgbTolerance = 0;
		public const int AlphaTolerance = 1;
		public const int TranslucentRgbTolerance = 2;
		public const double VisualRgbTolerance = 5;
		public const double MismatchRatio = .001;

		/// <summary>Compares two straight-alpha RGBA byte arrays of the same size (see <see cref="ToRgba"/>).</summary>
		public static SvgCompareResult Compare(byte[] rendered, byte[] reference)
		{
			if (rendered.Length != reference.Length || rendered.Length % 4 != 0)
			{
				return new SvgCompareResult(false, 1, rendered.Length / 4, 255);
			}

			int mismatched = 0;
			int maxDelta = 0;
			int visualThresholdSq = (int)Math.Ceiling(VisualRgbTolerance * VisualRgbTolerance);
			for (int i = 0; i < rendered.Length; i += 4)
			{
				int rgbDelta = Math.Max(Math.Abs(rendered[i] - reference[i]), Math.Max(Math.Abs(rendered[i + 1] - reference[i + 1]), Math.Abs(rendered[i + 2] - reference[i + 2])));
				int alphaDelta = Math.Abs(rendered[i + 3] - reference[i + 3]);
				maxDelta = Math.Max(maxDelta, Math.Max(rgbDelta, alphaDelta));

				bool opaque = rendered[i + 3] == 255 && reference[i + 3] == 255;
				bool failed = (opaque && rgbDelta > OpaqueRgbTolerance)
					|| alphaDelta > AlphaTolerance
					|| (!opaque && PremultipliedDelta(rendered, reference, i) > TranslucentRgbTolerance)
					|| VisualDistanceSqOverWhite(rendered, reference, i) > visualThresholdSq;
				if (failed)
				{
					mismatched++;
				}
			}

			double ratio = mismatched / (double)Math.Max(1, rendered.Length / 4);
			return new SvgCompareResult(ratio <= MismatchRatio, ratio, mismatched, maxDelta);
		}

		/// <summary>
		/// The image's pixels as straight-alpha RGBA, top row first - the layout of a decoded PNG. ImageBuffer rows
		/// run bottom-up, so row 0 of the result is the buffer's top row.
		/// </summary>
		public static byte[] ToRgba(ImageBuffer image)
		{
			var rgba = new byte[image.Width * image.Height * 4];
			byte[] buffer = image.GetBuffer();
			int index = 0;
			for (int row = 0; row < image.Height; row++)
			{
				int offset = image.GetBufferOffsetXY(0, image.Height - 1 - row);
				for (int x = 0; x < image.Width; x++, offset += 4)
				{
					rgba[index++] = buffer[offset + ImageBuffer.OrderR];
					rgba[index++] = buffer[offset + ImageBuffer.OrderG];
					rgba[index++] = buffer[offset + ImageBuffer.OrderB];
					rgba[index++] = buffer[offset + ImageBuffer.OrderA];
				}
			}

			return rgba;
		}

		/// <summary>
		/// agg-gui's diff view, opaque, so matching pixels are black and every miss glows: per channel the
		/// difference of the two pixels composited over white - what an eye sees - so colour hidden under zero
		/// alpha, which PNG encoders store arbitrarily, does not show.
		/// </summary>
		public static ImageBuffer DiffImage(ImageBuffer reference, ImageBuffer rendered)
		{
			var diff = new ImageBuffer(reference.Width, reference.Height);
			if (rendered.Width != reference.Width || rendered.Height != reference.Height)
			{
				return diff;
			}

			for (int y = 0; y < reference.Height; y++)
			{
				for (int x = 0; x < reference.Width; x++)
				{
					Color a = reference.GetPixel(x, y);
					Color b = rendered.GetPixel(x, y);
					diff.SetPixel(x, y, new Color(
						Math.Abs(OverWhite(a.red, a.alpha) - OverWhite(b.red, b.alpha)),
						Math.Abs(OverWhite(a.green, a.alpha) - OverWhite(b.green, b.alpha)),
						Math.Abs(OverWhite(a.blue, a.alpha) - OverWhite(b.blue, b.alpha)),
						255));
				}
			}

			return diff;
		}

		private static int PremultipliedDelta(byte[] a, byte[] b, int i)
		{
			int delta = 0;
			for (int c = 0; c < 3; c++)
			{
				delta = Math.Max(delta, Math.Abs(Premultiply(a[i + c], a[i + 3]) - Premultiply(b[i + c], b[i + 3])));
			}

			return delta;
		}

		private static int VisualDistanceSqOverWhite(byte[] a, byte[] b, int i)
		{
			int sum = 0;
			for (int c = 0; c < 3; c++)
			{
				int d = OverWhite(a[i + c], a[i + 3]) - OverWhite(b[i + c], b[i + 3]);
				sum += d * d;
			}

			return sum;
		}

		private static int Premultiply(int channel, int alpha) => (channel * alpha + 127) / 255;

		private static int OverWhite(int channel, int alpha) => (channel * alpha + 255 * (255 - alpha) + 127) / 255;
	}
}
