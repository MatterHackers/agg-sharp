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
	/// <summary>What a fill or stroke paints with: nothing, a colour, or a paint server (a gradient or pattern) by id.</summary>
	public readonly struct SvgPaint
	{
		private SvgPaint(bool isNone, Color color, string serverId, SvgPaint? fallback)
		{
			this.IsNone = isNone;
			this.Color = color;
			this.ServerId = serverId;
			this.FallbackColor = fallback?.IsNone == false && fallback.Value.ServerId == null ? fallback.Value.Color : (Color?)null;
		}

		public static SvgPaint None { get; } = new SvgPaint(true, default, null, null);

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

		/// <summary>The dash lengths, always an even count; null for a solid stroke.</summary>
		public IReadOnlyList<double> DashArray { get; private set; }

		public double DashOffset { get; private set; }

		public bool Visible { get; private set; } = true;

		/// <summary>font-size in user units (inherited; a percentage or em is of the parent's).</summary>
		public double FontSize { get; private set; } = SvgLength.DefaultFontSize;

		/// <summary>font-family as written (a comma-separated list, names possibly quoted), or null; inherited.</summary>
		public string FontFamily { get; private set; }

		/// <summary>font-weight bold, bolder or 600 and up: drawn in a bold face rather than a regular one.</summary>
		public bool FontBold { get; private set; }

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
		/// marker-start, marker-mid and marker-end: the <c>url(#id)</c> of the marker drawn at a shape's first, middle
		/// and last vertices, or null for none. Inherited, so a group's markers go on every shape in it.
		/// </summary>
		public string MarkerStart { get; private set; }

		public string MarkerMid { get; private set; }

		public string MarkerEnd { get; private set; }

		/// <summary>The element's own opacity (not inherited: a group's is applied to its composited layer).</summary>
		public double Opacity { get; private set; } = 1;

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
			style.StrokeWidth = SvgLength.Parse(Get("stroke-width"), style.StrokeWidth, viewportDiagonal);
			style.MiterLimit = Math.Max(1, SvgLength.ParseNumber(Get("stroke-miterlimit"), style.MiterLimit));
			style.DashOffset = SvgLength.Parse(Get("stroke-dashoffset"), style.DashOffset, viewportDiagonal);

			if (Get("font-size") is string fontSize)
			{
				style.FontSize = fontSize.Trim().EndsWith("em")
					? SvgLength.ParseNumber(fontSize.Trim().TrimEnd('m', 'e'), 1) * style.FontSize
					: SvgLength.Parse(fontSize, style.FontSize, style.FontSize);
			}

			if (Get("font-family") is string family)
			{
				style.FontFamily = family;
			}

			switch (Get("font-weight"))
			{
				case "bold":
				case "bolder":
					style.FontBold = true;
					break;
				case "normal":
				case "lighter":
					style.FontBold = false;
					break;
				case string weight when double.TryParse(weight, NumberStyles.Float, CultureInfo.InvariantCulture, out double numeric):
					style.FontBold = numeric >= 600;
					break;
			}

			if (Get("text-anchor") is string anchor)
			{
				style.TextAnchor = anchor;
			}

			if (Get("text-decoration") is string decoration)
			{
				style.TextDecoration = decoration == "none" ? null : decoration;
			}

			style.LetterSpacing = Get("letter-spacing") == "normal" ? 0 : SvgLength.Parse(Get("letter-spacing"), style.LetterSpacing, style.FontSize);
			style.WordSpacing = Get("word-spacing") == "normal" ? 0 : SvgLength.Parse(Get("word-spacing"), style.WordSpacing, style.FontSize);

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

		private SvgStyle InheritedCopy()
		{
			var copy = (SvgStyle)this.MemberwiseClone();
			copy.Opacity = 1;
			copy.DisplayNone = false;
			return copy;
		}
	}
}
