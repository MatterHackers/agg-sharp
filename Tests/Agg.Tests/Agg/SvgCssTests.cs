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
	// agg/Svg, stage 5: <style> sheets and the cascade they join.
	public class SvgCssTests
	{
		private static SvgDocument Parse(string body)
		{
			return SvgDocument.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">{body}</svg>");
		}

		[Test]
		public async Task TypeClassAndIdSelectorsApplyByTheirSpecificity()
		{
			SvgDocument document = Parse(
				"<style>#r { fill: blue } rect.a { fill: green } .a { fill: red } rect { fill: yellow; stroke: black }</style>"
				+ "<rect id=\"r\" class=\"a\" fill=\"gray\"/><rect id=\"s\" class=\"b a\"/><rect id=\"t\"/>");
			await Assert.That(document.GetElementById("r")["fill"]).IsEqualTo("blue");
			await Assert.That(document.GetElementById("s")["fill"]).IsEqualTo("green");
			await Assert.That(document.GetElementById("t")["fill"]).IsEqualTo("yellow");
			await Assert.That(document.GetElementById("t")["stroke"]).IsEqualTo("black");
		}

		[Test]
		public async Task TheLaterOfTwoEqualRulesWinsAndTheStyleAttributeBeatsBoth()
		{
			SvgDocument document = Parse(
				"<style>.a { fill: red } .b { fill: green }</style>"
				+ "<rect id=\"r\" class=\"b a\"/><rect id=\"s\" class=\"a\" style=\"fill:blue\"/>");
			await Assert.That(document.GetElementById("r")["fill"]).IsEqualTo("green");
			await Assert.That(document.GetElementById("s")["fill"]).IsEqualTo("blue");
		}

		[Test]
		public async Task ImportantRulesBeatTheStyleAttribute()
		{
			SvgDocument document = Parse(
				"<style>rect { fill: green !important } #r { fill: red }</style><rect id=\"r\" style=\"fill:blue\"/>");
			await Assert.That(document.GetElementById("r")["fill"]).IsEqualTo("green");
		}

		[Test]
		public async Task DescendantChildAndUniversalCombinatorsMatchTheirTrees()
		{
			SvgDocument document = Parse(
				"<style>/* a comment */ g rect { fill: green } g > circle { fill: blue } * { stroke: black } @media print { rect { fill: red } } rect[x] { fill: red }</style>"
				+ "<g><g><rect id=\"deep\"/><circle id=\"child\"/></g></g><rect id=\"top\" x=\"1\"/><circle id=\"lone\"/>");
			await Assert.That(document.GetElementById("deep")["fill"]).IsEqualTo("green");
			await Assert.That(document.GetElementById("child")["fill"]).IsEqualTo("blue");
			await Assert.That(document.GetElementById("top")["fill"]).IsNull();
			await Assert.That(document.GetElementById("lone")["fill"]).IsNull();
			await Assert.That(document.GetElementById("lone")["stroke"]).IsEqualTo("black");
		}

		[Test]
		public async Task ASheetStylesTheRender()
		{
			var image = SvgDocument.RenderToImage(
				"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><style type=\"text/css\">.g { fill: #00ff00 }</style>"
				+ "<rect class=\"g\" width=\"100\" height=\"100\" fill=\"red\"/></svg>",
				10,
				10);
			await Assert.That(image.GetPixel(5, 5)).IsEqualTo(new Color(0, 255, 0, 255));
		}
	}
}
