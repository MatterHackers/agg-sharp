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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Markdig.Agg.Editing;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	public class RichMarkdownWriterCodeTableTests
	{
		private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();

		public static IEnumerable<string> CodeAndTables()
		{
			yield return "Text\n\n```cs title=x\nvar a = 1;\n\nb();\n```\n";
			yield return "~~~~\n```\ninside\n```\n~~~~\n";
			yield return "Text\n\n    indented\n      more\n\nafter\n";
			yield return "```\n```\n";
			yield return "| Left | Center | Right | None |\n|:-----|:------:|------:|------|\n| **b** | *i* | [l](http://x) | `c` |\n";
			yield return "| a\\|b | `c|d` |\n|---|---|\n| <span>x</span> | ![i](a.png) |\n";
			yield return "| a |\r\n|---|\r\n| **b** |\r\n";
		}

		/// <summary>
		/// The judge for code blocks and tables: rewriting every one must not change the rendered HTML.
		/// </summary>
		[Test]
		[MethodDataSource(nameof(CodeAndTables))]
		public async Task RewrittenCodeAndTablesRenderTheSame(string markdown)
		{
			var document = RichMarkdownParser.Parse(markdown);
			var edited = document.Blocks.Where(block => block.Kind == RichBlockKind.CodeBlock || block.Kind == RichBlockKind.Table).ToList();
			await Assert.That(edited.Count).IsGreaterThan(0);
			edited.ForEach(block => block.Dirty = true);

			await Assert.That(Html(RichMarkdownWriter.Write(document))).IsEqualTo(Html(markdown));
		}

		[Test]
		public async Task CodeFenceGrowsPastRunsInTheCode()
		{
			await Assert.That(WriteCode("```", "cs", "a\n```\nb")).IsEqualTo("````cs\na\n```\nb\n````");
			await Assert.That(WriteCode("~~~", "", "```")).IsEqualTo("~~~\n```\n~~~");
			await Assert.That(WriteCode("", "", "x\n  y")).IsEqualTo("    x\n      y");

			// Indented code cannot start with a blank line or be empty, so those write fenced.
			await Assert.That(WriteCode("", "", "\nx")).IsEqualTo("```\n\nx\n```");
			await Assert.That(WriteCode("", "", "")).IsEqualTo("```\n```");
		}

		[Test]
		public async Task TableWritesPipeRowsWithAlignments()
		{
			var table = new RichBlock { Kind = RichBlockKind.Table, Dirty = true };
			table.TableRows.Add(new List<RichTableCell> { Cell(new RichRun("a|b")), Cell(new RichRun("c|d") { Code = true }) });
			table.TableRows.Add(new List<RichTableCell> { Cell(new RichRun("x"), new InlineAtom(InlineAtomKind.HardBreak, "  \n"), new RichRun("y")), Cell() });
			table.ColumnAlignments.AddRange(new RichAlignment?[] { RichAlignment.Center, null });
			var document = new RichDocument();
			document.Blocks.Add(table);

			string written = RichMarkdownWriter.Write(document);
			await Assert.That(written).IsEqualTo("| a\\|b | `c|d` |\n| :---: | --- |\n| x y |  |");

			var read = RichMarkdownParser.Parse(written).Blocks.Single();
			await Assert.That(read.Kind).IsEqualTo(RichBlockKind.Table);
			await Assert.That(((RichRun)read.TableRows[0][0].Inlines.Single()).Text).IsEqualTo("a|b");
		}

		private static RichTableCell Cell(params RichInline[] inlines) => new RichTableCell { Inlines = new List<RichInline>(inlines) };

		private static string WriteCode(string fence, string info, string code)
		{
			var document = new RichDocument();
			document.Blocks.Add(new RichBlock { Kind = RichBlockKind.CodeBlock, CodeFence = fence, CodeInfo = info, CodeText = code, Dirty = true });
			return RichMarkdownWriter.Write(document);
		}

		private static string Html(string markdown) => Regex.Replace(Markdown.ToHtml(markdown, Pipeline), @"\s+", " ").Trim();
	}
}
