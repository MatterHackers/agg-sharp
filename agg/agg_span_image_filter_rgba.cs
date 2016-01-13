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
#define USE_UNSAFE_CODE

using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using System;
using image_filter_scale_e = MatterHackers.Agg.ImageFilterLookUpTable.image_filter_scale_e;
using image_subpixel_scale_e = MatterHackers.Agg.ImageFilterLookUpTable.image_subpixel_scale_e;

namespace MatterHackers.Agg
{
	// it should be easy to write a 90 rotating or mirroring filter too. LBB 2012/01/14
	public class span_image_filter_rgba_nn_stepXby1 : span_image_filter
	{
		private const int base_shift = 8;
		private const int base_scale = (int)(1 << base_shift);
		private const int base_mask = base_scale - 1;

		public span_image_filter_rgba_nn_stepXby1(IImageBufferAccessor sourceAccessor, ISpanInterpolator spanInterpolator)
			: base(sourceAccessor, spanInterpolator, null)
		{
		}

		public override void generate(RGBA_Bytes[] span, int spanIndex, int x, int y, int len)
		{
			ImageBuffer SourceRenderingBuffer = (ImageBuffer)GetImageBufferAccessor().SourceImage;
			if (SourceRenderingBuffer.BitDepth != 32)
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
            RGBA_Bytes color = new RGBA_Bytes();
            do
            {
                color.blue = fg_ptr[bufferIndex++];
                color.green = fg_ptr[bufferIndex++];
                color.red = fg_ptr[bufferIndex++];
                color.alpha = fg_ptr[bufferIndex++];
                span[spanIndex++] = color;
            } while (--len != 0);
#endif
		}
	}

	//==============================================span_image_filter_rgba_nn
	public class span_image_filter_rgba_nn : span_image_filter
	{
		private const int baseShift = 8;
		private const int baseScale = (int)(1 << baseShift);
		private const int baseMask = baseScale - 1;

		public span_image_filter_rgba_nn(IImageBufferAccessor sourceAccessor, ISpanInterpolator spanInterpolator)
			: base(sourceAccessor, spanInterpolator, null)
		{
		}

