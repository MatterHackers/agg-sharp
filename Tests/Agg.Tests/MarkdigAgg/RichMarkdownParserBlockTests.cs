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

using System.Linq;
using System.Threading.Tasks;
using Markdig.Agg.Editing;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	/// <summary>
	/// Block shapes for lists, quotes and &lt;div align&gt; groups. Their byte-identical round trips are in
	/// <see cref="RichMarkdownParserTests.Corpus"/>.
	/// </summary>
	public class RichMarkdownParserBlockTests
	{
		[Test]
		public async Task NestedListItemsFlattenWithDepth()
		{
			var document = RichMarkdownParser.Parse("- a\n  - b\n    - c\n  - d\n- e\n");
			await Assert.That(Shape(document)).IsEqualTo("-0:- a|-1:- b|-2:- c|-1:- d|-0:- e");
			await Assert.That(document.Blocks[1].SeparatorBefore).IsEqualTo("\n  ");
			await Assert.That(document.Blocks[2].SeparatorBefore).IsEqualTo("\n    ");
			await Assert.That(PlainText(document.Blocks[2])).IsEqualTo("c");
			await Assert.That(document.Blocks.All(b => !b.List.Ordered)).IsTrue();
		}

		[Test]
		public async Task OrderedListsKeepStartAndDelimiter()
		{
			var document = RichMarkdownParser.Parse("3. three\n4. four\n\n10) ten\n11) eleven\n");
			await Assert.That(Shape(document)).IsEqualTo(".0:3. three|.0:4. four|)0:10) ten|)0:11) eleven");
			await Assert.That(document.Blocks.All(b => b.List.Ordered)).IsTrue();
			await Assert.That(document.Blocks[0].List.StartNumber).IsEqualTo(3);
			await Assert.That(document.Blocks[1].List.StartNumber).IsEqualTo(3);
			await Assert.That(document.Blocks[2].List.StartNumber).IsEqualTo(10);
			await Assert.That(document.Blocks[3].List.StartNumber).IsEqualTo(10);
			await Assert.That(PlainText(document.Blocks[3])).IsEqualTo("eleven");
		}

		[Test]
		public async Task BulletMarkersAndLooseItemsAreKept()
		{
			var bullets = RichMarkdownParser.Parse("* star\n* star two\n\n+ plus\n+ plus two\r\n");
			await Assert.That(Shape(bullets)).IsEqualTo("*0:* star|*0:* star two|+0:+ plus|+0:+ plus two");

			var loose = RichMarkdownParser.Parse("- loose\n\n- items\n\n\n- far apart\n");
			await Assert.That(Shape(loose)).IsEqualTo("-0:- loose|-0:- items|-0:- far apart");
			await Assert.That(loose.Blocks[2].SeparatorBefore).IsEqualTo("\n\n\n");
		}

		[Test]
		public async Task TaskCheckboxIsARawAtomAndWrappedLinesStayInTheItem()
		{
			var document = RichMarkdownParser.Parse("- [ ] task\n- [x] done\n  wrapped line\n");
			await Assert.That(Shape(document)).IsEqualTo("-0:- [ ] task|-0:- [x] done\n  wrapped line");
			var atom = (InlineAtom)document.Blocks[1].Inlines[0];
			await Assert.That(atom.Kind).IsEqualTo(InlineAtomKind.Raw);
			await Assert.That(atom.RawMarkdown).IsEqualTo("[x]");
			await Assert.That(PlainText(document.Blocks[1])).IsEqualTo("? done wrapped line");
		}

		[Test]
		public async Task ListWithAnUnmodelledItemIsOneRawBlock()
		{
			var paragraphs = RichMarkdownParser.Parse("- one para\n\n  second para\n- next\n");
			await Assert.That(Shape(paragraphs)).IsEqualTo("Raw:- one para\n\n  second para\n- next");

			var code = RichMarkdownParser.Parse("Intro\n\n- item\n\n  ```\n  code\n  ```\n");
			await Assert.That(Shape(code)).IsEqualTo("Paragraph:Intro|Raw:- item\n\n  ```\n  code\n  ```");
		}

		[Test]
		public async Task QuoteParagraphsAreQuoteBlocks()
		{
			var single = RichMarkdownParser.Parse("> one para\n> two lines\n");
			await Assert.That(Shape(single)).IsEqualTo("Quote:> one para\n> two lines");
			await Assert.That(PlainText(single.Blocks[0])).IsEqualTo("one para two lines");

			// The '>' line between paragraphs starts the second block, so the separator is a bare line break.
			var two = RichMarkdownParser.Parse("> first\n>\n> second\n>\n");
			await Assert.That(Shape(two)).IsEqualTo("Quote:> first|Quote:>\n> second\n>");
			await Assert.That(two.Blocks[1].SeparatorBefore).IsEqualTo("\n");
			await Assert.That(PlainText(two.Blocks[1])).IsEqualTo("second");

			var indented = RichMarkdownParser.Parse("  > indented\r\n  >\r\n  > quote\r\n");
			await Assert.That(Shape(indented)).IsEqualTo("Quote:> indented|Quote:  >\r\n  > quote");
			await Assert.That(indented.Blocks[0].SeparatorBefore).IsEqualTo("  ");
			await Assert.That(indented.Blocks[1].SeparatorBefore).IsEqualTo("\r\n");
		}

		[Test]
		public async Task NestedQuoteIsRaw()
		{
			var document = RichMarkdownParser.Parse("> outer\n>\n> > nested\n");
			await Assert.That(Shape(document)).IsEqualTo("Raw:> outer\n>\n> > nested");
		}

		[Test]
		public async Task AlignGroupFoldsIntoAlignmentAndKeepsItsWrapperBytes()
		{
			var document = RichMarkdownParser.Parse("<div align=\"center\">\n\n# Title\n\nCentered **text**\n\nMore\n\n</div>\n\nAfter\n");
			await Assert.That(Shape(document)).IsEqualTo("Heading:# Title|Paragraph:Centered **text**|Paragraph:More|Paragraph:After");
			await Assert.That(string.Join(",", document.Blocks.Select(b => b.Alignment)))
				.IsEqualTo("Center,Center,Center,Left");
			var group = document.Blocks[0].AlignGroup;
			await Assert.That(group).IsNotNull();
			await Assert.That(document.Blocks[1].AlignGroup).IsSameReferenceAs(group);
			await Assert.That(document.Blocks[2].AlignGroup).IsSameReferenceAs(group);
			await Assert.That(document.Blocks[3].AlignGroup).IsNull();
			await Assert.That(group.Alignment).IsEqualTo(RichAlignment.Center);
			await Assert.That(group.OriginalMemberCount).IsEqualTo(3);
			await Assert.That(group.OpenSource).IsEqualTo("<div align=\"center\">\n\n");
			await Assert.That(group.CloseSource).IsEqualTo("\n\n</div>");
			await Assert.That(document.Blocks[0].SeparatorBefore).IsEqualTo("");
			await Assert.That(document.Blocks[3].SeparatorBefore).IsEqualTo("\n\n");

			var right = RichMarkdownParser.Parse("<div align=\"right\">\r\n\r\nRight\r\n\r\n</div>");
			await Assert.That(Shape(right)).IsEqualTo("Paragraph:Right");
			await Assert.That(right.Blocks[0].Alignment).IsEqualTo(RichAlignment.Right);
			await Assert.That(right.Blocks[0].AlignGroup.OpenSource).IsEqualTo("<div align=\"right\">\r\n\r\n");
			await Assert.That(right.Blocks[0].AlignGroup.CloseSource).IsEqualTo("\r\n\r\n</div>");
		}

		[Test]
		public async Task UnclosedNestedOrNonTextAlignGroupsStayRaw()
		{
			var unclosed = RichMarkdownParser.Parse("<div align=\"center\">\n\nNever closed\n");
			await Assert.That(Shape(unclosed)).IsEqualTo("Raw:<div align=\"center\">|Paragraph:Never closed");
			await Assert.That(unclosed.Blocks[1].Alignment).IsEqualTo(RichAlignment.Left);

			var nested = RichMarkdownParser.Parse("<div>\n\n<div align=\"center\">\n\nNested\n\n</div>\n\n</div>\n");
			await Assert.That(Shape(nested)).IsEqualTo("Raw:<div>|Raw:<div align=\"center\">|Paragraph:Nested|Raw:</div>|Raw:</div>");

			var list = RichMarkdownParser.Parse("<div align=\"center\">\n\n- list\n\n</div>\n");
			await Assert.That(Shape(list)).IsEqualTo("Raw:<div align=\"center\">|-0:- list|Raw:</div>");
		}

		[Test]
		public async Task IndentedCodeThatLooksLikeAnAlignTagIsNotAGroup()
		{
			var document = RichMarkdownParser.Parse("Intro\n\n    <div align=\"center\">\n\nX\n\n    </div>\n");
			await Assert.That(Shape(document)).IsEqualTo("Paragraph:Intro|Raw:    <div align=\"center\">\n|Paragraph:X|Raw:    </div>");
			await Assert.That(document.Blocks[2].Alignment).IsEqualTo(RichAlignment.Left);
			await Assert.That(document.Blocks[2].AlignGroup).IsNull();
		}

		[Test]
		public async Task GroupsAreExplicitObjects()
		{
			var aligns = RichMarkdownParser.Parse("<div align=\"center\">\n\nA\n\n</div>\n\n<div align=\"left\">\n\nB\n\n</div>\n");
			await Assert.That(Shape(aligns)).IsEqualTo("Paragraph:A|Paragraph:B");
			await Assert.That(aligns.Blocks[0].AlignGroup).IsNotSameReferenceAs(aligns.Blocks[1].AlignGroup);
			await Assert.That(aligns.Blocks[1].AlignGroup.Alignment).IsEqualTo(RichAlignment.Left);
			await Assert.That(RichMarkdownWriter.Write(aligns)).IsEqualTo("<div align=\"center\">\n\nA\n\n</div>\n\n<div align=\"left\">\n\nB\n\n</div>\n");

			var separate = RichMarkdownParser.Parse("> a\n\n> b");
			await Assert.That(Shape(separate)).IsEqualTo("Quote:> a|Quote:> b");
			await Assert.That(separate.Blocks[0].QuoteGroup).IsNotSameReferenceAs(separate.Blocks[1].QuoteGroup);

			var one = RichMarkdownParser.Parse("> a\n>\n> b");
			await Assert.That(one.Blocks[0].QuoteGroup).IsSameReferenceAs(one.Blocks[1].QuoteGroup);
			await Assert.That(one.Blocks[0].QuoteGroup.OriginalMemberCount).IsEqualTo(2);

			var lists = RichMarkdownParser.Parse("- a\n* b\n+ c");
			await Assert.That(Shape(lists)).IsEqualTo("-0:- a|*0:* b|+0:+ c");
			await Assert.That(lists.Blocks.Select(b => b.ListGroup).Distinct().Count()).IsEqualTo(3);

			var nested = RichMarkdownParser.Parse("- a\n  - b\n- c\n");
			await Assert.That(nested.Blocks.Select(b => b.ListGroup).Distinct().Count()).IsEqualTo(1);
			await Assert.That(nested.Blocks[0].ListGroup.OriginalMemberCount).IsEqualTo(3);
		}

		[Test]
		public async Task DocumentCloneKeepsGroupSharingButNotIdentity()
		{
			var document = RichMarkdownParser.Parse("<div align=\"center\">\n\nA\n\nB\n\n</div>\n\n> q\n>\n> r\n\n- x\n  - y\n");
			var copy = document.Clone();
			for (int i = 0; i < document.Blocks.Count; i++)
			{
				var original = document.Blocks[i];
				var cloned = copy.Blocks[i];
				await Assert.That(original.AlignGroup == null || original.AlignGroup != cloned.AlignGroup).IsTrue();
				await Assert.That(original.QuoteGroup == null || original.QuoteGroup != cloned.QuoteGroup).IsTrue();
				await Assert.That(original.ListGroup == null || original.ListGroup != cloned.ListGroup).IsTrue();
			}

			await Assert.That(copy.Blocks[1].AlignGroup).IsSameReferenceAs(copy.Blocks[0].AlignGroup);
			await Assert.That(copy.Blocks[3].QuoteGroup).IsSameReferenceAs(copy.Blocks[2].QuoteGroup);
			await Assert.That(copy.Blocks[5].ListGroup).IsSameReferenceAs(copy.Blocks[4].ListGroup);

			copy.Blocks[0].AlignGroup.OpenSource = "changed";
			await Assert.That(document.Blocks[0].AlignGroup.OpenSource).IsEqualTo("<div align=\"center\">\n\n");
			await Assert.That(RichMarkdownWriter.Write(document)).IsEqualTo(RichMarkdownWriter.Write(document.Clone()));

			// RichBlock.Clone alone keeps the block in its groups.
			await Assert.That(document.Blocks[0].Clone().AlignGroup).IsSameReferenceAs(document.Blocks[0].AlignGroup);
		}

		[Test]
		public async Task ChangedGroupIsNotWrittenFromOriginalBytes()
		{
			// Regenerating a changed group is the writer step; until then it must not silently write stale bytes.
			var removed = RichMarkdownParser.Parse("- a\n- b\n");
			removed.Blocks.RemoveAt(1);
			await Assert.That(() => RichMarkdownWriter.Write(removed)).Throws<System.NotImplementedException>();

			var realigned = RichMarkdownParser.Parse("<div align=\"center\">\n\nA\n\n</div>\n");
			realigned.Blocks[0].Alignment = RichAlignment.Right;
			await Assert.That(() => RichMarkdownWriter.Write(realigned)).Throws<System.NotImplementedException>();
		}

		// List items show as marker + depth (e.g. "-1", ".0"); other blocks as their kind.
		private static string Shape(RichDocument document)
		{
			return string.Join("|", document.Blocks.Select(b =>
				(b.Kind == RichBlockKind.ListItem ? $"{b.List.Marker}{b.List.Depth}" : b.Kind.ToString()) + ":" + b.OriginalSource));
		}

		private static string PlainText(RichBlock block)
		{
			return string.Concat(block.Inlines.Select(inline => inline is RichRun run ? run.Text : "?"));
		}
	}
}
