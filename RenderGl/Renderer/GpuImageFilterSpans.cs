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
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// The spans software would hand span_interpolator_linear, so <c>GpuImageFilter.wgsl</c> can step its integer
	/// subpixel dda as software does in place of mapping each pixel centre in floats.
	/// </summary>
	/// <remarks>
	/// span_interpolator_linear maps only a span's two ends (rounded to 1/256) and steps a dda2_line_interpolator
	/// between them, so a pixel's subpixel coordinate depends on where its span starts and how long it is. Those
	/// are the scanline spans of the path as ImageGraphics2D rasterizes it, so the path is rasterized here too; the
	/// ends are mapped in doubles, as software maps them.
	/// </remarks>
	internal static class GpuImageFilterSpans
	{
		/// <summary>The ints ahead of the row table: enabled, first row, row count, the device-to-screen x and y offsets.</summary>
		public const int HeaderInts = 8;

		/// <summary>The ints in one span: start x, length, then x1, x2, y1, y2 in image subpixels.</summary>
		private const int SpanInts = 6;

		/// <summary>
		/// The shader's span table: a header, a (first span, span count) pair for each screen row, then the spans.
		/// Only the header, disabled, when the pixels are not software's (a scaled or rotated device, a scaled layer)
		/// or software would not use span_interpolator_linear over an affine (resample, perspective, bilinear).
		/// </summary>
		public static int[] Build(IVertexSource path, Affine transform, Affine deviceToScreen, ImageFilterFill fill, int sourceScale)
		{
			var disabled = new int[HeaderInts];
			bool unitDevice = deviceToScreen.sx == 1 && deviceToScreen.sy == -1 && deviceToScreen.shx == 0 && deviceToScreen.shy == 0
				&& deviceToScreen.tx == Math.Floor(deviceToScreen.tx) && deviceToScreen.ty == Math.Floor(deviceToScreen.ty);
			if (!unitDevice || sourceScale != 1 || fill.Kind == ImageFilterKind.Resample || !fill.IsAffine)
			{
				return disabled;
			}

			var rasterizer = new ScanlineRasterizer();
			rasterizer.add_path(new VertexSourceApplyTransform(path, transform));
			if (!rasterizer.rewind_scanlines())
			{
				return disabled;
			}

			Perspective screenToImage = fill.ScreenToImage();
			var affine = new Affine(screenToImage.sx, screenToImage.shy, screenToImage.shx, screenToImage.sy, screenToImage.tx, screenToImage.ty);

			var scanline = new scanline_unpacked_8();
			scanline.reset(rasterizer.min_x(), rasterizer.max_x());
			int firstRow = rasterizer.min_y();
			int rowCount = rasterizer.max_y() - firstRow + 1;
			var rows = new int[rowCount * 2];
			var spans = new List<int>();
			while (rasterizer.sweep_scanline(scanline))
			{
				int y = scanline.y();
				int row = (y - firstRow) * 2;
				rows[row] = spans.Count / SpanInts;
				int count = scanline.num_spans();
				rows[row + 1] = count;
				ScanlineSpan span = scanline.begin();
				for (; ; )
				{
					int len = Math.Abs(span.len);

					// span_interpolator_linear.begin(x + filter_dx_dbl, y + filter_dy_dbl, len): 0.5 for every generator.
					double x1 = span.x + 0.5, y1 = y + 0.5, x2 = span.x + len + 0.5, y2 = y + 0.5;
					affine.Transform(ref x1, ref y1);
					affine.Transform(ref x2, ref y2);
					spans.Add(span.x);
					spans.Add(len);
					spans.Add(Util.iround(x1 * 256));
					spans.Add(Util.iround(x2 * 256));
					spans.Add(Util.iround(y1 * 256));
					spans.Add(Util.iround(y2 * 256));
					if (--count == 0)
					{
						break;
					}

					span = scanline.GetNextScanlineSpan();
				}
			}

			var data = new int[HeaderInts + rows.Length + spans.Count];
			data[0] = 1;
			data[1] = firstRow;
			data[2] = rowCount;
			data[3] = (int)deviceToScreen.tx;
			data[4] = (int)deviceToScreen.ty;
			// A row's first span is counted from the end of the row table.
			rows.CopyTo(data, HeaderInts);
			spans.CopyTo(data, HeaderInts + rows.Length);
			return data;
		}
	}
}
