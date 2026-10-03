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
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.Font
{
	/// <summary>
	/// The scaled (and, usually, flattened) vertices of unstyled glyphs, kept so a run of text does not rebuild
	/// every outline's transform and curve-flattening chain - and its iterators - each time it is drawn.
	/// </summary>
	/// <remarks>
	/// Only the plain path of <see cref="StyledTypeFace.GetGlyphForCodePoint"/> is cached: no underline, no
	/// faux italic and no <see cref="GlyphStyle"/>. Its vertices depend on nothing but the face that draws the
	/// code point, the em scaling, the resolution scale and whether curves are flattened, which is the key, so
	/// a hit replays exactly the vertices the chain would have produced. The cache is process-wide because
	/// <see cref="Graphics2D.DrawString(string, double, double, double, Justification, Baseline, System.Nullable{Color}, bool, Color, bool)"/>
	/// builds a new <see cref="StyledTypeFace"/> every call. It is emptied when it reaches
	/// <see cref="MaxEntries"/>: a zooming view walks through em sizes, and a full clear is the simplest bound
	/// that a steady frame (a few hundred glyphs) never reaches.
	/// </remarks>
	internal static class PlainGlyphCache
	{
		private const int MaxEntries = 8192;

		private static readonly Dictionary<Key, VertexData[]> Entries = new Dictionary<Key, VertexData[]>();

		private readonly record struct Key(TypeFace Face, double EmScaling, double ResolutionScale, bool FlattenCurves, int CodePoint);

		/// <summary>
		/// The glyph's vertices without the terminating Stop, empty for a code point no face draws. Built from
		/// <paramref name="typeFaceStyle"/>'s own glyph on a miss; <paramref name="face"/> and
		/// <paramref name="emScaling"/> are the drawing face and its scaling that the caller already resolved.
		/// </summary>
		public static VertexData[] Get(StyledTypeFace typeFaceStyle, TypeFace face, double emScaling, double resolutionScale, int codePoint)
		{
			var key = new Key(face, emScaling, resolutionScale, typeFaceStyle.FlattenCurves, codePoint);
			lock (Entries)
			{
				if (Entries.TryGetValue(key, out VertexData[] cached))
				{
					return cached;
				}
			}

			VertexData[] vertices = Flatten(typeFaceStyle.GetGlyphForCodePoint(codePoint, resolutionScale));
			lock (Entries)
			{
				if (Entries.Count >= MaxEntries)
				{
					Entries.Clear();
				}

				Entries[key] = vertices;
			}

			return vertices;
		}

		private static VertexData[] Flatten(IVertexSource glyph)
		{
			if (glyph == null)
			{
				return Array.Empty<VertexData>();
			}

			var vertices = new List<VertexData>();
			foreach (VertexData vertexData in glyph.Vertices())
			{
				if (vertexData.Command != FlagsAndCommand.Stop)
				{
					vertices.Add(vertexData);
				}
			}

			return vertices.ToArray();
		}
	}
}
