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

using System.Threading.Tasks;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>The conv_contour port (Contour over ContourGenerator) against C++ AGG 2.4's agg_vcgen_contour.cpp.</summary>
	public class ContourTests
	{
		/// <summary>
		/// C++ width(w) keeps w for rewind, which sets the stroker to +w or -w by the polygon's orientation. An
		/// oriented square grown by 4 has its edges 2 out (the stroker offsets by half the width).
		/// </summary>
		[Test]
		public async Task WidthIsKeptForOrientedPolygons()
		{
			var square = new VertexStorage();
			AddSquare(square, 0, 0, 10, false, FlagsAndCommand.FlagCCW);
			var contour = new Contour(square) { Width = 4 };

			await Assert.That(contour.Width).IsEqualTo(4.0);
			await Assert.That(contour.GetBounds().Width).IsEqualTo(14.0);
		}

		/// <summary>
		/// C++ remove_all, which runs before each polygon, forgets the orientation, so with autodetection every
		/// polygon is grown by its own orientation - not by the first one's.
		/// </summary>
		[Test]
		public async Task EachPolygonAutodetectsItsOwnOrientation()
		{
			var both = new VertexStorage();
			AddSquare(both, 0, 0, 10, false, FlagsAndCommand.FlagNone);
			AddSquare(both, 20, 0, 10, true, FlagsAndCommand.FlagNone);

			var reversedAlone = new VertexStorage();
			AddSquare(reversedAlone, 20, 0, 10, true, FlagsAndCommand.FlagNone);

			RectangleDouble bothBounds = new Contour(both) { Width = 4, AutoDetectOrientation = true }.GetBounds();
			RectangleDouble aloneBounds = new Contour(reversedAlone) { Width = 4, AutoDetectOrientation = true }.GetBounds();

			await Assert.That(bothBounds.Right).IsEqualTo(aloneBounds.Right);
		}

		// Counter-clockwise (positive area) unless reversed.
		private static void AddSquare(VertexStorage path, double x, double y, double size, bool reversed, FlagsAndCommand orientation)
		{
			path.MoveTo(x, y);
			if (reversed)
			{
				path.LineTo(x, y + size);
				path.LineTo(x + size, y + size);
				path.LineTo(x + size, y);
			}
			else
			{
				path.LineTo(x + size, y);
				path.LineTo(x + size, y + size);
				path.LineTo(x, y + size);
			}

			path.ClosePolygon(orientation);
		}
	}
}
