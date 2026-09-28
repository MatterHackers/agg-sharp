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

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
#pragma warning disable CS0618 // gsv_text is obsolete for UI text but is the C++ ctrls' font.
	public class GsvTextTests
	{
		/// <summary>Like C++, a gsv_text never given text holds the empty string and yields only the stop.</summary>
		[Test]
		public async Task WithoutTextItDrawsNothing()
		{
			var text = new gsv_text();
			text.size(10, 0);

			await Assert.That(text.Vertices().Select(v => v.Command).ToArray()).IsEquivalentTo(new[] { FlagsAndCommand.Stop }, CollectionOrdering.Matching);

			text.text(null);
			await Assert.That(text.Vertices().Count()).IsEqualTo(1);
		}

		/// <summary>C++ flip(true) negates each glyph's y steps and moves a new line down the y axis instead of up,
		/// so from y = 0 the flipped outline is the plain one mirrored in y exactly, glyph and line break alike.</summary>
		[Test]
		public async Task FlipMirrorsGlyphsAndLinesInY()
		{
			var plain = new gsv_text();
			plain.size(8, 0);
			plain.start_point(10, 0);
			plain.text("Ag\nb");

			var flipped = new gsv_text();
			flipped.size(8, 0);
			flipped.flip(true);
			flipped.start_point(10, 0);
			flipped.text("Ag\nb");

			var mirrored = plain.Vertices().Select(v => (v.Command, v.Position.X, -v.Position.Y)).ToArray();
			var actual = flipped.Vertices().Select(v => (v.Command, v.Position.X, v.Position.Y)).ToArray();

			await Assert.That(actual.Length).IsGreaterThan(10);
			await Assert.That(actual.SequenceEqual(mirrored)).IsTrue();
		}

		/// <summary>A rewind starts the outline again from the start point. C++ gsv_text::rewind leaves the pen where
		/// the last pass ended, so a second pass (the GPU path reads a stroke's source more than once) drew the text
		/// shifted by its own width, and a line lower for each line break.</summary>
		[Test]
		public async Task EveryPassStartsAtTheStartPoint()
		{
			var text = new gsv_text();
			text.size(8, 0);
			text.start_point(10, 20);
			text.text("Ag\nb");

			var first = text.Vertices().Select(v => (v.Command, v.Position.X, v.Position.Y)).ToArray();
			var second = text.Vertices().Select(v => (v.Command, v.Position.X, v.Position.Y)).ToArray();

			await Assert.That(first[0]).IsEqualTo((FlagsAndCommand.MoveTo, 10.0, 20.0));
			await Assert.That(second.SequenceEqual(first)).IsTrue();
		}
	}
#pragma warning restore CS0618
}
