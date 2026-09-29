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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Svg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// <see cref="SvgDash"/> places dashes as tiny-skia (resvg) does: distance is measured along chords of the curve
	/// split until they are within 0.5 / scale of it, and each dash is cut from the true curve at the t that distance
	/// maps to. Those chords are shorter than the curve, so every dash lands a little further along than true arc length
	/// would put it.
	/// </summary>
	public class SvgDashTests
	{
		/// <summary>The dashes of <paramref name="path"/> as lists of points (curve vertices included).</summary>
		private static List<List<VertexData>> Dashes(VertexStorage path, double[] array, double offset)
		{
			var dashes = new List<List<VertexData>>();
			foreach (var vertex in SvgDash.Dash(path, array, offset, 1).Vertices())
			{
				if (vertex.Command == FlagsAndCommand.MoveTo)
				{
					dashes.Add(new List<VertexData>());
				}

				if (ShapePath.IsVertex(vertex.Command))
				{
					dashes.Last().Add(vertex);
				}
			}

			return dashes;
		}

		/// <summary>
		/// The quadratic (0,0) (50,50) (100,0) measures 114.595 this way (its true length is 114.78). Dashed 30 on 10 off,
		/// the first dash ends at t 0.238550 and the second runs from t 0.330991 to 0.625985 (computed from tiny-skia's
		/// dash.rs, in doubles).
		/// </summary>
		[Test]
		public async Task DashesAreMeasuredAlongTinySkiasChordsAndCutOnTheCurve()
		{
			var path = new VertexStorage();
			path.MoveTo(0, 0);
			path.Curve3(50, 50, 100, 0);

			var dashes = Dashes(path, new double[] { 30, 10 }, 0);

			await Assert.That(dashes.Count).IsEqualTo(3);
			await Assert.That(dashes[0][0].Position).IsEqualTo(new Vector2(0, 0));
			await AssertNear(dashes[0].Last().Position, QuadAt(0.238550340171656));
			await AssertNear(dashes[1][0].Position, QuadAt(0.330990616016159));
			await AssertNear(dashes[1].Last().Position, QuadAt(0.625985395598669));

			// Cut on the curve: each dash is a piece of the quadratic, not a polyline.
			await Assert.That(dashes[0][1].Command).IsEqualTo(FlagsAndCommand.Curve3);
		}

		/// <summary>
		/// A closed contour's first dash is drawn last, as a dash of its own: resvg's references leave the corner where the
		/// pattern wraps square cut (painting/stroke-dasharray/n-0), so it is not joined to the dash that reached the end.
		/// </summary>
		[Test]
		public async Task AClosedContoursFirstDashIsDrawnLastOnItsOwn()
		{
			var square = new VertexStorage();
			square.MoveTo(0, 0);
			square.LineTo(100, 0);
			square.LineTo(100, 100);
			square.LineTo(0, 100);
			square.ClosePolygon();

			// 400 long, 30 on 20 off from offset 10: dashes 40-70, 90-120, ..., 390-400, then 0-20.
			var dashes = Dashes(square, new double[] { 30, 20 }, 10);

			await Assert.That(dashes.Count).IsEqualTo(9);
			await AssertNear(dashes[7][0].Position, new Vector2(0, 10));
			await AssertNear(dashes[7].Last().Position, new Vector2(0, 0));
			await AssertNear(dashes[8][0].Position, new Vector2(0, 0));
			await AssertNear(dashes[8].Last().Position, new Vector2(20, 0));
		}

		private static Vector2 QuadAt(double t)
		{
			double u = 1 - t;
			return new Vector2(2 * u * t * 50 + t * t * 100, 2 * u * t * 50);
		}

		private static async Task AssertNear(Vector2 actual, Vector2 expected)
		{
			await Assert.That(actual.X).IsEqualTo(expected.X).Within(1e-6);
			await Assert.That(actual.Y).IsEqualTo(expected.Y).Within(1e-6);
		}
	}
}
