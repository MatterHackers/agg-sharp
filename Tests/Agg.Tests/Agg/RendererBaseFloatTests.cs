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
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// RendererBaseFloat's scanline renderers, SpanGradientFloat with GradientLinearColorFloat, and
	/// PixelFormatCompOpBGRAFloat, pinned to C++ AGG over rgba32 (AGG_BGRA128) by rendering compositing.cpp's two
	/// shapes - the gradient circle with its shadow through blender_rgba, then the gradient rounded rect through
	/// pixfmt_custom_blend_rgba&lt;comp_op_adaptor_rgba&gt; - into a 600x400 float buffer. The expected values are
	/// FNV-1a hashes of the whole buffer's float bytes from a C++ trace built against the reference renderer's
	/// patched headers with -ffp-contract=off. The last two cases move the shapes over the image edges, so the
	/// renderer_base clipping runs.
	/// </summary>
	public class RendererBaseFloatTests
	{
		private const int Width = 600;
		private const int Height = 400;

		[Test]
		[Arguments(3, 0.75, 1.0, 0.0, 0.0, 0xCE45AAB6230B9F62UL)]
		[Arguments(19, 0.75, 1.0, 0.0, 0.0, 0xB7A01D17F8E91096UL)]
		[Arguments(13, 0.5, 0.5, 0.0, 0.0, 0xD67A8428D8DC9A06UL)]
		[Arguments(0, 0.75, 0.6, 0.0, 0.0, 0xB549935540085C52UL)]
		[Arguments(21, 0.8, 0.9, -200.0, -150.0, 0x756C7970997D3D79UL)]
		[Arguments(12, 0.4, 0.7, 330.0, 180.0, 0x4F6BE97A7497567CUL)]
		public async Task CompositingShapesMatchCpp(int op, double srcAlpha, double dstAlpha, double ox, double oy, ulong expected)
		{
			var image = new ImageBufferFloat(Width, Height, 128, new BlenderBGRAFloat());
			var prim = new PixelFormatBGRAFloat(image, new BlenderRgbaFloat());
			var comp = new PixelFormatCompOpBGRAFloat(image, (CompOp)op);
			prim.Clear(new ColorF(0, 0, 0, 0));

			int da = (int)(uint)(dstAlpha * 255);
			Circle(
				new RendererBaseFloat(prim),
				SrgbLut.Rgba32FromSrgba8(0xFD, 0xF0, 0x6F, da),
				SrgbLut.Rgba32FromSrgba8(0xFE, 0x9F, 0x34, da),
				(70 * 3) + ox,
				100 + (24 * 3) + oy,
				(37 * 3) + ox,
				100 + (79 * 3) + oy,
				dstAlpha);

			int sa = (int)(uint)(srcAlpha * 255);
			SrcShape(
				new RendererBaseFloat(comp),
				SrgbLut.Rgba32FromSrgba8(0x7F, 0xC1, 0xFF, sa),
				SrgbLut.Rgba32FromSrgba8(0x05, 0x00, 0x5F, sa),
				300 + 50 + ox,
				100 + (24 * 3) + oy,
				107 + 50 + ox,
				100 + (79 * 3) + oy);

			await Assert.That(Fnv1a(image.GetBuffer(), Width * Height * 4)).IsEqualTo(expected);
		}

		// compositing.cpp's gradient_affine: gradient_x's 0..d2 run laid along x1,y1 -> x2,y2.
		private static Affine GradientAffine(double x1, double y1, double x2, double y2, double d2)
		{
			double dx = x2 - x1;
			double dy = y2 - y1;
			Affine mtx = Affine.NewIdentity();
			mtx *= Affine.NewScaling(Math.Sqrt((dx * dx) + (dy * dy)) / d2);
			mtx *= Affine.NewRotation(Math.Atan2(dy, dx));
			mtx *= Affine.NewTranslation(x1, y1);
			mtx.invert();
			return mtx;
		}

		private static SpanGradientFloat Gradient(ColorF c1, ColorF c2, double x1, double y1, double x2, double y2)
		{
			return new SpanGradientFloat(new span_interpolator_linear(GradientAffine(x1, y1, x2, y2, 100)), new gradient_x(), new GradientLinearColorFloat(c1, c2), 0, 100);
		}

		private static void Circle(RendererBaseFloat ren, ColorF c1, ColorF c2, double x1, double y1, double x2, double y2, double shadowAlpha)
		{
			var ras = new ScanlineRasterizer();
			var sl = new scanline_unpacked_8();
			double r = agg_math.CalcDistance(x1, y1, x2, y2) / 2;
			ras.add_path(new Ellipse(((x1 + x2) / 2) + 5, ((y1 + y2) / 2) - 3, r, r, 100));
			ren.RenderScanlinesAaSolid(ras, sl, new ColorF((float)0.6, (float)0.6, (float)0.6, (float)(0.7 * shadowAlpha)));

			ras.reset();
			ras.add_path(new Ellipse((x1 + x2) / 2, (y1 + y2) / 2, r, r, 100));
			ren.RenderScanlinesAa(ras, sl, Gradient(c1, c2, x1, y1, x2, y2));
		}

		private static void SrcShape(RendererBaseFloat ren, ColorF c1, ColorF c2, double x1, double y1, double x2, double y2)
		{
			var ras = new ScanlineRasterizer();
			ras.add_path(new RoundedRect(x1, y1, x2, y2, 40));
			ren.RenderScanlinesAa(ras, new scanline_unpacked_8(), Gradient(c1, c2, x1, y1, x2, y2));
		}

		private static ulong Fnv1a(float[] buffer, int count)
		{
			ulong h = 1469598103934665603UL;
			for (int i = 0; i < count; i++)
			{
				uint bits = BitConverter.SingleToUInt32Bits(buffer[i]);
				for (int b = 0; b < 4; b++)
				{
					h ^= (bits >> (8 * b)) & 0xFF;
					h *= 1099511628211UL;
				}
			}

			return h;
		}
	}
}
