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

using System.Threading.Tasks;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The size rule every curve tolerance scales by: the caller's own absolute value, untouched, at or under a
	/// printer bed, and grown in proportion to the shape above it.
	/// </summary>
	public class CurveToleranceTests
	{
		/// <summary>
		/// Desk-size shapes keep EXACTLY the tolerance they always had - not merely close to it - because a
		/// stored mesh built from them must not move by a single bit.
		/// </summary>
		[Test]
		[Arguments(0.0)]
		[Arguments(0.001)]
		[Arguments(50.0)]
		[Arguments(299.999)]
		[Arguments(300.0)]
		[Arguments(-5000.0)]
		public async Task AtOrUnderDeskSizeTheToleranceIsExactlyTheBase(double size)
		{
			await Assert.That(CurveTolerance.ScaleFor(size)).IsEqualTo(1.0);
			await Assert.That(CurveTolerance.ForSize(0.125, size)).IsEqualTo(0.125);
			await Assert.That(CurveTolerance.ForSize(0.25, size)).IsEqualTo(0.25);
		}

		/// <summary>
		/// Above the bed the tolerance is a pure proportion of the size, so the same shape at any unit is cut
		/// into the same segments.
		/// </summary>
		[Test]
		[Arguments(600.0, 2.0)]
		[Arguments(3000.0, 10.0)]
		[Arguments(6096.0, 20.32)]
		public async Task AboveDeskSizeTheToleranceGrowsInProportion(double size, double expectedScale)
		{
			await Assert.That(CurveTolerance.ScaleFor(size)).IsEqualTo(expectedScale).Within(1e-12);
			await Assert.That(CurveTolerance.ForSize(0.25, size)).IsEqualTo(0.25 * expectedScale).Within(1e-12);
		}

		/// <summary>
		/// A size that is not a number - an empty path's inverted bounds, a broken transform - reads as desk
		/// size rather than handing a caller a zero or infinite tolerance.
		/// </summary>
		[Test]
		[Arguments(double.NaN)]
		[Arguments(double.PositiveInfinity)]
		[Arguments(double.NegativeInfinity)]
		public async Task ASizeThatIsNotANumberReadsAsDeskSize(double size)
		{
			await Assert.That(CurveTolerance.ScaleFor(size)).IsEqualTo(1.0);
		}
	}
}
