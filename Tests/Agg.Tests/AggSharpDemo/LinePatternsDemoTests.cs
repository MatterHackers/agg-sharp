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
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// line_patterns.cpp's port against C++ AGG (demo_line_patterns.cpp).
	public class LinePatternsDemoTests
	{
		/// <summary>The default frame: the nine curves, the patterns unscaled and starting at 0.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new LinePatternsDemo()), "line_patterns_500x450");
		}

		/// <summary>The patterns stretched 2.2 times along the curves and slid 4.5 pixels along them.</summary>
		[Test]
		public async Task StretchedShiftedMatchesCppAgg()
		{
			var demo = new LinePatternsDemo();
			demo.ScaleXSlider.Value = 2.2;
			demo.StartXSlider.Value = 4.5;

			await AggReference.Check(Render(demo), "line_patterns_500x450_stretched_shifted");
		}

		/// <summary>
		/// A curve's point drags with the mouse, and a press inside a curve's control polygon but away from its
		/// points and edges grabs nothing (bezier_ctrl turns polygon_ctrl's in_polygon_check off).
		/// </summary>
		[Test]
		public async Task CurvePointsDragAndTheirPolygonInsideDoesNot()
		{
			var curve = new BezierCtrl();
			curve.SetCurve(0, 0, 100, 0, 100, 100, 0, 100);

			await Assert.That(curve.OnMouseButtonDown(50, 50)).IsFalse();
			curve.OnMouseButtonUp(50, 50);

			await Assert.That(curve.OnMouseButtonDown(101, 2)).IsTrue();
			curve.OnMouseMove(121, 32, true);
			curve.OnMouseButtonUp(121, 32);
			await Assert.That(curve.GetPoint(1).X).IsEqualTo(120);
			await Assert.That(curve.GetPoint(1).Y).IsEqualTo(30);
			await Assert.That(curve.GetPoint(0).X).IsEqualTo(0);
		}

		private static ImageBuffer Render(LinePatternsDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
