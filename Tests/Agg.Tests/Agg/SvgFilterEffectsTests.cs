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
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// agg/Svg filters past the basic set: 100x100 renders of a 100x100 viewBox, one user unit per pixel. Expected
	// colours are worked by hand in sRGB, so filters say color-interpolation-filters="sRGB".
	public class SvgFilterEffectsTests
	{
		private const string Whole = "filterUnits=\"userSpaceOnUse\" x=\"0\" y=\"0\" width=\"100\" height=\"100\" color-interpolation-filters=\"sRGB\"";

		/// <summary>The left half of the canvas in green.</summary>
		private const string LeftHalf = "<rect width=\"50\" height=\"100\" fill=\"#00ff00\" filter=\"url(#f)\"/>";

		private static ImageBuffer Filtered(string primitives, string element, string filterAttributes = Whole)
		{
			return SvgDocument.RenderToImage($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><filter id=\"f\" {filterAttributes}>{primitives}</filter>{element}</svg>", 100, 100);
		}

		/// <summary>The straight-alpha colour at SVG coordinates (x, y), y down.</summary>
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
		public async Task ComponentTransferMapsEachChannel()
		{
			// Red: R linear * .5 is 128; G table 1 -> 0 maps 0 to 255; B discrete keeps 0; A gamma .5 * a is half.
			ImageBuffer image = Filtered("<feComponentTransfer><feFuncR type=\"linear\" slope=\".5\"/><feFuncG type=\"table\" tableValues=\"1 0\"/>"
				+ "<feFuncB type=\"discrete\" tableValues=\"0 .5\"/><feFuncA type=\"gamma\" amplitude=\".5\"/></feComponentTransfer>",
				"<rect width=\"100\" height=\"100\" fill=\"#ff0000\" filter=\"url(#f)\"/>");
			await AssertColor(image, 50, 50, 128, 255, 0, 128);

			// discrete with two values: .5 and up take the second.
			ImageBuffer discrete = Filtered("<feComponentTransfer><feFuncR type=\"discrete\" tableValues=\"0 .5\"/></feComponentTransfer>",
				"<rect width=\"100\" height=\"100\" fill=\"#ff0000\" filter=\"url(#f)\"/>");
			await AssertColor(discrete, 50, 50, 128, 0, 0, 255);
		}

		[Test]
		public async Task MorphologyErodesAndDilatesByTheRadius()
		{
			const string Square = "<rect x=\"30\" y=\"30\" width=\"40\" height=\"40\" fill=\"#00ff00\" filter=\"url(#f)\"/>";
			ImageBuffer dilated = Filtered("<feMorphology operator=\"dilate\" radius=\"5\"/>", Square);
			await AssertColor(dilated, 25, 50, 0, 255, 0, 255);
			await Assert.That(Alpha(dilated, 24, 50)).IsEqualTo(0);

			ImageBuffer eroded = Filtered("<feMorphology radius=\"5\"/>", Square);
			await AssertColor(eroded, 35, 50, 0, 255, 0, 255);
			await Assert.That(Alpha(eroded, 34, 50)).IsEqualTo(0);
		}

		[Test]
		public async Task TileRepeatsTheInputsSubregion()
		{
			// The offset's subregion 40..60 holds green then clear; tiled from 40, it repeats every 20.
			ImageBuffer image = Filtered("<feOffset x=\"40\" width=\"20\"/><feTile/>", LeftHalf);
			await AssertColor(image, 0, 50, 0, 255, 0, 255);
			await AssertColor(image, 25, 50, 0, 255, 0, 255);
			await Assert.That(Alpha(image, 10, 50)).IsEqualTo(0);
			await Assert.That(Alpha(image, 75, 50)).IsEqualTo(0);
			await AssertColor(image, 85, 50, 0, 255, 0, 255);
		}

		[Test]
		public async Task ConvolveMatrixTurnsTheKernelAndHandlesEdges()
		{
			// The kernel is turned half a turn, so a 1 right of centre takes the pixel to the left: a shift right.
			const string Kernel = "<feConvolveMatrix kernelMatrix=\"0 0 0 0 0 1 0 0 0\"";
			ImageBuffer image = Filtered(Kernel + "/>", LeftHalf);
			await AssertColor(image, 50, 50, 0, 255, 0, 255);
			await Assert.That(Alpha(image, 51, 50)).IsEqualTo(0);
			await AssertColor(image, 0, 50, 0, 255, 0, 255);

			ImageBuffer none = Filtered(Kernel + " edgeMode=\"none\"/>", LeftHalf);
			await Assert.That(Alpha(none, 0, 50)).IsEqualTo(0);

			// A 1 below centre takes the pixel above: a shift down, so the top row of a full-height rect clears.
			ImageBuffer down = Filtered("<feConvolveMatrix kernelMatrix=\"0 0 0 0 0 0 0 1 0\" edgeMode=\"none\"/>", LeftHalf);
			await Assert.That(Alpha(down, 25, 0)).IsEqualTo(0);
			await AssertColor(down, 25, 99, 0, 255, 0, 255);

			// divisor 2 halves everything.
			await AssertColor(Filtered("<feConvolveMatrix kernelMatrix=\"0 0 0 0 1 0 0 0 0\" divisor=\"2\"/>", LeftHalf), 25, 50, 0, 255, 0, 128);
		}

		[Test]
		public async Task NonSeparableBlendModesMixHueSaturationAndLuminosity()
		{
			string Blend(string mode) => $"<feFlood flood-color=\"#00ffff\" result=\"b\"/><feBlend in=\"SourceGraphic\" in2=\"b\" mode=\"{mode}\"/>";
			string Graphic(string fill) => $"<rect width=\"100\" height=\"100\" fill=\"{fill}\" filter=\"url(#f)\"/>";

			// Cyan's luminosity is .7, red's .3. luminosity: cyan at .3 is (-.4, .6, .6), clipped to (0, .43, .43).
			await AssertColor(Filtered(Blend("luminosity"), Graphic("#ff0000")), 50, 50, 0, 109, 109, 255);

			// color and hue: red at .7 is (1.4, .4, .4), clipped to (1, .57, .57).
			await AssertColor(Filtered(Blend("color"), Graphic("#ff0000")), 50, 50, 255, 146, 146, 255);
			await AssertColor(Filtered(Blend("hue"), Graphic("#ff0000")), 50, 50, 255, 146, 146, 255);

			// saturation: grey's saturation is 0, so cyan goes grey at its own luminosity, .7.
			await AssertColor(Filtered(Blend("saturation"), Graphic("#808080")), 50, 50, 179, 179, 179, 255);
		}

		[Test]
		public async Task TurbulenceIsZeroOnTheLatticeAndVariesBetween()
		{
			const string Graphic = "<rect width=\"100\" height=\"100\" filter=\"url(#f)\"/>";

			// Perlin noise is 0 at every lattice point: at baseFrequency .1 that is every 10th user unit, in every
			// octave. fractalNoise maps 0 to .5 in each channel; turbulence sums |noise|, 0.
			ImageBuffer fractal = Filtered("<feTurbulence type=\"fractalNoise\" baseFrequency=\".1\" numOctaves=\"2\"/>", Graphic);
			await AssertColor(fractal, 10, 20, 128, 128, 128, 128);
			await AssertColor(fractal, 40, 70, 128, 128, 128, 128);
			ImageBuffer turbulence = Filtered("<feTurbulence baseFrequency=\".1\"/>", Graphic);
			await Assert.That(Alpha(turbulence, 30, 30)).IsEqualTo(0);

			// Between lattice points the noise is not 0, and the seed changes it.
			await Assert.That(Alpha(turbulence, 35, 33)).IsGreaterThan(0);
			ImageBuffer seeded = Filtered("<feTurbulence baseFrequency=\".1\" seed=\"7\"/>", Graphic);
			await Assert.That(At(seeded, 35, 33) != At(turbulence, 35, 33)).IsTrue();

			// baseFrequency 0 (and a negative, which usvg reads as 0) is the lattice everywhere.
			await AssertColor(Filtered("<feTurbulence type=\"fractalNoise\" baseFrequency=\"-1\"/>", Graphic), 35, 33, 128, 128, 128, 128);
		}

		[Test]
		public async Task LightingLightsTheAlphaSurface()
		{
			// A flat surface (the rect draws no alpha) has normal (0, 0, 1).
			const string Flat = "<rect width=\"100\" height=\"100\" fill=\"none\" filter=\"url(#f)\"/>";
			string Diffuse(string attributes, string light) => $"<feDiffuseLighting {attributes}>{light}</feDiffuseLighting>";

			// A distant light straight overhead: N.L is 1, so the colour is kd * lighting-color, opaque.
			await AssertColor(Filtered(Diffuse("diffuseConstant=\".5\" lighting-color=\"#ff0000\"", "<feDistantLight elevation=\"90\"/>"), Flat), 50, 50, 128, 0, 0, 255);

			// Level with the surface: N.L is 0, black.
			await AssertColor(Filtered(Diffuse("", "<feDistantLight/>"), Flat), 50, 50, 0, 0, 0, 255);

			// A point light 10 above (50, 50): full under it, cos 45 (180) 10 across.
			ImageBuffer point = Filtered(Diffuse("", "<fePointLight x=\"50\" y=\"50\" z=\"10\"/>"), Flat);
			await AssertColor(point, 50, 50, 255, 255, 255, 255);
			await AssertColor(point, 60, 50, 180, 180, 180, 255);
			await AssertColor(point, 50, 40, 180, 180, 180, 255);

			// A spot pointing straight down with a 30 degree cone: 2 across, (-L.S) * (N.L) is (10 / sqrt(104))^2, 245;
			// 10 across is 45 degrees off, outside the cone.
			ImageBuffer spot = Filtered(Diffuse("", "<feSpotLight x=\"50\" y=\"50\" z=\"10\" pointsAtX=\"50\" pointsAtY=\"50\" limitingConeAngle=\"30\"/>"), Flat);
			await AssertColor(spot, 50, 50, 255, 255, 255, 255);
			await AssertColor(spot, 52, 50, 245, 245, 245, 255);
			await AssertColor(spot, 60, 50, 0, 0, 0, 255);

			// Specular overhead: N.H is 1, ks * colour, with alpha the largest channel.
			await AssertColor(Filtered("<feSpecularLighting specularConstant=\".5\" lighting-color=\"#ff0000\"><feDistantLight elevation=\"90\"/></feSpecularLighting>", Flat),
				50, 50, 255, 0, 0, 128);

			// A slope: alpha falling left to right (the left half's right edge) tilts the normal toward +x, so a light
			// from +x (azimuth 0) at elevation 45 lights the edge fully where the flat interior gets cos 45.
			ImageBuffer edge = Filtered(Diffuse("", "<feDistantLight elevation=\"45\"/>"), LeftHalf);
			await AssertColor(edge, 25, 50, 180, 180, 180, 255);
			await Assert.That((int)At(edge, 49, 50).red).IsGreaterThan(200);
		}

		[Test]
		public async Task ObjectBoundingBoxPrimitiveUnitsPlaceLightsInTheBoxButLeaveTurbulenceFrequencies()
		{
			// A 50x50 box at (20, 20) with nothing drawn, so the surface is flat; the region is the whole canvas.
			const string Box = "<rect x=\"20\" y=\"20\" width=\"50\" height=\"50\" fill=\"none\" filter=\"url(#f)\"/>";
			const string BoxUnits = Whole + " primitiveUnits=\"objectBoundingBox\"";

			// (.2, .6) of the box is (30, 50); z is a fraction of the box's normalized diagonal, 50, so 10 - lit as
			// resvg's fePointLight/primitiveUnits=objectBoundingBox reference is.
			ImageBuffer point = Filtered("<feDiffuseLighting><fePointLight x=\".2\" y=\".6\" z=\".2\"/></feDiffuseLighting>", Box, BoxUnits);
			await AssertColor(point, 30, 50, 255, 255, 255, 255);
			await AssertColor(point, 40, 50, 180, 180, 180, 255);
			await AssertColor(point, 30, 40, 180, 180, 180, 255);

			// A spot's pointsAt is in the box too: from above (30, 50) straight down onto it, a 30 degree cone.
			ImageBuffer spot = Filtered("<feDiffuseLighting><feSpotLight x=\".2\" y=\".6\" z=\".2\" pointsAtX=\".2\" pointsAtY=\".6\" limitingConeAngle=\"30\"/></feDiffuseLighting>", Box, BoxUnits);
			await AssertColor(spot, 30, 50, 255, 255, 255, 255);
			await AssertColor(spot, 40, 50, 0, 0, 0, 255);

			// baseFrequency is not a length: resvg's feTurbulence/primitiveUnits=objectBoundingBox reference keeps it
			// per user unit.
			const string Noise = "<feTurbulence baseFrequency=\".05\" numOctaves=\"2\"/>";
			ImageBuffer boxNoise = Filtered(Noise, Box, BoxUnits);
			ImageBuffer userNoise = Filtered(Noise, Box);
			await Assert.That(boxNoise.GetBuffer()).IsEquivalentTo(userNoise.GetBuffer(), CollectionOrdering.Matching);
		}

		[Test]
		public async Task FilterFunctionsRunAsOnePrimitiveFilters()
		{
			ImageBuffer Red(string filter) => SvgDocument.RenderToImage(
				$"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><rect x=\"20\" y=\"20\" width=\"60\" height=\"60\" fill=\"#ff0000\" style=\"filter: {filter}\"/></svg>", 100, 100);

			// Functions work in sRGB: red's luminance weight .2126 is 54.
			await AssertColor(Red("grayscale(1)"), 50, 50, 54, 54, 54, 255);
			await AssertColor(Red("grayscale(100%)"), 50, 50, 54, 54, 54, 255);
			await AssertColor(Red("saturate(0)"), 50, 50, 54, 54, 54, 255);
			await AssertColor(Red("hue-rotate(180deg)"), 50, 50, 0, 109, 109, 255);
			await AssertColor(Red("hue-rotate(.5turn)"), 50, 50, 0, 109, 109, 255);

			// invert(.25) maps through the table .25 .75: 1 to .75, 0 to .25.
			await AssertColor(Red("invert(.25)"), 50, 50, 191, 64, 64, 255);
			await AssertColor(Red("opacity(.5)"), 50, 50, 255, 0, 0, 128);
			await AssertColor(Red("brightness(.5)"), 50, 50, 128, 0, 0, 255);
			await AssertColor(Red("contrast(0)"), 50, 50, 128, 128, 128, 255);

			// sepia(1): red takes the matrix's first column, .393 .349 .272; white its row sums, 1.351 1.203 .937.
			await AssertColor(Red("sepia(1)"), 50, 50, 100, 89, 69, 255);
			ImageBuffer white = SvgDocument.RenderToImage(
				"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><rect width=\"100\" height=\"100\" fill=\"#ffffff\" filter=\"sepia(1)\"/></svg>", 100, 100);
			await AssertColor(white, 50, 50, 255, 255, 239, 255);

			// Functions chain, left to right: grey 54, inverted.
			await AssertColor(Red("grayscale(1) invert(1)"), 50, 50, 201, 201, 201, 255);

			// blur spreads past the box (its region is -50% 200%); drop-shadow draws a moved shadow, colour first or last.
			await Assert.That(Alpha(Red("blur(2px)"), 18, 50)).IsGreaterThan(0);
			await AssertColor(Red("drop-shadow(10px 10px #0000ff)"), 85, 85, 0, 0, 255, 255);
			await AssertColor(Red("drop-shadow(#0000ff 10px 10px)"), 85, 85, 0, 0, 255, 255);
			await AssertColor(Red("drop-shadow(10px 10px #0000ff)"), 50, 50, 255, 0, 0, 255);

			// A negative amount makes the whole list invalid, which usvg skips: drawn unfiltered.
			await AssertColor(Red("grayscale(-1) invert(1)"), 50, 50, 255, 0, 0, 255);

			// Only a zero angle may drop its unit (svgtypes); hue-rotate(45) is invalid, so unfiltered too.
			await AssertColor(Red("hue-rotate(45)"), 50, 50, 255, 0, 0, 255);
			await AssertColor(Red("hue-rotate(0)"), 50, 50, 255, 0, 0, 255);
		}

		[Test]
		public async Task PrimitiveAttributesReadAsUsvgReadsThem()
		{
			const string Flat = "<rect width=\"100\" height=\"100\" fill=\"none\" filter=\"url(#f)\"/>";
			const string Red = "<rect width=\"100\" height=\"100\" fill=\"#ff0000\" filter=\"url(#f)\"/>";

			// flood-color="inherit" takes the filter element's flood-color.
			await AssertColor(Filtered("<feFlood flood-color=\"inherit\"/>", Red, Whole + " flood-color=\"#00ff00\""), 50, 50, 0, 255, 0, 255);

			// lighting-color="currentColor" is the nearest color property, black when there is none.
			const string Overhead = "<feDiffuseLighting lighting-color=\"currentColor\"><feDistantLight elevation=\"90\"/></feDiffuseLighting>";
			await AssertColor(Filtered(Overhead, Flat, Whole + " color=\"#0000ff\""), 50, 50, 0, 0, 255, 255);
			await AssertColor(Filtered(Overhead, Flat), 50, 50, 0, 0, 0, 255);

			// tableValues is a number list; "1px" does not parse, so the table is empty: the identity.
			await AssertColor(Filtered("<feComponentTransfer><feFuncR type=\"table\" tableValues=\"1px\"/></feComponentTransfer>",
				"<rect width=\"100\" height=\"100\" fill=\"#800000\" filter=\"url(#f)\"/>"), 50, 50, 128, 0, 0, 255);

			// dx and dy are numbers; a percentage does not parse, so the offset is 0.
			ImageBuffer offset = Filtered("<feOffset dx=\"20%\" dy=\"40%\"/>", "<rect x=\"20\" y=\"20\" width=\"60\" height=\"60\" fill=\"#ff0000\" filter=\"url(#f)\"/>");
			await AssertColor(offset, 20, 20, 255, 0, 0, 255);
			await Assert.That(Alpha(offset, 80, 80)).IsEqualTo(0);
		}

		[Test]
		public async Task FeImageDrawsAnElementOrAnImage()
		{
			// An element reference draws it where it is, in the filtered element's user space, in place of the graphic.
			ImageBuffer element = Filtered("<feImage href=\"#r\"/>", "<defs><rect id=\"r\" x=\"60\" y=\"10\" width=\"20\" height=\"20\" fill=\"#0000ff\"/></defs>" + LeftHalf);
			await AssertColor(element, 70, 20, 0, 0, 255, 255);
			await Assert.That(Alpha(element, 25, 50)).IsEqualTo(0);

			// An element that reaches itself through feImage draws nothing there rather than recursing.
			ImageBuffer self = Filtered("<feImage href=\"#s\"/>", "<rect id=\"s\" width=\"50\" height=\"100\" fill=\"#00ff00\" filter=\"url(#f)\"/>");
			await Assert.That(Alpha(self, 25, 50)).IsEqualTo(0);

			// An image is fitted into the subregion: a 2x2 red, blue / green, white stretched over 0..100.
			SvgDocument document = SvgDocument.Parse("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">"
				+ $"<filter id=\"f\" {Whole}><feImage href=\"data:image/png;base64,AAAA\" preserveAspectRatio=\"none\"/></filter>{LeftHalf}</svg>");
			document.ImageDecoder = _ =>
			{
				var quad = new ImageBuffer(2, 2);
				quad.SetPixel(0, 1, Color.Red);
				quad.SetPixel(1, 1, Color.Blue);
				quad.SetPixel(0, 0, new Color(0, 255, 0));
				quad.SetPixel(1, 0, Color.White);
				return quad;
			};
			ImageBuffer image = SvgRenderer.RenderToImage(document, 100, 100);

			// Bicubic smoothing bleeds a little of the neighbours into each quadrant's middle.
			await AssertColor(image, 20, 20, 255, 0, 0, 255, 12);
			await AssertColor(image, 80, 20, 0, 0, 255, 255, 12);
			await AssertColor(image, 80, 80, 255, 255, 255, 255, 12);
		}

		[Test]
		public async Task FeImageSmoothsAnImageBicubically()
		{
			// resvg draws feImage's raster through tiny-skia's bicubic (Mitchell, B = C = 1/3) filter, as its
			// filters/feImage references show. A 3x1 black, white, black stretched over 100: at x = 50 the white
			// pixel's centre is .015 away, so bicubic keeps .89 of it (226) where bilinear would keep .985 (251).
			SvgDocument document = SvgDocument.Parse("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">"
				+ $"<filter id=\"f\" {Whole}><feImage href=\"data:image/png;base64,AAAA\" preserveAspectRatio=\"none\"/></filter>{LeftHalf}</svg>");
			document.ImageDecoder = _ =>
			{
				var strip = new ImageBuffer(3, 1);
				strip.SetPixel(0, 0, Color.Black);
				strip.SetPixel(1, 0, Color.White);
				strip.SetPixel(2, 0, Color.Black);
				return strip;
			};
			ImageBuffer image = SvgRenderer.RenderToImage(document, 100, 100);
			await AssertColor(image, 50, 50, 226, 226, 226, 255, 3);
		}

		[Test]
		public async Task FeImageDrawsItsElementFromTheSubregionsCorner()
		{
			// usvg/resvg: the element's user-space origin is the subregion's top-left - here the filter region, 40,40.
			const string Target = "<defs><rect id=\"r\" x=\"10\" y=\"10\" width=\"20\" height=\"20\" fill=\"#0000ff\"/></defs>"
				+ "<rect x=\"40\" y=\"40\" width=\"50\" height=\"50\" fill=\"red\" filter=\"url(#f)\"/>";
			ImageBuffer region = Filtered("<feImage href=\"#r\"/>", Target, "x=\"0\" y=\"0\" width=\"1\" height=\"1\"");
			await AssertColor(region, 60, 60, 0, 0, 255, 255);
			await Assert.That(Alpha(region, 45, 45)).IsEqualTo(0);

			// x/y on the primitive move it; the subregion's corner counts even where it sticks out of the filter region.
			ImageBuffer moved = Filtered("<feImage href=\"#r\" x=\"50\" y=\"30\"/>", Target, "x=\"0\" y=\"0\" width=\"1\" height=\"1\"");
			await AssertColor(moved, 75, 45, 0, 0, 255, 255);
			await Assert.That(Alpha(moved, 75, 65)).IsEqualTo(0);

			// objectBoundingBox primitiveUnits: a missing y (and width, height) is of the box, not the filter region.
			ImageBuffer box = Filtered("<feImage href=\"#r\" x=\"0.5\"/>", Target, "primitiveUnits=\"objectBoundingBox\"");
			await AssertColor(box, 80, 68, 0, 0, 255, 255);

			// Under a rotation the element's whole transform applies, from the subregion's corner in its user space:
			// rotate(90 50 50) takes the 40..50 square that corner puts r's 0..10 at to x 50..60, y 40..50.
			ImageBuffer rotated = Filtered("<feImage href=\"#s\"/>", "<defs><rect id=\"s\" width=\"10\" height=\"10\" fill=\"#0000ff\"/></defs>"
				+ "<rect x=\"40\" y=\"40\" width=\"20\" height=\"20\" fill=\"red\" filter=\"url(#f)\" transform=\"rotate(90 50 50)\"/>", "x=\"0\" y=\"0\" width=\"1\" height=\"1\"");
			await AssertColor(rotated, 55, 45, 0, 0, 255, 255);
		}

		[Test]
		public async Task DisplacementMapMovesBySelectedChannels()
		{
			// R = 1 moves x by +.5 * 20 = 10; B = 0 moves y by -10 (samples above).
			ImageBuffer image = Filtered("<feFlood flood-color=\"#ff0000\" result=\"map\"/>"
				+ "<feDisplacementMap in=\"SourceGraphic\" in2=\"map\" scale=\"20\" xChannelSelector=\"R\" yChannelSelector=\"B\"/>", LeftHalf);
			await AssertColor(image, 39, 50, 0, 255, 0, 255);
			await Assert.That(Alpha(image, 40, 50)).IsEqualTo(0);
			await Assert.That(Alpha(image, 20, 5)).IsEqualTo(0);
			await AssertColor(image, 20, 10, 0, 255, 0, 255);
		}
	}
}
