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

using System;
using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>The hue ring and saturation/value triangle maths behind <see cref="ColorWheelPicker"/>.</summary>
	public class ColorWheelMathTests
	{
		[Test]
		public async Task HueIsTheCounterClockwiseAngleFromThreeOClock()
		{
			await Assert.That(ColorWheelMath.HueFromOffset(new Vector2(10, 0))).IsEqualTo(0.0);
			await Assert.That(Math.Abs(ColorWheelMath.HueFromOffset(new Vector2(0, 10)) - 90)).IsLessThan(1e-9);
			await Assert.That(Math.Abs(ColorWheelMath.HueFromOffset(new Vector2(-10, 0)) - 180)).IsLessThan(1e-9);
			await Assert.That(Math.Abs(ColorWheelMath.HueFromOffset(new Vector2(0, -10)) - 270)).IsLessThan(1e-9);
		}

		[Test]
		public async Task RingHitTestIsTheAnnulusInclusive()
		{
			await Assert.That(ColorWheelMath.InRing(new Vector2(50, 0), 40, 60)).IsTrue();
			await Assert.That(ColorWheelMath.InRing(new Vector2(40, 0), 40, 60)).IsTrue();
			await Assert.That(ColorWheelMath.InRing(new Vector2(0, 60), 40, 60)).IsTrue();
			await Assert.That(ColorWheelMath.InRing(new Vector2(30, 0), 40, 60)).IsFalse().Because("the middle belongs to the triangle");
			await Assert.That(ColorWheelMath.InRing(new Vector2(50, 50), 40, 60)).IsFalse();
		}

		[Test]
		public async Task TriangleVerticesAreThePureHueWhiteAndBlack()
		{
			var center = new Vector2(100, 100);
			ColorWheelMath.TriangleVertices(center, 50, 90, out var pure, out var white, out var black);
			await Assert.That((pure - new Vector2(100, 150)).Length).IsLessThan(1e-9).Because("the pure hue points at its own hue on the ring");
			await Assert.That(Math.Abs((white - center).Length - 50)).IsLessThan(1e-9);
			await Assert.That(Math.Abs((black - center).Length - 50)).IsLessThan(1e-9);

			// Each vertex maps to its own saturation/value.
			await AssertSv(ColorWheelMath.SaturationValueAt(pure, center, 50, 90), 1, 1);
			await AssertSv(ColorWheelMath.SaturationValueAt(white, center, 50, 90), 0, 1);
			await AssertSv(ColorWheelMath.SaturationValueAt(black, center, 50, 90), 0, 0);
		}

		[Test]
		public async Task SaturationValueRoundTripsThroughAPoint()
		{
			var center = new Vector2(0, 0);
			foreach (var hue in new[] { 0.0, 37.0, 200.0 })
			{
				foreach (var (s, v) in new[] { (.25, .5), (.8, .9), (.5, .1), (1.0, .3) })
				{
					var point = ColorWheelMath.PointAt(s, v, center, 80, hue);
					await Assert.That(ColorWheelMath.InTriangle(point, center, 80, hue)).IsTrue();
					await AssertSv(ColorWheelMath.SaturationValueAt(point, center, 80, hue), s, v);
				}
			}
		}

		[Test]
		public async Task PointsOutsideTheTriangleClampToItsEdge()
		{
			var center = new Vector2(0, 0);
			// Far beyond the pure-hue vertex (hue 0 points right) is still the pure hue.
			await Assert.That(ColorWheelMath.InTriangle(new Vector2(500, 0), center, 50, 0)).IsFalse();
			await AssertSv(ColorWheelMath.SaturationValueAt(new Vector2(500, 0), center, 50, 0), 1, 1);
			// Straight left, beyond the white-black edge: no saturation, half value.
			await AssertSv(ColorWheelMath.SaturationValueAt(new Vector2(-500, 0), center, 50, 0), 0, .5);
		}

		[Test]
		public async Task HexParsesEveryShapeAndRejectsJunk()
		{
			await Assert.That(ColorWheelMath.TryParseHex("#FF8000", out var c)).IsTrue();
			await Assert.That(c).IsEqualTo(new Color(255, 128, 0));
			await Assert.That(ColorWheelMath.TryParseHex("f80", out c)).IsTrue();
			await Assert.That(c).IsEqualTo(new Color(255, 136, 0));
			await Assert.That(ColorWheelMath.TryParseHex(" #f808 ", out c)).IsTrue();
			await Assert.That(c).IsEqualTo(new Color(255, 136, 0, 136));
			await Assert.That(ColorWheelMath.TryParseHex("#11223344", out c)).IsTrue();
			await Assert.That(c).IsEqualTo(new Color(0x11, 0x22, 0x33, 0x44));
			await Assert.That(ColorWheelMath.TryParseHex("#12345", out _)).IsFalse();
			await Assert.That(ColorWheelMath.TryParseHex("#GG0000", out _)).IsFalse();
			await Assert.That(ColorWheelMath.TryParseHex(null, out _)).IsFalse();
		}

		private static async Task AssertSv((double Saturation, double Value) actual, double saturation, double value)
		{
			await Assert.That(Math.Abs(actual.Saturation - saturation)).IsLessThan(1e-6);
			await Assert.That(Math.Abs(actual.Value - value)).IsLessThan(1e-6);
		}
	}
}
