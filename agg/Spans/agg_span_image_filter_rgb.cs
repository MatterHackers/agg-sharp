using MatterHackers.Agg.Image;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007, 2026 Lars Brubaker
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
//
// Adaptation for high precision colors has been sponsored by
// Liberty Technology Systems, Inc., visit http://lib-sys.com
//
// Liberty Technology Systems, Inc. is the provider of
// PostScript and PDF technology for software developers.
//
//----------------------------------------------------------------------------
using System;
using image_filter_scale_e = MatterHackers.Agg.ImageFilterLookUpTable.image_filter_scale_e;
using image_subpixel_scale_e = MatterHackers.Agg.ImageFilterLookUpTable.image_subpixel_scale_e;

namespace MatterHackers.Agg
{
	// it should be easy to write a 90 rotating or mirroring filter too. LBB 2012/01/14
	public class span_image_filter_rgb_nn_stepXby1 : span_image_filter
	{
		private const int base_shift = 8;
		private const int base_scale = (int)(1 << base_shift);
		private const int base_mask = base_scale - 1;

		public span_image_filter_rgb_nn_stepXby1(IImageBufferAccessor sourceAccessor, ISpanInterpolator spanInterpolator)
			: base(sourceAccessor, spanInterpolator, null)
		{
		}

		public override void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			ImageBuffer SourceRenderingBuffer = (ImageBuffer)GetImageBufferAccessor().SourceImage;
			if (SourceRenderingBuffer.BitDepth != 24)
			{
				throw new NotSupportedException("The source is expected to be 32 bit.");
			}
			ISpanInterpolator spanInterpolator = interpolator();
			spanInterpolator.begin(x + filter_dx_dbl(), y + filter_dy_dbl(), len);
			int x_hr;
			int y_hr;
			spanInterpolator.coordinates(out x_hr, out y_hr);
			int x_lr = x_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;
			int y_lr = y_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;
			int bufferIndex;
			bufferIndex = SourceRenderingBuffer.GetBufferOffsetXY(x_lr, y_lr);

