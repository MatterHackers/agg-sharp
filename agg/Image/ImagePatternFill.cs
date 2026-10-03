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
	/// <see cref="ImageGraphics2D"/>'s <see cref="IPatternFillGraphics"/>: the path is rasterized as any fill is, and
	/// its spans read the image through an <see cref="ImageBufferAccessorWrap"/> at each pixel's center mapped back
	/// by the inverse image transform.
	/// </summary>
	internal static class ImagePatternFill
	{
		public static void Fill(ImageGraphics2D graphics, IVertexSource path, IImageByte image, Affine imageToScreen, ImageWrapMode wrapX, ImageWrapMode wrapY)
		{
			if (image.BitDepth != 32)
			{
				throw new NotSupportedException("A pattern fill's image is expected to be 32 bit.");
			}

			Affine screenToImage = imageToScreen;
			screenToImage.invert();

			var accessor = new ImageBufferAccessorWrap(image, WrapFor(wrapX, image.Width), WrapFor(wrapY, image.Height));
			var spanGenerator = new NearestPatternSpan(accessor, new span_interpolator_linear(screenToImage));

			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			rasterizer.reset();
			rasterizer.add_path(new VertexSourceApplyTransform(path, graphics.GetTransform()));

			IImageByte destination = graphics.DestImage;
			// Straight span colours blend straight-over into a backbuffer; see ImageGraphics2D.StraightOverDestination.
			IImageByte target = graphics.StraightOverDestination() ?? new ImageClippingProxy(destination);
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), target, new span_allocator(), spanGenerator);
			destination.MarkImageChanged();
		}

		private static IWrapMode WrapFor(ImageWrapMode mode, int size)
		{
			return mode == ImageWrapMode.Reflect ? new WrapModeReflect(size) : new WrapModeRepeat(size);
		}

		/// <summary>Nearest-pixel sampling through any accessor (span_image_filter_rgba_nn reads the buffer directly, so it can not wrap).</summary>
		private sealed class NearestPatternSpan : ISpanGenerator
		{
			private readonly IImageBufferAccessor accessor;

			private readonly span_interpolator_linear interpolator;

			public NearestPatternSpan(IImageBufferAccessor accessor, span_interpolator_linear interpolator)
			{
				this.accessor = accessor;
				this.interpolator = interpolator;
			}

			public void prepare()
			{
			}

			public void generate(Color[] span, int spanIndex, int x, int y, int len)
			{
				this.interpolator.begin(x + 0.5, y + 0.5, len);
				do
				{
					this.interpolator.coordinates(out int xHr, out int yHr);

					// An arithmetic shift floors, so pixels left of or below the image land in the tile before it.
					byte[] buffer = this.accessor.span(
						xHr >> (int)ImageFilterLookUpTable.image_subpixel_scale_e.image_subpixel_shift,
						yHr >> (int)ImageFilterLookUpTable.image_subpixel_scale_e.image_subpixel_shift,
						1,
						out int offset);
					span[spanIndex].red = buffer[offset + ImageBuffer.OrderR];
					span[spanIndex].green = buffer[offset + ImageBuffer.OrderG];
					span[spanIndex].blue = buffer[offset + ImageBuffer.OrderB];
					span[spanIndex].alpha = buffer[offset + ImageBuffer.OrderA];
					spanIndex++;
					this.interpolator.Next();
				}
				while (--len != 0);
			}
		}
	}
}
