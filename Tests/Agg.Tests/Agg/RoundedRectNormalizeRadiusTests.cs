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
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THE
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Threading.Tasks;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// normalize_radius() has to shrink the corner radii until every pair of them fits the edge it sits
	/// on: the two X radii on a horizontal edge have to fit the width, the two Y radii on a vertical edge
	/// have to fit the height. Pairing an X sum against the height (or a Y sum against the width) happens
	/// to give the right answer for a uniform radius on a squarish rect, and the wrong one everywhere
	/// else - so these cases are deliberately non-uniform and elongated.
	/// </summary>
	public class RoundedRectNormalizeRadiusTests
	{
		/// <summary>
		/// A 200 x 10 rect with tiny X radii and 20 tall Y radii. The Y radii overflow the 10 of height
		/// four times over, so they have to come down to 5. Measuring the X sums against the height
		/// instead leaves them looking like they fit (4 into 10) and the arcs bulge 10 past both edges.
		/// </summary>
		[Test]
		public async Task TallCornersOnAShortWideRectAreCutDownToTheHeight()
		{
			var rect = new RoundedRect(0, 0, 200, 10);
			rect.radius(2, 20);

			rect.normalize_radius();

			var vertexBounds = BoundsOf(rect);

			await Assert.That(vertexBounds.Bottom).IsGreaterThanOrEqualTo(0 - Epsilon)
				.Because("the corner arcs may not reach below the rect");
			await Assert.That(vertexBounds.Top).IsLessThanOrEqualTo(10 + Epsilon)
				.Because("the corner arcs may not reach above the rect");
			await Assert.That(vertexBounds.Left).IsGreaterThanOrEqualTo(0 - Epsilon);
			await Assert.That(vertexBounds.Right).IsLessThanOrEqualTo(200 + Epsilon);
		}

		/// <summary>
		/// A 200 x 100 rect with 200 wide X radii and short 10 tall Y radii. The X pair overflows the
		/// width two to one, so k is 1/2 and the radii land at 100 x 5 - the bottom left arc bottoms out
		/// at the rect's midpoint. Measuring that pair against the 100 of height instead makes k 1/4 and
		/// leaves the shape far more square-cornered than asked for.
		/// </summary>
		[Test]
		public async Task WideCornersScaleByTheWidthTheySpanNotTheHeight()
		{
			var rect = new RoundedRect(0, 0, 200, 100);
			rect.radius(200, 10);

			rect.normalize_radius();

			// the bottom left arc runs from (0, ry) round to (rx, 0), so the lowest point of the whole
			// path sits exactly one scaled X radius in from the left edge
			await Assert.That(LowestVertexX(rect)).IsEqualTo(100).Within(Epsilon);
		}

		private const double Epsilon = 1e-9;

		private static RectangleDouble BoundsOf(IVertexSource source)
		{
			var bounds = RectangleDouble.ZeroIntersection;

			foreach (var vertex in source.Vertices())
			{
				if (vertex.IsVertex)
				{
					bounds.ExpandToInclude(vertex.Position);
				}
			}

			return bounds;
		}

		/// <summary>
		/// The X of the leftmost of the vertices sitting on the path's lowest scanline.
		/// </summary>
		private static double LowestVertexX(IVertexSource source)
		{
			double lowestY = BoundsOf(source).Bottom;
			double xAtLowestY = double.MaxValue;

			foreach (var vertex in source.Vertices())
			{
				if (vertex.IsVertex
					&& vertex.Y <= lowestY + Epsilon
					&& vertex.X < xAtLowestY)
				{
					xAtLowestY = vertex.X;
				}
			}

			return xAtLowestY;
		}
	}
}
