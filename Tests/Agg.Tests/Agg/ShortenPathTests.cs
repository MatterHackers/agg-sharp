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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// shorten_path (agg_shorten_path.h): cuts a sub-path's end back by a length, dropping whole segments
	/// from the end and moving the last remaining point along its segment.
	/// </summary>
	public class ShortenPathTests
	{
		/// <summary>
		/// C++ AGG never tests the first segment against the remaining length, so a shorten longer than that
		/// segment moved the end point back past the start and the path came out reversed (graph_test's
		/// arrowed short edges drew a stub pointing the wrong way). A shorten that uses up the whole path
		/// empties it.
		/// </summary>
		[Test]
		public async Task ShortenLongerThanThePathEmptiesIt()
		{
			var twoPoints = Sequence((0, 0), (10, 0));
			ShapePath.shorten_path(twoPoints, 25);
			await Assert.That(twoPoints.Count).IsEqualTo(0);

			var threePoints = Sequence((0, 0), (10, 0), (20, 0));
			ShapePath.shorten_path(threePoints, 25);
			await Assert.That(threePoints.Count).IsEqualTo(0);
		}

		[Test]
		public async Task ShortenWithinThePathMovesItsEnd()
		{
			var twoPoints = Sequence((0, 0), (10, 0));
			ShapePath.shorten_path(twoPoints, 4);
			await Assert.That(twoPoints.Count).IsEqualTo(2);
			await Assert.That(twoPoints[1].x).IsEqualTo(6.0);

			// The last segment is dropped whole and the first cut back by what is left.
			var threePoints = Sequence((0, 0), (10, 0), (20, 0));
			ShapePath.shorten_path(threePoints, 14);
			await Assert.That(threePoints.Count).IsEqualTo(2);
			await Assert.That(threePoints[1].x).IsEqualTo(6.0);
		}

		private static VertexSequence Sequence(params (double x, double y)[] points)
		{
			var sequence = new VertexSequence();
			foreach (var (x, y) in points)
			{
				sequence.Add(new VertexDistance(x, y));
			}

			sequence.close(false);
			return sequence;
		}
	}
}
