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

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// The GPU stand-in for renderer_scanline_aa_solid (and renderer_scanline_bin_solid): each run of equal
	/// cover as a one-pixel-high rectangle, its color's alpha scaled by the cover. A scanline without covers
	/// (scanline_bin) is filled at full cover.
	/// </summary>
	internal class RectangleScanlineSink : IScanlineSink
	{
		private readonly Graphics2D graphics;

		private readonly Color color;

		public RectangleScanlineSink(Graphics2D graphics, Color color)
		{
			this.graphics = graphics;
			this.color = color;
		}

		public void prepare()
		{
		}

		public void render(IScanlineCache scanline)
		{
			int y = scanline.y();
			byte[] covers = scanline.GetCovers();
			ScanlineSpan span = scanline.begin();
			for (int i = 0; i < scanline.num_spans(); i++)
			{
				if (i > 0)
				{
					span = scanline.GetNextScanlineSpan();
				}

				if (covers == null)
				{
					this.Fill(span.x, span.x + Math.Abs(span.len), y, 255);
					continue;
				}

				if (span.len < 0)
				{
					this.Fill(span.x, span.x - span.len, y, covers[span.cover_index]);
					continue;
				}

				int runStart = 0;
				for (int x = 1; x <= span.len; x++)
				{
					if (x == span.len || covers[span.cover_index + x] != covers[span.cover_index + runStart])
					{
						this.Fill(span.x + runStart, span.x + x, y, covers[span.cover_index + runStart]);
						runStart = x;
					}
				}
			}
		}

		private void Fill(int x1, int x2, int y, int cover)
		{
			this.graphics.FillRectangle(x1, y, x2, y + 1, new Color(this.color, this.color.alpha * cover / 255));
		}
	}
}
