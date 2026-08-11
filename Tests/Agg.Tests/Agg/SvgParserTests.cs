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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.SvgTools;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Covers <see cref="SvgParser.ParseSvgDString"/> path command handling.
	/// </summary>
	public class SvgParserTests
	{
		/// <summary>
		/// The 'e' from the MatterHackers wordmark (TestData\Svg\matterhackers_wordmark.svg).
		/// The load bearing part is "...a1.06,1.06,0,0,0-1,1.06s0,.09,0,.13c..." where a smooth cubic 's'
		/// directly follows an elliptical arc 'a'. Every coordinate in this glyph lies inside
		/// x [581.29, 738.11], y [173.89, 341.86].
		/// </summary>
		private const string EGlyphDString = "M581.29,257.57c0-47.11,31.91-83.68,78.4-83.68,50.82,0,78.07,38.56,78.4,94.7a1.05,1.05,0,0,1-1,1.07H624a1.06,1.06,0,0,0-1,1.06s0,.09,0,.13c3.46,23.78,17,38.48,40.43,38.48,16.13,0,25.68-7.17,30.43-18.88a1,1,0,0,1,1-.65h39.51a1,1,0,0,1,1.06,1,1,1,0,0,1,0,.24C729,318.71,704,341.86,663.71,341.86,611,341.86,581.29,305,581.29,257.57Z";

		/// <summary>
		/// A smooth cubic that follows an arc must start from a control point coincident with the
		/// current point (SVG 1.1 section 8.3.6). Reflecting a stale control point instead threw a
		/// control point ~73 units left of the glyph, which rendered as a spike crossing the 'e'.
		/// </summary>
		[Test]
		public async Task SmoothCurveAfterArcDoesNotEscapeGlyphBounds()
		{
			var bounds = new VertexStorage(EGlyphDString).GetBounds();

			await Assert.That(bounds.Left).IsEqualTo(581.29).Within(0.01);
			await Assert.That(bounds.Right).IsEqualTo(738.11).Within(0.01);
			await Assert.That(bounds.Bottom).IsEqualTo(173.89).Within(0.01);
			await Assert.That(bounds.Top).IsEqualTo(341.86).Within(0.01);
		}

		/// <summary>
		/// The minimal form of the wordmark defect: 'C' then 'a' then 's'. Because an arc is emitted as
		/// Curve4 vertices, "was the last stored vertex a Curve4" is not a valid test for "was the previous
		/// path command a cubic" - the previous command has to be tracked directly.
		/// </summary>
		[Test]
		public async Task SmoothCurveAfterArcStartsAtCurrentPoint()
		{
			// C ends at (20,-20) with second control point (10,-20), the arc then moves to (30,-10).
			var storage = new VertexStorage("M0,0C0,-10,10,-20,20,-20a10,10,0,0,1,10,10s10,10,20,20");

			var curve = LastCurve4Triple(storage);

			// Not Reflect((10,-20), (30,-10)) == (50,0), which is what the stale control point produced.
			await Assert.That(curve[0]).IsEqualTo(new Vector2(30, -10));
			await Assert.That(curve[1]).IsEqualTo(new Vector2(40, 0));
			await Assert.That(curve[2]).IsEqualTo(new Vector2(50, 10));
		}

		/// <summary>
		/// The companion to <see cref="SmoothCurveAfterArcStartsAtCurrentPoint"/>: a smooth cubic that
		/// really does follow a cubic must still reflect that cubic's second control point.
		/// </summary>
		[Test]
		public async Task SmoothCurveAfterCubicReflectsPreviousControlPoint()
		{
			var storage = new VertexStorage("M0,0C0,10,10,10,10,0s10,-10,20,0");

			var curve = LastCurve4Triple(storage);

			// Reflect((10,10), (10,0)) == (10,-10)
			await Assert.That(curve[0]).IsEqualTo(new Vector2(10, -10));
			await Assert.That(curve[1]).IsEqualTo(new Vector2(20, -10));
			await Assert.That(curve[2]).IsEqualTo(new Vector2(30, 0));
		}

		/// <summary>
		/// End to end check against the real file so the regression is caught at the SVG level, not just
		/// for a hand extracted d string.
		/// </summary>
		[Test]
		public async Task WordmarkGlyphsStayWithinTheirOwnExtents()
		{
			var elements = SvgParser.Parse(WordmarkPath(), flipY: false);

			// The wordmark is one <path> per letter group; the 4th is the 'e' of "Matter".
			var eGlyph = elements[3].VertexSource.GetBounds();

			await Assert.That(eGlyph.Left).IsEqualTo(581.29).Within(0.01);
		}

		private static List<Vector2> LastCurve4Triple(VertexStorage storage)
		{
			var curve4Points = storage.Vertices()
				.Where(v => v.Command == FlagsAndCommand.Curve4)
				.Select(v => v.Position)
				.ToList();

			return curve4Points.Skip(curve4Points.Count - 3).ToList();
		}

		/// <summary>
		/// Walks up from the test binary to the repo tree copy of TestData, matching the convention the
		/// rest of the test data in this project uses.
		/// </summary>
		private static string WordmarkPath()
		{
			string probe = Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
			for (int up = 0; up < 6 && probe != null; up++)
			{
				var candidate = Path.Combine(probe, "TestData", "Svg", "matterhackers_wordmark.svg");
				if (File.Exists(candidate))
				{
					return candidate;
				}

				probe = Path.GetDirectoryName(probe);
			}

			throw new FileNotFoundException("Could not find TestData\\Svg\\matterhackers_wordmark.svg above " + AppContext.BaseDirectory);
		}
	}
}
