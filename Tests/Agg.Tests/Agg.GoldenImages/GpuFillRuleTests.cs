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
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// <see cref="IFillRuleGraphics"/> on <c>Graphics2DGpu</c> against a real device, compared with the software
	/// rasterizer filling the same path under the same rule.
	/// </summary>
	/// <remarks>
	/// Two circles wound the same way, overlapping: non-zero fills the overlap, even-odd leaves it empty. The GPU
	/// anti-aliases with a halo, not AGG's area coverage: a pixel whose centre is just inside an edge is opaque on
	/// the GPU and about half covered in software (see <c>HaloAaTesselator.HaloWidth</c>), so an edge pixel can be
	/// off by half the shade's range. The tolerance allows that, and is still well under the full range a wrong
	/// rule gets wrong over the whole overlap.
	/// </remarks>
	[NotInParallel]
	public class GpuFillRuleTests
	{
		private const int Width = 120;
		private const int Height = 80;
		private const int Tolerance = 128;

		private static readonly Color Shade = new Color(30, 60, 90);

		[Test]
		[Arguments(Util.filling_rule_e.fill_even_odd)]
		[Arguments(Util.filling_rule_e.fill_non_zero)]
		public async Task OverlapFillsAsSoftwareDoes(Util.filling_rule_e rule)
		{
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			Graphics2D graphics = capture.BeginWidgetFrame(new ColorF(0, 0, 0, 0));
			graphics.FillRectangle(0, 0, Width, Height, Color.White);
			((IFillRuleGraphics)graphics).FillingRule = rule;
			graphics.Render(TwoCircles(), Shade);
			((IFillRuleGraphics)graphics).FillingRule = Util.filling_rule_e.fill_non_zero;
			ImageBuffer gpu = await capture.CaptureAsync();

			var software = new ImageBuffer(Width, Height);
			Graphics2D softwareGraphics = software.NewGraphics2D();
			softwareGraphics.FillRectangle(0, 0, Width, Height, Color.White);
			softwareGraphics.Rasterizer.filling_rule(rule);
			softwareGraphics.Render(TwoCircles(), Shade);
			softwareGraphics.Rasterizer.filling_rule(Util.filling_rule_e.fill_non_zero);

			// The middle of the overlap: empty under even-odd, filled under non-zero.
			await Assert.That(gpu.GetPixel(60, 40)).IsEqualTo(software.GetPixel(60, 40));
			await AssertNear(gpu, software);
		}

		/// <summary>
		/// The GPU caches a fill's tessellation by its geometry; the same path filled again under the other rule
		/// must not reuse the first rule's triangles.
		/// </summary>
		[Test]
		public async Task SamePathUnderTheOtherRuleIsTessellatedAgain()
		{
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			Graphics2D graphics = capture.BeginWidgetFrame(new ColorF(0, 0, 0, 0));
			graphics.FillRectangle(0, 0, Width, Height, Color.White);
			((IFillRuleGraphics)graphics).FillingRule = Util.filling_rule_e.fill_even_odd;
			graphics.Render(TwoCircles(), Shade);
			((IFillRuleGraphics)graphics).FillingRule = Util.filling_rule_e.fill_non_zero;
			graphics.Render(TwoCircles(), Shade);
			ImageBuffer gpu = await capture.CaptureAsync();

			await Assert.That(gpu.GetPixel(60, 40)).IsEqualTo(Shade);
		}

		/// <summary>
		/// RenderInRect fills a <see cref="ColoredVertexSource"/> marked even-odd by its rule - on the GPU too, which has
		/// no rasterizer to set it on - and puts non-zero back after.
		/// </summary>
		[Test]
		public async Task RenderInRectFillsEvenOddSourcesByTheirRule()
		{
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			Graphics2D graphics = capture.BeginWidgetFrame(new ColorF(0, 0, 0, 0));
			graphics.FillRectangle(0, 0, Width, Height, Color.White);
			var source = new[] { new ColoredVertexSource(TwoCircles(), Shade, FillEvenOdd: true) };
			graphics.RenderInRect(source, new RectangleDouble(20, 15, 100, 65), out _);
			ImageBuffer gpu = await capture.CaptureAsync();

			await Assert.That(gpu.GetPixel(60, 40)).IsEqualTo(Color.White);
			await Assert.That(((IFillRuleGraphics)graphics).FillingRule).IsEqualTo(Util.filling_rule_e.fill_non_zero);
		}

		private static IVertexSource TwoCircles()
		{
			var path = new VertexStorage();
			path.ConcatPath(new Ellipse(45, 40, 25, 25, 64));
			path.ConcatPath(new Ellipse(75, 40, 25, 25, 64));
			return path;
		}

		private static async Task AssertNear(ImageBuffer gpu, ImageBuffer software)
		{
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					Color g = gpu.GetPixel(x, y);
					Color s = software.GetPixel(x, y);
					int worst = Math.Max(Math.Max(Math.Abs(g.red - s.red), Math.Abs(g.green - s.green)), Math.Max(Math.Abs(g.blue - s.blue), Math.Abs(g.alpha - s.alpha)));
					if (worst > Tolerance)
					{
						await Assert.That(worst).IsLessThanOrEqualTo(Tolerance)
							.Because($"({x}, {y}): GPU {g} vs software {s}");
					}
				}
			}
		}
	}
}
