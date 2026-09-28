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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using MatterHackers.Agg.Font;

namespace MatterHackers.Agg.UI.RichText
{
	/// <summary>
	/// Per-paragraph layouts kept between <see cref="RichTextLayout.Layout"/> calls, so an editor re-measures only the
	/// paragraphs an edit changed. Paragraphs are matched by value (content, attributes and list number), not by
	/// index, so inserting or deleting a paragraph still reuses every other one. A change of width, font size or
	/// resolver starts over.
	/// </summary>
	public sealed class RichTextLayoutCache
	{
		private Dictionary<(Block, int), BlockLayout> previous = new Dictionary<(Block, int), BlockLayout>(KeyComparer.Instance);
		private Dictionary<(Block, int), BlockLayout> next = new Dictionary<(Block, int), BlockLayout>(KeyComparer.Instance);
		private double width = double.NaN;
		private double fontSize = double.NaN;
		private Func<InlineStyle, double, StyledTypeFace> resolver;

		/// <summary>Gets how many paragraphs the last layout reused rather than measured.</summary>
		public int LastReuseCount { get; private set; }

		internal void Begin(double width, double fontSize, Func<InlineStyle, double, StyledTypeFace> resolver)
		{
			if (width != this.width || fontSize != this.fontSize || resolver != this.resolver)
			{
				this.previous.Clear();
				(this.width, this.fontSize, this.resolver) = (width, fontSize, resolver);
			}

			this.next.Clear();
			this.LastReuseCount = 0;
		}

		internal BlockLayout Take(Block block, int ordinal)
		{
			if (this.previous.TryGetValue((block, ordinal), out var layout))
			{
				this.LastReuseCount++;
				return layout;
			}

			return null;
		}

		// Layouts are never mutated after they are built, so identical paragraphs may share one.
		// The key is a snapshot: the document's blocks are edited in place, which would change a live key's value.
		internal void Keep(Block block, int ordinal, BlockLayout layout) => this.next.TryAdd((block.Clone(), ordinal), layout);

		internal void End() => (this.previous, this.next) = (this.next, this.previous);

		// Block's own hash only covers attributes and run count; adding the text keeps same-shaped plain paragraphs
		// from all landing in one bucket.
		private sealed class KeyComparer : IEqualityComparer<(Block Block, int Ordinal)>
		{
			public static readonly KeyComparer Instance = new KeyComparer();

			public bool Equals((Block Block, int Ordinal) x, (Block Block, int Ordinal) y) => x.Ordinal == y.Ordinal && x.Block.Equals(y.Block);

			public int GetHashCode((Block Block, int Ordinal) key) => HashCode.Combine(key.Block.GetHashCode(), key.Block.Text, key.Ordinal);
		}
	}
}