		public override void generate(RGBA_Bytes[] span, int spanIndex, int x, int y, int len)
		{
			ImageBuffer SourceRenderingBuffer = (ImageBuffer)GetImageBufferAccessor().SourceImage;
			if (SourceRenderingBuffer.BitDepth != 32)
			{
				throw new NotSupportedException("The source is expected to be 32 bit.");
			}
			ISpanInterpolator spanInterpolator = interpolator();
			spanInterpolator.begin(x + filter_dx_dbl(), y + filter_dy_dbl(), len);
			byte[] fg_ptr = SourceRenderingBuffer.GetBuffer();
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
				color.alpha = fg_ptr[bufferIndex++];
				span[spanIndex] = color;
				spanIndex++;
				spanInterpolator.Next();
			} while (--len != 0);
		}
	};

	public class span_image_filter_rgba_bilinear : span_image_filter
	{
		private const int base_shift = 8;
		private const int base_scale = (int)(1 << base_shift);
		private const int base_mask = base_scale - 1;

		public span_image_filter_rgba_bilinear(IImageBufferAccessor src, ISpanInterpolator inter)
			: base(src, inter, null)
		{
		}

#if false
            public void generate(out RGBA_Bytes destPixel, int x, int y)
            {
                base.interpolator().begin(x + base.filter_dx_dbl(), y + base.filter_dy_dbl(), 1);

                int* fg = stackalloc int[4];

                byte* fg_ptr;

                IImage imageSource = base.source().DestImage;
                int maxx = (int)imageSource.Width() - 1;
                int maxy = (int)imageSource.Height() - 1;
                ISpanInterpolator spanInterpolator = base.interpolator();

                unchecked
                {
                    int x_hr;
                    int y_hr;

                    spanInterpolator.coordinates(out x_hr, out y_hr);

                    x_hr -= base.filter_dx_int();
                    y_hr -= base.filter_dy_int();

                    int x_lr = x_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;
                    int y_lr = y_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;

                    int weight;

                    fg[0] = fg[1] = fg[2] = fg[3] = (int)image_subpixel_scale_e.image_subpixel_scale * (int)image_subpixel_scale_e.image_subpixel_scale / 2;

                    x_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;
                    y_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;

                    fg_ptr = imageSource.GetPixelPointerY(y_lr) + (x_lr * 4);

                    weight = (int)(((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) *
                             ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
                    fg[0] += weight * fg_ptr[0];
                    fg[1] += weight * fg_ptr[1];
                    fg[2] += weight * fg_ptr[2];
                    fg[3] += weight * fg_ptr[3];

                    weight = (int)(x_hr * ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
                    fg[0] += weight * fg_ptr[4];
                    fg[1] += weight * fg_ptr[5];
                    fg[2] += weight * fg_ptr[6];
                    fg[3] += weight * fg_ptr[7];

                    ++y_lr;
                    fg_ptr = imageSource.GetPixelPointerY(y_lr) + (x_lr * 4);

                    weight = (int)(((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) * y_hr);
                    fg[0] += weight * fg_ptr[0];
                    fg[1] += weight * fg_ptr[1];
                    fg[2] += weight * fg_ptr[2];
                    fg[3] += weight * fg_ptr[3];

                    weight = (int)(x_hr * y_hr);
                    fg[0] += weight * fg_ptr[4];
                    fg[1] += weight * fg_ptr[5];
                    fg[2] += weight * fg_ptr[6];
                    fg[3] += weight * fg_ptr[7];

                    fg[0] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
                    fg[1] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
                    fg[2] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
                    fg[3] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;

                    destPixel.m_R = (byte)fg[OrderR];
                    destPixel.m_G = (byte)fg[OrderG];
                    destPixel.m_B = (byte)fg[ImageBuffer.OrderB];
                    destPixel.m_A = (byte)fg[OrderA];
                }
            }
#endif

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
					int tempA;

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
					tempB =
					tempA = (int)image_subpixel_scale_e.image_subpixel_scale * (int)image_subpixel_scale_e.image_subpixel_scale / 2;

					x_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;
					y_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;

					bufferIndex = SourceRenderingBuffer.GetBufferOffsetXY(x_lr, y_lr);

					weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) *
							 ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
					tempA += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];
					bufferIndex += 4;

					weight = (x_hr * ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
					tempA += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];

					y_lr++;
					bufferIndex = SourceRenderingBuffer.GetBufferOffsetXY(x_lr, y_lr);

					weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) * y_hr);
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
					tempA += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];
					bufferIndex += 4;

					weight = (x_hr * y_hr);
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
					tempA += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];

					tempR >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
					tempG >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
					tempB >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
					tempA >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;

					RGBA_Bytes color;
					color.red = (byte)tempR;
					color.green = (byte)tempG;
					color.blue = (byte)tempB;
					color.alpha = (byte)255;// tempA;
					span[spanIndex] = color;
					spanIndex++;
					spanInterpolator.Next();
				} while (--len != 0);
			}
		}
	}

	public class span_image_filter_rgba_bilinear_float : span_image_filter_float
	{
		public span_image_filter_rgba_bilinear_float(IImageBufferAccessorFloat src, ISpanInterpolatorFloat inter)
			: base(src, inter, null)
		{
		}

		public override void generate(RGBA_Floats[] span, int spanIndex, int x, int y, int len)
		{
			base.interpolator().begin(x + base.filter_dx_dbl(), y + base.filter_dy_dbl(), len);

			ImageBufferFloat SourceRenderingBuffer = (ImageBufferFloat)base.source().SourceImage;
			ISpanInterpolatorFloat spanInterpolator = base.interpolator();
			int bufferIndex;
			float[] fg_ptr = SourceRenderingBuffer.GetBuffer(out bufferIndex);

			unchecked
			{
				do
				{
					float tempR;
					float tempG;
					float tempB;
					float tempA;

					float x_hr;
					float y_hr;

					spanInterpolator.coordinates(out x_hr, out y_hr);

					x_hr -= base.filter_dx_dbl();
					y_hr -= base.filter_dy_dbl();

					int x_lr = (int)x_hr;
					int y_lr = (int)y_hr;
					float weight;

					tempR = tempG = tempB = tempA = 0;

					x_hr -= x_lr;
					y_hr -= y_lr;

					bufferIndex = SourceRenderingBuffer.GetBufferOffsetXY(x_lr, y_lr);

#if false
                    unsafe
                    {
                        fixed (float* pSource = fg_ptr)
                        {
                            Vector4f tempFinal = new Vector4f(0.0f, 0.0f, 0.0f, 0.0f);

                            Vector4f color0 = Vector4f.LoadAligned((Vector4f*)&pSource[bufferIndex + 0]);
                            weight = (1.0f - x_hr) * (1.0f - y_hr);
                            Vector4f weight4f = new Vector4f(weight, weight, weight, weight);
                            tempFinal = tempFinal + weight4f * color0;

                            Vector4f color1 = Vector4f.LoadAligned((Vector4f*)&pSource[bufferIndex + 4]);
                            weight = (x_hr) * (1.0f - y_hr);
                            weight4f = new Vector4f(weight, weight, weight, weight);
                            tempFinal = tempFinal + weight4f * color1;

                            y_lr++;
                            bufferIndex = SourceRenderingBuffer.GetBufferOffsetXY(x_lr, y_lr);

                            Vector4f color2 = Vector4f.LoadAligned((Vector4f*)&pSource[bufferIndex + 0]);
                            weight = (1.0f - x_hr) * (y_hr);
                            weight4f = new Vector4f(weight, weight, weight, weight);
                            tempFinal = tempFinal + weight4f * color2;

                            Vector4f color3 = Vector4f.LoadAligned((Vector4f*)&pSource[bufferIndex + 4]);
                            weight = (x_hr) * (y_hr);
                            weight4f = new Vector4f(weight, weight, weight, weight);
                            tempFinal = tempFinal + weight4f * color3;

                            RGBA_Floats color;
                            color.m_B = tempFinal.X;
                            color.m_G = tempFinal.Y;
                            color.m_R = tempFinal.Z;
                            color.m_A = tempFinal.W;
                            span[spanIndex] = color;
                        }
                    }
#else
					weight = (1.0f - x_hr) * (1.0f - y_hr);
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
					tempA += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];
					bufferIndex += 4;

					weight = (x_hr) * (1.0f - y_hr);
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
					tempA += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];

					y_lr++;
					bufferIndex = SourceRenderingBuffer.GetBufferOffsetXY(x_lr, y_lr);

					weight = (1.0f - x_hr) * (y_hr);
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
					tempA += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];
					bufferIndex += 4;

					weight = (x_hr) * (y_hr);
					tempR += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					tempG += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					tempB += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
					tempA += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];

					RGBA_Floats color;
					color.red = tempR;
					color.green = tempG;
					color.blue = tempB;
					color.alpha = tempA;
					span[spanIndex] = color;
