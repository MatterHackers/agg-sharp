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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Svg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// agg/Svg, stage 1: parsing of colours, lengths, transforms and styles, shape outlines, and what the renderer
	// draws for each feature. Renders are 100x100 of a 100x100 viewBox, so one user unit is one pixel.
	public class SvgModuleTests
	{
		private static ImageBuffer Render(string body, string rootAttributes = "viewBox=\"0 0 100 100\"")
		{
			return SvgDocument.RenderToImage($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" {rootAttributes}>{body}</svg>", 100, 100);
		}

		/// <summary>The pixel at SVG coordinates (x, y), y down from the top as SVG counts it.</summary>
		private static Color At(ImageBuffer image, int x, int y) => image.GetPixel(x, image.Height - 1 - y);

		[Test]
		public async Task ParsesEveryColourNotation()
		{
			(string Text, Color Expected)[] cases =
			{
				("green", new Color(0, 128, 0, 255)),
				("RebeccaPurple", new Color(0x66, 0x33, 0x99, 255)),
				("#f00", new Color(255, 0, 0, 255)),
				("#00ff0080", new Color(0, 255, 0, 128)),
				("#123456", new Color(0x12, 0x34, 0x56, 255)),
				("rgb(0, 128, 0)", new Color(0, 128, 0, 255)),
				("rgb(100%, 0%, 50%)", new Color(255, 0, 128, 255)),
				("rgba(10, 20, 30, 0.5)", new Color(10, 20, 30, 128)),
				("hsl(120, 100%, 25%)", new Color(0, 128, 0, 255)),
				("hsl(120, 100%, 25%, 0.5)", new Color(0, 128, 0, 128)),
			};
			foreach ((string text, Color expected) in cases)
			{
				await Assert.That(SvgColor.TryParse(text, out Color color)).IsTrue();
				await Assert.That(color).IsEqualTo(expected);
			}

			await Assert.That(SvgColor.TryParse("notacolor", out _)).IsFalse();
			await Assert.That(SvgColor.TryParse("#12", out _)).IsFalse();
		}

		[Test]
		public async Task ParsesLengthsWithUnitsAndPercentages()
		{
			await Assert.That(SvgLength.Parse("10", 0)).IsEqualTo(10);
			await Assert.That(SvgLength.Parse("10px", 0)).IsEqualTo(10);
			await Assert.That(SvgLength.Parse("1in", 0)).IsEqualTo(96);
			await Assert.That(SvgLength.Parse("25.4mm", 0)).IsEqualTo(96).Within(1e-9);
			await Assert.That(SvgLength.Parse("50%", 0, 200)).IsEqualTo(100);
			await Assert.That(SvgLength.Parse("1e1", 0)).IsEqualTo(10);
			await Assert.That(SvgLength.Parse("bogus", 7)).IsEqualTo(7);
			await Assert.That(SvgLength.ParseList("1,2 3-4.5.5").ToArray()).IsEquivalentTo(new[] { 1, 2, 3, -4.5, .5 });
		}

		private static Vector2 Apply(Affine transform, double x, double y)
		{
			var point = new Vector2(x, y);
			transform.transform(ref point);
			return new Vector2(Math.Round(point.X, 9), Math.Round(point.Y, 9));
		}

		[Test]
		public async Task TransformListsApplyRightmostFirst()
		{
			// translate(10) scale(2): scale first, so (1, 1) goes to (2, 2) and then (12, 2).
			await Assert.That(Apply(SvgTransform.Parse("translate(10) scale(2)"), 1, 1)).IsEqualTo(new Vector2(12, 2));

			// rotate about a centre leaves the centre where it is and turns clockwise on screen (y down).
			Affine rotate = SvgTransform.Parse("rotate(90 50 50)");
			await Assert.That(Apply(rotate, 50, 50)).IsEqualTo(new Vector2(50, 50));
			await Assert.That(Apply(rotate, 60, 50)).IsEqualTo(new Vector2(50, 60));

			await Assert.That(Apply(SvgTransform.Parse("skewX(45)"), 0, 10)).IsEqualTo(new Vector2(10, 10));
			await Assert.That(Apply(SvgTransform.Parse("matrix(1 0 0 1 5 6)"), 1, 1)).IsEqualTo(new Vector2(6, 7));

			// An invalid list is ignored, as the spec says.
			await Assert.That(SvgTransform.Parse("scale(1,2,3)").is_identity()).IsTrue();
		}

		[Test]
		public async Task ViewBoxMeetCentresAndNoneStretches()
		{
			var viewBox = new RectangleDouble(0, 0, 100, 50);
			await Assert.That(Apply(SvgViewport.ViewBoxTransform(viewBox, null, 100, 100), 0, 0)).IsEqualTo(new Vector2(0, 25));
			await Assert.That(Apply(SvgViewport.ViewBoxTransform(viewBox, "none", 100, 100), 100, 50)).IsEqualTo(new Vector2(100, 100));
			await Assert.That(Apply(SvgViewport.ViewBoxTransform(viewBox, "xMinYMax meet", 100, 100), 0, 0)).IsEqualTo(new Vector2(0, 50));
		}

		[Test]
		public async Task StyleAttributeOverridesAndPropertiesInherit()
		{
			SvgDocument document = SvgDocument.Parse(
				"<svg xmlns=\"http://www.w3.org/2000/svg\"><g fill=\"red\" opacity=\"0.5\" color=\"blue\" stroke-width=\"3\">"
				+ "<rect id=\"r\" fill=\"green\" style=\"fill: yellow; stroke: currentColor\"/></g></svg>");
			SvgElement group = document.Root.Children[0];
			SvgStyle groupStyle = SvgStyle.Compute(group, null, 100);
			SvgStyle rectStyle = SvgStyle.Compute(document.GetElementById("#r"), groupStyle, 100);
			await Assert.That(rectStyle.Fill.Color).IsEqualTo(new Color(255, 255, 0, 255));
			await Assert.That(rectStyle.Stroke.Color).IsEqualTo(new Color(0, 0, 255, 255));
			await Assert.That(rectStyle.StrokeWidth).IsEqualTo(3);
			await Assert.That(groupStyle.Opacity).IsEqualTo(.5);
			await Assert.That(rectStyle.Opacity).IsEqualTo(1);
		}

		[Test]
		public async Task RoundedRectClampsItsRadiiAndTakesAMissingOneFromTheOther()
		{
			VertexStorage rect = SvgShapes.Rect(0, 0, 20, 10, 50, -1);
			RectangleDouble bounds = rect.GetBounds();
			await Assert.That(bounds.Width).IsEqualTo(20).Within(1e-9);
			await Assert.That(bounds.Height).IsEqualTo(10).Within(1e-9);

			// rx 50 clamps to 10 and ry takes it, then clamps to 5: the path starts 10 in from the left.
			await Assert.That(rect.Vertices().First().X).IsEqualTo(10);
			await Assert.That(SvgShapes.Rect(0, 0, 0, 10, -1, -1)).IsNull();
			await Assert.That(SvgShapes.Ellipse(0, 0, 0, 5)).IsNull();
		}

		[Test]
		public async Task ASubpathAfterAClosepathStartsAtTheClosedSubpathsStart()
		{
			VertexStorage path = SvgShapes.Path("M 10 50 L 10 10 L 50 10 z L 60 60");
			VertexData[] vertices = path.Vertices().TakeWhile(v => !v.IsStop).ToArray();
			int close = Array.FindIndex(vertices, v => v.IsClose);
			await Assert.That(vertices[close + 1].IsMoveTo).IsTrue();
			await Assert.That(vertices[close + 1].Position).IsEqualTo(new Vector2(10, 50));
		}

		[Test]
		public async Task FillsInSvgCoordinatesTopDown()
		{
			ImageBuffer image = Render("<rect x=\"0\" y=\"0\" width=\"100\" height=\"20\" fill=\"#00f\"/>");
			await Assert.That(At(image, 50, 10)).IsEqualTo(new Color(0, 0, 255, 255));
			await Assert.That(At(image, 50, 80).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task FillOpacityComesOutAsStraightAlpha()
		{
			ImageBuffer image = Render("<rect width=\"100\" height=\"100\" fill=\"green\" fill-opacity=\"0.5\"/>");
			await Assert.That(At(image, 50, 50)).IsEqualTo(new Color(0, 128, 0, 128));
		}

		[Test]
		public async Task GroupOpacityCompositesTheGroupAsOneLayer()
		{
			// Two overlapping opaque rects in a half-opaque group: the overlap is as transparent as the rest.
			ImageBuffer image = Render("<g opacity=\"0.5\"><rect width=\"60\" height=\"60\" fill=\"red\"/><rect x=\"40\" y=\"40\" width=\"60\" height=\"60\" fill=\"blue\"/></g>");
			await Assert.That(At(image, 50, 50)).IsEqualTo(new Color(0, 0, 255, 128));
			await Assert.That(At(image, 10, 10)).IsEqualTo(new Color(255, 0, 0, 128));
		}

		[Test]
		public async Task EvenOddLeavesTheInnerSquareEmpty()
		{
			const string Squares = "M 0 0 H 100 V 100 H 0 Z M 25 25 H 75 V 75 H 25 Z";
			await Assert.That(At(Render($"<path d=\"{Squares}\" fill-rule=\"evenodd\"/>"), 50, 50).alpha).IsEqualTo((byte)0);
			await Assert.That(At(Render($"<path d=\"{Squares}\"/>"), 50, 50).alpha).IsEqualTo((byte)255);
		}

		[Test]
		public async Task StrokesWithWidthCapsAndDashes()
		{
			ImageBuffer butt = Render("<line x1=\"20\" y1=\"50\" x2=\"80\" y2=\"50\" stroke=\"black\" stroke-width=\"10\"/>");
			await Assert.That(At(butt, 50, 47).alpha).IsEqualTo((byte)255);
			await Assert.That(At(butt, 15, 50).alpha).IsEqualTo((byte)0);

			ImageBuffer square = Render("<line x1=\"20\" y1=\"50\" x2=\"80\" y2=\"50\" stroke=\"black\" stroke-width=\"10\" stroke-linecap=\"square\"/>");
			await Assert.That(At(square, 17, 50).alpha).IsEqualTo((byte)255);

			ImageBuffer dashed = Render("<line x1=\"0\" y1=\"50\" x2=\"100\" y2=\"50\" stroke=\"black\" stroke-width=\"10\" stroke-dasharray=\"20\"/>");
			await Assert.That(At(dashed, 10, 50).alpha).IsEqualTo((byte)255);
			await Assert.That(At(dashed, 30, 50).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task UseDrawsTheReferencedElementMovedAndDefsDrawNothing()
		{
			ImageBuffer image = Render(
				"<defs><rect id=\"box\" width=\"20\" height=\"20\" fill=\"green\"/></defs>"
				+ "<use xlink:href=\"#box\" x=\"50\" y=\"50\"/><rect width=\"10\" height=\"10\" display=\"none\"/>");
			await Assert.That(At(image, 60, 60)).IsEqualTo(new Color(0, 128, 0, 255));
			await Assert.That(At(image, 10, 10).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task ViewBoxScalesTheDrawing()
		{
			// A 10x10 viewBox drawn at 100x100: the 5x5 rect fills the top-left quarter.
			ImageBuffer image = Render("<rect width=\"5\" height=\"5\" fill=\"red\"/>", "viewBox=\"0 0 10 10\"");
			await Assert.That(At(image, 45, 45).alpha).IsEqualTo((byte)255);
			await Assert.That(At(image, 55, 55).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task AnUnsupportedPaintServerUsesItsFallbackColour()
		{
			ImageBuffer image = Render("<rect width=\"100\" height=\"100\" fill=\"url(#missing) blue\"/>");
			await Assert.That(At(image, 50, 50)).IsEqualTo(new Color(0, 0, 255, 255));
		}
	}
}
