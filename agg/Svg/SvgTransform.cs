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
using MatterHackers.Agg.Transform;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// SVG transform lists: matrix, translate, scale, rotate (with an optional centre), skewX and skewY.
	/// </summary>
	public static class SvgTransform
	{
		/// <summary>
		/// The transform <paramref name="text"/> describes, as an agg <see cref="Affine"/>. SVG applies a list's
		/// rightmost transform first; agg's <c>a * b</c> applies <c>a</c> first, so the list is folded from the right.
		/// A malformed list is the identity, as the SVG spec says an invalid transform attribute is ignored.
		/// </summary>
		public static Affine Parse(string text)
		{
			Affine result = Affine.NewIdentity();
			if (string.IsNullOrWhiteSpace(text))
			{
				return result;
			}

			var transforms = new List<Affine>();
			int index = 0;
			while (index < text.Length)
			{
				while (index < text.Length && (char.IsWhiteSpace(text[index]) || text[index] == ','))
				{
					index++;
				}

				if (index >= text.Length)
				{
					break;
				}

				int open = text.IndexOf('(', index);
				int close = open < 0 ? -1 : text.IndexOf(')', open);
				if (open < 0 || close < 0)
				{
					return Affine.NewIdentity();
				}

				string name = text.Substring(index, open - index).Trim();
				List<double> a = SvgLength.ParseList(text.Substring(open + 1, close - open - 1));
				Affine? transform = Create(name, a);
				if (transform == null)
				{
					return Affine.NewIdentity();
				}

				transforms.Add(transform.Value);
				index = close + 1;
			}

			for (int i = transforms.Count - 1; i >= 0; i--)
			{
				result *= transforms[i];
			}

			return result;
		}

		/// <summary>
		/// <paramref name="element"/>'s <paramref name="attribute"/> (transform, gradientTransform or
		/// patternTransform) about its transform-origin, as usvg resolves it: the origin's percentages and keywords
		/// are of the viewport, whatever the element's units, so a bounding-box gradient's "0.5 0.5" is its centre.
		/// </summary>
		public static Affine Resolve(SvgElement element, string attribute, double viewportWidth, double viewportHeight)
			=> Resolve(element[attribute], element["transform-origin"], viewportWidth, viewportHeight);

		/// <summary>The transform list <paramref name="text"/> about the transform-origin <paramref name="origin"/>.</summary>
		public static Affine Resolve(string text, string origin, double viewportWidth, double viewportHeight)
		{
			Affine transform = Parse(text);
			if (!(Origin(origin, viewportWidth, viewportHeight) is (double x, double y)))
			{
				return transform;
			}

			return Affine.NewTranslation(-x, -y) * transform * Affine.NewTranslation(x, y);
		}

		/// <summary>
		/// A CSS transform-origin (svgtypes' rules): one or two keywords or lengths, then an optional z length. One
		/// value names a side or a length and centres the other axis; two with left/right/top/bottom may come in
		/// either order. Null when it does not parse.
		/// </summary>
		private static (double X, double Y)? Origin(string text, double viewportWidth, double viewportHeight)
		{
			string[] parts = (text ?? "").Split(new[] { ' ', '\t', '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 0 || parts.Length > 3)
			{
				return null;
			}

			// Each part: its percentage when a keyword, and whether it can sit on the x or y axis.
			(string Length, bool Horizontal, bool Vertical, bool Keyword)? Part(string part)
			{
				switch (part.ToLowerInvariant())
				{
					case "left": return ("0%", true, false, true);
					case "right": return ("100%", true, false, true);
					case "top": return ("0%", false, true, true);
					case "bottom": return ("100%", false, true, true);
					case "center": return ("50%", true, true, false);
					default: return double.IsNaN(SvgLength.Parse(part, double.NaN, 1)) ? null : (part, true, true, false);
				}
			}

			var first = Part(parts[0]);
			var second = parts.Length > 1 ? Part(parts[1]) : ("50%", true, true, false);
			if (first == null || second == null || (parts.Length == 3 && (parts[2].EndsWith("%") || double.IsNaN(SvgLength.Parse(parts[2], double.NaN)))))
			{
				return null;
			}

			var (a, b) = (first.Value, second.Value);
			if (parts.Length == 1 && !a.Horizontal)
			{
				(a, b) = (b, a);
			}
			else if (parts.Length > 1 && (a.Keyword || b.Keyword))
			{
				if (a.Vertical && !a.Horizontal || b.Horizontal && !b.Vertical)
				{
					(a, b) = (b, a);
				}

				if (!a.Horizontal || !b.Vertical)
				{
					return null;
				}
			}

			return (SvgLength.Parse(a.Length, 0, viewportWidth), SvgLength.Parse(b.Length, 0, viewportHeight));
		}

		private static Affine? Create(string name, List<double> a)
		{
			switch (name)
			{
				case "matrix" when a.Count == 6:
					return new Affine(a[0], a[1], a[2], a[3], a[4], a[5]);
				case "translate" when a.Count == 1 || a.Count == 2:
					return Affine.NewTranslation(a[0], a.Count == 2 ? a[1] : 0);
				case "scale" when a.Count == 1 || a.Count == 2:
					return Affine.NewScaling(a[0], a.Count == 2 ? a[1] : a[0]);
				case "rotate" when a.Count == 1 || a.Count == 3:
					Affine rotation = Affine.NewRotation(a[0] * Math.PI / 180);
					return a.Count == 1
						? rotation
						: Affine.NewTranslation(-a[1], -a[2]) * rotation * Affine.NewTranslation(a[1], a[2]);
				case "skewX" when a.Count == 1:
					return new Affine(1, 0, Math.Tan(a[0] * Math.PI / 180), 1, 0, 0);
				case "skewY" when a.Count == 1:
					return new Affine(1, Math.Tan(a[0] * Math.PI / 180), 0, 1, 0, 0);
				default:
					return null;
			}
		}
	}
}
