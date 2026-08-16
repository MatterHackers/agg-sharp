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
using MatterHackers.RenderGl;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// Goldens for text on the classic D3D11 path, both the ordinary anti-aliased fill and the three-pass
	/// colour-masked LCD subpixel composite.
	/// </summary>
	/// <remarks>
	/// This is the port's highest-visibility risk, so the coverage here is deliberately wider than the other
	/// suites: sizes from 7 to 40 point, sub-pixel placements at every quarter pixel, light-on-dark as well
	/// as dark-on-light, and text over an existing fill. The LCD path in particular becomes three cached
	/// pipeline permutations under WebGPU (colour write masks stop being dynamic state), and it deliberately
	/// never writes destination alpha - a difference these goldens will catch as a whole-image mismatch if
	/// the new surface composites alpha differently.
	/// </remarks>
	[NotInParallel]
	public class GoldenTextTests
	{
		private const string Sample = "Handgloves 0123 fi.,;";

		private static readonly double[] Sizes = { 7, 8, 9, 10, 11, 12, 14, 18, 24, 32, 40 };

		private static async Task Check(string goldenName, bool lcd, ColorF background, Action<Graphics2D> draw)
		{
			bool wasLcdEnabled = LcdRenderSettings.Enabled;
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			try
			{
				LcdRenderSettings.Enabled = lcd;
				TypeFacePrinter.SnapBaselinesToWholePixels = true;

				using var capture = D3D11OffscreenCapture.Create();
				var graphics = capture.BeginWidgetFrame(background);

				draw(graphics);

				await GoldenImage.Check(capture.Capture(), goldenName);
			}
			finally
			{
				LcdRenderSettings.Enabled = wasLcdEnabled;
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
			}
		}

		/// <summary>Draws the sample string once per size, bottom to top, so one image covers the whole
		/// range of glyph rasterization the widget stack asks for.</summary>
		private static void DrawSizeLadder(Graphics2D graphics, Color color)
		{
			double y = 8;
			foreach (double size in Sizes)
			{
				graphics.DrawString($"{size:0} {Sample}", 8, y, size, color: color);
				y += size + 6;
			}
		}

		/// <summary>Draws the same short string at every quarter-pixel x and y offset, which is where the
		/// LCD mask cache's sub-pixel phase handling and the AA path's coverage differ most.</summary>
		private static void DrawSubPixelGrid(Graphics2D graphics, Color color, double pointSize)
		{
			for (int row = 0; row < 4; row++)
			{
				for (int column = 0; column < 4; column++)
				{
					graphics.DrawString(
						"Wave iIl1",
						12 + (column * 124) + (column * 0.25),
						40 + (row * 80) + (row * 0.25),
						pointSize,
						color: color);
				}
			}
		}

		[Test]
		public async Task AntiAliasedSizeLadder()
		{
			await Check("Text.Aa.SizeLadder", lcd: false, new ColorF(1, 1, 1, 1), graphics =>
				DrawSizeLadder(graphics, Color.Black));
		}

		[Test]
		public async Task AntiAliasedLightOnDark()
		{
			await Check("Text.Aa.LightOnDark", lcd: false, new ColorF(0.09f, 0.11f, 0.16f, 1), graphics =>
				DrawSizeLadder(graphics, new Color(238, 238, 230)));
		}

		[Test]
		public async Task AntiAliasedSubPixelPlacement()
		{
			await Check("Text.Aa.SubPixel", lcd: false, new ColorF(1, 1, 1, 1), graphics =>
			{
				DrawSubPixelGrid(graphics, Color.Black, 12);
				DrawSubPixelGrid(graphics, new Color(200, 40, 40), 9);
			});
		}

		[Test]
		public async Task LcdSizeLadder()
		{
			await Check("Text.Lcd.SizeLadder", lcd: true, new ColorF(1, 1, 1, 1), graphics =>
				DrawSizeLadder(graphics, Color.Black));
		}

		[Test]
		public async Task LcdLightOnDark()
		{
			await Check("Text.Lcd.LightOnDark", lcd: true, new ColorF(0.09f, 0.11f, 0.16f, 1), graphics =>
				DrawSizeLadder(graphics, new Color(238, 238, 230)));
		}

		[Test]
		public async Task LcdSubPixelPlacement()
		{
			await Check("Text.Lcd.SubPixel", lcd: true, new ColorF(1, 1, 1, 1), graphics =>
			{
				DrawSubPixelGrid(graphics, Color.Black, 12);
				DrawSubPixelGrid(graphics, new Color(200, 40, 40), 9);
			});
		}

		/// <summary>
		/// LCD text drawn over shapes rather than over the cleared background, so the composite is blending
		/// against colours instead of flat white - the case where a wrong blend factor is invisible in the
		/// ladder goldens.
		/// </summary>
		[Test]
		public async Task LcdOverFilledShapes()
		{
			await Check("Text.Lcd.OverShapes", lcd: true, new ColorF(1, 1, 1, 1), graphics =>
			{
				graphics.FillRectangle(0, 0, 512, 128, new Color(220, 60, 40));
				graphics.FillRectangle(0, 128, 512, 256, new Color(40, 110, 200));
				graphics.FillRectangle(0, 256, 512, 384, new Color(245, 245, 245));

				graphics.DrawString(Sample, 10, 40, 18, color: Color.White);
				graphics.DrawString(Sample, 10, 80, 12, color: Color.Black);
				graphics.DrawString(Sample, 10, 168, 18, color: Color.White);
				graphics.DrawString(Sample, 10, 208, 12, color: new Color(255, 240, 120));
				graphics.DrawString(Sample, 10, 296, 18, color: Color.Black);
				graphics.DrawString(Sample, 10, 336, 12, color: new Color(60, 60, 60));
			});
		}

		/// <summary>
		/// Both paths in one frame. The LCD composite leaves the colour write mask set until its
		/// <c>finally</c> restores it; if that restore is ever lost in the port, the ordinary text drawn
		/// after it here turns monochrome and this golden is what says so.
		/// </summary>
		[Test]
		public async Task LcdFollowedByOrdinaryDrawing()
		{
			await Check("Text.Lcd.ThenOrdinary", lcd: true, new ColorF(1, 1, 1, 1), graphics =>
			{
				graphics.DrawString(Sample, 10, 340, 20, color: Color.Black);

				// Full-colour geometry after the three masked passes: any leaked mask shows up immediately.
				graphics.FillRectangle(20, 250, 240, 320, new Color(220, 60, 40));
				graphics.FillRectangle(270, 250, 490, 320, new Color(40, 110, 200));
				graphics.Render(new VertexSource.Ellipse(256, 150, 90, 60), new Color(30, 160, 90));

				graphics.DrawString(Sample, 10, 40, 20, color: new Color(20, 20, 20));
			});
		}
	}
}
