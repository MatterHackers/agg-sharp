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
using System.Threading.Tasks;
using MatterHackers.Agg.Transform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	// C++ span_interpolator_persp_exact (pattern_resample.cpp's "Perspective Resample Exact").
	public class SpanInterpolatorPerspExactTests
	{
		private static readonly double[] Quad = { 100, 100, 500, 130, 470, 520, 90, 430 };

		/// <summary>
		/// Every pixel of the span is on the perspective (in 1/256 pixels, give or take the incremental iterator's
		/// last bit), not on a line between the span's ends.
		/// </summary>
		[Test]
		public async Task EveryPixelIsOnThePerspective()
		{
			var interpolator = new span_interpolator_persp_exact(Quad, -150, -150, 150, 150);
			var perspective = new Perspective(Quad, -150, -150, 150, 150);
			await Assert.That(interpolator.is_valid()).IsTrue();

			const double x = 103.5;
			const double y = 250.5;
			interpolator.begin(x, y, 40);
			for (int i = 0; i < 40; i++)
			{
				double tx = x + i;
				double ty = y;
				perspective.Transform(ref tx, ref ty);
				interpolator.coordinates(out int cx, out int cy);
				await Assert.That(Math.Abs(cx - Util.iround(tx * 256))).IsLessThanOrEqualTo(1);
				await Assert.That(Math.Abs(cy - Util.iround(ty * 256))).IsLessThanOrEqualTo(1);
				interpolator.Next();
			}
		}

		/// <summary>Its local scale is the lerp interpolator's: both measure the inverse transform a subpixel away.</summary>
		[Test]
		public async Task LocalScaleMatchesTheLerpInterpolator()
		{
			var exact = new span_interpolator_persp_exact(Quad, -150, -150, 150, 150);
			var lerp = new span_interpolator_persp_lerp(Quad, -150, -150, 150, 150);
			exact.begin(103.5, 250.5, 16);
			lerp.begin(103.5, 250.5, 16);
			for (int i = 0; i < 16; i++)
			{
				exact.local_scale(out int ex, out int ey);
				lerp.local_scale(out int lx, out int ly);
				await Assert.That(ex).IsEqualTo(lx);
				await Assert.That(ey).IsEqualTo(ly);
				exact.Next();
				lerp.Next();
			}
		}
	}
}
