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
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>A fill through a rasterizer gamma (C++ <c>ras.gamma(...)</c>) on whichever surface the demo draws on.</summary>
	internal static class GammaFill
	{
		/// <summary>
		/// Fills <paramref name="path"/> with its coverage mapped through <paramref name="gamma"/>: through the software
		/// rasterizer's gamma, put back to none afterwards (it cannot be read back, and every other fill assumes none),
		/// or through <see cref="IGammaGraphics.DrawWithCoverageGamma"/> on the GPU.
		/// </summary>
		public static void Render(Graphics2D graphics, IVertexSource path, Color color, IGammaFunction gamma)
		{
			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			if (rasterizer != null)
			{
				rasterizer.gamma(gamma);
				try
				{
					graphics.Render(path, color);
				}
				finally
				{
					rasterizer.gamma(new gamma_none());
				}
			}
			else if (graphics is IGammaGraphics gammaGraphics)
			{
				gammaGraphics.DrawWithCoverageGamma(gamma, color, () => graphics.Render(path, Color.White));
			}
			else
			{
				graphics.Render(path, color);
			}
		}

		/// <summary>
		/// pixfmt <c>apply_gamma_inv</c> over the demo's frame on a GPU surface (<see cref="IGammaGraphics.MapChannels"/>);
		/// false, doing nothing, on a surface without it.
		/// </summary>
		public static bool ApplyGammaInv(Graphics2D graphics, GammaLookUpTable gamma, int width, int height)
		{
			if (!(graphics is IGammaGraphics gammaGraphics))
			{
				return false;
			}

			byte[] inverse = Table(v => gamma.inv(v));
			gammaGraphics.MapChannels(new RectangleDouble(0, 0, width, height), inverse, inverse, inverse);
			return true;
		}

		/// <summary>The 256-entry channel table <see cref="IGammaGraphics.MapChannels"/> takes, each value through <paramref name="map"/>.</summary>
		public static byte[] Table(Func<int, byte> map)
		{
			var table = new byte[256];
			for (int i = 0; i < 256; i++)
			{
				table[i] = map(i);
			}

			return table;
		}
	}
}
