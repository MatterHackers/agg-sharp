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

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// C++ <c>renderer_base</c> over an rgba32 <see cref="IPixelFormatFloat"/>: the span calls clipped to the whole
	/// image, plus C++ <c>render_scanlines_aa_solid</c> and <c>render_scanlines_aa</c> into it. Kept apart from
	/// <see cref="ScanlineRenderer"/>'s float path, which blends through <see cref="IImageFloat"/> and folds the
	/// cover into alpha up front: this one hands C++'s covers through untouched, so results are bit-identical.
	/// </summary>
	public class RendererBaseFloat
	{
		private readonly IPixelFormatFloat pixelFormat;

		private ColorF[] spanColors = new ColorF[256];

		public RendererBaseFloat(IPixelFormatFloat pixelFormat)
		{
			this.pixelFormat = pixelFormat;
		}

		public IPixelFormatFloat PixelFormat => pixelFormat;

		private int XMax => pixelFormat.Width - 1;

		private int YMax => pixelFormat.Height - 1;

		/// <summary>C++ <c>renderer_base::blend_hline(x1, y, x2, c, cover)</c>: the inclusive run x1 to x2, in either order.</summary>
		public void BlendHline(int x1, int y, int x2, ColorF c, int cover)
		{
			if (x1 > x2)
			{
				(x1, x2) = (x2, x1);
			}

			if (y > YMax || y < 0 || x1 > XMax || x2 < 0)
			{
				return;
			}

			if (x1 < 0)
			{
				x1 = 0;
			}

			if (x2 > XMax)
			{
				x2 = XMax;
			}

			pixelFormat.BlendHline(x1, y, x2 - x1 + 1, c, cover);
		}

		/// <summary>C++ <c>renderer_base::blend_solid_hspan</c>.</summary>
		public void BlendSolidHspan(int x, int y, int len, ColorF c, byte[] covers, int coversIndex)
		{
			if (y > YMax || y < 0)
			{
				return;
			}

			if (x < 0)
			{
				len += x;
				if (len <= 0)
				{
					return;
				}

				coversIndex -= x;
				x = 0;
			}

			// C++ tests x + len > xmax, not >= - harmless, since the new length is the same either way.
			if (x + len > XMax)
			{
				len = XMax - x + 1;
				if (len <= 0)
				{
					return;
				}
			}

			pixelFormat.BlendSolidHspan(x, y, len, c, covers, coversIndex);
		}

		/// <summary>C++ <c>renderer_base::blend_color_hspan</c>.</summary>
		public void BlendColorHspan(int x, int y, int len, ColorF[] colors, int colorsIndex, byte[] covers, int coversIndex, int cover)
		{
			if (y > YMax || y < 0)
			{
				return;
			}

			if (x < 0)
			{
				len += x;
				if (len <= 0)
				{
					return;
				}

				coversIndex -= x;
				colorsIndex -= x;
				x = 0;
			}

			if (x + len > XMax)
			{
				len = XMax - x + 1;
				if (len <= 0)
				{
					return;
				}
			}

			pixelFormat.BlendColorHspan(x, y, len, colors, colorsIndex, covers, coversIndex, cover);
		}

		/// <summary>C++ <c>render_scanlines_aa_solid(ras, sl, ren, color)</c>.</summary>
		public void RenderScanlinesAaSolid(IRasterizer rasterizer, IScanlineCache scanline, ColorF color)
		{
			if (!rasterizer.rewind_scanlines())
			{
				return;
			}

			scanline.reset(rasterizer.min_x(), rasterizer.max_x());
			while (rasterizer.sweep_scanline(scanline))
			{
				int y = scanline.y();
				int numSpans = scanline.num_spans();
				byte[] covers = scanline.GetCovers();
				ScanlineSpan span = scanline.begin();
				for (; ; )
				{
					int x = span.x;
					if (span.len > 0)
					{
						BlendSolidHspan(x, y, span.len, color, covers, span.cover_index);
					}
					else
					{
						BlendHline(x, y, x - span.len - 1, color, covers[span.cover_index]);
					}

					if (--numSpans == 0)
					{
						break;
					}

					span = scanline.GetNextScanlineSpan();
				}
			}
		}

		/// <summary>C++ <c>render_scanlines_aa(ras, sl, ren, alloc, span_gen)</c>.</summary>
		public void RenderScanlinesAa(IRasterizer rasterizer, IScanlineCache scanline, ISpanGeneratorFloat spanGenerator)
		{
			if (!rasterizer.rewind_scanlines())
			{
				return;
			}

			scanline.reset(rasterizer.min_x(), rasterizer.max_x());
			spanGenerator.prepare();
			while (rasterizer.sweep_scanline(scanline))
			{
				int y = scanline.y();
				int numSpans = scanline.num_spans();
				byte[] covers = scanline.GetCovers();
				ScanlineSpan span = scanline.begin();
				for (; ; )
				{
					int x = span.x;
					int len = span.len < 0 ? -span.len : span.len;
					if (spanColors.Length < len)
					{
						spanColors = new ColorF[len];
					}

					spanGenerator.generate(spanColors, 0, x, y, len);
					BlendColorHspan(x, y, len, spanColors, 0, span.len < 0 ? null : covers, span.cover_index, covers[span.cover_index]);

					if (--numSpans == 0)
					{
						break;
					}

					span = scanline.GetNextScanlineSpan();
				}
			}
		}
	}
}
