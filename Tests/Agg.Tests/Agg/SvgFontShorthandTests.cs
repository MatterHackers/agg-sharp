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
using MatterHackers.Agg.Svg;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// The CSS font shorthand, expanded as usvg expands it (svgtypes' FontShorthand).
	public class SvgFontShorthandTests
	{
		private static SvgElement Styled(string style)
		{
			return SvgDocument.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\"><text id=\"t\" font-weight=\"bold\" font-variant=\"small-caps\" style=\"{style}\">A</text></svg>").GetElementById("t");
		}

		[Test]
		public async Task SizeAndFamilyAreSetAndTheOtherFontPropertiesReset()
		{
			// resvg's text/font/font-shorthand.
			SvgElement text = Styled("font-kerning: none; font: 50px 'Noto Sans'");
			await Assert.That(text["font-size"]).IsEqualTo("50px");
			await Assert.That(text["font-family"]).IsEqualTo("'Noto Sans'");
			await Assert.That(text["font-weight"]).IsEqualTo("normal");
			await Assert.That(text["font-variant"]).IsEqualTo("normal");
			await Assert.That(text["font-kerning"]).IsEqualTo("auto");
		}

		[Test]
		public async Task StyleVariantWeightAndStretchComeBeforeTheSizeAndLineHeightIsSkipped()
		{
			SvgElement text = Styled("font: italic small-caps 700 condensed 12.5em/1.5 serif, 'Noto Sans'");
			await Assert.That(text["font-style"]).IsEqualTo("italic");
			await Assert.That(text["font-variant"]).IsEqualTo("small-caps");
			await Assert.That(text["font-weight"]).IsEqualTo("700");
			await Assert.That(text["font-stretch"]).IsEqualTo("condensed");
			await Assert.That(text["font-size"]).IsEqualTo("12.5em");
			await Assert.That(text["font-family"]).IsEqualTo("serif, 'Noto Sans'");
		}

		[Test]
		public async Task AShorthandWithoutASizeAndFamilyChangesNothing()
		{
			SvgElement text = Styled("font: bold");
			await Assert.That(text["font-weight"]).IsEqualTo("bold");
			await Assert.That(text["font-variant"]).IsEqualTo("small-caps");
			await Assert.That(text["font"]).IsNull();
		}
	}
}
