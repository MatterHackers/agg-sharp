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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Vertex output of the conv_smooth_poly1 and conv_bspline ports. The expected sequences are traced by
	/// hand through the C++ AGG 2.4 sources (agg_vcgen_smooth_poly1.cpp, agg_vcgen_bspline.cpp,
	/// agg_bspline.cpp); the pixel-level checks against C++ itself are AggReferenceTests.DashesAndArrowheadsMatchCppAgg
	/// (smoothed variants) and AggReferenceTests.BSplineMatchesCppAgg. The end-poly vertex reads (0,0): the
	/// generators leave its coordinates alone, and the adapter zeroes them.
	/// </summary>
	public class SmoothPolygonBSplineTests
	{
		private const FlagsAndCommand M = FlagsAndCommand.MoveTo;
		private const FlagsAndCommand L = FlagsAndCommand.LineTo;
		private const FlagsAndCommand C3 = FlagsAndCommand.Curve3;
		private const FlagsAndCommand C4 = FlagsAndCommand.Curve4;
		private const FlagsAndCommand End = FlagsAndCommand.EndPoly;
		private const FlagsAndCommand Closed = FlagsAndCommand.EndPoly | FlagsAndCommand.FlagClose;

		[Test]
		public async Task SmoothPolygonRoundsAClosedSquareWithCurve4s()
		{
			var smooth = new SmoothPolygon(Square());
			await Assert.That(smooth.SmoothValue).IsEqualTo(1.0);

			// Every vertex is followed by its outgoing curve's two control points, a quarter edge out from the
			// square; the last curve comes back to the start.
			await AssertVertices(
				smooth,
				(M, 0, 0), (C4, 2.5, -2.5), (C4, 7.5, -2.5),
				(C4, 10, 0), (C4, 12.5, 2.5), (C4, 12.5, 7.5),
				(C4, 10, 10), (C4, 7.5, 12.5), (C4, 2.5, 12.5),
				(C4, 0, 10), (C4, -2.5, 7.5), (C4, -2.5, 2.5),
				(C4, 0, 0), (Closed, 0, 0));
		}

		[Test]
		public async Task SmoothPolygonEndsAnOpenPathWithCurve3s()
		{
			var path = new VertexStorage();
			path.MoveTo(0, 0);
			path.LineTo(10, 0);
			path.LineTo(10, 10);

			// Each end curve is a Curve3 with one control point: the first keeps only the control point near
			// the second vertex, the last only the one near the second vertex's end. C++ still computes both
			// from the path wrapped around, which is why the last one swings out past the corner.
			await AssertVertices(
				new SmoothPolygon(path),
				(M, 0, 0), (C3, 7.5, -2.5), (C3, 10, 0), (C3, 12.5, 2.5), (C3, 10, 10), (End, 0, 0));
		}

		[Test]
		public async Task SmoothValueScalesTheControlPoints()
		{
			var smooth = new SmoothPolygon(Square()) { SmoothValue = 2 };

			await AssertVertices(
				smooth,
				(M, 0, 0), (C4, 5, -5), (C4, 5, -5),
				(C4, 10, 0), (C4, 15, 5), (C4, 15, 5),
				(C4, 10, 10), (C4, 5, 15), (C4, 5, 15),
				(C4, 0, 10), (C4, -5, 5), (C4, -5, 5),
				(C4, 0, 0), (Closed, 0, 0));
		}

		[Test]
		public async Task TwoVertexPathsPassThroughAsALine()
		{
			await AssertVertices(new SmoothPolygon(Line(0, 0, 10, 0)), (M, 0, 0), (L, 10, 0));
			await AssertVertices(new BSplinePath(Line(0, 0, 10, 0)), (M, 0, 0), (L, 10, 0));
		}

		[Test]
		public async Task BSplineSamplesTheSplineAndEndsOnTheLastVertex()
		{
			var path = new VertexStorage();
			path.MoveTo(0, 0);
			path.LineTo(10, 0);
			path.LineTo(20, 10);
			var bspline = new BSplinePath(path);
			await Assert.That(bspline.InterpolationStep).IsEqualTo(1.0 / 50.0);
			bspline.InterpolationStep = 0.5;

			// x is linear in the vertex index. y is the natural cubic spline through 0, 0, 10, whose second
			// derivative is 0, 15, 0: y(t) = 2.5t^3 - 2.5t on [0, 1], so y(0.5) = -0.9375 and y(1.5) = 4.0625.
			await AssertVertices(
				bspline,
				(M, 0, 0), (L, 5, -0.9375), (L, 10, 0), (L, 15, 4.0625), (L, 20, 10), (End, 0, 0));
		}

		private static VertexStorage Square()
		{
			var square = new VertexStorage();
			square.MoveTo(0, 0);
			square.LineTo(10, 0);
			square.LineTo(10, 10);
			square.LineTo(0, 10);
			square.ClosePolygon();
			return square;
		}

		private static VertexStorage Line(double x1, double y1, double x2, double y2)
		{
			var line = new VertexStorage();
			line.MoveTo(x1, y1);
			line.LineTo(x2, y2);
			return line;
		}

		private static async Task AssertVertices(IVertexSource source, params (FlagsAndCommand command, double x, double y)[] expected)
		{
			var actual = new List<(FlagsAndCommand command, double x, double y)>();
			source.Rewind(0);
			FlagsAndCommand command;
			while (!ShapePath.IsStop(command = source.Vertex(out double x, out double y)) && actual.Count < 100)
			{
				actual.Add((command, x, y));
			}

			// Adding 0.0 turns the -0 of a rounded -1e-16 into 0, so it prints as "0".
			string Describe(IEnumerable<(FlagsAndCommand command, double x, double y)> vertices)
				=> string.Join(" ", vertices.Select(v => $"{v.command}({Math.Round(v.x, 6) + 0.0},{Math.Round(v.y, 6) + 0.0})"));

			await Assert.That(Describe(actual)).IsEqualTo(Describe(expected));
			for (int i = 0; i < expected.Length; i++)
			{
				await Assert.That(Math.Abs(actual[i].x - expected[i].x)).IsLessThan(1e-9);
				await Assert.That(Math.Abs(actual[i].y - expected[i].y)).IsLessThan(1e-9);
			}
		}
	}
}
