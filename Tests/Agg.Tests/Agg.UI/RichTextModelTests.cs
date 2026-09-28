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

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI.RichText;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// agg-gui's rich_text model_tests / commands_tests / editor core tests, ported to the C# model.
	public class RichTextModelTests
	{
		private static RichDoc Doc(params string[] paragraphs) => new RichDoc(paragraphs.Select(Block.Plain));

		private static DocRange Range(int block0, int offset0, int block1, int offset1) => new DocRange(new DocPos(block0, offset0), new DocPos(block1, offset1));

		[Test]
		public async Task ToggleBoldSetsThenClears()
		{
			var doc = Doc("hello");
			RichTextCommands.Apply(doc, Range(0, 0, 0, 5), RichCommand.ToggleBold);
			await Assert.That(doc.Blocks[0].Runs.All(r => r.Style.Bold)).IsTrue();
			RichTextCommands.Apply(doc, Range(0, 0, 0, 5), RichCommand.ToggleBold);
			await Assert.That(doc.Blocks[0].Runs.All(r => !r.Style.Bold)).IsTrue();
		}

		[Test]
		public async Task ToggleOverMixedRangeSetsAllAndNormalizes()
		{
			var doc = Doc("abcde");
			RichTextCommands.Apply(doc, Range(0, 2, 0, 4), RichCommand.ToggleBold);
			RichTextCommands.Apply(doc, Range(0, 0, 0, 5), RichCommand.ToggleBold);
			await Assert.That(doc.Blocks[0].Runs.Count).IsEqualTo(1);
			await Assert.That(doc.Blocks[0].Runs[0].Style.Bold).IsTrue();
		}

		[Test]
		public async Task SubrangeSplitsAtEdges()
		{
			var doc = Doc("abcdef");
			RichTextCommands.Apply(doc, Range(0, 1, 0, 4), RichCommand.ToggleBold);
			var runs = doc.Blocks[0].Runs;
			await Assert.That(string.Join("|", runs.Select(r => r.Text + (r.Style.Bold ? "*" : string.Empty)))).IsEqualTo("a|bcd*|ef");
		}

		[Test]
		public async Task CrossBlockInlineCommandStylesBothSides()
		{
			var doc = Doc("hello", "world");
			RichTextCommands.Apply(doc, Range(0, 3, 1, 2), RichCommand.ToggleItalic);
			await Assert.That(string.Join("|", doc.Blocks.SelectMany(b => b.Runs).Select(r => r.Text + (r.Style.Italic ? "/" : string.Empty))))
				.IsEqualTo("hel|lo/|wo/|rld");
		}

		[Test]
		public async Task ColourAndHighlightSetAndClear()
		{
			var doc = Doc("abc");
			var red = new Color(255, 0, 0);
			RichTextCommands.Apply(doc, Range(0, 0, 0, 3), RichCommand.SetTextColor(red));
			RichTextCommands.Apply(doc, Range(0, 0, 0, 3), RichCommand.SetHighlight(new Color(1, 2, 3)));
			await Assert.That(doc.Blocks[0].Runs[0].Style.TextColor == red).IsTrue();
			RichTextCommands.Apply(doc, Range(0, 0, 0, 3), RichCommand.SetHighlight(null));
			await Assert.That(doc.Blocks[0].Runs[0].Style.Highlight.HasValue).IsFalse();
			await Assert.That(doc.Blocks[0].Runs.Count).IsEqualTo(1);
		}

		[Test]
		public async Task BlockCommandsAlignListAndIndent()
		{
			var doc = Doc("a", "b");
			var both = Range(0, 0, 1, 1);
			RichTextCommands.Apply(doc, both, RichCommand.SetAlign(Justification.Right));
			await Assert.That(doc.Blocks.All(b => b.Align == Justification.Right)).IsTrue();

			RichTextCommands.Apply(doc, both, RichCommand.SetList(ListKind.Ordered));
			await Assert.That(doc.Blocks.All(b => b.List == ListKind.Ordered)).IsTrue();
			RichTextCommands.Apply(doc, both, RichCommand.SetList(ListKind.Ordered));
			await Assert.That(doc.Blocks.All(b => b.List == ListKind.None)).IsTrue();

			for (int i = 0; i < 20; i++)
			{
				RichTextCommands.Apply(doc, both, RichCommand.Indent);
			}

			await Assert.That(doc.Blocks[0].Indent).IsEqualTo(RichTextCommands.MaxIndent);
			for (int i = 0; i < 20; i++)
			{
				RichTextCommands.Apply(doc, both, RichCommand.Outdent);
			}

			await Assert.That(doc.Blocks[0].Indent).IsEqualTo(0);
		}

		[Test]
		public async Task StyleAtUsesTheCharacterBefore()
		{
			var doc = new RichDoc(new[] { new Block(TextRun.Plain("a"), new TextRun("b", InlineStyle.Default with { Bold = true })) });
			await Assert.That(RichTextCommands.StyleAt(doc, new DocPos(0, 1)).Bold).IsFalse();
			await Assert.That(RichTextCommands.StyleAt(doc, new DocPos(0, 2)).Bold).IsTrue();
		}

		[Test]
		public async Task CommonStyleReportsMixedAndBlockAttributes()
		{
			var doc = Doc("abcd", "ef");
			RichTextCommands.Apply(doc, Range(0, 0, 0, 2), RichCommand.ToggleBold);
			doc.Blocks[1].Align = Justification.Center;
			var common = RichTextCommands.RangeCommonStyle(doc, Range(0, 0, 1, 1));
			await Assert.That(common.Bold).IsNull();
			await Assert.That(common.FontFamilyAgrees).IsTrue();
			await Assert.That(common.Align).IsNull();
			await Assert.That(common.List).IsEqualTo(ListKind.None);
		}

		[Test]
		public async Task RemoveSplitMergeRoundTrip()
		{
			var doc = Doc("hello", "big", "world");
			var pos = RichTextEdits.RemoveRange(doc, Range(0, 2, 2, 3));
			await Assert.That(doc.PlainText).IsEqualTo("held");
			var split = RichTextEdits.SplitBlock(doc, new DocPos(0, 2));
			await Assert.That(doc.PlainText).IsEqualTo("he\nld");
			var joined = RichTextEdits.MergeBlockWithPrevious(doc, split.Block);
			await Assert.That(doc.PlainText).IsEqualTo("held");
			await Assert.That(pos).IsEqualTo(new DocPos(0, 2));
			await Assert.That(joined).IsEqualTo(new DocPos(0, 2));
		}

		[Test]
		public async Task ExtractAndSpliceKeepStylesAcrossParagraphs()
		{
			var doc = Doc("one", "two");
			RichTextCommands.Apply(doc, Range(0, 0, 1, 3), RichCommand.ToggleBold);
			var fragment = RichTextEdits.ExtractRange(doc, Range(0, 1, 1, 2));
			var target = Doc("[]");
			var end = RichTextEdits.SpliceFragment(target, new DocPos(0, 1), fragment);
			await Assert.That(target.PlainText).IsEqualTo("[ne\ntw]");
			await Assert.That(end).IsEqualTo(new DocPos(1, 2));
			await Assert.That(target.Blocks[1].Runs[0].Style.Bold).IsTrue();
			await Assert.That(target.Blocks[1].Runs[1].Style.Bold).IsFalse();
		}

		[Test]
		public async Task CollapsedToggleArmsPendingStyleForTyping()
		{
			var core = new RichEditCore(Doc("ab"));
			core.SetCaret(new DocPos(0, 2));
			core.Exec(RichCommand.ToggleBold);
			await Assert.That(core.CommonStyleOfSelection().Bold).IsEqualTo(true);
			core.Insert("c");
			var runs = core.Doc.Blocks[0].Runs;
			await Assert.That(runs.Count).IsEqualTo(2);
			await Assert.That(runs[1].Text).IsEqualTo("c");
			await Assert.That(runs[1].Style.Bold).IsTrue();
		}

		[Test]
		public async Task TypingEnterBackspaceAndDelete()
		{
			var core = new RichEditCore(new RichDoc());
			core.Insert("hello\nworld");
			await Assert.That(core.PlainText).IsEqualTo("hello\nworld");
			await Assert.That(core.Caret).IsEqualTo(new DocPos(1, 5));

			core.SetCaret(new DocPos(1, 0));
			core.Backspace();
			await Assert.That(core.PlainText).IsEqualTo("helloworld");
			core.DeleteForward();
			await Assert.That(core.PlainText).IsEqualTo("helloorld");
			core.SelectAll();
			core.Insert("x");
			await Assert.That(core.PlainText).IsEqualTo("x");
		}

		[Test]
		public async Task EnterOnEmptyListItemLeavesTheList()
		{
			var core = new RichEditCore(Doc("item"));
			core.Exec(RichCommand.SetList(ListKind.Bullet));
			core.SetCaret(new DocPos(0, 4));
			core.Split();
			await Assert.That(core.Doc.Blocks[1].List).IsEqualTo(ListKind.Bullet);
			core.Split();
			await Assert.That(core.Doc.Blocks.Count).IsEqualTo(2);
			await Assert.That(core.Doc.Blocks[1].List).IsEqualTo(ListKind.None);
		}

		[Test]
		public async Task TypingBurstUndoesAsOneStepThenRedoes()
		{
			var core = new RichEditCore(Doc("a"));
			core.SetCaret(new DocPos(0, 1));
			core.FeedUndo(0);
			core.Insert("b");
			core.FeedUndo(0.1);
			core.Insert("c");
			core.FeedUndo(0.2);
			await Assert.That(core.CanUndo).IsTrue();

			await Assert.That(core.Undo()).IsTrue();
			await Assert.That(core.PlainText).IsEqualTo("a");
			await Assert.That(core.Caret).IsEqualTo(new DocPos(0, 1));

			await Assert.That(core.Redo()).IsTrue();
			await Assert.That(core.PlainText).IsEqualTo("abc");
		}

		[Test]
		public async Task CommandUndoRestoresStyles()
		{
			var core = new RichEditCore(Doc("abc"));
			core.AddUndoPoint();
			core.SelectAll();
			core.Exec(RichCommand.ToggleUnderline);
			core.AddUndoPoint();
			await Assert.That(core.Doc.Blocks[0].Runs[0].Style.Underline).IsTrue();
			core.Undo();
			await Assert.That(core.Doc.Blocks[0].Runs[0].Style.Underline).IsFalse();
		}
	}
}
