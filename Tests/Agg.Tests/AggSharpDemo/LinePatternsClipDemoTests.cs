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
	// line_patterns_clip.cpp's port against C++ AGG (demo_line_patterns_clip.cpp).
	public class LinePatternsClipDemoTests
	{
		/// <summary>The default frame: the polyline in both passes, the pattern unscaled and starting at 0.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new LinePatternsClipDemo()), "line_patterns_clip_500x500");
		}

		/// <summary>
		/// The pattern stretched 1.7 times and slid 3.5 pixels, the last point moved, and zoomed in twice about
		/// (300, 200), which pushes the polyline further through the clip box and thins the ctrl's line and points
		/// before the zoom so they draw the same size.
		/// </summary>
		[Test]
		public async Task StretchedZoomedMovedMatchesCppAgg()
		{
			var demo = new LinePatternsClipDemo();
			demo.ScaleXSlider.Value = 1.7;
			demo.StartXSlider.Value = 3.5;
			demo.Polyline.SetPoint(4, 160, 330);
			demo.ZoomAbout(300, 200, true);
			demo.ZoomAbout(300, 200, true);

			await AggReference.Check(Render(demo), "line_patterns_clip_500x500_stretched_zoomed_moved");
		}

		/// <summary>A point drags through the zoom: the press and the move map back to the unzoomed polyline.</summary>
		[Test]
		public async Task PointDragsThroughTheZoom()
		{
			var demo = new LinePatternsClipDemo();
			demo.ZoomAbout(0, 0, true);

			// Point 4, (100, 300), is drawn at (110, 330); 11 pixels on screen is 10 before the zoom.
			demo.OnMouseDown(110, 330, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(121, 341, AggInputFlags.MouseLeft);
			demo.OnMouseUp(121, 341, AggInputFlags.MouseLeft, AggInputFlags.None);

			await Assert.That(demo.Polyline.GetPoint(4).X).IsEqualTo(110).Within(1e-9);
			await Assert.That(demo.Polyline.GetPoint(4).Y).IsEqualTo(310).Within(1e-9);
		}

		private static ImageBuffer Render(LinePatternsClipDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
