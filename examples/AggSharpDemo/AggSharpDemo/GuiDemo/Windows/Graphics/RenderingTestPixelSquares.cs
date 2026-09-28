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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's PixelTestSquares or PixelTestStrokes (rendering_test.rs), in physical pixels and the text colour:
	/// filled squares 1 to 10 pixels big and one pixel apart, top-aligned; or, with <see cref="Outlined"/>, three
	/// rows of 1 to 10 pixel holes ringed by 1, 2 and 3 pixel borders. Rings are four filled bars so their
	/// corners stay pixel exact.
	/// </summary>
	public class RenderingTestPixelSquares : GuiWidget
	{
		private const int Squares = 10;

		private readonly DemoTheme demoTheme;

		public RenderingTestPixelSquares(DemoTheme demoTheme, bool outlined)
			: base(outlined ? OutlinedWidth() : 65, outlined ? OutlinedHeight() : Squares + 4)
		{
			this.demoTheme = demoTheme;
			this.Outlined = outlined;
		}

		/// <summary>True for the stroke rings, false for the filled squares.</summary>
		public bool Outlined { get; }

		public override void OnDraw(Graphics2D graphics2D)
		{
			Vector2 snap = RenderingTestPixelLines.PixelSnap(graphics2D);
			Color color = this.demoTheme.Palette.TextColor;
			double top = snap.Y + this.Height;
			if (!this.Outlined)
			{
				// Two pixels below the text above; each square hangs down from the shared top edge.
				double x = snap.X;
				for (int size = 1; size <= Squares; size++)
				{
					graphics2D.FillRectangle(x, top - 2 - size, x + size, top - 2, color);
					x += size + 1;
				}
			}
			else
			{
				// Thinnest ring on top.
				double rowTop = top;
				for (int t = 1; t <= 3; t++)
				{
					double holeTop = rowTop - 1 - t;
					double x = snap.X + t;
					for (int s = 1; s <= Squares; s++)
					{
						double holeBottom = holeTop - s;
						graphics2D.FillRectangle(x - t, holeTop, x + s + t, holeTop + t, color);
						graphics2D.FillRectangle(x - t, holeBottom - t, x + s + t, holeBottom, color);
						graphics2D.FillRectangle(x - t, holeBottom, x, holeTop, color);
						graphics2D.FillRectangle(x + s, holeBottom, x + s + t, holeTop, color);
						x += s + (t * 2) + 1;
					}

					rowTop -= Squares + (t * 2) + 2;
				}
			}

			base.OnDraw(graphics2D);
		}

		private static int OutlinedWidth()
		{
			int width = 4;
			for (int s = 1; s <= Squares; s++)
			{
				width += s + 7;
			}

			return width;
		}

		private static int OutlinedHeight()
		{
			int height = 0;
			for (int t = 1; t <= 3; t++)
			{
				height += Squares + (t * 2) + 2;
			}

			return height;
		}
	}
}