			byte[] fg_ptr = SourceRenderingBuffer.GetBuffer();
#if USE_UNSAFE_CODE
            unsafe
            {
                fixed (byte* pSource = fg_ptr)
                {
                    do
                    {
                        span[spanIndex++] = *(RGBA_Bytes*)&(pSource[bufferIndex]);
                        bufferIndex += 4;
                    } while (--len != 0);
                }
            }
#else
			Color color = Color.White;
			do
			{
				color.blue = fg_ptr[bufferIndex++];
				color.green = fg_ptr[bufferIndex++];
				color.red = fg_ptr[bufferIndex++];
				span[spanIndex++] = color;
			} while (--len != 0);
#endif
		}
	}

	//===============================================span_image_filter_rgb_nn
	public class span_image_filter_rgb_nn : span_image_filter
	{
		private const int base_shift = 8;
		private const int base_scale = (int)(1 << base_shift);
		private const int base_mask = base_scale - 1;

		//--------------------------------------------------------------------
		public span_image_filter_rgb_nn(IImageBufferAccessor src, ISpanInterpolator inter)
			: base(src, inter, null)
		{
		}

		public override void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			ImageBuffer SourceRenderingBuffer = (ImageBuffer)GetImageBufferAccessor().SourceImage;
			if (SourceRenderingBuffer.BitDepth != 24)
			{
				throw new NotSupportedException("The source is expected to be 32 bit.");
			}
			ISpanInterpolator spanInterpolator = interpolator();
			spanInterpolator.begin(x + filter_dx_dbl(), y + filter_dy_dbl(), len);
			int offset;
			byte[] fg_ptr = SourceRenderingBuffer.GetBuffer(out offset);
			do
			{
				int x_hr;
				int y_hr;
				spanInterpolator.coordinates(out x_hr, out y_hr);
				int x_lr = x_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;
				int y_lr = y_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;
				int bufferIndex;
				bufferIndex = SourceRenderingBuffer.GetBufferOffsetXY(x_lr, y_lr);
				Color color;
				color.blue = fg_ptr[bufferIndex++];
				color.green = fg_ptr[bufferIndex++];
				color.red = fg_ptr[bufferIndex++];
				color.alpha = 255;
				span[spanIndex] = color;
				spanIndex++;
				spanInterpolator.Next();
			} while (--len != 0);
		}
	};

	//==========================================span_image_filter_rgb_bilinear
	public class span_image_filter_rgb_bilinear : span_image_filter
	{
		private const int base_shift = 8;
		private const int base_scale = (int)(1 << base_shift);
		private const int base_mask = base_scale - 1;

		//--------------------------------------------------------------------
		public span_image_filter_rgb_bilinear(IImageBufferAccessor src,
											ISpanInterpolator inter)
			: base(src, inter, null)
		{
			if (src.SourceImage.GetBytesBetweenPixelsInclusive() != 3)
			{
				throw new System.NotSupportedException("span_image_filter_rgb must have a 24 bit DestImage");
			}
		}

		public override void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			base.interpolator().begin(x + base.filter_dx_dbl(), y + base.filter_dy_dbl(), len);

			// The pixels come through the accessor (span, next_x, next_y), as in C++, so a clip or clamp accessor
			// decides what lies past the image's edge. The port read the buffer directly, which ran on into the
			// next row, or past the buffer's end, there (SpanImageFilterRgbTests.BilinearReadsPastTheEdgeThroughItsAccessor).
			IImageBufferAccessor source = base.GetImageBufferAccessor();
			ISpanInterpolator spanInterpolator = base.interpolator();
			int bufferIndex;
			byte[] fg_ptr;

			unchecked
			{
				do
				{
					int tempR;
					int tempG;
					int tempB;

					int x_hr;
					int y_hr;

					spanInterpolator.coordinates(out x_hr, out y_hr);

					x_hr -= base.filter_dx_int();
					y_hr -= base.filter_dy_int();

					int x_lr = x_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;
					int y_lr = y_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;
					int weight;

					tempR =
					tempG =
					tempB = (int)image_subpixel_scale_e.image_subpixel_scale * (int)image_subpixel_scale_e.image_subpixel_scale / 2;

					x_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;
					y_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;

					fg_ptr = source.span(x_lr, y_lr, 2, out bufferIndex);

					weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) *
							 ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];

					fg_ptr = source.next_x(out bufferIndex);
					weight = (x_hr * ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];

					fg_ptr = source.next_y(out bufferIndex);
					weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) * y_hr);
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];

					fg_ptr = source.next_x(out bufferIndex);
					weight = (x_hr * y_hr);
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];

					tempR >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
					tempG >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
					tempB >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;

					Color color;
					color.red = (byte)tempR;
					color.green = (byte)tempG;
					color.blue = (byte)tempB;
					color.alpha = 255;
					span[spanIndex] = color;
					spanIndex++;
					spanInterpolator.Next();
				} while (--len != 0);
			}
		}

		private void BlendInFilterPixel(int[] fg, ref int src_alpha, int back_r, int back_g, int back_b, int back_a, ImageBuffer SourceRenderingBuffer, int maxx, int maxy, int x_lr, int y_lr, int weight)
		{
			throw new NotImplementedException(); /*
            int[] fg_ptr;
            int bufferIndex;
            unchecked
            {
                if ((uint)x_lr <= (uint)maxx && (uint)y_lr <= (uint)maxy)
                {
                    fg_ptr = SourceRenderingBuffer.GetPixelPointerXY(x_lr, y_lr, out bufferIndex);

                    fg[0] += (weight * (fg_ptr[bufferIndex] & (int)RGBA_Bytes.m_R) >> (int)RGBA_Bytes.Shift.R);
                    fg[1] += (weight * (fg_ptr[bufferIndex] & (int)RGBA_Bytes.m_G) >> (int)RGBA_Bytes.Shift.G);
                    fg[2] += (weight * (fg_ptr[bufferIndex] & (int)RGBA_Bytes.m_G) >> (int)RGBA_Bytes.Shift.B);
                    src_alpha += weight * base_mask;
                }
                else
                {
                    fg[0] += (weight * back_r);
                    fg[1] += (weight * back_g);
                    fg[2] += (weight * back_b);
                    src_alpha += back_a * weight;
                }
            }
                                                      */
		}
	};

	//=====================================span_image_filter_rgb_bilinear_clip
	public class span_image_filter_rgb_bilinear_clip : span_image_filter
	{
		private Color m_OutsideSourceColor;

		private const int base_shift = 8;
		private const int base_scale = (int)(1 << base_shift);
		private const int base_mask = base_scale - 1;

		//--------------------------------------------------------------------
		public span_image_filter_rgb_bilinear_clip(IImageBufferAccessor src,
											IColorType back_color,
											ISpanInterpolator inter)
			: base(src, inter, null)
		{
			m_OutsideSourceColor = back_color.ToColor();
		}

		public IColorType background_color()
		{
			return m_OutsideSourceColor;
		}

		public void background_color(IColorType v)
		{
			m_OutsideSourceColor = v.ToColor();
		}

		public override void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			base.interpolator().begin(x + base.filter_dx_dbl(), y + base.filter_dy_dbl(), len);

			int[] accumulatedColor = new int[3];
			int sourceAlpha;

			int back_r = m_OutsideSourceColor.red;
			int back_g = m_OutsideSourceColor.green;
			int back_b = m_OutsideSourceColor.blue;
			int back_a = m_OutsideSourceColor.alpha;

			int bufferIndex;
			byte[] fg_ptr;

			ImageBuffer SourceRenderingBuffer = (ImageBuffer)base.GetImageBufferAccessor().SourceImage;
			int maxx = (int)SourceRenderingBuffer.Width - 1;
			int maxy = (int)SourceRenderingBuffer.Height - 1;
			ISpanInterpolator spanInterpolator = base.interpolator();

			unchecked
			{
				do
				{
					int x_hr;
					int y_hr;

					spanInterpolator.coordinates(out x_hr, out y_hr);

					x_hr -= base.filter_dx_int();
					y_hr -= base.filter_dy_int();

					int x_lr = x_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;
					int y_lr = y_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;
					int weight;

					if (x_lr >= 0 && y_lr >= 0 &&
					   x_lr < maxx && y_lr < maxy)
					{
						// Start at half a unit so the downshift rounds, as span_image_filter_rgb_bilinear does. C++ AGG
						// starts at 0 and truncates; the reference renderer's patched agg_span_image_filter_rgb.h matches
						// this (SpanImageFilterRgbTests pins it).
						accumulatedColor[0] =
						accumulatedColor[1] =
						accumulatedColor[2] = (int)image_subpixel_scale_e.image_subpixel_scale * (int)image_subpixel_scale_e.image_subpixel_scale / 2;

						x_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;
						y_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;

						fg_ptr = SourceRenderingBuffer.GetPixelPointerXY(x_lr, y_lr, out bufferIndex);

						weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) *
								 ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
						accumulatedColor[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
						accumulatedColor[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
						accumulatedColor[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];

						bufferIndex += 3;
						weight = (x_hr * ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
						accumulatedColor[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
						accumulatedColor[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
						accumulatedColor[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];

						y_lr++;
						fg_ptr = SourceRenderingBuffer.GetPixelPointerXY(x_lr, y_lr, out bufferIndex);

						weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) * y_hr);
						accumulatedColor[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
						accumulatedColor[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
						accumulatedColor[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];

						bufferIndex += 3;
						weight = (x_hr * y_hr);
						accumulatedColor[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
						accumulatedColor[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
						accumulatedColor[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];

						accumulatedColor[0] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
						accumulatedColor[1] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
						accumulatedColor[2] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;

						sourceAlpha = base_mask;
					}
					else
					{
						if (x_lr < -1 || y_lr < -1 ||
						   x_lr > maxx || y_lr > maxy)
						{
							accumulatedColor[0] = back_r;
							accumulatedColor[1] = back_g;
							accumulatedColor[2] = back_b;
							sourceAlpha = back_a;
						}
						else
						{
							accumulatedColor[0] =
							accumulatedColor[1] =
							accumulatedColor[2] = (int)image_subpixel_scale_e.image_subpixel_scale * (int)image_subpixel_scale_e.image_subpixel_scale / 2;
							sourceAlpha = (int)image_subpixel_scale_e.image_subpixel_scale * (int)image_subpixel_scale_e.image_subpixel_scale / 2;

							x_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;
							y_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;

							weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) *
									 ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
							BlendInFilterPixel(accumulatedColor, ref sourceAlpha, back_r, back_g, back_b, back_a, SourceRenderingBuffer, maxx, maxy, x_lr, y_lr, weight);

							x_lr++;

							weight = (x_hr * ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
							BlendInFilterPixel(accumulatedColor, ref sourceAlpha, back_r, back_g, back_b, back_a, SourceRenderingBuffer, maxx, maxy, x_lr, y_lr, weight);

							x_lr--;
							y_lr++;

							weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) * y_hr);
							BlendInFilterPixel(accumulatedColor, ref sourceAlpha, back_r, back_g, back_b, back_a, SourceRenderingBuffer, maxx, maxy, x_lr, y_lr, weight);

							x_lr++;

							weight = (x_hr * y_hr);
							BlendInFilterPixel(accumulatedColor, ref sourceAlpha, back_r, back_g, back_b, back_a, SourceRenderingBuffer, maxx, maxy, x_lr, y_lr, weight);

							accumulatedColor[0] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
							accumulatedColor[1] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
							accumulatedColor[2] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
							sourceAlpha >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
						}
					}

					span[spanIndex].red = (byte)accumulatedColor[0];
					span[spanIndex].green = (byte)accumulatedColor[1];
					span[spanIndex].blue = (byte)accumulatedColor[2];
					span[spanIndex].alpha = (byte)sourceAlpha;
					spanIndex++;
					spanInterpolator.Next();
				} while (--len != 0);
			}
		}

		private void BlendInFilterPixel(int[] accumulatedColor, ref int sourceAlpha, int back_r, int back_g, int back_b, int back_a, ImageBuffer SourceRenderingBuffer, int maxx, int maxy, int x_lr, int y_lr, int weight)
		{
			byte[] fg_ptr;
			unchecked
			{
				if ((uint)x_lr <= (uint)maxx && (uint)y_lr <= (uint)maxy)
				{
					int bufferIndex;
					fg_ptr = SourceRenderingBuffer.GetPixelPointerXY(x_lr, y_lr, out bufferIndex);

					accumulatedColor[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					accumulatedColor[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					accumulatedColor[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
					sourceAlpha += weight * base_mask;
				}
				else
				{
					accumulatedColor[0] += back_r * weight;
					accumulatedColor[1] += back_g * weight;
					accumulatedColor[2] += back_b * weight;
					sourceAlpha += back_a * weight;
				}
			}
		}
	};

	//===================================================span_image_filter_rgb
	public class span_image_filter_rgb : span_image_filter
	{
		private const int base_mask = 255;

		//--------------------------------------------------------------------
		public span_image_filter_rgb(IImageBufferAccessor src, ISpanInterpolator inter, ImageFilterLookUpTable filter)
			: base(src, inter, filter)
		{
			if (src.SourceImage.GetBytesBetweenPixelsInclusive() != 3)
			{
				throw new System.NotSupportedException("span_image_filter_rgb must have a 24 bit DestImage");
			}
		}

		public override void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			base.interpolator().begin(x + base.filter_dx_dbl(), y + base.filter_dy_dbl(), len);

			int f_r, f_g, f_b;

			byte[] fg_ptr;

			int diameter = m_filter.diameter();
			int start = m_filter.start();
			int[] weight_array = m_filter.weight_array();

			int x_count;
			int weight_y;

			ISpanInterpolator spanInterpolator = base.interpolator();

			do
			{
				spanInterpolator.coordinates(out x, out y);

				x -= base.filter_dx_int();
				y -= base.filter_dy_int();

				int x_hr = x;
				int y_hr = y;

				int x_lr = x_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;
				int y_lr = y_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;

				// Start at half a unit so the downshift rounds. C++ AGG starts at 0 and truncates, darkening by half a
				// unit on average; the reference renderer's patched agg_span_image_filter_rgb.h matches this
				// (SpanImageFilterRgbTests pins it). (f_b sums the red bytes and f_r the blue; they are stored back
				// crosswise, so the names cancel out.)
				f_b = f_g = f_r = (int)image_filter_scale_e.image_filter_scale / 2;

				int x_fract = x_hr & (int)image_subpixel_scale_e.image_subpixel_mask;
				int y_count = diameter;

				y_hr = (int)image_subpixel_scale_e.image_subpixel_mask - (y_hr & (int)image_subpixel_scale_e.image_subpixel_mask);

				int bufferIndex;
				fg_ptr = GetImageBufferAccessor().span(x_lr + start, y_lr + start, diameter, out bufferIndex);
				for (; ; )
				{
					x_count = (int)diameter;
					weight_y = weight_array[y_hr];
					x_hr = (int)image_subpixel_scale_e.image_subpixel_mask - x_fract;
					for (; ; )
					{
						int weight = (weight_y * weight_array[x_hr] +
									 (int)image_filter_scale_e.image_filter_scale / 2) >>
									 (int)image_filter_scale_e.image_filter_shift;

						f_b += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
						f_g += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
						f_r += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];

						if (--x_count == 0) break;
						x_hr += (int)image_subpixel_scale_e.image_subpixel_scale;
						fg_ptr = GetImageBufferAccessor().next_x(out bufferIndex);
					}

					if (--y_count == 0) break;
					y_hr += (int)image_subpixel_scale_e.image_subpixel_scale;
					fg_ptr = GetImageBufferAccessor().next_y(out bufferIndex);
				}

				f_b >>= (int)image_filter_scale_e.image_filter_shift;
				f_g >>= (int)image_filter_scale_e.image_filter_shift;
				f_r >>= (int)image_filter_scale_e.image_filter_shift;

				unchecked
				{
					if ((uint)f_b > base_mask)
					{
						if (f_b < 0) f_b = 0;
						if (f_b > base_mask) f_b = (int)base_mask;
					}

					if ((uint)f_g > base_mask)
					{
						if (f_g < 0) f_g = 0;
						if (f_g > base_mask) f_g = (int)base_mask;
					}

					if ((uint)f_r > base_mask)
					{
						if (f_r < 0) f_r = 0;
						if (f_r > base_mask) f_r = (int)base_mask;
					}
				}

				span[spanIndex].alpha = (byte)base_mask;
				span[spanIndex].red = (byte)f_b;
				span[spanIndex].green = (byte)f_g;
				span[spanIndex].blue = (byte)f_r;

				spanIndex++;
				spanInterpolator.Next();
			} while (--len != 0);
		}
	};

	//===============================================span_image_filter_rgb_2x2
	/// <summary>
	/// C++ span_image_filter_rgb_2x2: a 24-bit source sampled through the middle two taps of a filter lookup
	/// table (hanning, hamming, hermite in the image_filters demo), for an opaque span.
	/// </summary>
	public class span_image_filter_rgb_2x2 : span_image_filter
	{
		private const int base_mask = 255;

		//--------------------------------------------------------------------
		public span_image_filter_rgb_2x2(IImageBufferAccessor src, ISpanInterpolator inter, ImageFilterLookUpTable filter)
			: base(src, inter, filter)
		{
			if (src.SourceImage.GetBytesBetweenPixelsInclusive() != 3)
			{
				throw new NotSupportedException("span_image_filter_rgb_2x2 must have a 24 bit source image");
			}
		}

		public override void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			const int subpixelShift = (int)image_subpixel_scale_e.image_subpixel_shift;
			const int subpixelScale = (int)image_subpixel_scale_e.image_subpixel_scale;
			const int subpixelMask = (int)image_subpixel_scale_e.image_subpixel_mask;
			const int filterShift = (int)image_filter_scale_e.image_filter_shift;
			const int filterHalf = (int)image_filter_scale_e.image_filter_scale / 2;

			ISpanInterpolator spanInterpolator = interpolator();
			IImageBufferAccessor source = GetImageBufferAccessor();
			spanInterpolator.begin(x + filter_dx_dbl(), y + filter_dy_dbl(), len);

			int[] weightArray = filter().weight_array();
			// C++ offsets its weight pointer to the filter's middle two taps.
			int weightStart = (filter().diameter() / 2 - 1) << subpixelShift;
			var fg = new int[3];
			do
			{
				spanInterpolator.coordinates(out int x_hr, out int y_hr);
				x_hr -= filter_dx_int();
				y_hr -= filter_dy_int();

				int x_lr = x_hr >> subpixelShift;
				int y_lr = y_hr >> subpixelShift;

				// Start at half a unit so the downshift rounds, as span_image_filter_rgb_bilinear does. C++ AGG
				// starts at 0 and truncates, darkening by half a unit on average; the reference renderer's patched
				// agg_span_image_filter_rgb.h fixes it the same way (SpanImageFilterRgbTests pins it).
				fg[0] = fg[1] = fg[2] = filterHalf;

				x_hr &= subpixelMask;
				y_hr &= subpixelMask;

				byte[] buffer = source.span(x_lr, y_lr, 2, out int offset);
				int weight = (weightArray[weightStart + x_hr + subpixelScale] * weightArray[weightStart + y_hr + subpixelScale] + filterHalf) >> filterShift;
				AddWeighted(fg, weight, buffer, offset);

				buffer = source.next_x(out offset);
				weight = (weightArray[weightStart + x_hr] * weightArray[weightStart + y_hr + subpixelScale] + filterHalf) >> filterShift;
				AddWeighted(fg, weight, buffer, offset);

				buffer = source.next_y(out offset);
				weight = (weightArray[weightStart + x_hr + subpixelScale] * weightArray[weightStart + y_hr] + filterHalf) >> filterShift;
				AddWeighted(fg, weight, buffer, offset);

				buffer = source.next_x(out offset);
				weight = (weightArray[weightStart + x_hr] * weightArray[weightStart + y_hr] + filterHalf) >> filterShift;
				AddWeighted(fg, weight, buffer, offset);

				// fg is indexed by source byte order; C++'s downshift is a plain shift for 8-bit color. Only the top is
				// clamped, as in C++.
				int b = Math.Min(fg[ImageBuffer.OrderB] >> filterShift, base_mask);
				int g = Math.Min(fg[ImageBuffer.OrderG] >> filterShift, base_mask);
				int r = Math.Min(fg[ImageBuffer.OrderR] >> filterShift, base_mask);

				span[spanIndex].red = (byte)r;
				span[spanIndex].green = (byte)g;
				span[spanIndex].blue = (byte)b;
				span[spanIndex].alpha = base_mask;
				spanIndex++;
				spanInterpolator.Next();
			} while (--len != 0);
		}

		private static void AddWeighted(int[] fg, int weight, byte[] buffer, int offset)
		{
			fg[0] += weight * buffer[offset];
			fg[1] += weight * buffer[offset + 1];
			fg[2] += weight * buffer[offset + 2];
		}
	};
}