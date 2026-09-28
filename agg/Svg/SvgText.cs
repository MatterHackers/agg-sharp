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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// Lays out a &lt;text&gt; element - its text runs and &lt;tspan&gt;s, x/y/dx/dy lists, text-anchor, letter- and
	/// word-spacing and text-decoration - as outlines in user space, one path per run of characters that share a
	/// style. Each font-family in the list is offered to the document's <see cref="SvgDocument.FontResolver"/> in
	/// turn; with none, or none it knows, text is drawn in the embedded Liberation Sans (Bold for bold weights):
	/// agg has no font lookup of its own, and Liberation Sans is metric-compatible with Arial/Helvetica.
	/// </summary>
	internal static class SvgText
	{
		/// <summary>The paths to draw, in order: underlines and overlines under their glyphs, line-throughs over them.</summary>
		public static List<(VertexStorage Path, SvgStyle Style)> Layout(SvgElement text, SvgStyle textStyle, double viewportWidth, double viewportHeight, double viewportDiagonal, Func<string, bool, TypeFace> fontResolver = null)
		{
			var characters = new List<Character>();
			var positioned = new List<(SvgElement Element, int Start, int End)>();
			Collect(text, textStyle, viewportDiagonal, fontResolver, characters, positioned);
			Collapse(characters);

			// x/y/dx/dy lists give the positions of an element's characters (those left after collapsing), counted
			// from its first; an inner element's lists override its ancestors' (positioned is in document order).
			foreach ((SvgElement element, int start, int end) in positioned)
			{
				int first = characters.FindIndex(c => c.Index >= start);
				int last = characters.FindLastIndex(c => c.Index < end);
				if (first < 0 || last < first)
				{
					continue;
				}

				Assign(element["x"], viewportWidth, characters, first, last, (c, v) => c.X = v);
				Assign(element["y"], viewportHeight, characters, first, last, (c, v) => c.Y = v);
				Assign(element["dx"], viewportWidth, characters, first, last, (c, v) => c.Dx = v);
				Assign(element["dy"], viewportHeight, characters, first, last, (c, v) => c.Dy = v);
			}

			// Pen positions; a new absolute x or y starts a text chunk, which text-anchor shifts as a whole.
			double penX = 0, penY = 0;
			var chunks = new List<(int Start, int End)>();
			for (int i = 0; i < characters.Count; i++)
			{
				Character c = characters[i];
				if (i == 0 || c.X.HasValue || c.Y.HasValue)
				{
					chunks.Add((i, i));
				}

				penX = (c.X ?? penX) + (c.Dx ?? 0);
				penY = (c.Y ?? penY) + (c.Dy ?? 0);
				c.Left = penX;
				c.Baseline = penY;
				c.Advance = c.Face.GetAdvanceForCodePoint(c.Code) * c.Scale + c.Style.LetterSpacing + (c.Code == ' ' ? c.Style.WordSpacing : 0);

				// The font's kerning, within a chunk and one face: usvg shapes each chunk's text as one run, tspans
				// and all, so a pair kerns across a tspan boundary but not into a new chunk.
				if (i + 1 < characters.Count && characters[i + 1] is Character next && !next.X.HasValue && !next.Y.HasValue && next.Face == c.Face)
				{
					c.Advance += c.Face.GetKerningForCodePoints(c.Code, next.Code) * c.Scale;
				}

				penX += c.Advance;
				chunks[chunks.Count - 1] = (chunks[chunks.Count - 1].Start, i + 1);
			}

			foreach ((int start, int end) in chunks)
			{
				double width = characters[end - 1].Left + characters[end - 1].Advance - characters[start].Left;
				double shift = characters[start].Style.TextAnchor switch { "middle" => -width / 2, "end" => -width, _ => 0 };
				for (int i = start; i < end; i++)
				{
					characters[i].Left += shift;
				}
			}

			var under = new List<(VertexStorage, SvgStyle)>();
			var glyphs = new List<(VertexStorage, SvgStyle)>();
			var over = new List<(VertexStorage, SvgStyle)>();
			for (int start = 0; start < characters.Count;)
			{
				int end = start + 1;
				while (end < characters.Count && characters[end].Style == characters[start].Style && characters[end].Baseline == characters[start].Baseline)
				{
					end++;
				}

				var outline = new VertexStorage();
				for (int i = start; i < end; i++)
				{
					Character c = characters[i];
					IVertexSource glyph = c.Face.GetGlyphForCodePoint(c.Code);
					if (glyph != null)
					{
						// Font units run y up; SVG's user space runs y down.
						var toUser = Affine.NewScaling(c.Scale, -c.Scale) * Affine.NewTranslation(c.Left, c.Baseline);
						foreach (VertexData vertex in new VertexSourceApplyTransform(glyph, toUser).Vertices())
						{
							if (!vertex.IsStop)
							{
								outline.Add(vertex.Position.X, vertex.Position.Y, vertex.Command, vertex.Hint);
							}
						}
					}
				}

				glyphs.Add((outline, characters[start].Style));
				AddDecorations(characters, start, end, under, over);
				start = end;
			}

			return under.Concat(glyphs).Concat(over).ToList();
		}

		private static void AddDecorations(List<Character> characters, int start, int end, List<(VertexStorage, SvgStyle)> under, List<(VertexStorage, SvgStyle)> over)
		{
			Character first = characters[start];
			string decoration = first.Style.TextDecoration;
			if (string.IsNullOrEmpty(decoration))
			{
				return;
			}

			TypeFace face = first.Face;
			double left = first.Left;
			double right = characters[end - 1].Left + characters[end - 1].Advance;
			double thickness = (face.Underline_thickness > 0 ? face.Underline_thickness : face.UnitsPerEm / 20.0) * first.Scale;
			void Line(List<(VertexStorage, SvgStyle)> into, double centreAboveBaseline)
			{
				// centreAboveBaseline is in font units, up from the baseline.
				double centre = first.Baseline - centreAboveBaseline * first.Scale;
				var line = new VertexStorage();
				line.MoveTo(left, centre - thickness / 2);
				line.LineTo(right, centre - thickness / 2);
				line.LineTo(right, centre + thickness / 2);
				line.LineTo(left, centre + thickness / 2);
				line.ClosePolygon();
				into.Add((line, first.Style));
			}

			foreach (string kind in decoration.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
			{
				switch (kind)
				{
					case "underline":
						Line(under, face.Underline_position);
						break;
					case "overline":
						Line(under, face.Ascent);
						break;
					case "line-through":
						Line(over, (face.X_height > 0 ? face.X_height : face.UnitsPerEm / 2.0) / 2);
						break;
				}
			}
		}

		/// <summary>The characters under <paramref name="element"/> in document order, each with the style it draws in.</summary>
		private static void Collect(SvgElement element, SvgStyle style, double viewportDiagonal, Func<string, bool, TypeFace> fontResolver, List<Character> characters, List<(SvgElement, int, int)> positioned)
		{
			int slot = positioned.Count;
			int start = characters.Count;
			positioned.Add((element, start, start));
			TypeFace face = Face(style, fontResolver);
			foreach (object item in element.Content)
			{
				if (item is string run)
				{
					for (int i = 0; i < run.Length; i++)
					{
						int code = char.IsSurrogatePair(run, i) ? char.ConvertToUtf32(run, i++) : run[i];
						TypeFace drawing = face.ResolveFace(code);
						characters.Add(new Character { Index = characters.Count, Code = code, Style = style, Face = drawing, Scale = style.FontSize / drawing.UnitsPerEm });
					}
				}
				else if (item is SvgElement child && child.Name == "tspan")
				{
					SvgStyle childStyle = SvgStyle.Compute(child, style, viewportDiagonal);
					if (!childStyle.DisplayNone)
					{
						Collect(child, childStyle, viewportDiagonal, fontResolver, characters, positioned);
					}
				}
			}

			positioned[slot] = (element, start, characters.Count);
		}

		/// <summary>
		/// The first face <paramref name="fontResolver"/> gives for the style's font-family list (names unquoted, in
		/// order), else Liberation Sans in the style's weight.
		/// </summary>
		private static TypeFace Face(SvgStyle style, Func<string, bool, TypeFace> fontResolver)
		{
			if (fontResolver != null && style.FontFamily != null)
			{
				foreach (string family in style.FontFamily.Split(','))
				{
					string name = family.Trim().Trim('\'', '"').Trim();
					if (name.Length > 0 && fontResolver(name, style.FontBold) is TypeFace resolved)
					{
						return resolved;
					}
				}
			}

			return style.FontBold ? LiberationSansBoldFont.Instance : LiberationSansFont.Instance;
		}

		/// <summary>
		/// xml:space="default" (SVG 1.1): newlines dropped, tabs made spaces, runs of spaces collapsed to one and
		/// leading and trailing spaces removed - across tspan boundaries, so "a &lt;tspan&gt; b&lt;/tspan&gt;" keeps one
		/// space. Each character keeps its <see cref="Character.Index"/>, so elements still find the ones they hold.
		/// </summary>
		private static void Collapse(List<Character> characters)
		{
			characters.RemoveAll(c => c.Code == '\n' || c.Code == '\r');
			foreach (Character c in characters.Where(c => c.Code == '\t'))
			{
				c.Code = ' ';
			}

			for (int i = characters.Count - 1; i >= 0; i--)
			{
				if (characters[i].Code == ' ' && (i == 0 || characters[i - 1].Code == ' '))
				{
					characters.RemoveAt(i);
				}
			}

			while (characters.Count > 0 && characters[characters.Count - 1].Code == ' ')
			{
				characters.RemoveAt(characters.Count - 1);
			}
		}

		private static void Assign(string list, double percentOf, List<Character> characters, int first, int last, Action<Character, double> set)
		{
			if (string.IsNullOrWhiteSpace(list))
			{
				return;
			}

			string[] values = list.Split(new[] { ',', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < values.Length && first + i <= last; i++)
			{
				set(characters[first + i], SvgLength.Parse(values[i], 0, percentOf));
			}
		}

		private sealed class Character
		{
			/// <summary>Position among the text's characters before whitespace collapsing.</summary>
			public int Index;
			public int Code;
			public SvgStyle Style;
			public TypeFace Face;
			public double Scale;
			public double? X, Y, Dx, Dy;
			public double Left, Baseline, Advance;
		}
	}
}
