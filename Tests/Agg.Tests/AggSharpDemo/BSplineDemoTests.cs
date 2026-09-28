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
using MatterHackers.Agg.Tests.GoldenImages;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// bspline.cpp's port against C++ AGG (demo_bspline.cpp): the spline stroke, the interactive polygon's
	// outline and handles, and the two ctrls. The drags go through InteractivePolygon's three grab modes.
	public class BSplineDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new BSplineDemo()), "bspline_600x600");
		}

		/// <summary>A press within the point radius of a point drags just that point, keeping the grab offset.</summary>
		[Test]
		public async Task DraggingAPointMovesOnlyThatPoint()
		{
			var demo = Drag(503, 102, 483, 122);

			await Assert.That(demo.Polygon.GetPoint(1)).IsEqualTo(new Vector2(480, 120));
			await Assert.That(demo.Polygon.GetPoint(0)).IsEqualTo(new Vector2(100, 100));
			await Assert.That(demo.Polygon.GetPoint(2)).IsEqualTo(new Vector2(500, 500));
		}

		/// <summary>A press on an edge away from its points drags both of the edge's points.</summary>
		[Test]
		public async Task DraggingAnEdgeMovesBothItsPoints()
		{
			var demo = Drag(300, 103, 300, 113);

			await Assert.That(demo.Polygon.GetPoint(0)).IsEqualTo(new Vector2(100, 110));
			await Assert.That(demo.Polygon.GetPoint(1)).IsEqualTo(new Vector2(500, 110));
			await Assert.That(demo.Polygon.GetPoint(2)).IsEqualTo(new Vector2(500, 500));
		}

		/// <summary>A press inside the polygon away from its edges drags every point, and the frame shows it.</summary>
		[Test]
		public async Task DraggingInsideThePolygonMovesEveryPoint()
		{
			var demo = Drag(400, 400, 410, 390);

			var moved = new BSplineDemo();
			for (int i = 0; i < 6; i++)
			{
				Vector2 point = moved.Polygon.GetPoint(i);
				moved.Polygon.SetPoint(i, point.X + 10, point.Y - 10);
			}

			await Assert.That(Render(demo).GetBuffer()).IsEquivalentTo(Render(moved).GetBuffer());
		}

		/// <summary>
		/// A grabbed point's circle is drawn 1.2 times the point radius, and shrinks back when it is let go. The
		/// grabbed point is the polygon's leftmost, so its circle sets the left edge of the vertex bounds.
		/// </summary>
		[Test]
		public async Task GrabbedPointIsDrawnLarger()
		{
			var polygon = new InteractivePolygon(3, 5.0);
			polygon.SetPoint(0, 0, 0);
			polygon.SetPoint(1, 100, 0);
			polygon.SetPoint(2, 50, 100);

			await Assert.That(polygon.GetBounds().Left).IsEqualTo(-5.0).Within(1e-9);

			await Assert.That(polygon.OnMouseButtonDown(1, 1)).IsTrue();
			await Assert.That(polygon.Node).IsEqualTo(0);
			await Assert.That(polygon.GetBounds().Left).IsEqualTo(-6.0).Within(1e-9);

			polygon.OnMouseButtonUp(1, 1);
			await Assert.That(polygon.GetBounds().Left).IsEqualTo(-5.0).Within(1e-9);
		}

		private static BSplineDemo Drag(int fromX, int fromY, int toX, int toY)
		{
			var demo = new BSplineDemo();
			demo.OnMouseDown(fromX, fromY, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(toX, toY, AggInputFlags.MouseLeft);
			demo.OnMouseUp(toX, toY, AggInputFlags.MouseLeft, AggInputFlags.None);
			return demo;
		}

		private static ImageBuffer Render(BSplineDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
