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
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg
{
	/// <summary>Which of the software blurs <see cref="IBlurGraphics.BlurBox"/> reproduces.</summary>
	public enum BlurKind
	{
		/// <summary><see cref="Image.stack_blur"/> of the colour channels (C++ <c>stack_blur_calc_rgb</c>), radius rounded.</summary>
		Stack,

		/// <summary><see cref="Image.RecursiveBlur"/> of the colour channels (C++ <c>recursive_blur</c>, a true Gaussian).</summary>
		Recursive,

		/// <summary>simple_blur's span generator: the truncated mean of the 3x3 block; the radius is ignored.</summary>
		Box3x3,

		/// <summary><see cref="Image.SlightBlur"/>: a Gaussian reaching only the adjacent pixels.</summary>
		Slight,
	}

	/// <summary>
	/// A <see cref="Graphics2D"/> that can draw blurred - what a software surface does by drawing and then running
	/// <see cref="Image.stack_blur"/> over the pixels, for a surface (the GPU) that cannot read its pixels back
	/// mid-frame.
	/// </summary>
	public interface IBlurGraphics
	{
		/// <summary>
		/// Runs <paramref name="draw"/> into a transparent layer, stack-blurs the layer by <paramref name="radius"/>
		/// (rounded, as software stack_blur takes it; below 1 no blur) and composites it source-over. The draws are
		/// one layer and the blur is taken on premultiplied colour, so the result matches blurring the frame
		/// wherever the background under the blur is uniform.
		/// </summary>
		/// <param name="radius">The stack blur radius, in the current transform's units (scaled by its average scale, as the draws are).</param>
		/// <param name="draw">The draws to blur.</param>
		/// <param name="alphaToColor">When given (256 entries), each blurred pixel takes its colour from this table by
		/// its alpha, as <c>BlendFromLut</c> colours a blurred gray coverage image; the layer's own colour is ignored.</param>
		void DrawBlurred(double radius, Action draw, Color[] alphaToColor = null);

		/// <summary>
		/// Stack-blurs what is already drawn under <paramref name="region"/> (in the current transform) by
		/// <paramref name="radius"/>: the frame is blurred as software stack_blur blurs the whole image, then shown
		/// only inside the region, its anti-aliased edge mixing the blurred and the unblurred frame.
		/// </summary>
		/// <param name="region">Where the blur shows.</param>
		/// <param name="radius">The stack blur radius, as for <see cref="DrawBlurred"/>.</param>
		/// <exception cref="NotSupportedException">The surface cannot read back what it has drawn.</exception>
		void BlurUnder(IVertexSource region, double radius);

		/// <summary>
		/// Blurs what is already drawn under <paramref name="region"/> (in the current transform) with one of the
		/// software blurs, as that blur runs on a sub-image: the image is the whole device pixels the region's bounds
		/// touch (for <see cref="BlurKind.Box3x3"/> one pixel more each way, the ring its taps read), clipped to the
		/// target, and taps past its edge repeat its edge pixels - except for <see cref="BlurKind.Box3x3"/>, which
		/// leaves a pixel whose block would leave it as it is. The result shows inside the region, its anti-aliased
		/// edge mixing the blurred and the unblurred frame. Only the colour channels in <paramref name="channels"/>
		/// are blurred; alpha keeps the frame's own, as the software rgb blurs do.
		/// </summary>
		/// <param name="region">Where the blur shows; a pixel-aligned rectangle blurs exactly that box.</param>
		/// <param name="radius">The blur radius, scaled by the current transform's average scale.</param>
		/// <param name="kind">Which software blur to reproduce.</param>
		/// <param name="channels">The colour channels to blur (<see cref="ColorChannels.Alpha"/> is ignored).</param>
		/// <exception cref="NotSupportedException">The surface cannot read back what it has drawn, or does not do
		/// this blur.</exception>
		void BlurBox(IVertexSource region, double radius, BlurKind kind, ColorChannels channels = ColorChannels.All)
		{
			throw new NotSupportedException($"{this.GetType().Name} does not blur with {kind}.");
		}
	}
}
