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
