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
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using TUnit.Core;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// flash_rasterizer.cpp's port against C++ AGG (demo_flash_rasterizer.cpp).
	public class FlashRasterizerDemoTests
	{
		/// <summary>The default frame: shapes.txt's first shape fitted to the window, filled by
		/// render_scanlines_compound.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new FlashRasterizerDemo()), "flash_rasterizer_655x520");
		}

		/// <summary>The sixth shape, zoomed in four steps and turned three about (300, 250), so it runs off the
		/// window and is cut at its edges - where C++'s double-precision clipper (rasterizer_sl_clip_dbl) and the
		/// integer one differ.</summary>
		[Test]
		public async Task ZoomedRotatedSixthShapeMatchesCppAgg()
		{
			var demo = new FlashRasterizerDemo();
			for (int i = 0; i < 5; i++)
			{
				demo.NextShape();
			}

			demo.View = ZoomedRotated(demo.View);

			await AggReference.Check(Render(demo), "flash_rasterizer_655x520_shape5_zoomed_rotated");
		}

		/// <summary>Four + keys then three right-arrow keys with the mouse at (300, 250), as the golden's params.</summary>
		internal static Affine ZoomedRotated(Affine view)
		{
			for (int i = 0; i < 4; i++)
			{
				view = FlashDrawing.ApplyViewKey(Keys.Add, 300, 250, view).Value;
			}

			for (int i = 0; i < 3; i++)
			{
				view = FlashDrawing.ApplyViewKey(Keys.Right, 300, 250, view).Value;
			}

			return view;
		}

		private static ImageBuffer Render(AggDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
