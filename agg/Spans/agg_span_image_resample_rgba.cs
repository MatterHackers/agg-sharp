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
using image_subpixel_scale_e = MatterHackers.Agg.ImageFilterLookUpTable.image_subpixel_scale_e;

namespace MatterHackers.Agg
{
	//=========================================span_image_resample_rgba_affine
	/// <summary>
	/// C++ span_image_resample_rgba_affine: a 32-bit source resampled through an affine transform, the filter
	/// stretched over every source pixel a destination pixel covers (see <see cref="span_image_resample_affine"/>).
	/// The source is taken to be premultiplied: color above alpha comes out at alpha, as in C++.
	/// </summary>
	public class span_image_resample_rgba_affine : span_image_resample_affine
	{
		public span_image_resample_rgba_affine(IImageBufferAccessor src, ISpanInterpolator inter, ImageFilterLookUpTable filter)
			: base(src, inter, filter)
		{
			ImageResample.RequireBytesPerPixel(src, 4, nameof(span_image_resample_rgba_affine));
		}

		/// <summary>
		/// Give every pixel full alpha and clamp color only to full, as the rgb generator does, in place of taking
		/// the source to be premultiplied (ImageFilterFill.Opaque).
		/// </summary>
		public bool Opaque { get; set; }

		public override void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			ISpanInterpolator spanInterpolator = interpolator();
			spanInterpolator.begin(x + filter_dx_dbl(), y + filter_dy_dbl(), len);
			do
			{
				spanInterpolator.coordinates(out x, out y);
				span[spanIndex++] = ImageResample.PixelRgba(this, x, y, m_rx, m_ry, m_rx_inv, m_ry_inv, this.Opaque);
				spanInterpolator.Next();
			} while (--len != 0);
		}
	}

	//================================================span_image_resample_rgba
	/// <summary>
	/// C++ span_image_resample_rgba: a 32-bit source resampled through any interpolator that reports a local scale
	/// (span_interpolator_persp_lerp / _exact, usually under a span_subdiv_adaptor), the filter's footprint
	/// following that scale pixel by pixel. The source is taken to be premultiplied: color above alpha comes out
	/// at alpha, as in C++.
	/// </summary>
	public class span_image_resample_rgba : span_image_resample
	{
		public span_image_resample_rgba(IImageBufferAccessor src, ISpanInterpolator inter, ImageFilterLookUpTable filter)
			: base(src, inter, filter)
		{
			ImageResample.RequireBytesPerPixel(src, 4, nameof(span_image_resample_rgba));
		}

		/// <summary>
		/// Give every pixel full alpha and clamp color only to full, as the rgb generator does, in place of taking
		/// the source to be premultiplied (ImageFilterFill.Opaque).
		/// </summary>
		public bool Opaque { get; set; }

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
				span[spanIndex++] = ImageResample.PixelRgba(this, x, y, rx, ry, rx_inv, ry_inv, this.Opaque);
				spanInterpolator.Next();
			} while (--len != 0);
		}
	}
}
