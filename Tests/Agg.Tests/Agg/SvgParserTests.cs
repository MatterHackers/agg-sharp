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
using TUnit.Assertions.Enums;
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
		/// SVG 1.1 section 8.3.3: a command after closepath without its own moveto starts a new subpath at the
		/// closed one's start. The close vertex sits at (0, 0), so with no moveto emitted the curve flattener,
		/// which takes a curve's start from the previous vertex, drew the curve out of the origin.
		/// </summary>
		[Test]
		public async Task CurveAfterClosePathStartsAtTheClosedSubpathsStart()
		{
			var storage = new VertexStorage("M10,10 L20,10 L20,20 Z Q30,10 40,20");

			var bounds = new FlattenCurves(storage).GetBounds();

			await Assert.That(bounds.Left).IsEqualTo(10).Within(.001);
			await Assert.That(bounds.Bottom).IsEqualTo(10).Within(.001);
		}

		/// <summary>
		/// The "G" of resvg's structure/image/embedded-svg sample. Like every command, v takes a list: its tail
		/// "v-.012-4.827" is two vertical lines, which the parser read as one, then threw on the second number.
		/// </summary>
		[Test]
		public async Task VerticalLineToTakesAListOfNumbers()
		{
			var storage = new VertexStorage("m73.255 69.513h11.683v11.664c0 6.452-5.226 11.678-11.669 11.678-6.441 0-11.666-5.226-11.666-11.678v-16.501h-.017c0-6.447 5.241-11.676 11.667-11.676 6.459 0 11.683 5.225 11.683 11.676h-6.849c0-2.674-2.152-4.837-4.834-4.837-2.647 0-4.82 2.163-4.82 4.837v16.501c0 2.675 2.173 4.837 4.82 4.837 2.682 0 4.834-2.162 4.834-4.827v-.012-4.827h-4.834z");

			var lines = storage.Vertices().Where(v => v.Command == FlagsAndCommand.LineTo).Select(v => v.Position).ToList();
			var curveEnd = storage.Vertices().Last(v => v.Command == FlagsAndCommand.Curve4).Position;
			var tail = lines.Skip(lines.Count - 3).ToList();

			await Assert.That(tail[0].X).IsEqualTo(curveEnd.X).Within(1e-9);
			await Assert.That(tail[0].Y).IsEqualTo(curveEnd.Y - .012).Within(1e-9);
			await Assert.That(tail[1].Y).IsEqualTo(curveEnd.Y - .012 - 4.827).Within(1e-9);
			await Assert.That(tail[2].X).IsEqualTo(curveEnd.X - 4.834).Within(1e-9);
			await Assert.That(tail[2].Y).IsEqualTo(tail[1].Y).Within(1e-9);
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

		/// <summary>
		/// SVG 1.1 section 8.3.2: coordinate pairs after a moveto's first pair are implicit linetos.
		/// Treating them as further movetos (the parser's original behavior) leaves a path with no edges,
		/// so it fills nothing - an icon written this way rendered as blank space.
		/// </summary>
		[Test]
		public async Task ExtraPairsAfterRelativeMoveToAreLineTos()
		{
			var storage = new VertexStorage("m10 10 20 0 0 20z");

			await Assert.That(MoveToPoints(storage)).IsEquivalentTo(new[] { new Vector2(10, 10) }, CollectionOrdering.Matching);
			await Assert.That(LineToPoints(storage)).IsEquivalentTo(new[] { new Vector2(30, 10), new Vector2(30, 30) }, CollectionOrdering.Matching);
		}

		/// <summary>
		/// The absolute companion to <see cref="ExtraPairsAfterRelativeMoveToAreLineTos"/>. The implicit
		/// linetos inherit the moveto's case: 'M' makes them absolute, 'm' relative.
		/// </summary>
		[Test]
		public async Task ExtraPairsAfterAbsoluteMoveToAreLineTos()
		{
			var storage = new VertexStorage("M10 10 30 10 30 30z");

			await Assert.That(MoveToPoints(storage)).IsEquivalentTo(new[] { new Vector2(10, 10) }, CollectionOrdering.Matching);
			await Assert.That(LineToPoints(storage)).IsEquivalentTo(new[] { new Vector2(30, 10), new Vector2(30, 30) }, CollectionOrdering.Matching);
		}

		private static List<Vector2> MoveToPoints(VertexStorage storage)
		{
			return storage.Vertices()
				.Where(v => v.Command == FlagsAndCommand.MoveTo)
				.Select(v => v.Position)
				.ToList();
		}

		private static List<Vector2> LineToPoints(VertexStorage storage)
		{
			return storage.Vertices()
				.Where(v => v.Command == FlagsAndCommand.LineTo)
				.Select(v => v.Position)
				.ToList();
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
