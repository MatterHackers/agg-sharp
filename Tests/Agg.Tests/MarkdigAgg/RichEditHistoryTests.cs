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

using System;
using System.Linq;
using System.Threading.Tasks;
using Markdig.Agg.Editing;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	public class RichEditHistoryTests
	{
		private const string Original = "# Title\n\nFirst *para*  \nwith a hard break.\n\n- one\n- two\n";

		/// <summary>
		/// Drives the real edit operations the way the editor widget will: Record before, Committed after.
		/// </summary>
		private class Editor
		{
			public RichDocument Document;
			public RichSelection Selection;
			public readonly RichEditHistory History;

			public Editor(string markdown, DocPosition caret, int maxSteps = RichEditHistory.DefaultMaxSteps)
			{
				Document = RichMarkdownParser.Parse(markdown);
				Selection = RichSelection.At(caret);
				History = new RichEditHistory(maxSteps);
			}

			// One keystroke per character, as typing arrives.
			public void Type(string text)
			{
				foreach (char c in text)
				{
					Edit(RichEditKind.Typing, c.ToString(), () => Selection.IsEmpty
						? RichEditOperations.InsertText(Document, Selection.Caret, c.ToString())
						: RichEditOperations.InsertText(Document, RichEditOperations.DeleteSelection(Document, Selection).Caret, c.ToString()));
				}
			}

			// A paste arrives as one multi-character insert.
			public void Paste(string text) => Edit(RichEditKind.Typing, text, () => RichEditOperations.InsertText(Document, Selection.Caret, text));

			// detectChange reports a no-op (nothing written differently) to the history, as the widget will.
			public void Backspace(bool detectChange = false) => Edit(RichEditKind.Backspace, null, () => Selection.IsEmpty
				? RichEditOperations.Backspace(Document, Selection.Caret)
				: RichEditOperations.DeleteSelection(Document, Selection), detectChange);

			public void Delete(bool detectChange = false) => Edit(RichEditKind.Delete, null, () => RichEditOperations.Delete(Document, Selection.Caret), detectChange);

			// The widget's space key: the space is typing, then the shortcut (if it applies) is its own step.
			public void TypeSpaceWithShortcut()
			{
				Type(" ");
				var caret = Selection.Caret;
				History.Record(Document, Selection, RichEditKind.Other);
				bool applied = RichBlockOperations.TryApplyLineStartShortcut(Document, caret, out var newCaret);
				Selection = RichSelection.At(applied ? newCaret : caret);
				History.Committed(Selection, applied);
			}

			public void Enter() => Edit(RichEditKind.Other, null, () => RichEditOperations.SplitBlock(Document, Selection.Caret));

			public void SetHeading(int level) => Edit(RichEditKind.Other, null, () =>
			{
				RichBlockOperations.SetBlockKind(Document, Selection, level);
				return Selection;
			});

			public void MoveCaret(DocPosition caret)
			{
				Selection = RichSelection.At(caret);
				History.BreakCoalescing();
			}

			public bool Undo() => Adopt(History.Undo(Document, Selection));

			public bool Redo() => Adopt(History.Redo(Document, Selection));

			public string Markdown => RichMarkdownWriter.Write(Document);

			private void Edit(RichEditKind kind, string text, Func<RichSelection> op, bool detectChange = false)
			{
				string before = detectChange ? Markdown : null;
				History.Record(Document, Selection, kind, text);
				Selection = op();
				History.Committed(Selection, !detectChange || Markdown != before);
			}

			private bool Adopt((RichDocument Document, RichSelection Selection)? state)
			{
				if (state == null)
				{
					return false;
				}

				(Document, Selection) = state.Value;
				return true;
			}
		}

		private static DocPosition P(int block, int offset) => new DocPosition(block, offset);

		private static string Text(RichBlock block) => string.Concat(block.Inlines.Select(i => i is RichRun run ? run.Text : "@"));

		// Each block's kind, heading level, Dirty flag and text.
		private static string Fingerprint(RichDocument document) => string.Join("|", document.Blocks.Select(b => $"{b.Kind}{b.HeadingLevel}{b.Dirty}:{Text(b)}"));

		// Block 1 is "First para\nwith a hard break." - the caret at its end.
		private static DocPosition EndOfParagraph(RichDocument document) => P(1, document.Blocks[1].TextLength());

		[Test]
		public async Task TypingAWordIsOneStep()
		{
			var editor = new Editor(Original, P(0, 5));
			editor.Type("abc");
			await Assert.That(editor.History.UndoCount).IsEqualTo(1);
			await Assert.That(Text(editor.Document.Blocks[0])).IsEqualTo("Titleabc");

			await Assert.That(editor.Undo()).IsTrue();
			await Assert.That(Text(editor.Document.Blocks[0])).IsEqualTo("Title");
			await Assert.That(editor.History.CanUndo).IsFalse();
		}

		[Test]
		public async Task EachWordIsItsOwnStep()
		{
			var editor = new Editor(Original, P(0, 5));
			editor.Type(" hello world, don't");
			// " ", then "hello " (a word after a space), "world, " and "don't" - the apostrophe stays in its word.
			await Assert.That(editor.History.UndoCount).IsEqualTo(4);

			editor.Undo();
			await Assert.That(Text(editor.Document.Blocks[0])).IsEqualTo("Title hello world, ");
			editor.Undo();
			await Assert.That(Text(editor.Document.Blocks[0])).IsEqualTo("Title hello ");
			editor.Undo();
			await Assert.That(Text(editor.Document.Blocks[0])).IsEqualTo("Title ");
		}

		[Test]
		public async Task TypeThenHeadingUndoesInTwoSteps()
		{
			var editor = new Editor(Original, EndOfParagraph(RichMarkdownParser.Parse(Original)));
			editor.Type("xyz");
			editor.SetHeading(2);
			await Assert.That(editor.Document.Blocks[1].Kind).IsEqualTo(RichBlockKind.Heading);
			await Assert.That(editor.History.UndoCount).IsEqualTo(2);

			editor.Undo();
			await Assert.That(editor.Document.Blocks[1].Kind).IsEqualTo(RichBlockKind.Paragraph);
			await Assert.That(editor.Markdown).IsEqualTo("# Title\n\nFirst *para*  \nwith a hard break.xyz\n\n- one\n- two\n");

			editor.Undo();
			await Assert.That(editor.Markdown).IsEqualTo(Original);
		}

		[Test]
		public async Task UndoRestoresSelectionAndRedoReapplies()
		{
			var editor = new Editor(Original, P(0, 2));
			// Select "itl" and type over it: the replacement is one word, so one undo brings "itl" back, with the
			// selection as it was.
			var selection = new RichSelection(P(0, 1), P(0, 4));
			editor.Selection = selection;
			editor.Type("ab");
			await Assert.That(editor.Markdown).IsEqualTo(Original.Replace("Title", "Tabe"));
			await Assert.That(editor.History.UndoCount).IsEqualTo(1);

			editor.Undo();
			await Assert.That(editor.Markdown).IsEqualTo(Original);
			await Assert.That(editor.Selection).IsEqualTo(selection);
			await Assert.That(editor.History.CanRedo).IsTrue();

			await Assert.That(editor.Redo()).IsTrue();
			await Assert.That(editor.Markdown).IsEqualTo(Original.Replace("Title", "Tabe"));
			await Assert.That(editor.Selection).IsEqualTo(RichSelection.At(P(0, 3)));
			await Assert.That(editor.History.CanRedo).IsFalse();

			// A second round trip still restores the pristine snapshot.
			editor.Undo();
			await Assert.That(editor.Markdown).IsEqualTo(Original);
		}

		[Test]
		public async Task NewEditAfterUndoClearsRedo()
		{
			var editor = new Editor(Original, P(0, 5));
			editor.Type("a");
			editor.Undo();
			await Assert.That(editor.History.CanRedo).IsTrue();

			editor.Type("b");
			await Assert.That(editor.History.CanRedo).IsFalse();
			await Assert.That(editor.Redo()).IsFalse();
			await Assert.That(Text(editor.Document.Blocks[0])).IsEqualTo("Titleb");
		}

		[Test]
		public async Task CaretMoveBreaksTyping()
		{
			var editor = new Editor(Original, P(0, 5));
			editor.Type("ab");
			// Moving away and back to the same spot still starts a new step.
			editor.MoveCaret(P(0, 0));
			editor.MoveCaret(P(0, 7));
			editor.Type("cd");
			await Assert.That(editor.History.UndoCount).IsEqualTo(2);

			// Typing somewhere other than where the last keystroke left the caret also breaks.
			editor.Selection = RichSelection.At(P(0, 0));
			editor.Type("e");
			await Assert.That(editor.History.UndoCount).IsEqualTo(3);
		}

		[Test]
		public async Task BackspaceRunsMergeSeparatelyFromTyping()
		{
			var editor = new Editor(Original, P(0, 5));
			editor.Type("abc");
			editor.Backspace();
			editor.Backspace();
			await Assert.That(editor.History.UndoCount).IsEqualTo(2);
			editor.Type("z");
			await Assert.That(editor.History.UndoCount).IsEqualTo(3);

			editor.Undo();
			await Assert.That(Text(editor.Document.Blocks[0])).IsEqualTo("Titlea");
			editor.Undo();
			await Assert.That(Text(editor.Document.Blocks[0])).IsEqualTo("Titleabc");
		}

		[Test]
		public async Task EnterIsItsOwnStepAndBreaksTyping()
		{
			var editor = new Editor(Original, P(0, 5));
			editor.Type("ab");
			editor.Enter();
			editor.Type("cd");
			await Assert.That(editor.History.UndoCount).IsEqualTo(3);
			editor.Undo();
			editor.Undo();
			await Assert.That(editor.Document.Blocks.Count).IsEqualTo(RichMarkdownParser.Parse(Original).Blocks.Count);
		}

		[Test]
		public async Task CapDropsOldestSteps()
		{
			var editor = new Editor(Original, P(0, 5), maxSteps: 3);
			for (int i = 0; i < 5; i++)
			{
				editor.Enter();
			}

			await Assert.That(editor.History.UndoCount).IsEqualTo(3);
			while (editor.Undo())
			{
			}

			// Two Enters are beyond the cap and stay.
			await Assert.That(editor.Document.Blocks.Count).IsEqualTo(RichMarkdownParser.Parse(Original).Blocks.Count + 2);
			await Assert.That(new RichEditHistory().MaxSteps).IsEqualTo(200);
		}

		[Test]
		public async Task UndoToStartWritesOriginalBytes()
		{
			var editor = new Editor(Original, P(2, 0));
			var changes = 0;
			editor.History.Changed += (s, e) => changes++;

			// Touch a list item (Dirty plus list group regeneration), the paragraph and the heading.
			editor.Type("zero ");
			editor.Enter();
			editor.MoveCaret(EndOfParagraph(editor.Document));
			editor.Backspace();
			editor.MoveCaret(P(0, 0));
			editor.SetHeading(0);
			// The writer cannot write an edited list item yet (edited lists come next), so the edited state is
			// compared as a model fingerprint.
			var edited = Fingerprint(editor.Document);
			await Assert.That(editor.Document.Blocks.Count(b => b.Dirty)).IsGreaterThan(1);
			await Assert.That(changes).IsGreaterThan(0);

			while (editor.Undo())
			{
			}

			await Assert.That(editor.Markdown).IsEqualTo(Original);
			await Assert.That(editor.Document.Blocks.All(b => !b.Dirty)).IsTrue();

			// And redo all the way gets back the edited text.
			while (editor.Redo())
			{
			}

			await Assert.That(Fingerprint(editor.Document)).IsEqualTo(edited);
			editor.History.Clear();
			await Assert.That(editor.History.CanUndo || editor.History.CanRedo).IsFalse();
		}

		[Test]
		public async Task PendingStyleChangeBreaksTyping()
		{
			// The widget calls BreakCoalescing when the user picks a pending style mid-word.
			var editor = new Editor(Original, P(0, 5));
			editor.Type("hel");
			editor.History.BreakCoalescing();
			editor.Type("lo");
			await Assert.That(editor.History.UndoCount).IsEqualTo(2);
		}

		[Test]
		public async Task PasteIsItsOwnStep()
		{
			var editor = new Editor(Original, P(0, 5));
			editor.Type("ab");
			editor.Paste("cd");
			editor.Type("e");
			await Assert.That(editor.History.UndoCount).IsEqualTo(3);
			editor.Undo();
			editor.Undo();
			await Assert.That(Text(editor.Document.Blocks[0])).IsEqualTo("Titleab");
		}

		[Test]
		public async Task NoOpEditAddsNoStep()
		{
			var editor = new Editor("Plain text.\n", P(0, 0));
			editor.Backspace(detectChange: true);
			await Assert.That(editor.History.CanUndo).IsFalse();

			// A no-op neither ends a typing run nor clears redo.
			editor.MoveCaret(P(0, 11));
			editor.Type("ab");
			editor.Delete(detectChange: true);
			editor.Type("c");
			await Assert.That(editor.History.UndoCount).IsEqualTo(1);

			editor.Type(" d");
			editor.Undo();
			editor.Delete(detectChange: true);
			await Assert.That(editor.History.CanRedo).IsTrue();
		}

		[Test]
		public async Task UndoFromAChangedHandlerKeepsTheOuterResult()
		{
			var editor = new Editor(Original, P(0, 5));
			editor.Type("a");
			editor.Enter();
			bool reentered = false;
			editor.History.Changed += (s, e) =>
			{
				if (!reentered)
				{
					reentered = true;
					editor.History.Undo(editor.Document, editor.Selection);
				}
			};

			var result = editor.History.Undo(editor.Document, editor.Selection);
			await Assert.That(result).IsNotNull();
			await Assert.That(result.Value.Document).IsNotNull();
		}

		[Test]
		public async Task OneUndoRevertsJustTheLineStartShortcut()
		{
			var editor = new Editor("Plain text.\n", P(0, 0));
			editor.Enter();
			editor.MoveCaret(P(0, 0));
			editor.Type("#");
			editor.TypeSpaceWithShortcut();
			await Assert.That(editor.Document.Blocks[0].Kind).IsEqualTo(RichBlockKind.Heading);

			editor.Undo();
			await Assert.That(editor.Document.Blocks[0].Kind).IsEqualTo(RichBlockKind.Paragraph);
			await Assert.That(Text(editor.Document.Blocks[0])).IsEqualTo("# ");
			await Assert.That(editor.Selection).IsEqualTo(RichSelection.At(P(0, 2)));
		}

		[Test]
		public async Task MaxStepsMustBePositive()
		{
			await Assert.That(() => new RichEditHistory(0)).Throws<ArgumentOutOfRangeException>();
		}
	}
}
