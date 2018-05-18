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
#define USE_UNSAFE_CODE

using MatterHackers.Agg.Image;
using System;
using image_subpixel_scale_e = MatterHackers.Agg.ImageFilterLookUpTable.image_subpixel_scale_e;

namespace MatterHackers.Agg
{
	// it should be easy to write a 90 rotating or mirroring filter too. LBB 2012/01/14
	public class span_image_filter_gray_nn_stepXby1 : span_image_filter
	{
		private const int base_shift = 8;
		private const int base_scale = (int)(1 << base_shift);
		private const int base_mask = base_scale - 1;

		public span_image_filter_gray_nn_stepXby1(IImageBufferAccessor sourceAccessor, ISpanInterpolator spanInterpolator)
			: base(sourceAccessor, spanInterpolator, null)
		{
		}

		public override void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			ImageBuffer SourceRenderingBuffer = (ImageBuffer)GetImageBufferAccessor().SourceImage;
			int bytesBetweenPixelsInclusive = SourceRenderingBuffer.GetBytesBetweenPixelsInclusive();
			if (SourceRenderingBuffer.BitDepth != 8)
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
						span[spanIndex].red = pSource[bufferIndex];
						span[spanIndex].green = pSource[bufferIndex];
						span[spanIndex].blue = pSource[bufferIndex];
						span[spanIndex].alpha = 255;
						spanIndex++;
						bufferIndex += bytesBetweenPixelsInclusive;
					} while (--len != 0);
				}
			}
#else
            do
            {
                throw new Exception("this code is for 32 bit");
                color.m_B = fg_ptr[bufferIndex++];
                color.m_G = fg_ptr[bufferIndex++];
                color.m_R = fg_ptr[bufferIndex++];
                color.m_A = fg_ptr[bufferIndex++];
                span[spanIndex++] = color;
            } while (--len != 0);
