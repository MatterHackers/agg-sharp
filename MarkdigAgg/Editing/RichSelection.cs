/*
Copyright(c) 2026, Lars Brubaker
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
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
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

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// A selection in a <see cref="RichDocument"/>: where it started (Anchor) and where the caret is. An empty
	/// selection (Anchor == Caret) is a plain caret. Edit ops return one so an op can either place a caret or,
	/// like Backspace onto a Raw block, select something whole for the next keystroke to act on.
	/// <para>
	/// <see cref="WholeBlock"/> marks a selection of one block as an object (set by
	/// <see cref="RichEditOperations.WholeBlock"/>): deleting it removes the block. Without it, a range is text,
	/// even one that happens to span all of a code block's or table cell's text, so selecting that text and
	/// typing replaces the text and keeps the block.
	/// </para>
	/// </summary>
	public readonly record struct RichSelection(DocPosition Anchor, DocPosition Caret, bool WholeBlock = false)
	{
		public static RichSelection At(DocPosition caret) => new RichSelection(caret, caret);

		public bool IsEmpty => Anchor == Caret;

		public DocPosition Start => DocPosition.Min(Anchor, Caret);

		public DocPosition End => DocPosition.Max(Anchor, Caret);
	}
}
