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
using System.Globalization;
using System.Linq;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// What a fill or stroke paints with: nothing, a colour, a paint server (a gradient or pattern) by id, or
	/// context-fill / context-stroke - the context element's paint, resolved when drawing.
	/// </summary>
	public readonly struct SvgPaint
	{
		private SvgPaint(bool isNone, Color color, string serverId, SvgPaint? fallback, SvgContextPaintKind context = SvgContextPaintKind.None)
		{
			this.IsNone = isNone;
			this.Color = color;
			this.ServerId = serverId;
			this.FallbackColor = fallback?.IsNone == false && fallback.Value.ServerId == null ? fallback.Value.Color : (Color?)null;
			this.Context = context;
		}

		public static SvgPaint None { get; } = new SvgPaint(true, default, null, null);

		/// <summary>Which of the context element's paints this stands for, when it is context-fill or context-stroke.</summary>
		public SvgContextPaintKind Context { get; }

		public bool IsNone { get; }

		/// <summary>The colour, when this is neither none nor a paint server.</summary>
		public Color Color { get; }

		/// <summary>The id of the paint server <c>url(#id)</c> names, or null.</summary>
		public string ServerId { get; }

		/// <summary>The colour after a <c>url(#id)</c>, used when the server is missing or not supported yet.</summary>
		public Color? FallbackColor { get; }

		public static SvgPaint FromColor(Color color) => new SvgPaint(false, color, null, null);

		/// <summary>
		/// Parses a fill or stroke value; null when it is not a valid paint (an unknown colour name), so the
		/// caller keeps the inherited one.
		/// </summary>
		public static SvgPaint? Parse(string text, Color currentColor)
		{
			string value = text.Trim();
			if (value == "none")
			{
				return None;
			}

			if (value == "currentColor")
			{
				return FromColor(currentColor);
			}

			// Kept as keywords and inherited so: what they name depends on where the shape is drawn from.
			if (value == "context-fill")
			{
				return new SvgPaint(false, default, null, null, SvgContextPaintKind.Fill);
			}

			if (value == "context-stroke")
			{
				return new SvgPaint(false, default, null, null, SvgContextPaintKind.Stroke);
			}

			if (value.StartsWith("url("))
			{
				int close = value.IndexOf(')');
				if (close < 0)
				{
					return null;
				}

				string id = value.Substring(4, close - 4).Trim().Trim('\'', '"').TrimStart('#');
				string rest = value.Substring(close + 1).Trim();
				SvgPaint? fallback = rest.Length > 0 ? Parse(rest, currentColor) : null;
				return new SvgPaint(false, default, id, fallback);
			}

			return SvgColor.TryParse(value, out Color color) ? FromColor(color) : (SvgPaint?)null;
		}
	}

	/// <summary>Whether a paint is context-fill, context-stroke, or neither.</summary>
	public enum SvgContextPaintKind
	{
		None,
		Fill,
		Stroke,
	}

	/// <summary>
	/// The computed presentation properties of one element: its own attributes and style declarations over
	/// what it inherits from its parent. Opacity and display are not inherited; the rest are, as SVG says.
	/// </summary>
	public class SvgStyle
	{
		public Color CurrentColor { get; private set; } = Color.Black;

		public SvgPaint Fill { get; private set; } = SvgPaint.FromColor(Color.Black);

		public double FillOpacity { get; private set; } = 1;

		public bool FillEvenOdd { get; private set; }

		/// <summary>clip-rule evenodd: how a clipPath child's outline covers (inherited, as fill-rule is).</summary>
		public bool ClipEvenOdd { get; private set; }

		public SvgPaint Stroke { get; private set; } = SvgPaint.None;

		public double StrokeWidth { get; private set; } = 1;

		public double StrokeOpacity { get; private set; } = 1;

		public LineCap LineCap { get; private set; } = LineCap.Butt;

		public LineJoin LineJoin { get; private set; } = LineJoin.MiterRevert;

		public double MiterLimit { get; private set; } = 4;

		/// <summary>shape-rendering (inherited): resvg draws crispEdges and optimizeSpeed without anti-aliasing.</summary>
		public bool AntiAlias { get; private set; } = true;

		/// <summary>The dash lengths, always an even count; null for a solid stroke.</summary>
		public IReadOnlyList<double> DashArray { get; private set; }

		public double DashOffset { get; private set; }

		public bool Visible { get; private set; } = true;

		/// <summary>font-size in user units (inherited; a percentage or em is of the parent's).</summary>
		public double FontSize { get; private set; } = SvgLength.DefaultFontSize;

		/// <summary>font-family as written (a comma-separated list, names possibly quoted), or null; inherited.</summary>
		public string FontFamily { get; private set; }

		/// <summary>font-weight 600 and up: drawn in a bold face rather than a regular one.</summary>
		public bool FontBold => FontWeight >= 600;

		/// <summary>font-weight as a number from 100 to 900 (normal is 400), bolder and lighter stepping from the parent's.</summary>
		public int FontWeight { get; private set; } = 400;

		/// <summary>font-style: "normal", "italic" or "oblique".</summary>
		public string FontStyle { get; private set; } = "normal";

		/// <summary>font-stretch as a CSS width class, 1 (ultra-condensed) to 9 (ultra-expanded); 5 is normal.</summary>
		public int FontStretch { get; private set; } = 5;

		/// <summary>text-anchor: "start", "middle" or "end".</summary>
		public string TextAnchor { get; private set; } = "start";

		/// <summary>
		/// text-decoration's lines ("underline", "overline", "line-through", space separated), or null. Not inherited
		/// in CSS, but a decoration is drawn across all of its element's descendant text, which inheriting it gives.
		/// </summary>
		public string TextDecoration { get; private set; }

		/// <summary>letter-spacing and word-spacing in user units, added to each glyph's (each space's) advance.</summary>
		public double LetterSpacing { get; private set; }

		public double WordSpacing { get; private set; }

		/// <summary>
		/// False when kerning="0" or font-kerning="none" (both inherited) turns the font's pair kerning off, as usvg reads them.
		/// </summary>
		public bool Kerning => !kerningZero && !fontKerningNone;

		private bool kerningZero;
		private bool fontKerningNone;

		/// <summary>
		/// marker-start, marker-mid and marker-end: the <c>url(#id)</c> of the marker drawn at a shape's first, middle
		/// and last vertices, or null for none. Inherited, so a group's markers go on every shape in it.
		/// </summary>
		public string MarkerStart { get; private set; }

		public string MarkerMid { get; private set; }

		public string MarkerEnd { get; private set; }

		/// <summary>The element's own opacity (not inherited: a group's is applied to its composited layer).</summary>
		public double Opacity { get; private set; } = 1;

		/// <summary>The element's mix-blend-mode (not inherited): how its layer mixes with what is drawn behind it.</summary>
		public string MixBlendMode { get; private set; } = "normal";

		/// <summary>isolation: isolate (not inherited): the element's children blend only with each other.</summary>
		public bool Isolate { get; private set; }

		/// <summary>display="none": the element and its children draw nothing.</summary>
		public bool DisplayNone { get; private set; }

		/// <summary>
		/// The style of <paramref name="element"/> under <paramref name="parent"/> (null for the defaults), with
		/// percentage stroke widths and dashes relative to <paramref name="viewportDiagonal"/> (the viewport's
		/// diagonal over √2, as SVG defines it).
		/// </summary>
		public static SvgStyle Compute(SvgElement element, SvgStyle parent, double viewportDiagonal)
		{
			SvgStyle style = parent?.InheritedCopy() ?? new SvgStyle();
			string Get(string name)
			{
				string value = element[name];
				return value == null || value == "inherit" ? null : value;
			}

			if (Get("color") is string color && SvgColor.TryParse(color, out Color current))
			{
				style.CurrentColor = current;
			}

			if (Get("fill") is string fill && SvgPaint.Parse(fill, style.CurrentColor) is SvgPaint fillPaint)
			{
				style.Fill = fillPaint;
			}

			if (Get("stroke") is string stroke && SvgPaint.Parse(stroke, style.CurrentColor) is SvgPaint strokePaint)
			{
				style.Stroke = strokePaint;
			}

			style.FillOpacity = Clamp01(SvgLength.ParseNumber(Get("fill-opacity"), style.FillOpacity));
			style.StrokeOpacity = Clamp01(SvgLength.ParseNumber(Get("stroke-opacity"), style.StrokeOpacity));
			style.Opacity = Clamp01(SvgLength.ParseNumber(Get("opacity"), 1));
			style.MixBlendMode = Get("mix-blend-mode")?.Trim() ?? "normal";
			style.Isolate = Get("isolation")?.Trim() == "isolate";
			// First: em and ex in the lengths below are this element's font size. In font-size itself they, and
			// percentages, are the parent's; named sizes step 1.2 apart from it (usvg's convert_named_font_size).
			if (Get("font-size") is string fontSize)
			{
				int? step = fontSize.Trim() switch
				{
					"xx-small" => -3,
					"x-small" => -2,
					"small" or "smaller" => -1,
					"medium" => 0,
					"large" or "larger" => 1,
					"x-large" => 2,
					"xx-large" => 3,
					_ => null,
				};
				style.FontSize = step is int named
					? style.FontSize * Math.Pow(1.2, named)
					: SvgLength.Parse(fontSize, style.FontSize, style.FontSize, style.FontSize);
			}

			style.StrokeWidth = SvgLength.Parse(Get("stroke-width"), style.StrokeWidth, viewportDiagonal, style.FontSize);
			style.MiterLimit = Math.Max(1, SvgLength.ParseNumber(Get("stroke-miterlimit"), style.MiterLimit));
			style.DashOffset = SvgLength.Parse(Get("stroke-dashoffset"), style.DashOffset, viewportDiagonal, style.FontSize);

			if (Get("font-family") is string family)
			{
				style.FontFamily = family;
			}

			// usvg's steps: bolder from normal goes to 700 and lighter to 200, as Chrome and Inkscape draw them; a
			// weight that is not one of the nine keywords' numbers is ignored.
			switch (Get("font-weight"))
			{
				case "normal":
					style.FontWeight = 400;
					break;
				case "bold":
					style.FontWeight = 700;
					break;
				case "bolder":
					style.FontWeight = Math.Min(900, style.FontWeight + (style.FontWeight == 400 ? 300 : 100));
					break;
				case "lighter":
					style.FontWeight = Math.Max(100, style.FontWeight - (style.FontWeight == 400 ? 200 : 100));
					break;
				case "100" or "200" or "300" or "400" or "500" or "600" or "700" or "800" or "900":
					style.FontWeight = int.Parse(Get("font-weight"), CultureInfo.InvariantCulture);
					break;
			}

			if (Get("font-style") is "normal" or "italic" or "oblique")
			{
				style.FontStyle = Get("font-style");
			}

			if (Get("font-stretch") is string stretch)
			{
				style.FontStretch = stretch switch
				{
					"ultra-condensed" => 1,
					"extra-condensed" => 2,
					"condensed" or "narrower" => 3,
					"semi-condensed" => 4,
					"semi-expanded" => 6,
					"expanded" or "wider" => 7,
					"extra-expanded" => 8,
					"ultra-expanded" => 9,
					_ => 5,
				};
			}

			if (Get("text-anchor") is string anchor)
			{
				style.TextAnchor = anchor;
			}

			if (Get("text-decoration") is string decoration)
			{
				style.TextDecoration = decoration == "none" ? null : decoration;
			}

			// A percentage is of the viewport's diagonal, as usvg converts any length it has no axis for.
			style.LetterSpacing = Get("letter-spacing") == "normal" ? 0 : SvgLength.Parse(Get("letter-spacing"), style.LetterSpacing, viewportDiagonal, style.FontSize);
			style.WordSpacing = Get("word-spacing") == "normal" ? 0 : SvgLength.Parse(Get("word-spacing"), style.WordSpacing, viewportDiagonal, style.FontSize);
			if (Get("kerning") is string kerning)
			{
				style.kerningZero = SvgLength.Parse(kerning, -1, viewportDiagonal, style.FontSize) == 0;
			}

			if (Get("font-kerning") is string fontKerning)
			{
				style.fontKerningNone = fontKerning == "none";
			}

			switch (Get("fill-rule"))
			{
				case "evenodd":
					style.FillEvenOdd = true;
					break;
				case "nonzero":
					style.FillEvenOdd = false;
					break;
			}

			switch (Get("clip-rule"))
			{
				case "evenodd":
					style.ClipEvenOdd = true;
					break;
				case "nonzero":
					style.ClipEvenOdd = false;
					break;
			}

			switch (Get("shape-rendering"))
			{
				case "optimizeSpeed":
				case "crispEdges":
					style.AntiAlias = false;
					break;
				case "auto":
				case "geometricPrecision":
					style.AntiAlias = true;
					break;
			}

			switch (Get("stroke-linecap"))
			{
				case "butt":
					style.LineCap = LineCap.Butt;
					break;
				case "round":
					style.LineCap = LineCap.Round;
					break;
				case "square":
					style.LineCap = LineCap.Square;
					break;
			}

			// SVG's miter join bevels once the miter limit is passed; agg's plain Miter clips the point instead,
			// its MiterRevert is the one that falls back to a bevel.
			switch (Get("stroke-linejoin"))
			{
				case "miter":
				case "miter-clip":
					style.LineJoin = LineJoin.MiterRevert;
					break;
				case "round":
					style.LineJoin = LineJoin.Round;
					break;
				case "bevel":
					style.LineJoin = LineJoin.Bevel;
					break;
			}

			if (Get("stroke-dasharray") is string dashes)
			{
				style.DashArray = ParseDashArray(dashes, viewportDiagonal);
			}

			string Marker(string name, string inherited) => Get(name) is string value ? (value == "none" ? null : value) : inherited;
			style.MarkerStart = Marker("marker-start", style.MarkerStart);
			style.MarkerMid = Marker("marker-mid", style.MarkerMid);
			style.MarkerEnd = Marker("marker-end", style.MarkerEnd);

			if (Get("visibility") is string visibility)
			{
				style.Visible = visibility == "visible";
			}

			style.DisplayNone = element["display"] == "none";
			return style;
		}

		/// <summary>
		/// stroke-dasharray: "none", or lengths that repeat to an even count. A negative length makes the whole
		/// list invalid and a list summing to zero draws solid; both are null, a solid stroke.
		/// </summary>
		private static IReadOnlyList<double> ParseDashArray(string text, double viewportDiagonal)
		{
			if (text.Trim() == "none")
			{
				return null;
			}

			List<double> dashes = text.Split(new[] { ',', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(d => SvgLength.Parse(d, -1, viewportDiagonal))
				.ToList();
			if (dashes.Count == 0 || dashes.Any(d => d < 0) || dashes.Sum() <= 0)
			{
				return null;
			}

			if (dashes.Count % 2 == 1)
			{
				dashes.AddRange(dashes.ToList());
			}

			return dashes;
		}

		private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));

		/// <summary>
		/// This style as text glyphs fill in: nonzero, whatever fill-rule says - it does not apply to text, and usvg
		/// ignores it there.
		/// </summary>
		internal SvgStyle ForGlyphs()
		{
			if (!FillEvenOdd)
			{
				return this;
			}

			var copy = (SvgStyle)this.MemberwiseClone();
			copy.FillEvenOdd = false;
			return copy;
		}

		private SvgStyle InheritedCopy()
		{
			var copy = (SvgStyle)this.MemberwiseClone();
			copy.Opacity = 1;
			copy.MixBlendMode = "normal";
			copy.Isolate = false;
			copy.DisplayNone = false;
			return copy;
		}
	}
}
