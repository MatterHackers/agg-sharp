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
using MatterHackers.Agg.Tests.GoldenImages;
using MatterHackers.Agg.VertexSource;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// compositing2.cpp's port: its reference render against C++ AGG (demo_compositing2.cpp).
	public class Compositing2DemoTests
	{
		/// <summary>The default frame - src-over, both alphas 1 - matches C++ AGG byte for byte.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new Compositing2Demo()), "compositing2_600x400");
		}

		/// <summary>
		/// Both alphas at 0.5 (params 3 0.5 0.5): the ramps must be C++'s double-precision rgba::gradient, not
		/// rgba8::gradient, to match here.
		/// </summary>
		[Test]
		public async Task HalfAlphaMatchesCppAgg()
		{
			await AggReference.Check(Render(NewDemo(CompOp.SrcOver, 0.5, 0.5)), "compositing2_600x400_alpha050");
		}

		/// <summary>
		/// src-atop at alpha 0.5 (params 9 0.5 0.5), against the patched C++ that blends blue with the destination's
		/// blue: unpatched C++ AGG (and agg-rust's golden) differ from this in blue only, in 43006 bytes.
		/// </summary>
		[Test]
		public async Task SrcAtopHalfAlphaMatchesPatchedCppAgg()
		{
			await AggReference.Check(Render(NewDemo(CompOp.SrcAtop, 0.5, 0.5)), "compositing2_600x400_srcatop_a050");
		}

		/// <summary>color-burn with src alpha 0.75 (params 19 0.75 1), against the patched C++ color burn.</summary>
		[Test]
		public async Task ColorBurnMatchesPatchedCppAgg()
		{
			await AggReference.Check(Render(NewDemo(CompOp.ColorBurn, 0.75, 1.0)), "compositing2_600x400_colorburn_a075");
		}

		private static Compositing2Demo NewDemo(CompOp op, double srcAlpha, double dstAlpha)
		{
			var demo = new Compositing2Demo();
			demo.OperatorRbox.CurrentItem = (int)op;
			demo.SrcAlphaSlider.Value = srcAlpha;
			demo.DstAlphaSlider.Value = dstAlpha;
			return demo;
		}

		/// <summary>
		/// The GPU path's gradient circle is one fill: through xor onto an opaque rectangle, an opaque gradient leaves
		/// every pixel inside the circle transparent, where rings would leave their anti-aliased seams showing; and
		/// through src-in (which lerps by coverage) onto the same rectangle it leaves the circle opaque.
		/// </summary>
		[Test]
		[Arguments(CompOp.Xor, 0)]
		[Arguments(CompOp.SrcIn, 255)]
		public async Task GpuGradientCircleHasNoSeams(CompOp op, int insideAlpha)
		{
			var ramp = new Color[256];
			for (int i = 0; i < ramp.Length; i++)
			{
				ramp[i] = new Color(i, 255 - i, 128, 255);
			}

			using var capture = WebGpuOffscreenCapture.Create(120, 120);
			var graphics = capture.BeginWidgetFrame(new ColorF(0, 0, 0, 0));
			graphics.Render(new RoundedRect(0, 0, 120, 120, 0), Color.Black);
			var gradient = Compositing2Demo.GradientImage(ramp, 50);
			((ICompOpGraphics)graphics).DrawWithCompOp(op, () => Compositing2Demo.FillGradientCircle(graphics, gradient, 10, 10, 110, 110));
			var image = await capture.CaptureAsync();

			byte[] buffer = image.GetBuffer();
			for (int y = 12; y < 108; y++)
			{
				for (int x = 12; x < 108; x++)
				{
					double dx = x + 0.5 - 60;
					double dy = y + 0.5 - 60;
					if (Math.Sqrt((dx * dx) + (dy * dy)) < 48.5)
					{
						int alpha = buffer[image.GetBufferOffsetXY(x, y) + ImageBuffer.OrderA];
						await Assert.That(Math.Abs(alpha - insideAlpha)).IsLessThanOrEqualTo(2).Because($"alpha at ({x}, {y}) is {alpha}");
					}
				}
			}
		}

		private static ImageBuffer Render(Compositing2Demo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
