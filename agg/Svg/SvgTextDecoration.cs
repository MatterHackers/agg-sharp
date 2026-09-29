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

using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Svg
{
	/// <summary>text-decoration as usvg 0.45.1 resolves and lays it out.</summary>
	internal static class SvgTextDecoration
	{
		public static readonly string[] Kinds = { "underline", "overline", "line-through" };

		/// <summary>True when <paramref name="element"/>'s own text-decoration names <paramref name="kind"/> (usvg splits on spaces only).</summary>
		public static bool Declares(SvgElement element, string kind) => (element["text-decoration"] ?? "").Split(' ').Contains(kind);

		/// <summary>
		/// The decorations the &lt;text&gt; itself draws with: a kind declared on it or on any ancestor, outside the
		/// text or not, is drawn, in the &lt;text&gt;'s own fill and stroke.
		/// </summary>
		public static Dictionary<string, SvgStyle> ForText(SvgElement text, SvgStyle textStyle)
		{
			var decorations = new Dictionary<string, SvgStyle>();
			foreach (string kind in Kinds)
			{
				for (SvgElement node = text; node != null; node = node.Parent)
				{
					if (Declares(node, kind))
					{
						decorations[kind] = textStyle;
						break;
					}
				}
			}

			return decorations;
		}

		/// <summary>A tspan that declares a kind itself draws it in its own fill and stroke; others keep the enclosing element's.</summary>
		public static Dictionary<string, SvgStyle> ForChild(Dictionary<string, SvgStyle> parent, SvgElement child, SvgStyle childStyle)
		{
			var decorations = new Dictionary<string, SvgStyle>(parent);
			foreach (string kind in Kinds)
			{
				if (Declares(child, kind))
				{
					decorations[kind] = childStyle;
				}
			}

			return decorations;
		}

		/// <summary>
		/// A run's lines as usvg draws a span's: overline and underline before its glyphs, line-through after. Each
		/// line is a rectangle the font's underline thickness tall, broken where a glyph starts a chunk, is moved by
		/// dx or dy, is rotated or lies on a path; each piece is laid in its first glyph's frame, so it turns with it.
		/// </summary>
		public static void Add(IReadOnlyList<SvgText.Character> characters, int start, int end, List<(VertexStorage, SvgStyle)> before, List<(VertexStorage, SvgStyle)> after)
		{
			SvgText.Character first = characters[start];
			if (first.Decorations == null || first.Decorations.Count == 0)
			{
				return;
			}

			var metrics = new SvgFontMetrics(first.Face);
			double thickness = metrics.UnderlineThickness * first.Scale;
			void Line(List<(VertexStorage, SvgStyle)> into, string kind, double aboveBaseline)
			{
				if (!first.Decorations.TryGetValue(kind, out SvgStyle style))
				{
					return;
				}

				var line = new VertexStorage();
				double dy = -aboveBaseline * first.Scale;
				SvgText.Character spanStart = null;
				double width = 0;
				void Flush()
				{
					if (spanStart != null && width > 0)
					{
						var corners = new[] { new Vector2(0, dy - thickness / 2), new Vector2(width, dy - thickness / 2), new Vector2(width, dy + thickness / 2), new Vector2(0, dy + thickness / 2) };
						for (int k = 0; k < 4; k++)
						{
							Vector2 corner = spanStart.Placement.Transform(corners[k]);
							if (k == 0)
							{
								line.MoveTo(corner.X, corner.Y);
							}
							else
							{
								line.LineTo(corner.X, corner.Y);
							}
						}

						line.ClosePolygon();
					}

					spanStart = null;
				}

				for (int i = start; i < end; i++)
				{
					SvgText.Character c = characters[i];
					if (c.Hidden)
					{
						Flush();
						continue;
					}

					if (spanStart != null && (c.BreaksDecoration || c.Path != null))
					{
						Flush();
					}

					if (spanStart == null)
					{
						spanStart = c;
						width = c.Advance;
					}
					else
					{
						width += c.Advance;
					}
				}

				Flush();
				into.Add((line, style));
			}

			Line(before, "overline", first.Face.Ascent);
			Line(before, "underline", metrics.UnderlinePosition);
			Line(after, "line-through", metrics.LineThroughPosition);
		}
	}
}
