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

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// The text loop trans_curve1.cpp and trans_curve2.cpp share: glyphs laid out from pen position (0, 3),
	/// cut into short pieces (conv_segmentator, scale 3) and bent by a non-linear transform, stopping once the
	/// pen passes the path's end. Each glyph is filled in black on its own, as C++ does.
	/// </summary>
	internal static class TextAlongPath
	{
		public const string Text =
			"Anti-Grain Geometry is designed as a set of loosely coupled "
			+ "algorithms and class templates united with a common idea, "
			+ "so that all the components can be easily combined. Also, "
			+ "the template based design allows you to replace any part of "
			+ "the library without the necessity to modify a single byte in "
			+ "the existing code. ";

		public static void Draw(Graphics2D graphics, ITransform path, double totalLength, CurveTextFont font)
		{
			double x = 0.0;
			double y = 3.0;
			for (int i = 0; i < Text.Length; i++)
			{
				if (x > totalLength)
				{
					break;
				}

				IVertexSource glyph = font.GlyphAt(Text[i], x, y);
				if (glyph != null)
				{
					var pieces = new Segmentator(glyph) { ApproximationScale = 3.0 };
					graphics.Render(new VertexSourceApplyTransform(pieces, path), Color.Black);
				}

				x = font.NextPenX(Text, i, x, y);
			}
		}
	}
}
