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

using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Svg;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// agg/Svg's reading of a document: the XML around the SVG (DTD entities, gzip) and the value syntaxes it parses.
	// Each case is the smallest form of a resvg-test-suite case that failed on parsing alone.
	public class SvgParsingTests
	{
		private const string Doctype = "<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.1 Basic//EN\" \"http://www.w3.org/Graphics/SVG/1.1/DTD/svg11-basic.dtd\" [\n";

		[Test]
		public async Task AnInternalEntityExpandsInAnAttributeValue()
		{
			// structure/svg/attribute-value-via-ENTITY-reference
			SvgDocument document = SvgDocument.Parse(Doctype + "<!ENTITY fill_value \"green\">\n]>\n"
				+ "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect id=\"r\" fill=\"&fill_value;\"/></svg>");
			await Assert.That(document.GetElementById("r")["fill"]).IsEqualTo("green");
		}

		[Test]
		public async Task AnInternalEntityExpandsToElementsEachTimeItIsUsed()
		{
			// structure/svg/elements-via-ENTITY-reference-2
			SvgDocument document = SvgDocument.Parse(Doctype + "<!ENTITY Rect \"\n<rect width='20'/>\n<circle r='5'/>\n\">\n]>\n"
				+ "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"g1\">&Rect;</g><g id=\"g2\">&Rect;</g></svg>");
			await Assert.That(string.Join(",", document.GetElementById("g1").Children.Select(c => c.Name))).IsEqualTo("rect,circle");
			await Assert.That(string.Join(",", document.GetElementById("g2").Children.Select(c => c.Name))).IsEqualTo("rect,circle");
			await Assert.That(document.GetElementById("g2").Children[0]["width"]).IsEqualTo("20");
		}

		[Test]
		[Arguments("rgb(0%, 50%, 0%)", 0, 127, 0, 255)] // painting/fill/rgb-color-with-percentage-values
		[Arguments("RGB(0%, 50%, 0%)", 0, 127, 0, 255)] // painting/fill/uppercase-rgb-color
		[Arguments("rgb(-10%, 50%, 120%)", 0, 127, 255, 255)] // painting/fill/rgb-color-with-percentage-overflow
		[Arguments("rgb(0%, 45.5%, 0%)", 0, 116, 0, 255)]
		[Arguments("transparent", 0, 0, 0, 0)] // painting/fill/transparent
		public async Task ColoursParseAsResvgRendersThem(string text, int red, int green, int blue, int alpha)
		{
			await Assert.That(SvgColor.TryParse(text, out Color color)).IsTrue();
			await Assert.That(color).IsEqualTo(new Color(red, green, blue, alpha));
		}

		[Test]
		public async Task AnRgbColourMixingNumbersAfterANumberWithPercentagesIsInvalid()
		{
			// painting/fill/rgba-0-50percent-0-0.5: the first channel being a number makes the rest numbers.
			await Assert.That(SvgColor.TryParse("rgba(0, 50%, 0, 0.5)", out _)).IsFalse();
		}

		private static byte[] Gzip(string text)
		{
			var compressed = new MemoryStream();
			using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest))
			{
				byte[] bytes = Encoding.UTF8.GetBytes(text);
				gzip.Write(bytes, 0, bytes.Length);
			}

			return compressed.ToArray();
		}

		private const string BlueSquare = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" fill=\"blue\"/></svg>";

		private static Color Centre(string href, byte[] external = null)
		{
			SvgDocument document = SvgDocument.Parse("<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 100 100\">"
				+ $"<image width=\"100\" height=\"100\" xlink:href=\"{href}\"/></svg>");
			document.ResourceResolver = _ => external;
			ImageBuffer image = SvgRenderer.RenderToImage(document, 100, 100);
			return image.GetPixel(50, 50);
		}

		[Test]
		public async Task AnEmbeddedSvgzImageIsUnzipped()
		{
			// structure/image/embedded-svgz
			await Assert.That(Centre("data:image/svg+xml;base64," + System.Convert.ToBase64String(Gzip(BlueSquare)))).IsEqualTo(Color.Blue);
		}

		[Test]
		public async Task AnExternalSvgzImageIsUnzipped()
		{
			// structure/image/external-svgz
			await Assert.That(Centre("image.svgz", Gzip(BlueSquare))).IsEqualTo(Color.Blue);
		}

		[Test]
		public async Task AnSvgImageThatIsNotXmlDrawsNothing()
		{
			// usvg skips an image it cannot parse; the document around it still draws.
			await Assert.That(Centre("data:image/svg+xml;base64," + System.Convert.ToBase64String(Encoding.UTF8.GetBytes("<svg><rect></svg>"))).alpha).IsEqualTo((byte)0);
		}
	}
}
