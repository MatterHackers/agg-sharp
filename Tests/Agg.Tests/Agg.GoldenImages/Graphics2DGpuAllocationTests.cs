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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// The browser build runs on a single-threaded Mono GC with a small nursery, so every short-lived object a
	/// frame allocates brings the next mid-frame collection closer. A steady-state frame of ordinary 2D draws -
	/// everything already tessellated and cached - should allocate next to nothing on the GPU path.
	/// </summary>
	[NotInParallel]
	public class Graphics2DGpuAllocationTests
	{
		private const int WarmupFrames = 5;
		private const int MeasuredFrames = 50;

		/// <summary>
		/// The bytes one steady-state frame may allocate. The frame below allocated 191 KB when every draw
		/// rebuilt its shape's vertices (and a byte[] per coordinate) to hash it for the tessellation cache,
		/// and about 23 KB after. What is left is the compat layer's submission (about 0.7 KB per cached draw,
		/// 3.7 KB per immediate-mode batch), which this budget does not yet ask to go.
		/// </summary>
		private const long PerFrameBudgetBytes = 28 * 1024;

		private static readonly RoundedRect Button = new RoundedRect(20.25, 30.25, 140.75, 60.75, 6);
		private static readonly Ellipse Dot = new Ellipse(200.5, 100.5, 9, 9);
		private static readonly VertexStorage Triangle = MakeTriangle();
		private static readonly Affine Scaled = Affine.NewScaling(1.5) * Affine.NewTranslation(10.5, 4.25);

		private static VertexStorage MakeTriangle()
		{
			var triangle = new VertexStorage();
			triangle.MoveTo(300, 40);
			triangle.LineTo(360, 40);
			triangle.LineTo(330, 90);
			triangle.ClosePolygon();
			return triangle;
		}

		/// <summary>A representative widget frame: fills, strokes, a cached shape moved about, rectangles both
		/// pixel aligned and not, under the identity and under a scale.</summary>
		private static void DrawFrame(Graphics2D graphics)
		{
			graphics.Render(Button, new Color(40, 120, 200));
			graphics.Render(Dot, Color.Red);
			graphics.Render(Triangle, new Color(10, 200, 10, 128));
			graphics.Render(Triangle, 15.5, 7.25, Color.Black);
			graphics.Line(10.5, 200.25, 220.75, 260.5, Color.Black, 1.5);
			graphics.Line(10, 300, 200, 300, Color.Black, 1);
			graphics.Rectangle(250.5, 150.5, 330.25, 220.75, Color.Blue, 2);
			graphics.Rectangle(250, 230, 330, 290, Color.Blue, 1);
			graphics.FillRectangle(360, 150, 420, 210, Color.Green);
			graphics.FillRectangle(360.5, 220.25, 420.75, 280.5, Color.Green);

			graphics.PushTransform();
			graphics.SetTransform(graphics.GetTransform() * Scaled);
			graphics.Render(Button, new Color(200, 120, 40));
			graphics.Render(Dot, Color.Red);
			graphics.PopTransform();
		}

		[Test]
		public async Task ASteadyStateFrameStaysUnderItsAllocationBudget()
		{
			using var capture = WebGpuOffscreenCapture.Create();
			var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));

			for (int frame = 0; frame < WarmupFrames; frame++)
			{
				DrawFrame(graphics);
			}

			long before = GC.GetAllocatedBytesForCurrentThread();
			for (int frame = 0; frame < MeasuredFrames; frame++)
			{
				DrawFrame(graphics);
			}

			long perFrame = (GC.GetAllocatedBytesForCurrentThread() - before) / MeasuredFrames;
			Console.WriteLine($"Graphics2DGpu steady-state frame allocates {perFrame} bytes");

			await Assert.That(perFrame).IsLessThan(PerFrameBudgetBytes);
		}
	}
}
