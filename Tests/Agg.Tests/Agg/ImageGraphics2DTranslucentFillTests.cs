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
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Graphics2D colours are straight alpha, whatever the destination stores. A translucent fill through
	/// ImageGraphics2D onto a premultiplied buffer (every widget backbuffer) must land where the same fill lands on a
	/// straight-alpha buffer and where the GPU path puts it: (51 grey at alpha 150) over white is
	/// 255 * (1 - 150/255) + 51 * 150/255 = 135. Handing the straight colour to the premultiplied blender added the
	/// whole 51 on top (156), and a faint fill (alpha 24, which belongs at 236) clamped to white.
	/// </summary>
	public class ImageGraphics2DTranslucentFillTests
	{
		[Test]
		[Arguments(150, 135)]
		[Arguments(24, 236)]
		public async Task TranslucentFillBlendsAsStraightAlphaOnEveryDestination(int alpha, int expected)
		{
			foreach (IRecieveBlenderByte blender in new IRecieveBlenderByte[] { new BlenderBGRA(), new BlenderPreMultBGRA() })
			{
				var image = new ImageBuffer(8, 8, 32, blender);
				Graphics2D graphics = image.NewGraphics2D();
				graphics.Clear(Color.White);
				graphics.FillRectangle(0, 0, 8, 8, new Color(51, 51, 51, alpha));

				Color pixel = image.GetPixel(4, 4);
				await Assert.That(Math.Abs(pixel.red - expected)).IsLessThanOrEqualTo(1).Because(blender.GetType().Name);
				await Assert.That(pixel.alpha).IsEqualTo((byte)255).Because(blender.GetType().Name);
			}
		}

		/// <summary>
		/// A widget backbuffer is labelled premultiplied but holds straight colour, because its composite reads it
		/// straight. A translucent fill onto a transparent part of it must store (c, a), so the composited result
		/// matches drawing the fill straight onto the surface - the dark theme's half-alpha placeholder text, which
		/// premultiplying darkened from 143 to 86.
		/// </summary>
		[Test]
		public async Task TranslucentFillOnTransparentBackbufferCompositesStraight()
		{
			var fill = new Color(230, 230, 235, 128);
			var surfaceColor = new Color(56, 56, 66, 255);

			var parent = new GuiWidget(16, 16);
			var buffered = new FillingWidget(fill) { DoubleBuffer = true };
			parent.AddChild(buffered);
			parent.PerformLayout();

			var composited = new ImageBuffer(16, 16);
			Graphics2D compositedGraphics = composited.NewGraphics2D();
			compositedGraphics.Clear(surfaceColor);
			parent.OnDraw(compositedGraphics);

			var stored = buffered.BackBuffer.GetPixel(8, 8);
			await Assert.That(stored).IsEqualTo(fill);

			var direct = new ImageBuffer(16, 16);
			Graphics2D directGraphics = direct.NewGraphics2D();
			directGraphics.Clear(surfaceColor);
			directGraphics.FillRectangle(0, 0, 16, 16, fill);

			Color expected = direct.GetPixel(8, 8);
			Color actual = composited.GetPixel(8, 8);
			await Assert.That(Math.Abs(actual.red - expected.red)).IsLessThanOrEqualTo(2);
			await Assert.That(Math.Abs(actual.green - expected.green)).IsLessThanOrEqualTo(2);
			await Assert.That(Math.Abs(actual.blue - expected.blue)).IsLessThanOrEqualTo(2);
		}

		/// <summary>
		/// The anti-aliased edge of a fill (here half a pixel of cover) over a transparent part of a backbuffer must
		/// composite like the same edge drawn straight: the premultiplied blender stored the edge as (c * cover, cover),
		/// which the straight-reading composite darkened by the cover a second time.
		/// </summary>
		[Test]
		[Arguments(255)]
		[Arguments(128)]
		public async Task PartlyCoveredEdgeOnTransparentBackbufferCompositesStraight(int alpha)
		{
			var fill = new Color(230, 230, 235, alpha);
			var surfaceColor = new Color(56, 56, 66, 255);
			var edge = new RectangleDouble(0, 0, 8.5, 16);

			var parent = new GuiWidget(16, 16);
			parent.AddChild(new FillingWidget(fill, edge) { DoubleBuffer = true });
			parent.PerformLayout();

			var composited = new ImageBuffer(16, 16);
			Graphics2D compositedGraphics = composited.NewGraphics2D();
			compositedGraphics.Clear(surfaceColor);
			parent.OnDraw(compositedGraphics);

			var direct = new ImageBuffer(16, 16);
			Graphics2D directGraphics = direct.NewGraphics2D();
			directGraphics.Clear(surfaceColor);
			directGraphics.FillRectangle(edge, fill);

			Color expected = direct.GetPixel(8, 8);
			Color actual = composited.GetPixel(8, 8);
			await Assert.That(expected.red).IsGreaterThan(surfaceColor.red);
			await Assert.That(Math.Abs(actual.red - expected.red)).IsLessThanOrEqualTo(2);
			await Assert.That(Math.Abs(actual.green - expected.green)).IsLessThanOrEqualTo(2);
			await Assert.That(Math.Abs(actual.blue - expected.blue)).IsLessThanOrEqualTo(2);
		}

		/// <summary>The straight fill onto a premultiplied buffer still honours the graphics' clipping rect.</summary>
		[Test]
		public async Task StraightFillOnPremultipliedBufferStaysInsideTheClip()
		{
			var image = new ImageBuffer(16, 16, 32, new BlenderPreMultBGRA());
			Graphics2D graphics = image.NewGraphics2D();
			graphics.Clear(Color.White);
			graphics.SetClippingRect(new RectangleDouble(4, 4, 12, 12));
			graphics.FillRectangle(0, 0, 16, 16, new Color(51, 51, 51, 150));

			await Assert.That((int)image.GetPixel(8, 8).red).IsEqualTo(135);
			await Assert.That(image.GetPixel(2, 8)).IsEqualTo(Color.White);
			await Assert.That(image.GetPixel(13, 8)).IsEqualTo(Color.White);
			await Assert.That(image.GetPixel(8, 2)).IsEqualTo(Color.White);
			await Assert.That(image.GetPixel(8, 13)).IsEqualTo(Color.White);
		}

		private class FillingWidget : GuiWidget
		{
			private readonly Color fill;

			private readonly RectangleDouble? area;

			public FillingWidget(Color fill, RectangleDouble? area = null)
				: base(16, 16)
			{
				this.fill = fill;
				this.area = area;
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				graphics2D.FillRectangle(area ?? LocalBounds, fill);
				base.OnDraw(graphics2D);
			}
		}
	}
}
