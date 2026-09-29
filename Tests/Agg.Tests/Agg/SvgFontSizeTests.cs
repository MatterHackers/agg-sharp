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
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// agg/Svg font-size and the em/ex units, resolved as usvg does: 12 by default, ex half an em, named sizes
	// 1.2 apart, and em/ex lengths relative to the element's own font size.
	public class SvgFontSizeTests
	{
		private static SvgStyle Style(string svg, params int[] childPath)
		{
			SvgDocument document = SvgDocument.Parse(svg);
			SvgElement element = document.Root;
			SvgStyle style = SvgStyle.Compute(element, null, 200);
			foreach (int index in childPath)
			{
				element = element.Children[index];
				style = SvgStyle.Compute(element, style, 200);
			}

			return style;
		}

		[Test]
		public async Task FontSizeUnitsAreRelativeToTheParentsSize()
		{
			// resvg's text/font-size/ex, em-on-the-root-element and named-value: ex is half the parent's em, the
			// default size is usvg's 12, and named sizes step by 1.2 from the parent's.
			await Assert.That(Style("<svg xmlns=\"http://www.w3.org/2000/svg\"><g font-size=\"12\"><text font-size=\"5ex\"/></g></svg>", 0, 0).FontSize).IsEqualTo(30).Within(1e-9);
			await Assert.That(Style("<svg xmlns=\"http://www.w3.org/2000/svg\" font-size=\"3em\"/>").FontSize).IsEqualTo(36).Within(1e-9);
			await Assert.That(Style("<svg xmlns=\"http://www.w3.org/2000/svg\"><g font-size=\"10\"><g font-size=\"xx-large\"/></g></svg>", 0, 0).FontSize).IsEqualTo(17.28).Within(1e-9);
			await Assert.That(Style("<svg xmlns=\"http://www.w3.org/2000/svg\"><g font-size=\"10\"><g font-size=\"smaller\"/></g></svg>", 0, 0).FontSize).IsEqualTo(10 / 1.2).Within(1e-9);
		}

		[Test]
		public async Task EmAndExLengthsUseTheElementsFontSize()
		{
			// resvg's text/text/em-and-ex-coordinates: x="0.5em" at font-size 64 is 32.
			RectangleDouble em = SvgTextTests.Layout("<text x=\"0.5em\" y=\"100\" font-size=\"64\">l</text>")[0].Path.GetBounds();
			RectangleDouble px = SvgTextTests.Layout("<text x=\"32\" y=\"100\" font-size=\"64\">l</text>")[0].Path.GetBounds();
			await Assert.That(em.Left).IsEqualTo(px.Left).Within(1e-9);

			// A shape's em too (text/font-size/named-value's rects): 10em at font-size 20 is 200 wide.
			SvgDocument document = SvgDocument.Parse("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect font-size=\"20\" width=\"10em\" height=\"1ex\"/></svg>");
			RectangleDouble rect = SvgShapes.ToPath(document.Root.Children[0], 200, 200, 20).GetBounds();
			await Assert.That(rect.Width).IsEqualTo(200).Within(1e-9);
			await Assert.That(rect.Height).IsEqualTo(10).Within(1e-9);
		}
	}
}
