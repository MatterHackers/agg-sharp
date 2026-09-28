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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// conv_dash_marker.cpp's port against C++ AGG (demo_conv_dash_marker.cpp): the triangles, their smoothed
	// fill and outline, the dashed stroke with its arrowheads, and the five ctrls.
	public class ConvDashMarkerDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new ConvDashMarkerDemo()), "conv_dash_marker_500x330");
		}

		/// <summary>C++ grabs a vertex within 20 pixels and keeps the grab offset while dragging.</summary>
		[Test]
		public async Task DraggingNearAVertexMovesOnlyThatVertex()
		{
			var demo = new ConvDashMarkerDemo();
			demo.OnMouseDown(460, 165, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(440, 185, AggInputFlags.MouseLeft);
			demo.OnMouseUp(440, 185, AggInputFlags.MouseLeft, AggInputFlags.None);

			var moved = new ConvDashMarkerDemo();
			moved.SetVertex(1, 449, 190);

			await Assert.That(Render(demo).GetBuffer()).IsEquivalentTo(Render(moved).GetBuffer());
		}

		/// <summary>A press inside the triangle away from its vertices drags all three.</summary>
		[Test]
		public async Task DraggingInsideTheTriangleMovesEveryVertex()
		{
			var demo = new ConvDashMarkerDemo();
			demo.OnMouseDown(280, 180, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(290, 170, AggInputFlags.MouseLeft);
			demo.OnMouseUp(290, 170, AggInputFlags.MouseLeft, AggInputFlags.None);

			var moved = new ConvDashMarkerDemo();
			moved.SetVertex(0, 167, 50);
			moved.SetVertex(1, 479, 160);
			moved.SetVertex(2, 253, 300);

			await Assert.That(Render(demo).GetBuffer()).IsEquivalentTo(Render(moved).GetBuffer());
		}

		/// <summary>
		/// Even-Odd Fill changes the path's fills where the two polygons overlap, and is reset before the ctrls
		/// draw: the cap rbox and both sliders come out exactly as they do under the default non-zero rule.
		/// </summary>
		[Test]
		public async Task EvenOddFillChangesTheOverlapButNotTheCtrls()
		{
			ImageBuffer nonZero = Render(new ConvDashMarkerDemo());
			var evenOddDemo = new ConvDashMarkerDemo();
			evenOddDemo.EvenOddCbox.Checked = true;
			ImageBuffer evenOdd = Render(evenOddDemo);

			// Above every ctrl (they all sit below y = 85), only the path is drawn.
			await Assert.That(CountDifferences(nonZero, evenOdd, 0, 85, 500, 330)).IsGreaterThan(0);

			// The cap rbox, then the width and smooth sliders; the path stays clear of all three.
			await Assert.That(CountDifferences(nonZero, evenOdd, 0, 0, 135, 85)).IsEqualTo(0);
			await Assert.That(CountDifferences(nonZero, evenOdd, 135, 0, 500, 28)).IsEqualTo(0);
		}

		/// <summary>
		/// The GPU fills the path (and its smoothed, self-crossing outline) with the same regions software does, under
		/// either rule: it once left a stray light-blue wedge in the smoothed fill. Only pixels a full pixel clear of
		/// every edge are compared - edges differ by design, the GPU's halo anti-aliasing against AGG's area coverage
		/// (a 1-pixel stroke pixel is up to 179 levels apart; see <c>HaloAaTesselator.HaloWidth</c>) - and a wrong
		/// region is wrong across its whole inside.
		/// </summary>
		[Test]
		[NotInParallel]
		[Arguments(false, false, 1.0)]
		[Arguments(true, false, 1.0)]
		[Arguments(true, true, 2.0)]
		public async Task GpuFillsTheSameRegionsAsSoftware(bool evenOdd, bool close, double smooth)
		{
			var demo = new ConvDashMarkerDemo();
			demo.EvenOddCbox.Checked = evenOdd;
			demo.CloseCbox.Checked = close;
			demo.SmoothSlider.Value = smooth;
			ImageBuffer software = Render(demo);

			using var capture = WebGpuOffscreenCapture.Create(demo.Width, demo.Height);
			demo.Draw(capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1)));
			ImageBuffer gpu = await capture.CaptureAsync();

			int compared = 0;
			int bad = 0;
			string first = null;
			for (int y = 1; y < demo.Height - 1; y++)
			{
				for (int x = 1; x < demo.Width - 1; x++)
				{
					if (!IsInsideARegion(software, x, y))
					{
						continue;
					}

					compared++;
					if (Difference(gpu.GetPixel(x, y), software.GetPixel(x, y)) > 3)
					{
						bad++;
						first ??= $"({x}, {y}): GPU {gpu.GetPixel(x, y)} vs software {software.GetPixel(x, y)}";
					}
				}
			}

			// Most of the frame is region inside, so the check is not vacuous.
			await Assert.That(compared).IsGreaterThan(demo.Width * demo.Height / 2);
			await Assert.That(bad).IsEqualTo(0).Because(first ?? string.Empty);
		}

		/// <summary>Software's pixel and its eight neighbors are one color: no edge's anti-aliasing reaches it.</summary>
		private static bool IsInsideARegion(ImageBuffer software, int x, int y)
		{
			Color center = software.GetPixel(x, y);
			for (int dy = -1; dy <= 1; dy++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					if (Difference(software.GetPixel(x + dx, y + dy), center) > 2)
					{
						return false;
					}
				}
			}

			return true;
		}

		private static int Difference(Color a, Color b)
		{
			return Math.Max(Math.Max(Math.Abs(a.red - b.red), Math.Abs(a.green - b.green)), Math.Abs(a.blue - b.blue));
		}

		private static int CountDifferences(ImageBuffer a, ImageBuffer b, int left, int bottom, int right, int top)
		{
			int count = 0;
			for (int y = bottom; y < top; y++)
			{
				for (int x = left; x < right; x++)
				{
					if (a.GetPixel(x, y) != b.GetPixel(x, y))
					{
						count++;
					}
				}
			}

			return count;
		}

		private static ImageBuffer Render(ConvDashMarkerDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
