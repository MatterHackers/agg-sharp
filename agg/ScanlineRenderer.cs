using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg
{
	public class ScanlineRenderer
	{
		private VectorPOD<Color> tempSpanColors = new VectorPOD<Color>();
		private VectorPOD<ColorF> tempSpanColorsFloats = new VectorPOD<ColorF>();

		public void RenderSolid(IImageByte destImage, IRasterizer rasterizer, IScanlineCache scanLine, Color color)
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

		public void RenderSolid(IImageFloat destImage, IRasterizer rasterizer, IScanlineCache scanLine, ColorF color)
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

		protected virtual void RenderSolidSingleScanLine(IImageByte destImage, IScanlineCache scanLine, Color color)
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

		private void RenderSolidSingleScanLine(IImageFloat destImage, IScanlineCache scanLine, ColorF color)
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

		private static readonly byte[] FullCover = { 255 };

		/// <summary>
		/// C++ <c>render_scanlines_compound_layered</c>: every style of <paramref name="ras"/> drawn into
		/// <paramref name="destination"/> as layers, in the rasterizer's layer order. Where styles overlap on a
		/// scanline, each pixel's coverage is handed out top layer first until it is full (255), the colors are
		/// summed at those covers into a mix buffer, and the mix is blended into the destination once, so
		/// shared edges do not leak the background. The style colors should be premultiplied and the
		/// destination blend premultiplied colors (C++ renders it into a <c>pixfmt_*_pre</c>).
		/// </summary>
		/// <param name="ras">The compound rasterizer holding the styled paths.</param>
		/// <param name="sl_aa">An unpacked scanline (C++ <c>scanline_u8</c>): one cover per pixel.</param>
		/// <param name="destination">Where to blend; C++'s renderer_base clips, so pass a clipping proxy if
		/// the paths may leave the image.</param>
		/// <param name="sh">The color, or span generator, of each style.</param>
		public void RenderCompoundLayered(rasterizer_compound_aa ras, IScanlineCache sl_aa, IImageByte destination, IStyleHandler sh)
		{
			if (!ras.rewind_scanlines())
			{
				return;
			}

			int min_x = ras.min_x();
			int len = ras.max_x() - min_x + 2;
			sl_aa.reset(min_x, ras.max_x());

			var colorSpan = new Color[len];
			var mixBuffer = new Color[len];
			var coverBuffer = new byte[len];

			int num_styles;
			while ((num_styles = ras.sweep_styles()) > 0)
			{
				if (num_styles == 1)
				{
					RenderSingleStyle(ras, sl_aa, destination, sh, colorSpan);
				}
				else
				{
					int sl_start = ras.scanline_start();
					int sl_len = ras.scanline_length();
					if (sl_len != 0)
					{
						System.Array.Clear(mixBuffer, sl_start - min_x, sl_len);
						System.Array.Clear(coverBuffer, sl_start - min_x, sl_len);
						int sl_y = int.MaxValue;
						for (int i = 0; i < num_styles; i++)
						{
							int style = ras.style(i);
							bool solid = sh.IsSolid(style);
							if (ras.sweep_scanline(sl_aa, i))
							{
								byte[] srcCovers = sl_aa.GetCovers();
								ScanlineSpan span_aa = sl_aa.begin();
								int num_spans = sl_aa.num_spans();
								sl_y = sl_aa.y();
								for (; ; )
								{
									int spanLen = span_aa.len;
									Color solidColor = default;
									if (solid)
									{
										solidColor = sh.color(style);
									}
									else
									{
										sh.GenerateSpan(colorSpan, 0, span_aa.x, sl_aa.y(), spanLen, style);
									}

									int mixIndex = span_aa.x - min_x;
									int srcCoverIndex = span_aa.cover_index;
									for (int k = 0; k < spanLen; k++)
									{
										int cover = srcCovers[srcCoverIndex + k];
										int dstCover = coverBuffer[mixIndex + k];
										if (dstCover + cover > 255)
										{
											cover = 255 - dstCover;
										}

										if (cover != 0)
										{
											AddColor(ref mixBuffer[mixIndex + k], solid ? solidColor : colorSpan[k], cover);
											coverBuffer[mixIndex + k] = (byte)(dstCover + cover);
										}
									}

									if (--num_spans == 0) break;
									span_aa = sl_aa.GetNextScanlineSpan();
								}
							}
						}

						destination.blend_color_hspan(sl_start, sl_y, sl_len, mixBuffer, sl_start - min_x, FullCover, 0, true);
					}
				}
			}
		}

		/// <summary>
		/// C++ <c>render_scanlines_compound</c>: every style of <paramref name="ras"/> drawn into
		/// <paramref name="destination"/>. Where styles meet on a scanline, each style's covered colors are
		/// summed into a mix buffer (a full cover replaces what is there) and the mix is blended once over the
		/// pixels <paramref name="sl_bin"/> marks, so edges two styles share do not show the background. The
		/// style colors should be premultiplied.
		/// </summary>
		public void RenderCompound(rasterizer_compound_aa ras, IScanlineCache sl_aa, IScanlineCache sl_bin, IImageByte destination, span_allocator alloc, IStyleHandler sh)
		{
			if (!ras.rewind_scanlines())
			{
				return;
			}

			int min_x = ras.min_x();
			int len = ras.max_x() - min_x + 2;
			sl_aa.reset(min_x, ras.max_x());
			sl_bin.reset(min_x, ras.max_x());

			var colorSpan = new Color[len];
			var mixBuffer = new Color[len];

			int num_styles;
			while ((num_styles = ras.sweep_styles()) > 0)
			{
				if (num_styles == 1)
				{
					RenderSingleStyle(ras, sl_aa, destination, sh, colorSpan);
				}
				else if (ras.sweep_scanline(sl_bin, -1))
				{
					ScanlineSpan span_bin = sl_bin.begin();
					int num_spans = sl_bin.num_spans();
					for (; ; )
					{
						System.Array.Clear(mixBuffer, span_bin.x - min_x, span_bin.len);
						if (--num_spans == 0) break;
						span_bin = sl_bin.GetNextScanlineSpan();
					}

					for (int i = 0; i < num_styles; i++)
					{
						int style = ras.style(i);
						bool solid = sh.IsSolid(style);
						if (ras.sweep_scanline(sl_aa, i))
						{
							byte[] covers = sl_aa.GetCovers();
							ScanlineSpan span_aa = sl_aa.begin();
							num_spans = sl_aa.num_spans();
							for (; ; )
							{
								int spanLen = span_aa.len;
								Color solidColor = default;
								if (solid)
								{
									solidColor = sh.color(style);
								}
								else
								{
									sh.GenerateSpan(colorSpan, 0, span_aa.x, sl_aa.y(), spanLen, style);
								}

								int mixIndex = span_aa.x - min_x;
								for (int k = 0; k < spanLen; k++)
								{
									int cover = covers[span_aa.cover_index + k];
									Color c = solid ? solidColor : colorSpan[k];
									if (cover == 255)
									{
										mixBuffer[mixIndex + k] = c;
									}
									else
									{
										AddColor(ref mixBuffer[mixIndex + k], c, cover);
									}
								}

								if (--num_spans == 0) break;
								span_aa = sl_aa.GetNextScanlineSpan();
							}
						}
					}

					// Emit the blended result as a color hspan
					span_bin = sl_bin.begin();
					num_spans = sl_bin.num_spans();
					for (; ; )
					{
						destination.blend_color_hspan(span_bin.x, sl_bin.y(), span_bin.len, mixBuffer, span_bin.x - min_x, FullCover, 0, true);
						if (--num_spans == 0) break;
						span_bin = sl_bin.GetNextScanlineSpan();
					}
				}
			}
		}

		/// <summary>The compound renderers' scanline with one style: a plain solid fill or span-generated blend.</summary>
		private void RenderSingleStyle(rasterizer_compound_aa ras, IScanlineCache sl_aa, IImageByte destination, IStyleHandler sh, Color[] colorSpan)
		{
			if (!ras.sweep_scanline(sl_aa, 0))
			{
				return;
			}

			int style = ras.style(0);
			if (sh.IsSolid(style))
			{
				RenderSolidSingleScanLine(destination, sl_aa, sh.color(style));
				return;
			}

			byte[] covers = sl_aa.GetCovers();
			ScanlineSpan span_aa = sl_aa.begin();
			int num_spans = sl_aa.num_spans();
			for (; ; )
			{
				sh.GenerateSpan(colorSpan, 0, span_aa.x, sl_aa.y(), span_aa.len, style);
				destination.blend_color_hspan(span_aa.x, sl_aa.y(), span_aa.len, colorSpan, 0, covers, span_aa.cover_index, false);
				if (--num_spans == 0) break;
				span_aa = sl_aa.GetNextScanlineSpan();
			}
		}

		/// <summary>C++ <c>rgba8::add(c, cover)</c>: <paramref name="c"/> at <paramref name="cover"/> added to
		/// <paramref name="sum"/>, each channel saturating at 255; a full cover of an opaque color replaces it.</summary>
		private static void AddColor(ref Color sum, Color c, int cover)
		{
			if (cover == 255)
			{
				if (c.alpha == 255)
				{
					sum = c;
					return;
				}

				sum = new Color(
					System.Math.Min(sum.red + c.red, 255),
					System.Math.Min(sum.green + c.green, 255),
					System.Math.Min(sum.blue + c.blue, 255),
					System.Math.Min(sum.alpha + c.alpha, 255));
				return;
			}

			sum = new Color(
				System.Math.Min(sum.red + Rgba8Math.Multiply(c.red, cover), 255),
				System.Math.Min(sum.green + Rgba8Math.Multiply(c.green, cover), 255),
				System.Math.Min(sum.blue + Rgba8Math.Multiply(c.blue, cover), 255),
				System.Math.Min(sum.alpha + Rgba8Math.Multiply(c.alpha, cover), 255));
		}
	}
}