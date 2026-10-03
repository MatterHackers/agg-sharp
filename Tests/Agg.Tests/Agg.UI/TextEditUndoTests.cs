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

using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The undo history of a plain text field, driven through its public edit and undo calls.
	/// </summary>
	public class TextEditUndoTests
	{
		[Test]
		public async Task TypingUndoesOneCharacterAtATime()
		{
			var field = new InternalTextEditWidget("", 12, false, 0);
			field.OnKeyPress(new KeyPressEventArgs('a'));
			field.OnKeyPress(new KeyPressEventArgs('b'));

			field.Undo();
			await Assert.That(field.Text).IsEqualTo("a");
			await Assert.That(field.CharIndexToInsertBefore).IsEqualTo(1);

			field.Redo();
			await Assert.That(field.Text).IsEqualTo("ab");
			await Assert.That(field.CharIndexToInsertBefore).IsEqualTo(2);
		}

		[Test]
		public async Task ReplaceRangeIsOneUndoStep()
		{
			var field = new InternalTextEditWidget("", 12, false, 0);
			field.SetTextAsUndoBaseline("=sel + 1", 4);
			int textChanged = 0;
			field.TextChanged += (s, e) => textChanged++;

			field.ReplaceRange(1, 3, "self.");

			await Assert.That(field.Text).IsEqualTo("=self. + 1");
			await Assert.That(field.CharIndexToInsertBefore).IsEqualTo(6);
			await Assert.That(textChanged).IsGreaterThan(0);

			field.Undo();
			await Assert.That(field.Text).IsEqualTo("=sel + 1");
			await Assert.That(field.CharIndexToInsertBefore).IsEqualTo(4);

			field.Undo();
			await Assert.That(field.Text).IsEqualTo("=sel + 1").Because("the baseline is where undo stops");

			field.Redo();
			await Assert.That(field.Text).IsEqualTo("=self. + 1");
			await Assert.That(field.CharIndexToInsertBefore).IsEqualTo(6);
		}

		[Test]
		public async Task UndoAfterAProgramSetsTheTextStopsAtThatText()
		{
			var field = new InternalTextEditWidget("", 12, false, 0);
			field.Text = "abc";
			field.SetCursorPosition(3);
			field.OnKeyPress(new KeyPressEventArgs('d'));

			field.Undo();
			await Assert.That(field.Text).IsEqualTo("abc");
		}
	}
}
