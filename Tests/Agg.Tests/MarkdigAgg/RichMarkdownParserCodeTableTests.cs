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
	/// <summary>
	/// Block shapes for code blocks and tables. Every sample here is also in
	/// <see cref="RichMarkdownParserTests.Corpus"/>, which checks its byte-identical round trip.
	/// </summary>
	public class RichMarkdownParserCodeTableTests
	{
		private const string BacktickFence = "Intro\n\n```cs\nvar x = 1;\n\n  indented();\n```\n\nAfter\n";
		private const string TildeFence = "~~~~python\r\nprint('~~~')\r\n```\r\n~~~~\r\n";
		private const string LongFence = "````\n```\ninner\n```\n````\n";
		private const string IndentedFence = "  ```\n  a\n   b\n  ```\n";
		private const string FenceArguments = "```cs title=x\ncode\n```\n";
		private const string EntityInfo = "~~~a&#96;b  \ncode\n~~~\n";
		private const string IndentedFenceAfterParagraph = "Para\n\n ```\n a\n  b\n ```\n";
		private const string EmptyFence = "```\n```\n";
		private const string FenceAtEof = "Text\n\n```js\nlast()\n```";
		private const string UnclosedFence = "```js\nunclosed\n\nstill code";
		private const string IndentedCode = "Para\n\n    one\n\n    \n    two  \n\n\nAfter\n";
		private const string CrlfIndentedCode = "    code\r\n    two\r\n";
		private const string AlignedTable = "| Left | Center | Right | None |\n|:-----|:------:|------:|------|\n| **b** | *i* | [l](http://x) | `c` |\n";
		private const string EscapedPipes = "| a | b |\n|---|---|\n| 1 \\| x | `c|d` |\n";
		private const string NoOuterPipes = "a | b\n--- | :-:\n1 | 2\n";
		private const string ShortRows = "| a | b | c |\n|---|---|---|\n| 1 |\n| | x |\n";
		private const string CrlfTable = "| a |\r\n|---|\r\n| **b** |\r\n";
		private const string TableAtoms = "| ![i](a.png) | <b>x</b> |\n|---|---|\n";
		private const string WideRow = "|a|b|\n|-|-|\n|1|2|3|\n";
		private const string GridTable = "+---+\n| a |\n+===+\n| b |\n+---+\n";
		private const string TableInAlign = "<div align=\"center\">\n\n| a |\n|---|\n| 1 |\n\n</div>\n";
		private const string CodeInAlign = "<div align=\"center\">\n\n```\ncode\n```\n\n</div>\n";
		private const string CodeInQuote = "> ```\n> code\n> ```\n";

		public static IEnumerable<string> Samples => new[]
		{
			BacktickFence, TildeFence, LongFence, IndentedFence, FenceArguments, EntityInfo, IndentedFenceAfterParagraph, EmptyFence, FenceAtEof, UnclosedFence,
			IndentedCode, CrlfIndentedCode, AlignedTable, EscapedPipes, NoOuterPipes, ShortRows, CrlfTable, TableAtoms,
			WideRow, GridTable, TableInAlign, CodeInAlign, CodeInQuote,
		};

		[Test]
		public async Task FencedCodeKeepsTextLanguageAndFence()
		{
			var document = RichMarkdownParser.Parse(BacktickFence);
			await Assert.That(Kinds(document)).IsEqualTo("Paragraph|CodeBlock|Paragraph");
			var code = document.Blocks[1];
			await Assert.That(code.CodeText).IsEqualTo("var x = 1;\n\n  indented();");
			await Assert.That(code.CodeLanguage).IsEqualTo("cs");
			await Assert.That(code.CodeFence).IsEqualTo("```");
			await Assert.That(code.OriginalSource).IsEqualTo("```cs\nvar x = 1;\n\n  indented();\n```");

			var tilde = RichMarkdownParser.Parse(TildeFence).Blocks.Single();
			await Assert.That(tilde.Kind).IsEqualTo(RichBlockKind.CodeBlock);
			await Assert.That(tilde.CodeText).IsEqualTo("print('~~~')\n```");
			await Assert.That(tilde.CodeLanguage).IsEqualTo("python");
			await Assert.That(tilde.CodeFence).IsEqualTo("~~~~");

			var longFence = RichMarkdownParser.Parse(LongFence).Blocks.Single();
			await Assert.That(longFence.CodeText).IsEqualTo("```\ninner\n```");
			await Assert.That(longFence.CodeFence).IsEqualTo("````");
			await Assert.That(longFence.CodeLanguage).IsEqualTo("");

			// The fence's indentation is stripped from the content lines, so it belongs to the block's source.
			var indented = RichMarkdownParser.Parse(IndentedFence);
			await Assert.That(indented.Blocks.Single().CodeText).IsEqualTo("a\n b");
			await Assert.That(indented.Blocks[0].OriginalSource).IsEqualTo(IndentedFence.TrimEnd('\n'));
			await Assert.That(indented.Blocks[0].SeparatorBefore).IsEqualTo("");

			var afterParagraph = RichMarkdownParser.Parse(IndentedFenceAfterParagraph);
			await Assert.That(afterParagraph.Blocks[1].OriginalSource).IsEqualTo(" ```\n a\n  b\n ```");
			await Assert.That(afterParagraph.Blocks[1].SeparatorBefore).IsEqualTo("\n\n");
			await Assert.That(afterParagraph.Blocks[1].CodeText).IsEqualTo("a\n b");
		}

		[Test]
		public async Task InfoStringIsKeptAsWritten()
		{
			var arguments = RichMarkdownParser.Parse(FenceArguments).Blocks.Single();
			await Assert.That(arguments.Kind).IsEqualTo(RichBlockKind.CodeBlock);
			await Assert.That(arguments.CodeInfo).IsEqualTo("cs title=x");
			await Assert.That(arguments.CodeLanguage).IsEqualTo("cs");

			var entity = RichMarkdownParser.Parse(EntityInfo).Blocks.Single();
			await Assert.That(entity.Kind).IsEqualTo(RichBlockKind.CodeBlock);
			await Assert.That(entity.CodeInfo).IsEqualTo("a&#96;b");
			await Assert.That(entity.CodeLanguage).IsEqualTo("a&#96;b");
			await Assert.That(entity.CodeFence).IsEqualTo("~~~");

			var crlf = RichMarkdownParser.Parse(TildeFence).Blocks.Single();
			await Assert.That(crlf.CodeInfo).IsEqualTo("python");
			await Assert.That(RichMarkdownParser.Parse(IndentedCode).Blocks[1].CodeInfo).IsEqualTo("");
		}

		[Test]
		public async Task EmptyEofAndUnclosedFencesAreCodeBlocks()
		{
			var empty = RichMarkdownParser.Parse(EmptyFence).Blocks.Single();
			await Assert.That(empty.Kind).IsEqualTo(RichBlockKind.CodeBlock);
			await Assert.That(empty.CodeText).IsEqualTo("");

			var atEof = RichMarkdownParser.Parse(FenceAtEof);
			await Assert.That(atEof.Blocks[1].CodeText).IsEqualTo("last()");
			await Assert.That(atEof.Blocks[1].OriginalSource).IsEqualTo("```js\nlast()\n```");
			await Assert.That(atEof.TrailingText).IsEqualTo("");

			// An unclosed fence runs to the end of the document; Markdig's span stops at its opening line.
			var unclosed = RichMarkdownParser.Parse(UnclosedFence).Blocks.Single();
			await Assert.That(unclosed.Kind).IsEqualTo(RichBlockKind.CodeBlock);
			await Assert.That(unclosed.CodeText).IsEqualTo("unclosed\n\nstill code");
			await Assert.That(unclosed.OriginalSource).IsEqualTo(UnclosedFence);
		}

		[Test]
		public async Task IndentedCodeEndsAtItsLastLine()
		{
			var document = RichMarkdownParser.Parse(IndentedCode);
			await Assert.That(Kinds(document)).IsEqualTo("Paragraph|CodeBlock|Paragraph");
			var code = document.Blocks[1];
			await Assert.That(code.CodeText).IsEqualTo("one\n\n\ntwo  ");
			await Assert.That(code.CodeFence).IsEqualTo("");
			await Assert.That(code.OriginalSource).IsEqualTo("    one\n\n    \n    two  ");
			await Assert.That(document.Blocks[2].SeparatorBefore).IsEqualTo("\n\n\n");

			var crlf = RichMarkdownParser.Parse(CrlfIndentedCode);
			await Assert.That(crlf.Blocks.Single().CodeText).IsEqualTo("code\ntwo");
			await Assert.That(crlf.Blocks[0].OriginalSource).IsEqualTo("    code\r\n    two");
			await Assert.That(crlf.TrailingText).IsEqualTo("\r\n");
		}

		[Test]
		public async Task TableCellsReadAsParagraphInlines()
		{
			var table = RichMarkdownParser.Parse(AlignedTable).Blocks.Single();
			await Assert.That(table.Kind).IsEqualTo(RichBlockKind.Table);
			await Assert.That(Cells(table)).IsEqualTo("Left|Center|Right|None/b|i|l|c");
			await Assert.That(string.Join(",", table.ColumnAlignments.Select(a => a?.ToString() ?? "null")))
				.IsEqualTo("Left,Center,Right,null");
			var body = table.TableRows[1].Select(cell => (RichRun)cell.Inlines.Single()).ToList();
			await Assert.That(body[0].Bold).IsTrue();
			await Assert.That(body[1].Italic).IsTrue();
			await Assert.That(body[2].LinkUrl).IsEqualTo("http://x");
			await Assert.That(body[3].Code).IsTrue();
			await Assert.That(table.OriginalSource).IsEqualTo(AlignedTable.TrimEnd('\n'));

			var crlf = RichMarkdownParser.Parse(CrlfTable).Blocks.Single();
			await Assert.That(Cells(crlf)).IsEqualTo("a/b");
			await Assert.That(((RichRun)crlf.TableRows[1][0].Inlines.Single()).Bold).IsTrue();
			await Assert.That(crlf.OriginalSource).IsEqualTo("| a |\r\n|---|\r\n| **b** |");

			var atoms = RichMarkdownParser.Parse(TableAtoms).Blocks.Single();
			await Assert.That(atoms.Kind).IsEqualTo(RichBlockKind.Table);
			await Assert.That(atoms.TableRows.Count).IsEqualTo(1);
			await Assert.That(((InlineAtom)atoms.TableRows[0][0].Inlines.Single()).Kind).IsEqualTo(InlineAtomKind.Image);
			await Assert.That(atoms.TableRows[0][1].Inlines.Count).IsEqualTo(3);
		}

		[Test]
		public async Task EscapedAndCodePipesStayInTheirCell()
		{
			var table = RichMarkdownParser.Parse(EscapedPipes).Blocks.Single();
			await Assert.That(table.Kind).IsEqualTo(RichBlockKind.Table);
			await Assert.That(Cells(table)).IsEqualTo("a|b/1 ¦ x|c¦d");
			await Assert.That(((RichRun)table.TableRows[1][1].Inlines.Single()).Code).IsTrue();

			var bare = RichMarkdownParser.Parse(NoOuterPipes).Blocks.Single();
			await Assert.That(Cells(bare)).IsEqualTo("a|b/1|2");
			await Assert.That(string.Join(",", bare.ColumnAlignments.Select(a => a?.ToString() ?? "null"))).IsEqualTo("null,Center");
		}

		[Test]
		public async Task ShortRowsArePaddedWithEmptyCells()
		{
			var table = RichMarkdownParser.Parse(ShortRows).Blocks.Single();
			await Assert.That(table.Kind).IsEqualTo(RichBlockKind.Table);
			await Assert.That(Cells(table)).IsEqualTo("a|b|c/1||/|x|");
			await Assert.That(table.TableRows.All(row => row.Count == 3)).IsTrue();
		}

		[Test]
		public async Task UnmodelledTablesAndCodeInContainersStayRaw()
		{
			await Assert.That(Kinds(RichMarkdownParser.Parse(WideRow))).IsEqualTo("Raw");
			await Assert.That(Kinds(RichMarkdownParser.Parse(GridTable))).IsEqualTo("Raw");
			await Assert.That(Kinds(RichMarkdownParser.Parse(CodeInQuote))).IsEqualTo("Raw");

			// An align group holding a table or code block is not folded: the rich view aligns only text.
			var table = RichMarkdownParser.Parse(TableInAlign);
			await Assert.That(Kinds(table)).IsEqualTo("Raw|Table|Raw");
			await Assert.That(table.Blocks[1].AlignGroup).IsNull();

			var code = RichMarkdownParser.Parse(CodeInAlign);
			await Assert.That(Kinds(code)).IsEqualTo("Raw|CodeBlock|Raw");
			await Assert.That(code.Blocks[1].AlignGroup).IsNull();
		}

		[Test]
		public async Task CloneCopiesCodeAndCells()
		{
			var document = RichMarkdownParser.Parse(TildeFence + "\r\n" + AlignedTable);
			var copy = document.Clone();
			await Assert.That(copy.Blocks[0].CodeFence).IsEqualTo("~~~~");
			await Assert.That(copy.Blocks[1].TableRows[1][0].Inlines).IsNotSameReferenceAs(document.Blocks[1].TableRows[1][0].Inlines);
			await Assert.That(RichMarkdownWriter.Write(copy)).IsEqualTo(RichMarkdownWriter.Write(document));
		}

		[Test]
		public async Task DirtyCodeAndTableAreNotWrittenYet()
		{
			var document = RichMarkdownParser.Parse(BacktickFence + "\n" + AlignedTable);
			document.Blocks.First(b => b.Kind == RichBlockKind.CodeBlock).Dirty = true;
			await Assert.That(() => RichMarkdownWriter.Write(document)).Throws<System.NotImplementedException>();

			var table = RichMarkdownParser.Parse(AlignedTable);
			table.Blocks[0].Dirty = true;
			await Assert.That(() => RichMarkdownWriter.Write(table)).Throws<System.NotImplementedException>();
		}

		private static string Kinds(RichDocument document) => string.Join("|", document.Blocks.Select(b => b.Kind));

		// Rows split by '/', cells by '|'; a '|' inside a cell shows as a broken bar so it cannot pass for a split.
		private static string Cells(RichBlock table)
		{
			return string.Join("/", table.TableRows.Select(row => string.Join("|", row.Select(cell =>
				string.Concat(cell.Inlines.Select(inline => inline is RichRun run ? run.Text.Replace('|', '¦') : "?"))))));
		}
	}
}
