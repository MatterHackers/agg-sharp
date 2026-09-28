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
	public class SpanGradientTests
	{
		/// <summary>
		/// C++ gradient_radial and gradient_circle are <c>fast_sqrt(x*x + y*y)</c> and gradient_sqrt_xy is
		/// <c>fast_sqrt(|x|*|y|)</c>, where fast_sqrt takes an unsigned: a sum of 2^31 or more (a coordinate past
		/// about 46341 subpixels) is still a valid input, not a negative table index. The expected values are C++'s
		/// (agg_span_gradient.h built against agg_sqrt_tables.cpp) - the table approximation, so (50000, 0) is
		/// 49998, not the exact 50000.
		/// </summary>
		[Test]
		[Arguments(50000, 0, 49998, 0)]
		[Arguments(40000, 30000, 49998, 34635)]
		[Arguments(-46341, 1, 46341, 215)]
		public async Task GradientSqrtFunctionsMatchCppFastSqrt(int x, int y, int radial, int sqrtXy)
		{
			await Assert.That(new gradient_radial().calculate(x, y, 0)).IsEqualTo(radial);
			await Assert.That(new gradient_circle().calculate(x, y, 0)).IsEqualTo(radial);
			await Assert.That(new gradient_sqrt_xy().calculate(x, y, 0)).IsEqualTo(sqrtXy);
		}
	}
}