#endif
		}
	}

	/*
		//==============================================span_image_filter_gray_nn
		//template<class Source, class Interpolator>
		public class span_image_filter_gray_nn : span_image_filter // <Source, Interpolator>
		{
			//typedef Source source_type;
			//typedef typename source_type::color_type color_type;
			//typedef Interpolator interpolator_type;
			//typedef span_image_filter<source_type, interpolator_type> base_type;
			//typedef typename color_type::value_type value_type;
			//typedef typename color_type::calc_type calc_type;
			enum base_scale_e
			{
				base_shift = 8,//color_type::base_shift,
				base_mask  = 255//color_type::base_mask
			};

			//--------------------------------------------------------------------
			public span_image_filter_gray_nn() {}
			public span_image_filter_gray_nn(source_type src, interpolator_type inter) :
				base(src, inter, 0)
			{}

			//--------------------------------------------------------------------
			public void generate(color_type* span, int x, int y, unsigned len)
			{
				base_type::interpolator().begin(x + base_type::filter_dx_dbl(),
												y + base_type::filter_dy_dbl(), len);
				do
				{
					base_type::interpolator().coordinates(&x, &y);
					span->v = *(value_type*)
						base_type::source().span(x >> image_subpixel_shift,
												 y >> image_subpixel_shift,
												 1);
					span->a = base_mask;
					++span;
					++base_type::interpolator();
				} while(--len);
			}
		};

		//=========================================span_image_filter_gray_bilinear
		//template<class Source, class Interpolator>
		public class span_image_filter_gray_bilinear : span_image_filter // <Source, Interpolator>
		{
			//typedef Source source_type;
			//typedef typename source_type::color_type color_type;
			//typedef Interpolator interpolator_type;
			//typedef span_image_filter<source_type, interpolator_type> base_type;
			//typedef typename color_type::value_type value_type;
			//typedef typename color_type::calc_type calc_type;
			enum base_scale_e
			{
				base_shift = 8,//color_type::base_shift,
				base_mask  = 255//color_type::base_mask
			};

			//--------------------------------------------------------------------
			public span_image_filter_gray_bilinear() {}
			public span_image_filter_gray_bilinear(source_type src,
											interpolator_type inter) :
				base(src, inter, 0)
			{}

			//--------------------------------------------------------------------
			public void generate(color_type* span, int x, int y, unsigned len)
			{
				base_type::interpolator().begin(x + base_type::filter_dx_dbl(),
												y + base_type::filter_dy_dbl(), len);
				calc_type fg;
				value_type *fg_ptr;
				do
				{
					int x_hr;
					int y_hr;

					base_type::interpolator().coordinates(&x_hr, &y_hr);

					x_hr -= base_type::filter_dx_int();
					y_hr -= base_type::filter_dy_int();

					int x_lr = x_hr >> image_subpixel_shift;
					int y_lr = y_hr >> image_subpixel_shift;

					fg = image_subpixel_scale * image_subpixel_scale / 2;

					x_hr &= image_subpixel_mask;
					y_hr &= image_subpixel_mask;

					fg_ptr = (value_type*)base_type::source().span(x_lr, y_lr, 2);
					fg    += *fg_ptr * (image_subpixel_scale - x_hr) * (image_subpixel_scale - y_hr);

					fg_ptr = (value_type*)base_type::source().next_x();
					fg    += *fg_ptr * x_hr * (image_subpixel_scale - y_hr);

					fg_ptr = (value_type*)base_type::source().next_y();
					fg    += *fg_ptr * (image_subpixel_scale - x_hr) * y_hr;

					fg_ptr = (value_type*)base_type::source().next_x();
					fg    += *fg_ptr * x_hr * y_hr;

					span->v = value_type(fg >> (image_subpixel_shift * 2));
					span->a = base_mask;
					++span;
					++base_type::interpolator();
				} while(--len);
			}
		};

		//====================================span_image_filter_gray_bilinear_clip
		//template<class Source, class Interpolator>
		public class span_image_filter_gray_bilinear_clip : span_image_filter // <Source, Interpolator>
		{
			//typedef Source source_type;
			//typedef typename source_type::color_type color_type;
			//typedef Interpolator interpolator_type;
			//typedef span_image_filter<source_type, interpolator_type> base_type;
			//typedef typename color_type::value_type value_type;
			//typedef typename color_type::calc_type calc_type;
			enum base_scale_e
			{
				base_shift = 8,//color_type::base_shift,
				base_mask  = 255//color_type::base_mask
			};

			//--------------------------------------------------------------------
			public span_image_filter_gray_bilinear_clip() {}
			public span_image_filter_gray_bilinear_clip(source_type src,
												 color_type back_color,
												 interpolator_type inter) :
				base(src, inter, 0)
			{
				m_back_color = (back_color);
			}
			public color_type background_color() { return m_back_color; }
			public void background_color(color_type v)   { m_back_color = v; }

			//--------------------------------------------------------------------
			public void generate(color_type* span, int x, int y, unsigned len)
			{
				base_type::interpolator().begin(x + base_type::filter_dx_dbl(),
												y + base_type::filter_dy_dbl(), len);
				calc_type fg;
				calc_type src_alpha;
				value_type back_v = m_back_color.v;
				value_type back_a = m_back_color.a;

				value_type *fg_ptr;

				int maxx = base_type::source().width() - 1;
				int maxy = base_type::source().height() - 1;

				do
				{
					int x_hr;
					int y_hr;

					base_type::interpolator().coordinates(&x_hr, &y_hr);

					x_hr -= base_type::filter_dx_int();
					y_hr -= base_type::filter_dy_int();

					int x_lr = x_hr >> image_subpixel_shift;
					int y_lr = y_hr >> image_subpixel_shift;

					if(x_lr >= 0    && y_lr >= 0 &&
					   x_lr <  maxx && y_lr <  maxy)
					{
						fg = image_subpixel_scale * image_subpixel_scale / 2;

						x_hr &= image_subpixel_mask;
						y_hr &= image_subpixel_mask;
						fg_ptr = (value_type*)base_type::source().row_ptr(y_lr) + x_lr;

						fg += *fg_ptr++ * (image_subpixel_scale - x_hr) * (image_subpixel_scale - y_hr);
						fg += *fg_ptr++ * (image_subpixel_scale - y_hr) * x_hr;

						++y_lr;
						fg_ptr = (value_type*)base_type::source().row_ptr(y_lr) + x_lr;

						fg += *fg_ptr++ * (image_subpixel_scale - x_hr) * y_hr;
						fg += *fg_ptr++ * x_hr * y_hr;

						fg >>= image_subpixel_shift * 2;
						src_alpha = base_mask;
					}
					else
					{
						unsigned weight;
						if(x_lr < -1   || y_lr < -1 ||
						   x_lr > maxx || y_lr > maxy)
						{
							fg        = back_v;
							src_alpha = back_a;
						}
						else
						{
							fg =
							src_alpha = image_subpixel_scale * image_subpixel_scale / 2;

							x_hr &= image_subpixel_mask;
							y_hr &= image_subpixel_mask;

							weight = (image_subpixel_scale - x_hr) *
									 (image_subpixel_scale - y_hr);
							if(x_lr >= 0    && y_lr >= 0 &&
							   x_lr <= maxx && y_lr <= maxy)
							{
								fg += weight *
									*((value_type*)base_type::source().row_ptr(y_lr) + x_lr);
								src_alpha += weight * base_mask;
							}
							else
							{
								fg        += back_v * weight;
								src_alpha += back_a * weight;
							}

							x_lr++;

							weight = x_hr * (image_subpixel_scale - y_hr);
							if(x_lr >= 0    && y_lr >= 0 &&
							   x_lr <= maxx && y_lr <= maxy)
							{
								fg += weight *
									*((value_type*)base_type::source().row_ptr(y_lr) + x_lr);
								src_alpha += weight * base_mask;
							}
							else
							{
								fg        += back_v * weight;
								src_alpha += back_a * weight;
							}

							x_lr--;
							y_lr++;

							weight = (image_subpixel_scale - x_hr) * y_hr;
							if(x_lr >= 0    && y_lr >= 0 &&
							   x_lr <= maxx && y_lr <= maxy)
							{
								fg += weight *
									*((value_type*)base_type::source().row_ptr(y_lr) + x_lr);
								src_alpha += weight * base_mask;
							}
							else
							{
								fg        += back_v * weight;
								src_alpha += back_a * weight;
							}

							x_lr++;

							weight = x_hr * y_hr;
							if(x_lr >= 0    && y_lr >= 0 &&
							   x_lr <= maxx && y_lr <= maxy)
							{
								fg += weight *
									*((value_type*)base_type::source().row_ptr(y_lr) + x_lr);
								src_alpha += weight * base_mask;
							}
							else
							{
								fg        += back_v * weight;
								src_alpha += back_a * weight;
							}

							fg        >>= image_subpixel_shift * 2;
							src_alpha >>= image_subpixel_shift * 2;
						}
					}

					span->v = (value_type)fg;
					span->a = (value_type)src_alpha;
					++span;
					++base_type::interpolator();
				} while(--len);
			}

			color_type m_back_color;
		};

		//==============================================span_image_filter_gray_2x2
		//template<class Source, class Interpolator>
		public class span_image_filter_gray_2x2 : span_image_filter//<Source, Interpolator>
		{
			//typedef Source source_type;
			//typedef typename source_type::color_type color_type;
			//typedef Interpolator interpolator_type;
			//typedef span_image_filter<source_type, interpolator_type> base_type;
			//typedef typename color_type::value_type value_type;
			//typedef typename color_type::calc_type calc_type;
			enum base_scale_e
			{
				base_shift = 8,//color_type::base_shift,
				base_mask  = 255//color_type::base_mask
			};

			//--------------------------------------------------------------------
			public span_image_filter_gray_2x2() {}
			public span_image_filter_gray_2x2(source_type src,
									   interpolator_type inter,
									   image_filter_lut filter) :
				base(src, inter, &filter)
			{}

			//--------------------------------------------------------------------
			public void generate(color_type* span, int x, int y, unsigned len)
			{
				base_type::interpolator().begin(x + base_type::filter_dx_dbl(),
												y + base_type::filter_dy_dbl(), len);

				calc_type fg;

				value_type *fg_ptr;
				int16* weight_array = base_type::filter().weight_array() +
											((base_type::filter().diameter()/2 - 1) <<
											  image_subpixel_shift);
				do
				{
					int x_hr;
					int y_hr;

					base_type::interpolator().coordinates(&x_hr, &y_hr);

					x_hr -= base_type::filter_dx_int();
					y_hr -= base_type::filter_dy_int();

					int x_lr = x_hr >> image_subpixel_shift;
					int y_lr = y_hr >> image_subpixel_shift;

					unsigned weight;
					fg = image_filter_scale / 2;

					x_hr &= image_subpixel_mask;
					y_hr &= image_subpixel_mask;

					fg_ptr = (value_type*)base_type::source().span(x_lr, y_lr, 2);
					weight = (weight_array[x_hr + image_subpixel_scale] *
							  weight_array[y_hr + image_subpixel_scale] +
							  image_filter_scale / 2) >>
							  image_filter_shift;
					fg += weight * *fg_ptr;

					fg_ptr = (value_type*)base_type::source().next_x();
					weight = (weight_array[x_hr] *
							  weight_array[y_hr + image_subpixel_scale] +
							  image_filter_scale / 2) >>
							  image_filter_shift;
					fg += weight * *fg_ptr;

					fg_ptr = (value_type*)base_type::source().next_y();
					weight = (weight_array[x_hr + image_subpixel_scale] *
							  weight_array[y_hr] +
							  image_filter_scale / 2) >>
							  image_filter_shift;
					fg += weight * *fg_ptr;

					fg_ptr = (value_type*)base_type::source().next_x();
					weight = (weight_array[x_hr] *
							  weight_array[y_hr] +
							  image_filter_scale / 2) >>
							  image_filter_shift;
					fg += weight * *fg_ptr;

					fg >>= image_filter_shift;
					if(fg > base_mask) fg = base_mask;

					span->v = (value_type)fg;
					span->a = base_mask;
					++span;
					++base_type::interpolator();
				} while(--len);
			}
		};

		//==================================================span_image_filter_gray
		//template<class Source, class Interpolator>
		public class span_image_filter_gray : span_image_filter//<Source, Interpolator>
		{
			//typedef Source source_type;
			//typedef typename source_type::color_type color_type;
			//typedef Interpolator interpolator_type;
			//typedef span_image_filter<source_type, interpolator_type> base_type;
			//typedef typename color_type::value_type value_type;
			//typedef typename color_type::calc_type calc_type;
			enum base_scale_e
			{
				base_shift = 8,//color_type::base_shift,
				base_mask  = 255//color_type::base_mask
			};

			//--------------------------------------------------------------------
			public span_image_filter_gray() {}
			public span_image_filter_gray(source_type src,
								   interpolator_type inter,
								   image_filter_lut filter) :
				base(src, inter, filter)
			{}

			//--------------------------------------------------------------------
			public void generate(color_type* span, int x, int y, unsigned len)
			{
				base_type::interpolator().begin(x + base_type::filter_dx_dbl(),
												y + base_type::filter_dy_dbl(), len);

				int fg;
				value_type *fg_ptr;

				unsigned     diameter     = base_type::filter().diameter();
				int          start        = base_type::filter().start();
				int16* weight_array = base_type::filter().weight_array();

				int x_count;
				int weight_y;

				do
				{
					base_type::interpolator().coordinates(&x, &y);

					x -= base_type::filter_dx_int();
					y -= base_type::filter_dy_int();

					int x_hr = x;
					int y_hr = y;

					int x_lr = x_hr >> image_subpixel_shift;
					int y_lr = y_hr >> image_subpixel_shift;

					fg = image_filter_scale / 2;

					int x_fract = x_hr & image_subpixel_mask;
					unsigned y_count = diameter;

					y_hr = image_subpixel_mask - (y_hr & image_subpixel_mask);
					fg_ptr = (value_type*)base_type::source().span(x_lr + start,
																		 y_lr + start,
																		 diameter);
					for(;;)
					{
						x_count  = diameter;
						weight_y = weight_array[y_hr];
						x_hr = image_subpixel_mask - x_fract;
						for(;;)
						{
							fg += *fg_ptr *
								  ((weight_y * weight_array[x_hr] +
									image_filter_scale / 2) >>
									image_filter_shift);
							if(--x_count == 0) break;
							x_hr  += image_subpixel_scale;
							fg_ptr = (value_type*)base_type::source().next_x();
						}

						if(--y_count == 0) break;
						y_hr  += image_subpixel_scale;
						fg_ptr = (value_type*)base_type::source().next_y();
					}

					fg >>= image_filter_shift;
					if(fg < 0) fg = 0;
					if(fg > base_mask) fg = base_mask;
					span->v = (value_type)fg;
					span->a = base_mask;

					++span;
					++base_type::interpolator();
				} while(--len);
			}
		};

		//=========================================span_image_resample_gray_affine
		//template<class Source>
		public class span_image_resample_gray_affine : span_image_resample_affine//<Source>
		{
			//typedef Source source_type;
			//typedef typename source_type::color_type color_type;
			//typedef span_image_resample_affine<source_type> base_type;
			//typedef typename base_type::interpolator_type interpolator_type;
			//typedef typename color_type::value_type value_type;
			//typedef typename color_type::long_type long_type;
			enum base_scale_e
			{
				base_shift      = 8,//color_type::base_shift,
				base_mask       = 255,//color_type::base_mask,
				downscale_shift = image_filter_shift
			};

			//--------------------------------------------------------------------
			public span_image_resample_gray_affine() {}
			public span_image_resample_gray_affine(source_type src,
											interpolator_type inter,
											image_filter_lut filter) :
				base(src, inter, filter)
			{}

			//--------------------------------------------------------------------
			public void generate(color_type* span, int x, int y, unsigned len)
			{
				base_type::interpolator().begin(x + base_type::filter_dx_dbl(),
												y + base_type::filter_dy_dbl(), len);

				long_type fg;

				int diameter     = base_type::filter().diameter();
				int filter_scale = diameter << image_subpixel_shift;
				int radius_x     = (diameter * base_type::m_rx) >> 1;
				int radius_y     = (diameter * base_type::m_ry) >> 1;
				int len_x_lr     =
					(diameter * base_type::m_rx + image_subpixel_mask) >>
						image_subpixel_shift;

				int16* weight_array = base_type::filter().weight_array();

				do
				{
					base_type::interpolator().coordinates(&x, &y);

					x += base_type::filter_dx_int() - radius_x;
					y += base_type::filter_dy_int() - radius_y;

					fg = image_filter_scale / 2;

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
					value_type* fg_ptr =
						(value_type*)base_type::source().span(x_lr, y_lr, len_x_lr);
					for(;;)
					{
						int weight_y = weight_array[y_hr];
						x_hr = x_hr2;
						for(;;)
						{
							int weight = (weight_y * weight_array[x_hr] +
										 image_filter_scale / 2) >>
										 downscale_shift;

							fg += *fg_ptr * weight;
							total_weight += weight;
							x_hr  += base_type::m_rx_inv;
							if(x_hr >= filter_scale) break;
							fg_ptr = (value_type*)base_type::source().next_x();
						}
						y_hr += base_type::m_ry_inv;
						if(y_hr >= filter_scale) break;
						fg_ptr = (value_type*)base_type::source().next_y();
					}

					fg /= total_weight;
					if(fg < 0) fg = 0;
					if(fg > base_mask) fg = base_mask;

					span->v = (value_type)fg;
					span->a = base_mask;

					++span;
					++base_type::interpolator();
				} while(--len);
			}
		};

		//================================================span_image_resample_gray
		//template<class Source, class Interpolator>
		public class span_image_resample_gray : span_image_resample//<Source, Interpolator>
		{
			//typedef Source source_type;
			//typedef typename source_type::color_type color_type;
			//typedef Interpolator interpolator_type;
			//typedef span_image_resample<source_type, interpolator_type> base_type;
			//typedef typename color_type::value_type value_type;
			//typedef typename color_type::long_type long_type;
			enum base_scale_e
			{
				base_shift = 8,//color_type::base_shift,
				base_mask  = 255,//color_type::base_mask,
				downscale_shift = image_filter_shift
			};

			//--------------------------------------------------------------------
			public span_image_resample_gray() {}
			public span_image_resample_gray(source_type src,
									 interpolator_type inter,
									 image_filter_lut filter) :
				base(src, inter, filter)
			{}

			//--------------------------------------------------------------------
			public void generate(color_type* span, int x, int y, unsigned len)
			{
				base_type::interpolator().begin(x + base_type::filter_dx_dbl(),
												y + base_type::filter_dy_dbl(), len);
				long_type fg;

				int diameter = base_type::filter().diameter();
				int filter_scale = diameter << image_subpixel_shift;

				int16* weight_array = base_type::filter().weight_array();
				do
				{
					int rx;
					int ry;
					int rx_inv = image_subpixel_scale;
					int ry_inv = image_subpixel_scale;
					base_type::interpolator().coordinates(&x,  &y);
					base_type::interpolator().local_scale(&rx, &ry);
					base_type::adjust_scale(&rx, &ry);

					rx_inv = image_subpixel_scale * image_subpixel_scale / rx;
					ry_inv = image_subpixel_scale * image_subpixel_scale / ry;

					int radius_x = (diameter * rx) >> 1;
					int radius_y = (diameter * ry) >> 1;
					int len_x_lr =
						(diameter * rx + image_subpixel_mask) >>
							image_subpixel_shift;

					x += base_type::filter_dx_int() - radius_x;
					y += base_type::filter_dy_int() - radius_y;

					fg = image_filter_scale / 2;

					int y_lr = y >> image_subpixel_shift;
					int y_hr = ((image_subpixel_mask - (y & image_subpixel_mask)) *
								   ry_inv) >>
									   image_subpixel_shift;
					int total_weight = 0;
					int x_lr = x >> image_subpixel_shift;
					int x_hr = ((image_subpixel_mask - (x & image_subpixel_mask)) *
								   rx_inv) >>
									   image_subpixel_shift;
					int x_hr2 = x_hr;
					value_type* fg_ptr =
						(value_type*)base_type::source().span(x_lr, y_lr, len_x_lr);

					for(;;)
					{
						int weight_y = weight_array[y_hr];
						x_hr = x_hr2;
						for(;;)
						{
							int weight = (weight_y * weight_array[x_hr] +
										 image_filter_scale / 2) >>
										 downscale_shift;
							fg += *fg_ptr * weight;
							total_weight += weight;
							x_hr  += rx_inv;
							if(x_hr >= filter_scale) break;
							fg_ptr = (value_type*)base_type::source().next_x();
						}
						y_hr += ry_inv;
						if(y_hr >= filter_scale) break;
						fg_ptr = (value_type*)base_type::source().next_y();
					}

					fg /= total_weight;
					if(fg < 0) fg = 0;
					if(fg > base_mask) fg = base_mask;

					span->v = (value_type)fg;
					span->a = base_mask;

					++span;
					++base_type::interpolator();
				} while(--len);
			}
		};
		 */
}