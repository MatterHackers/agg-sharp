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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Images, gradients and pattern fills drawn through ImageGraphics2D onto a buffer labelled premultiplied (every
	/// widget backbuffer, which holds straight colour) blend straight-over, as solid fills do
	/// (<see cref="ImageGraphics2DTranslucentFillTests"/>). An icon pixel of grey 51 at alpha 150 over white is
	/// 255 * (1 - 150/255) + 51 * 150/255 = 135; the premultiplied blender added the whole 51 on top (156).
	/// </summary>
	public class ImageGraphics2DTranslucentImageTests
	{
		private static readonly Color IconPixel = new Color(51, 51, 51, 150);

		private static ImageBuffer TranslucentIcon()
		{
			var icon = new ImageBuffer(8, 8, 32, new BlenderBGRA());
			byte[] bytes = icon.GetBuffer();
			for (int y = 0; y < 8; y++)
			{
				for (int x = 0; x < 8; x++)
				{
					int offset = icon.GetBufferOffsetXY(x, y);
					bytes[offset + ImageBuffer.OrderR] = IconPixel.red;
					bytes[offset + ImageBuffer.OrderG] = IconPixel.green;
					bytes[offset + ImageBuffer.OrderB] = IconPixel.blue;
					bytes[offset + ImageBuffer.OrderA] = IconPixel.alpha;
				}
			}

			return icon;
		}

		private static ImageBuffer Opaque(IRecieveBlenderByte blender, Color color)
		{
			var image = new ImageBuffer(16, 16, 32, blender);
			image.NewGraphics2D().Clear(color);
			return image;
		}

		/// <summary>Whole-pixel (nearest), scaled and fractionally placed (resampled) draws over opaque white.</summary>
		[Test]
		[Arguments(4.0, 4.0, 1.0)]
		[Arguments(4.0, 4.0, 1.5)]
		[Arguments(4.25, 4.5, 1.0)]
		public async Task TranslucentImageOverOpaqueBackbufferBlendsStraight(double x, double y, double scale)
		{
			foreach (IRecieveBlenderByte blender in new IRecieveBlenderByte[] { new BlenderBGRA(), new BlenderPreMultBGRA() })
			{
				ImageBuffer image = Opaque(blender, Color.White);
				image.NewGraphics2D().Render(TranslucentIcon(), x, y, 0, scale, scale);

				// (7, 7) is inside the icon in every placement, away from its resampled edges.
				Color pixel = image.GetPixel(7, 7);
				await Assert.That(Math.Abs(pixel.red - 135)).IsLessThanOrEqualTo(1).Because($"{blender.GetType().Name} got {pixel.red}");
				await Assert.That(pixel.alpha).IsEqualTo((byte)255).Because(blender.GetType().Name);
			}
		}

		/// <summary>
		/// Onto a transparent backbuffer the icon must store its own straight (c, a), and the backbuffer composited onto
		/// a surface must match drawing the icon straight onto that surface - including the partly covered pixels a
		/// fractional or scaled placement leaves at the icon's edge, which the premultiplied blender darkened twice.
		/// </summary>
		[Test]
		[Arguments(4.0, 4.0, 1.0)]
		[Arguments(4.0, 4.0, 1.5)]
		[Arguments(4.25, 4.5, 1.0)]
		public async Task TranslucentImageOnTransparentBackbufferCompositesStraight(double x, double y, double scale)
		{
			var surfaceColor = new Color(56, 56, 66, 255);

			var backbuffer = new ImageBuffer(16, 16, 32, new BlenderPreMultBGRA());
			backbuffer.NewGraphics2D().Render(TranslucentIcon(), x, y, 0, scale, scale);

			Color stored = backbuffer.GetPixel(7, 7);
			await Assert.That(Math.Abs(stored.red - IconPixel.red)).IsLessThanOrEqualTo(1).Because($"stored {stored}");
			await Assert.That(Math.Abs(stored.alpha - IconPixel.alpha)).IsLessThanOrEqualTo(1).Because($"stored {stored}");

			ImageBuffer composited = Opaque(new BlenderBGRA(), surfaceColor);
			composited.NewGraphics2D().Render(backbuffer, 0, 0);

			ImageBuffer direct = Opaque(new BlenderBGRA(), surfaceColor);
			direct.NewGraphics2D().Render(TranslucentIcon(), x, y, 0, scale, scale);

			int worst = 0;
			for (int py = 0; py < 16; py++)
			{
				for (int px = 0; px < 16; px++)
				{
					Color a = composited.GetPixel(px, py);
					Color b = direct.GetPixel(px, py);
					worst = Math.Max(worst, Math.Max(Math.Abs(a.red - b.red), Math.Max(Math.Abs(a.green - b.green), Math.Abs(a.blue - b.blue))));
				}
			}

			await Assert.That(worst).IsLessThanOrEqualTo(2);
		}

		/// <summary>A gradient whose stops are translucent (51 grey at alpha 150) over opaque white is the straight 135.</summary>
		[Test]
		public async Task TranslucentGradientOverOpaqueBackbufferBlendsStraight()
		{
			foreach (IRecieveBlenderByte blender in new IRecieveBlenderByte[] { new BlenderBGRA(), new BlenderPreMultBGRA() })
			{
				ImageBuffer image = Opaque(blender, Color.White);
				var fill = new GradientFill { Shape = GradientShape.X, D2 = 16, Colors = new gradient_linear_color(IconPixel, IconPixel, 256) };
				((IGradientFillGraphics)image.NewGraphics2D()).FillPathWithGradient(new RoundedRect(0, 0, 16, 16, 0), fill);

				Color pixel = image.GetPixel(8, 8);
				await Assert.That(Math.Abs(pixel.red - 135)).IsLessThanOrEqualTo(1).Because($"{blender.GetType().Name} got {pixel.red}");
			}
		}

		/// <summary>A pattern fill with a translucent image over opaque white is the straight 135.</summary>
		[Test]
		public async Task TranslucentPatternOverOpaqueBackbufferBlendsStraight()
		{
			foreach (IRecieveBlenderByte blender in new IRecieveBlenderByte[] { new BlenderBGRA(), new BlenderPreMultBGRA() })
			{
				ImageBuffer image = Opaque(blender, Color.White);
				((IPatternFillGraphics)image.NewGraphics2D()).FillPathWithImage(new RoundedRect(0, 0, 16, 16, 0), TranslucentIcon(), Affine.NewIdentity(), ImageWrapMode.Repeat, ImageWrapMode.Repeat);

				Color pixel = image.GetPixel(8, 8);
				await Assert.That(Math.Abs(pixel.red - 135)).IsLessThanOrEqualTo(1).Because($"{blender.GetType().Name} got {pixel.red}");
			}
		}

		/// <summary>An image draw onto a backbuffer honours the destination's clipping rect.</summary>
		[Test]
		public async Task TranslucentImageHonoursClipping()
		{
			ImageBuffer image = Opaque(new BlenderPreMultBGRA(), Color.White);
			Graphics2D graphics = image.NewGraphics2D();
			graphics.SetClippingRect(new RectangleDouble(0, 0, 8, 16));
			graphics.Render(TranslucentIcon(), 4, 4);

			await Assert.That(Math.Abs(image.GetPixel(6, 6).red - 135)).IsLessThanOrEqualTo(1);
			await Assert.That(image.GetPixel(10, 6).red).IsEqualTo((byte)255);
		}
	}
}
