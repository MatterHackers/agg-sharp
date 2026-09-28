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
using MatterHackers.Agg.Tests.GoldenImages;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderGl;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.RenderGl
{
	// Graphics2DGpu caches a VertexSourceApplyTransform's inner shape and draws it at an offset. The offset
	// has to go through the graphics transform like the shape does, and only a pure translation may be
	// taken apart that way.
	[NotInParallel]
	public class Graphics2DGpuTranslatedSourceTests
	{
		private const int Size = 64;

		/// <summary>A 10-unit square moved to (10, 10), under the graphics transform <paramref name="graphicsTransform"/>,
		/// must cover the screen pixel (<paramref name="insideX"/>, <paramref name="insideY"/>) and not
		/// (<paramref name="outsideX"/>, <paramref name="outsideY"/>).</summary>
		[Test]
		[Arguments(1.0, 1.0, 0.0, 15, 15, 5, 5)]

		// Scale 2: the square covers 20..40.
		[Arguments(2.0, 2.0, 0.0, 30, 30, 12, 12)]

		// Flipped (y -> 64 - y), as a y-down demo is shown: the square covers x 10..20, y 44..54.
		[Arguments(1.0, -1.0, 64.0, 15, 49, 15, 15)]
		public async Task ATranslatedShapeLandsWhereTheTransformsPutIt(double scaleX, double scaleY, double offsetY, int insideX, int insideY, int outsideX, int outsideY)
		{
			var square = new VertexStorage();
			square.MoveTo(0, 0);
			square.LineTo(10, 0);
			square.LineTo(10, 10);
			square.LineTo(0, 10);
			square.ClosePolygon();

			ImageBuffer image = await Render(
				Affine.NewScaling(scaleX, scaleY) * Affine.NewTranslation(0, offsetY),
				new VertexSourceApplyTransform(square, Affine.NewTranslation(10, 10)));

			await Assert.That(image.GetPixel(insideX, insideY)).IsEqualTo(Color.Red);
			await Assert.That(image.GetPixel(outsideX, outsideY)).IsEqualTo(Color.White);
		}

		/// <summary>A unit-diagonal transform with shear is not a translation: its shear must not be dropped.</summary>
		[Test]
		public async Task AShearedShapeKeepsItsShear()
		{
			var square = new VertexStorage();
			square.MoveTo(0, 0);
			square.LineTo(10, 0);
			square.LineTo(10, 10);
			square.LineTo(0, 10);
			square.ClosePolygon();

			// x' = x + 2y + 10: the square's top row (y 9..10) spans x 28..40.
			var shear = new Affine(1, 0, 2, 1, 10, 10);
			ImageBuffer image = await Render(Affine.NewIdentity(), new VertexSourceApplyTransform(square, shear));

			await Assert.That(image.GetPixel(35, 19)).IsEqualTo(Color.Red);
			await Assert.That(image.GetPixel(12, 19)).IsEqualTo(Color.White);
		}

		private static async Task<ImageBuffer> Render(Affine graphicsTransform, IVertexSource source)
		{
			using var capture = WebGpuOffscreenCapture.Create(Size, Size);
			Graphics2DGpu graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			graphics.SetTransform(graphicsTransform);
			graphics.Render(source, Color.Red);
			graphics.PopTransform();
			return await capture.CaptureAsync();
		}
	}
}
