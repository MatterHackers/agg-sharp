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

using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// The glyph "a" several AGG examples draw (blur.cpp, conv_contour.cpp, rasterizer_compound.cpp's
	/// compose_path): two closed contours of lines and curve3s, in font units, untransformed.
	/// </summary>
	public static class GlyphA
	{
		/// <summary>A new path holding the glyph; flatten it (C++ conv_curve) after any transform.</summary>
		public static VertexStorage Create()
		{
			var p = new VertexStorage();
			p.MoveTo(28.47, 6.45);
			p.Curve3(21.58, 1.12, 19.82, 0.29);
			p.Curve3(17.19, -0.93, 14.21, -0.93);
			p.Curve3(9.57, -0.93, 6.57, 2.25);
			p.Curve3(3.56, 5.42, 3.56, 10.60);
			p.Curve3(3.56, 13.87, 5.03, 16.26);
			p.Curve3(7.03, 19.58, 11.99, 22.51);
			p.Curve3(16.94, 25.44, 28.47, 29.64);
			p.LineTo(28.47, 31.40);
			p.Curve3(28.47, 38.09, 26.34, 40.58);
			p.Curve3(24.22, 43.07, 20.17, 43.07);
			p.Curve3(17.09, 43.07, 15.28, 41.41);
			p.Curve3(13.43, 39.75, 13.43, 37.60);
			p.LineTo(13.53, 34.77);
			p.Curve3(13.53, 32.52, 12.38, 31.30);
			p.Curve3(11.23, 30.08, 9.38, 30.08);
			p.Curve3(7.57, 30.08, 6.42, 31.35);
			p.Curve3(5.27, 32.62, 5.27, 34.81);
			p.Curve3(5.27, 39.01, 9.57, 42.53);
			p.Curve3(13.87, 46.04, 21.63, 46.04);
			p.Curve3(27.59, 46.04, 31.40, 44.04);
			p.Curve3(34.28, 42.53, 35.64, 39.31);
			p.Curve3(36.52, 37.21, 36.52, 30.71);
			p.LineTo(36.52, 15.53);
			p.Curve3(36.52, 9.13, 36.77, 7.69);
			p.Curve3(37.01, 6.25, 37.57, 5.76);
			p.Curve3(38.13, 5.27, 38.87, 5.27);
			p.Curve3(39.65, 5.27, 40.23, 5.62);
			p.Curve3(41.26, 6.25, 44.19, 9.18);
			p.LineTo(44.19, 6.45);
			p.Curve3(38.72, -0.88, 33.74, -0.88);
			p.Curve3(31.35, -0.88, 29.93, 0.78);
			p.Curve3(28.52, 2.44, 28.47, 6.45);
			p.ClosePolygon();

			p.MoveTo(28.47, 9.62);
			p.LineTo(28.47, 26.66);
			p.Curve3(21.09, 23.73, 18.95, 22.51);
			p.Curve3(15.09, 20.36, 13.43, 18.02);
			p.Curve3(11.77, 15.67, 11.77, 12.89);
			p.Curve3(11.77, 9.38, 13.87, 7.06);
			p.Curve3(15.97, 4.74, 18.70, 4.74);
			p.Curve3(22.41, 4.74, 28.47, 9.62);
			p.ClosePolygon();
			return p;
		}
	}
}
