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
using MatterHackers.Agg.Font;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// A face's metrics as usvg 0.45.1's load_font resolves them from ttf-parser, in font units: the decoration
	/// lines and sub/super offsets come from the post and OS/2 tables, with usvg's fallbacks where a table is missing.
	/// </summary>
	internal readonly struct SvgFontMetrics
	{
		public readonly double XHeight;
		public readonly double UnderlinePosition;
		public readonly double UnderlineThickness;
		public readonly double LineThroughPosition;
		public readonly double SubscriptOffset;
		public readonly double SuperscriptOffset;

		public SvgFontMetrics(TypeFace face)
		{
			int unitsPerEm = face.UnitsPerEm;
			int xHeight = face.OS2XHeight ?? face.X_height;
			if (xHeight <= 0)
			{
				// Firefox's 45% of the height, as usvg falls back.
				xHeight = (int)((face.Ascent - face.Descent) * 0.45);
			}

			XHeight = xHeight;
			LineThroughPosition = face.StrikeoutPosition ?? xHeight / 2;
			int? thickness = face.PostUnderlineThickness ?? (face.Underline_thickness > 0 ? face.Underline_thickness : null);
			bool hasUnderline = face.PostUnderlineThickness.HasValue || face.Underline_thickness > 0;
			UnderlinePosition = hasUnderline ? face.Underline_position : -(short)unitsPerEm / 9;
			UnderlineThickness = thickness > 0 ? thickness.Value : unitsPerEm / 12;

			// usvg's generic fallbacks divide by 0.2 and 0.4 (Inkscape's 20% and 40%, inverted); kept as it has them.
			SubscriptOffset = face.SubscriptYOffset ?? Math.Round(unitsPerEm / 0.2);
			SuperscriptOffset = face.SuperscriptYOffset ?? Math.Round(unitsPerEm / 0.4);
		}
	}
}
