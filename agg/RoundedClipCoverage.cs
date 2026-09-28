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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg
{
	/// <summary>
	/// How much of a pixel a rounded-rectangle clip keeps - agg-gui's rounded layer clip. Shared by the CPU
	/// backbuffer, which scales its pixels by it, and the GPU layer composite, whose anti-aliased edge fringe
	/// ramps over the same 1 pixel band, so a rounded window cuts its content the same way on both.
	/// </summary>
	public static class RoundedClipCoverage
	{
		/// <summary>
		/// 1 inside <paramref name="bounds"/> rounded by <paramref name="radius"/>, 0 outside, and a linear
		/// ramp over the 1 pixel band centred on the edge: clamp(0.5 - signed distance, 0, 1).
		/// </summary>
		/// <param name="bounds">The clip rectangle.</param>
		/// <param name="radius">Corner radius, clamped to half the shorter side.</param>
		/// <param name="x">The point to test, normally a pixel centre.</param>
		/// <param name="y">The vertical twin of <paramref name="x"/>.</param>
		public static double Coverage(RectangleDouble bounds, double radius, double x, double y)
		{
			radius = ClampRadius(bounds, radius);

			// Signed distance to a rounded box: the distance from the radius-shrunk box, less the radius.
			double dx = Math.Abs(x - ((bounds.Left + bounds.Right) / 2)) - ((bounds.Width / 2) - radius);
			double dy = Math.Abs(y - ((bounds.Bottom + bounds.Top) / 2)) - ((bounds.Height / 2) - radius);
			double ox = Math.Max(dx, 0);
			double oy = Math.Max(dy, 0);
			double signedDistance = Math.Sqrt((ox * ox) + (oy * oy)) + Math.Min(Math.Max(dx, dy), 0) - radius;

			return Math.Clamp(0.5 - signedDistance, 0, 1);
		}

		/// <summary>The radius a clip of <paramref name="bounds"/> really rounds by: never negative and never
		/// more than half the shorter side.</summary>
		public static double ClampRadius(RectangleDouble bounds, double radius)
		{
			return Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
		}

		/// <summary>
		/// Scales every pixel of a premultiplied 32 bit <paramref name="image"/> by its <see cref="Coverage"/>
		/// at the pixel's centre - all four channels, which is what fading a premultiplied pixel means.
		/// </summary>
		/// <param name="image">A premultiplied BGRA image (a widget backbuffer).</param>
		/// <param name="bounds">The clip rectangle, in the image's pixels.</param>
		/// <param name="radius">Corner radius in the image's pixels.</param>
		public static void Apply(ImageBuffer image, RectangleDouble bounds, double radius)
		{
			byte[] buffer = image.GetBuffer();

			// Only the bands within reach of an edge can lose coverage: rows near the top and bottom are walked
			// whole, every other row only its left and right bands, so the cost is the rim, not the area.
			double reach = ClampRadius(bounds, radius) + 0.5;
			int width = image.Width;
			int leftBandEnd = Math.Clamp((int)Math.Ceiling(bounds.Left + reach), 0, width);
			int rightBandStart = Math.Clamp((int)Math.Floor(bounds.Right - reach), leftBandEnd, width);
			for (int y = 0; y < image.Height; y++)
			{
				double centerY = y + 0.5;
				int rowOffset = image.GetBufferOffsetY(y);
				if (centerY >= bounds.Bottom + reach && centerY <= bounds.Top - reach)
				{
					ApplyToSpan(buffer, rowOffset, 0, leftBandEnd, centerY, bounds, radius);
					ApplyToSpan(buffer, rowOffset, rightBandStart, width, centerY, bounds, radius);
				}
				else
				{
					ApplyToSpan(buffer, rowOffset, 0, width, centerY, bounds, radius);
				}
			}

			image.MarkImageChanged();
		}

		private static void ApplyToSpan(byte[] buffer, int rowOffset, int start, int end, double centerY, RectangleDouble bounds, double radius)
		{
			for (int x = start; x < end; x++)
			{
				double coverage = Coverage(bounds, radius, x + 0.5, centerY);
				if (coverage >= 1)
				{
					continue;
				}

				int offset = rowOffset + (x * 4);
				for (int channel = 0; channel < 4; channel++)
				{
					buffer[offset + channel] = (byte)((buffer[offset + channel] * coverage) + 0.5);
				}
			}
		}
	}
}
