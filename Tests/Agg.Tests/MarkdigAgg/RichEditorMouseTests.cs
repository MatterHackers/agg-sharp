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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	/// <summary>
	/// Mouse events sent through a container to a <see cref="RichMarkdownEditWidget"/> at points computed from its
	/// layouts (caret boxes placed by <see cref="RichMarkdownEditWidget.BlockOrigin"/>).
	/// </summary>
	public class RichEditorMouseTests
	{
		private sealed class Harness
		{
			public GuiWidget Container;
			public RichMarkdownEditWidget Editor;
			public HashSet<Keys> Held = new HashSet<Keys>();

			// A point in the container for a caret, at the middle of its caret box.
			public Vector2 At(int block, RichCaret caret)
			{
				var box = Editor.BlockLayout(block).CaretRect(caret);
				return ToContainer(new Vector2(box.Center.X, box.Center.Y + Editor.BlockOrigin(block)));
			}

			public Vector2 ToContainer(Vector2 inView) => Editor.DocumentView.TransformToParentSpace(Container, inView);

			public void Down(Vector2 point, int clicks = 1) => Container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, clicks, point.X, point.Y, 0));

			public void Move(Vector2 point) => Container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, point.X, point.Y, 0));

			public void Up(Vector2 point) => Container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));

			public void Click(Vector2 point, int clicks = 1)
			{
				Down(point, clicks);
				Up(point);
			}
		}

		private static Harness Make(string markdown)
		{
			var container = new GuiWidget(400, 300, SizeLimitsToSet.None)
			{
				DoubleBuffer = true,
			};
			var editor = new RichMarkdownEditWidget(new ThemeConfig())
			{
				Markdown = markdown,
			};
			container.AddChild(editor);
			container.PerformLayout();
			container.OnDraw(container.BackBuffer.NewGraphics2D());
			var harness = new Harness { Container = container, Editor = editor };
			editor.MouseKeyState = key => harness.Held.Contains(key);
			return harness;
		}

		[Test]
		public async Task AClickPlacesTheCaretAndTellsTheEditor()
		{
			var h = Make("Hello world here\n");
			int userChanges = 0;
			h.Editor.SelectionChangedByUser += (s, e) => userChanges++;

			h.Click(h.At(0, new RichCaret(6)));

			await Assert.That(h.Editor.Selection).IsEqualTo(RichSelection.At(new DocPosition(0, 6)));
			await Assert.That(h.Editor.ContainsFocus).IsTrue();
			await Assert.That(userChanges).IsEqualTo(1);
		}

		[Test]
		public async Task AClickRightOfAWordBrokenLineKeepsTheCaretAtTheLineEnd()
		{
			var h = Make(new string('W', 120) + "\n");
			var layout = (RichBlockLayout)h.Editor.BlockLayout(0);
			int end = layout.Lines[1].Start;

			h.Click(h.At(0, new RichCaret(end, AtLineEnd: true)));

			await Assert.That(h.Editor.Selection).IsEqualTo(RichSelection.At(new DocPosition(0, end)));
			await Assert.That(h.Editor.CaretAtLineEnd).IsTrue();
		}

		[Test]
		public async Task ADragSelectsAcrossTwoBlocks()
		{
			var h = Make("First block\n\nSecond block\n");
			var end = h.At(1, new RichCaret(3));

			h.Down(h.At(0, new RichCaret(2)));
			h.Move(end);
			h.Up(end);

			await Assert.That(h.Editor.Selection).IsEqualTo(new RichSelection(new DocPosition(0, 2), new DocPosition(1, 3)));
		}

		[Test]
		public async Task AShiftClickExtendsFromTheAnchor()
		{
			var h = Make("First block\n\nSecond block\n");
			h.Click(h.At(0, new RichCaret(2)));

			h.Held.Add(Keys.Shift);
			h.Click(h.At(1, new RichCaret(1)));

			await Assert.That(h.Editor.Selection).IsEqualTo(new RichSelection(new DocPosition(0, 2), new DocPosition(1, 1)));
		}

		[Test]
		public async Task ADoubleClickSelectsAWord()
		{
			var h = Make("Hello world here\n");

			h.Click(h.At(0, new RichCaret(8)), clicks: 2);

			await Assert.That(h.Editor.Selection).IsEqualTo(new RichSelection(new DocPosition(0, 6), new DocPosition(0, 11)));
		}

		[Test]
		public async Task ADoubleClickSelectsAWordInCode()
		{
			var h = Make("```\nvar some_name = 1;\n```\n");

			h.Click(h.At(0, new RichCaret(6)), clicks: 2);

			await Assert.That(h.Editor.Selection).IsEqualTo(new RichSelection(new DocPosition(0, 4), new DocPosition(0, 13)));
		}

		[Test]
		public async Task ATripleClickSelectsTheBlocksText()
		{
			var h = Make("First block\n\nSecond block\n");

			h.Click(h.At(1, new RichCaret(4)), clicks: 3);

			await Assert.That(h.Editor.Selection).IsEqualTo(new RichSelection(new DocPosition(1, 0), new DocPosition(1, 12)));
		}

		[Test]
		public async Task ClicksInATableCellStayInThatCell()
		{
			var h = Make("| a | b |\n|---|---|\n| one | three |\n");

			h.Click(h.At(0, new RichCaret(2, Row: 1, Column: 1)));
			await Assert.That(h.Editor.Selection).IsEqualTo(RichSelection.At(new DocPosition(0, 2, 1, 1)));

			h.Click(h.At(0, new RichCaret(2, Row: 1, Column: 1)), clicks: 3);
			await Assert.That(h.Editor.Selection).IsEqualTo(new RichSelection(new DocPosition(0, 0, 1, 1), new DocPosition(0, 5, 1, 1)));
		}

		[Test]
		public async Task AClickBetweenOrBelowBlocksGoesToTheNearestBlock()
		{
			var h = Make("First block\n\nSecond block\n");

			// The spacing under the first block's text is still the first block: its caret goes on its last line.
			var firstBounds = h.Editor.BlockBounds(0);
			h.Click(h.ToContainer(new Vector2(firstBounds.Right - 1, firstBounds.Bottom + 0.5)));
			await Assert.That(h.Editor.Selection).IsEqualTo(RichSelection.At(new DocPosition(0, 11)));

			// The document is shorter than the view: a click under it goes to the end of the last block.
			h.Click(new Vector2(350, 2));
			await Assert.That(h.Editor.Selection).IsEqualTo(RichSelection.At(new DocPosition(1, 12)));
		}

		[Test]
		public async Task AClickOnARawBlockSelectsItWhole()
		{
			var h = Make("Before\n\n***\n\nAfter\n");
			int? activated = null;
			h.Editor.RawBlockActivated += offset => activated = offset;

			h.Click(h.ToContainer(h.Editor.BlockBounds(1).Center));

			await Assert.That(h.Editor.Selection).IsEqualTo(RichEditOperations.WholeBlock(h.Editor.Document, 1));
			await Assert.That(activated).IsNull();
		}

		[Test]
		public async Task ADoubleClickOnARawBlockRaisesWhereItsSourceStarts()
		{
			string markdown = "Before\n\n***\n\nAfter\n";
			var h = Make(markdown);
			int? activated = null;
			h.Editor.RawBlockActivated += offset => activated = offset;

			h.Click(h.ToContainer(h.Editor.BlockBounds(1).Center), clicks: 2);

			await Assert.That(activated).IsEqualTo(markdown.IndexOf("***"));
			await Assert.That(h.Editor.Selection.WholeBlock).IsTrue();
		}

		[Test]
		public async Task TheSourceOffsetCountsAnEditedBlockBeforeIt()
		{
			var h = Make("Before\n\n***\n\nAfter\n");
			RichEditOperations.InsertText(h.Editor.Document, new DocPosition(0, 0), "Edited ");
			h.Editor.Relayout(0);

			int offset = RichMarkdownWriter.SourceOffsetOf(h.Editor.Document, 1);

			await Assert.That(h.Editor.Markdown.Substring(offset)).StartsWith("***");
		}

		[Test]
		public async Task OnlyAModifierClickOnALinkRaisesLinkClicked()
		{
			var h = Make("Go [there](https://example.test/x) now\n");
			var urls = new List<string>();
			h.Editor.LinkClicked += url => urls.Add(url);
			var onLink = h.At(0, new RichCaret(5));

			h.Click(onLink);
			await Assert.That(urls.Count).IsEqualTo(0);
			await Assert.That(h.Editor.Selection).IsEqualTo(RichSelection.At(new DocPosition(0, 5)));

			h.Held.Add(Keys.Control);
			h.Click(onLink);
			await Assert.That(urls).IsEquivalentTo(new[] { "https://example.test/x" }, CollectionOrdering.Matching);

			// Off the link, a modifier click is an ordinary click.
			h.Click(h.At(0, new RichCaret(1)));
			await Assert.That(urls.Count).IsEqualTo(1);
		}

		[Test]
		public async Task AWindowsKeyClickOnALinkOnlyPlacesTheCaret()
		{
			// Every host reports Command as Control; a Windows key can stay latched on X11 and in the browser.
			var h = Make("Go [there](https://example.test/x) now\n");
			var urls = new List<string>();
			h.Editor.LinkClicked += url => urls.Add(url);
			h.Held.Add(Keys.LWin);

			h.Click(h.At(0, new RichCaret(5)));

			await Assert.That(urls.Count).IsEqualTo(0);
			await Assert.That(h.Editor.Selection).IsEqualTo(RichSelection.At(new DocPosition(0, 5)));
		}

		[Test]
		public async Task AModifierClickOnALinkInARawBlockRaisesLinkClicked()
		{
			var h = Make("Before\n\n> > see [here](https://raw.test/y)\n\nAfter\n");
			await Assert.That(h.Editor.Document.Blocks[1].Kind).IsEqualTo(RichBlockKind.Raw);
			var urls = new List<string>();
			h.Editor.LinkClicked += url => urls.Add(url);
			var link = h.Editor.RawBlockWidget(1).DescendantsAndSelf<Markdig.Renderers.Agg.Inlines.TextLinkX>().Single();
			var onLink = h.ToContainer(link.TransformToParentSpace(h.Editor.DocumentView, link.LocalBounds.Center));

			h.Click(onLink);
			await Assert.That(urls.Count).IsEqualTo(0);
			await Assert.That(h.Editor.Selection).IsEqualTo(RichEditOperations.WholeBlock(h.Editor.Document, 1));

			h.Held.Add(Keys.Control);
			h.Click(onLink);
			await Assert.That(urls).IsEquivalentTo(new[] { "https://raw.test/y" }, CollectionOrdering.Matching);
		}

		[Test]
		public async Task ADragAfterADoubleClickGrowsByWholeWordsKeepingTheFirst()
		{
			var h = Make("alpha beta gamma delta\n");

			h.Down(h.At(0, new RichCaret(8)), clicks: 2);
			h.Move(h.At(0, new RichCaret(13)));
			await Assert.That(h.Editor.Selection).IsEqualTo(new RichSelection(new DocPosition(0, 6), new DocPosition(0, 16)));

			var back = h.At(0, new RichCaret(2));
			h.Move(back);
			h.Up(back);
			await Assert.That(h.Editor.Selection).IsEqualTo(new RichSelection(new DocPosition(0, 10), new DocPosition(0, 0)));
		}

		[Test]
		public async Task ADragAfterATripleClickGrowsByWholeBlocks()
		{
			var h = Make("First block\n\nSecond block\n");

			h.Down(h.At(1, new RichCaret(4)), clicks: 3);
			var target = h.At(0, new RichCaret(2));
			h.Move(target);
			h.Up(target);

			await Assert.That(h.Editor.Selection).IsEqualTo(new RichSelection(new DocPosition(1, 12), new DocPosition(0, 0)));
		}

		[Test]
		public async Task AShiftClickOnARawBlockExtendsFromTheAnchor()
		{
			var h = Make("Before\n\n***\n\nAfter\n");
			h.Click(h.At(0, new RichCaret(2)));

			h.Held.Add(Keys.Shift);
			h.Click(h.ToContainer(h.Editor.BlockBounds(1).Center));

			await Assert.That(h.Editor.Selection).IsEqualTo(new RichSelection(new DocPosition(0, 2), new DocPosition(1, 1)));
		}
	}
}
