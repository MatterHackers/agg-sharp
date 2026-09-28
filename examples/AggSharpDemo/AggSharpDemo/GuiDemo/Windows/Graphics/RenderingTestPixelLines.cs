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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's PixelTestLines / PixelTestLinesBitmap (rendering_test.rs): a block of alternating one-pixel white
	/// and black columns beside a block of alternating rows, in physical pixels. With <see cref="ThroughBitmap"/>
	/// the blocks are rasterised into an ImageBuffer first and that is drawn, so the two paths can be compared
	/// pixel for pixel.
	/// </summary>
	public class RenderingTestPixelLines : GuiWidget
	{
		/// <summary>Columns (and rows) in each block.</summary>
		public const int Count = 96;

		private const int Gap = 8;

		private ImageBuffer verticalBitmap;

		private ImageBuffer horizontalBitmap;

		public RenderingTestPixelLines(bool throughBitmap)
			: base(Count + Gap + Count, Count)
		{
			this.ThroughBitmap = throughBitmap;
		}

		/// <summary>True to draw the stripes through a cached bitmap rather than directly.</summary>
		public bool ThroughBitmap { get; }

		/// <summary>
		/// The offset that moves this widget's origin onto the physical pixel grid (agg-gui's snap_to_pixel), so a
		/// one-pixel rectangle covers exactly one pixel wherever the layout put the widget.
		/// </summary>
		public static Vector2 PixelSnap(Graphics2D graphics2D)
		{
			Affine transform = graphics2D.GetTransform();
			return new Vector2(Math.Round(transform.tx) - transform.tx, Math.Round(transform.ty) - transform.ty);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			Vector2 snap = PixelSnap(graphics2D);
			if (this.ThroughBitmap)
			{
				this.verticalBitmap ??= Rasterize(vertical: true);
				this.horizontalBitmap ??= Rasterize(vertical: false);
				graphics2D.Render(this.verticalBitmap, snap.X, snap.Y);
				graphics2D.Render(this.horizontalBitmap, snap.X + Count + Gap, snap.Y);
			}
			else
			{
				DrawStripes(graphics2D, snap.X, snap.Y, vertical: true);
				DrawStripes(graphics2D, snap.X + Count + Gap, snap.Y, vertical: false);
			}

			base.OnDraw(graphics2D);
		}

		private static ImageBuffer Rasterize(bool vertical)
		{
			var image = new ImageBuffer(Count, Count);
			DrawStripes(image.NewGraphics2D(), 0, 0, vertical);
			return image;
		}

		// White first: the leftmost column and, Y-up, the bottom row are white, as in egui.
		private static void DrawStripes(Graphics2D graphics2D, double x, double y, bool vertical)
		{
			for (int i = 0; i < Count; i += 2)
			{
				if (vertical)
				{
					graphics2D.FillRectangle(x + i, y, x + i + 1, y + Count, Color.White);
					graphics2D.FillRectangle(x + i + 1, y, x + i + 2, y + Count, Color.Black);
				}
				else
				{
					graphics2D.FillRectangle(x, y + i, x + Count, y + i + 1, Color.White);
					graphics2D.FillRectangle(x, y + i + 1, x + Count, y + i + 2, Color.Black);
				}
			}
		}
	}
}
