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

		public override void generate(Color[] span, int spanIndex, int x, int y, int len)
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
						span[spanIndex++] = *(Color*)&(pSource[bufferIndex]);
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

		public override void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			IImageBufferAccessor source = GetImageBufferAccessor();
			if (source.SourceImage.BitDepth != 32)
			{
				throw new NotSupportedException("The source is expected to be 32 bit.");
			}
			ISpanInterpolator spanInterpolator = interpolator();
			spanInterpolator.begin(x + filter_dx_dbl(), y + filter_dy_dbl(), len);
			do
			{
				int x_hr;
				int y_hr;
				spanInterpolator.coordinates(out x_hr, out y_hr);
				int x_lr = x_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;
				int y_lr = y_hr >> (int)image_subpixel_scale_e.image_subpixel_shift;
				// Through the accessor, as C++ does: outside the image it decides what is read (the edge pixel, the
				// background, a wrapped pixel), where indexing the buffer read the wrong pixel or past its end.
				byte[] fg_ptr = source.span(x_lr, y_lr, 1, out int bufferIndex);
				Color color;
				color.red = fg_ptr[bufferIndex + ImageBuffer.OrderR];
				color.green = fg_ptr[bufferIndex + ImageBuffer.OrderG];
				color.blue = fg_ptr[bufferIndex + ImageBuffer.OrderB];
				color.alpha = fg_ptr[bufferIndex + ImageBuffer.OrderA];
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

		/// <summary>
		/// C++'s span_image_filter_rgba_bilinear: each pixel weighs the 2x2 block under it, read through the image
		/// accessor as C++ does, so a clamp, wrap or clip accessor decides what lies past the edges, and alpha is
		/// filtered like the color channels (SpanImageFilterRgbaBilinearTests pins both).
		/// </summary>
		public override void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			const int subpixelShift = (int)image_subpixel_scale_e.image_subpixel_shift;
			const int subpixelScale = (int)image_subpixel_scale_e.image_subpixel_scale;
			const int subpixelMask = (int)image_subpixel_scale_e.image_subpixel_mask;

			ISpanInterpolator spanInterpolator = interpolator();
			IImageBufferAccessor source = GetImageBufferAccessor();
			spanInterpolator.begin(x + filter_dx_dbl(), y + filter_dy_dbl(), len);
			var fg = new int[4];
			do
			{
				spanInterpolator.coordinates(out int x_hr, out int y_hr);
				x_hr -= filter_dx_int();
				y_hr -= filter_dy_int();

				int x_lr = x_hr >> subpixelShift;
				int y_lr = y_hr >> subpixelShift;

				// Half the total weight, so the downshift rounds.
				fg[0] = fg[1] = fg[2] = fg[3] = subpixelScale * subpixelScale / 2;

				x_hr &= subpixelMask;
				y_hr &= subpixelMask;

				byte[] buffer = source.span(x_lr, y_lr, 2, out int offset);
				AddWeighted(fg, (subpixelScale - x_hr) * (subpixelScale - y_hr), buffer, offset);

				buffer = source.next_x(out offset);
				AddWeighted(fg, x_hr * (subpixelScale - y_hr), buffer, offset);

				buffer = source.next_y(out offset);
				AddWeighted(fg, (subpixelScale - x_hr) * y_hr, buffer, offset);

				buffer = source.next_x(out offset);
				AddWeighted(fg, x_hr * y_hr, buffer, offset);

				// fg is indexed by source byte order.
				span[spanIndex].red = (byte)(fg[ImageBuffer.OrderR] >> (subpixelShift * 2));
				span[spanIndex].green = (byte)(fg[ImageBuffer.OrderG] >> (subpixelShift * 2));
				span[spanIndex].blue = (byte)(fg[ImageBuffer.OrderB] >> (subpixelShift * 2));
				span[spanIndex].alpha = (byte)(fg[ImageBuffer.OrderA] >> (subpixelShift * 2));
				spanIndex++;
				spanInterpolator.Next();
			} while (--len != 0);
		}

		private static void AddWeighted(int[] fg, int weight, byte[] buffer, int offset)
		{
			fg[0] += weight * buffer[offset];
			fg[1] += weight * buffer[offset + 1];
			fg[2] += weight * buffer[offset + 2];
			fg[3] += weight * buffer[offset + 3];
		}
	}

	public class span_image_filter_rgba_bilinear_float : span_image_filter_float
	{
		public span_image_filter_rgba_bilinear_float(IImageBufferAccessorFloat src, ISpanInterpolatorFloat inter)
			: base(src, inter, null)
		{
		}

		public override void generate(ColorF[] span, int spanIndex, int x, int y, int len)
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

					ColorF color;
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
		private Color m_OutsideSourceColor;

		private const int base_shift = 8;
		private const int base_scale = (int)(1 << base_shift);
		private const int base_mask = base_scale - 1;

		public span_image_filter_rgba_bilinear_clip(IImageBufferAccessor src,
			IColorType back_color, ISpanInterpolator inter)
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
			ImageBuffer SourceRenderingBuffer = (ImageBuffer)base.GetImageBufferAccessor().SourceImage;
			int bufferIndex;
			byte[] fg_ptr;

			// Every weight is added, however small, and a pixel outside the image always takes the background, as in
			// C++ (the port used to skip weights under 256 and copy pixels straight through an identity matrix). The
			// sums start at half a unit so the shift rounds, as C++'s plain bilinear filter does; C++'s bilinear_clip
			// starts at 0 and truncates, darkening by half a unit on average, which the reference renderer's patched
			// agg_span_image_filter_rgba.h fixes too. SpanImageFilterRgbaBilinearClipTests pins it.
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
						accumulatedColor[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
						accumulatedColor[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
						accumulatedColor[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
						accumulatedColor[3] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];

						weight = (x_hr * ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
						bufferIndex += distanceBetweenPixelsInclusive;
						accumulatedColor[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
						accumulatedColor[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
						accumulatedColor[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
						accumulatedColor[3] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];

						weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) * y_hr);
						++y_lr;
						fg_ptr = SourceRenderingBuffer.GetPixelPointerXY(x_lr, y_lr, out bufferIndex);
						accumulatedColor[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
						accumulatedColor[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
						accumulatedColor[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
						accumulatedColor[3] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];
						weight = (x_hr * y_hr);
						bufferIndex += distanceBetweenPixelsInclusive;
						accumulatedColor[0] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
						accumulatedColor[1] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
						accumulatedColor[2] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
						accumulatedColor[3] += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];
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
							BlendInFilterPixel(accumulatedColor, back_r, back_g, back_b, back_a, SourceRenderingBuffer, maxx, maxy, x_lr, y_lr, weight);

							x_lr++;

							weight = (x_hr * ((int)image_subpixel_scale_e.image_subpixel_scale - y_hr));
							BlendInFilterPixel(accumulatedColor, back_r, back_g, back_b, back_a, SourceRenderingBuffer, maxx, maxy, x_lr, y_lr, weight);

							x_lr--;
							y_lr++;

							weight = (((int)image_subpixel_scale_e.image_subpixel_scale - x_hr) * y_hr);
							BlendInFilterPixel(accumulatedColor, back_r, back_g, back_b, back_a, SourceRenderingBuffer, maxx, maxy, x_lr, y_lr, weight);

							x_lr++;

							weight = (x_hr * y_hr);
							BlendInFilterPixel(accumulatedColor, back_r, back_g, back_b, back_a, SourceRenderingBuffer, maxx, maxy, x_lr, y_lr, weight);

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

	/// <summary>
	/// C++ AGG's span_image_filter_rgba_2x2: each pixel is a weighted sum of the 2x2 source pixels around the
	/// sample point, the weights read from the middle two taps of a filter lookup table (bilinear, hanning,
	/// hamming, hermite ...). Channels above alpha are clamped to it, as premultiplied color requires.
	/// </summary>
	public class span_image_filter_rgba_2x2 : span_image_filter
	{
		private const int base_mask = 255;

		public span_image_filter_rgba_2x2(IImageBufferAccessor src, ISpanInterpolator inter, ImageFilterLookUpTable filter)
			: base(src, inter, filter)
		{
			if (src.SourceImage.GetBytesBetweenPixelsInclusive() != 4)
			{
				throw new NotSupportedException("span_image_filter_rgba_2x2 must have a 32 bit source image");
			}
		}

		/// <summary>
		/// Give every pixel full alpha and clamp color only to full, as span_image_filter_rgb_2x2 does, in place of
		/// clamping it to the summed alpha (ImageFilterFill.Opaque).
		/// </summary>
		public bool Opaque { get; set; }

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
			var fg = new int[4];
			do
			{
				spanInterpolator.coordinates(out int x_hr, out int y_hr);
				x_hr -= filter_dx_int();
				y_hr -= filter_dy_int();

				int x_lr = x_hr >> subpixelShift;
				int y_lr = y_hr >> subpixelShift;

				// Start at half a unit so the downshift rounds, as span_image_filter_rgba_bilinear does. C++ AGG
				// starts at 0 and truncates, darkening by half a unit on average; the reference renderer's patched
				// agg_span_image_filter_rgba.h fixes it the same way (SpanImageFilterRgba2x2Tests pins it).
				fg[0] = fg[1] = fg[2] = fg[3] = filterHalf;

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

				// fg is indexed by source byte order; C++'s downshift is a plain shift for 8-bit color.
				int b = fg[ImageBuffer.OrderB] >> filterShift;
				int g = fg[ImageBuffer.OrderG] >> filterShift;
				int r = fg[ImageBuffer.OrderR] >> filterShift;
				int a = fg[ImageBuffer.OrderA] >> filterShift;

				if (a > base_mask || this.Opaque) a = base_mask;
				if (r > a) r = a;
				if (g > a) g = a;
				if (b > a) b = a;

				span[spanIndex].red = (byte)r;
				span[spanIndex].green = (byte)g;
				span[spanIndex].blue = (byte)b;
				span[spanIndex].alpha = (byte)a;
				spanIndex++;
				spanInterpolator.Next();
			} while (--len != 0);
		}

		private static void AddWeighted(int[] fg, int weight, byte[] buffer, int offset)
		{
			fg[0] += weight * buffer[offset];
			fg[1] += weight * buffer[offset + 1];
			fg[2] += weight * buffer[offset + 2];
			fg[3] += weight * buffer[offset + 3];
		}
	};

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

		public override void generate(Color[] span, int spanIndex, int x, int y, int len)
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
						fg_ptr = sourceAccessor.next_x(out bufferIndex);
					}

					if (--y_count == 0) break;
					y_hr += (int)image_subpixel_scale_e.image_subpixel_scale;
					fg_ptr = sourceAccessor.next_y(out bufferIndex);
				}

				f_b >>= (int)image_filter_scale_e.image_filter_shift;
				f_g >>= (int)image_filter_scale_e.image_filter_shift;
				f_r >>= (int)image_filter_scale_e.image_filter_shift;
				f_a >>= (int)image_filter_scale_e.image_filter_shift;

				// Clamped to 0..255 only, where C++ also clamps each color channel to alpha. That clamp is right for
				// premultiplied color and wrong for straight alpha, where it darkens every translucent pixel; the
				// source ImageGraphics2D draws through it is an agg-sharp ImageBuffer, which holds straight alpha, so
				// agg-sharp deliberately keeps its color (SpanImageFilterRgbaTests pins it). image_filters2's goldens
				// use this filter on an opaque image and match C++ byte for byte either way.
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

		public override void generate(ColorF[] span, int spanIndex, int xInt, int yInt, int len)
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
					float yweight = (float)m_filterFunction.calc_weight(System.Math.Sqrt((sourceSample.Y - sourceOrigin.Y) * (sourceSample.Y - sourceOrigin.Y)));
					x_count = (int)diameter;
					for (; ; )
					{
						float xweight = (float)m_filterFunction.calc_weight(System.Math.Sqrt((sourceSample.X - sourceOrigin.X) * (sourceSample.X - sourceOrigin.X)));
						float weight = xweight * yweight;

						f_r += weight * fg_ptr[bufferIndex + ImageBuffer.OrderR];
						f_g += weight * fg_ptr[bufferIndex + ImageBuffer.OrderG];
						f_b += weight * fg_ptr[bufferIndex + ImageBuffer.OrderB];
						f_a += weight * fg_ptr[bufferIndex + ImageBuffer.OrderA];

						totalWeight += weight;
						sourceSample.X += 1;
						if (--x_count == 0) break;
						fg_ptr = sourceAccessor.next_x(out bufferIndex);
					}

					sourceSample.X -= diameter;

					if (--y_count == 0) break;
					sourceSample.Y += 1;
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
}
