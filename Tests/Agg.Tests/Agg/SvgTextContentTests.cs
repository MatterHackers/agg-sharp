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

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Svg;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// What a <text> lays out, as usvg's svgtree prepares it: xml:space across tspans, and tref's linked text.
	// Each case is compared with the text as usvg reads it: written out, or measured in extra spaces.
	public class SvgTextContentTests
	{
		/// <summary>The bounds of everything the last element of the document (a &lt;text&gt;) draws.</summary>
		private static RectangleDouble Bounds(string body)
		{
			SvgDocument document = SvgDocument.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 200 200\" font-size=\"20\">{body}</svg>");
			SvgElement element = document.Root.Children.Last();
			SvgStyle style = SvgStyle.Compute(element, SvgStyle.Compute(document.Root, null, 200), 200);
			var bounds = RectangleDouble.ZeroIntersection;
			foreach (var run in SvgText.Layout(element, style, 200, 200, 200, document: document))
			{
				bounds.ExpandToInclude(run.Path.GetBounds());
			}

			return bounds;
		}

		private static RectangleDouble Plain(string text) => Bounds($"<text x=\"10\" y=\"100\">{text}</text>");

		/// <summary>Where the ink of <paramref name="words"/> (one space apart) ends with <paramref name="extraSpaces"/> more spaces before it.</summary>
		private static double RightWithExtraSpaces(string words, int extraSpaces)
		{
			double space = Font.LiberationSansFont.Instance.GetAdvanceForCodePoint(' ') * 20.0 / Font.LiberationSansFont.Instance.UnitsPerEm;
			return Bounds($"<text x=\"10\" y=\"100\">{words}</text>").Right + extraSpaces * space;
		}

		[Test]
		public async Task XmlSpacePreserveKeepsRunsOfSpacesAndMakesNewlinesSpaces()
		{
			RectangleDouble newline = Bounds("<text x=\"10\" y=\"100\" xml:space=\"preserve\">A  of\nB</text>");
			await Assert.That(newline.Right).IsEqualTo(RightWithExtraSpaces("A of B", 1)).Within(1e-6);
		}

		[Test]
		public async Task MixedXmlSpaceIsJudgedPerTextNodeAsUsvgDoes()
		{
			// resvg's tspan/mixed-xml-space-1: a preserved tspan keeps its spaces, the default text around it
			// loses the one that meets them on the right.
			RectangleDouble mixed = Bounds("<text x=\"10\" y=\"100\">\n  Text  <tspan xml:space=\"preserve\">  Text  </tspan>  Text\n</text>");
			await Assert.That(mixed.Right).IsEqualTo(RightWithExtraSpaces("Text Text Text", 3)).Within(1e-6);

			// mixed-xml-space-2: the other way about, the default tspan loses its leading space.
			RectangleDouble reversed = Bounds("<text x=\"10\" y=\"100\" xml:space=\"preserve\">  Text  <tspan xml:space=\"default\">  Text  </tspan>  Text  </text>");
			await Assert.That(reversed.Right).IsEqualTo(RightWithExtraSpaces("Text Text Text", 5)).Within(1e-6);
		}

		[Test]
		public async Task TrefDrawsTheTextOfTheElementItLinksTo()
		{
			const string Defs = "<defs><text id=\"t1\">Some <tspan>styled</tspan> text</text><rect id=\"r1\">Rect</rect><notsvg id=\"n1\">No</notsvg></defs>";
			RectangleDouble linked = Bounds(Defs + "<text x=\"10\" y=\"100\"><tref xlink:href=\"#t1\"/></text>");
			await Assert.That(linked.Right).IsEqualTo(Plain("Some styled text").Right).Within(1e-6);

			// Any SVG element's character data is linked; the tref's own children are not drawn.
			RectangleDouble rect = Bounds(Defs + "<text x=\"10\" y=\"100\"><tref xlink:href=\"#r1\">bad</tref></text>");
			await Assert.That(rect.Right).IsEqualTo(Plain("Rect").Right).Within(1e-6);

			// An element SVG does not define links nothing.
			RectangleDouble unknown = Bounds(Defs + "<text x=\"10\" y=\"100\">A<tref xlink:href=\"#n1\"/></text>");
			await Assert.That(unknown.Right).IsEqualTo(Plain("A").Right).Within(1e-6);
		}

		[Test]
		public async Task AnInvalidTextPathKeepsTheSpacesAroundIt()
		{
			// resvg's textPath/invalid-textPath-in-the-middle: the path-less textPath's text trims the spaces around
			// it before it is dropped, so "Some" and "text" end up two spaces apart.
			RectangleDouble text = Bounds("<text x=\"10\" y=\"100\">\n  Some\n  <textPath>\n    long\n  </textPath>\n  text\n</text>");
			await Assert.That(text.Right).IsEqualTo(RightWithExtraSpaces("Some text", 1)).Within(1e-6);
		}
	}
}
