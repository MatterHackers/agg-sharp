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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Svg;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// agg/Svg structure and group compositing, checked against resvg's semantics. Renders are 100x100 of a
	// 100x100 viewBox, so one user unit is one pixel.
	public class SvgStructureTests
	{
		private static ImageBuffer Render(string body, string rootAttributes = "viewBox=\"0 0 100 100\"")
		{
			return SvgDocument.RenderToImage($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" {rootAttributes}>{body}</svg>", 100, 100);
		}

		/// <summary>The pixel at SVG coordinates (x, y), y down from the top as SVG counts it.</summary>
		private static Color At(ImageBuffer image, int x, int y) => image.GetPixel(x, image.Height - 1 - y);

		[Test]
		public async Task TransformOriginMovesTheTransformsCentre()
		{
			// usvg: the origin's percentages and keywords are of the viewport, not the element's box.
			ImageBuffer center = Render("<rect x=\"40\" y=\"40\" width=\"20\" height=\"20\" fill=\"green\" transform=\"scale(2)\" transform-origin=\"center\"/>");
			await Assert.That(At(center, 35, 35).alpha).IsEqualTo((byte)255);

			ImageBuffer corner = Render("<rect x=\"80\" y=\"80\" width=\"20\" height=\"20\" fill=\"green\" transform=\"scale(2)\" transform-origin=\"bottom right\"/>");
			await Assert.That(At(corner, 65, 65).alpha).IsEqualTo((byte)255);

			// One keyword that names a side: the other axis is centred. "top" is (50%, 0).
			ImageBuffer top = Render("<rect x=\"40\" y=\"0\" width=\"20\" height=\"20\" fill=\"green\" transform=\"scale(2)\" style=\"transform-origin:top\"/>");
			await Assert.That(At(top, 35, 35).alpha).IsEqualTo((byte)255);

			ImageBuffer px = Render("<rect x=\"40\" y=\"40\" width=\"20\" height=\"20\" fill=\"green\" transform=\"scale(2)\" transform-origin=\"50px 50%\"/>");
			await Assert.That(At(px, 35, 35).alpha).IsEqualTo((byte)255);
		}

		[Test]
		[Arguments("<rect requiredExtensions=\"http://example.org/bogus\" width=\"100\" height=\"100\" fill=\"red\"/><rect width=\"100\" height=\"100\" fill=\"green\"/><rect width=\"100\" height=\"100\" fill=\"red\"/>")]
		[Arguments("<unknown/><rect width=\"100\" height=\"100\" fill=\"green\"/><rect width=\"100\" height=\"100\" fill=\"red\"/>")]
		[Arguments("<rect systemLanguage=\"ru-RU\" width=\"100\" height=\"100\" fill=\"red\"/><rect systemLanguage=\"ru, en-GB\" width=\"100\" height=\"100\" fill=\"green\"/><rect width=\"100\" height=\"100\" fill=\"red\"/>")]
		[Arguments("<rect requiredFeatures=\"http://www.w3.org/TR/SVG11/feature#Shape\" width=\"100\" height=\"100\" fill=\"green\"/><rect width=\"100\" height=\"100\" fill=\"red\"/>")]
		public async Task SwitchDrawsOnlyItsFirstChildWhoseConditionsPass(string children)
		{
			// usvg's switch: the first element child (unknown elements are not SVG) with no requiredExtensions, only
			// known requiredFeatures and a systemLanguage matching "en" (a region suffix ignored).
			await Assert.That(At(Render($"<switch>{children}</switch>"), 50, 50)).IsEqualTo(new Color(0, 128, 0, 255));
		}

		[Test]
		public async Task AFailedConditionHidesAnElementOutsideASwitch()
		{
			await Assert.That(At(Render("<rect width=\"100\" height=\"100\" fill=\"red\" systemLanguage=\"ru\"/>"), 50, 50).alpha).IsEqualTo((byte)0);
			await Assert.That(At(Render("<rect width=\"100\" height=\"100\" fill=\"red\"/>", "viewBox=\"0 0 100 100\" systemLanguage=\"ru\""), 50, 50).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task ASymbolDrawsOnlyThroughAUse()
		{
			// Unused, a symbol draws nothing; used, its children draw with its opacity (its SVG 2 transform ignored).
			await Assert.That(At(Render("<symbol id=\"s\"><rect width=\"100\" height=\"100\" fill=\"green\"/></symbol>"), 50, 50).alpha).IsEqualTo((byte)0);
			ImageBuffer used = Render("<symbol id=\"s\" opacity=\".5\" transform=\"translate(50 0)\"><rect x=\"10\" y=\"10\" width=\"20\" height=\"20\" fill=\"#008000\"/></symbol><use xlink:href=\"#s\"/>");
			await Assert.That(At(used, 20, 20)).IsEqualTo(new Color(0, 128, 0, 128));
			await Assert.That(At(used, 70, 20).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task ASymbolIsANewViewportAtTheUsesRectangle()
		{
			// The use's x/y/width/height are the viewport: the viewBox fits into it, and what falls outside is clipped.
			// -50 -50 150 150 into 100x100 at (50, 50): scale 2/3, content (0, 0) lands at (50 + 33.3, 50 + 33.3).
			ImageBuffer fitted = Render("<symbol id=\"s\" viewBox=\"-50 -50 150 150\"><rect width=\"150\" height=\"150\" fill=\"green\"/></symbol>"
				+ "<use xlink:href=\"#s\" x=\"25\" y=\"25\" width=\"50\" height=\"50\"/>");
			await Assert.That(At(fitted, 45, 45).alpha).IsEqualTo((byte)255);
			await Assert.That(At(fitted, 40, 40).alpha).IsEqualTo((byte)0);
			await Assert.That(At(fitted, 80, 80).alpha).IsEqualTo((byte)0);

			// No viewBox: the viewport is the use's rectangle (100% by default), moved with the use's transform.
			ImageBuffer clipped = Render("<symbol id=\"s\"><rect x=\"-20\" y=\"-20\" width=\"200\" height=\"200\" fill=\"green\"/></symbol>"
				+ "<use xlink:href=\"#s\" transform=\"translate(10 10)\" width=\"50\" height=\"50\"/>");
			await Assert.That(At(clipped, 5, 5).alpha).IsEqualTo((byte)0);
			await Assert.That(At(clipped, 30, 30).alpha).IsEqualTo((byte)255);
			await Assert.That(At(clipped, 70, 70).alpha).IsEqualTo((byte)0);

			// overflow="visible" does not clip.
			ImageBuffer visible = Render("<symbol id=\"s\" overflow=\"visible\"><rect x=\"-20\" y=\"-20\" width=\"200\" height=\"200\" fill=\"green\"/></symbol>"
				+ "<use xlink:href=\"#s\" transform=\"translate(10 10)\" width=\"50\" height=\"50\"/>");
			await Assert.That(At(visible, 5, 5).alpha).IsEqualTo((byte)255);
		}

		[Test]
		public async Task ANestedSvgIsANewViewportClippedToItsRectangle()
		{
			// Its x/y/width/height clip what it draws, and percentages inside are of its own width/height.
			ImageBuffer clipped = Render("<svg x=\"20\" y=\"20\" width=\"40\" height=\"40\"><rect width=\"100\" height=\"100\" fill=\"green\"/></svg>");
			await Assert.That(At(clipped, 30, 30).alpha).IsEqualTo((byte)255);
			await Assert.That(At(clipped, 70, 70).alpha).IsEqualTo((byte)0);

			ImageBuffer percent = Render("<svg width=\"50\" height=\"50\" overflow=\"visible\"><rect width=\"100%\" height=\"100%\" fill=\"green\"/></svg>");
			await Assert.That(At(percent, 40, 40).alpha).IsEqualTo((byte)255);
			await Assert.That(At(percent, 60, 60).alpha).IsEqualTo((byte)0);

			// Only a viewBox and no rectangle: not clipped.
			ImageBuffer viewBoxOnly = Render("<svg viewBox=\"0 0 100 100\"><rect x=\"-20\" y=\"-20\" width=\"50\" height=\"50\" fill=\"green\"/></svg>", "viewBox=\"-50 -50 100 100\"");
			await Assert.That(At(viewBoxOnly, 35, 35).alpha).IsEqualTo((byte)255);
		}

		[Test]
		public async Task AUsesWidthAndHeightReplaceTheSvgsItReferences()
		{
			// The use's width (30) replaces the svg's 80; its height stays 80. Only the nearest use's size counts.
			ImageBuffer used = Render("<defs><svg id=\"s\" width=\"80\" height=\"80\"><rect width=\"100\" height=\"100\" fill=\"green\"/></svg></defs><use xlink:href=\"#s\" width=\"30\"/>");
			await Assert.That(At(used, 20, 70).alpha).IsEqualTo((byte)255);
			await Assert.That(At(used, 40, 20).alpha).IsEqualTo((byte)0);
			await Assert.That(At(used, 20, 90).alpha).IsEqualTo((byte)0);

			ImageBuffer nested = Render("<defs><svg id=\"s\" width=\"80\" height=\"80\"><rect width=\"100\" height=\"100\" fill=\"green\"/></svg><use id=\"u\" xlink:href=\"#s\" height=\"30\"/></defs><use xlink:href=\"#u\" width=\"10\"/>");
			await Assert.That(At(nested, 70, 20).alpha).IsEqualTo((byte)255);
			await Assert.That(At(nested, 20, 40).alpha).IsEqualTo((byte)0);
		}

		[Test]
		[Arguments("width=\"0\" height=\"0\"")]
		[Arguments("width=\"-50\" height=\"-100\"")]
		[Arguments("viewBox=\"0 0 100 100\" width=\"100\" height=\"0\"")]
		public async Task ARootWithNoSizeDrawsNothing(string rootAttributes)
		{
			// usvg rejects a document whose width or height is zero or negative; resvg draws nothing.
			await Assert.That(At(Render("<rect width=\"100\" height=\"100\" fill=\"green\"/>", rootAttributes), 50, 50).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task ElementsOutsideTheSvgNamespaceAreNotDrawn()
		{
			// usvg skips an element (and all it holds) whose namespace is not SVG's, whatever its local name.
			ImageBuffer image = Render("<s:g xmlns=\"http://www.example.org/notsvg\" xmlns:s=\"http://www.w3.org/2000/svg\">"
				+ "<s:rect width=\"100\" height=\"100\" fill=\"green\"/><rect width=\"100\" height=\"100\" fill=\"red\"/></s:g>");
			await Assert.That(At(image, 50, 50)).IsEqualTo(new Color(0, 128, 0, 255));
		}

		[Test]
		[Arguments("<circle cx=\"50\" cy=\"50\" r=\"40.3\" fill=\"green\" shape-rendering=\"crispEdges\"/>")]
		[Arguments("<g shape-rendering=\"optimizeSpeed\"><circle cx=\"50\" cy=\"50\" r=\"40.3\" fill=\"none\" stroke=\"green\" stroke-width=\"3.3\"/></g>")]
		public async Task ShapeRenderingWithoutAntiAliasingDrawsWholePixels(string body)
		{
			// resvg draws crispEdges and optimizeSpeed (inherited) with anti-aliasing off: every pixel is covered or not.
			ImageBuffer image = Render(body);
			int partial = 0;
			for (int y = 0; y < 100; y++)
			{
				for (int x = 0; x < 100; x++)
				{
					byte alpha = At(image, x, y).alpha;
					partial += alpha != 0 && alpha != 255 ? 1 : 0;
				}
			}

			await Assert.That(partial).IsEqualTo(0);
			await Assert.That(At(image, 50, 10).alpha).IsEqualTo((byte)255);
		}

		[Test]
		public async Task ShapeRenderingDoesNotAffectText()
		{
			ImageBuffer image = Render("<text x=\"5\" y=\"70\" font-size=\"60\" shape-rendering=\"crispEdges\">Text</text>");
			bool partial = false;
			for (int y = 0; y < 100 && !partial; y++)
			{
				for (int x = 0; x < 100 && !partial; x++)
				{
					byte alpha = At(image, x, y).alpha;
					partial = alpha != 0 && alpha != 255;
				}
			}

			await Assert.That(partial).IsTrue();
		}
	}
}
