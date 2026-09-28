/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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

using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>CodeLayout's wrapped rows, measured one unit per character so a width is a column count.</summary>
	public class CodeLayoutTests
	{
		private static (CodeDocument document, CodeLayout layout) Wrapped(string text, double width)
		{
			var document = new CodeDocument(text);
			var layout = new CodeLayout(document, (s, start, end) => end - start);
			layout.Configure(true, width);
			document.TextChanged += (s, e) => layout.Invalidate();
			return (document, layout);
		}

		private static string RowTexts(CodeDocument document, CodeLayout layout)
		{
			return Enumerable.Range(0, layout.RowCount)
				.Select(r => layout.Row(r))
				.Select(row => document.Text.Substring(row.Start, row.End - row.Start))
				.Aggregate((a, b) => a + "|" + b);
		}

		[Test]
		public async Task LinesBreakAfterTheLastWordThatFitsAndOverlongWordsBreakWhereTheyOverflow()
		{
			(CodeDocument document, CodeLayout layout) = Wrapped("let a = b;\n\nabcdefghij xy\n    indented words", 6);
			await Assert.That(RowTexts(document, layout)).IsEqualTo("let a|= b;||abcdef|ghij|xy|    in|dented|words");

			// The whitespace a soft wrap broke at stays on its row; the next row starts after it.
			CodeRow first = layout.Row(0);
			await Assert.That((first.End, first.NextStart, first.CaretEnd)).IsEqualTo((5, 6, 5));

			// A hard-broken word: the caret stops a character short of the next row's start.
			CodeRow broken = layout.Row(3);
			await Assert.That((broken.End, broken.NextStart, broken.CaretEnd)).IsEqualTo((18, 18, 17));

			// Only a line's first row is numbered.
			await Assert.That(Enumerable.Range(0, layout.RowCount).Count(r => layout.Row(r).FirstInLine)).IsEqualTo(document.LineCount);
			await Assert.That(layout.FirstRowOfLine(2)).IsEqualTo(3);

			// Unwrapped, every line is one row.
			layout.Configure(false, 6);
			await Assert.That(layout.RowCount).IsEqualTo(4);
			await Assert.That(layout.Row(2).End).IsEqualTo(document.LineEnd(2));
		}

		[Test]
		public async Task CaretsMapToRowsAndBack()
		{
			(CodeDocument document, CodeLayout layout) = Wrapped("let a = b;\nxy", 6);

			// "let a" | " " | "= b;": the space after "a" (index 5) and the caret before it are on row 0.
			await Assert.That(layout.RowOfIndex(5)).IsEqualTo(0);
			await Assert.That(layout.RowOfIndex(6)).IsEqualTo(1);
			await Assert.That(layout.RowOfIndex(10)).IsEqualTo(1);
			await Assert.That(layout.RowOfIndex(11)).IsEqualTo(2);
			await Assert.That(layout.XOfIndex(8)).IsEqualTo(2);

			// A click past a soft row's end stops before the whitespace; on the line's last row, at the line's end.
			await Assert.That(layout.IndexAtX(0, 50)).IsEqualTo(5);
			await Assert.That(layout.IndexAtX(1, 50)).IsEqualTo(10);
			await Assert.That(layout.IndexAtX(1, 1.4)).IsEqualTo(7);

			// An edit lays the rows out again.
			document.SetCaret(0);
			document.Insert("xxxxxxxxxxxxx ");
			await Assert.That(RowTexts(document, layout)).StartsWith("xxxxxx|");
		}

		[Test]
		public async Task UpAndDownMoveByRowKeepingTheX()
		{
			(_, CodeLayout layout) = Wrapped("abcde fghij\nk\nlmnop", 6);

			// Rows: "abcde" | "fghij" | "k" | "lmnop". From x 4 on row 0 down through "k" and on keeps x 4.
			int caret = layout.MoveVertically(4, 1);
			await Assert.That(caret).IsEqualTo(10);
			caret = layout.MoveVertically(caret, 1);
			await Assert.That(caret).IsEqualTo(13);
			caret = layout.MoveVertically(caret, 1);
			await Assert.That(caret).IsEqualTo(18);

			// The last row stays put; a page up clamps to the first row.
			await Assert.That(layout.MoveVertically(caret, 1)).IsEqualTo(18);
			await Assert.That(layout.MoveVertically(caret, -10)).IsEqualTo(4);
		}
	}
}
