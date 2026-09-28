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
	/// C++ gradient_lut sized each segment's interpolator end - start + 1, so a ramp stopped two steps short of
	/// its end color; agg-sharp (and the patched C++ reference) land the last entry of a segment on its stop.
	/// </summary>
	public class GradientLutTests
	{
		[Test]
		[Arguments(true)]
		[Arguments(false)]
		public async Task TwoStopRampReachesItsEndColor(bool fastInterpolator)
		{
			var lut = new gradient_lut(256, fastInterpolator);
			lut.add_color(0.0, new Color(0, 0, 0, 255));
			lut.add_color(1.0, new Color(255, 255, 255, 255));
			lut.build_lut();

			await Assert.That(lut.size()).IsEqualTo(256);
			await Assert.That(lut[0]).IsEqualTo(new Color(0, 0, 0, 255));
			await Assert.That(lut[255]).IsEqualTo(new Color(255, 255, 255, 255));
		}

		/// <summary>Stops are sorted; before the first and after the last the LUT holds their colors. (The segment
		/// is 51 entries so its 200-step blue ramp divides evenly and the DDA lands exactly.)</summary>
		[Test]
		public async Task StopsAreSortedAndTheEndsAreFlat()
		{
			var lut = new gradient_lut(100);
			lut.add_color(0.71, new Color(0, 0, 200, 255));
			lut.add_color(0.2, new Color(200, 0, 0, 255));
			lut.build_lut();

			await Assert.That(lut[0]).IsEqualTo(new Color(200, 0, 0, 255));
			await Assert.That(lut[20]).IsEqualTo(new Color(200, 0, 0, 255));
			await Assert.That(lut[70]).IsEqualTo(new Color(0, 0, 200, 255));
			await Assert.That(lut[99]).IsEqualTo(new Color(0, 0, 200, 255));
		}
	}
}
