/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Markdig.Agg.Editing;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	public class RichMarkdownEditWidgetTests
	{
		private static (GuiWidget Container, RichMarkdownEditWidget Editor) Make(string markdown, double width = 400, double height = 300)
		{
			var container = new GuiWidget(width, height, SizeLimitsToSet.None)
			{
				DoubleBuffer = true,
			};
			var editor = new RichMarkdownEditWidget(new ThemeConfig())
			{
				Markdown = markdown,
			};
			container.AddChild(editor);
			container.PerformLayout();
			return (container, editor);
		}

		private static void Draw(GuiWidget container)
		{
			container.BackBuffer.NewGraphics2D().Clear(Color.White);
			container.OnDraw(container.BackBuffer.NewGraphics2D());
		}

		private static string Paragraphs(int count)
		{
			var text = new StringBuilder();
			for (int i = 0; i < count; i++)
			{
				text.Append($"Paragraph number {i} with a few words.\n\n");
			}

			return text.ToString();
		}

		[Test]
		public async Task DrawsTheDocument()
		{
			var (container, _) = Make("# Title\n\nSome **bold** text and `code`.\n\n- one\n- two\n\n> quoted\n");
			Draw(container);

			var buffer = container.BackBuffer;
			bool anyInk = false;
			for (int y = 0; y < buffer.Height && !anyInk; y++)
			{
				for (int x = 0; x < buffer.Width; x++)
				{
					if (buffer.GetPixel(x, y) != Color.White)
					{
						anyInk = true;
						break;
					}
				}
			}

			await Assert.That(anyInk).IsTrue();
		}

		[Test]
		public async Task AnEditReLaysOutOnlyTheEditedBlock()
		{
			var (container, editor) = Make(Paragraphs(6));
			Draw(container);
			int before = editor.LayoutCount;

			RichEditOperations.InsertText(editor.Document, new DocPosition(3, 0), "Edited ");
			editor.Relayout(3);
			Draw(container);

			await Assert.That(editor.LayoutCount - before).IsEqualTo(1);
		}

		[Test]
		public async Task AWidthChangeReLaysOutEveryBlock()
		{
			var (container, editor) = Make(Paragraphs(6));
			Draw(container);
			int before = editor.LayoutCount;

			container.Width = 250;
			container.PerformLayout();
			Draw(container);

			await Assert.That(editor.LayoutCount - before).IsEqualTo(editor.Document.Blocks.Count);
		}

		[Test]
		public async Task AnUntouchedDocumentReadsBackByteIdentical()
		{
			string source = "---\ntitle: x\n---\n# Heading\n\nText with *style*  and ![img](a.png).\n\n1. one\n2. two\n\n> quote\n>\n> more\n\n```cs\ncode();\n```\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n***\n";
			var (container, editor) = Make(source);
			Draw(container);

			await Assert.That(editor.Markdown).IsEqualTo(source);
		}

		[Test]
		public async Task AnEmptyDocumentIsOneEmptyParagraphThatWritesNothing()
		{
			var (container, editor) = Make("");
			editor.EmptyHint = "No notes yet...";
			Draw(container);

			await Assert.That(editor.IsEmpty).IsTrue();
			await Assert.That(editor.Markdown).IsEqualTo("");
		}

		[Test]
		public async Task AMultiParagraphQuoteHasNoGapBetweenItsParagraphs()
		{
			var (container, editor) = Make("> one\n>\n> two\n");
			Draw(container);

			await Assert.That(editor.Document.Blocks.Count).IsEqualTo(2);
			await Assert.That(editor.BlockBounds(0).Bottom).IsEqualTo(editor.BlockBounds(1).Top);

			// The bars of the two paragraphs meet, so they read as one bar.
			var first = (RichBlockLayout)editor.BlockLayout(0);
			var second = (RichBlockLayout)editor.BlockLayout(1);
			double firstBarBottom = editor.BlockBounds(0).Top - first.Height + first.QuoteBar.Value.Bottom;
			double secondBarTop = editor.BlockBounds(1).Top - second.Height + second.QuoteBar.Value.Top;
			await Assert.That(firstBarBottom).IsEqualTo(secondBarTop).Within(0.001);
		}

		[Test]
		public async Task ARawBlockIsHostedAsAChildAtItsBlock()
		{
			var (container, editor) = Make("Before\n\n***\n\nAfter\n");
			Draw(container);

			await Assert.That(editor.Document.Blocks[1].Kind).IsEqualTo(RichBlockKind.Raw);
			var host = editor.RawBlockWidget(1);
			await Assert.That(host).IsNotNull();
			await Assert.That(host.Parent).IsEqualTo(editor.DocumentView);

			var bounds = editor.BlockBounds(1);
			await Assert.That(host.Position.Y).IsGreaterThanOrEqualTo(bounds.Bottom);
			await Assert.That(host.Position.Y + host.Height).IsLessThanOrEqualTo(bounds.Top + 0.001);
			await Assert.That(editor.RawBlockWidget(0)).IsNull();
		}

		[Test]
		public async Task ATableDrawsItsCellsAndCaretsRoundTripThroughTheWidget()
		{
			var (container, editor) = Make("| a | b |\n|---|---|\n| one | two |\n| three | four |\n");
			Draw(container);

			var table = editor.BlockLayout(0) as RichTableLayout;
			await Assert.That(table).IsNotNull();
			await Assert.That(table.Rows.Count).IsEqualTo(3);
			await Assert.That(editor.RawBlockWidget(0)).IsNull();

			// The cell "four" has ink inside its box once drawn.
			double origin = editor.BlockOrigin(0);
			var cell = table.Cell(2, 1).Rect;
			var cellInView = new RectangleDouble(cell.Left, cell.Bottom + origin, cell.Right, cell.Top + origin);
			var cellInContainer = editor.DocumentView.TransformToParentSpace(container, cellInView);
			int ink = 0;
			for (int y = (int)cellInContainer.Bottom + 2; y < (int)cellInContainer.Top - 1; y++)
			{
				for (int x = (int)cellInContainer.Left + 2; x < (int)cellInContainer.Right - 1; x++)
				{
					if (container.BackBuffer.GetPixel(x, y) != Color.White)
					{
						ink++;
					}
				}
			}

			await Assert.That(ink).IsGreaterThan(0);

			// A caret in a cell, placed in the widget by the block's origin, hit-tests back to the same caret.
			var caret = new RichCaret(2, Row: 2, Column: 1);
			var caretRect = table.CaretRect(caret);
			var inView = new Vector2(caretRect.Center.X, caretRect.Center.Y + origin);
			await Assert.That(editor.BlockBounds(0).Contains(inView)).IsTrue();
			await Assert.That(table.HitTest(new Vector2(inView.X, inView.Y - origin))).IsEqualTo(caret);
		}

		private const string Wrapping = "Typing many words into a block so that it wraps onto several more lines ";

		[Test]
		public async Task GrowingTheFirstBlockKeepsTheViewAtTheTop()
		{
			var (container, editor) = Make(Paragraphs(60), 400, 200);
			Draw(container);

			for (int i = 0; i < 5; i++)
			{
				RichEditOperations.InsertText(editor.Document, new DocPosition(0, 0), Wrapping);
				editor.Relayout(0);
				Draw(container);
			}

			await Assert.That(editor.ScrollOffsetFromTop()).IsEqualTo(0);
			await Assert.That(editor.DrawnBlocks.First).IsEqualTo(0);
		}

		[Test]
		public async Task ABlockAboveTheViewGrowingLeavesTheVisibleTextStill()
		{
			var (container, editor) = Make(Paragraphs(60), 400, 200);
			Draw(container);
			editor.SetScrollOffsetFromTop(editor.MaxScrollFromTop() / 2);
			Draw(container);
			int watched = editor.DrawnBlocks.First + 1;
			double YInView() => editor.DocumentView.TransformToParentSpace(editor, new Vector2(0, editor.BlockBounds(watched).Top)).Y;
			double before = YInView();

			for (int i = 0; i < 5; i++)
			{
				RichEditOperations.InsertText(editor.Document, new DocPosition(1, 0), Wrapping);
				editor.Relayout(1);
				Draw(container);
			}

			await Assert.That(YInView()).IsEqualTo(before).Within(0.5);
		}

		[Test]
		public async Task AdoptingAnUndoneDocumentShowsItAndClampsTheSelection()
		{
			string source = "First\n\nSecond\n";
			var (container, editor) = Make(source);
			Draw(container);

			var history = new RichEditHistory();
			history.Record(editor.Document, editor.Selection, RichEditKind.Other);
			var typed = RichEditOperations.InsertText(editor.Document, new DocPosition(1, 6), " and more");
			editor.Relayout(1);
			history.Committed(typed);
			editor.Selection = typed;

			var undone = history.Undo(editor.Document, editor.Selection).Value;
			editor.SetDocument(undone.Document, undone.Selection);
			Draw(container);
			await Assert.That(editor.Markdown).IsEqualTo(source);
			await Assert.That(((RichBlockLayout)editor.BlockLayout(1)).Block).IsEqualTo(editor.Document.Blocks[1]);

			// A selection past the end of a shorter document is pulled back into it.
			editor.Selection = RichSelection.At(new DocPosition(1, 6));
			editor.Document = RichMarkdownParser.Parse("Only\n");
			await Assert.That(editor.Selection.Caret).IsEqualTo(new DocPosition(0, 4));
		}

		[Test]
		public async Task AThemeChangeRebuildsRawBlockWidgets()
		{
			var (container, editor) = Make("Before\n\n***\n\nAfter\n");
			Draw(container);
			var oldHost = editor.RawBlockWidget(1);

			editor.Theme = new ThemeConfig();
			Draw(container);

			await Assert.That(oldHost.HasBeenClosed).IsTrue();
			await Assert.That(editor.RawBlockWidget(1)).IsNotNull();
			await Assert.That(editor.RawBlockWidget(1)).IsNotEqualTo(oldHost);
		}

		[Test]
		public async Task ANewDocumentClosesTheOldRawWidgetsEvenWithNoWidth()
		{
			var (container, editor) = Make("Before\n\n***\n\nAfter\n");
			Draw(container);
			var oldHost = editor.RawBlockWidget(1);

			container.RemoveChild(editor);
			editor.Width = 0;
			editor.Markdown = "Plain\n";

			await Assert.That(oldHost.HasBeenClosed).IsTrue();
		}

		[Test]
		public async Task OnlyOrderedItemsReLayOutWhenAnInsertRenumbersThem()
		{
			var (container, bullets) = Make("- a\n- b\n- c\n- d\n");
			Draw(container);
			int before = bullets.LayoutCount;
			RichEditOperations.SplitBlock(bullets.Document, new DocPosition(0, 1));
			bullets.Relayout(0, 1);

			// The split item and the new one; the bullets below show no number, so they keep their layouts.
			await Assert.That(bullets.LayoutCount - before).IsEqualTo(2);

			var (orderedContainer, ordered) = Make("1. a\n2. b\n3. c\n4. d\n");
			Draw(orderedContainer);
			before = ordered.LayoutCount;
			RichEditOperations.SplitBlock(ordered.Document, new DocPosition(0, 1));
			ordered.Relayout(0, 1);

			// Plus the three items below, which each moved down a number.
			await Assert.That(ordered.LayoutCount - before).IsEqualTo(5);
		}

		[Test]
		public async Task ScrollingChangesWhichBlocksDraw()
		{
			var (container, editor) = Make(Paragraphs(60), 400, 200);
			Draw(container);
			var top = editor.DrawnBlocks;

			await Assert.That(top.First).IsEqualTo(0);
			await Assert.That(top.Last).IsLessThan(59);

			editor.SetScrollOffsetFromTop(editor.MaxScrollFromTop());
			Draw(container);
			var bottom = editor.DrawnBlocks;

			await Assert.That(bottom.First).IsGreaterThan(top.Last);
			await Assert.That(bottom.Last).IsEqualTo(59);
		}
	}
}
