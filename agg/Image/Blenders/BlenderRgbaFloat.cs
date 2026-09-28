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

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// A C++ AGG rgba32 blender (agg_pixfmt_rgba.h) as <see cref="PixelFormatBGRAFloat"/> drives it: one pixel of a
	/// premultiplied float BGRA buffer at a time, with and without a coverage. The two overloads are kept apart
	/// because C++ calls the uncovered one at full cover, and a * 255 / 255 in float is not always a.
	/// </summary>
	public interface IBlenderRgbaFloat
	{
		/// <summary>C++ <c>blend_pix(p, cr, cg, cb, alpha, cover)</c>.</summary>
		void BlendPix(float[] p, int offset, ColorF c, int cover);

		/// <summary>C++ <c>blend_pix(p, cr, cg, cb, alpha)</c>: the color at full cover.</summary>
		void BlendPix(float[] p, int offset, ColorF c);
	}

	/// <summary>
	/// C++ <c>blender_rgba&lt;rgba32&gt;</c>: a straight (not premultiplied) color into a premultiplied buffer. The
	/// color channels lerp toward the source by its alpha, and alpha prelerps, which is premultiplying on the fly.
	/// </summary>
	public sealed class BlenderRgbaFloat : IBlenderRgbaFloat
	{
		/// <inheritdoc/>
		public void BlendPix(float[] p, int offset, ColorF c, int cover)
		{
			BlendPix(p, offset, c.red, c.green, c.blue, RgbaFloatMath.MultCover(c.alpha, cover));
		}

		/// <inheritdoc/>
		public void BlendPix(float[] p, int offset, ColorF c)
		{
			BlendPix(p, offset, c.red, c.green, c.blue, c.alpha);
		}

		private static void BlendPix(float[] p, int offset, float r, float g, float b, float alpha)
		{
			p[offset + ImageBuffer.OrderR] = RgbaFloatMath.Lerp(p[offset + ImageBuffer.OrderR], r, alpha);
			p[offset + ImageBuffer.OrderG] = RgbaFloatMath.Lerp(p[offset + ImageBuffer.OrderG], g, alpha);
			p[offset + ImageBuffer.OrderB] = RgbaFloatMath.Lerp(p[offset + ImageBuffer.OrderB], b, alpha);
			p[offset + ImageBuffer.OrderA] = RgbaFloatMath.Prelerp(p[offset + ImageBuffer.OrderA], alpha, alpha);
		}
	}

	/// <summary>
	/// C++ <c>blender_rgba_pre&lt;rgba32&gt;</c>: a premultiplied color into a premultiplied buffer (src-over). A
	/// cover scales all four channels before the prelerp.
	/// </summary>
	public sealed class BlenderRgbaPreFloat : IBlenderRgbaFloat
	{
		/// <inheritdoc/>
		public void BlendPix(float[] p, int offset, ColorF c, int cover)
		{
			BlendPix(
				p,
				offset,
				RgbaFloatMath.MultCover(c.red, cover),
				RgbaFloatMath.MultCover(c.green, cover),
				RgbaFloatMath.MultCover(c.blue, cover),
				RgbaFloatMath.MultCover(c.alpha, cover));
		}

		/// <inheritdoc/>
		public void BlendPix(float[] p, int offset, ColorF c)
		{
			BlendPix(p, offset, c.red, c.green, c.blue, c.alpha);
		}

		private static void BlendPix(float[] p, int offset, float r, float g, float b, float alpha)
		{
			p[offset + ImageBuffer.OrderR] = RgbaFloatMath.Prelerp(p[offset + ImageBuffer.OrderR], r, alpha);
			p[offset + ImageBuffer.OrderG] = RgbaFloatMath.Prelerp(p[offset + ImageBuffer.OrderG], g, alpha);
			p[offset + ImageBuffer.OrderB] = RgbaFloatMath.Prelerp(p[offset + ImageBuffer.OrderB], b, alpha);
			p[offset + ImageBuffer.OrderA] = RgbaFloatMath.Prelerp(p[offset + ImageBuffer.OrderA], alpha, alpha);
		}
	}
}
