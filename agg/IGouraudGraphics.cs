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

using System.Collections.Generic;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg
{
	/// <summary>
	/// A <see cref="Graphics2D"/> that fills Gouraud-shaded triangles with <see cref="span_gouraud_rgba"/>'s own
	/// pixels without a software rasterizer: each pixel takes the colour the span generator gives it, at the
	/// coverage a scanline rasterizer gives it.
	/// </summary>
	/// <remarks>
	/// Each triangle is its own path (<see cref="span_gouraud"/> is a vertex source: the triangle, or the hexagon
	/// its dilation grows), in the current transform like any path. Its corners and colours are read as the span
	/// generator reads them; it does not need to be prepared.
	/// </remarks>
	public interface IGouraudGraphics
	{
		/// <summary>
		/// Fills each triangle in turn, source-over, as a scanline rasterizer with <c>gamma(coverageGamma)</c> and a
		/// span_gouraud_rgba render them one after another (C++ gouraud.cpp's <c>render_gouraud</c>).
		/// </summary>
		/// <param name="triangles">The triangles, drawn in order.</param>
		/// <param name="coverageGamma">The rasterizer gamma on each pixel's coverage, or null for none.</param>
		void FillGouraud(IReadOnlyList<span_gouraud_rgba> triangles, IGammaFunction coverageGamma = null);

		/// <summary>
		/// Fills the triangles as one compound rasterizer pass does, each a style of its own (C++ gouraud_mesh.cpp):
		/// where triangles share a pixel their coverage-weighted colours add up before the sum is drawn source-over,
		/// so a shared edge is anti-aliased once and adjacent triangles meet without a seam.
		/// </summary>
		/// <param name="triangles">The triangles, one style each.</param>
		void FillGouraudCompound(IReadOnlyList<span_gouraud_rgba> triangles);
	}
}
