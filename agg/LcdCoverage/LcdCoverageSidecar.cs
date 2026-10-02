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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Svg;

namespace MatterHackers.Agg.LcdCoverage
{
	/// <summary>
	/// The LCD subpixel coverage of a vector-drawn image (an SVG icon), kept beside the image's ordinary
	/// single-alpha pixels so a 1:1 draw can composite it per channel the way text is.
	/// </summary>
	/// <remarks>
	/// The image's pixels are drawn once at load and blitted as a bitmap ever after, so the per-channel
	/// coverage the LCD pipeline would have produced is gone by the time the image is drawn. This keeps it:
	/// <see cref="ChannelAlpha"/> is the same picture rasterized through the LCD filter, and
	/// <see cref="SourceAlpha"/> is the image's own alpha at that moment. The agg-gui reference renders its
	/// SVGs into an <c>LcdBuffer</c> for the same reason (<c>svg.rs</c>,
	/// <c>render_svg_tree_to_lcd_buffer_at_size</c>).
	/// <para>
	/// <b>Coverage only, never colour.</b> Colour is read from the image when it is drawn (see
	/// <see cref="BuildLcdBuffer"/>), so colour-only edits made after load - a dark-theme
	/// <c>InvertLightness</c>, a fade - still show. That is also why the sidecar is immutable and shared by
	/// reference between an image and its copies: nothing about it depends on the pixels it rides on, beyond
	/// their size and their alpha, and both are checked at draw time.
	/// </para>
	/// <para>
	/// Row 0 is the bottom row in both planes, as in <see cref="ImageBuffer"/> and <see cref="LcdBuffer"/>.
	/// </para>
	/// </remarks>
	public sealed class LcdCoverageSidecar
	{
		private readonly byte[] channelAlpha;

		private readonly byte[] sourceAlpha;

		private LcdCoverageSidecar(int width, int height, byte[] channelAlpha, byte[] sourceAlpha)
		{
			this.Width = width;
			this.Height = height;
			this.channelAlpha = channelAlpha;
			this.sourceAlpha = sourceAlpha;
		}

		/// <summary>Width in pixels of the image this coverage was made for.</summary>
		public int Width { get; }

		/// <summary>Height in pixels of the image this coverage was made for.</summary>
		public int Height { get; }

		/// <summary>Per-channel (R, G, B) coverage, 3 bytes per pixel, as an <see cref="LcdMask"/> packs it.</summary>
		public ReadOnlySpan<byte> ChannelAlpha => this.channelAlpha;

		/// <summary>The image's own alpha when this was made, 1 byte per pixel.</summary>
		public ReadOnlySpan<byte> SourceAlpha => this.sourceAlpha;

