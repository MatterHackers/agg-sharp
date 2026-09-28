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
using System.Diagnostics;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Tests.GoldenImages;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	/// <summary>
	/// A demo whose renderer writes pixels itself (renderer_outline_aa) draws those same pixels on the GPU through a <see cref="Graphics2DSpanImage"/>: the GPU frame matches the
	/// software frame outside the ctrls (vector fills and strokes, which each path anti-aliases its own way).
	/// </summary>
	[NotInParallel]
	public class GpuSpanDemoTests
	{
		private const int Tolerance = 3;

		// The blurred shadow carries the GPU's anti-aliasing of its edge (not AGG's), spread thin by the blur.
		private const int BlurTolerance = 4;

		[Test]
		public async Task LionOutlineMatchesTheSoftwareFrame()
		{
			await AssertGpuMatchesSoftware(new LionOutlineDemo(), 25);
		}

		/// <summary>The bezier ctrls cross the whole frame, so the pixels they touch are left out rather than a band.</summary>
		[Test]
		public async Task LinePatternsMatchesTheSoftwareFrame()
		{
			var demo = new LinePatternsDemo();
			await AssertGpuMatchesSoftware(demo, 0, ctrls =>
			{
				foreach (BezierCtrl curve in demo.Curves)
				{
					curve.Render(ctrls);
				}

				demo.ScaleXSlider.Render(ctrls);
				demo.StartXSlider.Render(ctrls);
			});
		}

		/// <summary>The sliders and the length text sit in the bottom 45 rows; the polyline ctrl crosses the frame.</summary>
		[Test]
		public async Task LinePatternsClipMatchesTheSoftwareFrame()
		{
			var demo = new LinePatternsClipDemo();
			await AssertGpuMatchesSoftware(demo, 45, ctrls => ctrls.Render(new VertexSourceApplyTransform(demo.Polyline, demo.Zoom), Color.Black));
		}

		/// <summary>
		/// The four pixel-renderer lanes; the scanline lane (top middle) and the labels are vector draws, left out
		/// by the boxes around them, and the ctrls sit in the bottom 45 rows.
		/// </summary>
		[Test]
		public async Task Rasterizers2MatchesTheSoftwareFrame()
		{
			var demo = new Rasterizers2Demo();
			await AssertGpuMatchesSoftware(demo, 45, vectors =>
			{
				double w = demo.Width, h = demo.Height;
				vectors.FillRectangle((w / 2) - 85, h - (h / 4) + 20 - 85, (w / 2) + 85, h - (h / 4) + 20 + 85, Color.Black);
				foreach ((double x, double y) in new[] { (50.0, 80.0), ((w / 2) - 50, 80.0), (50.0, (h / 2) + 50), ((w / 2) - 50, (h / 2) + 50), (w - (w / 5) - 50, (h / 2) + 50) })
				{
					vectors.FillRectangle(x - 3, y - 30, x + 150, y + 12, Color.Black);
				}
			});
		}

		[Test]
		public async Task ScanlineBooleanMatchesTheSoftwareFrame()
		{
			var demo = new ScanlineBooleanDemo();
			await AssertGpuMatchesSoftware(demo, 150, vectors =>
			{
				vectors.Render(new Stroke(demo.Quad1, 1), Color.Black);
				vectors.Render(new Stroke(demo.Quad2, 1), Color.Black);
			});
		}

		[Test]
		public async Task ScanlineBoolean2MatchesTheSoftwareFrame()
		{
			await AssertGpuMatchesSoftware(new ScanlineBoolean2Demo(), 150);
		}

		[Test]
		public async Task FlashRasterizerMatchesTheSoftwareFrame()
		{
			var demo = new FlashRasterizerDemo();
			await AssertGpuMatchesSoftware(demo, 0, vectors => FlashOutlinesAndHelp(vectors, demo));
		}

		/// <summary>Translucent layers, so the layered mix goes through premultiplied color spans; the ctrls are in the bottom 80 rows
		/// and the background triangles' diagonals are vector draws.</summary>
		[Test]
		public async Task RasterizerCompoundMatchesTheSoftwareFrame()
		{
			var demo = new RasterizerCompoundDemo();
			demo.Alpha1Slider.Value = 0.5;
			demo.Alpha3Slider.Value = 0.7;
			await AssertGpuMatchesSoftware(demo, 80, vectors =>
			{
				var diagonals = new VertexStorage();
				diagonals.MoveTo(0, 0);
				diagonals.LineTo(demo.Width, demo.Height);
				diagonals.MoveTo(0, demo.Height);
				diagonals.LineTo(demo.Width, 0);
				vectors.Render(new Stroke(diagonals, 1), Color.Black);
			});
		}

		/// <summary>The gradient nodes alone (the edges are vector fills over them), away from the ctrls.</summary>
		[Test]
		public async Task GraphTestNodesMatchTheSoftwareFrame()
		{
			var demo = new GraphTestDemo();
			demo.DrawEdgesCbox.Checked = false;
			await AssertGpuMatchesSoftware(demo, 0, ctrls =>
			{
				demo.TypeRbox.Render(ctrls);
				demo.WidthSlider.Render(ctrls);
				demo.BenchmarkCbox.Render(ctrls);
				demo.DrawNodesCbox.Render(ctrls);
				demo.DrawEdgesCbox.Render(ctrls);
				demo.DraftCbox.Render(ctrls);
				demo.TranslucentCbox.Render(ctrls);
			});
		}

		/// <summary>The ctrls are in the bottom 95 rows; the black disc under the gradient is a vector fill, so its rim is left out.</summary>
		[Test]
		public async Task DistortionsMatchesTheSoftwareFrame()
		{
			var demo = new DistortionsDemo();
			await AssertGpuMatchesSoftware(demo, 95, vectors =>
			{
				// The demo is its image plus 300 by 60; the disc sits right of the image's, radius min(w, h) / 2 - 20.
				double imageWidth = demo.Width - 300, imageHeight = demo.Height - 60;
				double radius = (Math.Min(imageWidth, imageHeight) / 2) - 20;
				var rim = new Ellipse(imageWidth - (imageWidth / 10) + (imageWidth / 2) + 10, (imageHeight / 2) + 50, radius, radius);
				vectors.Render(new Stroke(rim, 1), Color.Black);
			});
		}

		[Test]
		public async Task FlashRasterizer2MatchesTheSoftwareFrame()
		{
			var demo = new FlashRasterizer2Demo();
			await AssertGpuMatchesSoftware(demo, 0, vectors => FlashOutlinesAndHelp(vectors, demo));
		}

		/// <summary>Dilation, gamma and opacity away from their defaults, so each reaches the GPU frame; the sliders are in the bottom 45 rows.</summary>
		[Test]
		public async Task GouraudMatchesTheSoftwareFrame()
		{
			var demo = new GouraudDemo();
			demo.DilationSlider.Value = 0.6;
			demo.GammaSlider.Value = 0.55;
			demo.AlphaSlider.Value = 0.7;
			await AssertGpuMatchesSoftware(demo, 45);
		}

		/// <summary>The report text is a vector draw in the bottom 30 rows.</summary>
		[Test]
		public async Task GouraudMeshMatchesTheSoftwareFrame()
		{
			var demo = new GouraudMeshDemo { ReportedMilliseconds = 1 };
			await AssertGpuMatchesSoftware(demo, 30);
		}

		/// <summary>
		/// Each blur method - stack, recursive, and the recursive blur of only the green channel - over the shadow; the
		/// ctrls, the shadow quad's outline and handles, the glyph's edge and the shadow's own edge (sharp in the
		/// channels the third method leaves alone) are vector draws, left out.
		/// </summary>
		[Test]
		[Arguments(0)]
		[Arguments(1)]
		[Arguments(2)]
		public async Task BlurMatchesTheSoftwareFrame(int method)
		{
			var demo = new BlurDemo();
			demo.MethodRbox.CurrentItem = method;
			await AssertGpuMatchesSoftware(demo, 0, ctrls =>
			{
				demo.MethodRbox.Render(ctrls);
				demo.RadiusSlider.Render(ctrls);
				demo.RedCbox.Render(ctrls);
				demo.GreenCbox.Render(ctrls);
				demo.BlueCbox.Render(ctrls);
				ctrls.Render(demo.ShadowQuad, Color.Black);
				ctrls.Render(new Stroke(demo.Shape, 1), Color.Black);
				if (method == 2)
				{
					ctrls.Render(new Stroke(demo.Shadow, 1), Color.Black);
				}
			}, BlurTolerance);
		}

		/// <summary>
		/// The 3x3 box blur inside the circle against simple_blur's own span generator run over the GPU's unblurred
		/// frame (the lion's many anti-aliased edges are the GPU's, not AGG's); the circle's edge, inside the rim, is
		/// left out.
		/// </summary>
		[Test]
		public async Task SimpleBlurMatchesTheSoftwareBlurOfTheGpuFrame()
		{
			var demo = new SimpleBlurDemo();
			ImageBuffer gpu = await CaptureGpuFrame(demo);
			demo.BlurCircle = false;
			ImageBuffer expected = await CaptureGpuFrame(demo);

			var circle = new Ellipse(100, 102, 100, 100, 100);
			var rasterizer = new ScanlineRasterizer();
			rasterizer.add_path(circle);
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), expected, new span_allocator(), new SimpleBlurDemo.SimpleBlurSpanGenerator(new ImageBuffer(expected)));

			await AssertFramesMatch(demo, gpu, expected, 0, vectors => vectors.Render(new Stroke(circle, 10), Color.Black), 0);
		}

		/// <summary>
		/// The slight blur against SlightBlur run over the GPU's frame drawn unblurred (its strokes are the GPU's, not
		/// AGG's); the ctrls, drawn after the blur, are in the bottom 92 rows.
		/// </summary>
		[Test]
		public async Task LineThicknessMatchesTheSoftwareBlurOfTheGpuFrame()
		{
			var demo = new LineThicknessDemo();
			ImageBuffer gpu = await CaptureGpuFrame(demo);
			double radius = demo.BlurSlider.Value;
			demo.BlurSlider.Value = 0;
			ImageBuffer expected = await CaptureGpuFrame(demo);
			new SlightBlur(radius).Blur(expected, new RectangleInt(0, 0, demo.Width - 1, demo.Height - 1));

			await AssertFramesMatch(demo, gpu, expected, 92, null, 1);
		}

		/// <summary>The flash demos' outlines and help text, vector draws over their fills.</summary>
		private static void FlashOutlinesAndHelp(Graphics2D vectors, FlashRasterizerDemo demo)
		{
			for (int i = 0; i < demo.Shape.Paths; i++)
			{
				if (demo.Shape.Style(i).Line >= 0)
				{
					vectors.Render(new Stroke(FlashDrawing.Outline(demo.Shape, demo.View, i), 1), Color.Black);
				}
			}

			vectors.Render(new Stroke(FlashDrawing.HelpText(), 1), Color.Black);
		}

		/// <summary>
		/// The demo's GPU frame against its software reference frame, above the bottom <paramref name="ctrlBand"/> rows,
		/// where the ctrls are, and away from the pixels <paramref name="drawCtrls"/> touches (and their neighbors,
		/// where anti-aliasing reaches).
		/// </summary>
		private static async Task AssertGpuMatchesSoftware(AggDemo demo, int ctrlBand, Action<Graphics2D> drawCtrls = null, int tolerance = Tolerance)
		{
			ImageBuffer software = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, software);
			await AssertFramesMatch(demo, await CaptureGpuFrame(demo), software, ctrlBand, drawCtrls, tolerance);
		}

		/// <summary>The demo drawn on the GPU offscreen, twice (the first frame builds pipelines), timing each.</summary>
		private static async Task<ImageBuffer> CaptureGpuFrame(AggDemo demo)
		{
			using var capture = WebGpuOffscreenCapture.Create(demo.Width, demo.Height);
			ImageBuffer gpu = null;
			for (int frame = 0; frame < 2; frame++)
			{
				var timer = Stopwatch.StartNew();
				demo.Draw(capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1)));
				gpu = await capture.CaptureAsync();
				Console.WriteLine($"{demo.Name}: GPU frame {timer.Elapsed.TotalMilliseconds:F1} ms");
			}

			return gpu;
		}

		/// <summary>
		/// Compares the frames above the bottom <paramref name="ctrlBand"/> rows and away from the pixels
		/// <paramref name="drawCtrls"/> touches, within <paramref name="tolerance"/> per colour channel.
		/// </summary>
		private static async Task AssertFramesMatch(AggDemo demo, ImageBuffer gpu, ImageBuffer expected, int ctrlBand, Action<Graphics2D> drawCtrls, int tolerance)
		{
			var ctrlPixels = new ImageBuffer(demo.Width, demo.Height);
			drawCtrls?.Invoke(ctrlPixels.NewGraphics2D());

			int bad = 0;
			string first = null;
			for (int y = ctrlBand; y < demo.Height; y++)
			{
				for (int x = 0; x < demo.Width; x++)
				{
					if (NearCtrl(ctrlPixels, x, y))
					{
						continue;
					}

					Color g = gpu.GetPixel(x, y);
					Color s = expected.GetPixel(x, y);
					int worst = Math.Max(Math.Max(Math.Abs(g.red - s.red), Math.Abs(g.green - s.green)), Math.Abs(g.blue - s.blue));
					if (worst > tolerance)
					{
						bad++;
						first ??= $"({x}, {y}): GPU {g} vs expected {s}";
					}
				}
			}

			await Assert.That(bad).IsEqualTo(0).Because(first ?? string.Empty);
		}

		private static bool NearCtrl(ImageBuffer ctrlPixels, int x, int y)
		{
			for (int dy = -1; dy <= 1; dy++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					int nx = x + dx, ny = y + dy;
					if (nx >= 0 && ny >= 0 && nx < ctrlPixels.Width && ny < ctrlPixels.Height && ctrlPixels.GetPixel(nx, ny).alpha != 0)
					{
						return true;
					}
				}
			}

			return false;
		}
	}
}
