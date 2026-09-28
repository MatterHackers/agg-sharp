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
	// agg/Svg, stage 2: linear and radial gradients. Renders are 100x100 of a 100x100 viewBox, so one user unit
	// is one pixel; a pixel's colour is sampled at its centre, so pixel x of a 0..100 ramp sits at x + 0.5.
	public class SvgGradientTests
	{
		private const string WhiteToBlack = "<stop offset=\"0\" stop-color=\"white\"/><stop offset=\"1\" stop-color=\"black\"/>";

		private static ImageBuffer Render(string body)
		{
			return SvgDocument.RenderToImage($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 100 100\">{body}</svg>", 100, 100);
		}

		private static Color At(ImageBuffer image, int x, int y) => image.GetPixel(x, image.Height - 1 - y);

		/// <summary>The grey level expected <paramref name="t"/> of the way along a white-to-black ramp.</summary>
		private static int Grey(double t) => (int)Math.Round(255 * (1 - t));

		[Test]
		public async Task LinearGradientRunsAcrossTheShapesBoundingBox()
		{
			ImageBuffer image = Render($"<linearGradient id=\"g\">{WhiteToBlack}</linearGradient><rect x=\"20\" y=\"0\" width=\"60\" height=\"100\" fill=\"url(#g)\"/>");
			foreach (int x in new[] { 20, 50, 79 })
			{
				await Assert.That((int)At(image, x, 50).red).IsEqualTo(Grey((x + .5 - 20) / 60)).Within(2);
			}

			await Assert.That(At(image, 10, 50).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task UserSpaceGradientTransformAndPadSpread()
		{
			// userSpaceOnUse from x 25 to 75, then rotated a quarter turn about (50, 50): the ramp runs top to bottom.
			ImageBuffer image = Render($"<linearGradient id=\"g\" gradientUnits=\"userSpaceOnUse\" x1=\"25\" x2=\"75\" gradientTransform=\"rotate(90 50 50)\">{WhiteToBlack}</linearGradient>"
				+ "<rect width=\"100\" height=\"100\" fill=\"url(#g)\"/>");
			await Assert.That((int)At(image, 10, 5).red).IsEqualTo(255);
			await Assert.That((int)At(image, 10, 50).red).IsEqualTo(Grey(.51)).Within(2);
			await Assert.That((int)At(image, 90, 95).red).IsEqualTo(0);
		}

		[Test]
		public async Task ReflectAndRepeatSpreadBeyondTheVector()
		{
			string Gradient(string spread) => $"<linearGradient id=\"g\" gradientUnits=\"userSpaceOnUse\" x2=\"50\" spreadMethod=\"{spread}\">{WhiteToBlack}</linearGradient><rect width=\"100\" height=\"100\" fill=\"url(#g)\"/>";

			// 10.5 is a fifth of the way along the vector; 60.5 is a fifth into its second period.
			ImageBuffer reflect = Render(Gradient("reflect"));
			await Assert.That((int)At(reflect, 60, 50).red).IsEqualTo(Grey(.79)).Within(2);
			ImageBuffer repeat = Render(Gradient("repeat"));
			await Assert.That((int)At(repeat, 60, 50).red).IsEqualTo(Grey(.21)).Within(2);
		}

		[Test]
		public async Task HrefInheritsStopsAndAttributes()
		{
			ImageBuffer image = Render($"<linearGradient id=\"base\" x1=\"1\" x2=\"0\">{WhiteToBlack}</linearGradient>"
				+ "<linearGradient id=\"g\" xlink:href=\"#base\" spreadMethod=\"pad\"/><rect width=\"100\" height=\"100\" fill=\"url(#g)\"/>");

			// Inherited x1=1 x2=0 runs the ramp right to left.
			await Assert.That((int)At(image, 2, 50).red).IsLessThan(10);
			await Assert.That((int)At(image, 97, 50).red).IsGreaterThan(245);
		}

		[Test]
		public async Task StopOpacityAndFillOpacityScaleAlpha()
		{
			ImageBuffer image = Render("<linearGradient id=\"g\"><stop offset=\"0\" stop-color=\"red\" stop-opacity=\".5\"/><stop offset=\"1\" stop-color=\"red\" stop-opacity=\".5\"/></linearGradient>"
				+ "<rect width=\"100\" height=\"100\" fill=\"url(#g)\" fill-opacity=\".5\"/>");
			Color pixel = At(image, 50, 50);
			await Assert.That((int)pixel.alpha).IsEqualTo(64).Within(1);
			await Assert.That((int)pixel.red).IsGreaterThan(250);
		}

		[Test]
		public async Task RadialGradientRunsFromItsCentreAndFocus()
		{
			ImageBuffer centred = Render($"<radialGradient id=\"g\">{WhiteToBlack}</radialGradient><rect width=\"100\" height=\"100\" fill=\"url(#g)\"/>");
			await Assert.That((int)At(centred, 50, 50).red).IsGreaterThan(245);
			await Assert.That((int)At(centred, 75, 50).red).IsEqualTo(Grey(.51)).Within(3);
			await Assert.That((int)At(centred, 2, 2).red).IsEqualTo(0);

			// With the focus at (25, 50) the white point moves there.
			ImageBuffer focused = Render($"<radialGradient id=\"g\" fx=\"25%\">{WhiteToBlack}</radialGradient><rect width=\"100\" height=\"100\" fill=\"url(#g)\"/>");
			await Assert.That((int)At(focused, 25, 50).red).IsGreaterThan(245);
			await Assert.That((int)At(focused, 50, 50).red).IsLessThan(200);
		}

		[Test]
		public async Task StrokesTakeGradientsToo()
		{
			ImageBuffer image = Render($"<linearGradient id=\"g\" gradientUnits=\"userSpaceOnUse\" x2=\"100\">{WhiteToBlack}</linearGradient>"
				+ "<line x1=\"0\" y1=\"50\" x2=\"100\" y2=\"50\" stroke=\"url(#g)\" stroke-width=\"10\"/>");
			await Assert.That((int)At(image, 80, 50).red).IsEqualTo(Grey(.805)).Within(2);
			await Assert.That((int)At(image, 80, 50).alpha).IsEqualTo(255);
		}
	}
}
