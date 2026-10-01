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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Markdig.Agg.Editing;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	public class RichStyleOperationsTests
	{
		private static RichSelection Range(int block, int from, int to) => new RichSelection(new DocPosition(block, from), new DocPosition(block, to));

		private static RichSelection Caret(int block, int offset) => RichSelection.At(new DocPosition(block, offset));

		/// <summary>
		/// A block's runs as "text" or "text:flags" joined by '|', flags B I C S and @url for a link. Asserting on
		/// the model rather than written markdown keeps these tests independent of the inline writer.
		/// </summary>
		private static string Runs(RichBlock block) => Runs(block.Inlines);

		/// <summary>
		/// As <see cref="Runs(RichBlock)"/> for any inline list (a table cell's); an atom shows as "[atom]".
		/// </summary>
		private static string Runs(List<RichInline> inlines) => string.Join("|", inlines.Select(inline =>
		{
			var run = inline as RichRun;
			string flags = (inline.Bold ? "B" : "") + (inline.Italic ? "I" : "") + (run?.Code == true ? "C" : "")
				+ (inline.Strike ? "S" : "") + (inline.LinkUrl != null ? "@" + inline.LinkUrl : "");
			string text = run?.Text ?? "[atom]";
			return flags.Length == 0 ? text : text + ":" + flags;
		}));

		/// <summary>
		/// Writes the edited document and reads it back, so a test can assert the styles survive in markdown.
		/// </summary>
		private static (string Markdown, RichDocument Reread) RoundTrip(RichDocument document)
		{
			string markdown = RichMarkdownWriter.Write(document);
			return (markdown, RichMarkdownParser.Parse(markdown));
		}

		[Test]
		public async Task ToggleBoldMidWordSplitsTheRun()
		{
			var document = RichMarkdownParser.Parse("abcdef\n");
			var selection = Range(0, 2, 4);

			var result = RichStyleOperations.ToggleStyle(document, selection, RichInlineStyle.Bold);

			await Assert.That(result).IsEqualTo(selection);
			await Assert.That(document.Blocks[0].Dirty).IsTrue();
			await Assert.That(Runs(document.Blocks[0])).IsEqualTo("ab|cd:B|ef");
		}

		[Test]
		public async Task ToggleRemovesWhenAllHaveItAndMergesBack()
		{
			var document = RichMarkdownParser.Parse("ab**cd**ef\n");

			RichStyleOperations.ToggleStyle(document, Range(0, 2, 4), RichInlineStyle.Bold);

			// One plain run again, not three that happen to share a style.
			await Assert.That(document.Blocks[0].Inlines.Count).IsEqualTo(1);
			await Assert.That(Runs(document.Blocks[0])).IsEqualTo("abcdef");
		}

		[Test]
		public async Task MixedSelectionAppliesToAllWithoutFragments()
		{
			var document = RichMarkdownParser.Parse("ab**cd**ef\n");

			RichStyleOperations.ToggleStyle(document, Range(0, 0, 6), RichInlineStyle.Bold);

			await Assert.That(document.Blocks[0].Inlines.Count).IsEqualTo(1);
			await Assert.That(Runs(document.Blocks[0])).IsEqualTo("abcdef:B");
		}

		[Test]
		public async Task AcrossParagraphsSkipsRawBlock()
		{
			var document = RichMarkdownParser.Parse("one\n\n---\n\ntwo\n");
			await Assert.That(document.Blocks[1].Kind).IsEqualTo(RichBlockKind.Raw);
			var selection = new RichSelection(new DocPosition(0, 1), new DocPosition(2, 2));

			RichStyleOperations.ToggleStyle(document, selection, RichInlineStyle.Italic);

			await Assert.That(document.Blocks[1].Dirty).IsFalse();
			await Assert.That(Runs(document.Blocks[0]) + "/" + Runs(document.Blocks[2])).IsEqualTo("o|ne:I/tw:I|o");
		}

		[Test]
		public async Task InsideOneTableCell()
		{
			var document = RichMarkdownParser.Parse("| a | b |\n|---|---|\n| cell | x |\n");
			var table = document.Blocks[0];
			await Assert.That(table.Kind).IsEqualTo(RichBlockKind.Table);
			var selection = new RichSelection(new DocPosition(0, 0, 1, 0), new DocPosition(0, 4, 1, 0));

			RichStyleOperations.ToggleStyle(document, selection, RichInlineStyle.Strike);

			var run = (RichRun)table.TableRows[1][0].Inlines.Single();
			await Assert.That(run.Strike).IsTrue();
			await Assert.That(run.Text).IsEqualTo("cell");
			await Assert.That(table.Dirty).IsTrue();
		}

		[Test]
		public async Task StyleAtReportsAllNoneAndMixed()
		{
			var document = RichMarkdownParser.Parse("ab***cd***ef\n");

			var mixed = RichStyleOperations.StyleAt(document, Range(0, 0, 4));
			await Assert.That(mixed.Coverage(RichInlineStyle.Bold)).IsEqualTo(RichStyleCoverage.Mixed);

			var all = RichStyleOperations.StyleAt(document, Range(0, 2, 4));
			await Assert.That(all.Coverage(RichInlineStyle.Bold)).IsEqualTo(RichStyleCoverage.All);
			await Assert.That(all.Coverage(RichInlineStyle.Italic)).IsEqualTo(RichStyleCoverage.All);
			await Assert.That(all.Coverage(RichInlineStyle.Code)).IsEqualTo(RichStyleCoverage.None);

			var none = RichStyleOperations.StyleAt(document, Range(0, 4, 6));
			await Assert.That(none.Coverage(RichInlineStyle.Bold)).IsEqualTo(RichStyleCoverage.None);
			await Assert.That(document.Blocks[0].Dirty).IsFalse();
		}

		[Test]
		public async Task StyleAtCaretFollowsTypingRuleAndLinkRange()
		{
			var document = RichMarkdownParser.Parse("**ab** [link](http://x) c\n");

			var insideBold = RichStyleOperations.StyleAt(document, Caret(0, 2));
			await Assert.That(insideBold.Coverage(RichInlineStyle.Bold)).IsEqualTo(RichStyleCoverage.All);

			var insideLink = RichStyleOperations.StyleAt(document, Caret(0, 5));
			await Assert.That(insideLink.LinkUrl).IsEqualTo("http://x");

			// A caret just after a link shows it (so the link button edits it), though typing there does not
			// extend it.
			var afterLink = RichStyleOperations.StyleAt(document, Caret(0, 7));
			await Assert.That(afterLink.LinkUrl).IsEqualTo("http://x");
			await Assert.That(RichStyleOperations.TypingStyle(document, new DocPosition(0, 7)).LinkUrl).IsNull();

			var wholeLink = RichStyleOperations.StyleAt(document, Range(0, 3, 7));
			await Assert.That(wholeLink.LinkUrl).IsEqualTo("http://x");
			var partLink = RichStyleOperations.StyleAt(document, Range(0, 2, 7));
			await Assert.That(partLink.LinkUrl).IsNull();
		}

		[Test]
		public async Task PendingStyleMakesTypedTextBold()
		{
			var document = RichMarkdownParser.Parse("ab\n");
			var pending = default(RichPendingStyle).Toggle(RichInlineStyle.Bold);
			var caret = new DocPosition(0, 2);

			var shown = RichStyleOperations.StyleAt(document, RichSelection.At(caret), pending);
			await Assert.That(shown.Coverage(RichInlineStyle.Bold)).IsEqualTo(RichStyleCoverage.All);

			var style = pending.ApplyPending(RichStyleOperations.TypingStyle(document, caret));
			RichEditOperations.InsertText(document, caret, "cd", style);

			await Assert.That(Runs(document.Blocks[0])).IsEqualTo("ab|cd:B");
			await Assert.That(pending.Toggle(RichInlineStyle.Bold).IsEmpty).IsTrue();
		}

		[Test]
		public async Task SetAndRemoveLinkOnRange()
		{
			var document = RichMarkdownParser.Parse("see here now\n");

			RichStyleOperations.SetLink(document, Range(0, 4, 8), "http://a");
			await Assert.That(Runs(document.Blocks[0])).IsEqualTo("see |here:@http://a| now");

			RichStyleOperations.RemoveLink(document, Range(0, 4, 8));
			await Assert.That(document.Blocks[0].Inlines.Count).IsEqualTo(1);
			await Assert.That(Runs(document.Blocks[0])).IsEqualTo("see here now");
		}

		[Test]
		public async Task CollapsedCaretLinkOps()
		{
			var document = RichMarkdownParser.Parse("go [a **b** c][ref] end\n\n[ref]: http://old\n");

			// Set on a caret inside a link retargets the whole link and makes it inline.
			RichStyleOperations.SetLink(document, Caret(0, 4), "http://new");
			var links = document.Blocks[0].Inlines.OfType<RichRun>().Where(r => r.LinkUrl != null).ToList();
			await Assert.That(links.Count).IsEqualTo(3);
			await Assert.That(links.All(r => r.LinkUrl == "http://new" && r.LinkLabel == null)).IsTrue();

			// Remove on a caret inside a link removes the whole link, whatever runs it spans.
			RichStyleOperations.RemoveLink(document, Caret(0, 4));
			await Assert.That(document.Blocks[0].Inlines.OfType<RichRun>().Any(r => r.LinkUrl != null)).IsFalse();

			// Set on a caret with no link types the url as linked text.
			var plain = RichMarkdownParser.Parse("x\n");
			var result = RichStyleOperations.SetLink(plain, Caret(0, 1), "http://u");
			await Assert.That(result).IsEqualTo(Caret(0, 9));
			await Assert.That(plain.Blocks[0].Dirty).IsTrue();
			await Assert.That(Runs(plain.Blocks[0])).IsEqualTo("x|http://u:@http://u");
		}

		[Test]
		public async Task BoldMidWordSurvivesWriteAndRead()
		{
			var document = RichMarkdownParser.Parse("abcdef\n");

			RichStyleOperations.ToggleStyle(document, Range(0, 2, 4), RichInlineStyle.Bold);

			var (_, reread) = RoundTrip(document);
			await Assert.That(Runs(reread.Blocks[0])).IsEqualTo("ab|cd:B|ef");
		}

		[Test]
		public async Task BoldOverAnImageWritesOneBoldSpan()
		{
			var document = RichMarkdownParser.Parse("a ![i](p.png) b\n");

			var state = RichStyleOperations.StyleAt(document, Range(0, 0, 5));
			await Assert.That(state.Coverage(RichInlineStyle.Bold)).IsEqualTo(RichStyleCoverage.None);
			RichStyleOperations.ToggleStyle(document, Range(0, 0, 5), RichInlineStyle.Bold);
			await Assert.That(Runs(document.Blocks[0])).IsEqualTo("a :B|[atom]:B| b:B");
			state = RichStyleOperations.StyleAt(document, Range(0, 0, 5));
			await Assert.That(state.Coverage(RichInlineStyle.Bold)).IsEqualTo(RichStyleCoverage.All);

			var (markdown, reread) = RoundTrip(document);
			await Assert.That(markdown).IsEqualTo("**a ![i](p.png) b**\n");
			await Assert.That(Runs(reread.Blocks[0])).IsEqualTo("a :B|[atom]:B| b:B");

			// Toggling again over all-bold text and image removes it from both.
			RichStyleOperations.ToggleStyle(document, Range(0, 0, 5), RichInlineStyle.Bold);
			await Assert.That(Runs(document.Blocks[0])).IsEqualTo("a |[atom]| b");
		}

		[Test]
		public async Task CodeLeavesAnImageAlone()
		{
			var document = RichMarkdownParser.Parse("![i](p.png)\n");

			RichStyleOperations.ToggleStyle(document, Range(0, 0, 1), RichInlineStyle.Code);

			await Assert.That(Runs(document.Blocks[0])).IsEqualTo("[atom]");
			await Assert.That(document.Blocks[0].Dirty).IsFalse();
			var state = RichStyleOperations.StyleAt(document, Range(0, 0, 1));
			await Assert.That(state.Coverage(RichInlineStyle.Code)).IsEqualTo(RichStyleCoverage.None);
		}

		[Test]
		public async Task LinkOverStyledTextSurvivesWriteAndRead()
		{
			var document = RichMarkdownParser.Parse("x **bold** y\n");

			RichStyleOperations.SetLink(document, Range(0, 0, 8), "http://a");

			var (_, reread) = RoundTrip(document);
			await Assert.That(Runs(reread.Blocks[0])).IsEqualTo("x :@http://a|bold:B@http://a| y:@http://a");
		}

		[Test]
		public async Task LinkOverAnImageKeepsOneLink()
		{
			var document = RichMarkdownParser.Parse("a ![i](p.png) b\n");

			RichStyleOperations.SetLink(document, Range(0, 0, 5), "http://a");

			var (markdown, reread) = RoundTrip(document);
			await Assert.That(markdown).IsEqualTo("[a ![i](p.png) b](http://a)\n");
			await Assert.That(Runs(reread.Blocks[0])).IsEqualTo("a :@http://a|[atom]:@http://a| b:@http://a");
			var whole = RichStyleOperations.StyleAt(reread, Range(0, 0, 5));
			await Assert.That(whole.LinkUrl).IsEqualTo("http://a");

			// A caret in the link removes all of it, image included.
			RichStyleOperations.RemoveLink(reread, Caret(0, 1));
			await Assert.That(Runs(reread.Blocks[0])).IsEqualTo("a |[atom]| b");
		}

		[Test]
		public async Task BoldAcrossTableCellsStylesEachCellInReadingOrder()
		{
			var document = RichMarkdownParser.Parse("| a | b |\n|---|---|\n| cell | x |\n| y | z |\n");
			var selection = new RichSelection(new DocPosition(0, 2, 1, 0), new DocPosition(0, 1, 2, 0));

			RichStyleOperations.ToggleStyle(document, selection, RichInlineStyle.Bold);

			var (_, reread) = RoundTrip(document);
			var rows = reread.Blocks[0].TableRows;
			await Assert.That(Runs(rows[0][1].Inlines)).IsEqualTo("b");
			await Assert.That(Runs(rows[1][0].Inlines)).IsEqualTo("ce|ll:B");
			await Assert.That(Runs(rows[1][1].Inlines)).IsEqualTo("x:B");
			await Assert.That(Runs(rows[2][0].Inlines)).IsEqualTo("y:B");
			await Assert.That(Runs(rows[2][1].Inlines)).IsEqualTo("z");

			var state = RichStyleOperations.StyleAt(document, selection);
			await Assert.That(state.Coverage(RichInlineStyle.Bold)).IsEqualTo(RichStyleCoverage.All);
		}

		[Test]
		public async Task LinkAcrossTableCellsLinksEachCell()
		{
			var document = RichMarkdownParser.Parse("| a | b |\n|---|---|\n| c | d |\n");
			var selection = new RichSelection(new DocPosition(0, 0, 1, 0), new DocPosition(0, 1, 1, 1));

			RichStyleOperations.SetLink(document, selection, "http://a");

			var (_, reread) = RoundTrip(document);
			var row = reread.Blocks[0].TableRows[1];
			await Assert.That(Runs(row[0].Inlines) + "/" + Runs(row[1].Inlines)).IsEqualTo("c:@http://a/d:@http://a");
		}

		[Test]
		public async Task EmptyDocument()
		{
			var document = RichMarkdownParser.Parse("");
			await Assert.That(document.Blocks.Count).IsEqualTo(0);

			await Assert.That(RichStyleOperations.StyleAt(document, Caret(0, 0))).IsEqualTo(default(RichStyleState));
			var typing = RichStyleOperations.TypingStyle(document, new DocPosition(0, 0));
			await Assert.That(Runs(new List<RichInline> { typing.WithText("t") })).IsEqualTo("t");
			await Assert.That(RichStyleOperations.RemoveLink(document, Caret(0, 0))).IsEqualTo(Caret(0, 0));
			await Assert.That(document.Blocks.Count).IsEqualTo(0);

			var result = RichStyleOperations.SetLink(document, Caret(0, 0), "http://u");
			await Assert.That(result).IsEqualTo(Caret(0, 8));
			await Assert.That(Runs(document.Blocks[0])).IsEqualTo("http://u:@http://u");
		}

		[Test]
		public async Task CaretAtEitherEdgeOfLinkShowsIt()
		{
			var document = RichMarkdownParser.Parse("[link](http://x \"T\") c\n");

			var atEnd = RichStyleOperations.StyleAt(document, Caret(0, 4));
			await Assert.That(atEnd.LinkUrl).IsEqualTo("http://x");
			await Assert.That(atEnd.LinkTitle).IsEqualTo("T");
			await Assert.That(RichStyleOperations.StyleAt(document, Caret(0, 0)).LinkUrl).IsEqualTo("http://x");
			await Assert.That(RichStyleOperations.StyleAt(document, Caret(0, 6)).LinkUrl).IsNull();
		}

		[Test]
		public async Task RetargetFromCaretKeepsTitle()
		{
			var document = RichMarkdownParser.Parse("[a](http://old \"T\")\n");

			RichStyleOperations.SetLink(document, Caret(0, 1), "http://new");

			var run = (RichRun)document.Blocks[0].Inlines.Single();
			await Assert.That(run.LinkUrl).IsEqualTo("http://new");
			await Assert.That(run.LinkTitle).IsEqualTo("T");
		}

		[Test]
		public async Task CaretLinkDisplayTextIsTrimmedOneLine()
		{
			var document = RichMarkdownParser.Parse("x\n");
			string url = "  http://u\n\tz ";

			var result = RichStyleOperations.SetLink(document, Caret(0, 1), url);

			await Assert.That(result).IsEqualTo(Caret(0, 12));
			await Assert.That(document.Blocks.Count).IsEqualTo(1);
			var link = (RichRun)document.Blocks[0].Inlines[1];
			await Assert.That(link.Text).IsEqualTo("http://u  z");
			await Assert.That(link.LinkUrl).IsEqualTo(url);
		}

		[Test]
		public async Task CaretLinkInsideCodeIsNotCode()
		{
			var document = RichMarkdownParser.Parse("`abc`\n");

			RichStyleOperations.SetLink(document, Caret(0, 1), "http://u");

			await Assert.That(Runs(document.Blocks[0])).IsEqualTo("a:C|http://u:@http://u|bc:C");
		}

		[Test]
		public async Task NoChangeLeavesBlockClean()
		{
			var document = RichMarkdownParser.Parse("ab cd\n");

			RichStyleOperations.RemoveLink(document, Range(0, 0, 5));

			await Assert.That(document.Blocks[0].Dirty).IsFalse();
			await Assert.That(Runs(document.Blocks[0])).IsEqualTo("ab cd");
		}
	}
}
