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
using MatterHackers.Agg.Transform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	// C++ span_interpolator_linear_subdiv (pattern_perspective.cpp's interpolator) over a perspective transform.
	public class SpanInterpolatorLinearSubdivTests
	{
		private static readonly double[] Quad = { 100, 100, 500, 130, 470, 520, 90, 430 };

		/// <summary>
		/// It lands exactly on the transformed point (in 1/256 pixels) at the span's start and at every subdivision
		/// boundary (16 pixels by default), however the dda steps between them rounded.
		/// </summary>
		[Test]
		public async Task HitsTheExactTransformAtEverySubdivisionBoundary()
		{
			var perspective = new Perspective(Quad, -150, -150, 150, 150);
			var interpolator = new span_interpolator_linear_subdiv(perspective);

			const double x = 103.5;
			const double y = 250.5;
			const int len = 40;
			interpolator.begin(x, y, len);
			for (int i = 0; i < len; i++)
			{
				if (i % 16 == 0)
				{
					double tx = x + i;
					double ty = y;
					perspective.Transform(ref tx, ref ty);
					interpolator.coordinates(out int cx, out int cy);
					await Assert.That(cx).IsEqualTo(Util.iround(tx * 256));
					await Assert.That(cy).IsEqualTo(Util.iround(ty * 256));
				}

				interpolator.Next();
			}
		}

		/// <summary>Between boundaries it steps C++'s forward-adjusted dda2 line: here pixel 5 of the first 16.</summary>
		[Test]
		public async Task StepsADdaLineBetweenBoundaries()
		{
			var perspective = new Perspective(Quad, -150, -150, 150, 150);
			var interpolator = new span_interpolator_linear_subdiv(perspective);
			interpolator.begin(103.5, 250.5, 40);

			double x1 = 103.5, y1 = 250.5, x2 = 103.5 + 16, y2 = 250.5;
			perspective.Transform(ref x1, ref y1);
			perspective.Transform(ref x2, ref y2);
			var expectedX = new dda2_line_interpolator(Util.iround(x1 * 256), Util.iround(x2 * 256), 16);
			for (int i = 0; i < 5; i++)
			{
				interpolator.Next();
				expectedX.Next();
			}

			interpolator.coordinates(out int cx, out _);
			await Assert.That(cx).IsEqualTo(expectedX.y());
		}
	}
}
