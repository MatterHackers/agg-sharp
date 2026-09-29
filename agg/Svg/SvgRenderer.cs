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
					Affine nested = NestedViewport(context, element) * transform;
					WithOpacity(context, target, opacity, layer => DrawChildren(context, element, style, nested, layer, useDepth));
					break;
				case "use":
					SvgElement referenced = context.Document.GetElementById(element["href"]);
					if (referenced != null && useDepth < MaxUseDepth && !IsAncestorOrSelf(referenced, element))
					{
						Affine offset = Affine.NewTranslation(
							SvgLength.Parse(element["x"], 0, context.ViewportWidth),
							SvgLength.Parse(element["y"], 0, context.ViewportHeight)) * transform;
						if (referenced.Name == "symbol")
						{
							WithOpacity(context, target, opacity, layer => DrawSymbol(context, element, referenced, style, transform, layer, useDepth + 1));
						}
						else
						{
							WithOpacity(context, target, opacity, layer => Draw(context, referenced, style, offset, layer, useDepth + 1));
						}
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
						foreach ((VertexStorage run, SvgStyle runStyle) in SvgText.Layout(element, style, context.ViewportWidth, context.ViewportHeight, context.Diagonal, context.Document.FontResolver, context.Document.Fonts).Where(r => r.Style.Visible))
						{
							DrawShape(context, run, runStyle, transform, layer);
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
							SvgMarker.Draw(context, path, style, transform, layer, useDepth);
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
			Affine contentToPixels = Affine.NewTranslation(x, y) * useTransform;
			if (SvgViewport.ParseViewBox(symbol["viewBox"]) is RectangleDouble viewBox && width > 0 && height > 0)
			{
				contentToPixels = SvgViewport.ViewBoxTransform(viewBox, symbol["preserveAspectRatio"], width, height) * contentToPixels;
			}

			SvgStyle symbolStyle = SvgStyle.Compute(symbol, useStyle, context.Diagonal);
			(double viewportWidth, double viewportHeight) = (context.ViewportWidth, context.ViewportHeight);
			context.ViewportWidth = use["width"] != null && width > 0 ? width : viewportWidth;
			context.ViewportHeight = use["height"] != null && height > 0 ? height : viewportHeight;
			try
			{
				string overflow = symbol["overflow"];
				Action<ImageBuffer> drawChildren = layer => DrawChildren(context, symbol, symbolStyle, contentToPixels, layer, useDepth);
				Action<ImageBuffer> draw = drawChildren;
				if (overflow != "visible" && overflow != "auto" && width > 0 && height > 0)
				{
					draw = layer =>
					{
						ImageBuffer content = SvgClipMask.NewLayer(layer);
						drawChildren(content);
						ImageBuffer coverage = SvgClipMask.NewLayer(layer);
						coverage.NewGraphics2D().Render(new VertexSourceApplyTransform(SvgShapes.Rect(x, y, width, height, 0, 0), useTransform), Color.White);
						SvgClipMask.Multiply(content, coverage, (pixels, i) => pixels[i + ImageBuffer.OrderA]);
						CompositeLayer(layer, content, 1);
					};
				}

				WithOpacity(context, target, symbolStyle.Opacity, draw);
			}
			finally
			{
				(context.ViewportWidth, context.ViewportHeight) = (viewportWidth, viewportHeight);
			}
		}

		/// <summary>A nested &lt;svg&gt;: moved to its x/y and, with a viewBox, fitted into its width/height.</summary>
		private static Affine NestedViewport(Context context, SvgElement element)
		{
			Affine offset = Affine.NewTranslation(
				SvgLength.Parse(element["x"], 0, context.ViewportWidth),
				SvgLength.Parse(element["y"], 0, context.ViewportHeight));
			RectangleDouble? viewBox = SvgViewport.ParseViewBox(element["viewBox"]);
			if (viewBox == null)
			{
				return offset;
			}

			double width = SvgLength.Parse(element["width"], context.ViewportWidth, context.ViewportWidth);
			double height = SvgLength.Parse(element["height"], context.ViewportHeight, context.ViewportHeight);
			return SvgViewport.ViewBoxTransform(viewBox.Value, element["preserveAspectRatio"], width, height) * offset;
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

		/// <summary>Fills then strokes <paramref name="path"/> (user space) with <paramref name="transform"/> to pixels.</summary>
		private static void DrawShape(Context context, VertexStorage path, SvgStyle style, Affine transform, ImageBuffer target)
		{
			// Curves are flattened in user space, finely enough for the scale they are drawn at, so the stroker
			// (which needs straight segments) and the fill see the same outline.
			double scale = Math.Sqrt(Math.Abs(transform.sx * transform.sy - transform.shx * transform.shy));
			if (scale <= 0)
			{
				return;
			}

			var flattened = new FlattenCurves(path) { ResolutionScale = scale };
			Graphics2D graphics = target.NewGraphics2D();
			SvgServerPaint fill = Paint(context, style.Fill, style.FillOpacity, flattened, transform);
			if (!fill.IsNone)
			{
				Fill(graphics, target, new VertexSourceApplyTransform(flattened, transform), fill, style.FillEvenOdd);
			}

			SvgServerPaint stroke = style.StrokeWidth > 0 ? Paint(context, style.Stroke, style.StrokeOpacity, flattened, transform) : default;
			if (!stroke.IsNone)
			{
				IVertexSource outline = flattened;
				if (style.DashArray != null)
				{
					var dash = new Dash(flattened);
					for (int i = 0; i + 1 < style.DashArray.Count; i += 2)
					{
						dash.AddDash(style.DashArray[i], style.DashArray[i + 1]);
					}

					dash.DashStart(style.DashOffset);
					outline = dash;
				}

				var strokeOutline = new Stroke(outline, style.StrokeWidth)
				{
					LineCap = style.LineCap,
					LineJoin = style.LineJoin,
					MiterLimit = style.MiterLimit,
					ApproximationScale = scale,
				};
				Fill(graphics, target, new VertexSourceApplyTransform(strokeOutline, transform), stroke, false);
			}
		}

		/// <summary>
		/// What <paramref name="paint"/> draws with on a shape of outline <paramref name="shape"/>: its gradient,
		/// else its colour (or, for a server that is not a gradient, its fallback colour).
		/// </summary>
		private static SvgServerPaint Paint(Context context, SvgPaint paint, double opacity, IVertexSource shape, Affine transform)
		{
			if (paint.ServerId != null && !paint.IsNone)
			{
				SvgElement server = context.Document.GetElementById(paint.ServerId);
				if (SvgGradient.Resolve(context.Document, server, shape.GetBounds(), transform, opacity, context.ViewportWidth, context.ViewportHeight) is SvgServerPaint gradient)
				{
					return gradient;
				}

				// A pattern used inside its own content would recurse forever: that use gets the fallback colour.
				if (server != null && !context.ActivePatterns.Contains(server)
					&& SvgPattern.Resolve(context.Document, server, shape.GetBounds(), transform, opacity, context.ViewportWidth, context.ViewportHeight, (owner, contentToTile, tile) => DrawPatternContent(context, server, owner, contentToTile, tile)) is SvgServerPaint pattern)
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

		private static void Fill(Graphics2D graphics, ImageBuffer target, IVertexSource pixels, SvgServerPaint paint, bool evenOdd)
		{
			if (paint.Spans == null)
			{
				graphics.Rasterizer.filling_rule(evenOdd ? Util.filling_rule_e.fill_even_odd : Util.filling_rule_e.fill_non_zero);
				graphics.Render(pixels, paint.Solid.Value);
				graphics.Rasterizer.filling_rule(Util.filling_rule_e.fill_non_zero);
				return;
			}

			// A bare rasterizer has no clip box, and the renderer writes every span it is given: without this a shape
			// reaching past the target indexes outside its rows.
			var rasterizer = new ScanlineRasterizer();
			rasterizer.SetVectorClipBox(0, 0, target.Width, target.Height);
			rasterizer.filling_rule(evenOdd ? Util.filling_rule_e.fill_even_odd : Util.filling_rule_e.fill_non_zero);
			rasterizer.add_path(pixels);
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), target, new span_allocator(), paint.Spans);
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

			/// <summary>The viewport's normalized diagonal, what a percentage of neither width nor height is of.</summary>
			public double Diagonal => Math.Sqrt((this.ViewportWidth * this.ViewportWidth + this.ViewportHeight * this.ViewportHeight) / 2);
		}
	}
}
