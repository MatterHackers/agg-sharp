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
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The resvg suite's pass rule: a few shades anywhere, more only on the reference's edges, and the drawing must
	// still be the same shape, place and colour.
	public class SvgTolerantCompareTests
	{
		private const int Size = 40;

		/// <summary>A Size x Size transparent image with an opaque green square from (left, top), side 20, and optional antialiased left and right edge columns.</summary>
		private static byte[] Square(int left, int top, byte red = 0, byte green = 128, byte blue = 0, byte alpha = 255, int edgeAlpha = -1)
		{
			var rgba = new byte[Size * Size * 4];
			for (int y = top; y < top + 20; y++)
			{
				for (int x = left; x < left + 20; x++)
				{
					Set(rgba, x, y, red, green, blue, alpha);
				}

				if (edgeAlpha >= 0)
				{
					Set(rgba, left - 1, y, red, green, blue, (byte)edgeAlpha);
					Set(rgba, left + 20, y, red, green, blue, (byte)edgeAlpha);
				}
			}

			return rgba;
		}

		private static void Set(byte[] rgba, int x, int y, byte red, byte green, byte blue, byte alpha)
		{
			int i = (y * Size + x) * 4;
			rgba[i] = red;
			rgba[i + 1] = green;
			rgba[i + 2] = blue;
			rgba[i + 3] = alpha;
		}

		[Test]
		public async Task InteriorPixelsMayBeOffByThreeShadesButNotFour()
		{
			byte[] reference = Square(10, 10);
			await Assert.That(SvgTolerantCompare.Compare(Square(10, 10, green: 131), reference, Size).Pass).IsTrue();
			await Assert.That(SvgTolerantCompare.Compare(Square(10, 10, green: 132), reference, Size).Pass).IsFalse();
		}

		[Test]
		public async Task AntialiasedEdgesMayDifferWhereTheReferenceHasAnEdge()
		{
			// agg's exact-area coverage against a supersampled edge: the same edge column, 60 alpha levels apart.
			byte[] reference = Square(10, 10, edgeAlpha: 100);
			SvgTolerantCompareResult result = SvgTolerantCompare.Compare(Square(10, 10, edgeAlpha: 160), reference, Size);
			await Assert.That(result.BadPixels).IsEqualTo(0);

			// The same 60 levels in the middle of the square, away from any edge, is a wrong colour.
			byte[] blotched = Square(10, 10);
			Set(blotched, 20, 20, 0, 68, 0, 255);
			await Assert.That(SvgTolerantCompare.Compare(blotched, Square(10, 10), Size).BadPixels).IsEqualTo(1);
		}

		[Test]
		public async Task AShapeShiftedTwoPixelsFails()
		{
			await Assert.That(SvgTolerantCompare.Compare(Square(12, 10), Square(10, 10), Size).Pass).IsFalse();
		}

		[Test]
		public async Task AMissingOrRecolouredShapeFails()
		{
			byte[] reference = Square(10, 10);
			await Assert.That(SvgTolerantCompare.Compare(new byte[reference.Length], reference, Size).Pass).IsFalse();
			await Assert.That(SvgTolerantCompare.Compare(Square(10, 10, red: 128, green: 0), reference, Size).Pass).IsFalse();
		}

		[Test]
		public async Task EdgesAllWithinToleranceStillFailWhenTheyAddUpToMoreCoverage()
		{
			// Every pixel of a wider edge is within the edge tolerance, but the shape is 2 x 20 x 0.25 px bigger - past 1% of its 400 px.
			byte[] reference = Square(10, 10, edgeAlpha: 0);
			SvgTolerantCompareResult result = SvgTolerantCompare.Compare(Square(10, 10, edgeAlpha: 64), reference, Size);
			await Assert.That(result.BadPixels).IsEqualTo(0);
			await Assert.That(result.Pass).IsFalse();
		}

		[Test]
		public async Task ATranslucentFillOneAlphaLevelOffPasses()
		{
			// Rounding a large translucent fill differently must not trip the coverage guard.
			await Assert.That(SvgTolerantCompare.Compare(Square(10, 10, alpha: 51), Square(10, 10, alpha: 50), Size).Pass).IsTrue();
		}

		[Test]
		public async Task ColourUnderZeroAlphaDoesNotCount()
		{
			byte[] reference = Square(10, 10);
			byte[] rendered = Square(10, 10);
			Set(rendered, 0, 0, 255, 0, 255, 0);
			await Assert.That(SvgTolerantCompare.Compare(rendered, reference, Size).BadPixels).IsEqualTo(0);
		}
	}
}
