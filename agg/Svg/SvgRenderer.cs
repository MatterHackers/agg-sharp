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
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// Draws an <see cref="SvgDocument"/> with agg's scanline renderer: shapes filled and stroked with solid
	/// colours, gradients and patterns, images, text, markers, transforms, viewBox fitting, &lt;use&gt;, clip paths and
	/// masks, filters (see <see cref="SvgFilter"/>), and group opacity through an offscreen layer. Drawing is
	/// premultiplied, as compositing layers needs; <see cref="RenderToImage"/> hands back straight alpha.
	/// </summary>
	/// <remarks>
	/// Not yet drawn: some filter primitives.
	/// </remarks>
	public static class SvgRenderer
	{
		/// <summary>How deep &lt;use&gt; may nest before a (possibly self-referencing) chain is cut off.</summary>
		private const int MaxUseDepth = 16;

		/// <summary>
		/// Renders <paramref name="document"/> into a new <paramref name="width"/> by <paramref name="height"/>
		/// image, the document's size stretched to fill it, as a resvg reference render is. The result has
		/// straight (not premultiplied) alpha, like any other agg image.
		/// </summary>
		public static ImageBuffer RenderToImage(SvgDocument document, int width, int height)
		{
			var premultiplied = new ImageBuffer(width, height, 32, new BlenderPreMultBGRA());
			Render(document, premultiplied);
			return Unpremultiply(premultiplied);
		}

		/// <summary>
		/// Draws <paramref name="document"/> over <paramref name="premultipliedTarget"/> (an image with a
		/// premultiplied blender), filling it; the SVG's top is the image's top.
		/// </summary>
		public static void Render(SvgDocument document, ImageBuffer premultipliedTarget)
		{
			(double docWidth, double docHeight) = document.Size;

			// Document units to pixels, then SVG's y-down to agg's y-up.
			Affine toPixels = DocumentToViewport(document)
				* Affine.NewScaling(premultipliedTarget.Width / docWidth, premultipliedTarget.Height / docHeight)
				* new Affine(1, 0, 0, -1, 0, premultipliedTarget.Height);
			DrawDocument(document, toPixels, premultipliedTarget);
		}

		/// <summary>
		/// The root's user space to the document's own 0..<see cref="SvgDocument.Size"/> space: its viewBox fitted
		/// by its preserveAspectRatio, or nothing without a viewBox.
		/// </summary>
		internal static Affine DocumentToViewport(SvgDocument document)
		{
			(double docWidth, double docHeight) = document.Size;
			RectangleDouble? viewBox = SvgViewport.ParseViewBox(document.Root["viewBox"]);
			return viewBox.HasValue
				? SvgViewport.ViewBoxTransform(viewBox.Value, document.Root["preserveAspectRatio"], docWidth, docHeight)
				: Affine.NewIdentity();
		}

		/// <summary>Draws <paramref name="document"/>'s root with <paramref name="toPixels"/> from its user space.</summary>
		private static void DrawDocument(SvgDocument document, Affine toPixels, ImageBuffer premultipliedTarget)
		{
			(double docWidth, double docHeight) = document.Size;
			SvgElement root = document.Root;
			RectangleDouble? viewBox = SvgViewport.ParseViewBox(root["viewBox"]);

			// usvg rejects a root whose width or height is zero or negative, so resvg draws nothing. Size falls back
			// to the viewBox's for such a root, which other callers of it rely on; drawing must not.
			if (SvgLength.Parse(root["width"], 1, viewBox?.Width ?? 100) <= 0 || SvgLength.Parse(root["height"], 1, viewBox?.Height ?? 100) <= 0)
			{
				return;
			}

			var context = new Context(document, premultipliedTarget)
			{
				ViewportWidth = viewBox?.Width ?? docWidth,
				ViewportHeight = viewBox?.Height ?? docHeight,
			};

			SvgStyle style = SvgStyle.Compute(root, null, context.Diagonal);
			if (!style.DisplayNone && SvgSwitch.ConditionsPass(root))
			{
				WithOpacity(context, premultipliedTarget, style.Opacity, target => DrawChildren(context, root, style, toPixels, target, 0));
			}
		}

		/// <summary>Straight-alpha copy of a premultiplied image, each colour channel divided back out of its alpha.</summary>
		public static ImageBuffer Unpremultiply(ImageBuffer premultiplied)
		{
			var result = new ImageBuffer(premultiplied.Width, premultiplied.Height, 32, new BlenderBGRA());
			byte[] source = premultiplied.GetBuffer();
			byte[] destination = result.GetBuffer();
			for (int i = 0; i + 3 < source.Length && i + 3 < destination.Length; i += 4)
			{
				int alpha = source[i + ImageBuffer.OrderA];
				if (alpha == 0)
				{
					continue;
				}

				for (int c = 0; c < 3; c++)
				{
					destination[i + c] = (byte)Math.Min(255, (source[i + c] * 255 + alpha / 2) / alpha);
				}

				destination[i + ImageBuffer.OrderA] = (byte)alpha;
			}

			return result;
		}

		/// <summary>
		/// Draws <paramref name="element"/> alone, as a &lt;use&gt; would, with <paramref name="transform"/> (the
		/// referencing user space to pixels): its styles inherited down its own ancestors. feImage draws its element so.
		/// </summary>
		internal static void DrawReferenced(Context context, SvgElement element, Affine transform, ImageBuffer target, int useDepth)
		{
			var ancestors = new List<SvgElement>();
			for (SvgElement e = element.Parent; e != null; e = e.Parent)
			{
				ancestors.Insert(0, e);
			}

			SvgStyle style = null;
			foreach (SvgElement ancestor in ancestors)
			{
				style = SvgStyle.Compute(ancestor, style, context.Diagonal);
			}

			if (useDepth < MaxUseDepth)
			{
				Draw(context, element, style, transform, target, useDepth + 1);
			}
		}

		/// <summary>An SVG document drawn with <paramref name="toPixels"/>: what an &lt;image&gt; of an SVG file draws through.</summary>
		internal static void DrawImageDocument(SvgDocument document, Affine toPixels, ImageBuffer premultipliedTarget) => DrawDocument(document, toPixels, premultipliedTarget);

		internal static void DrawChildren(Context context, SvgElement parent, SvgStyle parentStyle, Affine transform, ImageBuffer target, int useDepth)
		{
			foreach (SvgElement child in parent.Children)
			{
				Draw(context, child, parentStyle, transform, target, useDepth);
			}
		}

		private static void Draw(Context context, SvgElement element, SvgStyle parentStyle, Affine parentTransform, ImageBuffer target, int useDepth)
		{
			SvgStyle style = SvgStyle.Compute(element, parentStyle, context.Diagonal);
			if (style.DisplayNone || !SvgSwitch.ConditionsPass(element))
			{
				return;
			}

			// agg's a * b applies a first: the element's own transform, then everything above it.
			Affine transform = SvgTransform.Resolve(element, "transform", context.ViewportWidth, context.ViewportHeight) * parentTransform;
			bool filtered = SvgFilter.Applies(element);
			bool clipped = SvgClipMask.Applies(context, element);

			// Order, as in resvg: filter, then clip-path and mask, then opacity last - so a filter that replaces the
			// drawing (a flood, say) is still faded by the element's opacity.
			// A blend mode or isolation also needs the element on a layer of its own: the layer is what blends with
			// the backdrop, and a fresh transparent layer is the isolated backdrop its children blend with.
			bool layered = filtered || clipped || style.MixBlendMode != "normal" || style.Isolate;
			double opacity = style.Opacity;
			Action<ImageBuffer> draw = layer => DrawElement(context, element, style, layered ? 1 : opacity, transform, layer, useDepth);
			if (filtered)
			{
				Action<ImageBuffer> unfiltered = draw;
				draw = layer => SvgFilter.Draw(context, element, transform, layer, unfiltered);
			}

			if (clipped)
			{
				Action<ImageBuffer> unclipped = draw;
				draw = layer => SvgClipMask.Draw(context, element, transform, layer, unclipped);
			}

			if (style.MixBlendMode != "normal" || style.Isolate)
			{
				WithBlendedLayer(target, opacity, style.MixBlendMode, draw);
			}
			else if (filtered || clipped)
			{
				WithOpacity(context, target, opacity, draw);
			}
			else
			{
				draw(target);
			}
		}

		/// <summary>
		/// Draws <paramref name="element"/> (its filter, clip-path and mask aside) with <paramref name="transform"/> to
		/// pixels, at <paramref name="opacity"/> (its own, or 1 when the caller applies it after those effects).
		/// </summary>
		private static void DrawElement(Context context, SvgElement element, SvgStyle style, double opacity, Affine transform, ImageBuffer target, int useDepth)
		{
			switch (element.Name)
			{
				case "g":
				case "a":
					WithOpacity(context, target, opacity, layer => DrawChildren(context, element, style, transform, layer, useDepth));
					break;
				case "switch":
					if (SvgSwitch.Chosen(element) is SvgElement chosen)
					{
						WithOpacity(context, target, opacity, layer => Draw(context, chosen, style, transform, layer, useDepth));
					}

					break;
				case "svg":
					DrawNestedSvg(context, element, style, opacity, transform, target, useDepth);
					break;
				case "use":
					SvgElement referenced = context.Document.GetElementById(element["href"]);
					if (referenced != null && useDepth < MaxUseDepth && !IsAncestorOrSelf(referenced, element))
					{
						SvgContextElement outerContext = context.ContextElement;
						context.ContextElement = SvgContextElement.Resolve(outerContext, style, SvgClipMask.ObjectBounds(context, element, 0) ?? default, transform);
						Affine offset = Affine.NewTranslation(
							SvgLength.Parse(element["x"], 0, context.ViewportWidth),
							SvgLength.Parse(element["y"], 0, context.ViewportHeight)) * transform;
						if (referenced.Name == "symbol")
						{
							WithOpacity(context, target, opacity, layer => DrawSymbol(context, element, referenced, style, transform, layer, useDepth + 1));
						}
						else if (referenced.Name == "svg")
						{
							// usvg: the use's width/height, each set or not, replace the svg's - and a use nearer the svg
							// resets both, so an outer use's width does not reach through an inner use's height.
							(double? useWidth, double? useHeight) = (context.UseWidth, context.UseHeight);
							context.UseWidth = element["width"] != null ? SvgLength.Parse(element["width"], context.ViewportWidth, context.ViewportWidth) : null;
							context.UseHeight = element["height"] != null ? SvgLength.Parse(element["height"], context.ViewportHeight, context.ViewportHeight) : null;
							try
							{
								WithOpacity(context, target, opacity, layer => Draw(context, referenced, style, offset, layer, useDepth + 1));
							}
							finally
							{
								(context.UseWidth, context.UseHeight) = (useWidth, useHeight);
							}
						}
						else
						{
							WithOpacity(context, target, opacity, layer => Draw(context, referenced, style, offset, layer, useDepth + 1));
						}

						context.ContextElement = outerContext;
					}

					break;
				case "image":
					if (style.Visible)
					{
						WithOpacity(context, target, opacity, layer => SvgImage.Draw(context.Document, element, transform, layer, context.ViewportWidth, context.ViewportHeight, DrawDocument));
					}

					break;
				case "text":
					// Each run's own visibility decides: a visible tspan shows inside a hidden text.
					WithOpacity(context, target, opacity, layer =>
					{
						foreach ((VertexStorage run, SvgStyle runStyle) in SvgText.Layout(element, style, context.ViewportWidth, context.ViewportHeight, context.Diagonal, context.Document.FontResolver, context.Document.Fonts, context.Document).Where(r => r.Style.Visible))
						{
							DrawShape(context, run, runStyle, transform, layer, antiAlias: true);
						}
					});
					break;
				default:
					VertexStorage path = SvgShapes.ToPath(element, context.ViewportWidth, context.ViewportHeight, style.FontSize);
					if (path != null && style.Visible)
					{
						// Markers draw over the shape (paint-order is not read), inside its opacity.
						WithOpacity(context, target, opacity, layer =>
						{
							DrawShape(context, path, style, transform, layer);
							SvgContextElement outerContext = context.ContextElement;
							context.ContextElement = SvgContextElement.Resolve(outerContext, style, new FlattenCurves(path).GetBounds(), transform);
							SvgMarker.Draw(context, path, style, transform, layer, useDepth);
							context.ContextElement = outerContext;
						});
					}

					break;
			}
		}

		/// <summary>
		/// A &lt;symbol&gt; drawn by a &lt;use&gt;, as usvg does: the use's x/y/width/height (100% by default) are a new
		/// viewport the symbol's viewBox fits into, clipped unless the symbol's overflow is visible or auto. Only the
		/// use's own width/height change what percentages inside are of. The symbol's transform is ignored - SVG 1.1
		/// gives a symbol none, and the resvg suite follows 1.1 there.
		/// </summary>
		private static void DrawSymbol(Context context, SvgElement use, SvgElement symbol, SvgStyle useStyle, Affine useTransform, ImageBuffer target, int useDepth)
		{
			double x = SvgLength.Parse(use["x"], 0, context.ViewportWidth);
			double y = SvgLength.Parse(use["y"], 0, context.ViewportHeight);
			double width = SvgLength.Parse(use["width"], context.ViewportWidth, context.ViewportWidth);
			double height = SvgLength.Parse(use["height"], context.ViewportHeight, context.ViewportHeight);
			SvgStyle symbolStyle = SvgStyle.Compute(symbol, useStyle, context.Diagonal);
			double percentWidth = use["width"] != null && width > 0 ? width : context.ViewportWidth;
			double percentHeight = use["height"] != null && height > 0 ? height : context.ViewportHeight;
			DrawViewport(context, symbol, symbolStyle, symbolStyle.Opacity, useTransform, new RectangleDouble(x, y, x + width, y + height), true, percentWidth, percentHeight, target, useDepth);
		}

		/// <summary>
		/// A nested &lt;svg&gt;, as usvg's convert_svg: its x/y/width/height (100% by default; a referencing use's
		/// width/height replace them) are a new viewport its viewBox fits into, and what percentages inside are of
		/// when it has no viewBox. It is clipped to that rectangle unless its overflow is visible or auto - or unless
		/// it is sized neither by both its own width and height nor by a use: a viewBox-only svg is not clipped.
		/// </summary>
		private static void DrawNestedSvg(Context context, SvgElement svg, SvgStyle style, double opacity, Affine transform, ImageBuffer target, int useDepth)
		{
			double x = SvgLength.Parse(svg["x"], 0, context.ViewportWidth);
			double y = SvgLength.Parse(svg["y"], 0, context.ViewportHeight);
			double width = context.UseWidth ?? SvgLength.Parse(svg["width"], context.ViewportWidth, context.ViewportWidth);
			double height = context.UseHeight ?? SvgLength.Parse(svg["height"], context.ViewportHeight, context.ViewportHeight);
			bool sized = context.UseWidth != null || context.UseHeight != null || (svg["width"] != null && svg["height"] != null);
			RectangleDouble? viewBox = SvgViewport.ParseViewBox(svg["viewBox"]);
			bool validSize = width > 0 && height > 0;
			double percentWidth = viewBox?.Width ?? (validSize ? width : context.ViewportWidth);
			double percentHeight = viewBox?.Height ?? (validSize ? height : context.ViewportHeight);
			DrawViewport(context, svg, style, opacity, transform, new RectangleDouble(x, y, x + width, y + height), sized, percentWidth, percentHeight, target, useDepth);
		}

		/// <summary>
		/// Draws <paramref name="owner"/>'s children in a new viewport, <paramref name="viewport"/> (x/y/width/height as
		/// a y-down rectangle, in the space <paramref name="transform"/> takes to pixels): the owner's viewBox fitted
		/// into it by its preserveAspectRatio, clipped to it when <paramref name="mayClip"/> and the owner's overflow is
		/// not visible or auto, and percentages inside taken of <paramref name="percentWidth"/> by
		/// <paramref name="percentHeight"/>. The shared body of &lt;symbol&gt; and nested &lt;svg&gt;.
		/// </summary>
		private static void DrawViewport(Context context, SvgElement owner, SvgStyle ownerStyle, double opacity, Affine transform, RectangleDouble viewport, bool mayClip, double percentWidth, double percentHeight, ImageBuffer target, int useDepth)
		{
			double width = viewport.Width, height = viewport.Height;
			Affine contentToPixels = Affine.NewTranslation(viewport.Left, viewport.Bottom) * transform;
			if (SvgViewport.ParseViewBox(owner["viewBox"]) is RectangleDouble viewBox && width > 0 && height > 0)
			{
				contentToPixels = SvgViewport.ViewBoxTransform(viewBox, owner["preserveAspectRatio"], width, height) * contentToPixels;
			}

			(double viewportWidth, double viewportHeight) = (context.ViewportWidth, context.ViewportHeight);
			(context.ViewportWidth, context.ViewportHeight) = (percentWidth, percentHeight);
			try
			{
				string overflow = owner["overflow"];
				Action<ImageBuffer> drawChildren = layer => DrawChildren(context, owner, ownerStyle, contentToPixels, layer, useDepth);
				Action<ImageBuffer> draw = drawChildren;
				if (mayClip && overflow != "visible" && overflow != "auto" && width > 0 && height > 0)
				{
					draw = layer =>
					{
						ImageBuffer content = SvgClipMask.NewLayer(layer);
						drawChildren(content);
						ImageBuffer coverage = SvgClipMask.NewLayer(layer);
						coverage.NewGraphics2D().Render(new VertexSourceApplyTransform(SvgShapes.Rect(viewport.Left, viewport.Bottom, width, height, 0, 0), transform), Color.White);
						SvgClipMask.Multiply(content, coverage, (pixels, i) => pixels[i + ImageBuffer.OrderA]);
						CompositeLayer(layer, content, 1);
					};
				}

				WithOpacity(context, target, opacity, draw);
			}
			finally
			{
				(context.ViewportWidth, context.ViewportHeight) = (viewportWidth, viewportHeight);
			}
		}

		private static bool IsAncestorOrSelf(SvgElement candidate, SvgElement element)
		{
			for (SvgElement e = element; e != null; e = e.Parent)
			{
				if (e == candidate)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Fills then strokes <paramref name="path"/> (user space) with <paramref name="transform"/> to pixels;
		/// <paramref name="antiAlias"/> overrides the style's shape-rendering, which text does not follow.
		/// </summary>
		private static void DrawShape(Context context, VertexStorage path, SvgStyle style, Affine transform, ImageBuffer target, bool? antiAlias = null)
		{
			// Curves are flattened in user space, finely enough for the scale they are drawn at, so the stroker
			// (which needs straight segments) and the fill see the same outline.
			double scale = Math.Sqrt(Math.Abs(transform.sx * transform.sy - transform.shx * transform.shy));
			if (scale <= 0)
			{
				return;
			}

			var flattened = new FlattenCurves(path) { ResolutionScale = scale };
			SvgServerPaint fill = Paint(context, style.Fill, style.FillOpacity, flattened, transform);
			if (!fill.IsNone)
			{
				Fill(target, new VertexSourceApplyTransform(flattened, transform), fill, style.FillEvenOdd, antiAlias ?? style.AntiAlias);
			}

			SvgServerPaint stroke = style.StrokeWidth > 0 ? Paint(context, style.Stroke, style.StrokeOpacity, flattened, transform) : default;
			if (!stroke.IsNone)
			{
				IVertexSource outline = flattened;
				if (style.DashArray != null)
				{
					// Dashed on the curves, measured at tiny-skia's resolution scale (the longer transformed axis).
					double dashScale = Math.Max(Math.Sqrt(transform.sx * transform.sx + transform.shx * transform.shx), Math.Sqrt(transform.shy * transform.shy + transform.sy * transform.sy));
					VertexStorage dashes = SvgDash.Dash(path, style.DashArray, style.DashOffset, dashScale);
					if (dashes == null)
					{
						return;
					}

					outline = new FlattenCurves(dashes) { ResolutionScale = scale };
				}

				var strokeOutline = new Stroke(outline, style.StrokeWidth)
				{
					LineCap = style.LineCap,
					LineJoin = style.LineJoin,
					MiterLimit = style.MiterLimit,
					ApproximationScale = scale,
				};
				Fill(target, new VertexSourceApplyTransform(strokeOutline, transform), stroke, false, antiAlias ?? style.AntiAlias);
			}
		}

		/// <summary>
		/// What <paramref name="paint"/> draws with on a shape of outline <paramref name="shape"/>: its gradient,
		/// else its colour (or, for a server that is not a gradient, its fallback colour).
		/// </summary>
		private static SvgServerPaint Paint(Context context, SvgPaint paint, double opacity, IVertexSource shape, Affine transform)
		{
			RectangleDouble bounds;
			if (paint.Context != SvgContextPaintKind.None)
			{
				// Painted as the context element would be: over its box, in its user space.
				if (!(context.ContextElement is SvgContextElement contextElement))
				{
					return default;
				}

				(paint, bounds, transform) = (contextElement.Paint(paint.Context), contextElement.Bounds, contextElement.Transform);
			}
			else
			{
				bounds = shape.GetBounds();
			}

			if (paint.ServerId != null && !paint.IsNone)
			{
				SvgElement server = context.Document.GetElementById(paint.ServerId);
				if (SvgGradient.Resolve(context.Document, server, bounds, transform, opacity, context.ViewportWidth, context.ViewportHeight) is SvgServerPaint gradient)
				{
					return gradient;
				}

				// A pattern used inside its own content would recurse forever: that use gets the fallback colour.
				if (server != null && !context.ActivePatterns.Contains(server)
					&& SvgPattern.Resolve(context.Document, server, bounds, transform, opacity, context.ViewportWidth, context.ViewportHeight, (owner, contentToTile, tile) => DrawPatternContent(context, server, owner, contentToTile, tile)) is SvgServerPaint pattern)
				{
					return pattern;
				}
			}

			return new SvgServerPaint(PaintColor(paint, opacity), null);
		}

		/// <summary>
		/// Draws a pattern's content into its tile. The content inherits style from the pattern element's own
		/// ancestors, not from the shape being painted (SVG 1.1 section 13.3).
		/// </summary>
		private static void DrawPatternContent(Context context, SvgElement server, SvgElement owner, Affine contentToTile, ImageBuffer tile)
		{
			context.ActivePatterns.Add(server);
			try
			{
				DrawChildren(context, owner, InheritedStyle(context, owner), contentToTile, tile, 0);
			}
			finally
			{
				context.ActivePatterns.Remove(server);
			}
		}

		/// <summary>The style <paramref name="element"/> computes to under its ancestors, whatever draws it.</summary>
		internal static SvgStyle InheritedStyle(Context context, SvgElement element)
		{
			return SvgStyle.Compute(element, element.Parent == null ? null : InheritedStyle(context, element.Parent), context.Diagonal);
		}

		/// <summary>
		/// Fills <paramref name="pixels"/> (already in pixel space) with <paramref name="paint"/>. Without
		/// <paramref name="antiAlias"/> each pixel is covered or not: a coverage threshold of one half stands in for
		/// tiny-skia's non-anti-aliased fill, which covers the pixels whose centres are inside.
		/// </summary>
		/// <remarks>
		/// Every fill goes straight to <see cref="ScanlineRenderer"/>, never through Graphics2D.Render: ImageGraphics2D
		/// blends a solid fill onto a target labelled premultiplied with straight-over math (widget backbuffers hold
		/// straight colour), while SVG paint is already premultiplied for this genuinely premultiplied target
		/// (<see cref="PaintColor"/>), so going through graphics.Render would blend a premultiplied colour as straight.
		/// </remarks>
		private static void Fill(ImageBuffer target, IVertexSource pixels, SvgServerPaint paint, bool evenOdd, bool antiAlias = true)
		{
			// A bare rasterizer has no clip box, and the renderer writes every span it is given: without this a shape
			// reaching past the target indexes outside its rows.
			var rasterizer = new ScanlineRasterizer();
			rasterizer.SetVectorClipBox(0, 0, target.Width, target.Height);
			rasterizer.filling_rule(evenOdd ? Util.filling_rule_e.fill_even_odd : Util.filling_rule_e.fill_non_zero);
			if (!antiAlias)
			{
				rasterizer.gamma(new gamma_threshold(0.5));
			}

			rasterizer.add_path(pixels);
			if (paint.Spans == null)
			{
				new ScanlineRenderer().RenderSolid(target, rasterizer, new scanline_unpacked_8(), paint.Solid.Value);
			}
			else
			{
				new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), target, new span_allocator(), paint.Spans);
			}

			target.MarkImageChanged();
		}

		/// <summary>The premultiplied colour a paint draws with at <paramref name="opacity"/>, or null for none.</summary>
		private static Color? PaintColor(SvgPaint paint, double opacity)
		{
			Color? color = paint.IsNone ? null : paint.ServerId == null ? paint.Color : paint.FallbackColor;
			if (color == null)
			{
				return null;
			}

			Color c = color.Value;
			int alpha = (int)Math.Round(c.alpha * opacity);
			if (alpha <= 0)
			{
				return null;
			}

			int Premultiply(int channel) => (channel * alpha + 127) / 255;
			return new Color(Premultiply(c.red), Premultiply(c.green), Premultiply(c.blue), alpha);
		}

		/// <summary>
		/// Runs <paramref name="draw"/> straight onto <paramref name="target"/> when <paramref name="opacity"/> is 1,
		/// else onto a transparent layer that is then composited over the target at that opacity - so overlapping
		/// children of a translucent group do not show through each other.
		/// </summary>
		private static void WithOpacity(Context context, ImageBuffer target, double opacity, Action<ImageBuffer> draw)
		{
			if (opacity >= 1)
			{
				draw(target);
				return;
			}

			if (opacity <= 0)
			{
				return;
			}

			var layer = new ImageBuffer(target.Width, target.Height, 32, new BlenderPreMultBGRA());
			draw(layer);
			CompositeLayer(target, layer, opacity);
		}

		/// <summary>
		/// Draws onto a transparent layer, then mixes it (faded by <paramref name="opacity"/>) into
		/// <paramref name="target"/> with a CSS blend mode - the target being the nearest isolated group's layer,
		/// or the canvas.
		/// </summary>
		private static void WithBlendedLayer(ImageBuffer target, double opacity, string blendMode, Action<ImageBuffer> draw)
		{
			if (opacity <= 0)
			{
				return;
			}

			var layer = new ImageBuffer(target.Width, target.Height, 32, new BlenderPreMultBGRA());
			draw(layer);
			byte[] source = layer.GetBuffer();
			if (opacity < 1)
			{
				int scale = (int)Math.Round(opacity * 255);
				for (int i = 0; i < source.Length; i++)
				{
					source[i] = (byte)((source[i] * scale + 127) / 255);
				}
			}

			byte[] destination = target.GetBuffer();
			byte[] blended = SvgFilterPrimitives.Blend(source, destination, target.Width, new SvgPixelRect(0, 0, target.Width, target.Height), blendMode);
			Array.Copy(blended, destination, Math.Min(blended.Length, destination.Length));
			target.MarkImageChanged();
		}

		/// <summary>Premultiplied source-over of <paramref name="layer"/> scaled by <paramref name="opacity"/>.</summary>
		internal static void CompositeLayer(ImageBuffer target, ImageBuffer layer, double opacity)
		{
			byte[] destination = target.GetBuffer();
			byte[] source = layer.GetBuffer();
			int scale = (int)Math.Round(opacity * 255);
			for (int i = 0; i + 3 < source.Length && i + 3 < destination.Length; i += 4)
			{
				int sourceAlpha = (source[i + ImageBuffer.OrderA] * scale + 127) / 255;
				if (sourceAlpha == 0)
				{
					continue;
				}

				for (int c = 0; c < 4; c++)
				{
					int s = (source[i + c] * scale + 127) / 255;
					destination[i + c] = (byte)Math.Min(255, s + (destination[i + c] * (255 - sourceAlpha) + 127) / 255);
				}
			}

			target.MarkImageChanged();
		}

		internal sealed class Context
		{
			public Context(SvgDocument document, ImageBuffer target)
			{
				this.Document = document;
				this.Target = target;
			}

			public SvgDocument Document { get; }

			public ImageBuffer Target { get; }

			/// <summary>The patterns whose content is being drawn right now.</summary>
			public HashSet<SvgElement> ActivePatterns { get; } = new HashSet<SvgElement>();

			/// <summary>The clip paths and masks being applied right now: one that reaches itself draws nothing.</summary>
			public HashSet<SvgElement> ActiveClipsAndMasks { get; } = new HashSet<SvgElement>();

			/// <summary>The markers whose content is being drawn right now: a marker used inside itself is skipped.</summary>
			public HashSet<SvgElement> ActiveMarkers { get; } = new HashSet<SvgElement>();

			/// <summary>The elements feImage primitives are drawing right now: one that reaches itself draws nothing.</summary>
			public HashSet<SvgElement> ActiveFeImages { get; } = new HashSet<SvgElement>();

			/// <summary>The root viewport's size in user units: what percentages are of.</summary>
			public double ViewportWidth { get; set; }

			public double ViewportHeight { get; set; }

			/// <summary>
			/// The width and height of the &lt;use&gt; whose &lt;svg&gt; is being drawn, where it gives them: they replace
			/// that svg's own (usvg's use_size).
			/// </summary>
			public double? UseWidth { get; set; }

			public double? UseHeight { get; set; }

			/// <summary>The use or marked path whose fill and stroke context-fill and context-stroke name, or null.</summary>
			public SvgContextElement ContextElement { get; set; }

			/// <summary>The viewport's normalized diagonal, what a percentage of neither width nor height is of.</summary>
			public double Diagonal => Math.Sqrt((this.ViewportWidth * this.ViewportWidth + this.ViewportHeight * this.ViewportHeight) / 2);
		}
	}
}
