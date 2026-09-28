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

using MatterHackers.Agg.Image;

namespace MatterHackers.Agg
{
	/// <summary>
	/// C++ AGG's floating point to 8-bit color conversion, for ports that must match C++ byte for byte.
	/// </summary>
	public static class Rgba8
	{
		/// <summary>
		/// C++ <c>rgba8(rgba(r, g, b, a))</c>: each channel (0 to 1) times 255, rounded half up. No sRGB
		/// conversion; for colors C++ names as <c>srgba8</c> use <see cref="SrgbLut.FromSrgba8"/>.
		/// </summary>
		public static Color FromRgba(double r, double g, double b, double a = 1.0)
		{
			return new Color(Round(r), Round(g), Round(b), Round(a));

			static int Round(double v) => (int)((v * 255) + 0.5);
		}

		/// <summary>
		/// C++ <c>rgba8::gradient</c>: <paramref name="from"/> moved toward <paramref name="to"/> by
		/// <paramref name="k"/> (0 to 1), which is rounded to 0..255 first and then applied to every channel,
		/// alpha included, with rgba8's rounded integer lerp.
		/// </summary>
		public static Color Gradient(Color from, Color to, double k)
		{
			// C++ uround(k * base_mask), then passed to lerp as an int8u.
			int ik = (byte)(uint)((k * 255) + 0.5);
			return new Color(
				Rgba8Math.Lerp(from.red, to.red, ik),
				Rgba8Math.Lerp(from.green, to.green, ik),
				Rgba8Math.Lerp(from.blue, to.blue, ik),
				Rgba8Math.Lerp(from.alpha, to.alpha, ik));
		}
	}
}
