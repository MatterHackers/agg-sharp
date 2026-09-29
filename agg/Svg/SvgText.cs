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
	/// style. With <see cref="SvgDocument.Fonts"/> set, the face is CSS-matched from it (family, weight, style,
	/// stretch). Otherwise each font-family in the list is offered to the document's <see cref="SvgDocument.FontResolver"/> in
	/// turn; with none, or none it knows, text is drawn in the embedded Liberation Sans (Bold for bold weights):
	/// agg has no font lookup of its own, and Liberation Sans is metric-compatible with Arial/Helvetica.
	/// </summary>
	internal static class SvgText
	{
		/// <summary>The paths to draw, in order: underlines and overlines under their glyphs, line-throughs over them.</summary>
		public static List<(VertexStorage Path, SvgStyle Style)> Layout(SvgElement text, SvgStyle textStyle, double viewportWidth, double viewportHeight, double viewportDiagonal, Func<string, bool, TypeFace> fontResolver = null, SvgFontSet fonts = null, SvgDocument document = null)
		{
			var characters = new List<Character>();
			var positioned = new List<(SvgElement Element, int Start, int End, double FontSize)>();
			var from = new Source(viewportDiagonal, fontResolver, fonts, (textPath, fontSize) => SvgTextPath.Resolve(textPath, document, viewportWidth, viewportHeight, fontSize), SvgTextContent.Resolve(text, document));
			Collect(text, null, default, textStyle, from, null, false, SvgTextDecoration.ForText(text, textStyle), characters, positioned);

			// x/y/dx/dy lists give the positions of an element's characters (those left after collapsing), counted
			// from its first; an inner element's lists override its ancestors' (positioned is in document order).
			foreach ((SvgElement element, int start, int end, double fontSize) in positioned)
			{
				int first = characters.FindIndex(c => c.Index >= start);
				int last = characters.FindLastIndex(c => c.Index < end);
				if (first < 0 || last < first)
				{
					continue;
				}

				// As in usvg, a textPath's own x/y/dx/dy are not read.
				if (element.Name != "textPath")
				{
					Assign(element["x"], viewportWidth, fontSize, characters, first, last, (c, v) => c.X = v);
					Assign(element["y"], viewportHeight, fontSize, characters, first, last, (c, v) => c.Y = v);
					Assign(element["dx"], viewportWidth, fontSize, characters, first, last, (c, v) => c.Dx = v);
					Assign(element["dy"], viewportHeight, fontSize, characters, first, last, (c, v) => c.Dy = v);
				}

				AssignRotate(element["rotate"], characters, first, last);
			}

			// Skipped characters took their place in the x/y/dx/dy/rotate lists, and only that.
			characters.RemoveAll(c => c.Skip);

			// A new absolute x or y starts a text chunk, which text-anchor shifts as a whole; so do the first
			// character on a textPath and the first after it.
			var chunks = new List<(int Start, int End)>();
			for (int i = 0; i < characters.Count; i++)
			{
				Character c = characters[i];
				if (i == 0 || c.X.HasValue || c.Y.HasValue || c.Path != characters[i - 1].Path)
				{
					chunks.Add((i, i + 1));
				}
				else
				{
					chunks[chunks.Count - 1] = (chunks[chunks.Count - 1].Start, i + 1);
				}
			}

			double penX = 0, penY = 0;
			foreach ((int start, int end) in chunks)
			{
				for (int i = start; i < end; i++)
				{
					// A decoration line breaks where its glyphs stop following on from each other.
					Character c = characters[i];
					c.BreaksDecoration = i == start || (c.Dx ?? 0) != 0 || (c.Dy ?? 0) != 0 || c.Rotate != 0;
				}

				for (int i = start; i < end; i++)
				{
					// The font's kerning, within a chunk and one face: usvg shapes each chunk's text as one run,
					// tspans and all, so a pair kerns across a tspan boundary but not into a new chunk.
					Character c = characters[i];
					c.Width = c.Face.GetAdvanceForCodePoint(c.Code) * c.Scale;
					if (i + 1 < end && characters[i + 1].Face == c.Face && c.Style.Kerning)
					{
						c.Width += c.Face.GetKerningForCodePoints(c.Code, characters[i + 1].Code) * c.Scale;
					}

					// As usvg's apply_letter_spacing, the chunk's last glyph gets no letter-spacing after it: it would
					// widen the chunk's box and shift its anchoring.
					c.Advance = c.Width + (i + 1 < end ? c.Style.LetterSpacing : 0) + (c.Code == ' ' ? c.Style.WordSpacing : 0);
				}

				SvgTextLength.Apply(characters, start, end);
				if (characters[start].Path != null)
				{
					(penX, penY) = PlaceOnPath(characters, start, end);
					continue;
				}

				for (int i = start; i < end; i++)
				{
					Character c = characters[i];
					penX = (c.X ?? penX) + (c.Dx ?? 0);
					penY = (c.Y ?? penY) + (c.Dy ?? 0);
					c.Left = penX;
					c.Baseline = penY + c.BaselineShift;
					penX += c.Advance;
				}

				double width = characters[end - 1].Left + characters[end - 1].Advance - characters[start].Left;
				double shift = Anchor(characters[start].Style, width);
				for (int i = start; i < end; i++)
				{
					Character c = characters[i];
					c.Left += shift;
					c.Placement = Affine.NewRotation(c.Rotate * Math.PI / 180) * Affine.NewTranslation(c.Left, c.Baseline);
				}
			}

			var drawn = new List<(VertexStorage, SvgStyle)>();
			for (int start = 0; start < characters.Count;)
			{
				int end = start + 1;
				while (end < characters.Count && characters[end].Style == characters[start].Style && characters[end].Baseline == characters[start].Baseline && characters[end].Path == characters[start].Path)
				{
					end++;
				}

				var outline = new VertexStorage();
				for (int i = start; i < end; i++)
				{
					Character c = characters[i];
					IVertexSource glyph = c.Face.GetGlyphForCodePoint(c.Code);
					if (glyph != null && !c.Hidden)
					{
						// Font units run y up; SVG's user space runs y down.
						var toUser = Affine.NewScaling(c.Scale, -c.Scale) * c.Placement;
						foreach (VertexData vertex in new VertexSourceApplyTransform(glyph, toUser).Vertices())
						{
							if (!vertex.IsStop)
							{
								outline.Add(vertex.Position.X, vertex.Position.Y, vertex.Command, vertex.Hint);
							}
						}
					}
				}

				// As usvg draws a span: overline and underline, the glyphs, then line-through.
				var over = new List<(VertexStorage, SvgStyle)>();
				SvgTextDecoration.Add(characters, start, end, drawn, over);
				drawn.Add((outline, characters[start].Style.ForGlyphs()));
				drawn.AddRange(over);
				start = end;
			}

			return drawn;
		}

		private static double Anchor(SvgStyle style, double width) => style.TextAnchor switch { "middle" => -width / 2, "end" => -width, _ => 0 };

		/// <summary>
		/// Lays a chunk along its textPath as usvg does: each glyph's advance midpoint goes to the point that far
		/// along the path - from the chunk's x (an extra offset), startOffset and text-anchor, plus the dx so far - and
		/// the glyph turns about it to the path's direction; dy and the baseline shift move it across the path, and
		/// rotate turns it about its own origin. A glyph whose midpoint is off the path is hidden.
		/// </summary>
		/// <returns>Where a chunk after this one starts: one advance on from the last placed glyph's midpoint.</returns>
		private static (double X, double Y) PlaceOnPath(List<Character> characters, int start, int end)
		{
			SvgTextPath path = characters[start].Path;
			double total = 0;
			for (int i = start; i < end; i++)
			{
				total += characters[i].Advance;
			}

			double along = (characters[start].X ?? 0) + path.StartOffset + Anchor(characters[start].Style, total);
			double across = 0, lastX = 0, lastY = 0;
			for (int i = start; i < end; i++)
			{
				Character c = characters[i];
				along += c.Dx ?? 0;
				across += c.Dy ?? 0;
				double half = c.Width / 2;
				if (path.TryPlace(along + half, out double x, out double y, out double angle))
				{
					c.Placement = Affine.NewRotation(c.Rotate * Math.PI / 180) * Affine.NewTranslation(-half, across + c.BaselineShift) * Affine.NewRotation(angle) * Affine.NewTranslation(x, y);
					lastX = x + c.Advance;
					lastY = y;
				}
				else
				{
					c.Hidden = true;
				}

				along += c.Advance;
			}

			return (lastX, lastY);
		}

		/// <summary>The characters under <paramref name="element"/> in document order, each with the style it draws in.</summary>
		/// <param name="parent">The element's parent, null for the &lt;text&gt;: the baseline properties are not inherited
		/// but, as in usvg, fall back to the direct parent's.</param>
		/// <param name="shift">The baseline-shift of the tspans enclosing this one: their lengths summed, in user units
		/// up, and how many were super and sub, which usvg measures in the innermost span's font.</param>
		/// <param name="onPath">The textPath's path the characters lay along, null for none.</param>
		/// <param name="skip">True when the characters draw nothing (they are in a textPath with no path, or one not
		/// directly in the &lt;text&gt;) but, as in usvg, still take their place in the position lists.</param>
		/// <param name="decorations">Each text-decoration drawn over these characters, with the style it draws in.</param>
		private static void Collect(SvgElement element, SvgElement parent, (double Up, int Supers, int Subs) shift, SvgStyle style, Source from, SvgTextPath onPath, bool skip, Dictionary<string, SvgStyle> decorations, List<Character> characters, List<(SvgElement, int, int, double)> positioned)
		{
			int slot = positioned.Count;
			int start = characters.Count;
			positioned.Add((element, start, start, style.FontSize));
			SvgFontSet fonts = from.Fonts;
			TypeFace face = Face(style, from.FontResolver, fonts);
			if (parent != null)
			{
				// usvg ignores the <text>'s own baseline-shift and sums the tspans'.
				string baselineShiftValue = element["baseline-shift"];
				shift = baselineShiftValue switch
				{
					"super" => (shift.Up, shift.Supers + 1, shift.Subs),
					"sub" => (shift.Up, shift.Supers, shift.Subs + 1),
					_ => (shift.Up + ParseBaselineShift(baselineShiftValue, style.FontSize), shift.Supers, shift.Subs),
				};
			}

			// sub and super move by the OS/2 script offsets of this span's own font, at its own size, however
			// deeply they nest.
			double? textLength = SvgTextLength.Read(element, from.ViewportDiagonal, style.FontSize);
			bool spacingAndGlyphs = element["lengthAdjust"] == "spacingAndGlyphs";
			var metrics = new SvgFontMetrics(face);
			double scriptShift = (shift.Supers * metrics.SuperscriptOffset - shift.Subs * metrics.SubscriptOffset) * style.FontSize / face.UnitsPerEm;
			double baselineShift = -(shift.Up + scriptShift) + AlignmentShift(element, parent, face, style.FontSize);
			foreach (object item in from.Content[element])
			{
				if (item is string run)
				{
					for (int i = 0; i < run.Length; i++)
					{
						int code = char.IsSurrogatePair(run, i) ? char.ConvertToUtf32(run, i++) : run[i];
						TypeFace drawing = face.ResolveFace(code);
						if (fonts != null && !drawing.HasGlyph(code) && fonts.FallbackFor(face, code) is TypeFace fallback)
						{
							drawing = fallback;
						}

						characters.Add(new Character { Index = characters.Count, Code = code, Style = style, Face = drawing, Scale = style.FontSize / drawing.UnitsPerEm, BaselineShift = baselineShift, Path = onPath, Skip = skip, Decorations = decorations, Element = element, TextLength = textLength, SpacingAndGlyphs = spacingAndGlyphs });
					}
				}
				else if (item is SvgElement child)
				{
					SvgStyle childStyle = SvgStyle.Compute(child, style, from.ViewportDiagonal);
					if (!childStyle.DisplayNone)
					{
						SvgTextPath childPath = onPath;
						bool childSkip = skip;
						if (child.Name == "textPath")
						{
							// A textPath lays out only directly in the <text>.
							childPath = parent == null ? from.ResolvePath(child, childStyle.FontSize) : null;
							childSkip |= childPath == null;
						}

						Collect(child, element, shift, childStyle, from, childPath, childSkip, SvgTextDecoration.ForChild(decorations, child, childStyle), characters, positioned);
					}
				}
			}

			positioned[slot] = (element, start, characters.Count, style.FontSize);
		}

		/// <summary>
		/// The first face <paramref name="fontResolver"/> gives for the style's font-family list (names unquoted, in
		/// order), else Liberation Sans in the style's weight.
		/// </summary>
		private static TypeFace Face(SvgStyle style, Func<string, bool, TypeFace> fontResolver, SvgFontSet fonts)
		{
			if (fonts != null)
			{
				IEnumerable<string> families = (style.FontFamily ?? "").Split(',').Select(f => f.Trim().Trim('\'', '"').Trim()).Where(f => f.Length > 0);
				if (fonts.Match(families, style.FontWeight, style.FontStyle, style.FontStretch) is TypeFace matched)
				{
					return matched;
				}
			}

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
		/// A baseline-shift length (up, a percentage of the font size); sub, super, baseline and anything else are 0.
		/// </summary>
		private static double ParseBaselineShift(string value, double fontSize)
		{
			return value == null ? 0 : SvgLength.Parse(value, 0, fontSize, fontSize);
		}

		/// <summary>
		/// How far down to move the glyphs so the baseline dominant-baseline or alignment-baseline names sits on the
		/// text's y. These are Chrome's (and usvg's) hardcoded positions from the face's ascent, descent and x-height,
		/// not the font's BASE table, which few fonts have.
		/// </summary>
		private static double AlignmentShift(SvgElement element, SvgElement parent, TypeFace face, double fontSize)
		{
			string Find(string name) => element[name] is string own && own != "inherit" ? own : parent?[name];
			string baseline = Find("alignment-baseline");
			if (baseline == null || baseline == "auto" || baseline == "baseline")
			{
				baseline = Find("dominant-baseline");
			}

			double scale = fontSize / face.UnitsPerEm;
			double ascent = face.Ascent * scale;
			double descent = face.Descent * scale;
			return baseline switch
			{
				"before-edge" or "text-before-edge" => ascent,
				"middle" => new SvgFontMetrics(face).XHeight * scale / 2,
				"central" => ascent - (ascent - descent) / 2,
				"after-edge" or "text-after-edge" or "ideographic" => descent,
				"hanging" => ascent * 0.8,
				"mathematical" => ascent / 2,
				_ => 0,
			};
		}

		/// <summary>
		/// rotate: one angle per character, in degrees clockwise about its origin; characters past the list's end
		/// take its last angle. A list with anything but plain numbers in it ("5mm") is ignored whole, as usvg does.
		/// </summary>
		private static void AssignRotate(string list, List<Character> characters, int first, int last)
		{
			var angles = new List<double>();
			foreach (string value in (list ?? "").Split(new[] { ',', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
			{
				if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double angle))
				{
					return;
				}

				angles.Add(angle);
			}

			for (int i = 0; angles.Count > 0 && first + i <= last; i++)
			{
				characters[first + i].Rotate = angles[Math.Min(i, angles.Count - 1)];
			}
		}

		private static void Assign(string list, double percentOf, double fontSize, List<Character> characters, int first, int last, Action<Character, double> set)
		{
			if (string.IsNullOrWhiteSpace(list))
			{
				return;
			}

			string[] values = list.Split(new[] { ',', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < values.Length && first + i <= last; i++)
			{
				set(characters[first + i], SvgLength.Parse(values[i], 0, percentOf, fontSize));
			}
		}

		internal sealed class Character
		{
			/// <summary>Each text-decoration drawn over the character, keyed by kind, with the style it draws in.</summary>
			public Dictionary<string, SvgStyle> Decorations;
			/// <summary>Starts a chunk, or is moved by dx or dy or rotated: a decoration line breaks before it.</summary>
			public bool BreaksDecoration;
			/// <summary>The element whose text the character is: usvg's span.</summary>
			public SvgElement Element;
			/// <summary>That element's own textLength, null for none.</summary>
			public double? TextLength;
			public bool SpacingAndGlyphs;
						/// <summary>Position among the text's characters, those that draw nothing included.</summary>
			public int Index;
			public int Code;
			public SvgStyle Style;
			public TypeFace Face;
			public double Scale;
			public double? X, Y, Dx, Dy;
			/// <summary>Degrees clockwise, from the rotate lists.</summary>
			public double Rotate;
			/// <summary>From baseline-shift, dominant-baseline and alignment-baseline, in user units down.</summary>
			public double BaselineShift;
			public double Left, Baseline, Advance;
			/// <summary>The glyph's own advance with kerning: <see cref="Advance"/> without letter- and word-spacing.</summary>
			public double Width;
			/// <summary>The path the character lies along, from its textPath; null for none.</summary>
			public SvgTextPath Path;
			/// <summary>In a textPath that draws nothing: counted in the position lists, then dropped.</summary>
			public bool Skip;
			/// <summary>On a path but off either end of it.</summary>
			public bool Hidden;
			/// <summary>From the glyph's user-unit frame (origin on its baseline at its pen position, y down) to user space.</summary>
			public Affine Placement = Affine.NewIdentity();
		}

		/// <summary>What <see cref="Collect"/> reads the same for every element of one &lt;text&gt;.</summary>
		/// <param name="Content">Each element's runs, whitespace handled, and child elements that lay out (<see cref="SvgTextContent"/>).</param>
		private sealed record Source(double ViewportDiagonal, Func<string, bool, TypeFace> FontResolver, SvgFontSet Fonts, Func<SvgElement, double, SvgTextPath> ResolvePath, Dictionary<SvgElement, List<object>> Content);
	}
}
