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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Markdig.Agg.Editing;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	public class RichMarkdownWriterTests
	{
		// The pipeline the parser reads with, so "renders the same" means the same to the viewer.
		private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();

		/// <summary>
		/// The judge: rewriting every paragraph and heading must not change the rendered HTML. Blocks in an
		/// alignment group are left clean - regenerating a changed group is a later step.
		/// </summary>
		[Test]
		[MethodDataSource(typeof(RichMarkdownParserTests), nameof(RichMarkdownParserTests.Corpus))]
		public async Task RewrittenParagraphsAndHeadingsRenderTheSame(string markdown)
		{
			var document = RichMarkdownParser.Parse(markdown);
			foreach (var block in document.Blocks)
			{
				if ((block.Kind == RichBlockKind.Paragraph || block.Kind == RichBlockKind.Heading) && block.AlignGroup == null)
				{
					block.Dirty = true;
				}
			}

			string rewritten = RichMarkdownWriter.Write(document);
			await Assert.That(Html(rewritten)).IsEqualTo(Html(markdown));
		}

		// Style boundaries that CommonMark's delimiter rules make easy to get wrong.
		public static IEnumerable<string> StyleEdges()
		{
			yield return "*a***b**\n";
			yield return "**a***b*\n";
			yield return "*a*~~b~~**c**\n";
			// A style covering exactly one link writes inside it ([**y**](v)): the model cannot tell that from **[y](v)**, and both look the same.
			yield return "[**x**](u) **[y](v) z** [a *b*](w) *w [c](x)*\n";
			yield return "`a`**b**`c` **`d`**\n";
			yield return "**a *b***c\n";
			yield return "a**b**c *d* e\n";
			yield return "x **\"q\"** y a**\"b\"**c a*`d`*e\n";
			yield return "**^b^** **![i](a.png) text** *text <b>x</b>* [![i](a.png)](u)\n";
			yield return "# Heading with **bold** and `code` #\n";
			yield return "Line one  \n**bold line**\\\n1. item-like\n";
			yield return "[t](</a b> \"ti\\\"tle\") [e](<>) [p](/a\\(b\\))\n";
			yield return "&amp;lt; &#42; \\_ \\\\ \\# and *it\\*s*\n";
		}

		[Test]
		[MethodDataSource(nameof(StyleEdges))]
		public async Task StyleEdgesRenderTheSame(string markdown)
		{
			await RewrittenParagraphsAndHeadingsRenderTheSame(markdown);
		}

		[Test]
		public async Task NestedStylesShareDelimiters()
		{
			await Assert.That(Write(Run("a ", bold: true), Run("b", bold: true, italic: true), Run(" c", bold: true))).IsEqualTo("**a *b* c**");
			await Assert.That(Write(Run("both", bold: true, italic: true))).IsEqualTo("***both***");
			await Assert.That(Write(Run("x"), Run("y", italic: true), Run("z"))).IsEqualTo("x*y*z");
			await Assert.That(Write(Run("gone", strike: true))).IsEqualTo("~~gone~~");
		}

		[Test]
		public async Task WhitespaceMovesOutsideDelimiters()
		{
			await Assert.That(Write(Run("a"), Run(" bold ", bold: true), Run("b"))).IsEqualTo("a **bold** b");
			await Assert.That(Write(Run("a"), Run(" ", bold: true), Run("b"))).IsEqualTo("a b");
			await Assert.That(Write(Run("x "), Run(" it ", italic: true, bold: true))).IsEqualTo("x  ***it*** ");
		}

		[Test]
		public async Task LiteralSyntaxIsEscaped()
		{
			await Assert.That(Write(Run("2 * 3 and [x] `y`"))).IsEqualTo("2 \\* 3 and \\[x\\] \\`y\\`");
			await Assert.That(Write(Run("snake_case _under_"))).IsEqualTo("snake_case \\_under\\_");
			await Assert.That(Write(Run("# not a heading"))).IsEqualTo("\\# not a heading");
			await Assert.That(Write(Run("1. not a list"))).IsEqualTo("1\\. not a list");
			await Assert.That(Write(Run("- not a bullet"))).IsEqualTo("\\- not a bullet");
			await Assert.That(Write(Run("> not a quote"))).IsEqualTo("\\> not a quote");
			await Assert.That(Write(Run("<b>not html</b>"))).IsEqualTo("\\<b>not html\\</b>");
			await Assert.That(Write(Run("&lt; stays text, & too"))).IsEqualTo("\\&lt; stays text, & too");
			await Assert.That(Write(Run("a < b"))).IsEqualTo("a < b");

			// Each of these must read back as the text itself.
			foreach (var text in new[] { "<b>x</b>", "&lt;", "*a* _b_ **c**", "[l](u)", "~s~ ^p^ ==m== ++i++", "http://x.com www.y.com", "a\\b", "C#", "!", "---", "===" })
			{
				await Assert.That(Html(Write(Run(text)))).IsEqualTo(Html(PlainHtmlOf(text)));
			}
		}

		[Test]
		public async Task UnflankedEmphasisFallsBackToTags()
		{
			// a**"b"**c would be literal asterisks: only the span that cannot flank uses a tag.
			await Assert.That(Write(Run("a"), Run("\"b\"", bold: true), Run("c"))).IsEqualTo("a<strong>\"b\"</strong>c");
			await Assert.That(Write(Run("a"), Run("b", bold: true), Run("c"))).IsEqualTo("a**b**c");
			await Assert.That(Write(Run("b", code: true, bold: true), Run("c"))).IsEqualTo("<strong>`b`</strong>c");
		}

		[Test]
		public async Task TableCellsEscapePipesOutsideCode()
		{
			string cell = RichInlineWriter.Write(new RichInline[] { Run("a|b "), Run("c|d", code: true) }, RichInlineContext.TableCell);
			await Assert.That(cell).IsEqualTo("a\\|b `c|d`");
			await Assert.That(Write(Run("a|b"))).IsEqualTo("a|b");
		}

		[Test]
		public async Task LinkUrlAndTitleKeepEntitiesAndLines()
		{
			var link = Run("t", url: "/a&amp;b\nc");
			link.LinkTitle = "x &copy;\ny";
			string written = Write(link);
			await Assert.That(written).IsEqualTo("[t](/a&amp;amp;b%0Ac \"x &amp;copy; y\")");

			var read = (RichRun)RichMarkdownParser.Parse(written).Blocks[0].Inlines[0];
			await Assert.That(read.LinkUrl).IsEqualTo("/a&amp;b%0Ac");
			await Assert.That(read.LinkTitle).IsEqualTo("x &copy; y");
		}

		[Test]
		public async Task MultiLineHeadingContentStaysOneHeading()
		{
			var hardBreak = new InlineAtom(InlineAtomKind.HardBreak, "  \n");
			var html = new InlineAtom(InlineAtomKind.Html, "<span\nclass=\"x\">");
			var cases = new (int Level, RichInline[] Inlines, string Expected)[]
			{
				(3, new RichInline[] { Run("a"), hardBreak, Run("b") }, "### a b"),
				(1, new RichInline[] { Run("a "), html, Run("b") }, "a <span\nclass=\"x\">b\n==="),
				(3, new RichInline[] { Run("a "), html, Run("b") }, "### a <span class=\"x\">b"),
			};
			foreach (var (level, inlines, expected) in cases)
			{
				var document = new RichDocument();
				document.Blocks.Add(new RichBlock { Kind = RichBlockKind.Heading, HeadingLevel = level, Inlines = new List<RichInline>(inlines), Dirty = true });
				string written = RichMarkdownWriter.Write(document);
				await Assert.That(written).IsEqualTo(expected);

				var blocks = RichMarkdownParser.Parse(written).Blocks;
				await Assert.That(blocks.Count).IsEqualTo(1);
				await Assert.That(blocks[0].Kind).IsEqualTo(RichBlockKind.Heading);
				await Assert.That(blocks[0].HeadingLevel).IsEqualTo(level);
			}
		}

		[Test]
		public async Task HeadingsAreAtx()
		{
			var document = RichMarkdownParser.Parse("Title\n=====\n\nText\n");
			document.Blocks[0].Dirty = true;
			await Assert.That(RichMarkdownWriter.Write(document)).IsEqualTo("# Title\n\nText\n");

			document.Blocks[0].Inlines = new List<RichInline> { Run("C #") };
			await Assert.That(RichMarkdownWriter.Write(document)).IsEqualTo("# C \\#\n\nText\n");
		}

		[Test]
		public async Task LinksKeepStyledTextInside()
		{
			var link = Run("see ", url: "http://x.com");
			var boldInLink = Run("this", url: "http://x.com", bold: true);
			await Assert.That(Write(link, boldInLink)).IsEqualTo("[see **this**](http://x.com)");

			var titled = Run("t", url: "a b(c)");
			titled.LinkTitle = "say \"hi\"";
			await Assert.That(Write(titled)).IsEqualTo("[t](<a b(c)> \"say \\\"hi\\\"\")");

			var reference = Run("text", url: "http://docs");
			reference.LinkLabel = "docs";
			await Assert.That(Write(reference)).IsEqualTo("[text][docs]");

			// "!" before a link would make it an image.
			await Assert.That(Write(Run("wow!"), Run("l", url: "u"))).IsEqualTo("wow\\![l](u)");
		}

		[Test]
		public async Task AtomInsideStyleStaysInside()
		{
			var image = new InlineAtom(InlineAtomKind.Image, "![i](a.png)") { Bold = true };
			await Assert.That(Write(Run("a ", bold: true), image, Run(" b", bold: true))).IsEqualTo("**a ![i](a.png) b**");
			await Assert.That(Write(image)).IsEqualTo("**![i](a.png)**");
			await Assert.That(Write(image, Run(" b"))).IsEqualTo("**![i](a.png)** b");

			var hardBreak = new InlineAtom(InlineAtomKind.HardBreak, "  \n");
			await Assert.That(Write(Run("a"), hardBreak, Run("- b"))).IsEqualTo("a  \n\\- b");
		}

		[Test]
		public async Task CodeUsesEnoughBackticks()
		{
			await Assert.That(Write(Run("x", code: true))).IsEqualTo("`x`");
			await Assert.That(Write(Run("a`b", code: true))).IsEqualTo("``a`b``");
			await Assert.That(Write(Run("`x", code: true))).IsEqualTo("`` `x ``");
			await Assert.That(Write(Run(" x ", code: true))).IsEqualTo("`  x  `");
			await Assert.That(Write(Run("*not em*", code: true, bold: true))).IsEqualTo("**`*not em*`**");
		}

		private static string Write(params RichInline[] inlines) => RichInlineWriter.Write(inlines);

		private static RichRun Run(string text, bool bold = false, bool italic = false, bool strike = false, bool code = false, string url = null)
		{
			return new RichRun(text) { Bold = bold, Italic = italic, Strike = strike, Code = code, LinkUrl = url };
		}

		// The HTML a paragraph of exactly this text renders as, built without going through markdown.
		private static string PlainHtmlOf(string text) => "&#" + string.Join(";&#", System.Linq.Enumerable.Select(text, c => ((int)c).ToString())) + ";";

		/// <summary>
		/// Rendered HTML with whitespace runs collapsed: a rewrite joins soft-wrapped lines with a space, which
		/// renders the same as the line break it replaces.
		/// </summary>
		private static string Html(string markdown)
		{
			// A rewrite may write <b> as ** (and ** as <strong> where ** cannot flank): the same rendering.
			string html = Markdown.ToHtml(markdown, Pipeline);
			html = Regex.Replace(html, "<(/?)b>", "<$1strong>");
			html = Regex.Replace(html, "<(/?)i>", "<$1em>");
			html = Regex.Replace(html, "<(/?)(s|strike)>", "<$1del>");
			return Regex.Replace(html, @"\s+", " ").Trim();
		}
	}
}
