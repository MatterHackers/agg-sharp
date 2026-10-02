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

namespace MatterHackers.Agg.Font
{
	/// <summary>
	/// The four glyph-shaping settings - Width, Interval, Faux Weight and Faux Italic - as one immutable value, so a
	/// <see cref="StyledTypeFace"/> can be given its own (<see cref="StyledTypeFace.Style"/>) instead of following the
	/// process-wide <see cref="TextStyleSettings"/>. Each value is clamped to the range
	/// <see cref="TextStyleSettings"/> allows, and means what the setting of the same name there means.
	/// </summary>
	/// <remarks>
	/// A record, so two styles with the same values are equal: a text run's render identity names its style, and an
	/// equal style must give back the cached raster.
	/// </remarks>
	public sealed record GlyphStyle
	{
		/// <summary>Every setting at its default: glyphs exactly as the font draws them.</summary>
		public static readonly GlyphStyle Identity = new GlyphStyle();

		public GlyphStyle(double width = 1, double interval = 0, double fauxWeight = 0, double fauxItalic = 0)
		{
			this.Width = Math.Clamp(width, .75, 1.25);
			this.Interval = Math.Clamp(interval, -.2, .2);
			this.FauxWeight = Math.Clamp(fauxWeight, -1, 1);
			this.FauxItalic = Math.Clamp(fauxItalic, -1, 1);
		}

		/// <summary>Horizontal scale of every glyph outline; advances are unchanged. 0.75 - 1.25.</summary>
		public double Width { get; }

		/// <summary>Extra letter spacing added to every advance, as a fraction of the em. -0.2 - 0.2.</summary>
		public double Interval { get; }

		/// <summary>Synthetic weight: positive heavier, negative lighter; within 0.05 of zero nothing is applied. -1 - 1.</summary>
		public double FauxWeight { get; }

		/// <summary>Synthetic slant: each outline is sheared by a third of this. -1 - 1.</summary>
		public double FauxItalic { get; }

		/// <summary>Whether every setting is at its default, so a glyph takes the plain, unstyled path.</summary>
		public bool IsIdentity => this.Width == 1 && this.Interval == 0 && this.FauxWeight == 0 && this.FauxItalic == 0;

		/// <summary>
		/// How far, in pixels, Faux Weight moves each outline edge outward at <paramref name="emSizeInPixels"/>:
		/// positive grows the ink, negative shrinks it, 0 in the dead zone.
		/// </summary>
		/// <remarks>
		/// agg-gui's <c>faux_weight * size / 15</c>, the divisor its reference demo's slider-to-pixels conversion.
		/// Which sign of contour width that needs depends on the glyph's winding; see
		/// <see cref="StyledTypeFace"/>.
		/// </remarks>
		public double FauxWeightGrowthInPixels(double emSizeInPixels)
		{
			return Math.Abs(this.FauxWeight) < .05 ? 0 : this.FauxWeight * emSizeInPixels / 15;
		}
	}
}
