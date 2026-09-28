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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// image_transforms.cpp's port against C++ AGG (demo_image_transforms.cpp).
	public class ImageTransformsDemoTests
	{
		/// <summary>The default frame: example 0's identity image matrix under the unturned star.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new ImageTransformsDemo()), "image_transforms_320x300");
		}

		/// <summary>
		/// Example 2 with the star and the image turned and scaled and both centers moved, so the bilinear
		/// filter blends between pixels and meets the image's edge.
		/// </summary>
		[Test]
		public async Task TurnedExample2FrameMatchesCppAgg()
		{
			var demo = new ImageTransformsDemo();
			demo.PolygonAngleSlider.Value = -20;
			demo.PolygonScaleSlider.Value = 1.2;
			demo.ImageAngleSlider.Value = 30;
			demo.ImageScaleSlider.Value = 1.3;
			demo.ExampleRbox.CurrentItem = 2;
			demo.ImageCenterX = 180;
			demo.ImageCenterY = 160;
			demo.PolygonCenterX = 150;
			demo.PolygonCenterY = 140;

			await AggReference.Check(Render(demo), "image_transforms_320x300_example2_turned_moved");
		}

		[Test]
		public async Task DraggingTheImageCenterMovesIt()
		{
			var demo = new ImageTransformsDemo();
			int x = (int)demo.ImageCenterX;
			int y = (int)demo.ImageCenterY;

			demo.OnMouseDown(x, y, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(x + 30, y + 20, AggInputFlags.MouseLeft);
			demo.OnMouseUp(x + 30, y + 20, AggInputFlags.MouseLeft, AggInputFlags.None);

			await Assert.That(demo.ImageCenterX).IsEqualTo(x + 30.0);
			await Assert.That(demo.ImageCenterY).IsEqualTo(y + 20.0);
			await Assert.That(demo.PolygonCenterX).IsEqualTo(160.0);
		}

		private static ImageBuffer Render(ImageTransformsDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
