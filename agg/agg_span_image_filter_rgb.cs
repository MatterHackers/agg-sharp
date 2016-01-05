using MatterHackers.Agg.Image;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
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

		public override void generate(RGBA_Bytes[] span, int spanIndex, int x, int y, int len)
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
			RGBA_Bytes color = RGBA_Bytes.White;
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

		public override void generate(RGBA_Bytes[] span, int spanIndex, int x, int y, int len)
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
				RGBA_Bytes color;
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

		public override void generate(RGBA_Bytes[] span, int spanIndex, int x, int y, int len)
		{
			base.interpolator().begin(x + base.filter_dx_dbl(), y + base.filter_dy_dbl(), len);

			ImageBuffer SourceRenderingBuffer = (ImageBuffer)base.GetImageBufferAccessor().SourceImage;
			ISpanInterpolator spanInterpolator = base.interpolator();
			int bufferIndex;
			byte[] fg_ptr = SourceRenderingBuffer.GetBuffer(out bufferIndex);

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

					bufferIndex = SourceRenderingBuffer.GetBufferOffsetXY(x_lr, y_lr);

					weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) *
							 ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
					bufferIndex += 3;

					weight = (x_hr * ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];

					y_lr++;
					bufferIndex = SourceRenderingBuffer.GetBufferOffsetXY(x_lr, y_lr);

					weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) * y_hr);
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
					bufferIndex += 3;

					weight = (x_hr * y_hr);
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];

					tempR >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
					tempG >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
					tempB >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;

					RGBA_Bytes color;
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
		private RGBA_Bytes m_OutsideSourceColor;

		private const int base_shift = 8;
		private const int base_scale = (int)(1 << base_shift);
		private const int base_mask = base_scale - 1;

		//--------------------------------------------------------------------
		public span_image_filter_rgb_bilinear_clip(IImageBufferAccessor src,
											IColorType back_color,
											ISpanInterpolator inter)
			: base(src, inter, null)
		{
			m_OutsideSourceColor = back_color.GetAsRGBA_Bytes();
		}

		public IColorType background_color()
		{
			return m_OutsideSourceColor;
		}

		public void background_color(IColorType v)
		{
			m_OutsideSourceColor = v.GetAsRGBA_Bytes();
		}

		public override void generate(RGBA_Bytes[] span, int spanIndex, int x, int y, int len)
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

		public override void generate(RGBA_Bytes[] span, int spanIndex, int x, int y, int len)
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
						GetImageBufferAccessor().next_x(out bufferIndex);
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
	public class span_image_filter_rgb_2x2 : span_image_filter
	{
		private const int base_mask = 255;

		//--------------------------------------------------------------------
		public span_image_filter_rgb_2x2(IImageBufferAccessor src, ISpanInterpolator inter, ImageFilterLookUpTable filter)
			: base(src, inter, filter)
		{
		}

		public override void generate(RGBA_Bytes[] span, int spanIndex, int x, int y, int len)
		{
			throw new NotImplementedException(); /*
            ISpanInterpolator spanInterpolator = base.interpolator();
            spanInterpolator.begin(x + base.filter_dx_dbl(), y + base.filter_dy_dbl(), len);

            int[] fg = new int[3];

            int[] fg_ptr;
            int bufferIndex;
            int[] weight_array = filter().weight_array();
            int weightArrayIndex = ((filter().diameter() / 2 - 1) << (int)image_subpixel_scale_e.image_subpixel_shift);

            do
            {
                int x_hr;
                int y_hr;

                spanInterpolator.coordinates(out x_hr, out y_hr);

                x_hr -= filter_dx_int();
                y_hr -= filter_dy_int();

                int x_lr = x_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;
                int y_lr = y_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;

                int weight;
                fg[0] = fg[1] = fg[2] = (int)image_filter_scale_e.image_filter_scale / 2;

                x_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;
                y_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;

                fg_ptr = source().span(x_lr, y_lr, 2, out bufferIndex);
                weight = ((weight_array[x_hr + (int)image_subpixel_scale_e.image_subpixel_scale] *
                          weight_array[y_hr + (int)image_subpixel_scale_e.image_subpixel_scale] +
                          (int)image_filter_scale_e.image_filter_scale / 2) >>
                          (int)image_filter_scale_e.image_filter_shift);
                fg[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
                fg[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
                fg[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];

                fg_ptr = source().next_x(out bufferIndex);
                weight = ((weight_array[x_hr] *
                          weight_array[y_hr + (int)image_subpixel_scale_e.image_subpixel_scale] +
                          (int)image_filter_scale_e.image_filter_scale / 2) >>
                          (int)image_filter_scale_e.image_filter_shift);
                fg[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
                fg[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
                fg[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];

                fg_ptr = source().next_y(out bufferIndex);
                weight = ((weight_array[x_hr + (int)image_subpixel_scale_e.image_subpixel_scale] *
                          weight_array[y_hr] +
                          (int)image_filter_scale_e.image_filter_scale / 2) >>
                          (int)image_filter_scale_e.image_filter_shift);
                fg[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
                fg[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
                fg[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];

                fg_ptr = source().next_x(out bufferIndex);
                weight = ((weight_array[x_hr] *
                          weight_array[y_hr] +
                          (int)image_filter_scale_e.image_filter_scale / 2) >>
                          (int)image_filter_scale_e.image_filter_shift);
                fg[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
                fg[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
                fg[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];

                fg[0] >>= (int)image_filter_scale_e.image_filter_shift;
                fg[1] >>= (int)image_filter_scale_e.image_filter_shift;
                fg[2] >>= (int)image_filter_scale_e.image_filter_shift;

                if (fg[0] > base_mask) fg[0] = (int)base_mask;
                if (fg[1] > base_mask) fg[1] = (int)base_mask;
                if (fg[2] > base_mask) fg[2] = (int)base_mask;

                span[spanIndex].m_ARGBData = base_mask << (int)RGBA_Bytes.Shift.A | fg[0] << (int)RGBA_Bytes.Shift.R | fg[1] << (int)RGBA_Bytes.Shift.G | fg[2] << (int)RGBA_Bytes.Shift.B;

                spanIndex++;
                spanInterpolator.Next();
            } while (--len != 0);
                                                      */
		}
	};

	/*
	//==========================================span_image_resample_rgb_affine
	template<class Source>
	class span_image_resample_rgb_affine :
	public span_image_resample_affine<Source>
	{
	public:
		typedef Source source_type;
		typedef typename source_type::color_type color_type;
		typedef typename source_type::order_type order_type;
		typedef span_image_resample_affine<source_type> base_type;
		typedef typename base_type::interpolator_type interpolator_type;
		typedef typename color_type::value_type value_type;
		typedef typename color_type::long_type long_type;
		enum base_scale_e
		{
			base_shift      = 8,//color_type::base_shift,
			base_mask       = 255,//color_type::base_mask,
			downscale_shift = image_filter_shift
		};

		//--------------------------------------------------------------------
		span_image_resample_rgb_affine() {}
		span_image_resample_rgb_affine(source_type& src,
									   interpolator_type& inter,
									   const ImageFilterLookUpTable& filter) :
			base(src, inter, filter)
		{}

		//--------------------------------------------------------------------
		void generate(color_type* span, int x, int y, unsigned len)
		{
			base_type::interpolator().begin(x + base_type::filter_dx_dbl(),
											y + base_type::filter_dy_dbl(), len);

			long_type fg[3];

			int diameter     = base_type::filter().diameter();
			int filter_scale = diameter << image_subpixel_shift;
			int radius_x     = (diameter * base_type::m_rx) >> 1;
			int radius_y     = (diameter * base_type::m_ry) >> 1;
			int len_x_lr     =
				(diameter * base_type::m_rx + image_subpixel_mask) >>
					image_subpixel_shift;

			const int16* weight_array = base_type::filter().weight_array();

			do
			{
				base_type::interpolator().coordinates(&x, &y);

				x += base_type::filter_dx_int() - radius_x;
				y += base_type::filter_dy_int() - radius_y;

				fg[0] = fg[1] = fg[2] = image_filter_scale / 2;

				int y_lr = y >> image_subpixel_shift;
				int y_hr = ((image_subpixel_mask - (y & image_subpixel_mask)) *
								base_type::m_ry_inv) >>
									image_subpixel_shift;
				int total_weight = 0;
				int x_lr = x >> image_subpixel_shift;
				int x_hr = ((image_subpixel_mask - (x & image_subpixel_mask)) *
								base_type::m_rx_inv) >>
									image_subpixel_shift;

				int x_hr2 = x_hr;
				const value_type* fg_ptr =
					source().pix_ptr(x_lr, y_lr, len_x_lr);
				for(;;)
				{
					int weight_y = weight_array[y_hr];
					x_hr = x_hr2;
					for(;;)
					{
						int weight = (weight_y * weight_array[x_hr] +
									 image_filter_scale / 2) >>
									 downscale_shift;

						fg[0] += *fg_ptr++ * weight;
						fg[1] += *fg_ptr++ * weight;
						fg[2] += *fg_ptr   * weight;
						total_weight += weight;
						x_hr  += base_type::m_rx_inv;
						if(x_hr >= filter_scale) break;
						fg_ptr = SourceRenderingBuffer.next_x();
					}
					y_hr += base_type::m_ry_inv;
					if(y_hr >= filter_scale) break;
					fg_ptr = SourceRenderingBuffer.next_y();
				}

				fg[0] /= total_weight;
				fg[1] /= total_weight;
				fg[2] /= total_weight;

				if(fg[0] < 0) fg[0] = 0;
				if(fg[1] < 0) fg[1] = 0;
				if(fg[2] < 0) fg[2] = 0;

				if(fg[order_type::R] > base_mask) fg[order_type::R] = base_mask;
				if(fg[order_type::G] > base_mask) fg[order_type::G] = base_mask;
				if(fg[order_type::B] > base_mask) fg[order_type::B] = base_mask;

				span->r = (value_type)fg[order_type::R];
				span->g = (value_type)fg[order_type::G];
				span->b = (value_type)fg[order_type::B];
				span->a = base_mask;

				span++;
				++base_type::interpolator();
			} while(--len);
		}
	};
	 */

	//=================================================span_image_resample_rgb
	public class span_image_resample_rgb
		: span_image_resample
	{
		private const int base_mask = 255;
		private const int downscale_shift = (int)ImageFilterLookUpTable.image_filter_scale_e.image_filter_shift;

		//--------------------------------------------------------------------
		public span_image_resample_rgb(IImageBufferAccessor src,
							ISpanInterpolator inter,
							ImageFilterLookUpTable filter) :
			base(src, inter, filter)
		{
			if (src.SourceImage.GetRecieveBlender().NumPixelBits != 24)
			{
				throw new System.FormatException("You have to use a rgb blender with span_image_resample_rgb");
			}
		}

		public override void generate(RGBA_Bytes[] span, int spanIndex, int x, int y, int len)
		{
			ISpanInterpolator spanInterpolator = base.interpolator();
			spanInterpolator.begin(x + base.filter_dx_dbl(), y + base.filter_dy_dbl(), len);

			int[] fg = new int[3];

			byte[] fg_ptr;
			int[] weightArray = filter().weight_array();
			int diameter = (int)base.filter().diameter();
			int filter_scale = diameter << (int)image_subpixel_scale_e.image_subpixel_shift;

			int[] weight_array = weightArray;

			do
			{
				int rx;
				int ry;
				int rx_inv = (int)image_subpixel_scale_e.image_subpixel_scale;
				int ry_inv = (int)image_subpixel_scale_e.image_subpixel_scale;
				spanInterpolator.coordinates(out x, out y);
				spanInterpolator.local_scale(out rx, out ry);
				base.adjust_scale(ref rx, ref ry);

				rx_inv = (int)image_subpixel_scale_e.image_subpixel_scale * (int)image_subpixel_scale_e.image_subpixel_scale / rx;
				ry_inv = (int)image_subpixel_scale_e.image_subpixel_scale * (int)image_subpixel_scale_e.image_subpixel_scale / ry;

				int radius_x = (diameter * rx) >> 1;
				int radius_y = (diameter * ry) >> 1;
				int len_x_lr =
					(diameter * rx + (int)image_subpixel_scale_e.image_subpixel_mask) >>
						(int)(int)image_subpixel_scale_e.image_subpixel_shift;

				x += base.filter_dx_int() - radius_x;
				y += base.filter_dy_int() - radius_y;

				fg[0] = fg[1] = fg[2] = (int)image_filter_scale_e.image_filter_scale / 2;

				int y_lr = y >> (int)(int)image_subpixel_scale_e.image_subpixel_shift;
				int y_hr = (((int)image_subpixel_scale_e.image_subpixel_mask - (y & (int)image_subpixel_scale_e.image_subpixel_mask)) *
							   ry_inv) >> (int)(int)image_subpixel_scale_e.image_subpixel_shift;
				int total_weight = 0;
				int x_lr = x >> (int)(int)image_subpixel_scale_e.image_subpixel_shift;
				int x_hr = (((int)image_subpixel_scale_e.image_subpixel_mask - (x & (int)image_subpixel_scale_e.image_subpixel_mask)) *
							   rx_inv) >> (int)(int)image_subpixel_scale_e.image_subpixel_shift;
				int x_hr2 = x_hr;
				int sourceIndex;
				fg_ptr = base.GetImageBufferAccessor().span(x_lr, y_lr, len_x_lr, out sourceIndex);

				for (; ; )
				{
					int weight_y = weight_array[y_hr];
					x_hr = x_hr2;
					for (; ; )
					{
						int weight = (weight_y * weight_array[x_hr] +
									 (int)image_filter_scale_e.image_filter_scale / 2) >>
									 downscale_shift;
						fg[0] += fg_ptr[sourceIndex + ImageBuffer.OrderR] * weight;
						fg[1] += fg_ptr[sourceIndex + ImageBuffer.OrderG] * weight;
						fg[2] += fg_ptr[sourceIndex + ImageBuffer.OrderB] * weight;
						total_weight += weight;
						x_hr += rx_inv;
						if (x_hr >= filter_scale) break;
						fg_ptr = base.GetImageBufferAccessor().next_x(out sourceIndex);
					}
					y_hr += ry_inv;
					if (y_hr >= filter_scale)
					{
						break;
					}

					fg_ptr = base.GetImageBufferAccessor().next_y(out sourceIndex);
				}

				fg[0] /= total_weight;
				fg[1] /= total_weight;
				fg[2] /= total_weight;

				if (fg[0] < 0) fg[0] = 0;
				if (fg[1] < 0) fg[1] = 0;
				if (fg[2] < 0) fg[2] = 0;

				if (fg[0] > base_mask) fg[0] = base_mask;
				if (fg[1] > base_mask) fg[1] = base_mask;
				if (fg[2] > base_mask) fg[2] = base_mask;

				span[spanIndex].alpha = base_mask;
				span[spanIndex].red = (byte)fg[0];
				span[spanIndex].green = (byte)fg[1];
				span[spanIndex].blue = (byte)fg[2];

				spanIndex++;
				interpolator().Next();
			} while (--len != 0);
		}
	}
}