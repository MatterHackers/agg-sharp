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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// lion_outline.cpp's port against C++ AGG (demo_lion_outline.cpp): the outline rasterizer and the
	// conv_stroke path, each with its ctrls, the mouse pose, and drawing under a graphics transform and clip.
	public class LionOutlineDemoTests
	{
		/// <summary>The default frame: width 1 through rasterizer_outline_aa, round joins, no caps.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new LionOutlineDemo()), "lion_outline_512x512");
		}

		/// <summary>Turned, scaled and skewed, with a width-2.5 profile that scales with the lion.</summary>
		[Test]
		public async Task TurnedWideFrameMatchesCppAgg()
		{
			var demo = new LionOutlineDemo();
			demo.SetPose(0.7, 1.6, 150, -80);
			demo.WidthSlider.Value = 2.5;

			await AggReference.Check(Render(demo), "lion_outline_512x512_turned_wide");
		}

		/// <summary>The checkbox's conv_stroke path (round joins) at width 1.5.</summary>
		[Test]
		public async Task ScanlineFrameMatchesCppAgg()
		{
			var demo = new LionOutlineDemo();
			demo.ScanlineCbox.Checked = true;
			demo.WidthSlider.Value = 1.5;

			await AggReference.Check(Render(demo), "lion_outline_512x512_scanline");
		}

		/// <summary>
		/// A left-drag sets the angle and scale from the pointer's offset from the middle, a right-drag the
		/// skew, as lion_outline.cpp's on_mouse_button_down does.
		/// </summary>
		[Test]
		public async Task LeftDragTurnsAndRightDragSkewsTheLion()
		{
			var demo = new LionOutlineDemo();
			ImageBuffer before = Render(demo);

			// 100 pixels straight above the middle, reached by a drag: a quarter turn at scale 1.
			demo.OnMouseDown(356, 256, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(256, 356, AggInputFlags.MouseLeft);
			demo.OnMouseUp(256, 356, AggInputFlags.MouseLeft, AggInputFlags.None);

			var turned = new LionOutlineDemo();
			turned.SetPose(Math.PI / 2, 1.0, 0, 0);
			await Assert.That(AggReference.Compare(Render(turned), Render(demo)).Identical).IsTrue();
			await Assert.That(AggReference.Compare(before, Render(demo)).Identical).IsFalse();

			demo.OnMouseDown(300, 200, AggInputFlags.MouseRight, AggInputFlags.MouseRight);
			demo.OnMouseMove(310, 220, AggInputFlags.MouseRight);
			demo.OnMouseUp(310, 220, AggInputFlags.MouseRight, AggInputFlags.None);

			var skewed = new LionOutlineDemo();
			skewed.SetPose(Math.PI / 2, 1.0, 310, 220);
			await Assert.That(AggReference.Compare(Render(skewed), Render(demo)).Identical).IsTrue();
		}

		/// <summary>
		/// Drawn on an image graphics that is translated and clipped (the GPU-mode view on a software surface),
		/// the outline rasterizer follows the transform and stays inside the clip: the clipped window shows
		/// the reference frame shifted, and nothing outside it is touched.
		/// </summary>
		[Test]
		public async Task OutlinesFollowTheGraphicsTranslationAndClip()
		{
			var demo = new LionOutlineDemo();
			ImageBuffer reference = Render(demo);

			var image = new ImageBuffer(700, 600);
			Graphics2D graphics = image.NewGraphics2D();
			graphics.SetTransform(Affine.NewTranslation(100, 50));
			var clip = new RectangleDouble(150, 120, 450, 420);
			graphics.SetClippingRect(clip);
			demo.Draw(graphics);

			int wrong = 0;
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					bool inside = x >= clip.Left && x < clip.Right && y >= clip.Bottom && y < clip.Top;
					Color expected = inside ? reference.GetPixel(x - 100, y - 50) : new Color(0, 0, 0, 0);
					if (image.GetPixel(x, y) != expected)
					{
						wrong++;
					}
				}
			}

			await Assert.That(wrong).IsEqualTo(0);
		}

		/// <summary>Under a 2x scale the outlines scale with the rest of the demo instead of staying demo-sized.</summary>
		[Test]
		public async Task OutlinesFollowTheGraphicsScale()
		{
			var demo = new LionOutlineDemo();
			var image = new ImageBuffer(1024, 1024);
			Graphics2D graphics = image.NewGraphics2D();
			graphics.SetTransform(Affine.NewScaling(2));
			demo.Draw(graphics);

			// Unscaled, the lion spans about x 140..380; at 2x its right side lies past 600. Rows below 100
			// hold the (scaled) ctrls, whose label also reaches past 600, so they are left out.
			int darkRightOfSix = 0;
			for (int y = 100; y < image.Height; y++)
			{
				for (int x = 600; x < image.Width; x++)
				{
					if (image.GetPixel(x, y).red < 128)
					{
						darkRightOfSix++;
					}
				}
			}

			await Assert.That(darkRightOfSix).IsGreaterThan(0);
		}

		private static ImageBuffer Render(LionOutlineDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
