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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Svg;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// agg/Svg, stage 5: filters. Renders are 100x100 of a 100x100 viewBox, one user unit per pixel. Filters whose
	// expected colours are worked by hand in sRGB say color-interpolation-filters="sRGB"; 0 and 255 channels are the
	// same in linearRGB, so those tests leave the default.
	public class SvgFilterTests
	{
		/// <summary>A filter covering the whole canvas, so the default region does not cut the result.</summary>
		private const string Whole = "filterUnits=\"userSpaceOnUse\" x=\"0\" y=\"0\" width=\"100\" height=\"100\"";

		/// <summary>The left half of the canvas in green.</summary>
		private const string LeftHalf = "<rect width=\"50\" height=\"100\" fill=\"#00ff00\" filter=\"url(#f)\"/>";

		private static ImageBuffer Render(string body)
		{
			return SvgDocument.RenderToImage($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">{body}</svg>", 100, 100);
		}

		private static ImageBuffer Filtered(string filterAttributes, string primitives, string element)
		{
			return Render($"<filter id=\"f\" {filterAttributes}>{primitives}</filter>{element}");
		}

		/// <summary>The straight-alpha colour at SVG coordinates (x, y), y down from the top as SVG counts it.</summary>
		private static Color At(ImageBuffer image, int x, int y) => image.GetPixel(x, image.Height - 1 - y);

		private static int Alpha(ImageBuffer image, int x, int y) => At(image, x, y).alpha;

		private static async Task AssertColor(ImageBuffer image, int x, int y, int red, int green, int blue, int alpha, int tolerance = 1)
		{
			Color c = At(image, x, y);
			await Assert.That(Math.Abs(c.red - red) <= tolerance && Math.Abs(c.green - green) <= tolerance
				&& Math.Abs(c.blue - blue) <= tolerance && Math.Abs(c.alpha - alpha) <= tolerance)
				.IsTrue().Because($"({x}, {y}) is {c.red}, {c.green}, {c.blue}, {c.alpha}; expected {red}, {green}, {blue}, {alpha}");
		}

		[Test]
		public async Task FloodFillsTheDefaultRegionAndItsSubregion()
		{
			// The default region is the box (20..80) grown by 10% of its size each way: 14..86.
			ImageBuffer image = Filtered("", "<feFlood flood-color=\"#ff0000\" flood-opacity=\".5\"/>",
				"<rect x=\"20\" y=\"20\" width=\"60\" height=\"60\" fill=\"#00ff00\" filter=\"url(#f)\"/>");
			await AssertColor(image, 14, 50, 255, 0, 0, 128);
			await AssertColor(image, 85, 85, 255, 0, 0, 128);
			await Assert.That(Alpha(image, 13, 50)).IsEqualTo(0);
			await Assert.That(Alpha(image, 86, 50)).IsEqualTo(0);

			ImageBuffer sub = Filtered(Whole, "<feFlood x=\"10\" width=\"20\" flood-color=\"#0000ff\"/>", LeftHalf);
			await AssertColor(sub, 10, 50, 0, 0, 255, 255);
			await AssertColor(sub, 29, 50, 0, 0, 255, 255);
			await Assert.That(Alpha(sub, 30, 50)).IsEqualTo(0);
			await Assert.That(Alpha(sub, 9, 50)).IsEqualTo(0);
		}

		[Test]
		public async Task OffsetMovesTheGraphicInUserUnits()
		{
			ImageBuffer image = Filtered(Whole, "<feOffset dx=\"10\" dy=\"20\"/>", "<rect x=\"20\" y=\"20\" width=\"20\" height=\"20\" fill=\"#00ff00\" filter=\"url(#f)\"/>");
			await AssertColor(image, 30, 40, 0, 255, 0, 255);
			await AssertColor(image, 49, 59, 0, 255, 0, 255);
			await Assert.That(Alpha(image, 25, 25)).IsEqualTo(0);
			await Assert.That(Alpha(image, 50, 50)).IsEqualTo(0);

			// Under scale(2) the same offset is 20 pixels: the 10x10 rect at 10 lands at 40..60 across, 60..80 down.
			ImageBuffer scaled = Filtered(Whole, "<feOffset dx=\"10\" dy=\"20\"/>",
				"<rect x=\"10\" y=\"10\" width=\"10\" height=\"10\" fill=\"#00ff00\" transform=\"scale(2)\" filter=\"url(#f)\"/>");
			await AssertColor(scaled, 40, 60, 0, 255, 0, 255);
			await AssertColor(scaled, 59, 79, 0, 255, 0, 255);
			await Assert.That(Alpha(scaled, 30, 30)).IsEqualTo(0);

			// primitiveUnits objectBoundingBox: dx is a fraction of the box's width (40), so .5 is 20.
			ImageBuffer box = Filtered(Whole + " primitiveUnits=\"objectBoundingBox\"", "<feOffset dx=\".5\"/>",
				"<rect width=\"40\" height=\"10\" fill=\"#00ff00\" filter=\"url(#f)\"/>");
			await AssertColor(box, 20, 5, 0, 255, 0, 255);
			await AssertColor(box, 59, 5, 0, 255, 0, 255);
			await Assert.That(Alpha(box, 10, 5)).IsEqualTo(0);
		}

		[Test]
		public async Task GaussianBlurSpreadsEdgesSymmetricallyAndKeepsTheInterior()
		{
			foreach (string deviation in new[] { "1", "3" })
			{
				// 1 takes the true-Gaussian path, 3 the three box blurs.
				ImageBuffer image = Filtered(Whole, $"<feGaussianBlur stdDeviation=\"{deviation}\"/>",
					"<rect x=\"30\" y=\"30\" width=\"40\" height=\"40\" fill=\"#00ff00\" filter=\"url(#f)\"/>");
				await AssertColor(image, 50, 50, 0, 255, 0, 255);

				// An edge blurs to half on either side: the pixels either side of it add up to full.
				int inside = Alpha(image, 30, 50);
				int outside = Alpha(image, 29, 50);
				await Assert.That(inside > 128 && outside < 128 && outside > 0).IsTrue().Because($"{deviation}: {inside}, {outside}");
				await Assert.That(Math.Abs(inside + outside - 255)).IsLessThanOrEqualTo(3);
				await Assert.That(Alpha(image, 15, 50)).IsEqualTo(0);
			}

			// "3 0" blurs across only.
			ImageBuffer across = Filtered(Whole, "<feGaussianBlur stdDeviation=\"3 0\"/>",
				"<rect x=\"30\" y=\"30\" width=\"40\" height=\"40\" fill=\"#00ff00\" filter=\"url(#f)\"/>");
			await Assert.That(Alpha(across, 28, 50)).IsGreaterThan(0);
			await Assert.That(Alpha(across, 50, 28)).IsEqualTo(0);
		}

		[Test]
		public async Task CompositeOperatorsCombineInAndIn2()
		{
			const string Blue = "<feFlood flood-color=\"#0000ff\" result=\"blue\"/>";
			ImageBuffer Composite(string attributes) => Filtered(Whole, Blue + $"<feComposite {attributes}/>", LeftHalf);

			ImageBuffer over = Composite("in=\"SourceGraphic\" in2=\"blue\"");
			await AssertColor(over, 25, 50, 0, 255, 0, 255);
			await AssertColor(over, 75, 50, 0, 0, 255, 255);

			ImageBuffer inside = Composite("in=\"blue\" in2=\"SourceGraphic\" operator=\"in\"");
			await AssertColor(inside, 25, 50, 0, 0, 255, 255);
			await Assert.That(Alpha(inside, 75, 50)).IsEqualTo(0);

			ImageBuffer outside = Composite("in=\"blue\" in2=\"SourceGraphic\" operator=\"out\"");
			await Assert.That(Alpha(outside, 25, 50)).IsEqualTo(0);
			await AssertColor(outside, 75, 50, 0, 0, 255, 255);

			ImageBuffer atop = Composite("in=\"blue\" in2=\"SourceGraphic\" operator=\"atop\"");
			await AssertColor(atop, 25, 50, 0, 0, 255, 255);
			await Assert.That(Alpha(atop, 75, 50)).IsEqualTo(0);

			ImageBuffer xor = Composite("in=\"blue\" in2=\"SourceGraphic\" operator=\"xor\"");
			await Assert.That(Alpha(xor, 25, 50)).IsEqualTo(0);
			await AssertColor(xor, 75, 50, 0, 0, 255, 255);

			// Arithmetic, in sRGB: .5 * green + .5 * blue is (0, .5, .5, 1) on the left, half-opaque blue on the right.
			ImageBuffer arithmetic = Filtered(Whole + " color-interpolation-filters=\"sRGB\"",
				Blue + "<feComposite in=\"SourceGraphic\" in2=\"blue\" operator=\"arithmetic\" k2=\".5\" k3=\".5\"/>", LeftHalf);
			await AssertColor(arithmetic, 25, 50, 0, 128, 128, 255);
			await AssertColor(arithmetic, 75, 50, 0, 0, 255, 128);
		}

		[Test]
		public async Task MergeStacksItsNodesAndResultsChainByName()
		{
			ImageBuffer merged = Filtered(Whole, "<feFlood flood-color=\"#ff0000\" result=\"red\"/><feMerge><feMergeNode in=\"red\"/><feMergeNode in=\"SourceGraphic\"/></feMerge>", LeftHalf);
			await AssertColor(merged, 25, 50, 0, 255, 0, 255);
			await AssertColor(merged, 75, 50, 255, 0, 0, 255);

			// A named result is found past later ones; an unknown name falls back to the previous result.
			ImageBuffer named = Filtered(Whole, "<feFlood flood-color=\"#ff0000\" result=\"a\"/><feFlood flood-color=\"#0000ff\"/><feOffset in=\"a\"/>", LeftHalf);
			await AssertColor(named, 75, 50, 255, 0, 0, 255);
			ImageBuffer unknown = Filtered(Whole, "<feFlood flood-color=\"#ff0000\" result=\"a\"/><feFlood flood-color=\"#0000ff\"/><feOffset in=\"nope\"/>", LeftHalf);
			await AssertColor(unknown, 75, 50, 0, 0, 255, 255);

			// SourceAlpha is the graphic's alpha in black.
			ImageBuffer alpha = Filtered(Whole, "<feOffset in=\"SourceAlpha\"/>", LeftHalf);
			await AssertColor(alpha, 25, 50, 0, 0, 0, 255);
		}

		[Test]
		public async Task ColorMatrixTypesTransformStraightColour()
		{
			const string Red = "<rect width=\"100\" height=\"100\" fill=\"#ff0000\" filter=\"url(#f)\"/>";
			string SRgb = Whole + " color-interpolation-filters=\"sRGB\"";

			// saturate 0 in sRGB: every channel is .213 of red, 54.
			await AssertColor(Filtered(SRgb, "<feColorMatrix type=\"saturate\" values=\"0\"/>", Red), 50, 50, 54, 54, 54, 255);

			// The same in the default linearRGB: .213 linear is 127 once back in sRGB.
			await AssertColor(Filtered(Whole, "<feColorMatrix type=\"saturate\" values=\"0\"/>", Red), 50, 50, 127, 127, 127, 255);

			// hueRotate 180: red to (0, .426, .426).
			await AssertColor(Filtered(SRgb, "<feColorMatrix type=\"hueRotate\" values=\"180\"/>", Red), 50, 50, 0, 109, 109, 255);

			// luminanceToAlpha: white's luminance is 1, in black.
			await AssertColor(Filtered(Whole, "<feColorMatrix type=\"luminanceToAlpha\"/>", Red.Replace("#ff0000", "#ffffff")), 50, 50, 0, 0, 0, 255);

			// matrix: red and blue swapped.
			await AssertColor(Filtered(Whole, "<feColorMatrix values=\"0 0 1 0 0  0 1 0 0 0  1 0 0 0 0  0 0 0 1 0\"/>", Red), 50, 50, 0, 0, 255, 255);
		}

		[Test]
		public async Task BlendModesMixInOverIn2()
		{
			string Blend(string flood, string mode) =>
				$"<feFlood flood-color=\"{flood}\" result=\"b\"/><feBlend in=\"SourceGraphic\" in2=\"b\" mode=\"{mode}\"/>";
			string Graphic(string fill) => $"<rect width=\"100\" height=\"100\" fill=\"{fill}\" filter=\"url(#f)\"/>";

			await AssertColor(Filtered(Whole, Blend("#00ffff", "multiply"), Graphic("#ffff00")), 50, 50, 0, 255, 0, 255);
			await AssertColor(Filtered(Whole, Blend("#0000ff", "screen"), Graphic("#ff0000")), 50, 50, 255, 0, 255, 255);
			await AssertColor(Filtered(Whole, Blend("#00ffff", "darken"), Graphic("#ffff00")), 50, 50, 0, 255, 0, 255);
			await AssertColor(Filtered(Whole, Blend("#00ffff", "lighten"), Graphic("#ffff00")), 50, 50, 255, 255, 255, 255);
			await AssertColor(Filtered(Whole, Blend("#00ffff", "difference"), Graphic("#ffff00")), 50, 50, 255, 0, 255, 255);
			await AssertColor(Filtered(Whole, Blend("#00ffff", "normal"), Graphic("#ffff00")), 50, 50, 255, 255, 0, 255);
		}

		[Test]
		public async Task DropShadowDrawsAMovedShadowUnderTheGraphic()
		{
			ImageBuffer image = Filtered(Whole, "<feDropShadow dx=\"10\" dy=\"10\" stdDeviation=\"0\" flood-opacity=\".5\"/>",
				"<rect x=\"20\" y=\"20\" width=\"20\" height=\"20\" fill=\"#00ff00\" filter=\"url(#f)\"/>");
			await AssertColor(image, 30, 30, 0, 255, 0, 255);
			await AssertColor(image, 45, 45, 0, 0, 0, 128);
			await Assert.That(Alpha(image, 25, 45)).IsEqualTo(0);
		}

		[Test]
		public async Task BadReferencesHideTheElementAndNoneOrFunctionsDoNot()
		{
			await Assert.That(Alpha(Render("<rect width=\"100\" height=\"100\" filter=\"url(#missing)\"/>"), 50, 50)).IsEqualTo(0);
			await Assert.That(Alpha(Render("<rect id=\"r\" width=\"10\" height=\"10\"/><rect width=\"100\" height=\"100\" filter=\"url(#r)\"/>"), 50, 50)).IsEqualTo(0);
			await Assert.That(Alpha(Render("<filter id=\"f\"/><rect width=\"100\" height=\"100\" filter=\"url(#f)\"/>"), 50, 50)).IsEqualTo(0);
			await Assert.That(Alpha(Render("<rect width=\"100\" height=\"100\" filter=\"none\"/>"), 50, 50)).IsEqualTo(255);
			await Assert.That(Alpha(Render("<rect width=\"100\" height=\"100\" filter=\"blur(2)\"/>"), 50, 50)).IsEqualTo(255);

			// A filter that inherits its primitives through href, then a second filter in the list, both apply.
			ImageBuffer chained = Render($"<filter id=\"a\" {Whole}><feOffset dx=\"10\"/></filter><filter id=\"b\" href=\"#a\"/>"
				+ $"<filter id=\"c\" {Whole}><feOffset dy=\"10\"/></filter>"
				+ "<rect width=\"10\" height=\"10\" fill=\"#00ff00\" filter=\"url(#b) url(#c)\"/>");
			await AssertColor(chained, 15, 15, 0, 255, 0, 255);
			await Assert.That(Alpha(chained, 5, 5)).IsEqualTo(0);
		}

		[Test]
		public async Task OpacityAppliesAfterTheFilterClipAndMask()
		{
			// The flood replaces the group's drawing, so opacity inside the filter would be lost: resvg applies it last.
			ImageBuffer group = Render($"<filter id=\"f\" {Whole}><feFlood flood-color=\"#ff0000\"/></filter>"
				+ "<g opacity=\".5\" filter=\"url(#f)\"><rect width=\"10\" height=\"10\"/></g>");
			await AssertColor(group, 50, 50, 255, 0, 0, 128);

			ImageBuffer shape = Render($"<clipPath id=\"c\"><rect width=\"50\" height=\"100\"/></clipPath><filter id=\"f\" {Whole}><feFlood flood-color=\"#ff0000\"/></filter>"
				+ "<rect width=\"10\" height=\"10\" opacity=\".5\" filter=\"url(#f)\" clip-path=\"url(#c)\"/>");
			await AssertColor(shape, 25, 50, 255, 0, 0, 128);
			await Assert.That(Alpha(shape, 75, 50)).IsEqualTo(0);
		}

		[Test]
		public async Task AClipPathCutsTheFilteredResult()
		{
			ImageBuffer image = Render($"<clipPath id=\"c\"><rect width=\"50\" height=\"100\"/></clipPath><filter id=\"f\" {Whole}><feFlood flood-color=\"#0000ff\"/></filter>"
				+ "<rect x=\"40\" y=\"40\" width=\"20\" height=\"20\" filter=\"url(#f)\" clip-path=\"url(#c)\"/>");
			await AssertColor(image, 10, 10, 0, 0, 255, 255);
			await Assert.That(Alpha(image, 60, 10)).IsEqualTo(0);
		}
	}
}
