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
using System.Threading.Tasks;
using Markdig.Agg.Editing;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	/// <summary>
	/// How the rich editor reads one-line aligned HTML, spaces the end of a list and shades code on any theme.
	/// </summary>
	public class RichEditorPolishTests
	{
		[Test]
		[Arguments("<div align=\"center\">Centered: version 3</div>\n", RichBlockKind.Paragraph, RichAlignment.Center)]
		[Arguments("Intro\n\n<p align='right'> Right **bold** </p>\n\nAfter\n", RichBlockKind.Paragraph, RichAlignment.Right)]
		[Arguments("<h2 align=\"center\">Title</h2>\r\n", RichBlockKind.Heading, RichAlignment.Center)]
		public async Task OneLineAlignedElementIsAnAlignedBlockThatRoundTrips(string markdown, RichBlockKind kind, RichAlignment alignment)
		{
			var document = RichMarkdownParser.Parse(markdown);

			var aligned = document.Blocks.Find(b => b.Alignment == alignment);
			await Assert.That(aligned).IsNotNull();
			await Assert.That(aligned.Kind).IsEqualTo(kind);
			await Assert.That(RichMarkdownWriter.Write(document)).IsEqualTo(markdown);
		}

		[Test]
		public async Task OneLineAlignedHeadingKeepsItsLevelAndText()
		{
			var document = RichMarkdownParser.Parse("<h3 align=\"right\">Lid</h3>\n");

			await Assert.That(document.Blocks.Count).IsEqualTo(1);
			await Assert.That(document.Blocks[0].HeadingLevel).IsEqualTo(3);
			await Assert.That(((RichRun)document.Blocks[0].Inlines[0]).Text).IsEqualTo("Lid");
		}

		[Test]
		[Arguments("left")]
		[Arguments("center")]
		public async Task EnterBeforeAOneLineAlignedHeadingKeepsItAHeading(string alignment)
		{
			// The heading the Enter pushes down is still clean, so it is written from its own source: that source
			// must be its markdown ("## Title"), not the bare text inside the tags.
			var document = RichMarkdownParser.Parse($"<h2 align=\"{alignment}\">Title</h2>\n");
			RichEditOperations.SplitBlock(document, new DocPosition(0, 0));

			var reread = RichMarkdownParser.Parse(RichMarkdownWriter.Write(document));
			var title = reread.Blocks.Find(b => b.Inlines.Count > 0 && ((RichRun)b.Inlines[0]).Text == "Title");
			await Assert.That(title).IsNotNull();
			await Assert.That(title.Kind).IsEqualTo(RichBlockKind.Heading);
			await Assert.That(title.HeadingLevel).IsEqualTo(2);
		}

		[Test]
		public async Task CleanOneLineAlignedHeadingSourceIsItsMarkdown()
		{
			var document = RichMarkdownParser.Parse("<h2 align=\"center\">Title</h2>\n");

			await Assert.That(document.Blocks[0].OriginalSource).IsEqualTo("## Title");
		}

		[Test]
		public async Task EditedOneLineAlignedElementIsWrittenInTheWrapperForm()
		{
			var document = RichMarkdownParser.Parse("<div align=\"center\">Old</div>\n");
			var block = document.Blocks[0];
			block.Inlines = new List<RichInline> { new RichRun("New") };
			block.Dirty = true;

			var written = RichMarkdownWriter.Write(document);
			await Assert.That(written).IsEqualTo("<div align=\"center\">\n\nNew\n\n</div>\n");

			// And the written form reads back as the same centered paragraph.
			var reread = RichMarkdownParser.Parse(written);
			await Assert.That(reread.Blocks.Count).IsEqualTo(1);
			await Assert.That(reread.Blocks[0].Alignment).IsEqualTo(RichAlignment.Center);
		}

		[Test]
		[Arguments("<div align=\"center\"><div>nested</div></div>\n")]
		[Arguments("<div align=\"center\" class=\"x\">extra attribute</div>\n")]
		[Arguments("<div align=\"center\"># not a paragraph</div>\n")]
		public async Task OtherHtmlStaysRaw(string markdown)
		{
			var document = RichMarkdownParser.Parse(markdown);

			await Assert.That(document.Blocks[0].Kind).IsEqualTo(RichBlockKind.Raw);
			await Assert.That(RichMarkdownWriter.Write(document)).IsEqualTo(markdown);
		}

		[Test]
		public async Task ListEndTakesParagraphSpacing()
		{
			var document = RichMarkdownParser.Parse("- a\n- b\n\n1. c\n\nAfter\n");
			var blocks = document.Blocks;
			var style = new RichLayoutStyle(deviceScale: 1);

			await Assert.That(blocks.Count).IsEqualTo(4);
			await Assert.That(EndsList(blocks, 0)).IsFalse();
			await Assert.That(EndsList(blocks, 1)).IsTrue().Because("a bullet list straight into a numbered one needs a gap");
			await Assert.That(EndsList(blocks, 2)).IsTrue().Because("a list straight into a paragraph needs a gap");
			await Assert.That(EndsList(blocks, 3)).IsFalse();

			double inside = RichBlockLayout.Layout(blocks[0], 400, style).Height;
			double last = RichBlockLayout.Layout(blocks[1], 400, style, endsList: true).Height;
			await Assert.That(last - inside).IsEqualTo(style.BlockSpacing(blocks[3]).After - style.BlockSpacing(blocks[0]).After).Within(0.001);
			await Assert.That(style.BlockSpacing(blocks[1], endsList: true).After).IsEqualTo(style.BlockSpacing(blocks[3]).After);
		}

		[Test]
		public async Task NestedItemBeforeItsParentsSiblingDoesNotEndTheList()
		{
			var blocks = RichMarkdownParser.Parse("- a\n  - b\n- c\n").Blocks;

			await Assert.That(EndsList(blocks, 1)).IsFalse();
			await Assert.That(EndsList(blocks, 2)).IsTrue();
		}

		[Test]
		public async Task CodeAndStripeShadingShowOnLightAndDarkThemes()
		{
			foreach (var (text, background) in new[] { (new Color("#333"), Color.White), (new Color("#ddd"), new Color("#333")) })
			{
				var style = new RichLayoutStyle(new ThemeConfig { TextColor = text, BackgroundColor = background }, deviceScale: 1);
				foreach (var shade in new[] { style.CodeBackgroundColor, style.TableStripeColor })
				{
					var shown = background.Blend(new Color(shade, 255), shade.Alpha0To255 / 255.0);
					int difference = System.Math.Abs(shown.Red0To255 - background.Red0To255);
					await Assert.That(difference).IsGreaterThanOrEqualTo(10).Because("shading a reader cannot see marks nothing");
				}

				await Assert.That(ThemeConfig.ContrastRatio(style.LinkColor, background)).IsGreaterThanOrEqualTo(4.4);
			}
		}

		private static bool EndsList(List<RichBlock> blocks, int index) => RichDocumentView.EndsList(blocks, index);
	}
}