#endif
					spanIndex++;
					spanInterpolator.Next();
				} while (--len != 0);
			}
		}
	};

	//====================================span_image_filter_rgba_bilinear_clip
	public class span_image_filter_rgba_bilinear_clip : span_image_filter
	{
		private RGBA_Bytes m_OutsideSourceColor;

		private const int base_shift = 8;
		private const int base_scale = (int)(1 << base_shift);
		private const int base_mask = base_scale - 1;

		public span_image_filter_rgba_bilinear_clip(IImageBufferAccessor src,
			IColorType back_color, ISpanInterpolator inter)
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
			ImageBuffer SourceRenderingBuffer = (ImageBuffer)base.GetImageBufferAccessor().SourceImage;
			int bufferIndex;
			byte[] fg_ptr;

			if (base.m_interpolator.GetType() == typeof(MatterHackers.Agg.span_interpolator_linear)
				&& ((MatterHackers.Agg.span_interpolator_linear)base.m_interpolator).transformer().GetType() == typeof(MatterHackers.Agg.Transform.Affine)
			&& ((MatterHackers.Agg.Transform.Affine)((MatterHackers.Agg.span_interpolator_linear)base.m_interpolator).transformer()).is_identity())
			{
				fg_ptr = SourceRenderingBuffer.GetPixelPointerXY(x, y, out bufferIndex);
				//unsafe
				{
#if true
					do
					{
						span[spanIndex].blue = (byte)fg_ptr[bufferIndex++];
						span[spanIndex].green = (byte)fg_ptr[bufferIndex++];
						span[spanIndex].red = (byte)fg_ptr[bufferIndex++];
						span[spanIndex].alpha = (byte)fg_ptr[bufferIndex++];
						++spanIndex;
					} while (--len != 0);
#else
                        fixed (byte* pSource = &fg_ptr[bufferIndex])
                        {
                            int* pSourceInt = (int*)pSource;
                            fixed (RGBA_Bytes* pDest = &span[spanIndex])
                            {
                                int* pDestInt = (int*)pDest;
                                do
                                {
                                    *pDestInt++ = *pSourceInt++;
                                } while (--len != 0);
                            }
                        }
#endif
				}

				return;
			}

			base.interpolator().begin(x + base.filter_dx_dbl(), y + base.filter_dy_dbl(), len);

			int[] accumulatedColor = new int[4];

			int back_r = m_OutsideSourceColor.red;
			int back_g = m_OutsideSourceColor.green;
			int back_b = m_OutsideSourceColor.blue;
			int back_a = m_OutsideSourceColor.alpha;

			int distanceBetweenPixelsInclusive = base.GetImageBufferAccessor().SourceImage.GetBytesBetweenPixelsInclusive();
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
						accumulatedColor[2] =
						accumulatedColor[3] = (int)image_subpixel_scale_e.image_subpixel_scale * (int)image_subpixel_scale_e.image_subpixel_scale / 2;

						x_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;
						y_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;

						fg_ptr = SourceRenderingBuffer.GetPixelPointerXY(x_lr, y_lr, out bufferIndex);

						weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) *
								 ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
						if (weight > base_mask)
						{
							accumulatedColor[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
							accumulatedColor[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
							accumulatedColor[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
							accumulatedColor[3] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];
						}

						weight = (x_hr * ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
						if (weight > base_mask)
						{
							bufferIndex += distanceBetweenPixelsInclusive;
							accumulatedColor[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
							accumulatedColor[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
							accumulatedColor[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
							accumulatedColor[3] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];
						}

						weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) * y_hr);
						if (weight > base_mask)
						{
							++y_lr;
							fg_ptr = SourceRenderingBuffer.GetPixelPointerXY(x_lr, y_lr, out bufferIndex);
							accumulatedColor[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
							accumulatedColor[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
							accumulatedColor[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
							accumulatedColor[3] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];
						}
						weight = (x_hr * y_hr);
						if (weight > base_mask)
						{
							bufferIndex += distanceBetweenPixelsInclusive;
							accumulatedColor[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
							accumulatedColor[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
							accumulatedColor[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
							accumulatedColor[3] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];
						}
						accumulatedColor[0] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
						accumulatedColor[1] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
						accumulatedColor[2] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
						accumulatedColor[3] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
					}
					else
					{
						if (x_lr < -1 || y_lr < -1 ||
						   x_lr > maxx || y_lr > maxy)
						{
							accumulatedColor[0] = back_r;
							accumulatedColor[1] = back_g;
							accumulatedColor[2] = back_b;
							accumulatedColor[3] = back_a;
						}
						else
						{
							accumulatedColor[0] =
							accumulatedColor[1] =
							accumulatedColor[2] =
							accumulatedColor[3] = (int)image_subpixel_scale_e.image_subpixel_scale * (int)image_subpixel_scale_e.image_subpixel_scale / 2;

							x_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;
							y_hr &= (int)image_subpixel_scale_e.image_subpixel_mask;

							weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) *
									 ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
							if (weight > base_mask)
							{
								BlendInFilterPixel(accumulatedColor, back_r, back_g, back_b, back_a, SourceRenderingBuffer, maxx, maxy, x_lr, y_lr, weight);
							}

							x_lr++;

							weight = (x_hr * ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
							if (weight > base_mask)
							{
								BlendInFilterPixel(accumulatedColor, back_r, back_g, back_b, back_a, SourceRenderingBuffer, maxx, maxy, x_lr, y_lr, weight);
							}

							x_lr--;
							y_lr++;

							weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) * y_hr);
							if (weight > base_mask)
							{
								BlendInFilterPixel(accumulatedColor, back_r, back_g, back_b, back_a, SourceRenderingBuffer, maxx, maxy, x_lr, y_lr, weight);
							}

							x_lr++;

							weight = (x_hr * y_hr);
							if (weight > base_mask)
							{
								BlendInFilterPixel(accumulatedColor, back_r, back_g, back_b, back_a, SourceRenderingBuffer, maxx, maxy, x_lr, y_lr, weight);
							}

							accumulatedColor[0] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
							accumulatedColor[1] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
							accumulatedColor[2] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
							accumulatedColor[3] >>= (int)image_subpixel_scale_e.image_subpixel_shift * 2;
						}
					}

					span[spanIndex].red = (byte)accumulatedColor[0];
					span[spanIndex].green = (byte)accumulatedColor[1];
					span[spanIndex].blue = (byte)accumulatedColor[2];
					span[spanIndex].alpha = (byte)accumulatedColor[3];
					++spanIndex;
					spanInterpolator.Next();
				} while (--len != 0);
			}
		}

		private void BlendInFilterPixel(int[] accumulatedColor, int back_r, int back_g, int back_b, int back_a, IImageByte SourceRenderingBuffer, int maxx, int maxy, int x_lr, int y_lr, int weight)
		{
			byte[] fg_ptr;
			unchecked
			{
				if ((uint)x_lr <= (uint)maxx && (uint)y_lr <= (uint)maxy)
				{
					int bufferIndex = SourceRenderingBuffer.GetBufferOffsetXY(x_lr, y_lr);
					fg_ptr = SourceRenderingBuffer.GetBuffer();

					accumulatedColor[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
					accumulatedColor[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
					accumulatedColor[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
					accumulatedColor[3] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];
				}
				else
				{
					accumulatedColor[0] += back_r * weight;
					accumulatedColor[1] += back_g * weight;
					accumulatedColor[2] += back_b * weight;
					accumulatedColor[3] += back_a * weight;
				}
			}
		}
	};

	/*

	//==============================================span_image_filter_rgba_2x2
	//template<class Source, class Interpolator>
	public class span_image_filter_rgba_2x2 : span_image_filter//<Source, Interpolator>
	{
		//typedef Source source_type;
		//typedef typename source_type::color_type color_type;
		//typedef typename source_type::order_type order_type;
		//typedef Interpolator interpolator_type;
		//typedef span_image_filter<source_type, interpolator_type> base_type;
		//typedef typename color_type::value_type value_type;
		//typedef typename color_type::calc_type calc_type;
		enum base_scale_e
		{
			base_shift = 8, //color_type::base_shift,
			base_mask  = 255,//color_type::base_mask
		};

		//--------------------------------------------------------------------
		public span_image_filter_rgba_2x2() {}
		public span_image_filter_rgba_2x2(pixfmt_alpha_blend_bgra32 src,
								   interpolator_type inter,
								   ImageFilterLookUpTable filter) :
			base(src, inter, filter)
		{}

		//--------------------------------------------------------------------
		public void generate(color_type* span, int x, int y, unsigned len)
		{
			base.interpolator().begin(x + base.filter_dx_dbl(),
											y + base.filter_dy_dbl(), len);

			calc_type fg[4];

			byte *fg_ptr;
			int16* weight_array = base.filter().weight_array() +
										((base.filter().diameter()/2 - 1) <<
										  image_subpixel_shift);

			do
			{
				int x_hr;
				int y_hr;

				base.interpolator().coordinates(&x_hr, &y_hr);

				x_hr -= base.filter_dx_int();
				y_hr -= base.filter_dy_int();

				int x_lr = x_hr >> image_subpixel_shift;
				int y_lr = y_hr >> image_subpixel_shift;

				unsigned weight;
				fg[0] = fg[1] = fg[2] = fg[3] = (int)image_filter_scale_e.image_filter_scale / 2;

				x_hr &= image_subpixel_mask;
				y_hr &= image_subpixel_mask;

				fg_ptr = base.source().span(x_lr, y_lr, 2);
				weight = (weight_array[x_hr + image_subpixel_scale] *
						  weight_array[y_hr + image_subpixel_scale] +
						  (int)image_filter_scale_e.image_filter_scale / 2) >>
						  image_filter_shift;
				fg[0] += weight * *fg_ptr++;
				fg[1] += weight * *fg_ptr++;
				fg[2] += weight * *fg_ptr++;
				fg[3] += weight * *fg_ptr;

				fg_ptr = base.source().next_x();
				weight = (weight_array[x_hr] *
						  weight_array[y_hr + image_subpixel_scale] +
						  (int)image_filter_scale_e.image_filter_scale / 2) >>
						  image_filter_shift;
				fg[0] += weight * *fg_ptr++;
				fg[1] += weight * *fg_ptr++;
				fg[2] += weight * *fg_ptr++;
				fg[3] += weight * *fg_ptr;

				fg_ptr = base.source().next_y();
				weight = (weight_array[x_hr + image_subpixel_scale] *
						  weight_array[y_hr] +
						  (int)image_filter_scale_e.image_filter_scale / 2) >>
						  image_filter_shift;
				fg[0] += weight * *fg_ptr++;
				fg[1] += weight * *fg_ptr++;
				fg[2] += weight * *fg_ptr++;
				fg[3] += weight * *fg_ptr;

				fg_ptr = base.source().next_x();
				weight = (weight_array[x_hr] *
						  weight_array[y_hr] +
						  (int)image_filter_scale_e.image_filter_scale / 2) >>
						  image_filter_shift;
				fg[0] += weight * *fg_ptr++;
				fg[1] += weight * *fg_ptr++;
				fg[2] += weight * *fg_ptr++;
				fg[3] += weight * *fg_ptr;

				fg[0] >>= image_filter_shift;
				fg[1] >>= image_filter_shift;
				fg[2] >>= image_filter_shift;
				fg[3] >>= image_filter_shift;

				if(fg[ImageBuffer.OrderA] > base_mask)         fg[ImageBuffer.OrderA] = base_mask;
				if(fg[ImageBuffer.OrderR] > fg[ImageBuffer.OrderA]) fg[ImageBuffer.OrderR] = fg[ImageBuffer.OrderA];
				if(fg[ImageBuffer.OrderG] > fg[ImageBuffer.OrderA]) fg[ImageBuffer.OrderG] = fg[ImageBuffer.OrderA];
				if(fg[ImageBuffer.OrderB] > fg[ImageBuffer.OrderA]) fg[ImageBuffer.OrderB] = fg[ImageBuffer.OrderA];

				span->r = (byte)fg[ImageBuffer.OrderR];
				span->g = (byte)fg[ImageBuffer.OrderG];
				span->b = (byte)fg[ImageBuffer.OrderB];
				span->a = (byte)fg[ImageBuffer.OrderA];
				++span;
				++base.interpolator();
			} while(--len);
		}
	};
*/

	public class span_image_filter_rgba : span_image_filter
	{
		private const int base_mask = 255;

		//--------------------------------------------------------------------
		public span_image_filter_rgba(IImageBufferAccessor src, ISpanInterpolator inter, ImageFilterLookUpTable filter)
			: base(src, inter, filter)
		{
			if (src.SourceImage.GetBytesBetweenPixelsInclusive() != 4)
			{
				throw new System.NotSupportedException("span_image_filter_rgba must have a 32 bit DestImage");
			}
		}

		public override void generate(RGBA_Bytes[] span, int spanIndex, int x, int y, int len)
		{
			base.interpolator().begin(x + base.filter_dx_dbl(), y + base.filter_dy_dbl(), len);

			int f_r, f_g, f_b, f_a;

			byte[] fg_ptr;

			int diameter = m_filter.diameter();
			int start = m_filter.start();
			int[] weight_array = m_filter.weight_array();

			int x_count;
			int weight_y;

			ISpanInterpolator spanInterpolator = base.interpolator();
			IImageBufferAccessor sourceAccessor = GetImageBufferAccessor();

			do
			{
				spanInterpolator.coordinates(out x, out y);

				x -= base.filter_dx_int();
				y -= base.filter_dy_int();

				int x_hr = x;
				int y_hr = y;

				int x_lr = x_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;
				int y_lr = y_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;

				f_b = f_g = f_r = f_a = (int)image_filter_scale_e.image_filter_scale / 2;

				int x_fract = x_hr & (int)image_subpixel_scale_e.image_subpixel_mask;
				int y_count = diameter;

				y_hr = (int)image_subpixel_scale_e.image_subpixel_mask - (y_hr & (int)image_subpixel_scale_e.image_subpixel_mask);

				int bufferIndex;
				fg_ptr = sourceAccessor.span(x_lr + start, y_lr + start, diameter, out bufferIndex);
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
						f_a += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];

						if (--x_count == 0) break;
						x_hr += (int)image_subpixel_scale_e.image_subpixel_scale;
						sourceAccessor.next_x(out bufferIndex);
					}

					if (--y_count == 0) break;
					y_hr += (int)image_subpixel_scale_e.image_subpixel_scale;
					fg_ptr = sourceAccessor.next_y(out bufferIndex);
				}

				f_b >>= (int)image_filter_scale_e.image_filter_shift;
				f_g >>= (int)image_filter_scale_e.image_filter_shift;
				f_r >>= (int)image_filter_scale_e.image_filter_shift;
				f_a >>= (int)image_filter_scale_e.image_filter_shift;

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

					if ((uint)f_a > base_mask)
					{
						if (f_a < 0) f_a = 0;
						if (f_a > base_mask) f_a = (int)base_mask;
					}
				}

				span[spanIndex].red = (byte)f_b;
				span[spanIndex].green = (byte)f_g;
				span[spanIndex].blue = (byte)f_r;
				span[spanIndex].alpha = (byte)f_a;

				spanIndex++;
				spanInterpolator.Next();
			} while (--len != 0);
		}
	};

	public class span_image_filter_rgba_float : span_image_filter_float
	{
		public span_image_filter_rgba_float(IImageBufferAccessorFloat src, ISpanInterpolatorFloat inter, IImageFilterFunction filterFunction)
			: base(src, inter, filterFunction)
		{
			if (src.SourceImage.GetFloatsBetweenPixelsInclusive() != 4)
			{
				throw new System.NotSupportedException("span_image_filter_rgba must have a 32 bit DestImage");
			}
		}

		public override void generate(RGBA_Floats[] span, int spanIndex, int xInt, int yInt, int len)
		{
			base.interpolator().begin(xInt + base.filter_dx_dbl(), yInt + base.filter_dy_dbl(), len);

			float f_r, f_g, f_b, f_a;

			float[] fg_ptr;

			int radius = (int)m_filterFunction.radius();
			int diameter = radius * 2;
			int start = -(int)(diameter / 2 - 1);

			int x_count;

			ISpanInterpolatorFloat spanInterpolator = base.interpolator();
			IImageBufferAccessorFloat sourceAccessor = source();

			do
			{
				float x = xInt;
				float y = yInt;
				spanInterpolator.coordinates(out x, out y);
				//x -= (float)base.filter_dx_dbl();
				//y -= (float)base.filter_dy_dbl();
				int sourceXInt = (int)x;
				int sourceYInt = (int)y;
				Vector2 sourceOrigin = new Vector2(x, y);
				Vector2 sourceSample = new Vector2(sourceXInt + start, sourceYInt + start);

				f_b = f_g = f_r = f_a = 0;

				int y_count = diameter;

				int bufferIndex;
				fg_ptr = sourceAccessor.span(sourceXInt + start, sourceYInt + start, diameter, out bufferIndex);
				float totalWeight = 0.0f;
				for (; ; )
				{
					float yweight = (float)m_filterFunction.calc_weight(System.Math.Sqrt((sourceSample.y - sourceOrigin.y) * (sourceSample.y - sourceOrigin.y)));
					x_count = (int)diameter;
					for (; ; )
					{
						float xweight = (float)m_filterFunction.calc_weight(System.Math.Sqrt((sourceSample.x - sourceOrigin.x) * (sourceSample.x - sourceOrigin.x)));
						float weight = xweight * yweight;

						f_r += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
						f_g += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
						f_b += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
						f_a += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];

						totalWeight += weight;
						sourceSample.x += 1;
						if (--x_count == 0) break;
						sourceAccessor.next_x(out bufferIndex);
					}

					sourceSample.x -= diameter;

					if (--y_count == 0) break;
					sourceSample.y += 1;
					fg_ptr = sourceAccessor.next_y(out bufferIndex);
				}

				if (f_b < 0) f_b = 0; if (f_b > 1) f_b = 1;
				if (f_r < 0) f_r = 0; if (f_r > 1) f_r = 1;
				if (f_g < 0) f_g = 0; if (f_g > 1) f_g = 1;

				span[spanIndex].red = f_r;
				span[spanIndex].green = f_g;
				span[spanIndex].blue = f_b;
				span[spanIndex].alpha = 1;// f_a;

				spanIndex++;
				spanInterpolator.Next();
			} while (--len != 0);
		}
	};

	/*

		//========================================span_image_resample_rgba_affine
		public class span_image_resample_rgba_affine : span_image_resample_affine
		{
			//typedef Source source_type;
			//typedef typename source_type::color_type color_type;
			//typedef typename source_type::order_type order_type;
			//typedef span_image_resample_affine<source_type> base_type;
			//typedef typename base.interpolator_type interpolator_type;
			//typedef typename color_type::value_type value_type;
			//typedef typename color_type::long_type long_type;
			enum base_scale_e
			{
				base_shift      = 8, //color_type::base_shift,
				base_mask       = 255,//color_type::base_mask,
				downscale_shift = image_filter_shift
			};

			//--------------------------------------------------------------------
			public span_image_resample_rgba_affine() {}
			public span_image_resample_rgba_affine(pixfmt_alpha_blend_bgra32 src,
											interpolator_type inter,
											ImageFilterLookUpTable filter) :
				base(src, inter, filter)
			{}

			//--------------------------------------------------------------------
			public void generate(color_type* span, int x, int y, unsigned len)
			{
				base.interpolator().begin(x + base.filter_dx_dbl(),
												y + base.filter_dy_dbl(), len);

				long_type fg[4];

				int diameter     = base.filter().diameter();
				int filter_scale = diameter << image_subpixel_shift;
				int radius_x     = (diameter * base.m_rx) >> 1;
				int radius_y     = (diameter * base.m_ry) >> 1;
				int len_x_lr     =
					(diameter * base.m_rx + image_subpixel_mask) >>
						image_subpixel_shift;

				int16* weight_array = base.filter().weight_array();

				do
				{
					base.interpolator().coordinates(&x, &y);

					x += base.filter_dx_int() - radius_x;
					y += base.filter_dy_int() - radius_y;

					fg[0] = fg[1] = fg[2] = fg[3] = (int)image_filter_scale_e.image_filter_scale / 2;

					int y_lr = y >> image_subpixel_shift;
					int y_hr = ((image_subpixel_mask - (y & image_subpixel_mask)) *
									base.m_ry_inv) >>
										image_subpixel_shift;
					int total_weight = 0;
					int x_lr = x >> image_subpixel_shift;
					int x_hr = ((image_subpixel_mask - (x & image_subpixel_mask)) *
									base.m_rx_inv) >>
										image_subpixel_shift;

					int x_hr2 = x_hr;
					byte* fg_ptr = base.source().span(x_lr, y_lr, len_x_lr);
					for(;;)
					{
						int weight_y = weight_array[y_hr];
						x_hr = x_hr2;
						for(;;)
						{
							int weight = (weight_y * weight_array[x_hr] +
										 (int)image_filter_scale_e.image_filter_scale / 2) >>
										 downscale_shift;

							fg[0] += *fg_ptr++ * weight;
							fg[1] += *fg_ptr++ * weight;
							fg[2] += *fg_ptr++ * weight;
							fg[3] += *fg_ptr++ * weight;
							total_weight += weight;
							x_hr  += base.m_rx_inv;
							if(x_hr >= filter_scale) break;
							fg_ptr = base.source().next_x();
						}
						y_hr += base.m_ry_inv;
						if(y_hr >= filter_scale) break;
						fg_ptr = base.source().next_y();
					}

					fg[0] /= total_weight;
					fg[1] /= total_weight;
					fg[2] /= total_weight;
					fg[3] /= total_weight;

					if(fg[0] < 0) fg[0] = 0;
					if(fg[1] < 0) fg[1] = 0;
					if(fg[2] < 0) fg[2] = 0;
					if(fg[3] < 0) fg[3] = 0;

					if(fg[ImageBuffer.OrderA] > base_mask)         fg[ImageBuffer.OrderA] = base_mask;
					if(fg[ImageBuffer.OrderR] > fg[ImageBuffer.OrderA]) fg[ImageBuffer.OrderR] = fg[ImageBuffer.OrderA];
					if(fg[ImageBuffer.OrderG] > fg[ImageBuffer.OrderA]) fg[ImageBuffer.OrderG] = fg[ImageBuffer.OrderA];
					if(fg[ImageBuffer.OrderB] > fg[ImageBuffer.OrderA]) fg[ImageBuffer.OrderB] = fg[ImageBuffer.OrderA];

					span->r = (byte)fg[ImageBuffer.OrderR];
					span->g = (byte)fg[ImageBuffer.OrderG];
					span->b = (byte)fg[ImageBuffer.OrderB];
					span->a = (byte)fg[ImageBuffer.OrderA];

					++span;
					++base.interpolator();
				} while(--len);
			}
		};
		 */

	//==============================================span_image_resample_rgba
	public class span_image_resample_rgba
		: span_image_resample
	{
		private const int base_mask = 255;
		private const int downscale_shift = (int)ImageFilterLookUpTable.image_filter_scale_e.image_filter_shift;

		//--------------------------------------------------------------------
		public span_image_resample_rgba(IImageBufferAccessor src,
							ISpanInterpolator inter,
							ImageFilterLookUpTable filter) :
			base(src, inter, filter)
		{
			if (src.SourceImage.GetRecieveBlender().NumPixelBits != 32)
			{
				throw new System.FormatException("You have to use a rgba blender with span_image_resample_rgba");
			}
		}

		//--------------------------------------------------------------------
		public override void generate(RGBA_Bytes[] span, int spanIndex, int x, int y, int len)
		{
			ISpanInterpolator spanInterpolator = base.interpolator();
			spanInterpolator.begin(x + base.filter_dx_dbl(), y + base.filter_dy_dbl(), len);

			int[] fg = new int[4];

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

				fg[0] = fg[1] = fg[2] = fg[3] = (int)image_filter_scale_e.image_filter_scale / 2;

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
						fg[3] += fg_ptr[sourceIndex + ImageBuffer.OrderA] * weight;
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
				fg[3] /= total_weight;

				if (fg[0] < 0) fg[0] = 0;
				if (fg[1] < 0) fg[1] = 0;
				if (fg[2] < 0) fg[2] = 0;
				if (fg[3] < 0) fg[3] = 0;

				if (fg[0] > base_mask) fg[0] = base_mask;
				if (fg[1] > base_mask) fg[1] = base_mask;
				if (fg[2] > base_mask) fg[2] = base_mask;
				if (fg[3] > base_mask) fg[3] = base_mask;

				span[spanIndex].red = (byte)fg[0];
				span[spanIndex].green = (byte)fg[1];
				span[spanIndex].blue = (byte)fg[2];
				span[spanIndex].alpha = (byte)fg[3];

				spanIndex++;
				interpolator().Next();
			} while (--len != 0);
		}

		/*
					ISpanInterpolator spanInterpolator = base.interpolator();
					spanInterpolator.begin(x + base.filter_dx_dbl(), y + base.filter_dy_dbl(), len);

					int* fg = stackalloc int[4];

					byte* fg_ptr;
					fixed (int* pWeightArray = filter().weight_array())
					{
						int diameter = (int)base.filter().diameter();
						int filter_scale = diameter << (int)image_subpixel_scale_e.image_subpixel_shift;

						int* weight_array = pWeightArray;

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

							fg[0] = fg[1] = fg[2] = fg[3] = (int)image_filter_scale_e.image_filter_scale / 2;

							int y_lr = y >> (int)(int)image_subpixel_scale_e.image_subpixel_shift;
							int y_hr = (((int)image_subpixel_scale_e.image_subpixel_mask - (y & (int)image_subpixel_scale_e.image_subpixel_mask)) *
										   ry_inv) >>
											   (int)(int)image_subpixel_scale_e.image_subpixel_shift;
							int total_weight = 0;
							int x_lr = x >> (int)(int)image_subpixel_scale_e.image_subpixel_shift;
							int x_hr = (((int)image_subpixel_scale_e.image_subpixel_mask - (x & (int)image_subpixel_scale_e.image_subpixel_mask)) *
										   rx_inv) >>
											   (int)(int)image_subpixel_scale_e.image_subpixel_shift;
							int x_hr2 = x_hr;
							fg_ptr = base.source().span(x_lr, y_lr, (int)len_x_lr);

							for(;;)
							{
								int weight_y = weight_array[y_hr];
								x_hr = x_hr2;
								for(;;)
								{
									int weight = (weight_y * weight_array[x_hr] +
												 (int)image_filter_scale_e.image_filter_scale / 2) >>
												 downscale_shift;
									fg[0] += *fg_ptr++ * weight;
									fg[1] += *fg_ptr++ * weight;
									fg[2] += *fg_ptr++ * weight;
									fg[3] += *fg_ptr++ * weight;
									total_weight += weight;
									x_hr  += rx_inv;
									if(x_hr >= filter_scale) break;
									fg_ptr = base.source().next_x();
								}
								y_hr += ry_inv;
								if (y_hr >= filter_scale)
								{
									break;
								}

								fg_ptr = base.source().next_y();
							}

							fg[0] /= total_weight;
							fg[1] /= total_weight;
							fg[2] /= total_weight;
							fg[3] /= total_weight;

							if(fg[0] < 0) fg[0] = 0;
							if(fg[1] < 0) fg[1] = 0;
							if(fg[2] < 0) fg[2] = 0;
							if(fg[3] < 0) fg[3] = 0;

							if(fg[0] > fg[0]) fg[0] = fg[0];
							if(fg[1] > fg[1]) fg[1] = fg[1];
							if(fg[2] > fg[2]) fg[2] = fg[2];
							if (fg[3] > base_mask) fg[3] = base_mask;

							span->R_Byte = (byte)fg[ImageBuffer.OrderR];
							span->G_Byte = (byte)fg[ImageBuffer.OrderG];
							span->B_Byte = (byte)fg[ImageBuffer.OrderB];
							span->A_Byte = (byte)fg[ImageBuffer.OrderA];

							++span;
							interpolator().Next();
						} while(--len != 0);
					}
															  */
	};
}

//#endif