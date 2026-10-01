/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Threading.Tasks;
using Markdig.Agg.Editing;
using MatterHackers.Agg.Tests;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	// These swap Clipboard.Instance.
	[NotInParallel(SharedStateKeys.Clipboard)]
	public class RichEditorClipboardTests
	{
		private static (RichMarkdownEditWidget Editor, SimulatedClipboard Clipboard) Make(string markdown)
		{
			var clipboard = new SimulatedClipboard();
			Clipboard.SetSystemClipboard(clipboard);
			var container = new GuiWidget(400, 300, SizeLimitsToSet.None);
			var editor = new RichMarkdownEditWidget(new ThemeConfig())
			{
				Markdown = markdown,
				UseMacKeyBindings = false,
			};
			container.AddChild(editor);
			container.PerformLayout();
			return (editor, clipboard);
		}

		private static void Key(RichMarkdownEditWidget editor, Keys keys)
		{
			editor.OnKeyDown(new KeyEventArgs(keys));
		}

		private static void Select(RichMarkdownEditWidget editor, int block, int from, int to)
		{
			editor.Selection = new RichSelection(new DocPosition(block, from), new DocPosition(block, to));
		}

		[Test]
		public async Task CopyThenPasteKeepsBoldAndLink()
		{
			var (editor, clipboard) = Make("A **bold** [link](https://x.com) end\n");
			Select(editor, 0, 2, 11);
			Key(editor, Keys.Control | Keys.C);
			await Assert.That(clipboard.GetText()).IsEqualTo("bold link");
			await Assert.That(clipboard.GetHtml()).Contains("<strong");
			await Assert.That(clipboard.GetHtml()).Contains("href=\"https://x.com\"");

			editor.Selection = RichSelection.At(new DocPosition(0, 15));
			Key(editor, Keys.Control | Keys.V);
			await Assert.That(editor.Markdown).IsEqualTo("A **bold** [link](https://x.com) end**bold** [link](https://x.com)\n");
		}

		[Test]
		public async Task CutIsOneUndoStep()
		{
			var (editor, clipboard) = Make("Hello world\n");
			Select(editor, 0, 5, 11);
			Key(editor, Keys.Control | Keys.X);
			await Assert.That(editor.Markdown).IsEqualTo("Hello\n");
			await Assert.That(clipboard.GetText()).IsEqualTo(" world");

			Key(editor, Keys.Control | Keys.Z);
			await Assert.That(editor.Markdown).IsEqualTo("Hello world\n");
		}

		[Test]
		public async Task MarkdownTextFromElsewherePastesFormatted()
		{
			var (editor, clipboard) = Make("Start\n");
			clipboard.SetText("**bold** and\n\n- item");
			editor.Selection = RichSelection.At(new DocPosition(0, 5));
			Key(editor, Keys.Control | Keys.V);
			await Assert.That(editor.Markdown).IsEqualTo("Start**bold** and\n\n- item\n");
			await Assert.That(editor.Document.Blocks[1].Kind).IsEqualTo(RichBlockKind.ListItem);

			// The paste is one undo step of its own.
			Key(editor, Keys.Control | Keys.Z);
			await Assert.That(editor.Markdown).IsEqualTo("Start\n");
		}

		[Test]
		public async Task PlainLineTakesTheCaretStyleAndReplacesTheSelection()
		{
			var (editor, clipboard) = Make("**Hello world**\n");
			clipboard.SetText("there");
			Select(editor, 0, 6, 11);
			Key(editor, Keys.Control | Keys.V);
			await Assert.That(editor.Markdown).IsEqualTo("**Hello there**\n");
		}

		[Test]
		public async Task CodeBlockGetsPlainText()
		{
			var (editor, clipboard) = Make("```\nab\n```\n");
			clipboard.SetText("**x**");
			editor.Selection = RichSelection.At(new DocPosition(0, 1));
			Key(editor, Keys.Control | Keys.V);
			await Assert.That(editor.Markdown).IsEqualTo("```\na**x**b\n```\n");
		}

		[Test]
		public async Task CopyInOneEditorPastesFormattedInAnother()
		{
			var (first, _) = Make("# Head line\n\nSome text\n");
			first.Selection = new RichSelection(new DocPosition(0, 5), new DocPosition(1, 4));
			Key(first, Keys.Control | Keys.C);

			// A second editor in the same process; Make swaps in a new clipboard, so carry the text over.
			var text = Clipboard.Instance.GetText();
			var (second, clipboard) = Make("");
			clipboard.SetText(text);
			Key(second, Keys.Control | Keys.V);
			await Assert.That(second.Markdown).IsEqualTo("# line\n\nSome");
		}

		[Test]
		public async Task CrlfCopyIsStillRecognisedOnPaste()
		{
			var (editor, clipboard) = Make("# Title\r\n\r\n<div>\r\nhi\r\n</div>\r\n");
			Key(editor, Keys.Control | Keys.A);
			Key(editor, Keys.Control | Keys.C);
			var (other, otherClipboard) = Make("");
			otherClipboard.SetText(clipboard.GetText());
			Key(other, Keys.Control | Keys.V);
			await Assert.That(other.Markdown).StartsWith("# Title");
		}

		[Test]
		public async Task ShiftDownCopyPastesNoEmptyItem()
		{
			var (editor, _) = Make("- a\n- b\n\nEnd\n");
			Key(editor, Keys.Down | Keys.Shift);
			await Assert.That(editor.Selection.End).IsEqualTo(new DocPosition(1, 0));
			Key(editor, Keys.Control | Keys.C);
			editor.Selection = RichSelection.At(new DocPosition(2, 3));
			Key(editor, Keys.Control | Keys.V);
			await Assert.That(editor.Markdown).IsEqualTo("- a\n- b\n\nEnd\n\n- a\n");
		}

		[Test]
		public async Task DarkThemeCopyPastesWithALightBackground()
		{
			var (editor, clipboard) = Make("Some text\n");
			editor.Theme = new ThemeConfig { BackgroundColor = new MatterHackers.Agg.Color("#101010"), TextColor = new MatterHackers.Agg.Color("#eeeeee") };
			Key(editor, Keys.Control | Keys.A);
			Key(editor, Keys.Control | Keys.C);
			string html = clipboard.GetHtml().ToLowerInvariant();
			await Assert.That(html).DoesNotContain("101010");
			await Assert.That(html).DoesNotContain("eeeeee");
		}

		[Test]
		public async Task HostHandlesPasteFirst()
		{
			var (editor, clipboard) = Make("Text\n");
			clipboard.SetText("more");
			editor.PasteRequested += (s, e) => e.Handled = true;
			editor.Selection = RichSelection.At(new DocPosition(0, 4));
			Key(editor, Keys.Control | Keys.V);
			await Assert.That(editor.Markdown).IsEqualTo("Text\n");
		}
	}
}
