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
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Tests.GoldenImages;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// gouraud.cpp's port against C++ AGG (demo_gouraud.cpp): six span_gouraud_rgba triangles, dilated, under
	// a gamma_linear rasterizer gamma, and the three sliders drawn after the gamma is put back to none.
	public class GouraudDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new GouraudDemo()), "gouraud_400x320");
		}

		/// <summary>Thin dilation, a low gamma and 0.6 opacity (so the seams and the alpha blend both show), on a moved triangle.</summary>
		[Test]
		public async Task ThinDimMovedFrameMatchesCppAgg()
		{
			var demo = new GouraudDemo();
			demo.DilationSlider.Value = 0.05;
			demo.GammaSlider.Value = 0.5;
			demo.AlphaSlider.Value = 0.6;
			demo.SetVertex(0, 80, 40);
			demo.SetVertex(1, 350, 200);
			demo.SetVertex(2, 120, 290);

			await AggReference.Check(Render(demo), "gouraud_400x320_thin_dim_moved");
		}

		/// <summary>
		/// A software surface with a rasterizer but no byte back buffer (LcdBufferGraphics2D) still gets the
		/// triangles: rendered into a layer and drawn as an image. The corner of the first outer (red, green,
		/// black) triangle, dark in the reference, must be dark here too rather than left white.
		/// </summary>
		[Test]
		public async Task LcdSurfaceStillDrawsTheTriangles()
		{
			var demo = new GouraudDemo();
			var buffer = new LcdBuffer(demo.Width, demo.Height);
			demo.Draw(new LcdBufferGraphics2D(buffer));

			Color pixel = buffer.ToImageBufferCollapsed().GetPixel(236, 55);
			Color reference = Render(new GouraudDemo()).GetPixel(236, 55);

			await Assert.That(reference.red + reference.green + reference.blue).IsLessThan(150);
			await Assert.That(pixel.red + pixel.green + pixel.blue).IsLessThan(150);
		}

		/// <summary>A press inside the triangle away from its corners drags all three corners.</summary>
		[Test]
		public async Task DraggingInsideTheTriangleMovesEveryCorner()
		{
			var demo = new GouraudDemo();
			demo.OnMouseDown(190, 180, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(200, 170, AggInputFlags.MouseLeft);
			demo.OnMouseUp(200, 170, AggInputFlags.MouseLeft, AggInputFlags.None);

			await Assert.That(demo.GetVertex(0)).IsEqualTo(new Vector2(67, 50));
			await Assert.That(demo.GetVertex(1)).IsEqualTo(new Vector2(379, 160));
			await Assert.That(demo.GetVertex(2)).IsEqualTo(new Vector2(153, 300));
		}

		private static ImageBuffer Render(GouraudDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
