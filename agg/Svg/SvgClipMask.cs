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
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// clip-path and mask: the element is drawn onto its own layer, which is then multiplied by a coverage layer
	/// (the clip path's shapes, antialiased) and by the mask's luminance (or alpha, for mask-type="alpha") before
	/// being composited. A reference to a missing element, or to one that is not a clipPath (mask), is ignored;
	/// objectBoundingBox units on an element with no box, a zero-sized mask region, or a clip or mask that
	/// references itself make the element draw nothing, as resvg does.
	/// </summary>
	internal static class SvgClipMask
	{
		/// <summary>How deep clip paths and masks may nest through each other.</summary>
		private const int MaxDepth = 16;

		public static bool Applies(SvgRenderer.Context context, SvgElement element)
		{
			return Reference(context, element, "clip-path", "clipPath") != null || Reference(context, element, "mask", "mask") != null;
		}

		/// <summary>
		/// Runs <paramref name="draw"/> onto a layer, clips and masks it as <paramref name="element"/> says, and
		/// composites it over <paramref name="target"/>. <paramref name="transform"/> is the element's user space to pixels.
		/// </summary>
		public static void Draw(SvgRenderer.Context context, SvgElement element, Affine transform, ImageBuffer target, Action<ImageBuffer> draw)
		{
			ImageBuffer layer = NewLayer(target);
			draw(layer);
			RectangleDouble? bounds = ObjectBounds(context, element, 0);
			if (Reference(context, element, "clip-path", "clipPath") is SvgElement clip && !ApplyClip(context, clip, bounds, transform, layer, 0))
			{
				return;
			}

			if (Reference(context, element, "mask", "mask") is SvgElement mask && !ApplyMask(context, mask, bounds, transform, layer, 0))
			{
				return;
			}

			SvgRenderer.CompositeLayer(target, layer, 1);
		}

		/// <summary>
		/// The element's bounding box in its own user space (its transform not applied): its outline's, or its
		/// children's under their transforms; null for an element that draws no geometry.
		/// </summary>
		internal static RectangleDouble? ObjectBounds(SvgRenderer.Context context, SvgElement element, int depth)
		{
			if (depth > MaxDepth)
			{
				return null;
			}

			var bounds = RectangleDouble.ZeroIntersection;
			bool any = false;
			void Include(IVertexSource path, Affine transform)
			{
				foreach (VertexData vertex in new VertexSourceApplyTransform(new FlattenCurves(path), transform).Vertices())
				{
					if (!vertex.IsClose && !vertex.IsStop)
					{
						bounds.ExpandToInclude(vertex.Position);
						any = true;
					}
				}
			}

			void IncludeBox(RectangleDouble? box, Affine transform)
			{
				if (box is RectangleDouble b)
				{
					Include(SvgShapes.Poly(new List<double> { b.Left, b.Bottom, b.Right, b.Bottom, b.Right, b.Top, b.Left, b.Top }, true), transform);
				}
			}

			switch (element.Name)
			{
				case "g":
				case "a":
				case "switch":
					foreach (SvgElement child in element.Children)
					{
						IncludeBox(ObjectBounds(context, child, depth + 1), SvgTransform.Resolve(child, "transform", context.ViewportWidth, context.ViewportHeight));
					}

					break;
				case "use":
					if (context.Document.GetElementById(element["href"]) is SvgElement referenced)
					{
						Affine offset = SvgTransform.Resolve(referenced, "transform", context.ViewportWidth, context.ViewportHeight) * Affine.NewTranslation(
							SvgLength.Parse(element["x"], 0, context.ViewportWidth),
							SvgLength.Parse(element["y"], 0, context.ViewportHeight));
						IncludeBox(ObjectBounds(context, referenced, depth + 1), offset);
					}

					break;
				case "text":
					SvgStyle style = SvgRenderer.InheritedStyle(context, element);
					foreach ((VertexStorage run, _) in SvgText.Layout(element, style, context.ViewportWidth, context.ViewportHeight, context.Diagonal, context.Document.FontResolver, context.Document.Fonts, context.Document))
					{
						Include(run, Affine.NewIdentity());
					}

					break;
				default:
					if (SvgShapes.ToPath(element, context.ViewportWidth, context.ViewportHeight) is VertexStorage path)
					{
						Include(path, Affine.NewIdentity());
					}

					break;
			}

			return any ? bounds : (RectangleDouble?)null;
		}

		private static SvgElement Reference(SvgRenderer.Context context, SvgElement element, string property, string kind)
		{
			string value = element[property];
			if (value == null || value == "none")
			{
				return null;
			}

			SvgElement referenced = context.Document.GetElementById(value);
			return referenced?.Name == kind ? referenced : null;
		}

		/// <summary>The unit square to <paramref name="bounds"/>: objectBoundingBox units.</summary>
		private static Affine BoxUnits(RectangleDouble bounds)
		{
			return Affine.NewScaling(bounds.Width, bounds.Height) * Affine.NewTranslation(bounds.Left, bounds.Bottom);
		}

		/// <summary>Multiplies <paramref name="layer"/> by <paramref name="clip"/>'s coverage; false when nothing may draw.</summary>
		private static bool ApplyClip(SvgRenderer.Context context, SvgElement clip, RectangleDouble? bounds, Affine transform, ImageBuffer layer, int depth)
		{
			if (depth > MaxDepth || !context.ActiveClipsAndMasks.Add(clip))
			{
				return false;
			}

			try
			{
				Affine clipToUser = SvgTransform.Resolve(clip, "transform", context.ViewportWidth, context.ViewportHeight);
				if (clip["clipPathUnits"] == "objectBoundingBox")
				{
					if (!(bounds is RectangleDouble box) || box.Width <= 0 || box.Height <= 0)
					{
						return false;
					}

					clipToUser = BoxUnits(box) * clipToUser;
				}

				Affine toPixels = clipToUser * transform;
				ImageBuffer coverage = NewLayer(layer);
				SvgStyle clipStyle = SvgRenderer.InheritedStyle(context, clip);
				foreach (SvgElement child in clip.Children)
				{
					SvgStyle style = SvgStyle.Compute(child, clipStyle, context.Diagonal);
					if (style.DisplayNone)
					{
						continue;
					}

					Affine childTransform = SvgTransform.Resolve(child, "transform", context.ViewportWidth, context.ViewportHeight) * toPixels;
					ImageBuffer childCoverage = NewLayer(layer);
					DrawClipChild(context, child, style, childTransform, childCoverage);

					// A child's own clip-path clips its contribution, in the child's user space.
					if (Reference(context, child, "clip-path", "clipPath") is SvgElement childClip
						&& !ApplyClip(context, childClip, ObjectBounds(context, child, 0), childTransform, childCoverage, depth + 1))
					{
						continue;
					}

					SvgRenderer.CompositeLayer(coverage, childCoverage, 1);
				}

				// clip-path on the clipPath itself intersects the two, in the clipped element's space.
				if (Reference(context, clip, "clip-path", "clipPath") is SvgElement nested
					&& !ApplyClip(context, nested, bounds, transform, coverage, depth + 1))
				{
					return false;
				}

				Multiply(layer, coverage, (pixels, i) => pixels[i + ImageBuffer.OrderA]);
				return true;
			}
			finally
			{
				context.ActiveClipsAndMasks.Remove(clip);
			}
		}

		/// <summary>
		/// One clipPath child as opaque coverage: a shape, a text, or a &lt;use&gt; of either (groups and anything
		/// else are not valid clipPath content). Fill and stroke do not matter; clip-rule and visibility do.
		/// </summary>
		private static void DrawClipChild(SvgRenderer.Context context, SvgElement child, SvgStyle style, Affine toPixels, ImageBuffer coverage)
		{
			if (child.Name == "use")
			{
				if (context.Document.GetElementById(child["href"]) is SvgElement referenced && referenced.Name != "use" && referenced.Name != "g")
				{
					Affine offset = SvgTransform.Resolve(referenced, "transform", context.ViewportWidth, context.ViewportHeight) * Affine.NewTranslation(
						SvgLength.Parse(child["x"], 0, context.ViewportWidth),
						SvgLength.Parse(child["y"], 0, context.ViewportHeight)) * toPixels;
					DrawClipChild(context, referenced, SvgStyle.Compute(referenced, style, context.Diagonal), offset, coverage);
				}

				return;
			}

			var paths = new List<(VertexStorage Path, SvgStyle Style)>();
			if (child.Name == "text")
			{
				paths.AddRange(SvgText.Layout(child, style, context.ViewportWidth, context.ViewportHeight, context.Diagonal, context.Document.FontResolver, context.Document.Fonts, context.Document));
			}
			else if (SvgShapes.ToPath(child, context.ViewportWidth, context.ViewportHeight, style.FontSize) is VertexStorage path)
			{
				paths.Add((path, style));
			}

			Graphics2D graphics = coverage.NewGraphics2D();
			foreach ((VertexStorage path, SvgStyle pathStyle) in paths.Where(p => p.Style.Visible))
			{
				graphics.Rasterizer.filling_rule(pathStyle.ClipEvenOdd ? Util.filling_rule_e.fill_even_odd : Util.filling_rule_e.fill_non_zero);
				graphics.Render(new VertexSourceApplyTransform(new FlattenCurves(path), toPixels), Color.White);
			}

			graphics.Rasterizer.filling_rule(Util.filling_rule_e.fill_non_zero);
		}

		/// <summary>Multiplies <paramref name="layer"/> by <paramref name="mask"/>'s luminance; false when nothing may draw.</summary>
		private static bool ApplyMask(SvgRenderer.Context context, SvgElement mask, RectangleDouble? bounds, Affine transform, ImageBuffer layer, int depth)
		{
			if (depth > MaxDepth || !context.ActiveClipsAndMasks.Add(mask))
			{
				return false;
			}

			try
			{
				bool boxUnits = mask["maskUnits"] != "userSpaceOnUse";
				bool boxContent = mask["maskContentUnits"] == "objectBoundingBox";
				RectangleDouble box = bounds ?? default;
				if ((boxUnits || boxContent) && (bounds == null || box.Width <= 0 || box.Height <= 0))
				{
					return false;
				}

				// The mask region: fractions of the box, or user-space lengths; -10%, -10%, 120%, 120% by default.
				double RegionLength(string name, double fallback, double viewport)
				{
					return boxUnits
						? SvgLength.Parse(mask[name], fallback, 1)
						: SvgLength.Parse(mask[name], fallback * viewport, viewport);
				}

				double x = RegionLength("x", -.1, context.ViewportWidth);
				double y = RegionLength("y", -.1, context.ViewportHeight);
				double width = RegionLength("width", 1.2, context.ViewportWidth);
				double height = RegionLength("height", 1.2, context.ViewportHeight);
				if (width <= 0 || height <= 0)
				{
					return false;
				}

				Affine regionToPixels = (boxUnits ? BoxUnits(box) : Affine.NewIdentity()) * transform;
				ImageBuffer content = NewLayer(layer);
				Affine contentToPixels = (boxContent ? BoxUnits(box) : Affine.NewIdentity()) * transform;
				SvgRenderer.DrawChildren(context, mask, SvgRenderer.InheritedStyle(context, mask), contentToPixels, content, 0);

				ImageBuffer region = NewLayer(layer);
				region.NewGraphics2D().Render(new VertexSourceApplyTransform(SvgShapes.Rect(x, y, width, height, 0, 0), regionToPixels), Color.White);
				Multiply(content, region, (pixels, i) => pixels[i + ImageBuffer.OrderA]);

				// mask on the mask element masks its content, in the masked element's space.
				if (Reference(context, mask, "mask", "mask") is SvgElement nested
					&& !ApplyMask(context, nested, bounds, transform, content, depth + 1))
				{
					return false;
				}

				// Luminance of a premultiplied pixel is the straight colour's luminance times its alpha.
				Func<byte[], int, int> weight = mask["mask-type"] == "alpha"
					? (pixels, i) => pixels[i + ImageBuffer.OrderA]
					: (Func<byte[], int, int>)((pixels, i) => (int)Math.Round(
						.2125 * pixels[i + ImageBuffer.OrderR] + .7154 * pixels[i + ImageBuffer.OrderG] + .0721 * pixels[i + ImageBuffer.OrderB]));
				Multiply(layer, content, weight);
				return true;
			}
			finally
			{
				context.ActiveClipsAndMasks.Remove(mask);
			}
		}

		internal static ImageBuffer NewLayer(ImageBuffer like) => new ImageBuffer(like.Width, like.Height, 32, new BlenderPreMultBGRA());

		/// <summary>Scales every premultiplied pixel of <paramref name="layer"/> by <paramref name="weight"/> (0..255) of the same pixel in <paramref name="by"/>.</summary>
		internal static void Multiply(ImageBuffer layer, ImageBuffer by, Func<byte[], int, int> weight)
		{
			byte[] pixels = layer.GetBuffer();
			byte[] weights = by.GetBuffer();
			for (int i = 0; i + 3 < pixels.Length && i + 3 < weights.Length; i += 4)
			{
				int w = Math.Min(255, weight(weights, i));
				for (int c = 0; c < 4; c++)
				{
					pixels[i + c] = (byte)((pixels[i + c] * w + 127) / 255);
				}
			}

			layer.MarkImageChanged();
		}
	}
}
