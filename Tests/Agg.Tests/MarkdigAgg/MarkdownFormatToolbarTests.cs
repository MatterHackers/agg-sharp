/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Linq;
using System.Threading.Tasks;
using Markdig.Agg.Editing;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	public class MarkdownFormatToolbarTests
	{
		/// <summary>
		/// The real widget-free commands, counting the toolbar's requests to focus the text and the edits it reports.
		/// </summary>
		private class CountingCommands : RichDocumentCommands
		{
			public CountingCommands(RichDocument document, RichSelection selection)
				: base(document, selection)
			{
				DocumentChanged += (s, e) => Changes++;
			}

			public int Focuses { get; private set; }

			public int Changes { get; private set; }

			public override void FocusText() => Focuses++;
		}

		private static (MarkdownFormatToolbar Toolbar, CountingCommands Commands) Make(string markdown, RichSelection selection)
		{
			var commands = new CountingCommands(RichMarkdownParser.Parse(markdown), selection);
			return (new MarkdownFormatToolbar(new ThemeConfig(), commands), commands);
		}

		private static RichSelection Range(int block, int from, int to) => new RichSelection(new DocPosition(block, from), new DocPosition(block, to));

		private static RichSelection Caret(int block, int offset) => RichSelection.At(new DocPosition(block, offset));

		private static string Markdown(RichDocumentCommands commands) => RichMarkdownWriter.Write(commands.Document);

		[Test]
		public async Task BoldOverASelectionBoldsIt()
		{
			var (toolbar, commands) = Make("Hello world\n", Range(0, 0, 5));
			await Assert.That(toolbar.BoldButton.IsPressed).IsFalse();

			toolbar.BoldButton.InvokeClick();

			await Assert.That(Markdown(commands)).IsEqualTo("**Hello** world\n");
			await Assert.That(toolbar.BoldButton.IsPressed).IsTrue();
		}

		[Test]
		public async Task BoldAtACaretFlipsThePendingStyleOnly()
		{
			var (toolbar, commands) = Make("Hello world\n", Caret(0, 3));

			toolbar.BoldButton.InvokeClick();

			await Assert.That(commands.PendingStyle.Flipped).IsEqualTo(RichInlineStyle.Bold);
			await Assert.That(toolbar.BoldButton.IsPressed).IsTrue();
			await Assert.That(Markdown(commands)).IsEqualTo("Hello world\n");

			// Moving the caret drops the pending style, and the button follows.
			commands.Selection = Caret(0, 1);
			await Assert.That(toolbar.BoldButton.IsPressed).IsFalse();
		}

		[Test]
		public async Task StyleButtonsShowAllMixedAndNone()
		{
			var (toolbar, commands) = Make("**ab**cd\n", Range(0, 0, 2));
			await Assert.That(toolbar.BoldButton.Coverage).IsEqualTo(RichStyleCoverage.All);

			commands.Selection = Range(0, 0, 4);
			await Assert.That(toolbar.BoldButton.Coverage).IsEqualTo(RichStyleCoverage.Mixed);

			commands.Selection = Range(0, 2, 4);
			await Assert.That(toolbar.BoldButton.Coverage).IsEqualTo(RichStyleCoverage.None);
		}

		[Test]
		public async Task SizeListSetsHeadingTwo()
		{
			var (toolbar, commands) = Make("Hello world\n", Caret(0, 2));
			await Assert.That(toolbar.SizeList.SelectedIndex).IsEqualTo(0);

			toolbar.SizeList.SelectedIndex = 2;

			await Assert.That(Markdown(commands)).IsEqualTo("## Hello world\n");
		}

		[Test]
		public async Task SizeListFollowsTheSelection()
		{
			var (toolbar, commands) = Make("# Title\n\nBody\n", Caret(1, 1));
			await Assert.That(toolbar.SizeList.SelectedIndex).IsEqualTo(0);

			commands.Selection = Caret(0, 2);
			await Assert.That(toolbar.SizeList.SelectedIndex).IsEqualTo(1);

			// Over a heading and a paragraph the size is mixed: blank, and the document is untouched.
			commands.Selection = new RichSelection(new DocPosition(0, 1), new DocPosition(1, 2));
			await Assert.That(toolbar.SizeList.SelectedIndex).IsEqualTo(-1);
			await Assert.That(Markdown(commands)).IsEqualTo("# Title\n\nBody\n");
		}

		[Test]
		public async Task AlignmentIsDisabledInAList()
		{
			var (toolbar, commands) = Make("- one\n- two\n\nplain\n", Caret(0, 1));
			await Assert.That(toolbar.AlignCenterButton.Enabled).IsFalse();
			await Assert.That(toolbar.BulletListButton.IsPressed).IsTrue();

			commands.Selection = Caret(2, 1);
			await Assert.That(toolbar.AlignCenterButton.Enabled).IsTrue();
			await Assert.That(toolbar.AlignLeftButton.IsPressed).IsTrue();

			toolbar.AlignCenterButton.InvokeClick();
			await Assert.That(toolbar.AlignCenterButton.IsPressed).IsTrue();
			await Assert.That(commands.Document.Blocks[2].Alignment).IsEqualTo(RichAlignment.Center);
		}

		[Test]
		public async Task NumberedListTogglesOnAndOff()
		{
			var (toolbar, commands) = Make("one\n", Caret(0, 1));

			toolbar.NumberedListButton.InvokeClick();
			await Assert.That(Markdown(commands)).IsEqualTo("1. one\n");
			await Assert.That(toolbar.NumberedListButton.IsPressed).IsTrue();
			await Assert.That(toolbar.AlignLeftButton.Enabled).IsFalse();

			toolbar.NumberedListButton.InvokeClick();
			await Assert.That(Markdown(commands)).IsEqualTo("one\n");
		}

		[Test]
		public async Task TheLinkRowSetsAndRemovesAUrl()
		{
			var (toolbar, commands) = Make("Hello world\n", Range(0, 0, 5));

			toolbar.LinkButton.InvokeClick();
			await Assert.That(toolbar.LinkEditor.Visible).IsTrue();
			await Assert.That(toolbar.LinkEditor.Url).IsEqualTo("");

			toolbar.LinkEditor.Url = "https://example.com";
			toolbar.LinkEditor.Accept();
			await Assert.That(toolbar.LinkEditor.Visible).IsFalse();
			await Assert.That(Markdown(commands)).IsEqualTo("[Hello](https://example.com) world\n");
			await Assert.That(toolbar.LinkButton.IsPressed).IsTrue();

			// Reopening edits that link; Remove keeps the text.
			toolbar.LinkButton.InvokeClick();
			await Assert.That(toolbar.LinkEditor.Url).IsEqualTo("https://example.com");
			toolbar.LinkEditor.Remove();
			await Assert.That(Markdown(commands)).IsEqualTo("Hello world\n");
		}

		[Test]
		public async Task AHostDialogReplacesTheLinkRow()
		{
			var (toolbar, _) = Make("[Hi](https://a.b) there\n", Caret(0, 1));
			string requested = null;
			toolbar.LinkRequested += (s, e) => requested = e.CurrentUrl;

			toolbar.LinkButton.InvokeClick();

			await Assert.That(requested).IsEqualTo("https://a.b");
			await Assert.That(toolbar.LinkEditor.Visible).IsFalse();
		}

		[Test]
		public async Task TableInsertsATwoByTwoTable()
		{
			var (toolbar, commands) = Make("Hello\n", Caret(0, 5));

			toolbar.TableButton.InvokeClick();

			var table = commands.Document.Blocks[1];
			await Assert.That(table.Kind).IsEqualTo(RichBlockKind.Table);
			await Assert.That(table.TableRows.Count).IsEqualTo(2);
			await Assert.That(table.TableRows[0].Count).IsEqualTo(2);
			await Assert.That(commands.Selection.Caret).IsEqualTo(new DocPosition(1, 0, 0, 0));
		}

		[Test]
		public async Task CodeBlockRoundTrips()
		{
			var (toolbar, commands) = Make("one\n\n**two**\n\nafter\n", new RichSelection(new DocPosition(0, 1), new DocPosition(1, 2)));

			toolbar.CodeBlockButton.InvokeClick();

			await Assert.That(commands.Document.Blocks.Count).IsEqualTo(2);
			await Assert.That(commands.Document.Blocks[0].CodeText).IsEqualTo("one\ntwo");
			await Assert.That(Markdown(commands)).IsEqualTo("```\none\ntwo\n```\n\nafter\n");
			await Assert.That(commands.Selection).IsEqualTo(new RichSelection(new DocPosition(0, 1), new DocPosition(0, 6)));
			await Assert.That(toolbar.CodeBlockButton.IsPressed).IsTrue();
			await Assert.That(toolbar.BoldButton.Enabled).IsFalse();

			toolbar.CodeBlockButton.InvokeClick();

			await Assert.That(Markdown(commands)).IsEqualTo("one\n\ntwo\n\nafter\n");
			await Assert.That(commands.Selection).IsEqualTo(new RichSelection(new DocPosition(0, 1), new DocPosition(1, 2)));
		}

		[Test]
		public async Task CodeBlockFromAListItemLeavesTheList()
		{
			var (toolbar, commands) = Make("- one\n- two\n- three\n", Caret(1, 0));

			toolbar.CodeBlockButton.InvokeClick();

			await Assert.That(Markdown(commands)).IsEqualTo("- one\n\n```\ntwo\n```\n\n- three\n");
		}

		[Test]
		public async Task CodeBlockOverAllBlocksRefreshesWithoutCrashing()
		{
			var (toolbar, commands) = Make("one\n\ntwo\n", new RichSelection(new DocPosition(0, 1), new DocPosition(1, 2)));

			toolbar.CodeBlockButton.InvokeClick();

			await Assert.That(Markdown(commands)).IsEqualTo("```\none\ntwo\n```\n");
			await Assert.That(toolbar.CodeBlockButton.IsPressed).IsTrue();
		}

		[Test]
		public async Task EveryToolbarActionGivesTheTextItsFocusBack()
		{
			var (toolbar, commands) = Make("Hello world\n", Range(0, 0, 5));

			toolbar.BoldButton.InvokeClick();
			await Assert.That(commands.Focuses).IsEqualTo(1);

			commands.Selection = Caret(0, 2);
			toolbar.ItalicButton.InvokeClick();
			await Assert.That(commands.Focuses).IsEqualTo(2);

			toolbar.LinkButton.InvokeClick();
			toolbar.LinkEditor.Cancel();
			await Assert.That(commands.Focuses).IsEqualTo(3);
		}

		[Test]
		public async Task AClickThatChangesNothingReportsNoChange()
		{
			var (toolbar, commands) = Make("Hello\n", Caret(0, 2));

			toolbar.AlignLeftButton.InvokeClick();
			await Assert.That(commands.Changes).IsEqualTo(0);

			toolbar.AlignRightButton.InvokeClick();
			await Assert.That(commands.Changes).IsEqualTo(1);
		}

		[Test]
		public async Task CodeBlockIsDisabledWhereItCannotApply()
		{
			var (toolbar, commands) = Make("before\n\n| a | b |\n| - | - |\n| c | d |\n", Caret(0, 1));
			await Assert.That(toolbar.CodeBlockButton.Enabled).IsTrue();

			commands.Selection = RichSelection.At(new DocPosition(1, 0, 1, 1));
			await Assert.That(toolbar.CodeBlockButton.Enabled).IsFalse();

			commands.Selection = new RichSelection(new DocPosition(0, 1), new DocPosition(1, 1, 0, 0));
			await Assert.That(toolbar.CodeBlockButton.Enabled).IsFalse();
		}

		[Test]
		public async Task CodeBlockToParagraphsDropsBlankLines()
		{
			var (toolbar, commands) = Make("```\na\n\nb\n```\n", Caret(0, 2));

			toolbar.CodeBlockButton.InvokeClick();

			await Assert.That(commands.Document.Blocks.Count).IsEqualTo(2);
			await Assert.That(Markdown(commands)).IsEqualTo("a\n\nb\n");
			await Assert.That(commands.Selection.Caret).IsEqualTo(new DocPosition(1, 0));
		}

		[Test]
		public async Task LinkRowEscapeCancelsAndAddsAScheme()
		{
			var (toolbar, commands) = Make("Hello world\n", Range(0, 0, 5));

			toolbar.LinkButton.InvokeClick();
			toolbar.LinkEditor.OnKeyDown(new KeyEventArgs(Keys.Escape));
			await Assert.That(toolbar.LinkEditor.Visible).IsFalse();
			await Assert.That(Markdown(commands)).IsEqualTo("Hello world\n");

			toolbar.LinkButton.InvokeClick();
			toolbar.LinkEditor.Url = "example.com";
			toolbar.LinkEditor.Accept();
			await Assert.That(Markdown(commands)).IsEqualTo("[Hello](https://example.com) world\n");

			await Assert.That(MarkdownLinkEditor.WithScheme("Fillets.md")).IsEqualTo("Fillets.md");
			await Assert.That(MarkdownLinkEditor.WithScheme("mailto:a@b.c")).IsEqualTo("mailto:a@b.c");
			await Assert.That(MarkdownLinkEditor.WithScheme("docs/page")).IsEqualTo("docs/page");
		}

		[Test]
		public async Task LinkRowClosesWhenTheCaretMovesOrTheDocumentIsSwapped()
		{
			var (toolbar, commands) = Make("Hello world\n", Range(0, 0, 5));

			toolbar.LinkButton.InvokeClick();
			commands.Selection = Caret(0, 8);
			await Assert.That(toolbar.LinkEditor.Visible).IsFalse();

			toolbar.LinkButton.InvokeClick();
			toolbar.Commands = new RichDocumentCommands(RichMarkdownParser.Parse("Other\n"));
			await Assert.That(toolbar.LinkEditor.Visible).IsFalse();
		}

		[Test]
		public async Task SizeListNamesDeepHeadings()
		{
			var (toolbar, commands) = Make("#### Deep\n\nBody\n", Caret(0, 1));
			await Assert.That(toolbar.SizeList.SelectedLabel).IsEqualTo("Heading 4");

			commands.Selection = Caret(1, 1);
			await Assert.That(toolbar.SizeList.SelectedLabel).IsEqualTo("Normal");
			await Assert.That(toolbar.SizeList.MenuItems.Count).IsEqualTo(4);

			toolbar.SizeList.SelectedIndex = 3;
			await Assert.That(Markdown(commands)).IsEqualTo("#### Deep\n\n### Body\n");
		}

		/// <summary>
		/// Related buttons share one outlined group, in reading order; the size list stands alone between the first
		/// two. Each group is one item of the wrapping strip (its direct child row), which is what a host sizing its
		/// window to the strip's ContentWidth relies on.
		/// </summary>
		[Test]
		public async Task TheGroupsHoldTheirButtonsInOrder()
		{
			var (toolbar, _) = Make("Hello world\n", Caret(0, 1));
			var expected = new[]
			{
				new[] { toolbar.BoldButton, toolbar.ItalicButton, toolbar.StrikeButton, toolbar.CodeButton },
				new[] { toolbar.AlignLeftButton, toolbar.AlignCenterButton, toolbar.AlignRightButton },
				new[] { toolbar.BulletListButton, toolbar.NumberedListButton, toolbar.QuoteButton },
				new[] { toolbar.LinkButton, toolbar.CodeBlockButton, toolbar.TableButton },
			};

			await Assert.That(toolbar.Groups.Count).IsEqualTo(expected.Length);
			for (int i = 0; i < expected.Length; i++)
			{
				await Assert.That(toolbar.Groups[i].Buttons.ToArray()).IsEquivalentTo(expected[i], CollectionOrdering.Matching);
				await Assert.That(toolbar.Groups[i].Children.Count).IsEqualTo(expected[i].Length);
			}

			// Laid out wide enough for one row, so the strip has placed its items in a row.
			var host = new GuiWidget(1200 * GuiWidget.DeviceScale, 200 * GuiWidget.DeviceScale);
			host.AddChild(toolbar);
			host.PerformLayout();
			var strip = toolbar.Children.OfType<FlowLeftRightWithWrapping>().Single();
			var stripItems = toolbar.Groups.Cast<GuiWidget>().Append(toolbar.SizeList);
			foreach (var item in stripItems)
			{
				await Assert.That(item.Parent?.Parent).IsSameReferenceAs(strip);
			}

			await Assert.That(toolbar.SizeList.Parent).IsSameReferenceAs(toolbar.Groups[0].Parent);
			await Assert.That(toolbar.SizeList.Parent.Children.IndexOf(toolbar.SizeList))
				.IsEqualTo(toolbar.SizeList.Parent.Children.IndexOf(toolbar.Groups[0]) + 1);
		}

		/// <summary>
		/// Every icon is drawn in an em box of one height on one baseline across the whole toolbar, so B, the list
		/// glyphs and the wide link and code glyphs read as one row; the ink each button draws lands inside its box.
		/// The groups and the size list share one centre line and one height. Checked at 1x and 2x.
		/// </summary>
		[Test]
		[NotInParallel]
		[Arguments(1.0)]
		[Arguments(2.0)]
		public async Task EveryIconIsDrawnAtOneSizeOnOneBaseline(double scale)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = scale;
				var (toolbar, _) = Make("Hello world\n", Caret(0, 1));
				var host = new GuiWidget(1200 * scale, 200 * scale);
				host.AddChild(toolbar);
				host.PerformLayout();

				var buttons = toolbar.Groups.SelectMany(g => g.Buttons).ToList();
				await Assert.That(buttons.Count).IsEqualTo(13);
				var first = buttons[0].TransformToParentSpace(toolbar, buttons[0].IconEmBox);
				// The box is the face's ascent to descent, which Font Awesome sets a little taller than its em.
				await Assert.That(first.Height).IsEqualTo(MarkdownFormatButton.IconDesignSize * scale).Within(.1 * MarkdownFormatButton.IconDesignSize * scale);
				foreach (var button in buttons)
				{
					var box = button.TransformToParentSpace(toolbar, button.IconEmBox);
					await Assert.That(box.Height).IsEqualTo(first.Height).Because(button.ToolTipText);
					await Assert.That(box.Bottom).IsEqualTo(first.Bottom).Because(button.ToolTipText);
					await Assert.That(button.Width).IsEqualTo(MarkdownFormatButton.DesignWidth * scale);
					await Assert.That(button.Height).IsEqualTo(MarkdownFormatToolbar.GroupHeight - 2 * scale);

					var ink = InkBounds(button);
					await Assert.That(ink.Width).IsGreaterThan(0).Because(button.ToolTipText + " draws its icon");
					var local = button.IconEmBox;
					await Assert.That(ink.Bottom >= local.Bottom - 1 && ink.Top <= local.Top + 1
						&& ink.Left >= local.Left - 1 && ink.Right <= local.Right + 1).IsTrue()
						.Because($"{button.ToolTipText}'s ink {ink} lands in its em box {local}");
				}

				var centre = toolbar.Groups[0].TransformToParentSpace(toolbar, toolbar.Groups[0].LocalBounds).Center.Y;
				foreach (var group in toolbar.Groups)
				{
					var bounds = group.TransformToParentSpace(toolbar, group.LocalBounds);
					await Assert.That(bounds.Center.Y).IsEqualTo(centre).Within(.5);
					await Assert.That(bounds.Height).IsEqualTo(MarkdownFormatToolbar.GroupHeight);
				}

				var list = toolbar.SizeList;
				var listOuter = list.TransformToParentSpace(toolbar, list.LocalBounds);
				listOuter.Inflate(list.DeviceBorder);
				await Assert.That(listOuter.Height).IsEqualTo(MarkdownFormatToolbar.GroupHeight);
				await Assert.That(listOuter.Center.Y).IsEqualTo(centre).Within(.5);
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		/// <summary>The bounds of every pixel <paramref name="button"/> draws, in its local pixels.</summary>
		private static RectangleDouble InkBounds(GuiWidget button)
		{
			var image = new ImageBuffer((int)button.Width, (int)button.Height);
			button.OnDraw(image.NewGraphics2D());
			var ink = RectangleDouble.ZeroIntersection;
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					if (image.GetPixel(x, y).alpha > 0)
					{
						ink.ExpandToInclude(x, y);
						ink.ExpandToInclude(x + 1, y + 1);
					}
				}
			}

			return ink;
		}
	}
}
