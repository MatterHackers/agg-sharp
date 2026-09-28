/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Tests;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	// Every MarkdownWidget constructor writes the static MarkdownWidget.Theme the selection colour is read from,
	// and these swap Clipboard.Instance.
	[NotInParallel(new[] { SharedStateKeys.MarkdownWidget, SharedStateKeys.Clipboard })]
	public class MarkdownClipboardSelectionTests
	{
		[Test]
		public async Task CopySelectionStoresPlainTextAndHeadingHtml()
		{
			var clipboard = new SimulatedClipboard();
			Clipboard.SetSystemClipboard(clipboard);

			var widget = new MarkdownWidget(new ThemeConfig(), scrollContent: false)
			{
				TextSelectionEnabled = true,
				Markdown =
				"""
				# Preview Heading

				Body copy target.
				"""
			};

			var start = widget.PlainText.IndexOf("Preview Heading", System.StringComparison.Ordinal);
			widget.SetSelection(start, start + "Preview Heading".Length);

			await Assert.That(widget.TryCopySelectionToClipboard()).IsTrue();
			await Assert.That(clipboard.GetText()).IsEqualTo("Preview Heading");
			await Assert.That(clipboard.GetHtml()).Contains("<h1 style=");
			await Assert.That(clipboard.GetHtml()).Contains(">Preview Heading</h1>");
		}

		[Test]
		public async Task FullSelectionUsesMarkdownHtmlExport()
		{
			var clipboard = new SimulatedClipboard();
			Clipboard.SetSystemClipboard(clipboard);

			var widget = new MarkdownWidget(new ThemeConfig(), scrollContent: false)
			{
				TextSelectionEnabled = true,
				Markdown =
				"""
				# Title

				Paragraph text.
				"""
			};

			widget.SetSelection(0, widget.PlainText.Length);
			await Assert.That(widget.TryCopySelectionToClipboard()).IsTrue();
			await Assert.That(clipboard.GetHtml()).Contains("<h1 style=");
			await Assert.That(clipboard.GetHtml()).Contains(">Title</h1>");
			await Assert.That(clipboard.GetHtml()).Contains("<p style=");
			await Assert.That(clipboard.GetHtml()).Contains(">Paragraph text.</p>");
		}

		[Test]
		public async Task FullSelectionUsesInlineStylesSuitableForEmailClients()
		{
			var clipboard = new SimulatedClipboard();
			Clipboard.SetSystemClipboard(clipboard);

			var widget = new MarkdownWidget(new ThemeConfig(), scrollContent: false)
			{
				TextSelectionEnabled = true,
				Markdown =
				"""
				# Styled Title

				- First item
				- Second item

				```cs
				Console.WriteLine("Hello");
				```
				"""
			};

			widget.SetSelection(0, widget.PlainText.Length);
			await Assert.That(widget.TryCopySelectionToClipboard()).IsTrue();
			await Assert.That(clipboard.GetHtml()).Contains("<h1 style=");
			await Assert.That(clipboard.GetHtml()).Contains("font-size:20pt");
			await Assert.That(clipboard.GetHtml()).Contains("<pre style=");
			await Assert.That(clipboard.GetHtml()).Contains("font-family:Consolas");
		}

		[Test]
		public async Task SelectionChangesRenderedPixelsToShowHighlight()
		{
			var markdownWidget = new MarkdownWidget(new ThemeConfig(), scrollContent: false)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				TextSelectionEnabled = true,
				Markdown =
				"""
				# Preview Heading

				Body copy target.
				"""
			};

			var container = new GuiWidget(400, 120)
			{
				DoubleBuffer = true,
			};
			container.AddChild(markdownWidget);
			container.PerformLayout();

			container.BackBuffer.NewGraphics2D().Clear(Color.White);
			container.OnDraw(container.BackBuffer.NewGraphics2D());
			var beforeSelection = new ImageBuffer(container.BackBuffer);

			var start = markdownWidget.PlainText.IndexOf("Preview Heading", System.StringComparison.Ordinal);
			markdownWidget.SetSelection(start, start + "Preview Heading".Length);

			container.BackBuffer.NewGraphics2D().Clear(Color.White);
			container.OnDraw(container.BackBuffer.NewGraphics2D());

			await Assert.That(container.BackBuffer == beforeSelection).IsFalse();
		}

		[Test]
		public async Task PartialSelectionPreservesRenderedMarkdownStructureInHtml()
		{
			var clipboard = new SimulatedClipboard();
			Clipboard.SetSystemClipboard(clipboard);

			var widget = new MarkdownWidget(new ThemeConfig(), scrollContent: false)
			{
				TextSelectionEnabled = true,
				Markdown =
				"""
				Ignore before.

				**Bold text**

				~~Strike~~ and `inline code` and [MatterCAD](https://example.com/docs)

				---

				- First bullet
				- Second bullet
				  - Nested bullet

				| Feature | Status |
				| --- | --- |
				| Links | Ready |

				Ignore after.
				"""
			};

			var selectionStart = widget.PlainText.IndexOf("Bold text", System.StringComparison.Ordinal);
			var selectionEnd = widget.PlainText.IndexOf("Ready", System.StringComparison.Ordinal) + "Ready".Length;
			widget.SetSelection(selectionStart, selectionEnd);

			await Assert.That(widget.TryCopySelectionToClipboard()).IsTrue();
			await Assert.That(clipboard.GetHtml()).Contains("<strong style=");
			await Assert.That(clipboard.GetHtml()).Contains("<del style=");
			await Assert.That(clipboard.GetHtml()).Contains("<code style=");
			await Assert.That(clipboard.GetHtml()).Contains("<a href=\"https://example.com/docs\" style=");
			await Assert.That(clipboard.GetHtml()).Contains("<hr style=");
			await Assert.That(clipboard.GetHtml()).Contains("<ul style=");
			await Assert.That(clipboard.GetHtml()).Contains("<li style=");
			await Assert.That(clipboard.GetHtml()).Contains("<table style=");
			await Assert.That(clipboard.GetHtml()).Contains("<th style=");
			await Assert.That(clipboard.GetHtml()).Contains("<td style=");
		}

		[Test]
		public async Task RightClickMenuShowsReadonlyMarkdownActionsAndCanCopySelection()
		{
			var clipboard = new SimulatedClipboard();
			Clipboard.SetSystemClipboard(clipboard);

			var systemWindow = new SystemWindow(600, 300);
			var widget = new MarkdownWidget(new ThemeConfig(), scrollContent: false)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				TextSelectionEnabled = true,
				Markdown =
				"""
				# Preview Heading

				Body copy target.
				"""
			};

			systemWindow.AddChild(widget);
			systemWindow.PerformLayout();

			var selectionText = "Preview Heading";
			var start = widget.PlainText.IndexOf(selectionText, System.StringComparison.Ordinal);
			widget.SetSelection(start, start + selectionText.Length);
			widget.OnMouseUp(new MouseEventArgs(MouseButtons.Right, 1, 10, 10, 0));

			var cutItem = systemWindow.FindDescendant("Cut Menu Item") as PopupMenu.MenuItem;
			var copyItem = systemWindow.FindDescendant("Copy Menu Item") as PopupMenu.MenuItem;
			var pasteItem = systemWindow.FindDescendant("Paste Menu Item") as PopupMenu.MenuItem;
			var selectAllItem = systemWindow.FindDescendant("Select All Menu Item") as PopupMenu.MenuItem;

			await Assert.That(cutItem).IsNotNull();
			await Assert.That(copyItem).IsNotNull();
			await Assert.That(pasteItem).IsNotNull();
			await Assert.That(selectAllItem).IsNotNull();
			await Assert.That(cutItem.Enabled).IsFalse();
			await Assert.That(copyItem.Enabled).IsTrue();
			await Assert.That(pasteItem.Enabled).IsFalse();
			await Assert.That(selectAllItem.Enabled).IsTrue();

			copyItem.InvokeClick();

			await Assert.That(clipboard.GetText()).IsEqualTo(selectionText);
			await Assert.That(clipboard.GetHtml()).Contains(">Preview Heading</h1>");
		}
	}
}
