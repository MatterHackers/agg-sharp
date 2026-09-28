/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>HsvColor conversions: exact byte round trips and the edge cases.</summary>
	public class HsvColorTests
	{
		[Test]
		public async Task EveryRedGreenPairWithSampledBlueRoundTripsExactly()
		{
			// All 65536 red/green pairs against every seventh blue (37 values) - 2.4M colours, well under a second.
			int failures = 0;
			for (int r = 0; r < 256; r++)
			{
				for (int g = 0; g < 256; g++)
				{
					for (int b = 0; b < 256; b += 7)
					{
						var color = new Color(r, g, b, 200);
						HsvColor.ToHsv(color, out var h, out var s, out var v);
						if (HsvColor.FromHsv(h, s, v, 200 / 255.0) != color)
						{
							failures++;
						}
					}
				}
			}

			await Assert.That(failures).IsEqualTo(0);
		}

		[Test]
		public async Task PrimariesGreysAndWrapHaveTheExpectedHsv()
		{
			HsvColor.ToHsv(Color.Blue, out var h, out var s, out var v);
			await Assert.That(h).IsEqualTo(240.0);
			await Assert.That(s).IsEqualTo(1.0);
			await Assert.That(v).IsEqualTo(1.0);

			HsvColor.ToHsv(new Color(255, 0, 1), out h, out _, out _);
			await Assert.That(h > 359 && h < 360).IsTrue().Because("a red leaning blue wraps near 360, not negative");

			HsvColor.ToHsv(Color.Gray, out h, out s, out _);
			await Assert.That(h).IsEqualTo(0.0);
			await Assert.That(s).IsEqualTo(0.0);

			HsvColor.ToHsv(Color.Black, out _, out s, out v);
			await Assert.That(s).IsEqualTo(0.0);
			await Assert.That(v).IsEqualTo(0.0);

			await Assert.That(HsvColor.FromHsv(360, 1, 1)).IsEqualTo(Color.Red);
			await Assert.That(HsvColor.FromHsv(-240, 1, 1)).IsEqualTo(new Color(0, 255, 0));
			await Assert.That(HsvColor.FromHsv(double.NaN, double.NaN, 1)).IsEqualTo(Color.White);
			await Assert.That(HsvColor.NormalizeHue(-1e-18)).IsEqualTo(0.0);
			await Assert.That(HsvColor.ToHex(new Color(1, 171, 255))).IsEqualTo("#01ABFF");
			await Assert.That(HsvColor.ToHex(new Color(1, 171, 255, 16))).IsEqualTo("#01ABFF10");
		}
	}
}
