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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Threading.Tasks;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// <see cref="TypeFacePrinter.SnapBaselinesToWholePixels"/> on the WebGPU path: a label whose baseline falls
	/// inside a device pixel draws its bottom edge as one solid row with snapping on and only partly covers that
	/// row with snapping off - on the GPU exactly as on the CPU, straight to the frame and through a retained GPU
	/// backbuffer, at 1x and at fractional display scales.
	/// </summary>
	/// <remarks>Snapping, LCD and <see cref="GuiWidget.DeviceScale"/> are process wide, so this is
	/// <c>[NotInParallel]</c> and restores them.</remarks>
	[NotInParallel]
	public class GpuTextBaselineSnapTests
	{
		private const int FrameWidth = 200;
		private const int FrameHeight = 90;

		private static GuiWidget BuildLabel(bool doubleBuffer)
		{
			var root = new GuiWidget(FrameWidth, FrameHeight);
			// A label's origin is its baseline. The holder's fraction rides into a backbuffer's composite and the
			// label's into its transform; together they put the baseline at device y 12.4, inside a pixel.
			var holder = new GuiWidget(FrameWidth - 10, FrameHeight - 10) { DoubleBuffer = doubleBuffer, Position = new Vector2(3, 2.3) };
			holder.AddChild(new TextWidget("HHH", pointSize: 12, textColor: Color.Black) { Position = new Vector2(6, 10.1) });
			root.AddChild(holder);
			return root;
		}

		private static async Task<ImageBuffer> DrawOnGpuAsync(GuiWidget root)
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
			var frame = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			root.OnDraw(frame);
			var image = await capture.CaptureAsync();
			await Assert.That(capture.Device.LastUncapturedError).IsNull();
			return image;
		}

		private static ImageBuffer DrawOnCpu(GuiWidget root)
		{
			var image = new ImageBuffer(FrameWidth, FrameHeight);
			Graphics2D graphics2D = image.NewGraphics2D();
			graphics2D.Clear(Color.White);
			root.OnDraw(graphics2D);
			return image;
		}

		private static int RowInk(ImageBuffer image, int y)
		{
			int ink = 0;
			for (int x = 0; x < image.Width; x++)
			{
				ink += 255 - image.GetPixel(x, y).green;
			}

			return ink;
		}

		/// <summary>
		/// The bottom inked row's ink over the row above it. "H" is upright stems down to the baseline, so a
		/// baseline on a pixel edge gives a bottom row as dark as the one above (about 1), and a baseline inside a
		/// pixel only partly covers its bottom row.
		/// </summary>
		private static double BottomRowCoverage(ImageBuffer image)
		{
			for (int y = 0; y < image.Height - 1; y++)
			{
				if (RowInk(image, y) > 0)
				{
					return RowInk(image, y) / (double)Math.Max(1, RowInk(image, y + 1));
				}
			}

			return 0;
		}

		private static async Task AssertSameImage(ImageBuffer expected, ImageBuffer actual, string what)
		{
			int worst = 0;
			for (int y = 0; y < expected.Height; y++)
			{
				for (int x = 0; x < expected.Width; x++)
				{
					Color a = expected.GetPixel(x, y);
					Color b = actual.GetPixel(x, y);
					worst = Math.Max(worst, Math.Max(Math.Abs(a.red - b.red), Math.Max(Math.Abs(a.green - b.green), Math.Abs(a.blue - b.blue))));
				}
			}

			await Assert.That(worst).IsLessThanOrEqualTo(2).Because($"{what}: worst channel delta {worst}");
		}

		[Test]
		[Arguments(1.0, false)]
		[Arguments(1.0, true)]
		[Arguments(1.5, false)]
		[Arguments(1.5, true)]
		[Arguments(3.5, true)]
		public async Task SnappingPutsTheGpuBaselineOnAWholeDevicePixel(double displayScale, bool doubleBuffer)
		{
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			bool wasLcd = LcdRenderSettings.Enabled;
			double wasScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = displayScale;
				LcdRenderSettings.Enabled = false;

				// One tree drawn both ways, so a label already on screen has to follow the toggle as well.
				GuiWidget root = BuildLabel(doubleBuffer);
				TypeFacePrinter.SnapBaselinesToWholePixels = true;
				ImageBuffer snappedGpu = await DrawOnGpuAsync(root);
				ImageBuffer snappedCpu = DrawOnCpu(root);

				TypeFacePrinter.SnapBaselinesToWholePixels = false;
				ImageBuffer unsnappedGpu = await DrawOnGpuAsync(root);
				ImageBuffer unsnappedCpu = DrawOnCpu(root);

				double snapped = BottomRowCoverage(snappedGpu);
				double unsnapped = BottomRowCoverage(unsnappedGpu);
				await Assert.That(snapped).IsGreaterThan(.9)
					.Because($"with snapping on the baseline sits on a pixel edge, so the stems' bottom row is solid (was {snapped:0.00})");
				await Assert.That(unsnapped).IsLessThan(.8)
					.Because($"with snapping off the baseline is inside a pixel, so the bottom row is only partly covered (was {unsnapped:0.00})");

				await AssertSameImage(snappedCpu, snappedGpu, "the GPU snaps the baseline as the CPU does");
				await AssertSameImage(unsnappedCpu, unsnappedGpu, "the GPU leaves an unsnapped baseline where the CPU does");
			}
			finally
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
				LcdRenderSettings.Enabled = wasLcd;
				GuiWidget.DeviceScale = wasScale;
			}
		}
	}
}
