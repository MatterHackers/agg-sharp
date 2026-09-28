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
using System;
using MatterHackers.Agg.Image;
using image_filter_scale_e = MatterHackers.Agg.ImageFilterLookUpTable.image_filter_scale_e;
using image_subpixel_scale_e = MatterHackers.Agg.ImageFilterLookUpTable.image_subpixel_scale_e;

namespace MatterHackers.Agg
{
	//==========================================span_image_resample_rgb_affine
	/// <summary>
	/// C++ span_image_resample_rgb_affine: a 24-bit source resampled through an affine transform, the filter
	/// stretched over every source pixel a destination pixel covers (see <see cref="span_image_resample_affine"/>),
	/// for an opaque span.
	/// </summary>
	public class span_image_resample_rgb_affine : span_image_resample_affine
	{
		public span_image_resample_rgb_affine(IImageBufferAccessor src, ISpanInterpolator inter, ImageFilterLookUpTable filter)
			: base(src, inter, filter)
		{
			ImageResample.RequireBytesPerPixel(src, 3, nameof(span_image_resample_rgb_affine));
		}

		public override void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			ISpanInterpolator spanInterpolator = interpolator();
			spanInterpolator.begin(x + filter_dx_dbl(), y + filter_dy_dbl(), len);
			do
			{
				spanInterpolator.coordinates(out x, out y);
				span[spanIndex++] = ImageResample.PixelRgb(this, x, y, m_rx, m_ry, m_rx_inv, m_ry_inv);
				spanInterpolator.Next();
			} while (--len != 0);
		}
	}

	//=================================================span_image_resample_rgb
	/// <summary>
	/// C++ span_image_resample_rgb: a 24-bit source resampled through any interpolator that reports a local scale
	/// (span_interpolator_persp_lerp / _exact, usually under a span_subdiv_adaptor), the filter's footprint
	/// following that scale pixel by pixel, for an opaque span.
	/// </summary>
	public class span_image_resample_rgb : span_image_resample
	{
		public span_image_resample_rgb(IImageBufferAccessor src, ISpanInterpolator inter, ImageFilterLookUpTable filter)
			: base(src, inter, filter)
		{
			ImageResample.RequireBytesPerPixel(src, 3, nameof(span_image_resample_rgb));
		}

		public override void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			const int subpixelScale = (int)image_subpixel_scale_e.image_subpixel_scale;

			ISpanInterpolator spanInterpolator = interpolator();
			spanInterpolator.begin(x + filter_dx_dbl(), y + filter_dy_dbl(), len);
			do
			{
				spanInterpolator.coordinates(out x, out y);
				spanInterpolator.local_scale(out int rx, out int ry);
				adjust_scale(ref rx, ref ry);

				int rx_inv = subpixelScale * subpixelScale / rx;
				int ry_inv = subpixelScale * subpixelScale / ry;
				span[spanIndex++] = ImageResample.PixelRgb(this, x, y, rx, ry, rx_inv, ry_inv);
				spanInterpolator.Next();
			} while (--len != 0);
		}
	}

	// The per-pixel sum all four resamplers (24- and 32-bit, affine and perspective) share: C++ repeats it in each
	// generate.
	internal static class ImageResample
	{
		private const int BaseMask = 255;

		public static void RequireBytesPerPixel(IImageBufferAccessor src, int bytesPerPixel, string generator)
		{
			if (src.SourceImage.GetBytesBetweenPixelsInclusive() != bytesPerPixel)
			{
				throw new NotSupportedException($"{generator} must have a {bytesPerPixel * 8} bit source image");
			}
		}

		/// <summary>The 24-bit resample: <see cref="Sum"/> rounded to the nearest level and clamped, opaque.</summary>
		public static Color PixelRgb(span_image_filter generator, int x, int y, int rx, int ry, int rx_inv, int ry_inv)
		{
			Sum(generator, x, y, rx, ry, rx_inv, ry_inv, false, out int red, out int green, out int blue, out _, out int total_weight);

			// Round to the nearest level. C++ AGG divides the bare sums, truncating and darkening by half a level on
			// average; the reference renderer's patched agg_span_image_filter_rgb.h adds the same half
			// (SpanImageResampleRgbTests pins it).
			int half = total_weight / 2;
			return new Color(Clamp((red + half) / total_weight), Clamp((green + half) / total_weight), Clamp((blue + half) / total_weight), BaseMask);
		}

		/// <summary>
		/// The 32-bit resample: <see cref="Sum"/> rounded to the nearest level, alpha clamped to full and each color
		/// channel to alpha, as C++ does - the source is taken to be premultiplied (image_resample.cpp draws it
		/// through its premultiplied renderer). When <paramref name="opaque"/>, the 24-bit resample's result instead:
		/// alpha is never read, and each color channel is clamped only to full.
		/// </summary>
		public static Color PixelRgba(span_image_filter generator, int x, int y, int rx, int ry, int rx_inv, int ry_inv, bool opaque)
		{
			if (opaque)
			{
				return PixelRgb(generator, x, y, rx, ry, rx_inv, ry_inv);
			}

			Sum(generator, x, y, rx, ry, rx_inv, ry_inv, true, out int red, out int green, out int blue, out int alpha, out int total_weight);

			// Round as PixelRgb does; the reference renderer's patched agg_span_image_filter_rgba.h adds the same half
			// (SpanImageResampleRgbaTests pins it).
			int half = total_weight / 2;
			int a = Clamp((alpha + half) / total_weight);
			int r = Math.Min(Clamp((red + half) / total_weight), a);
			int g = Math.Min(Clamp((green + half) / total_weight), a);
			int b = Math.Min(Clamp((blue + half) / total_weight), a);
			return new Color(r, g, b, a);
		}

		/// <summary>
		/// The filter, scaled to <paramref name="rx"/> by <paramref name="ry"/> subpixels per source pixel, summed
		/// over the source around (<paramref name="x"/>, <paramref name="y"/>) in subpixels, with the total weight
		/// to divide by. <paramref name="alpha"/> is summed only when <paramref name="hasAlpha"/>.
		/// </summary>
		private static void Sum(span_image_filter generator, int x, int y, int rx, int ry, int rx_inv, int ry_inv, bool hasAlpha,
			out int red, out int green, out int blue, out int alpha, out int total_weight)
		{
			const int subpixelShift = (int)image_subpixel_scale_e.image_subpixel_shift;
			const int subpixelMask = (int)image_subpixel_scale_e.image_subpixel_mask;
			const int filterShift = (int)image_filter_scale_e.image_filter_shift;
			const int filterHalf = (int)image_filter_scale_e.image_filter_scale / 2;

			IImageBufferAccessor source = generator.GetImageBufferAccessor();
			int[] weight_array = generator.filter().weight_array();
			int diameter = generator.filter().diameter();
			int filter_scale = diameter << subpixelShift;

			int radius_x = (diameter * rx) >> 1;
			int radius_y = (diameter * ry) >> 1;
			int len_x_lr = (diameter * rx + subpixelMask) >> subpixelShift;

			x += generator.filter_dx_int() - radius_x;
			y += generator.filter_dy_int() - radius_y;

			red = 0;
			green = 0;
			blue = 0;
			alpha = 0;

			int y_lr = y >> subpixelShift;
			int y_hr = ((subpixelMask - (y & subpixelMask)) * ry_inv) >> subpixelShift;
			total_weight = 0;
			int x_lr = x >> subpixelShift;
			int x_hr = ((subpixelMask - (x & subpixelMask)) * rx_inv) >> subpixelShift;
			int x_hr2 = x_hr;
			byte[] buffer = source.span(x_lr, y_lr, len_x_lr, out int offset);
			for (; ; )
			{
				int weight_y = weight_array[y_hr];
				x_hr = x_hr2;
				for (; ; )
				{
					int weight = (weight_y * weight_array[x_hr] + filterHalf) >> filterShift;
					red += buffer[offset + ImageBuffer.OrderR] * weight;
					green += buffer[offset + ImageBuffer.OrderG] * weight;
					blue += buffer[offset + ImageBuffer.OrderB] * weight;
					if (hasAlpha)
					{
						alpha += buffer[offset + ImageBuffer.OrderA] * weight;
					}

					total_weight += weight;
					x_hr += rx_inv;
					if (x_hr >= filter_scale)
					{
						break;
					}

					buffer = source.next_x(out offset);
				}

				y_hr += ry_inv;
				if (y_hr >= filter_scale)
				{
					break;
				}

				buffer = source.next_y(out offset);
			}
		}

		private static int Clamp(int value) => value < 0 ? 0 : value > BaseMask ? BaseMask : value;
	}
}
