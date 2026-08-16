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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.LcdCoverage;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// The cross-backend half of the text suite: the same scenes as <see cref="GoldenTextTests"/>, rendered
	/// on wgpu and compared against the classic path's PNGs. The LCD cases matter most - colour write masks
	/// are immutable pipeline state in WebGPU, so the three-pass composite becomes three cached pipeline
	/// permutations, and these goldens are what says whether the permutation is right.
	/// </summary>
	[NotInParallel]
	public class GoldenTextOnWebGpuTests
	{
		private static async Task Check(string goldenName, bool lcd, ColorF background, Action<Graphics2D> draw)
		{
			bool wasLcdEnabled = LcdRenderSettings.Enabled;
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			try
			{
				LcdRenderSettings.Enabled = lcd;
				TypeFacePrinter.SnapBaselinesToWholePixels = true;

				using var capture = WebGpuOffscreenCapture.Create();
				var graphics = capture.BeginWidgetFrame(background);

				draw(graphics);

				var rendered = await capture.CaptureAsync();

				await Assert.That(capture.Device.LastUncapturedError).IsNull();

				await GoldenImage.Check(rendered, goldenName);
			}
			finally
			{
				LcdRenderSettings.Enabled = wasLcdEnabled;
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
			}
		}

		[Test]
		public async Task AntiAliasedSizeLadder()
			=> await Check("Text.Aa.SizeLadder", lcd: false, new ColorF(1, 1, 1, 1), GoldenTextScenes.SizeLadderDarkOnLight);

		[Test]
		public async Task AntiAliasedLightOnDark()
			=> await Check("Text.Aa.LightOnDark", lcd: false, GoldenTextScenes.DarkBackground, GoldenTextScenes.SizeLadderLightOnDark);

		[Test]
		public async Task AntiAliasedSubPixelPlacement()
			=> await Check("Text.Aa.SubPixel", lcd: false, new ColorF(1, 1, 1, 1), GoldenTextScenes.SubPixelPlacement);

		[Test]
		public async Task LcdSizeLadder()
			=> await Check("Text.Lcd.SizeLadder", lcd: true, new ColorF(1, 1, 1, 1), GoldenTextScenes.SizeLadderDarkOnLight);

		[Test]
		public async Task LcdLightOnDark()
			=> await Check("Text.Lcd.LightOnDark", lcd: true, GoldenTextScenes.DarkBackground, GoldenTextScenes.SizeLadderLightOnDark);

		[Test]
		public async Task LcdSubPixelPlacement()
			=> await Check("Text.Lcd.SubPixel", lcd: true, new ColorF(1, 1, 1, 1), GoldenTextScenes.SubPixelPlacement);

		[Test]
		public async Task LcdOverFilledShapes()
			=> await Check("Text.Lcd.OverShapes", lcd: true, new ColorF(1, 1, 1, 1), GoldenTextScenes.OverFilledShapes);

		[Test]
		public async Task LcdFollowedByOrdinaryDrawing()
			=> await Check("Text.Lcd.ThenOrdinary", lcd: true, new ColorF(1, 1, 1, 1), GoldenTextScenes.LcdThenOrdinary);
	}
}
