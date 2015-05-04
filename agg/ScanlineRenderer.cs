using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg
{
	public class ScanlineRenderer
	{
		private VectorPOD<RGBA_Bytes> tempSpanColors = new VectorPOD<RGBA_Bytes>();
		private VectorPOD<RGBA_Floats> tempSpanColorsFloats = new VectorPOD<RGBA_Floats>();

		public void RenderSolid(IImageByte destImage, IRasterizer rasterizer, IScanlineCache scanLine, RGBA_Bytes color)
		{
			if (rasterizer.rewind_scanlines())
			{
				scanLine.reset(rasterizer.min_x(), rasterizer.max_x());
				while (rasterizer.sweep_scanline(scanLine))
				{
					RenderSolidSingleScanLine(destImage, scanLine, color);
				}
			}
		}

		public void RenderSolid(IImageFloat destImage, IRasterizer rasterizer, IScanlineCache scanLine, RGBA_Floats color)
		{
			if (rasterizer.rewind_scanlines())
			{
				scanLine.reset(rasterizer.min_x(), rasterizer.max_x());
				while (rasterizer.sweep_scanline(scanLine))
				{
					RenderSolidSingleScanLine(destImage, scanLine, color);
				}
			}
		}

		protected virtual void RenderSolidSingleScanLine(IImageByte destImage, IScanlineCache scanLine, RGBA_Bytes color)
		{
			int y = scanLine.y();
			int num_spans = scanLine.num_spans();
			ScanlineSpan scanlineSpan = scanLine.begin();

			byte[] ManagedCoversArray = scanLine.GetCovers();
			for (; ; )
			{
				int x = scanlineSpan.x;
				if (scanlineSpan.len > 0)
				{
					destImage.blend_solid_hspan(x, y, scanlineSpan.len, color, ManagedCoversArray, scanlineSpan.cover_index);
				}
				else
				{
					int x2 = (x - (int)scanlineSpan.len - 1);
					destImage.blend_hline(x, y, x2, color, ManagedCoversArray[scanlineSpan.cover_index]);
				}
				if (--num_spans == 0) break;
				scanlineSpan = scanLine.GetNextScanlineSpan();
			}
		}

		private void RenderSolidSingleScanLine(IImageFloat destImage, IScanlineCache scanLine, RGBA_Floats color)
		{
			int y = scanLine.y();
			int num_spans = scanLine.num_spans();
			ScanlineSpan scanlineSpan = scanLine.begin();

			byte[] ManagedCoversArray = scanLine.GetCovers();
			for (; ; )
			{
				int x = scanlineSpan.x;
				if (scanlineSpan.len > 0)
				{
					destImage.blend_solid_hspan(x, y, scanlineSpan.len, color, ManagedCoversArray, scanlineSpan.cover_index);
				}
				else
				{
					int x2 = (x - (int)scanlineSpan.len - 1);
					destImage.blend_hline(x, y, x2, color, ManagedCoversArray[scanlineSpan.cover_index]);
				}
				if (--num_spans == 0) break;
				scanlineSpan = scanLine.GetNextScanlineSpan();
			}
		}

		public void RenderSolidAllPaths(IImageByte destImage,
			IRasterizer ras,
			IScanlineCache sl,
			IVertexSource vs,
			RGBA_Bytes[] color_storage,
			int[] path_id,
			int num_paths)
		{
			for (int i = 0; i < num_paths; i++)
			{
				ras.reset();

				ras.add_path(vs, path_id[i]);

				RenderSolid(destImage, ras, sl, color_storage[i]);
			}
		}

		private void GenerateAndRenderSingleScanline(IScanlineCache scanLineCache, IImageByte destImage, span_allocator alloc, ISpanGenerator span_gen)
		{
			int y = scanLineCache.y();
			int num_spans = scanLineCache.num_spans();
			ScanlineSpan scanlineSpan = scanLineCache.begin();

			byte[] ManagedCoversArray = scanLineCache.GetCovers();
			for (; ; )
			{
				int x = scanlineSpan.x;
				int len = scanlineSpan.len;
				if (len < 0) len = -len;

				if (tempSpanColors.Capacity() < len)
				{
					tempSpanColors.Capacity(len);
				}

				span_gen.generate(tempSpanColors.Array, 0, x, y, len);
				bool useFirstCoverForAll = scanlineSpan.len < 0;
				destImage.blend_color_hspan(x, y, len, tempSpanColors.Array, 0, ManagedCoversArray, scanlineSpan.cover_index, useFirstCoverForAll);

				if (--num_spans == 0) break;
				scanlineSpan = scanLineCache.GetNextScanlineSpan();
			}
		}

		private void GenerateAndRenderSingleScanline(IScanlineCache scanLineCache, IImageFloat destImageFloat, span_allocator alloc, ISpanGeneratorFloat span_gen)
		{
			int y = scanLineCache.y();
			int num_spans = scanLineCache.num_spans();
			ScanlineSpan scanlineSpan = scanLineCache.begin();

			byte[] ManagedCoversArray = scanLineCache.GetCovers();
			for (; ; )
			{
				int x = scanlineSpan.x;
				int len = scanlineSpan.len;
				if (len < 0) len = -len;

				if (tempSpanColorsFloats.Capacity() < len)
				{
					tempSpanColorsFloats.Capacity(len);
				}

				span_gen.generate(tempSpanColorsFloats.Array, 0, x, y, len);
				bool useFirstCoverForAll = scanlineSpan.len < 0;
				destImageFloat.blend_color_hspan(x, y, len, tempSpanColorsFloats.Array, 0, ManagedCoversArray, scanlineSpan.cover_index, useFirstCoverForAll);

				if (--num_spans == 0) break;
				scanlineSpan = scanLineCache.GetNextScanlineSpan();
			}
		}

		public void GenerateAndRender(IRasterizer rasterizer, IScanlineCache scanlineCache, IImageByte destImage, span_allocator spanAllocator, ISpanGenerator spanGenerator)
		{
			if (rasterizer.rewind_scanlines())
			{
				scanlineCache.reset(rasterizer.min_x(), rasterizer.max_x());
				spanGenerator.prepare();
				while (rasterizer.sweep_scanline(scanlineCache))
				{
					GenerateAndRenderSingleScanline(scanlineCache, destImage, spanAllocator, spanGenerator);
				}
			}
		}

		public void GenerateAndRender(IRasterizer rasterizer, IScanlineCache scanlineCache, IImageFloat destImage, span_allocator spanAllocator, ISpanGeneratorFloat spanGenerator)
		{
			if (rasterizer.rewind_scanlines())
			{
				scanlineCache.reset(rasterizer.min_x(), rasterizer.max_x());
				spanGenerator.prepare();
				while (rasterizer.sweep_scanline(scanlineCache))
				{
					GenerateAndRenderSingleScanline(scanlineCache, destImage, spanAllocator, spanGenerator);
				}
			}
		}

		public void RenderCompound(rasterizer_compound_aa ras, IScanlineCache sl_aa, IScanlineCache sl_bin, IImageByte imageFormat, span_allocator alloc, IStyleHandler sh)
		{
#if false
            unsafe
            {
                if (ras.rewind_scanlines())
                {
                    int min_x = ras.min_x();
                    int len = ras.max_x() - min_x + 2;
                    sl_aa.reset(min_x, ras.max_x());
                    sl_bin.reset(min_x, ras.max_x());

                    //typedef typename BaseRenderer::color_type color_type;
                    ArrayPOD<RGBA_Bytes> color_span = alloc.allocate((int)len * 2);
                    byte[] ManagedCoversArray = sl_aa.GetCovers();
                    fixed (byte* pCovers = ManagedCoversArray)
                    {
                        fixed (RGBA_Bytes* pColorSpan = color_span.Array)
                        {
                            int mix_bufferOffset = len;
                            int num_spans;

                            int num_styles;
                            int style;
                            bool solid;
                            while ((num_styles = ras.sweep_styles()) > 0)
                            {
                                if (num_styles == 1)
                                {
                                    // Optimization for a single style. Happens often
                                    //-------------------------
                                    if (ras.sweep_scanline(sl_aa, 0))
                                    {
                                        style = ras.style(0);
                                        if (sh.is_solid(style))
                                        {
                                            // Just solid fill
                                            //-----------------------
                                            RenderSolidSingleScanLine(imageFormat, sl_aa, sh.color(style));
                                        }
                                        else
                                        {
                                            // Arbitrary span generator
                                            //-----------------------
                                            ScanlineSpan span_aa = sl_aa.Begin();
                                            num_spans = sl_aa.num_spans();
                                            for (; ; )
                                            {
                                                len = span_aa.len;
                                                sh.generate_span(pColorSpan,
                                                                 span_aa.x,
                                                                 sl_aa.y(),
                                                                 (int)len,
                                                                 style);

                                                imageFormat.blend_color_hspan(span_aa.x,
                                                                      sl_aa.y(),
                                                                      (int)span_aa.len,
                                                                      pColorSpan,
                                                                      &pCovers[span_aa.cover_index], 0);
                                                if (--num_spans == 0) break;
                                                span_aa = sl_aa.GetNextScanlineSpan();
                                            }
                                        }
                                    }
                                }
                                else // there are multiple styles
                                {
                                    if (ras.sweep_scanline(sl_bin, -1))
                                    {
                                        // Clear the spans of the mix_buffer
                                        //--------------------
                                        ScanlineSpan span_bin = sl_bin.Begin();
                                        num_spans = sl_bin.num_spans();
                                        for (; ; )
                                        {
                                            agg_basics.MemClear((byte*)&pColorSpan[mix_bufferOffset + span_bin.x - min_x],
                                                   span_bin.len * sizeof(RGBA_Bytes));

                                            if (--num_spans == 0) break;
                                            span_bin = sl_bin.GetNextScanlineSpan();
                                        }

                                        for (int i = 0; i < num_styles; i++)
                                        {
                                            style = ras.style(i);
                                            solid = sh.is_solid(style);

                                            if (ras.sweep_scanline(sl_aa, (int)i))
                                            {
                                                //IColorType* colors;
                                                //IColorType* cspan;
                                                //typename ScanlineAA::cover_type* covers;
                                                ScanlineSpan span_aa = sl_aa.Begin();
                                                num_spans = sl_aa.num_spans();
                                                if (solid)
                                                {
                                                    // Just solid fill
                                                    //-----------------------
                                                    for (; ; )
                                                    {
                                                        RGBA_Bytes c = sh.color(style);
                                                        len = span_aa.len;
                                                        RGBA_Bytes* colors = &pColorSpan[mix_bufferOffset + span_aa.x - min_x];
                                                        byte* covers = &pCovers[span_aa.cover_index];
                                                        do
                                                        {
                                                            if (*covers == cover_full)
                                                            {
                                                                *colors = c;
                                                            }
                                                            else
                                                            {
                                                                colors->add(c, *covers);
                                                            }
                                                            ++colors;
                                                            ++covers;
                                                        }
                                                        while (--len != 0);
                                                        if (--num_spans == 0) break;
                                                        span_aa = sl_aa.GetNextScanlineSpan();
                                                    }
                                                }
                                                else
                                                {
                                                    // Arbitrary span generator
                                                    //-----------------------
                                                    for (; ; )
                                                    {
                                                        len = span_aa.len;
                                                        RGBA_Bytes* colors = &pColorSpan[mix_bufferOffset + span_aa.x - min_x];
                                                        RGBA_Bytes* cspan = pColorSpan;
                                                        sh.generate_span(cspan,
                                                                         span_aa.x,
                                                                         sl_aa.y(),
                                                                         (int)len,
                                                                         style);
                                                        byte* covers = &pCovers[span_aa.cover_index];
                                                        do
                                                        {
                                                            if (*covers == cover_full)
                                                            {
                                                                *colors = *cspan;
                                                            }
                                                            else
                                                            {
                                                                colors->add(*cspan, *covers);
                                                            }
                                                            ++cspan;
                                                            ++colors;
                                                            ++covers;
                                                        }
                                                        while (--len != 0);
                                                        if (--num_spans == 0) break;
                                                        span_aa = sl_aa.GetNextScanlineSpan();
                                                    }
                                                }
                                            }
                                        }

                                        // Emit the blended result as a color hspan
                                        //-------------------------
                                        span_bin = sl_bin.Begin();
                                        num_spans = sl_bin.num_spans();
                                        for (; ; )
                                        {
                                            imageFormat.blend_color_hspan(span_bin.x,
                                                                  sl_bin.y(),
                                                                  (int)span_bin.len,
                                                                  &pColorSpan[mix_bufferOffset + span_bin.x - min_x],
                                                                  null,
                                                                  cover_full);
                                            if (--num_spans == 0) break;
                                            span_bin = sl_bin.GetNextScanlineSpan();
                                        }
                                    } // if(ras.sweep_scanline(sl_bin, -1))
                                } // if(num_styles == 1) ... else
                            } // while((num_styles = ras.sweep_styles()) > 0)
                        }
                    }
                } // if(ras.rewind_scanlines())
            }
#endif
		}
	}
}