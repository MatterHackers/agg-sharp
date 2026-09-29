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
using System.IO;
using System.Linq;
using MatterHackers.Agg.Font;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// A set of TrueType faces that text picks from by CSS font matching (CSS Fonts 3 §5.2, as fontdb does it for
	/// resvg): the first family in font-family that the set has, then the closest stretch, style and weight within
	/// it. Generic families (serif, sans-serif, ...) name one of the set's families; a family list that matches
	/// nothing falls back to <see cref="Serif"/>, as usvg's does. Set it on <see cref="SvgDocument.Fonts"/>.
	/// </summary>
	public class SvgFontSet
	{
		private readonly List<Face> faces = new List<Face>();

		public string Serif { get; set; } = "Times New Roman";

		public string SansSerif { get; set; } = "Arial";

		public string Cursive { get; set; } = "Comic Sans MS";

		public string Fantasy { get; set; } = "Impact";

		public string Monospace { get; set; } = "Courier New";

		/// <summary>Adds each .ttf and .otf file in <paramref name="folder"/> (not its subfolders) that loads.</summary>
		public void AddFolder(string folder)
		{
			foreach (string path in Directory.GetFiles(folder).Where(p => p.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)).OrderBy(p => p, StringComparer.Ordinal))
			{
				Add(File.ReadAllBytes(path));
			}
		}

		/// <summary>Adds the TrueType/OpenType font in <paramref name="font"/>; false if it does not load.</summary>
		public bool Add(byte[] font)
		{
			var typeFace = new TypeFace();
			Typography.OpenFont.PreviewFontInfo preview;
			try
			{
				preview = new Typography.OpenFont.OpenFontReader().ReadPreview(new MemoryStream(font));
				if (preview == null || !typeFace.LoadTTF(new MemoryStream(font)))
				{
					return false;
				}
			}
			catch (Exception e) when (e is OverflowException or EndOfStreamException or IndexOutOfRangeException or ArgumentException or NotSupportedException)
			{
				// Typography cannot read every font (Amiri's GPOS type-8 lookups overflow its reader); a folder of
				// fonts still loads the rest.
				return false;
			}

			(int weight, int stretch, bool italic, bool oblique) = ReadOs2(font);
			faces.Add(new Face
			{
				TypeFace = typeFace,
				Family = preview.TypographicFamilyName ?? preview.Name,
				Weight = weight,
				Stretch = stretch,
				Style = italic ? "italic" : oblique ? "oblique" : "normal",
			});
			return true;
		}

		/// <summary>
		/// The face for a font-family list (names unquoted, generic names as written) in the given weight (100-900),
		/// style ("normal", "italic", "oblique") and stretch (1-9), or null when the set has none of the families
		/// and no <see cref="Serif"/> family either.
		/// </summary>
		public TypeFace Match(IEnumerable<string> families, int weight, string style, int stretch)
		{
			foreach (string family in families.Append("serif"))
			{
				string name = family.ToLowerInvariant() switch
				{
					"serif" => Serif,
					"sans-serif" => SansSerif,
					"cursive" => Cursive,
					"fantasy" => Fantasy,
					"monospace" => Monospace,
					_ => family,
				};
				var candidates = faces.Where(f => string.Equals(f.Family, name, StringComparison.OrdinalIgnoreCase)).ToList();
				if (candidates.Count > 0)
				{
					return BestMatch(candidates, weight, style, stretch).TypeFace;
				}
			}

			return null;
		}

		/// <summary>
		/// A face in the set, other than <paramref name="primary"/>, in the same weight, style and stretch that has a
		/// glyph for <paramref name="codePoint"/> - usvg's fallback for characters the chosen face lacks - or null.
		/// </summary>
		public TypeFace FallbackFor(TypeFace primary, int codePoint)
		{
			Face base_ = faces.FirstOrDefault(f => f.TypeFace == primary);
			if (base_ == null)
			{
				return null;
			}

			return faces.FirstOrDefault(f => f != base_ && f.Style == base_.Style && f.Weight == base_.Weight && f.Stretch == base_.Stretch && f.TypeFace.HasGlyph(codePoint))?.TypeFace;
		}

		private static Face BestMatch(List<Face> candidates, int weight, string style, int stretch)
		{
			// Stretch: the exact width, else narrower ones first (nearest first) for normal and narrower requests,
			// wider ones first for wider requests.
			int bestStretch = candidates.Any(f => f.Stretch == stretch)
				? stretch
				: stretch <= 5
					? candidates.Where(f => f.Stretch < stretch).Select(f => f.Stretch).DefaultIfEmpty(0).Max() is int narrower && narrower > 0 ? narrower : candidates.Min(f => f.Stretch)
					: candidates.Where(f => f.Stretch > stretch).Select(f => f.Stretch).DefaultIfEmpty(10).Min() is int wider && wider < 10 ? wider : candidates.Max(f => f.Stretch);
			candidates = candidates.Where(f => f.Stretch == bestStretch).ToList();

			string[] styleOrder = style switch
			{
				"italic" => new[] { "italic", "oblique", "normal" },
				"oblique" => new[] { "oblique", "italic", "normal" },
				_ => new[] { "normal", "oblique", "italic" },
			};
			string bestStyle = styleOrder.First(s => candidates.Any(f => f.Style == s));
			candidates = candidates.Where(f => f.Style == bestStyle).ToList();

			// Weight: exact; 400 tries 500 first and 500 tries 400 first; then lighter weights (nearest first) for
			// requests up to 500, heavier ones for heavier requests; then the other direction.
			Face Nearest(IEnumerable<Face> set, bool descending) => (descending ? set.OrderByDescending(f => f.Weight) : set.OrderBy(f => f.Weight)).FirstOrDefault();
			Face exact = candidates.FirstOrDefault(f => f.Weight == weight);
			if (exact != null)
			{
				return exact;
			}

			if (weight == 400 && candidates.FirstOrDefault(f => f.Weight == 500) is Face medium)
			{
				return medium;
			}

			if (weight == 500 && candidates.FirstOrDefault(f => f.Weight == 400) is Face regular)
			{
				return regular;
			}

			return weight <= 500
				? Nearest(candidates.Where(f => f.Weight < weight), true) ?? Nearest(candidates.Where(f => f.Weight > weight), false)
				: Nearest(candidates.Where(f => f.Weight > weight), false) ?? Nearest(candidates.Where(f => f.Weight < weight), true);
		}

		/// <summary>usWeightClass, usWidthClass and fsSelection's italic and oblique bits from the font's OS/2 table.</summary>
		private static (int Weight, int Stretch, bool Italic, bool Oblique) ReadOs2(byte[] font)
		{
			int U16(int at) => (font[at] << 8) | font[at + 1];
			int tables = U16(4);
			for (int i = 0; i < tables; i++)
			{
				int record = 12 + i * 16;
				if (font[record] == 'O' && font[record + 1] == 'S' && font[record + 2] == '/' && font[record + 3] == '2')
				{
					int os2 = (font[record + 8] << 24) | (font[record + 9] << 16) | (font[record + 10] << 8) | font[record + 11];
					int selection = U16(os2 + 62);
					return (U16(os2 + 4), Math.Clamp(U16(os2 + 6), 1, 9), (selection & 1) != 0, (selection & 0x200) != 0);
				}
			}

			return (400, 5, false, false);
		}

		private sealed class Face
		{
			public TypeFace TypeFace;
			public string Family;
			public int Weight;
			public int Stretch;
			public string Style;
		}
	}
}
