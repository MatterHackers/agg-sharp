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
using TUnit.Assertions.Enums;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	public class RichMarkdownParserTests
	{
		// Each string stresses one way the block partition or span arithmetic could drift by a byte.
		public static IEnumerable<string> Corpus()
		{
			yield return "";
			yield return "\n";
			yield return "\n\n\n";
			yield return "Just text";
			yield return "Just text\n";
			yield return "Line one\nline two\n\n\n\nAfter three blank lines\n\n";
			yield return "---\ntitle: Notes\ntags: [a, b]\n---\n# Heading\n\nBody\n";
			yield return "---\r\ntitle: x\r\n---\r\n\r\nText\r\n";
			yield return "---\nnot closed\n\nText\n";
			yield return "# One\r\n\r\nA **bold** and *it*\r\nsecond line\r\n\r\n## Two\r\n";
			yield return "No trailing newline after **this**";
			yield return "Setext One\n==========\n\nSetext Two\n---\n\nAfter\n";
			yield return "See [the docs][docs] and [inline](http://x.com).\n\n[docs]: http://docs.example.com\n";
			yield return "[docs]: http://docs.example.com\n\nUse [docs].\n";
			yield return "[a]: /a\n[b]: /b \"Title\"\nText after defs\n";
			yield return "***both*** **bold *nested italic* bold** _under_ __double__ ~~gone~~\n";
			yield return "Inline <span style=\"color:red\">html</span> here\n";
			yield return "An image ![alt text](images/a.png \"t\") and <http://auto.link> and www.bare.com\n";
			yield return "Escapes \\*not italic\\* \\[x\\] &amp; &copy; `code ``with`` ticks`\n";
			yield return "Hard  \nbreak and back\\\nslash break\n";
			yield return "- item\n- item two\n\n> quote\n> more\n\n```cs\ncode\n```\n\n    indented code\n\n---\n\n| a | b |\n|---|:-:|\n| 1 | 2 |\n";
			yield return "<div align=\"center\">\n\nCentered\n\n</div>\n";
			yield return "  Indented paragraph\n\n   ### Indented heading ###   \n";
			yield return "Para\n- list right after\n\nText ^sup^ and ~sub~ ==m== ++i++ **^b^**\r\n";
			yield return "* [ ] task\n* [x] done\n\n\n";
			yield return "Title link [t](/u \"title\")\n";
			yield return "trailing spaces   \n\n  \t\n";

			// Lists, quotes and alignment groups (see RichMarkdownParserBlockTests for their shapes).
			yield return "- a\n  - b\n    - c\n  - d\n- e\n";
			yield return "3. three\n4. four\n\n10) ten\n11) eleven\n";
			yield return "* star\n* star two\n\n+ plus\n+ plus two\r\n";
			yield return "- loose\n\n- items\n\n\n- far apart\n";
			yield return "- [ ] task\n- [x] done\n  wrapped line\n";
			yield return "- one para\n\n  second para\n- next\n";
			yield return "- item\n\n  ```\n  code\n  ```\n";
			yield return "> one para\n> two lines\n";
			yield return "> first\n>\n> second\n>\n";
			yield return "  > indented\r\n  >\r\n  > quote\r\n";
			yield return "> outer\n>\n> > nested\n";
			yield return "<div align=\"center\">\n\n# Title\n\nCentered **text**\n\nMore\n\n</div>\n\nAfter\n";
			yield return "<div align=\"right\">\r\n\r\nRight\r\n\r\n</div>";
			yield return "<div align=\"center\">\n\nNever closed\n";
			yield return "<div>\n\n<div align=\"center\">\n\nNested\n\n</div>\n\n</div>\n";
			yield return "<div align=\"center\">\n\n- list\n\n</div>\n";

			// Code blocks and tables (see RichMarkdownParserCodeTableTests for their shapes).
			foreach (var markdown in RichMarkdownParserCodeTableTests.Samples)
			{
				yield return markdown;
			}
		}

		[Test]
		[MethodDataSource(nameof(Corpus))]
		public async Task UneditedDocumentRoundTripsByteForByte(string markdown)
		{
			var document = RichMarkdownParser.Parse(markdown);
			await Assert.That(RichMarkdownWriter.Write(document)).IsEqualTo(markdown);

			// Separators and trailing text are whitespace only, or content would be hidden from the rich view.
			foreach (var block in document.Blocks)
			{
				await Assert.That(string.IsNullOrWhiteSpace(block.SeparatorBefore)).IsTrue();
				await Assert.That(block.OriginalSource.Length).IsGreaterThan(0);

				// A block starting on a line break means a span began early and swallowed the gap before it.
				await Assert.That(block.OriginalSource[0] == '\n' || block.OriginalSource[0] == '\r').IsFalse();
			}

			await Assert.That(string.IsNullOrWhiteSpace(document.TrailingText)).IsTrue();
		}

		[Test]
		public async Task FrontmatterIsHiddenAndNotABlock()
		{
			var document = RichMarkdownParser.Parse("---\ntitle: Notes\n---\n# Heading\n\nBody\n");
			await Assert.That(document.Frontmatter).IsEqualTo("---\ntitle: Notes\n---\n");
			await Assert.That(document.Blocks.Select(b => b.Kind).ToArray())
				.IsEquivalentTo(new[] { RichBlockKind.Heading, RichBlockKind.Paragraph }, CollectionOrdering.Matching);
			await Assert.That(document.Blocks[1].SeparatorBefore).IsEqualTo("\n\n");
			await Assert.That(document.TrailingText).IsEqualTo("\n");
		}

		[Test]
		public async Task HeadingsKeepLevelAndText()
		{
			var document = RichMarkdownParser.Parse("## Two\r\n\r\nSetext\r\n---\r\n");
			await Assert.That(document.Blocks[0].Kind).IsEqualTo(RichBlockKind.Heading);
			await Assert.That(document.Blocks[0].HeadingLevel).IsEqualTo(2);
			await Assert.That(PlainText(document.Blocks[0])).IsEqualTo("Two");
			await Assert.That(document.Blocks[1].Kind).IsEqualTo(RichBlockKind.Heading);
			await Assert.That(document.Blocks[1].HeadingLevel).IsEqualTo(2);
			await Assert.That(document.Blocks[1].OriginalSource).IsEqualTo("Setext\r\n---");
			await Assert.That(PlainText(document.Blocks[1])).IsEqualTo("Setext");
		}

		[Test]
		public async Task EmphasisBecomesRunStyles()
		{
			var block = RichMarkdownParser.Parse("a **b *c* d** ***e*** ~~f~~ `g`\n").Blocks.Single();
			await Assert.That(block.Kind).IsEqualTo(RichBlockKind.Paragraph);
			var runs = block.Inlines.Cast<RichRun>().ToList();
			await Assert.That(string.Join("|", runs.Select(r => r.Text))).IsEqualTo("a |b |c| d| |e| |f| |g");

			await Assert.That(runs[1].Bold && !runs[1].Italic).IsTrue();
			await Assert.That(runs[2].Bold && runs[2].Italic).IsTrue();
			await Assert.That(runs[5].Bold && runs[5].Italic).IsTrue();
			await Assert.That(runs[7].Strike).IsTrue();
			await Assert.That(runs[9].Code).IsTrue();
			await Assert.That(runs[0].Bold || runs[0].Italic || runs[0].Code).IsFalse();
		}

		[Test]
		public async Task LinksSetLinkUrlOnTheirRuns()
		{
			var block = RichMarkdownParser.Parse("[go **there**](http://x.com) or [ref]\n\n[ref]: /r\n").Blocks[0];
			var runs = block.Inlines.Cast<RichRun>().ToList();
			await Assert.That(runs[0].Text).IsEqualTo("go ");
			await Assert.That(runs[0].LinkUrl).IsEqualTo("http://x.com");
			await Assert.That(runs[1].Text).IsEqualTo("there");
			await Assert.That(runs[1].Bold).IsTrue();
			await Assert.That(runs[1].LinkUrl).IsEqualTo("http://x.com");
			await Assert.That(runs[2].LinkUrl).IsNull();
			await Assert.That(runs[3].Text).IsEqualTo("ref");
			await Assert.That(runs[3].LinkUrl).IsEqualTo("/r");
			await Assert.That(runs[0].LinkLabel).IsNull();
			await Assert.That(runs[3].LinkLabel).IsEqualTo("ref");
		}

		[Test]
		public async Task ReferenceLinksKeepTheirLabelAsWritten()
		{
			var block = RichMarkdownParser.Parse("[t][My Ref], [r2][] [r3] [i](/i \"T\")\n\n[my ref]: /a\n[r2]: /b \"Title\"\n[r3]: /c\n").Blocks[0];
			var links = block.Inlines.Cast<RichRun>().Where(r => r.LinkUrl != null).ToList();
			await Assert.That(string.Join("|", links.Select(r => $"{r.Text}>{r.LinkUrl}>{r.LinkLabel ?? "null"}>{r.LinkTitle ?? "null"}")))
				.IsEqualTo("t>/a>My Ref>null|r2>/b>r2>null|r3>/c>r3>null|i>/i>null>T");
		}

		[Test]
		public async Task DefinitionsAtTheTopOfAParagraphBecomeTheirOwnRawBlock()
		{
			var document = RichMarkdownParser.Parse("[a]: /a\n[b]: /b \"Title\"\nText after defs\n");
			await Assert.That(Shape(document)).IsEqualTo("Raw:[a]: /a\n[b]: /b \"Title\"|Paragraph:Text after defs");
			await Assert.That(document.Blocks[1].SeparatorBefore).IsEqualTo("\n");
			await Assert.That(PlainText(document.Blocks[1])).IsEqualTo("Text after defs");

			var indented = RichMarkdownParser.Parse("Intro\r\n\r\n [a]: /a\r\n  **Bold** text\r\n");
			// A Raw block keeps the indentation of its first line; a modelled block's goes to its separator.
			await Assert.That(Shape(indented)).IsEqualTo("Paragraph:Intro|Raw: [a]: /a|Paragraph:**Bold** text");
			await Assert.That(indented.Blocks[2].SeparatorBefore).IsEqualTo("\r\n  ");
		}

		[Test]
		public async Task HeadingClosingSequenceBelongsToTheHeading()
		{
			var document = RichMarkdownParser.Parse("# H #\n## Two ##   \r\n### Three   \nText\n");
			await Assert.That(Shape(document)).IsEqualTo("Heading:# H #|Heading:## Two ##   |Heading:### Three   |Paragraph:Text");
			await Assert.That(PlainText(document.Blocks[0])).IsEqualTo("H");
			await Assert.That(PlainText(document.Blocks[1])).IsEqualTo("Two");
		}

		[Test]
		public async Task ReferenceDefinitionsSurviveAsRawBlocks()
		{
			var document = RichMarkdownParser.Parse("Use [docs].\n\n[docs]: http://d.com\n");
			await Assert.That(Shape(document)).IsEqualTo("Paragraph:Use [docs].|Raw:[docs]: http://d.com");
		}

		[Test]
		public async Task AtomsKeepTheirExactSource()
		{
			var block = RichMarkdownParser.Parse("![a](b.png) <span>x</span> <http://a.com> www.c.com end  \nnext\n").Blocks.Single();
			var atoms = block.Inlines.OfType<InlineAtom>().ToList();
			await Assert.That(atoms.Select(a => a.Kind).ToArray()).IsEquivalentTo(new[]
			{
				InlineAtomKind.Image,
				InlineAtomKind.Html,
				InlineAtomKind.Html,
				InlineAtomKind.Autolink,
				InlineAtomKind.Autolink,
				InlineAtomKind.HardBreak,
			}, CollectionOrdering.Matching);
			await Assert.That(atoms[0].RawMarkdown).IsEqualTo("![a](b.png)");
			await Assert.That(atoms[1].RawMarkdown).IsEqualTo("<span>");
			await Assert.That(atoms[2].RawMarkdown).IsEqualTo("</span>");
			await Assert.That(atoms[3].RawMarkdown).IsEqualTo("<http://a.com>");
			await Assert.That(atoms[4].RawMarkdown).IsEqualTo("www.c.com");
			await Assert.That(atoms[5].RawMarkdown).IsEqualTo("  \n");
			await Assert.That(PlainText(block)).IsEqualTo("? ?x? ? ? end?next");
		}

		[Test]
		public async Task PairedStyleTagsBecomeStyles()
		{
			var styled = RichMarkdownParser.Parse("a<b>x<i>y</i></b><del>z</del><S>w</S>\n").Blocks.Single();
			await Assert.That(styled.Inlines.OfType<InlineAtom>().Count()).IsEqualTo(0);
			var runs = styled.Inlines.Cast<RichRun>().ToList();
			await Assert.That(string.Join(",", runs.Select(r => r.Text + (r.Bold ? "B" : "") + (r.Italic ? "I" : "") + (r.Strike ? "S" : ""))))
				.IsEqualTo("a,xB,yBI,zwS");

			// Attributes, an unpaired tag or crossed pairs keep their tags as atoms.
			foreach (var markdown in new[] { "<b class=\"k\">x</b>\n", "a <b>x\n", "<b><i>x</b></i>\n" })
			{
				var block = RichMarkdownParser.Parse(markdown).Blocks.Single();
				await Assert.That(block.Inlines.OfType<InlineAtom>().Count()).IsGreaterThan(0);
				await Assert.That(block.Inlines.Any(inline => inline.Bold || inline.Italic)).IsFalse();
			}
		}

		[Test]
		public async Task CrlfHardBreaksKeepTheirWholeSource()
		{
			var spaces = RichMarkdownParser.Parse("a  \r\nb\r\n").Blocks.Single();
			await Assert.That(spaces.Inlines.OfType<InlineAtom>().Single().RawMarkdown).IsEqualTo("  \r\n");

			var block = RichMarkdownParser.Parse("a\\\r\nb\r\n").Blocks.Single();
			await Assert.That(block.Inlines.OfType<InlineAtom>().Single().RawMarkdown).IsEqualTo("\\\r\n");
			await Assert.That(PlainText(block)).IsEqualTo("a?b");
		}

		[Test]
		public async Task EscapesAndEntitiesReadAsPlainText()
		{
			var block = RichMarkdownParser.Parse("\\*a\\* &amp; b\nc\n").Blocks.Single();
			await Assert.That(PlainText(block)).IsEqualTo("*a* & b c");
		}

		[Test]
		public async Task UnmodelledBlocksAreRaw()
		{
			var document = RichMarkdownParser.Parse("---\n\n+---+\n| a |\n+---+\n\n<div>\nx\n</div>\n");
			await Assert.That(document.Blocks.All(b => b.Kind == RichBlockKind.Raw)).IsTrue();
			await Assert.That(document.Blocks.Count).IsEqualTo(3);
		}

		[Test]
		public async Task TitledLinkKeepsItsTitleOnTheRuns()
		{
			var block = RichMarkdownParser.Parse("See [the *docs*](/u \"The title\") now\n").Blocks.Single();
			await Assert.That(block.Kind).IsEqualTo(RichBlockKind.Paragraph);
			var runs = block.Inlines.Cast<RichRun>().ToList();
			await Assert.That(string.Join("|", runs.Select(r => r.Text))).IsEqualTo("See |the |docs| now");
			await Assert.That(runs[1].LinkUrl).IsEqualTo("/u");
			await Assert.That(runs[1].LinkTitle).IsEqualTo("The title");
			await Assert.That(runs[2].LinkTitle).IsEqualTo("The title");
			await Assert.That(runs[2].Italic).IsTrue();
			await Assert.That(runs[3].LinkTitle).IsNull();
		}

		[Test]
		public async Task UnmodelledInlinesBecomeRawAtomsOfTheirSource()
		{
			var block = RichMarkdownParser.Parse("a ^sup^ b ~sub~ c ==mark== d ++ins++ e\n").Blocks.Single();
			await Assert.That(block.Kind).IsEqualTo(RichBlockKind.Paragraph);
			var atoms = block.Inlines.OfType<InlineAtom>().ToList();
			await Assert.That(atoms.All(a => a.Kind == InlineAtomKind.Raw)).IsTrue();
			await Assert.That(string.Join("|", atoms.Select(a => a.RawMarkdown))).IsEqualTo("^sup^|~sub~|==mark==|++ins++");
			await Assert.That(PlainText(block)).IsEqualTo("a ? b ? c ? d ? e");
		}

		[Test]
		public async Task DirtyListItemIsRegenerated()
		{
			var document = RichMarkdownParser.Parse("* item\n");
			document.Blocks[0].Dirty = true;
			await Assert.That(RichMarkdownWriter.Write(document)).IsEqualTo("* item\n");
		}

		private static string Shape(RichDocument document)
		{
			return string.Join("|", document.Blocks.Select(b => $"{b.Kind}:{b.OriginalSource}"));
		}

		private static string PlainText(RichBlock block)
		{
			return string.Concat(block.Inlines.Select(inline => inline is RichRun run ? run.Text : "?"));
		}
	}
}