		/// <summary>
		/// Rasterizes <paramref name="document"/> through the LCD filter at <paramref name="image"/>'s size and
		/// records <paramref name="image"/>'s alpha beside it. <paramref name="image"/> must be the same
		/// document drawn by <see cref="SvgRenderer.RenderToImage"/> at that size.
		/// </summary>
		/// <remarks>
		/// <see cref="SvgRenderer"/> draws into an <see cref="ImageBuffer"/>, not an arbitrary
		/// <see cref="Graphics2D"/>, and much of what it draws (group opacity, clip paths, masks, filters) goes
		/// through offscreen layers that an <see cref="LcdBufferGraphics2D"/> could not stand in for. So rather
		/// than fill through the LCD pipeline path by path, this does the pipeline's two steps to the whole
		/// picture: the document is rendered 3x wide - exactly the horizontal supersample
		/// <see cref="LcdMaskBuilder"/> rasterizes a path into - and that render's alpha is run through the same
		/// 5-tap <see cref="LcdFilter"/>. Every SVG feature the renderer supports therefore gets subpixel
		/// coverage, with none falling back. The filter weight and gamma are the ones current at load; an icon
		/// loaded before a later settings change keeps the old ones.
		/// </remarks>
		public static LcdCoverageSidecar FromSvg(SvgDocument document, ImageBuffer image)
		{
			if (document == null)
			{
				throw new ArgumentNullException(nameof(document));
			}

			if (image == null || image.BitDepth != 32 || image.Width <= 0 || image.Height <= 0)
			{
				return null;
			}

			int width = image.Width;
			int height = image.Height;
			int grayWidth = width * 3;

			ImageBuffer supersampled = SvgRenderer.RenderToImage(document, grayWidth, height);
			byte[] supersampledBuffer = supersampled.GetBuffer();
			var gray = new byte[grayWidth * height];
			for (int y = 0; y < height; y++)
			{
				int offset = supersampled.GetBufferOffsetY(y);
				for (int x = 0; x < grayWidth; x++, offset += 4)
				{
					gray[(y * grayWidth) + x] = supersampledBuffer[offset + ImageBuffer.OrderA];
				}
			}

			LcdMask mask = LcdFilter.Apply5TapFilter(gray, grayWidth, width, height, LcdRenderSettings.PrimaryWeight, LcdRenderSettings.Gamma);

			byte[] imageBuffer = image.GetBuffer();
			var sourceAlpha = new byte[width * height];
			for (int y = 0; y < height; y++)
			{
				int offset = image.GetBufferOffsetY(y);
				for (int x = 0; x < width; x++, offset += 4)
				{
					sourceAlpha[(y * width) + x] = imageBuffer[offset + ImageBuffer.OrderA];
				}
			}

			return new LcdCoverageSidecar(width, height, mask.Data, sourceAlpha);
		}

		/// <summary>True when this coverage was made for an image of <paramref name="image"/>'s size and depth.</summary>
		public bool Fits(ImageBuffer image)
		{
			return image != null && image.BitDepth == 32 && image.Width == this.Width && image.Height == this.Height;
		}

