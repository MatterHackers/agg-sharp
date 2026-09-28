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
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// compositing.cpp's port (AGG_BGRA128): its reference render against C++ AGG (demo_compositing.cpp), the float
	// window written out through srgba8(rgba32) on both sides.
	public class CompositingDemoTests
	{
		/// <summary>The default frame - src-over, src alpha 0.75, dst alpha 1 - matches C++ AGG byte for byte.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new CompositingDemo()), "compositing_600x400");
		}

		/// <summary>xor at src alpha 0.5 (params 11 0.5 1).</summary>
		[Test]
		public async Task XorHalfAlphaMatchesCppAgg()
		{
			await AggReference.Check(Render(NewDemo(CompOp.Xor, 0.5, 1.0)), "compositing_600x400_xor_a050");
		}

		/// <summary>
		/// difference with dst alpha 0.6 (params 22 0.75 0.6): the picture blended at a partial cover and the circle
		/// and its shadow at partial alpha.
		/// </summary>
		[Test]
		public async Task DifferencePartialDstMatchesCppAgg()
		{
			await AggReference.Check(Render(NewDemo(CompOp.Difference, 0.75, 0.6)), "compositing_600x400_difference_dst060");
		}

		/// <summary>
		/// The GPU stand-in draws the software scene - the picture at its cover, the gradients through
		/// IGradientFillGraphics, the rounded rectangle through ICompOpGraphics - so at opaque alphas, where no colours
		/// are mixed, the Porter-Duff operators match the float window away from edges. A handful of pixels on the
		/// picture's and circle's rims, where the GPU's anti-aliasing reaches further than software's, are allowed.
		/// </summary>
		[Test]
		[NotInParallel]
		[Arguments(CompOp.Clear)]
		[Arguments(CompOp.Src)]
		[Arguments(CompOp.Dst)]
		[Arguments(CompOp.SrcOver)]
		[Arguments(CompOp.DstOver)]
		[Arguments(CompOp.SrcIn)]
		[Arguments(CompOp.DstIn)]
		[Arguments(CompOp.SrcOut)]
		[Arguments(CompOp.DstOut)]
		[Arguments(CompOp.SrcAtop)]
		[Arguments(CompOp.DstAtop)]
		[Arguments(CompOp.Xor)]
		public async Task GpuPorterDuffAtOpaqueAlphasMatchesTheFloatWindow(CompOp op)
		{
			var (compared, bad, _) = await CompareGpuWithFloatWindow(NewDemo(op, 1.0, 1.0), 3);

			await Assert.That(compared).IsGreaterThan(80000);
			await Assert.That(bad).IsLessThanOrEqualTo(150);
		}

		/// <summary>
		/// Where colours mix - translucent alphas, and the blend-mode operators from plus to exclusion - the GPU
		/// stand-in composites in a linear-light float layer as C++ does in its rgba32 window, so it matches the
		/// float window away from edges there too, within 3 levels. Before the linear layer the worst were 68 levels at
		/// dst alpha 0.6 and 202 for color-burn.
		/// </summary>
		/// <remarks>
		/// color-burn divides by the source colour, and the rounded rectangle's dark end is nearly black in linear
		/// light (0x05 is 0.0015), so it holds the gradient to its float colours: read as sRGB bytes it was 19 levels
		/// off at opaque alphas across the rectangle.
		/// </remarks>
		[Test]
		[NotInParallel]
		[MethodDataSource(nameof(MixingCases))]
		public async Task GpuMixedColoursMatchTheFloatWindow(CompOp op, double srcAlpha, double dstAlpha)
		{
			var (compared, bad, _) = await CompareGpuWithFloatWindow(NewDemo(op, srcAlpha, dstAlpha), 3);

			await Assert.That(compared).IsGreaterThan(75000);
			await Assert.That(bad).IsLessThanOrEqualTo(150);
		}

		/// <summary>Every operator at translucent alphas, and the blend-mode operators at opaque ones.</summary>
		public static System.Collections.Generic.IEnumerable<Func<(CompOp, double, double)>> MixingCases()
		{
			for (var op = CompOp.Clear; op <= CompOp.Exclusion; op++)
			{
				var captured = op;
				yield return () => (captured, 0.75, 0.6);
				if (op >= CompOp.Plus)
				{
					yield return () => (captured, 1.0, 1.0);
				}
			}
		}

		/// <summary>
		/// Renders <paramref name="demo"/> in software and on the GPU and compares them above the sliders and left of
		/// the operator rbox, at pixels software shows one colour around (so edges, where the two rasterizers' anti-
		/// aliasing differs, are left out). Answers how many were compared, how many differ by more than
		/// <paramref name="tolerance"/> in some channel, and the largest difference among those compared.
		/// </summary>
		private static async Task<(int Compared, int Bad, int Worst)> CompareGpuWithFloatWindow(CompositingDemo demo, int tolerance)
		{
			ImageBuffer software = Render(demo);

			using var capture = WebGpuOffscreenCapture.Create(demo.Width, demo.Height);
			demo.Draw(capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1)));
			ImageBuffer gpu = await capture.CaptureAsync();

			int compared = 0;
			int bad = 0;
			int worst = 0;
			for (int y = 35; y < demo.Height - 1; y++)
			{
				for (int x = 1; x < 410; x++)
				{
					Color center = software.GetPixel(x, y);
					bool flat = true;
					for (int dy = -1; dy <= 1 && flat; dy++)
					{
						for (int dx = -1; dx <= 1 && flat; dx++)
						{
							flat = Difference(software.GetPixel(x + dx, y + dy), center) <= 6;
						}
					}

					if (flat)
					{
						compared++;
						int difference = Difference(gpu.GetPixel(x, y), center);
						worst = Math.Max(worst, difference);
						if (difference > tolerance)
						{							bad++;
						}
					}
				}
			}

			return (compared, bad, worst);
		}

		private static int Difference(Color a, Color b)
		{
			return Math.Max(Math.Max(Math.Abs(a.red - b.red), Math.Abs(a.green - b.green)), Math.Abs(a.blue - b.blue));
		}

		private static CompositingDemo NewDemo(CompOp op, double srcAlpha, double dstAlpha)
		{
			var demo = new CompositingDemo();
			demo.OperatorRbox.CurrentItem = (int)op;
			demo.SrcAlphaSlider.Value = srcAlpha;
			demo.DstAlphaSlider.Value = dstAlpha;
			return demo;
		}

		private static ImageBuffer Render(CompositingDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
