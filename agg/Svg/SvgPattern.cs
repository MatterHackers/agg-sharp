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
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// The &lt;pattern&gt; paint server (SVG 1.1 section 13.3): its content is drawn once into a tile image at the
	/// resolution it will be seen at, and the tile is then repeated over the shape through an image span generator
	/// reading via <see cref="ImageBufferAccessorWrap"/>.
	/// </summary>
	public static class SvgPattern
	{
		private const int MaxHrefDepth = 16;

		/// <summary>The largest tile edge in pixels; a pattern drawn bigger than this is sampled from a coarser tile.</summary>
		private const int MaxTileSize = 4096;

		/// <summary>
		/// Draws the children of a pattern element (<paramref name="contentOwner"/>) through
		/// <paramref name="contentToTile"/> (content units to tile pixels) onto a premultiplied tile image.
		/// </summary>
		public delegate void DrawContent(SvgElement contentOwner, Affine contentToTile, ImageBuffer tile);

		/// <summary>
		/// Resolves <paramref name="server"/> (a pattern) for a shape whose user-space bounding box is
		/// <paramref name="bounds"/>, drawn with <paramref name="userToPixels"/>, at <paramref name="opacity"/>.
		/// Returns null when <paramref name="server"/> is not a pattern, so the caller uses the paint's fallback.
		/// </summary>
		public static SvgServerPaint? Resolve(SvgDocument document, SvgElement server, RectangleDouble bounds, Affine userToPixels, double opacity, double viewportWidth, double viewportHeight, DrawContent drawContent)
		{
			if (server == null || server.Name != "pattern")
			{
				return null;
			}

			// Attributes a pattern leaves out, and its content if it has none, come from the pattern its href names.
			List<SvgElement> chain = HrefChain(document, server);
			string Get(string name) => chain.Select(e => e[name]).FirstOrDefault(v => v != null);
			SvgElement contentOwner = chain.FirstOrDefault(e => e.Children.Count > 0);
			var none = new SvgServerPaint(null, null);

			bool boundingBoxUnits = Get("patternUnits") != "userSpaceOnUse";
			bool hasArea = bounds.Width > 0 && bounds.Height > 0;
			// As usvg: no content, or bounding-box units on a shape with no area, is no server - the paint's
			// fallback colour is used.
			if (contentOwner == null || (boundingBoxUnits && !hasArea))
			{
				return null;
			}

			// The tile's rectangle in user space: fractions of the bounding box, or lengths of the viewport.
			double percentWidth = boundingBoxUnits ? 1 : viewportWidth;
			double percentHeight = boundingBoxUnits ? 1 : viewportHeight;
			double x = SvgLength.Parse(Get("x"), 0, percentWidth);
			double y = SvgLength.Parse(Get("y"), 0, percentHeight);
			double width = SvgLength.Parse(Get("width"), 0, percentWidth);
			double height = SvgLength.Parse(Get("height"), 0, percentHeight);
			if (boundingBoxUnits)
			{
				x = bounds.Left + x * bounds.Width;
				y = bounds.Bottom + y * bounds.Height;
				width *= bounds.Width;
				height *= bounds.Height;
			}

			if (width <= 0 || height <= 0)
			{
				return null;
			}

			// Tile space - (0, 0) at the tile's top-left - to pixels; agg's a * b applies a first.
			Affine tileToPixels = Affine.NewTranslation(x, y) * SvgTransform.Resolve(Get("patternTransform"), Get("transform-origin"), viewportWidth, viewportHeight) * userToPixels;
			double scaleX = Math.Sqrt(tileToPixels.sx * tileToPixels.sx + tileToPixels.shy * tileToPixels.shy);
			double scaleY = Math.Sqrt(tileToPixels.shx * tileToPixels.shx + tileToPixels.sy * tileToPixels.sy);
			int pixelWidth = (int)Math.Min(MaxTileSize, Math.Max(1, Math.Ceiling(width * scaleX - 1e-6)));
			int pixelHeight = (int)Math.Min(MaxTileSize, Math.Max(1, Math.Ceiling(height * scaleY - 1e-6)));

			// The tile image holds exactly one period, so wrapping it repeats the pattern seamlessly; its rows run
			// bottom-up, as agg's do.
			double pixelsPerUnitX = pixelWidth / width, pixelsPerUnitY = pixelHeight / height;
			var tileToImage = new Affine(pixelsPerUnitX, 0, 0, -pixelsPerUnitY, 0, pixelHeight);

			// A viewBox fits the content into the tile, and overrides patternContentUnits; else objectBoundingBox
			// content is in fractions of the box.
			Affine contentToTile = Affine.NewIdentity();
			RectangleDouble? viewBox = SvgViewport.ParseViewBox(Get("viewBox"));
			if (viewBox.HasValue)
			{
				contentToTile = SvgViewport.ViewBoxTransform(viewBox.Value, Get("preserveAspectRatio"), width, height);
			}
			else if (Get("patternContentUnits") == "objectBoundingBox")
			{
				if (!hasArea)
				{
					return none;
				}

				contentToTile = Affine.NewScaling(bounds.Width, bounds.Height);
			}

			var tile = new ImageBuffer(pixelWidth, pixelHeight, 32, new BlenderPreMultBGRA());
			drawContent(contentOwner, contentToTile * tileToImage, tile);
			if (opacity < 1)
			{
				ScaleAlpha(tile, opacity);
			}

			Affine imageToTile = tileToImage;
			imageToTile.invert();
			var accessor = new ImageBufferAccessorWrap(tile, new WrapModeRepeat(pixelWidth), new WrapModeRepeat(pixelHeight));
			return ImageSpans(accessor, imageToTile * tileToPixels, smooth: true) is ISpanGenerator spans ? new SvgServerPaint(null, spans) : none;
		}

		/// <summary>
		/// A span generator that draws the premultiplied image behind <paramref name="accessor"/> through
		/// <paramref name="imageToPixels"/>: pixel for pixel when that is a whole-pixel move, else sampled bilinearly
		/// (or nearest-neighbour when not <paramref name="smooth"/>; tiny-skia's bicubic, Mitchell with B = C = 1/3, when
		/// <paramref name="bicubic"/>). Null when the transform is singular.
		/// </summary>
		internal static ISpanGenerator ImageSpans(IImageBufferAccessor accessor, Affine imageToPixels, bool smooth, bool bicubic = false)
		{
			if (Math.Abs(imageToPixels.sx * imageToPixels.sy - imageToPixels.shx * imageToPixels.shy) < 1e-12)
			{
				return null;
			}

			Affine pixelsToImage = imageToPixels;
			pixelsToImage.invert();
			var interpolator = new span_interpolator_linear(pixelsToImage);

			bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;
			bool wholePixelMove = Near(imageToPixels.sx, 1) && Near(imageToPixels.sy, 1) && Near(imageToPixels.shx, 0) && Near(imageToPixels.shy, 0)
				&& Near(imageToPixels.tx, Math.Round(imageToPixels.tx)) && Near(imageToPixels.ty, Math.Round(imageToPixels.ty));
			if (wholePixelMove || !smooth)
			{
				return new span_image_filter_rgba_nn(accessor, interpolator);
			}

			if (bicubic)
			{
				return new span_image_filter_rgba(accessor, interpolator, new ImageFilterLookUpTable(new image_filter_mitchell(), true));
			}

			return new span_image_filter_rgba_2x2(accessor, interpolator, new ImageFilterLookUpTable(new image_filter_bilinear()));
		}

		/// <summary>Multiplies every channel of a premultiplied image by <paramref name="opacity"/>.</summary>
		internal static void ScaleAlpha(ImageBuffer premultiplied, double opacity)
		{
			byte[] buffer = premultiplied.GetBuffer();
			int scale = (int)Math.Round(Math.Max(0, Math.Min(1, opacity)) * 255);
			for (int i = 0; i < buffer.Length; i++)
			{
				buffer[i] = (byte)((buffer[i] * scale + 127) / 255);
			}

			premultiplied.MarkImageChanged();
		}

		/// <summary><paramref name="server"/>, then each pattern its href leads to, stopping at a loop or a non-pattern.</summary>
		private static List<SvgElement> HrefChain(SvgDocument document, SvgElement server)
		{
			var chain = new List<SvgElement>();
			for (SvgElement e = server; e != null && e.Name == "pattern" && !chain.Contains(e) && chain.Count < MaxHrefDepth; e = document.GetElementById(e["href"]))
			{
				chain.Add(e);
			}

			return chain;
		}
	}
}