		/// <summary>
		/// The two-plane buffer that paints <paramref name="image"/> as it looks now with this coverage: each
		/// channel's alpha is the coverage scaled by how much the image's alpha has changed since load, and the
		/// colour is the image's own.
		/// </summary>
		/// <remarks>
		/// Per channel, <c>A' = A_c * (a_now / a_orig)</c>, so a fade applied to the image after load fades the
		/// subpixel coverage with it, and the colour plane is <c>C * A'</c> (premultiplied, as
		/// <see cref="LcdBuffer"/> holds it).
		/// <para>
		/// <b>The colour bytes are read as straight alpha</b>, whatever the image's blender says. That is how
		/// both backends already blit them - the software path hands them to the destination's blender, the GPU
		/// path blends <c>SrcAlpha, OneMinusSrcAlpha</c> - and it is what an icon actually holds:
		/// <see cref="SvgRenderer.RenderToImage"/> returns straight alpha, and <c>LoadIcon</c> stamps it
		/// <see cref="BlenderPreMultBGRA"/> without converting the bytes.
		/// </para>
		/// <para>
		/// <b>Fringe pixels</b>: the filter spreads coverage about a subpixel past where the gray raster
		/// stops, so some pixels have channel coverage but no image alpha, and so no colour or alpha ratio of
		/// their own. They borrow both from the nearest horizontal neighbour that has image alpha (the spread
		/// is horizontal only), and are left empty when there is none.
		/// </para>
		/// <para>
		/// <b>Null when the image has gained ink since load</b> - any pixel whose alpha is now above its alpha
		/// at load. That is what drawing into the image does (a badge, a <c>ReplaceColor</c> that sets alpha, a
		/// non-transparent background from <c>AnyAlphaToColor</c>), and the coverage knows nothing about that
		/// ink: compositing through it would drop the ink wherever the coverage is zero and clip it to the old
		/// silhouette elsewhere. The caller then takes the plain blit, which shows the image as it is. The
		/// reviewer-suggested second test, "alpha with no coverage", is the same test: where the coverage is
		/// zero the load-time alpha was zero too (bar rounding, where it can be a few levels and must not
		/// count), so any new alpha there is already alpha above load. Lower alpha is a fade and is kept.
		/// </para>
		/// </remarks>
		public LcdBuffer BuildLcdBuffer(ImageBuffer image)
		{
			if (!this.Fits(image))
			{
				throw new ArgumentException("The image is not the size this coverage was made for.", nameof(image));
			}

			byte[] pixels = image.GetBuffer();
			for (int y = 0; y < this.Height; y++)
			{
				int pixelOffset = image.GetBufferOffsetY(y);
				int rowStart = y * this.Width;
				for (int x = 0; x < this.Width; x++, pixelOffset += 4)
				{
					if (pixels[pixelOffset + ImageBuffer.OrderA] > this.sourceAlpha[rowStart + x])
					{
						return null;
					}
				}
			}

			var result = new LcdBuffer(this.Width, this.Height);
			byte[] colorPlane = result.ColorPlane;
			byte[] alphaPlane = result.AlphaPlane;

			for (int y = 0; y < this.Height; y++)
			{
				int rowStart = y * this.Width;
				for (int x = 0; x < this.Width; x++)
				{
					int coverageOffset = (rowStart + x) * 3;
					if (this.channelAlpha[coverageOffset] == 0
						&& this.channelAlpha[coverageOffset + 1] == 0
						&& this.channelAlpha[coverageOffset + 2] == 0)
					{
						continue;
					}

					int sourceX = this.NearestColoredColumn(rowStart, x);
					if (sourceX < 0)
					{
						continue;
					}

					int pixelOffset = image.GetBufferOffsetXY(sourceX, y);
					float ratio = Math.Min(1.0f, pixels[pixelOffset + ImageBuffer.OrderA] / (float)this.sourceAlpha[rowStart + sourceX]);
					float red = pixels[pixelOffset + ImageBuffer.OrderR] / 255.0f;
					float green = pixels[pixelOffset + ImageBuffer.OrderG] / 255.0f;
					float blue = pixels[pixelOffset + ImageBuffer.OrderB] / 255.0f;

					int planeOffset = result.PixelOffset(x, y);
					float alphaRed = (this.channelAlpha[coverageOffset] / 255.0f) * ratio;
					float alphaGreen = (this.channelAlpha[coverageOffset + 1] / 255.0f) * ratio;
					float alphaBlue = (this.channelAlpha[coverageOffset + 2] / 255.0f) * ratio;

					alphaPlane[planeOffset] = ToByte(alphaRed);
					alphaPlane[planeOffset + 1] = ToByte(alphaGreen);
					alphaPlane[planeOffset + 2] = ToByte(alphaBlue);
					colorPlane[planeOffset] = ToByte(red * alphaRed);
					colorPlane[planeOffset + 1] = ToByte(green * alphaGreen);
					colorPlane[planeOffset + 2] = ToByte(blue * alphaBlue);
				}
			}

			// The planes were written directly, so the change stamp is owed by hand - see LcdBuffer.ChangedCount.
			result.MarkChanged();
			return result;
		}

		/// <summary>
		/// <paramref name="x"/> when the image had alpha there at load, else the nearer of its left and right
		/// neighbours (two columns out at most - the filter's reach) that did, or -1.
		/// </summary>
		private int NearestColoredColumn(int rowStart, int x)
		{
			for (int distance = 0; distance <= 2; distance++)
			{
				if (x - distance >= 0 && this.sourceAlpha[rowStart + x - distance] > 0)
				{
					return x - distance;
				}

				if (x + distance < this.Width && this.sourceAlpha[rowStart + x + distance] > 0)
				{
					return x + distance;
				}
			}

			return -1;
		}

		/// <summary>Quantizes a 0..1 value to a byte, rounding half up then clamping, as LcdBuffer does.</summary>
		private static byte ToByte(float value)
		{
			return (byte)Math.Clamp((value * 255.0f) + 0.5f, 0.0f, 255.0f);
		}
	}
}
