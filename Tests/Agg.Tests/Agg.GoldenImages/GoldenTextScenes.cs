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

using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// The drawing for the text golden suite, shared by the classic D3D11 suite that captured the goldens
	/// (<see cref="GoldenTextTests"/>) and the wgpu suite held to them
	/// (<see cref="GoldenTextOnWebGpuTests"/>).
	/// </summary>
	public static class GoldenTextScenes
	{
		/// <summary>The string every ladder draws: ascenders, descenders, digits, a ligature pair and
		/// punctuation, which is where hinting and coverage differences show first.</summary>
		public const string Sample = "Handgloves 0123 fi.,;";

		private static readonly double[] Sizes = { 7, 8, 9, 10, 11, 12, 14, 18, 24, 32, 40 };

		/// <summary>The dark background the light-on-dark cases clear to.</summary>
		public static ColorF DarkBackground => new ColorF(0.09f, 0.11f, 0.16f, 1);

		/// <summary>Draws the sample string once per size, bottom to top, so one image covers the whole
		/// range of glyph rasterization the widget stack asks for.</summary>
		public static void DrawSizeLadder(Graphics2D graphics, Color color)
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
		public static void DrawSubPixelGrid(Graphics2D graphics, Color color, double pointSize)
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

		/// <summary>The size ladder in black on the cleared background.</summary>
		public static void SizeLadderDarkOnLight(Graphics2D graphics) => DrawSizeLadder(graphics, Color.Black);

		/// <summary>The size ladder in near-white, for a dark-cleared frame.</summary>
		public static void SizeLadderLightOnDark(Graphics2D graphics)
			=> DrawSizeLadder(graphics, new Color(238, 238, 230));

		/// <summary>Two sub-pixel grids at different sizes and colours.</summary>
		public static void SubPixelPlacement(Graphics2D graphics)
		{
			DrawSubPixelGrid(graphics, Color.Black, 12);
			DrawSubPixelGrid(graphics, new Color(200, 40, 40), 9);
		}

		/// <summary>
		/// Text drawn over shapes rather than over the cleared background, so the composite is blending
		/// against colours instead of flat white - the case where a wrong blend factor is invisible in the
		/// ladder goldens.
		/// </summary>
		public static void OverFilledShapes(Graphics2D graphics)
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
		}

		/// <summary>
		/// Both paths in one frame. The LCD composite leaves the colour write mask set until its
		/// <c>finally</c> restores it; if that restore is ever lost in the port, the ordinary text drawn
		/// after it here turns monochrome and this golden is what says so.
		/// </summary>
		public static void LcdThenOrdinary(Graphics2D graphics)
		{
			graphics.DrawString(Sample, 10, 340, 20, color: Color.Black);

			// Full-colour geometry after the three masked passes: any leaked mask shows up immediately.
			graphics.FillRectangle(20, 250, 240, 320, new Color(220, 60, 40));
			graphics.FillRectangle(270, 250, 490, 320, new Color(40, 110, 200));
			graphics.Render(new Ellipse(256, 150, 90, 60), new Color(30, 160, 90));

			graphics.DrawString(Sample, 10, 40, 20, color: new Color(20, 20, 20));
		}
	}
}
