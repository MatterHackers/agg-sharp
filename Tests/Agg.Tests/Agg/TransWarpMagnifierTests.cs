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
	/// <summary>
	/// trans_warp_magnifier against C++ AGG 2.4, set up as lion_lens.cpp's default lens (center 200,150,
	/// magnification 3, radius 70 / 3). The expected values were printed by a small program built from
	/// agg_trans_warp_magnifier.cpp (clang, -ffp-contract=off) and are compared exactly.
	/// </summary>
	public class TransWarpMagnifierTests
	{
		// Inside the lens, on its rim's far side, far outside, the center itself, and just outside the rim.
		private static readonly (double X, double Y, double TX, double TY, double IX, double IY)[] Traced =
		{
			(210, 155, 230, 165, 210, 155),
			(250, 150, 296.66666666666663, 150, 249.99999999999994, 150),
			(100, 40, 68.608602946838744, 5.4694632415226181, 99.999999999999972, 39.999999999999972),
			(200, 150, 200, 150, 200, 150),
			(263.5, 171.25, 307.75443338171846, 186.05955447813412, 263.5, 171.25),
		};

		[Test]
		public async Task TransformAndInverseMatchCppAgg()
		{
			var lens = new TransWarpMagnifier
			{
				Magnification = 3.0,
				Radius = 70.0 / 3.0,
			};
			lens.SetCenter(200, 150);

			foreach (var p in Traced)
			{
				double x = p.X;
				double y = p.Y;
				lens.Transform(ref x, ref y);
				await Assert.That(x).IsEqualTo(p.TX);
				await Assert.That(y).IsEqualTo(p.TY);

				lens.InverseTransform(ref x, ref y);
				await Assert.That(x).IsEqualTo(p.IX);
				await Assert.That(y).IsEqualTo(p.IY);
			}
		}

		/// <summary>
		/// C++ divides 0 by 0 at the center of a zero-radius lens (the radius slider reaches 0) and returns NaN;
		/// the port leaves every point where it is. The tools/cpp-renderer patch of agg_trans_warp_magnifier.cpp
		/// does the same, so the goldens agree.
		/// </summary>
		[Test]
		public async Task ZeroRadiusLeavesEveryPointAloneIncludingTheCenter()
		{
			var lens = new TransWarpMagnifier
			{
				Magnification = 3.0,
				Radius = 0,
			};
			lens.SetCenter(200.1, 150.3);

			foreach (var (px, py) in new[] { (200.1, 150.3), (210.7, 155.2), (-3.3, 999.9) })
			{
				double x = px;
				double y = py;
				lens.Transform(ref x, ref y);
				await Assert.That(x).IsEqualTo(px);
				await Assert.That(y).IsEqualTo(py);

				x = px;
				y = py;
				lens.InverseTransform(ref x, ref y);
				await Assert.That(x).IsEqualTo(px);
				await Assert.That(y).IsEqualTo(py);
			}
		}

		[Test]
		public async Task DefaultsLeaveThePlaneAlone()
		{
			var lens = new TransWarpMagnifier();
			double x = 12.5;
			double y = -3;
			lens.Transform(ref x, ref y);

			await Assert.That(x).IsEqualTo(12.5);
			await Assert.That(y).IsEqualTo(-3.0);
		}
	}
}
