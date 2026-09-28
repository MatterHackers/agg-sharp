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
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	/// <summary>
	/// image_filters on the GPU filters each step's result again on the device (retained layers read through
	/// <see cref="IImageFilterGraphics"/>), so its artifacts build up as the software view's do.
	/// </summary>
	/// <remarks>
	/// The shader steps span_interpolator_linear's integer dda along software's spans, so away from the circle a step
	/// is software's to the unit; what differs is the circle's edge, the GPU's anti-aliasing, and each step filters
	/// that difference a filter's reach further in. So the margin, how far inside the edge the comparison starts,
	/// grows with the steps and the filter's reach, and the tolerance stays tight.
	/// Measured on Metal, the worst difference at each pixel's depth inside the edge: one step is exact from 3 in
	/// (6 for sinc), but kaiser; ten steps of bilinear, bicubic and hanning are within 1 from 5 in; ten of sinc
	/// within 3 from 17 in (normalized) or 12 in (not).
	/// </remarks>
	[NotInParallel]
	public class ImageFiltersDemoGpuTests
	{
		// The image's lower-left corner in the frame (Draw's copy_from).
		private const int ImageX = 110;
		private const int ImageY = 35;

		public static (int Filter, int Steps, bool Normalize, double Margin, int Tolerance)[] Cases() => new[]
		{
			// One turn: the step reads the first step's layer, flipped and premultiplied, so a wrong row order or
			// colour shows here at once. Sinc reaches 4 pixels, so the first step's circle edge reaches that far in.
			// Kaiser, 2 wide, runs the 2x2 generator on the GPU where C++ runs span_image_filter_rgb, which rounds
			// its sum differently: a unit everywhere.
			(1, 1, true, 3.0, 0),
			(2, 1, true, 3.0, 0),
			(5, 1, true, 3.0, 0),
			(8, 1, true, 3.0, 1),
			(14, 1, true, 6.0, 0),

			// Ten turns: the edge's difference is filtered ten times over, a filter's reach further in each time.
			(1, 10, true, 6.0, 1),
			(2, 10, true, 6.0, 1),
			(5, 10, true, 6.0, 1),
			(14, 10, true, 20.0, 3),

			// Normalize Filter off: sinc's weights don't sum to 1, which C++'s rgb generators turn into a darker
			// colour at full alpha. Were alpha the summed weight, as the rgba generators give, the white under the
			// layer would show through more each step (one step was 39 off, ten were 244).
			(14, 1, false, 6.0, 0),
			(14, 10, false, 16.0, 3),
		};

		[Test]
		[MethodDataSource(nameof(Cases))]
		public async Task GpuStepsMatchSoftware((int Filter, int Steps, bool Normalize, double Margin, int Tolerance) test)
		{
			var demo = new ImageFiltersDemo();
			demo.NormalizeCbox.Checked = test.Normalize;
			demo.FiltersRbox.CurrentItem = test.Filter;
			demo.OnCtrlChange();
			for (int i = 0; i < test.Steps; i++)
			{
				demo.SingleStepCbox.Checked = true;
				demo.OnCtrlChange();
			}

			ImageBuffer software = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, software);

			using var capture = WebGpuOffscreenCapture.Create(demo.Width, demo.Height);
			demo.Draw(capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1)));
			ImageBuffer gpu = await capture.CaptureAsync();

			var image = SpheresImage.Load();
			double radius = (Math.Min(image.Width, image.Height) / 2.0) - 4 - test.Margin;
			int compared = 0;
			int outside = 0;
			int worst = 0;
			string worstAt = null;
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					double dx = x + 0.5 - (image.Width / 2.0);
					double dy = y + 0.5 - (image.Height / 2.0);
					if ((dx * dx) + (dy * dy) > radius * radius)
					{
						continue;
					}

					Color g = gpu.GetPixel(ImageX + x, ImageY + y);
					Color s = software.GetPixel(ImageX + x, ImageY + y);
					int difference = Math.Max(Math.Max(Math.Abs(g.red - s.red), Math.Abs(g.green - s.green)), Math.Abs(g.blue - s.blue));
					compared++;
					if (difference > test.Tolerance)
					{
						outside++;
					}

					if (difference > worst)
					{
						worst = difference;
						worstAt = $"({x}, {y}): GPU {g} vs software {s}";
					}
				}
			}

			Console.WriteLine($"filter {test.Filter}, {test.Steps} steps, normalize {test.Normalize}: worst {worst} at {worstAt}, {outside} of {compared} over {test.Tolerance}");
			await Assert.That(outside).IsEqualTo(0).Because(worstAt ?? string.Empty);
		}
	}
}
