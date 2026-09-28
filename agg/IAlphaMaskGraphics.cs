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

namespace MatterHackers.Agg
{
	/// <summary>
	/// A <see cref="Graphics2D"/> that can draw through an alpha mask - what a software surface does with an
	/// <see cref="AlphaMaskAdaptor"/> over its pixels (C++ pixfmt_amask_adaptor / scanline_u8_am), for a surface
	/// (the GPU) whose pixels cannot be wrapped.
	/// </summary>
	public interface IAlphaMaskGraphics
	{
		/// <summary>
		/// Runs <paramref name="draw"/> into a transparent layer, multiplies the layer by <paramref name="mask"/> and
		/// composites it source-over. The mask's pixel (0, 0) sits at the current transform's origin and each mask pixel
		/// covers one unit of it, so a mask built the size of the frame lines up with the frame's pixels; outside the mask
		/// nothing shows, as with <see cref="AlphaMaskByteClipped"/>. The draws are masked as one layer, where software
		/// masks each draw: the two agree wherever the mask is 0 or 255, or the draws do not overlap.
		/// </summary>
		/// <param name="mask">The coverage: the byte of an 8-bit image, or the alpha of a 32-bit one. 0 hides, 255 shows.</param>
		/// <param name="draw">The draws to mask.</param>
		void DrawMasked(IImageByte mask, Action draw);
	}
}
