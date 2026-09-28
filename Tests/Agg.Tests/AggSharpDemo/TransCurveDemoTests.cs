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
using System.Collections.Generic;
using System.Linq;
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
	// trans_curve1.cpp and trans_curve2.cpp's ports. The C++ reference (demo_trans_curve.cpp) cannot draw the
	// originals' Win32 TrueType font, so it draws gsv_text; with GsvCurveTextFont swapped in, the demos match it
	// byte for byte - the bent text, the B-splines, the interactive polygons and the ctrls. The Liberation Serif
	// text the demos really draw has no C++ reference and is only checked to land along the curve.
	public class TransCurveDemoTests
	{
		private static readonly double[] InitialPoints = { 50, 50, 170, 130, 230, 270, 370, 330, 430, 470, 550, 550 };

		[Test]
		public async Task TransCurve1DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new TransCurve1Demo { TextFont = new GsvCurveTextFont() }), "trans_curve1_600x600");
		}

		/// <summary>Closed curve, index lookup instead of arc length, no fixed length, fewer points, every point moved.</summary>
		[Test]
		public async Task TransCurve1ClosedAndMovedMatchesCppAgg()
		{
			var demo = new TransCurve1Demo { TextFont = new GsvCurveTextFont() };
			demo.NumPointsSlider.Value = 60;
			demo.CloseBox.Checked = true;
			demo.PreserveXScaleBox.Checked = false;
			demo.FixedLengthBox.Checked = false;
			double[] points = { 80, 60, 200, 90, 250, 300, 330, 280, 480, 420, 520, 560 };
			for (int i = 0; i < 6; i++)
			{
				demo.SetPoint(i, points[i * 2], points[(i * 2) + 1]);
			}

			await AggReference.Check(Render(demo), "trans_curve1_600x600_closed_moved");
		}

		[Test]
		public async Task TransCurve2DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new TransCurve2Demo { TextFont = new GsvCurveTextFont() }), "trans_curve2_600x600");
		}

		[Test]
		public async Task TransCurve2IndexedAndMovedMatchesCppAgg()
		{
			var demo = new TransCurve2Demo { TextFont = new GsvCurveTextFont() };
			demo.NumPointsSlider.Value = 40;
			demo.PreserveXScaleBox.Checked = false;
			demo.FixedLengthBox.Checked = false;
			double[] points1 = { 70, 30, 190, 110, 250, 250, 390, 310, 450, 450, 570, 530 };
			double[] points2 = { 30, 70, 150, 150, 210, 290, 350, 350, 410, 490, 530, 570 };
			for (int i = 0; i < 6; i++)
			{
				demo.SetPoint(1, i, points1[i * 2], points1[(i * 2) + 1]);
				demo.SetPoint(2, i, points2[i * 2], points2[(i * 2) + 1]);
			}

			await AggReference.Check(Render(demo), "trans_curve2_600x600_indexed_moved");
		}

		[Test]
		public async Task DraggingACurvePointMovesIt()
		{
			var demo = new TransCurve1Demo();
			demo.OnMouseDown(372, 328, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(400, 300, AggInputFlags.MouseLeft);
			demo.OnMouseUp(400, 300, AggInputFlags.MouseLeft, AggInputFlags.None);

			var moved = new TransCurve1Demo();
			moved.SetPoint(3, 398, 302);

			await Assert.That(Render(demo).GetBuffer()).IsEquivalentTo(Render(moved).GetBuffer());
		}

		[Test]
		public async Task AnimateRunsContinuouslyAndMovesTheCurve()
		{
			var demo = new TransCurve2Demo();
			byte[] before = Render(demo).GetBuffer();

			demo.OnMouseDown(355, 30, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseUp(355, 30, AggInputFlags.MouseLeft, AggInputFlags.None);
			await Assert.That(demo.AnimateBox.Checked).IsTrue();
			await Assert.That(demo.WaitMode).IsFalse();

			demo.OnIdle();
			await Assert.That(Render(demo).GetBuffer()).IsNotEquivalentTo(before);
		}

		/// <summary>
		/// The TrueType text is drawn (plenty of pixels differ from the same frame without text), hugs the
		/// curve (every changed pixel is within a glyph's height of it) and stands on its left, where y above the
		/// baseline goes: nearly every changed pixel is left of the nearest spline segment, so text flipped
		/// upside down or onto the other side fails.
		/// </summary>
		[Test]
		public async Task TrueTypeTextRendersAlongTheCurve()
		{
			ImageBuffer withText = Render(new TransCurve1Demo());
			ImageBuffer withoutText = Render(new TransCurve1Demo { TextFont = new NoTextFont() });

			var curve = new List<Vector2>();
			var path = new VertexStorage();
			path.MoveTo(InitialPoints[0], InitialPoints[1]);
			for (int i = 1; i < 6; i++)
			{
				path.LineTo(InitialPoints[i * 2], InitialPoints[(i * 2) + 1]);
			}

			foreach (VertexData vertex in new BSplinePath(path) { InterpolationStep = 1.0 / 200 }.Vertices())
			{
				if (vertex.IsVertex)
				{
					curve.Add(vertex.Position);
				}
			}

			int changed = 0;
			int leftOfCurve = 0;
			double farthest = 0;
			for (int y = 0; y < withText.Height; y++)
			{
				for (int x = 0; x < withText.Width; x++)
				{
					if (withText.GetPixel(x, y) != withoutText.GetPixel(x, y))
					{
						changed++;
						var pixel = new Vector2(x + 0.5, y + 0.5);
						int nearest = 0;
						for (int i = 1; i < curve.Count; i++)
						{
							if ((curve[i] - pixel).Length < (curve[nearest] - pixel).Length)
							{
								nearest = i;
							}
						}

						farthest = Math.Max(farthest, (curve[nearest] - pixel).Length);

						// The direction of travel at the nearest spline point, crossed with the offset to the pixel.
						int from = Math.Min(nearest, curve.Count - 2);
						Vector2 along = curve[from + 1] - curve[from];
						Vector2 offset = pixel - curve[nearest];
						if ((along.X * offset.Y) - (along.Y * offset.X) > 0)
						{
							leftOfCurve++;
						}
					}
				}
			}

			await Assert.That(changed).IsGreaterThan(5000);
			await Assert.That(farthest).IsLessThan(50.0);

			// Descenders and antialiased edges dip just below the baseline; the glyph bodies do not.
			await Assert.That((double)leftOfCurve / changed).IsGreaterThan(0.9);
		}

		private static ImageBuffer Render(AggDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}

		// Advances like text but draws nothing, for the frame the text is compared against.
		private class NoTextFont : CurveTextFont
		{
			public override IVertexSource GlyphAt(char character, double x, double y) => null;

			public override double NextPenX(string text, int index, double x, double y) => x + 10;
		}
	}
}
