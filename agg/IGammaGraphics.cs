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

namespace MatterHackers.Agg
{
	/// <summary>
	/// A <see cref="Graphics2D"/> that can apply AGG's gammas without a software rasterizer or readable pixels:
	/// a rasterizer gamma on fill coverage (<c>rasterizer.gamma(...)</c>) and a per-channel table over pixels
	/// already drawn (pixfmt <c>apply_gamma_inv</c>, or quantising to a packed pixel format).
	/// </summary>
	public interface IGammaGraphics
	{
		/// <summary>
		/// Runs <paramref name="drawCoverage"/> into a transparent layer, maps each pixel's coverage (the layer's
		/// alpha) through <paramref name="gamma"/> and composites <paramref name="color"/> at that coverage times
		/// its own alpha, source-over - what a software rasterizer with <c>gamma(gamma)</c> set does to a fill.
		/// </summary>
		/// <param name="gamma">The coverage curve, 0..1 to 0..1.</param>
		/// <param name="color">The fill colour; its alpha scales the mapped coverage, as the rasterizer's does.</param>
		/// <param name="drawCoverage">The shapes, drawn opaque (their colour is ignored). Overlapping shapes merge
		/// into one coverage before the curve, so draw shapes that must stay separate in separate calls.</param>
		void DrawWithCoverageGamma(IGammaFunction gamma, Color color, Action drawCoverage);

		/// <summary>
		/// Replaces the red, green and blue of every pixel already drawn inside <paramref name="region"/> (in the
		/// current transform) by their entries in the tables; alpha is left alone. As pixfmt <c>apply_gamma_inv</c>
		/// with <see cref="GammaLookUpTable.inv"/> tables, or a quantisation to fewer bits per channel.
		/// </summary>
		/// <param name="region">The area to map; outside it the frame is untouched.</param>
		/// <param name="red">256 entries: the new red for each old red.</param>
		/// <param name="green">256 entries, for green.</param>
		/// <param name="blue">256 entries, for blue.</param>
		/// <exception cref="NotSupportedException">The surface cannot read back what it has drawn.</exception>
		void MapChannels(RectangleDouble region, byte[] red, byte[] green, byte[] blue);
	}
}
