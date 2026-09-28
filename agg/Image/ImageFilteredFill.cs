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
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// <see cref="ImageGraphics2D"/>'s <see cref="IImageFilterGraphics"/>: the path is rasterized as any fill is and
	/// its spans come from the C++ AGG span generator <see cref="ImageFilterFill.Kind"/> names, reading the image
	/// through the accessor <see cref="ImageFilterFill.Edge"/> names.
	/// </summary>
	internal static class ImageFilteredFill
	{
		public static void Fill(ImageGraphics2D graphics, IVertexSource path, IImageByte image, ImageFilterFill fill)
		{
			ISpanGenerator spanGenerator = NewSpanGenerator(image, fill);
			if (fill.Opaque)
			{
				spanGenerator = new span_converter(spanGenerator, new OpaqueConverter());
			}

			if (fill.BrightnessToAlpha != null)
			{
				spanGenerator = new span_converter(spanGenerator, new BrightnessToAlphaConverter(fill.BrightnessToAlpha));
			}

			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			rasterizer.reset();
			rasterizer.add_path(new VertexSourceApplyTransform(path, graphics.GetTransform()));

			IImageByte destination = graphics.DestImage;
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), new ImageClippingProxy(destination), new span_allocator(), spanGenerator);
			destination.MarkImageChanged();
		}

		/// <summary>The span generator that draws <paramref name="image"/> as <paramref name="fill"/> says, spans in the target's pixels.</summary>
		public static span_image_filter NewSpanGenerator(IImageByte image, ImageFilterFill fill)
		{
			if (image == null)
			{
				throw new ArgumentNullException(nameof(image));
			}

			if (image.BitDepth != 32)
			{
				throw new NotSupportedException("A filtered image fill's image is expected to be 32 bit.");
			}

			if ((fill.Kind == ImageFilterKind.Filter || fill.Kind == ImageFilterKind.Resample) && fill.Filter == null)
			{
				throw new ArgumentException("Filter and Resample need the filter's weight table.", nameof(fill));
			}

			if (fill.BilinearScreenToImage != null && fill.Kind == ImageFilterKind.Resample)
			{
				throw new NotSupportedException("A bilinear mapping has no resample generator.");
			}

			IImageBufferAccessor accessor = NewAccessor(image, fill);
			Perspective screenToImage = fill.ScreenToImage();
			var affine = new Affine(screenToImage.sx, screenToImage.shy, screenToImage.shx, screenToImage.sy, screenToImage.tx, screenToImage.ty);

			if (fill.Kind == ImageFilterKind.Resample)
			{
				if (fill.IsAffine)
				{
					var resampleAffine = new span_image_resample_rgba_affine(accessor, new span_interpolator_linear(affine), fill.Filter) { Opaque = fill.Opaque };
					resampleAffine.blur(fill.Blur);
					return resampleAffine;
				}

				// span_interpolator_persp_exact is built from quads: the image's rectangle and where it lands.
				double[] quad = new double[8];
				double[] corners = { 0, 0, image.Width, 0, image.Width, image.Height, 0, image.Height };
				var imageToScreen = fill.ImageToScreen;
				for (int i = 0; i < 8; i += 2)
				{
					double x = corners[i], y = corners[i + 1];
					imageToScreen.Transform(ref x, ref y);
					quad[i] = x;
					quad[i + 1] = y;
				}

				var resample = new span_image_resample_rgba(accessor, new span_subdiv_adaptor(new span_interpolator_persp_exact(quad, 0, 0, image.Width, image.Height)), fill.Filter) { Opaque = fill.Opaque };
				resample.blur(fill.Blur);
				return resample;
			}

			ISpanInterpolator interpolator = fill.BilinearScreenToImage != null ? new span_interpolator_linear(fill.BilinearScreenToImage)
				: fill.IsAffine ? new span_interpolator_linear(affine) : new span_interpolator_trans(screenToImage);
			switch (fill.Kind)
			{
				case ImageFilterKind.Nearest:
					return new span_image_filter_rgba_nn(accessor, interpolator);

				case ImageFilterKind.Bilinear:
					return new span_image_filter_rgba_bilinear(accessor, interpolator);

				default:
					return fill.Filter.diameter() == 2
						? new span_image_filter_rgba_2x2(accessor, interpolator, fill.Filter) { Opaque = fill.Opaque }
						: new span_image_filter_rgba(accessor, interpolator, fill.Filter);
			}
		}

		/// <summary>The index <see cref="ImageFilterFill.BrightnessToAlpha"/> looks a pixel's alpha up at.</summary>
		public static int BrightnessIndex(int red, int green, int blue, int levels)
		{
			return Math.Min((red + green + blue) * levels / (3 * 255), levels - 1);
		}

		private static IImageBufferAccessor NewAccessor(IImageByte image, ImageFilterFill fill)
		{
			switch (fill.Edge)
			{
				case ImageFilterEdge.Clip:
					return new ImageBufferAccessorClip(image, fill.Background);

				case ImageFilterEdge.Repeat:
					return new ImageBufferAccessorWrap(image, new WrapModeRepeat(image.Width), new WrapModeRepeat(image.Height));

				case ImageFilterEdge.Reflect:
					return new ImageBufferAccessorWrap(image, new WrapModeReflect(image.Width), new WrapModeReflect(image.Height));

				default:
					return new ImageBufferAccessorClamp(image);
			}
		}

		/// <summary>
		/// <see cref="ImageFilterFill.Opaque"/>: the rgb generators' full alpha for the generators that give the summed
		/// alpha. None of them clamps colour to it then (span_image_filter_rgba never does; the 2x2 and resample
		/// generators are told), so colour is clamped only to 0..255, as the rgb generators clamp it.
		/// </summary>
		private sealed class OpaqueConverter : ISpanGenerator
		{
			public void prepare()
			{
			}

			public void generate(Color[] span, int spanIndex, int x, int y, int len)
			{
				for (int i = spanIndex; i < spanIndex + len; i++)
				{
					span[i].alpha = 255;
				}
			}
		}

		/// <summary>span_conv_brightness_alpha: each pixel's alpha from its brightness through the table.</summary>
		private sealed class BrightnessToAlphaConverter : ISpanGenerator
		{
			private readonly byte[] brightnessToAlpha;

			public BrightnessToAlphaConverter(byte[] brightnessToAlpha)
			{
				this.brightnessToAlpha = brightnessToAlpha;
			}

			public void prepare()
			{
			}

			public void generate(Color[] span, int spanIndex, int x, int y, int len)
			{
				for (int i = spanIndex; i < spanIndex + len; i++)
				{
					span[i].alpha = this.brightnessToAlpha[BrightnessIndex(span[i].red, span[i].green, span[i].blue, this.brightnessToAlpha.Length)];
				}
			}
		}
	}
}
