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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	/// <summary>
	/// renderer_primitives' shapes, traced by hand through agg_renderer_primitives.h. The pixel-level check
	/// against C++ itself is the graph_test draft-mode golden (GraphTestDemoTests).
	/// </summary>
	public class RendererPrimitivesTests
	{
		private static readonly Color Line = new Color(255, 0, 0);
		private static readonly Color Fill = new Color(0, 0, 255);

		[Test]
		public async Task OutlinedRectangleDrawsItsBorderInTheLineColorAndItsInsideInTheFillColor()
		{
			var image = new ImageBuffer(6, 6);
			var primitives = new RendererPrimitives(new ImageClippingProxy(image)) { LineColor = Line, FillColor = Fill };

			primitives.OutlinedRectangle(1, 1, 4, 4);

			await Assert.That(image.GetPixel(1, 1)).IsEqualTo(Line);
			await Assert.That(image.GetPixel(4, 1)).IsEqualTo(Line);
			await Assert.That(image.GetPixel(4, 4)).IsEqualTo(Line);
			await Assert.That(image.GetPixel(1, 4)).IsEqualTo(Line);
			await Assert.That(image.GetPixel(2, 2)).IsEqualTo(Fill);
			await Assert.That(image.GetPixel(3, 3)).IsEqualTo(Fill);
			await Assert.That(image.GetPixel(0, 0).alpha).IsEqualTo((byte)0);
			await Assert.That(image.GetPixel(5, 5).alpha).IsEqualTo((byte)0);
		}

		/// <summary>A bar reaching past the image is cut to it, as renderer_base::blend_bar clips.</summary>
		[Test]
		public async Task SolidRectangleIsClippedToTheImage()
		{
			var image = new ImageBuffer(4, 4);
			var primitives = new RendererPrimitives(image) { FillColor = Fill };

			primitives.SolidRectangle(2, 2, -3, 9);

			await Assert.That(image.GetPixel(0, 3)).IsEqualTo(Fill);
			await Assert.That(image.GetPixel(2, 2)).IsEqualTo(Fill);
			await Assert.That(image.GetPixel(3, 2).alpha).IsEqualTo((byte)0);
			await Assert.That(image.GetPixel(0, 1).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task SolidEllipseFillsItsWholeRadius()
		{
			var image = new ImageBuffer(11, 11);
			var primitives = new RendererPrimitives(image) { FillColor = Fill };

			primitives.SolidEllipse(5, 5, 3, 3);

			await Assert.That(image.GetPixel(2, 5)).IsEqualTo(Fill);
			await Assert.That(image.GetPixel(8, 5)).IsEqualTo(Fill);
			await Assert.That(image.GetPixel(5, 2)).IsEqualTo(Fill);
			await Assert.That(image.GetPixel(5, 8)).IsEqualTo(Fill);
			await Assert.That(image.GetPixel(1, 5).alpha).IsEqualTo((byte)0);
			await Assert.That(image.GetPixel(5, 9).alpha).IsEqualTo((byte)0);
			await Assert.That(image.GetPixel(2, 2).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task OutlinedEllipseRingsItsFillInTheLineColor()
		{
			var image = new ImageBuffer(11, 11);
			var primitives = new RendererPrimitives(image) { LineColor = Line, FillColor = Fill };

			primitives.OutlinedEllipse(5, 5, 3, 3);

			await Assert.That(image.GetPixel(2, 5)).IsEqualTo(Line);
			await Assert.That(image.GetPixel(8, 5)).IsEqualTo(Line);
			await Assert.That(image.GetPixel(5, 2)).IsEqualTo(Line);
			await Assert.That(image.GetPixel(5, 8)).IsEqualTo(Line);
			await Assert.That(image.GetPixel(5, 5)).IsEqualTo(Fill);
			await Assert.That(image.GetPixel(1, 5).alpha).IsEqualTo((byte)0);
		}
	}
}
