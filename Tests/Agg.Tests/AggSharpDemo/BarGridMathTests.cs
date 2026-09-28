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
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	/// <summary>The bar grid's heights, colours and camera, as agg-gui's bar_grid shaders compute them.</summary>
	public class BarGridMathTests
	{
		private const double Tolerance = 1e-9;

		[Test]
		public async Task ThePhaseStaysBoundedAndContinuousOverALongRun()
		{
			// agg-gui's bar_wave_phase_stays_bounded_for_long_running_animation.
			double shortElapsed = 12.345;
			double period = 2 * Math.PI / BarGridMath.WaveSpeed;
			double longElapsed = shortElapsed + (period * 100_000);

			double shortPhase = BarGridMath.WavePhase(shortElapsed);
			double longPhase = BarGridMath.WavePhase(longElapsed);

			await Assert.That(longPhase).IsGreaterThanOrEqualTo(0);
			await Assert.That(longPhase).IsLessThan(2 * Math.PI);
			await Assert.That(Math.Abs(shortPhase - longPhase)).IsLessThan(1e-4);
			await Assert.That(BarGridMath.WavePhase(1)).IsEqualTo(BarGridMath.WaveSpeed).Within(Tolerance);
		}

		[Test]
		public async Task HeightsFollowTheDiagonalSineWave()
		{
			// sin(0) = 0: the middle of the range.
			await Assert.That(BarGridMath.Height(0, 0, 0)).IsEqualTo((BarGridMath.MinHeight + BarGridMath.MaxHeight) / 2).Within(Tolerance);
			await Assert.That(BarGridMath.Height(0, 0, Math.PI / 2)).IsEqualTo(BarGridMath.MaxHeight).Within(Tolerance);
			await Assert.That(BarGridMath.Height(0, 0, 3 * Math.PI / 2)).IsEqualTo(BarGridMath.MinHeight).Within(Tolerance);

			// The wave runs along the diagonal: one step in column equals one step in row.
			await Assert.That(BarGridMath.Height(3, 1, 0.7)).IsEqualTo(BarGridMath.Height(1, 3, 0.7)).Within(Tolerance);

			// Every bar stays in range at any time.
			for (int column = 0; column < BarGridMath.Columns; column++)
			{
				double h = BarGridMath.Height(column, 5, 2.3);
				await Assert.That(h).IsGreaterThanOrEqualTo(BarGridMath.MinHeight - Tolerance);
				await Assert.That(h).IsLessThanOrEqualTo(BarGridMath.MaxHeight + Tolerance);
			}

			// The grid is centred on the origin.
			await Assert.That(BarGridMath.BarCenter(0, 0)).IsEqualTo((-7.5, -3.5));
			await Assert.That(BarGridMath.BarCenter(15, 7)).IsEqualTo((7.5, 3.5));
		}

		[Test]
		public async Task ColoursBlendAcrossColumnsTowardTheAccentAndThePeak()
		{
			var palette = BarGridMath.PaletteFor(dark: true, new ColorF(1, 1, 1));

			// A front bar at the bottom of the wave is exactly the left or right colour.
			double lowAtColumn0 = 3 * Math.PI / 2;
			await AssertColor(BarGridMath.BaseColor(0, 0, lowAtColumn0, palette), palette.Left);
			double lowAtColumn15 = (3 * Math.PI / 2) - (15 * BarGridMath.WaveFrequency);
			await AssertColor(BarGridMath.BaseColor(15, 0, lowAtColumn15, palette), palette.Right);

			// The back row takes 35% of the accent.
			double lowAtRow7 = (3 * Math.PI / 2) - (7 * BarGridMath.WaveFrequency);
			var left = palette.Left;
			var accent = palette.Accent;
			await AssertColor(
				BarGridMath.BaseColor(0, 7, lowAtRow7, palette),
				new ColorF(left.red + ((accent.red - left.red) * 0.35), left.green + ((accent.green - left.green) * 0.35), left.blue + ((accent.blue - left.blue) * 0.35)));

			// At its peak a bar takes 25% of the peak colour (white on dark).
			var peak = BarGridMath.BaseColor(0, 0, Math.PI / 2, palette);
			await AssertColor(peak, new ColorF(left.red + ((1 - left.red) * 0.25), left.green + ((1 - left.green) * 0.25), left.blue + ((1 - left.blue) * 0.25)));

			// The light theme peaks toward the text colour.
			var text = new ColorF(0.1, 0.2, 0.3);
			await AssertColor(BarGridMath.PaletteFor(dark: false, text).Peak, text);
		}

		[Test]
		public async Task FacesAreLitByAmbientPlusDiffuse()
		{
			double[] light = BarGridMath.LightDirection;
			await Assert.That(BarGridMath.Lighting(new[] { 0.0, 1, 0 })).IsEqualTo(0.45 + (0.55 * light[1])).Within(Tolerance);

			// A face turned away from the light gets only the ambient.
			await Assert.That(BarGridMath.Lighting(new[] { -1.0, 0, 0 })).IsEqualTo(0.45).Within(Tolerance);
			await Assert.That(BarGridMath.Lighting(light)).IsEqualTo(1.0).Within(Tolerance);
		}

		[Test]
		public async Task TheCameraLooksAtTheGridCentreWithTheWholeGridInFront()
		{
			double aspect = 300.0 / 230;
			var viewProjection = BarGridMath.Multiply(BarGridMath.Projection(aspect), BarGridMath.View());

			var target = BarGridMath.Transform(viewProjection, BarGridMath.Target[0], BarGridMath.Target[1], BarGridMath.Target[2]);
			await Assert.That(target[0] / target[3]).IsEqualTo(0).Within(1e-9);
			await Assert.That(target[1] / target[3]).IsEqualTo(0).Within(1e-9);

			// Every corner of the tallest possible grid is in front of the camera and inside the depth range.
			// agg-gui frames it tight, so the outer columns run off the sides: x and y are not bounded.
			foreach (double x in new[] { -8.0, 8.0 })
			{
				foreach (double y in new[] { 0.0, BarGridMath.MaxHeight })
				{
					foreach (double z in new[] { -4.0, 4.0 })
					{
						var clip = BarGridMath.Transform(viewProjection, x, y, z);
						await Assert.That(clip[3]).IsGreaterThan(0).Because($"w of ({x}, {y}, {z})");
						await Assert.That(Math.Abs(clip[2] / clip[3])).IsLessThan(1.0).Because($"depth of ({x}, {y}, {z})");
					}
				}
			}
		}

		private static async Task AssertColor(ColorF actual, ColorF expected)
		{
			await Assert.That(actual.red).IsEqualTo(expected.red).Within(1e-6f);
			await Assert.That(actual.green).IsEqualTo(expected.green).Within(1e-6f);
			await Assert.That(actual.blue).IsEqualTo(expected.blue).Within(1e-6f);
		}
	}
}
