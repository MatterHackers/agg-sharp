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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// agg/Svg <text>: per-glyph rotate and the baseline properties. Bounds are in SVG's y-down user space, so a
	// run's Top is its lowest point.
	public class SvgTextPositionTests
	{
		private static RectangleDouble Bounds(string text) => SvgTextTests.Layout(text)[0].Path.GetBounds();

		[Test]
		public async Task RotateTurnsEachGlyphAboutItsOrigin()
		{
			// An upright "l" is tall and thin; turned 90 degrees clockwise about its origin its stem points right along the baseline.
			RectangleDouble upright = Bounds("<text x=\"100\" y=\"100\" font-size=\"50\">l</text>");
			RectangleDouble turned = Bounds("<text x=\"100\" y=\"100\" font-size=\"50\" rotate=\"90\">l</text>");
			await Assert.That(upright.Height).IsGreaterThan(upright.Width);
			await Assert.That(turned.Width).IsEqualTo(upright.Height).Within(1e-6);
			await Assert.That(turned.Left).IsGreaterThanOrEqualTo(99.999);
		}

		[Test]
		public async Task RotateRepeatsItsLastAngleForTheRemainingCharacters()
		{
			// Both glyphs turned: the run stays as flat as one turned "l".
			RectangleDouble one = Bounds("<text x=\"100\" y=\"100\" font-size=\"50\" rotate=\"90\">l</text>");
			RectangleDouble two = Bounds("<text x=\"100\" y=\"100\" font-size=\"50\" rotate=\"90\">ll</text>");
			await Assert.That(two.Height).IsEqualTo(one.Height).Within(1e-6);
		}

		[Test]
		public async Task RotateWithAUnitIsIgnored()
		{
			// resvg's text/text/rotate-with-an-invalid-angle: rotate="5mm" is not a list of numbers, so nothing turns.
			RectangleDouble upright = Bounds("<text x=\"100\" y=\"100\" font-size=\"50\">l</text>");
			RectangleDouble invalid = Bounds("<text x=\"100\" y=\"100\" font-size=\"50\" rotate=\"5mm\">l</text>");
			await Assert.That(invalid.Width).IsEqualTo(upright.Width).Within(1e-9);
		}

		[Test]
		public async Task DominantBaselineCentralCentresTheEmBoxOnY()
		{
			TypeFace face = LiberationSansFont.Instance;
			double scale = 40.0 / face.UnitsPerEm;
			double auto = Bounds("<text x=\"20\" y=\"100\" font-size=\"40\">x</text>").Top;
			double central = Bounds("<text x=\"20\" y=\"100\" font-size=\"40\" dominant-baseline=\"central\">x</text>").Top;
			await Assert.That(central - auto).IsEqualTo((face.Ascent + face.Descent) / 2.0 * scale).Within(1e-6);

			// alignment-baseline on a tspan wins over the text's dominant-baseline.
			double hanging = Bounds("<text x=\"20\" y=\"100\" font-size=\"40\" dominant-baseline=\"central\"><tspan alignment-baseline=\"hanging\">x</tspan></text>").Top;
			await Assert.That(hanging - auto).IsEqualTo(face.Ascent * 0.8 * scale).Within(1e-6);
		}

		[Test]
		public async Task BaselineShiftOnTspansSumsUpwards()
		{
			// The text's own baseline-shift is ignored (as usvg does); 10 + 50% of 40 = 30 up.
			double plain = Bounds("<text x=\"20\" y=\"100\" font-size=\"40\">x</text>").Top;
			double shifted = Bounds("<text x=\"20\" y=\"100\" font-size=\"40\" baseline-shift=\"30\"><tspan baseline-shift=\"10\"><tspan baseline-shift=\"50%\">x</tspan></tspan></text>").Top;
			await Assert.That(plain - shifted).IsEqualTo(30).Within(1e-6);
		}
	}
}
