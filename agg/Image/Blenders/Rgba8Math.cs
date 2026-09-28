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

using System.Runtime.CompilerServices;

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// The 8-bit blend arithmetic of C++ AGG 2.4's rgba8 (agg_color_rgba.h). They round where a plain
	/// "&gt;&gt; 8" truncates, and they are exact at the ends - Multiply(a, 255) == a and Lerp(p, q, 255) == q -
	/// so a full cover or an opaque color changes nothing.
	/// <para>
	/// Converted to them, and byte-identical to C++ AGG: BlenderBGRA, BlenderRGBA, BlenderBGR, blender_gray's
	/// blend step, DoCopyOrBlend's cover-times-alpha, BlenderPreMultBGR (C++ blender_rgb_pre) and BlenderPreMultBGRA
	/// (C++ blender_rgba_pre, at every cover); BlenderGammaBGRA and BlenderGammaBGR are C++ blender_rgb_gamma.
	/// BlenderPolyColorPreMultBGRA blends by the same prelerp but has no C++ counterpart (it leaves destination
	/// alpha alone). blenderGrayFromRed, blenderGrayClampedMax, BlenderBGRAHalfHalf and BlenderBGRAExactCopy fold
	/// a cover into alpha with Multiply, as ImageBuffer's solid blends did before handing them the cover, but still
	/// blend with a plain shift, to be converted when a demo needs them.
	/// blender_gray's color-to-gray weights (77/151/28) also differ from C++ gray8's (55/184/18).
	/// </para>
	/// </summary>
	public static class Rgba8Math
	{
		/// <summary>a * b / 255, rounded - C++ rgba8::multiply, which is also its mult_cover for an 8-bit cover.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Multiply(int a, int b)
		{
			int t = a * b + 128;
			return ((t >> 8) + t) >> 8;
		}

		/// <summary>Moves <paramref name="p"/> toward <paramref name="q"/> by <paramref name="a"/>/255, rounded - C++ rgba8::lerp.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Lerp(int p, int q, int a)
		{
			int t = (q - p) * a + 128 - (p > q ? 1 : 0);
			return p + (((t >> 8) + t) >> 8);
		}

		/// <summary>
		/// Moves <paramref name="p"/> toward <paramref name="q"/>, which is already premultiplied by
		/// <paramref name="a"/> - C++ rgba8::prelerp. Blending alpha is Prelerp(dstAlpha, alpha, alpha).
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Prelerp(int p, int q, int a)
		{
			return p + q - Multiply(p, a);
		}
	}
}
