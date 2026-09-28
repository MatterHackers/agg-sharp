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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg
{
	/// <summary>How a pattern fill tiles its image past the image's edge, on one axis.</summary>
	public enum ImageWrapMode
	{
		/// <summary>The image repeats: 0 .. n-1, 0 .. n-1 (C++ wrap_mode_repeat).</summary>
		Repeat,

		/// <summary>The image repeats mirrored on every other tile: 0 .. n-1, n-1 .. 0 (C++ wrap_mode_reflect).</summary>
		Reflect,
	}

	/// <summary>
	/// A <see cref="Graphics2D"/> that can fill a path with a tiled image - C++ AGG's span_pattern / image_accessor_wrap
	/// fill. <see cref="ImageGraphics2D"/> does it with a span generator, the GPU surface with a textured mesh.
	/// </summary>
	public interface IPatternFillGraphics
	{
		/// <summary>
		/// Fills <paramref name="path"/> (in the current transform, like any other fill, and anti-aliased) with
		/// <paramref name="image"/> tiled over the plane.
		/// </summary>
		/// <param name="path">The shape to fill.</param>
		/// <param name="image">A 32 bit image. Its bytes are composited as <see cref="Graphics2D.Render(IImageByte, double, double)"/>
		/// composites them: by the destination's blender in software, as straight alpha on the GPU.</param>
		/// <param name="imageToScreen">Maps image pixels (row 0 at the bottom) to the target's pixels; the current
		/// transform does not move the image. Each target pixel takes its nearest image pixel.</param>
		/// <param name="wrapX">How the image tiles horizontally.</param>
		/// <param name="wrapY">How the image tiles vertically.</param>
		void FillPathWithImage(IVertexSource path, IImageByte image, Affine imageToScreen, ImageWrapMode wrapX, ImageWrapMode wrapY);
	}
}
