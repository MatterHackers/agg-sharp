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
using System.IO;
using System.IO.Compression;
using System.Text;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// The &lt;image&gt; element (SVG 1.1 section 5.7): a raster image or another SVG document, from a data: URI or
	/// through <see cref="SvgDocument.ResourceResolver"/>, fitted into its x/y/width/height by preserveAspectRatio.
	/// Raster bytes are decoded by <see cref="SvgDocument.ImageDecoder"/>, since agg itself has no image codecs.
	/// </summary>
	internal static class SvgImage
	{
		/// <summary>
		/// Draws <paramref name="element"/> with <paramref name="userToPixels"/> onto the premultiplied
		/// <paramref name="target"/>; an SVG image is drawn by <paramref name="drawDocument"/>. Draws nothing when the
		/// image cannot be loaded or decoded. A smoothed raster is sampled bilinearly, or with tiny-skia's bicubic
		/// filter when <paramref name="bicubic"/> - which only feImage asks for, as resvg's filter references show.
		/// </summary>
		public static void Draw(SvgDocument document, SvgElement element, Affine userToPixels, ImageBuffer target, double viewportWidth, double viewportHeight, Action<SvgDocument, Affine, ImageBuffer> drawDocument, bool bicubic = false)
		{
			// As in usvg, a document drawn as an SVG image draws no images of its own - any, not only ones that
			// include it again - so a self-including chain shows once.
			if (document.NestingDepth > 0)
			{
				return;
			}

			byte[] data = Load(document, element["href"], out string mediaType);
			if (data == null)
			{
				return;
			}

			SvgDocument svgImage = null;
			ImageBuffer raster = null;
			data = Gunzip(data);
			if (IsSvg(data, mediaType))
			{
				try
				{
					svgImage = SvgDocument.Parse(Encoding.UTF8.GetString(data));
				}
				catch (System.Xml.XmlException)
				{
					// As in usvg, an SVG image that does not parse is skipped, not the document drawing it.
					return;
				}

				svgImage.ImageDecoder = document.ImageDecoder;
				svgImage.ResourceResolver = document.ResourceResolver;
				svgImage.FontResolver = document.FontResolver;
				svgImage.Fonts = document.Fonts;
				svgImage.NestingDepth = document.NestingDepth + 1;
			}
			else
			{
				raster = document.ImageDecoder?.Invoke(data);
				if (raster == null || raster.Width <= 0 || raster.Height <= 0)
				{
					return;
				}
			}

			// A missing width or height is the image's own (SVG 2's auto), keeping its aspect when the other is set.
			(double intrinsicWidth, double intrinsicHeight) = svgImage != null ? svgImage.Size : (raster.Width, raster.Height);
			double x = SvgLength.Parse(element["x"], 0, viewportWidth);
			double y = SvgLength.Parse(element["y"], 0, viewportHeight);
			double width = SvgLength.Parse(element["width"], -1, viewportWidth);
			double height = SvgLength.Parse(element["height"], -1, viewportHeight);
			if (width < 0)
			{
				width = height < 0 ? intrinsicWidth : height * intrinsicWidth / intrinsicHeight;
			}

			if (height < 0)
			{
				height = width * intrinsicHeight / intrinsicWidth;
			}

			if (width <= 0 || height <= 0)
			{
				return;
			}

			// The image's own space (y down, 0..intrinsic size) fitted into the viewport, then to pixels.
			Affine imageToPixels = SvgViewport.ViewBoxTransform(new RectangleDouble(0, 0, intrinsicWidth, intrinsicHeight), element["preserveAspectRatio"], width, height)
				* Affine.NewTranslation(x, y) * userToPixels;
			var viewport = new RectangleDouble(x, y, x + width, y + height);
			if (svgImage != null)
			{
				// Drawn on its own layer, then copied back inside the viewport only: an SVG image is clipped to it.
				var layer = new ImageBuffer(target.Width, target.Height, 32, new BlenderPreMultBGRA());
				drawDocument(svgImage, SvgRenderer.DocumentToViewport(svgImage) * imageToPixels, layer);
				var layerSpans = new span_image_filter_rgba_nn(new ImageBufferAccessorClip(layer, new Color(0, 0, 0, 0)), new span_interpolator_linear(Affine.NewIdentity()));
				FillInViewport(viewport, viewport, userToPixels, layerSpans, target);
				return;
			}

			DrawRaster(raster, element, imageToPixels, viewport, userToPixels, target, bicubic);
		}

		/// <summary>
		/// Fills the part of the viewport the image covers (all of it, when "slice" overflows it) with the image,
		/// read through a clamping accessor so its edges are not blended with the transparency beyond them.
		/// </summary>
		private static void DrawRaster(ImageBuffer raster, SvgElement element, Affine imageToPixels, RectangleDouble viewport, Affine userToPixels, ImageBuffer target, bool bicubic)
		{
			ImageBuffer premultiplied = Premultiply(raster);

			// Raster rows run bottom-up; the image's own space runs top-down.
			Affine rasterToPixels = new Affine(1, 0, 0, -1, 0, raster.Height) * imageToPixels;
			ISpanGenerator spans = SvgPattern.ImageSpans(new ImageBufferAccessorClamp(premultiplied), rasterToPixels, Smooth(element), bicubic);
			if (spans == null)
			{
				return;
			}

			Affine pixelsToUser = userToPixels;
			pixelsToUser.invert();
			FillInViewport(RectangleInUserSpace(new RectangleDouble(0, 0, raster.Width, raster.Height), rasterToPixels * pixelsToUser), viewport, userToPixels, spans, target);
		}

		/// <summary>The user-space bounds of <paramref name="rectangle"/> carried by <paramref name="toUser"/>.</summary>
		private static RectangleDouble RectangleInUserSpace(RectangleDouble rectangle, Affine toUser)
		{
			var bounds = RectangleDouble.ZeroIntersection;
			foreach ((double cornerX, double cornerY) in new[] { (rectangle.Left, rectangle.Bottom), (rectangle.Right, rectangle.Bottom), (rectangle.Left, rectangle.Top), (rectangle.Right, rectangle.Top) })
			{
				double ux = cornerX, uy = cornerY;
				toUser.Transform(ref ux, ref uy);
				bounds.ExpandToInclude(ux, uy);
			}

			return bounds;
		}

		/// <summary>Fills <paramref name="area"/> (user space), clipped to <paramref name="viewport"/>, with <paramref name="spans"/>.</summary>
		private static void FillInViewport(RectangleDouble area, RectangleDouble viewport, Affine userToPixels, ISpanGenerator spans, ImageBuffer target)
		{
			if (!area.IntersectWithRectangle(viewport))
			{
				return;
			}

			var rectangle = new VertexStorage();
			rectangle.MoveTo(area.Left, area.Bottom);
			rectangle.LineTo(area.Right, area.Bottom);
			rectangle.LineTo(area.Right, area.Top);
			rectangle.LineTo(area.Left, area.Top);
			rectangle.ClosePolygon();

			// A bare rasterizer has no clip box, and the renderer writes every span it is given: without this a shape
			// reaching past the target indexes outside its rows.
			var rasterizer = new ScanlineRasterizer();
			rasterizer.SetVectorClipBox(0, 0, target.Width, target.Height);
			rasterizer.add_path(new VertexSourceApplyTransform(rectangle, userToPixels));
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), target, new span_allocator(), spans);
			target.MarkImageChanged();
		}

		/// <summary>image-rendering (inherited): optimizeSpeed, pixelated and crisp-edges ask for unsmoothed pixels.</summary>
		private static bool Smooth(SvgElement element)
		{
			for (SvgElement e = element; e != null; e = e.Parent)
			{
				string rendering = e["image-rendering"];
				if (rendering != null && rendering != "inherit")
				{
					return rendering != "optimizeSpeed" && rendering != "pixelated" && rendering != "crisp-edges";
				}
			}

			return true;
		}

		/// <summary>The bytes <paramref name="href"/> names: a data: URI's payload, else what the document's resolver returns.</summary>
		private static byte[] Load(SvgDocument document, string href, out string mediaType)
		{
			mediaType = "";
			if (string.IsNullOrWhiteSpace(href))
			{
				return null;
			}

			href = href.Trim();
			if (!href.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					return document.ResourceResolver?.Invoke(href);
				}
				catch (Exception)
				{
					return null;
				}
			}

			// data:[<media type>][;base64],<data> (RFC 2397).
			int comma = href.IndexOf(',');
			if (comma < 0)
			{
				return null;
			}

			string header = href.Substring(5, comma - 5);
			string payload = href.Substring(comma + 1);
			mediaType = header.Split(';')[0].Trim().ToLowerInvariant();
			try
			{
				return header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
					? Convert.FromBase64String(payload)
					: Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
			}
			catch (FormatException)
			{
				return null;
			}
		}

		/// <summary>
		/// Whether <paramref name="data"/> is SVG. The content decides, as it does in resvg - a data: URI's media type
		/// is often wrong - with the media type as a tie-breaker for text that is not obviously markup.
		/// </summary>
		/// <summary>
		/// An .svgz (gzip-compressed SVG, whether a file or a data: URI) unzipped; other bytes as they are. Told
		/// apart by gzip's magic number, as usvg does - no image format agg decodes starts with it.
		/// </summary>
		private static byte[] Gunzip(byte[] data)
		{
			if (data.Length < 2 || data[0] != 0x1F || data[1] != 0x8B)
			{
				return data;
			}

			try
			{
				using var gzip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
				var unzipped = new MemoryStream();
				gzip.CopyTo(unzipped);
				return unzipped.ToArray();
			}
			catch (InvalidDataException)
			{
				return data;
			}
		}

		private static bool IsSvg(byte[] data, string mediaType)
		{
			foreach (byte b in data)
			{
				if (!char.IsWhiteSpace((char)b) && b != 0xEF && b != 0xBB && b != 0xBF)
				{
					return b == '<' || mediaType == "image/svg+xml";
				}
			}

			return false;
		}

		/// <summary>A premultiplied copy of a straight-alpha image, as the renderer's span generators expect.</summary>
		private static ImageBuffer Premultiply(ImageBuffer straight)
		{
			var result = new ImageBuffer(straight.Width, straight.Height, 32, new BlenderPreMultBGRA());
			byte[] destination = result.GetBuffer();
			for (int y = 0; y < straight.Height; y++)
			{
				int to = result.GetBufferOffsetY(y);
				for (int x = 0; x < straight.Width; x++, to += 4)
				{
					Color c = straight.GetPixel(x, y);
					destination[to + ImageBuffer.OrderR] = (byte)((c.red * c.alpha + 127) / 255);
					destination[to + ImageBuffer.OrderG] = (byte)((c.green * c.alpha + 127) / 255);
					destination[to + ImageBuffer.OrderB] = (byte)((c.blue * c.alpha + 127) / 255);
					destination[to + ImageBuffer.OrderA] = c.alpha;
				}
			}

			result.MarkImageChanged();
			return result;
		}
	}
}
