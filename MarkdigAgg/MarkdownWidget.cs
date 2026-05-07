/*
Copyright(c) 2025, Lars Brubaker, John Lewin
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;
using System.Linq;
using System.Text;
using Markdig.Renderers.Agg;
using Markdig.Renderers.Agg.Inlines;

namespace Markdig.Agg
{
	public class MarkdownWidget : ScrollableWidget
	{
		private readonly List<MarkdownTextWidget> orderedTextWidgets = new List<MarkdownTextWidget>();
		private readonly FlowLayoutWidget contentPanel;
		private readonly AggMarkdownDocument markdownDocument;
		private bool mouseSelectionInProgress;
		private bool suppressClickOnMouseUp;
		private int selectionAnchorIndex;
		private int selectionFocusIndex;
		private string documentText = string.Empty;
		private MarkdownPathHandler pathHandler;
		private string _markDownText = null;
		public static ThemeConfig Theme { get; private set; }

		/// <summary>
		/// Fired when a relative link is navigated within the markdown content.
		/// The string argument is the resolved full file path that was loaded.
		/// </summary>
		public event EventHandler<string> UriNavigated;

		public static Action<string> LaunchBrowser { get; set; }

		// ImageSequence imageSequenceToLoadInto, string uriToLoad, Action doneLoading = null
		public static Action<ImageSequence, string, Action> RetrieveImageSquenceAsync;
		// string uriToLoad, Action<string> updateResult, bool addToAppCache = true, Action<HttpRequestMessage> addHeaders = null
		public static Action<string, Action<string>, bool, Action<HttpRequestMessage>> RetrieveText;
		public static Func<string, string> ResolveAssetUrl { get; set; }

        public MarkdownWidget(ThemeConfig theme, string contentUri, bool scrollContent = true)
			: this(theme, scrollContent)
		{
            pathHandler = new MarkdownPathHandler(contentUri);
		}

		public MarkdownWidget(ThemeConfig theme, bool scrollContent = true)
			: base(scrollContent)
		{
			markdownDocument = new AggMarkdownDocument();

			MarkdownWidget.Theme = theme;
			this.HAnchor = HAnchor.Stretch;
			this.ScrollArea.HAnchor = HAnchor.Stretch;
			this.ScrollArea.VAnchor = VAnchor.Fit;
			if (scrollContent)
			{
				this.VAnchor = VAnchor.Stretch;
				this.ScrollArea.Margin = new BorderDouble(0, 0, 15, 0);
			}
			else
			{
				this.VAnchor = VAnchor.Fit;
			}

			var lastScroll = this.TopLeftOffset;
			this.ScrollPositionChanged += (s, e) =>
			{
				lastScroll = TopLeftOffset;
			};

			// make sure as the scrolling area changes height we maintain our current scroll position
			this.ScrollArea.BoundsChanged += (s, e) =>
			{
				TopLeftOffset = lastScroll;
			};

			contentPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};

			this.AddChild(contentPanel);
		}

		public bool TextSelectionEnabled
		{
			get;
			set;
		}

		public bool HasSelection => selectionAnchorIndex != selectionFocusIndex;

		public int SelectionStart => Math.Min(selectionAnchorIndex, selectionFocusIndex);

		public int SelectionEnd => Math.Max(selectionAnchorIndex, selectionFocusIndex);

		public string PlainText => documentText;

		public override void OnSizeChanged(EventArgs e)
		{
			contentPanel.Height = contentPanel.Height - 1;

			base.OnSizeChanged(e);
		}

		public void LoadUri(string uri)
		{
			try
			{
				if (uri.StartsWith("Docs/Help") || uri.StartsWith("Docs\\Help"))
				{
					uri = Path.Combine(StaticData.RootPath, uri);
				}

				string fullPath = pathHandler.ResolvePath(uri);

				if (File.Exists(fullPath))
				{
					string markDown = File.ReadAllText(fullPath);

					// Strip frontmatter before displaying
					markDown = StripFrontmatter(markDown);

					pathHandler.UpdateCurrentDirectory(fullPath);

					UiThread.RunOnIdle(() =>
					{
						this.Markdown = markDown;
						UriNavigated?.Invoke(this, fullPath);
					});
				}
			}
			catch
			{
				this.Markdown = "";
			}
		}

		public string ResolveImageSource(string uri)
		{
			if (string.IsNullOrWhiteSpace(uri))
			{
				return uri;
			}

			var remappedUri = ResolveAssetUrl?.Invoke(uri);
			if (!string.IsNullOrWhiteSpace(remappedUri))
			{
				return remappedUri;
			}

			if (uri.StartsWith("Docs/Help") || uri.StartsWith("Docs\\Help"))
			{
				return Path.Combine(StaticData.RootPath, uri);
			}

			if (pathHandler != null
				&& !Uri.TryCreate(uri, UriKind.Absolute, out _))
			{
				try
				{
					return pathHandler.ResolvePath(uri);
				}
				catch
				{
				}
			}

			return uri;
		}

		public void SetContentUri(string contentUri)
		{
			if (!string.IsNullOrWhiteSpace(contentUri))
			{
				var directory = File.Exists(contentUri)
					? Path.GetDirectoryName(contentUri)
					: contentUri;

				if (pathHandler == null)
				{
					pathHandler = new MarkdownPathHandler(directory);
				}
				else
				{
					// Preserve the existing basePath (set at construction to the doc root) so that
					// cross-directory relative links don't throw "outside base directory".
					// Only shift currentDirectory to resolve links relative to the new article.
					pathHandler.SetCurrentDirectory(directory);
				}
			}
		}

		/// <summary>
		/// Strips YAML frontmatter from markdown content if present
		/// </summary>
		/// <param name="content">The markdown content that may contain frontmatter</param>
		/// <returns>The markdown content without frontmatter</returns>
		public static string StripFrontmatter(string content)
		{
			if (string.IsNullOrEmpty(content))
			{
				return content;
			}

			var lines = content.Split('\n');
			if (lines.Length < 2 || !lines[0].Trim().Equals("---"))
			{
				return content;
			}

			int frontmatterEndIndex = -1;
			for (int i = 1; i < lines.Length; i++)
			{
				var line = lines[i].Trim();
				if (line.Equals("---"))
				{
					frontmatterEndIndex = i;
					break;
				}
			}

			if (frontmatterEndIndex == -1)
			{
				return content;
			}

			var remainingLines = lines.Skip(frontmatterEndIndex + 1);
			return string.Join("\n", remainingLines);
		}

		/// <summary>
		/// Gets or sets the markdown to display.
		/// </summary>
		public string Markdown
		{
			get => _markDownText;
			set
			{
				if (_markDownText != value)
				{
					_markDownText = value;

					// Empty self
					contentPanel.CloseChildren();

					this.Width = 10;
					this.ScrollPositionFromTop = Vector2.Zero;

					// Parse and reconstruct
					markdownDocument.Markdown = value;
					markdownDocument.Parse(MarkdownWidget.Theme, contentPanel);
					RebuildSelectionModel();
				}
			}
		}

		public string MatchingText
		{
			get => markdownDocument.MatchingText;
			set => markdownDocument.MatchingText = value;
		}

		public void ClearSelection()
		{
			if (!HasSelection)
			{
				return;
			}

			selectionFocusIndex = selectionAnchorIndex;
			InvalidateTextSelectionVisuals();
		}

		public void SetSelection(int start, int end)
		{
			var clampedStart = Math.Max(0, Math.Min(start, documentText.Length));
			var clampedEnd = Math.Max(0, Math.Min(end, documentText.Length));
			selectionAnchorIndex = clampedStart;
			selectionFocusIndex = clampedEnd;
			Focus();
			InvalidateTextSelectionVisuals();
		}

		public void SelectAll()
		{
			if (string.IsNullOrEmpty(documentText))
			{
				return;
			}

			SetSelection(0, documentText.Length);
		}

		public MarkdownClipboardData GetSelectionClipboardData()
		{
			if (!HasSelection)
			{
				return default;
			}

			var start = SelectionStart;
			var end = SelectionEnd;
			var selectedText = documentText.Substring(start, end - start);
			var selectedHtml = AggMarkdownDocument.StyleHtmlFragment(BuildSelectionHtml(start, end), Theme);
			if (start == 0
				&& end == documentText.Length
				&& !string.IsNullOrWhiteSpace(_markDownText))
			{
				selectedHtml = AggMarkdownDocument.ToStyledHtml(_markDownText, Theme, markdownDocument.Pipeline);
			}

			return new MarkdownClipboardData(selectedText, selectedHtml);
		}

		public bool TryCopySelectionToClipboard()
		{
			if (!HasSelection || Clipboard.Instance == null)
			{
				return false;
			}

			var clipboardData = GetSelectionClipboardData();
			if (string.IsNullOrEmpty(clipboardData.PlainText))
			{
				return false;
			}

			if (!string.IsNullOrWhiteSpace(clipboardData.Html))
			{
				Clipboard.Instance.SetTextAndHtml(clipboardData.PlainText, clipboardData.Html);
			}
			else
			{
				Clipboard.Instance.SetText(clipboardData.PlainText);
			}

			return true;
		}

		internal bool TryGetWidgetSelection(MarkdownTextWidget widget, out int selectionStart, out int selectionEnd, out Color selectionColor)
		{
			selectionStart = 0;
			selectionEnd = 0;
			selectionColor = new Color(Theme?.PrimaryAccentColor ?? Color.LightBlue, 90);

			if (!HasSelection)
			{
				return false;
			}

			var start = Math.Max(SelectionStart, widget.DocumentStartIndex);
			var end = Math.Min(SelectionEnd, widget.DocumentTextEndExclusive);
			if (end <= start)
			{
				return false;
			}

			selectionStart = start - widget.DocumentStartIndex;
			selectionEnd = end - widget.DocumentStartIndex;
			return true;
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (TextSelectionEnabled
				&& mouseEvent.Button == MouseButtons.Left
				&& TryGetDocumentIndexForPoint(mouseEvent.Position, out int selectionIndex))
			{
				selectionAnchorIndex = selectionIndex;
				selectionFocusIndex = selectionIndex;
				mouseSelectionInProgress = true;
				suppressClickOnMouseUp = false;
				Focus();
				InvalidateTextSelectionVisuals();
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (TextSelectionEnabled
				&& mouseSelectionInProgress
				&& TryGetDocumentIndexNearPoint(mouseEvent.Position, out int selectionIndex))
			{
				if (selectionFocusIndex != selectionIndex)
				{
					selectionFocusIndex = selectionIndex;
					suppressClickOnMouseUp = HasSelection;
					InvalidateTextSelectionVisuals();
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (TextSelectionEnabled && mouseEvent.Button == MouseButtons.Left)
			{
				if (mouseSelectionInProgress)
				{
					TryGetDocumentIndexNearPoint(mouseEvent.Position, out selectionFocusIndex);
					InvalidateTextSelectionVisuals();
				}

				mouseSelectionInProgress = false;
				if (suppressClickOnMouseUp)
				{
					suppressClickOnMouseUp = false;
					return;
				}
			}

			if (TextSelectionEnabled && mouseEvent.Button == MouseButtons.Right)
			{
				ShowRightClickMenu(mouseEvent);
			}

			base.OnMouseUp(mouseEvent);
		}

		private void ShowRightClickMenu(MouseEventArgs mouseEvent)
		{
			if (this.Parents<SystemWindow>().LastOrDefault() == null)
			{
				return;
			}

			var popupMenu = new PopupMenu(Theme);

			var cut = popupMenu.CreateMenuItem("Cut".Localize());
			cut.Enabled = false;

			var copy = popupMenu.CreateMenuItem("Copy".Localize());
			copy.Enabled = HasSelection;
			copy.Click += (s, e) => TryCopySelectionToClipboard();

			var paste = popupMenu.CreateMenuItem("Paste".Localize());
			paste.Enabled = false;

			popupMenu.CreateSeparator();

			var selectAll = popupMenu.CreateMenuItem("Select All".Localize());
			selectAll.Enabled = !string.IsNullOrEmpty(documentText);
			selectAll.Click += (s, e) => SelectAll();

			popupMenu.ShowMenu(this, mouseEvent);
		}

		private void RebuildSelectionModel()
		{
			orderedTextWidgets.Clear();
			documentText = string.Empty;
			selectionAnchorIndex = 0;
			selectionFocusIndex = 0;

			foreach (var textWidget in EnumerateMarkdownTextWidgets(contentPanel))
			{
				orderedTextWidgets.Add(textWidget);
			}

			var builder = new StringBuilder();
			for (int i = 0; i < orderedTextWidgets.Count; i++)
			{
				var current = orderedTextWidgets[i];
				var next = i + 1 < orderedTextWidgets.Count ? orderedTextWidgets[i + 1] : null;
				current.DocumentStartIndex = builder.Length;
				current.TrailingText = GetSeparatorBetween(current, next);
				builder.Append(current.Text);
				builder.Append(current.TrailingText);
			}

			documentText = builder.ToString();
			InvalidateTextSelectionVisuals();
		}

		private void InvalidateTextSelectionVisuals()
		{
			foreach (var textWidget in orderedTextWidgets)
			{
				textWidget.Invalidate();
			}

			Invalidate();
		}

		private string BuildSelectionHtml(int selectionStart, int selectionEnd)
		{
			var selectedTextWidgets = orderedTextWidgets
				.Where(textWidget => TextWidgetOverlapsSelection(textWidget, selectionStart, selectionEnd))
				.ToList();

			if (selectedTextWidgets.Count == 0)
			{
				return string.IsNullOrEmpty(documentText)
					? string.Empty
					: $"<p>{EscapeHtml(documentText.Substring(selectionStart, selectionEnd - selectionStart))}</p>";
			}

			var firstBlock = GetTopLevelBlock(selectedTextWidgets.First());
			var lastBlock = GetTopLevelBlock(selectedTextWidgets.Last());
			var topLevelBlocks = contentPanel.Children.ToList();
			var firstIndex = topLevelBlocks.IndexOf(firstBlock);
			var lastIndex = topLevelBlocks.IndexOf(lastBlock);
			if (firstIndex < 0 || lastIndex < 0)
			{
				return string.Empty;
			}

			var html = new StringBuilder();
			for (int i = firstIndex; i <= lastIndex; i++)
			{
				var block = topLevelBlocks[i];
				var renderWholeBlock = i > firstIndex && i < lastIndex;
				if (ReferenceEquals(firstBlock, lastBlock))
				{
					renderWholeBlock = false;
				}

				html.Append(RenderSelectionNode(block, selectionStart, selectionEnd, renderWholeBlock));
			}

			return html.ToString();
		}

		private string RenderSelectionNode(GuiWidget node, int selectionStart, int selectionEnd, bool renderWholeNode)
		{
			switch (node)
			{
				case MarkdownTextWidget textWidget:
					return RenderSelectedText(textWidget, selectionStart, selectionEnd, renderWholeNode);

				case HeadingRowX heading:
					return WrapIfNotEmpty($"h{heading.Level}", RenderChildren(node, selectionStart, selectionEnd, renderWholeNode));

				case ParagraphX:
					return WrapIfNotEmpty("p", RenderChildren(node, selectionStart, selectionEnd, renderWholeNode));

				case QuoteBlockX:
					return WrapIfNotEmpty("blockquote", RenderChildren(node, selectionStart, selectionEnd, renderWholeNode));

				case CodeInlineX:
					return WrapIfNotEmpty("code", RenderChildren(node, selectionStart, selectionEnd, renderWholeNode));

				case EmphasisInlineX emphasisInline:
					return RenderEmphasisInline(emphasisInline, selectionStart, selectionEnd, renderWholeNode);

				case TextLinkX textLink:
					return RenderLink(textLink, selectionStart, selectionEnd, renderWholeNode);

				case CodeBlockX:
					return RenderCodeBlock(node, selectionStart, selectionEnd, renderWholeNode);

				case ListX list:
					return RenderList(list, selectionStart, selectionEnd, renderWholeNode);

				case AggTable table:
					return RenderTable(table, selectionStart, selectionEnd);

				case ThematicBreakX:
					return renderWholeNode ? "<hr />" : string.Empty;

				case HorizontalLine:
					return renderWholeNode ? "<hr />" : string.Empty;

				default:
					return RenderChildren(node, selectionStart, selectionEnd, renderWholeNode);
			}
		}

		private string RenderChildren(GuiWidget node, int selectionStart, int selectionEnd, bool renderWholeNode)
		{
			var html = new StringBuilder();
			var children = node.Children.ToList();
			for (int i = 0; i < children.Count; i++)
			{
				var child = children[i];
				html.Append(RenderSelectionNode(child, selectionStart, selectionEnd, renderWholeNode));
             if (i + 1 < children.Count)
				{
                   var nextTextSiblingIndex = i + 1;
					while (nextTextSiblingIndex < children.Count
						&& children[nextTextSiblingIndex].DescendantsAndSelf<MarkdownTextWidget>().FirstOrDefault() == null)
					{
						nextTextSiblingIndex++;
					}

					if (nextTextSiblingIndex < children.Count)
					{
						html.Append(RenderSeparatorBetweenChildren(children[i], children[nextTextSiblingIndex], selectionStart, selectionEnd, renderWholeNode));
					}
				}
			}

			return html.ToString();
		}

		private string RenderSeparatorBetweenChildren(GuiWidget currentChild, GuiWidget nextChild, int selectionStart, int selectionEnd, bool renderWholeNode)
		{
			var currentLastText = currentChild.DescendantsAndSelf<MarkdownTextWidget>().LastOrDefault();
			var nextFirstText = nextChild.DescendantsAndSelf<MarkdownTextWidget>().FirstOrDefault();
			if (currentLastText == null || nextFirstText == null)
			{
				return string.Empty;
			}

			if (StartsOrEndsWithWhitespace(currentLastText, nextFirstText))
			{
				return string.Empty;
			}

			var separatorStart = renderWholeNode
				? currentLastText.DocumentTextEndExclusive
				: Math.Max(currentLastText.DocumentTextEndExclusive, selectionStart);
			var separatorEnd = renderWholeNode
				? currentLastText.DocumentEndExclusive
				: Math.Min(currentLastText.DocumentEndExclusive, selectionEnd);
			if (separatorEnd <= separatorStart)
			{
				return string.Empty;
			}

			var separatorOffset = separatorStart - currentLastText.DocumentTextEndExclusive;
			var separatorLength = separatorEnd - separatorStart;
			if (separatorOffset < 0
				|| separatorLength <= 0
				|| separatorOffset + separatorLength > currentLastText.TrailingText.Length)
			{
				return string.Empty;
			}

			return EscapeHtml(currentLastText.TrailingText.Substring(separatorOffset, separatorLength));
		}

		private static bool StartsOrEndsWithWhitespace(MarkdownTextWidget currentLastText, MarkdownTextWidget nextFirstText)
		{
			var currentText = currentLastText.Text;
			if (!string.IsNullOrEmpty(currentText)
				&& char.IsWhiteSpace(currentText[currentText.Length - 1]))
			{
				return true;
			}

			var nextText = nextFirstText.Text;
			if (!string.IsNullOrEmpty(nextText)
				&& char.IsWhiteSpace(nextText[0]))
			{
				return true;
			}

			return false;
		}

		private string RenderSelectedText(MarkdownTextWidget textWidget, int selectionStart, int selectionEnd, bool renderWholeNode)
		{
			if (string.IsNullOrEmpty(textWidget.Text))
			{
				return string.Empty;
			}

			int localStart;
			int localEnd;
			if (renderWholeNode)
			{
				localStart = 0;
				localEnd = textWidget.Text.Length;
			}
			else
			{
				localStart = Math.Max(0, selectionStart - textWidget.DocumentStartIndex);
				localEnd = Math.Min(textWidget.Text.Length, selectionEnd - textWidget.DocumentStartIndex);
				if (localEnd <= localStart)
				{
					return string.Empty;
				}
			}

			var html = EscapeHtml(textWidget.Text.Substring(localStart, localEnd - localStart));
			if (string.IsNullOrEmpty(html))
			{
				return string.Empty;
			}

			if (textWidget.StrikeThrough)
			{
				html = $"<del>{html}</del>";
			}

			if (textWidget.Bold
				&& !HasAncestor<HeadingRowX>(textWidget)
				&& !IsTableHeaderText(textWidget))
			{
				html = $"<strong>{html}</strong>";
			}

			return html;
		}

		private string RenderEmphasisInline(EmphasisInlineX emphasisInline, int selectionStart, int selectionEnd, bool renderWholeNode)
		{
			var innerHtml = RenderChildren(emphasisInline, selectionStart, selectionEnd, renderWholeNode);
			if (string.IsNullOrEmpty(innerHtml))
			{
				return string.Empty;
			}

			return emphasisInline.Delimiter == '~'
				? $"<del>{innerHtml}</del>"
				: $"<strong>{innerHtml}</strong>";
		}

		private string RenderLink(TextLinkX textLink, int selectionStart, int selectionEnd, bool renderWholeNode)
		{
			var innerHtml = RenderChildren(textLink, selectionStart, selectionEnd, renderWholeNode);
			if (string.IsNullOrEmpty(innerHtml))
			{
				return string.Empty;
			}

			var href = WebUtility.HtmlEncode(textLink.Url ?? string.Empty);
			return $"<a href=\"{href}\">{innerHtml}</a>";
		}

		private string RenderCodeBlock(GuiWidget codeBlock, int selectionStart, int selectionEnd, bool renderWholeNode)
		{
			var lines = codeBlock.Children
				.Select(child => RenderSelectionNode(child, selectionStart, selectionEnd, renderWholeNode))
				.Where(line => !string.IsNullOrEmpty(line))
				.ToList();
			if (lines.Count == 0)
			{
				return string.Empty;
			}

			return $"<pre><code>{string.Join("\n", lines)}</code></pre>";
		}

		private string RenderList(ListX list, int selectionStart, int selectionEnd, bool renderWholeNode)
		{
			var items = list.Children
				.OfType<ListItemX>()
				.Where(item => renderWholeNode || NodeHasSelectedText(item, selectionStart, selectionEnd))
				.ToList();
			if (items.Count == 0)
			{
				return string.Empty;
			}

			var tagName = IsOrderedList(items.First()) ? "ol" : "ul";
			var html = new StringBuilder();
			foreach (var item in items)
			{
				html.Append(RenderListItem(item, selectionStart, selectionEnd, renderWholeNode));
			}

			return $"<{tagName}>{html}</{tagName}>";
		}

		private string RenderListItem(ListItemX item, int selectionStart, int selectionEnd, bool renderWholeNode)
		{
			var contentHtml = new StringBuilder();
			foreach (var child in item.Children.Skip(1))
			{
				contentHtml.Append(RenderSelectionNode(child, selectionStart, selectionEnd, renderWholeNode));
			}

			return string.IsNullOrEmpty(contentHtml.ToString())
				? string.Empty
				: $"<li>{contentHtml}</li>";
		}

		private string RenderTable(AggTable table, int selectionStart, int selectionEnd)
		{
			var rows = table.Children.OfType<AggTableRow>().ToList();
			if (rows.Count == 0 || !rows.Any(row => NodeHasSelectedText(row, selectionStart, selectionEnd)))
			{
				return string.Empty;
			}

			var html = new StringBuilder();
			var headerRows = rows.Where(row => row.IsHeadingRow).ToList();
			var bodyRows = rows.Where(row => !row.IsHeadingRow).ToList();

			html.Append("<table>");
			if (headerRows.Count > 0)
			{
				html.Append("<thead>");
				foreach (var row in headerRows)
				{
					html.Append(RenderTableRow(row, "th"));
				}
				html.Append("</thead>");
			}

			if (bodyRows.Count > 0)
			{
				html.Append("<tbody>");
				foreach (var row in bodyRows)
				{
					html.Append(RenderTableRow(row, "td"));
				}
				html.Append("</tbody>");
			}

			html.Append("</table>");
			return html.ToString();
		}

		private string RenderTableRow(AggTableRow row, string cellTagName)
		{
			var html = new StringBuilder();
			html.Append("<tr>");
			foreach (var cell in row.Cells)
			{
				html.Append('<').Append(cellTagName);
				var alignment = GetTableCellAlignment(cell);
				if (!string.IsNullOrEmpty(alignment))
				{
					html.Append(" align=\"").Append(alignment).Append('"');
				}
				html.Append('>');
				html.Append(RenderSelectionNode(cell, 0, int.MaxValue, true));
				html.Append("</").Append(cellTagName).Append('>');
			}
			html.Append("</tr>");
			return html.ToString();
		}

		private static string GetTableCellAlignment(AggTableCell cell)
		{
			if ((cell.FlowHAnchor & HAnchor.Right) == HAnchor.Right)
			{
				return "right";
			}

			if ((cell.FlowHAnchor & HAnchor.Center) == HAnchor.Center)
			{
				return "center";
			}

			if ((cell.FlowHAnchor & HAnchor.Left) == HAnchor.Left)
			{
				return "left";
			}

			return null;
		}

		private bool NodeHasSelectedText(GuiWidget node, int selectionStart, int selectionEnd)
		{
			return node.DescendantsAndSelf<MarkdownTextWidget>()
				.Any(textWidget => TextWidgetOverlapsSelection(textWidget, selectionStart, selectionEnd));
		}

		private GuiWidget GetTopLevelBlock(GuiWidget widget)
		{
			var current = widget;
			while (current?.Parent != null && !ReferenceEquals(current.Parent, contentPanel))
			{
				current = current.Parent;
			}

			return current ?? widget;
		}

		private static bool TextWidgetOverlapsSelection(MarkdownTextWidget textWidget, int selectionStart, int selectionEnd)
		{
			return selectionEnd > textWidget.DocumentStartIndex && selectionStart < textWidget.DocumentTextEndExclusive;
		}

		private static bool IsOrderedList(ListItemX item)
		{
			var markerText = item.Children.OfType<TextWidget>().FirstOrDefault()?.Text?.Trim();
			if (string.IsNullOrWhiteSpace(markerText) || !markerText.EndsWith(".", StringComparison.Ordinal))
			{
				return false;
			}

			var markerWithoutPeriod = markerText.Substring(0, markerText.Length - 1);
			return markerWithoutPeriod.Length > 0 && markerWithoutPeriod.All(char.IsDigit);
		}

		private static bool HasAncestor<T>(GuiWidget widget)
			where T : GuiWidget
		{
			var current = widget.Parent;
			while (current != null)
			{
				if (current is T)
				{
					return true;
				}

				current = current.Parent;
			}

			return false;
		}

		private static bool IsTableHeaderText(GuiWidget widget)
		{
			var row = widget.Parents<AggTableRow>().FirstOrDefault();
			return row?.IsHeadingRow == true;
		}

		private static string WrapIfNotEmpty(string tagName, string innerHtml)
		{
			return string.IsNullOrEmpty(innerHtml)
				? string.Empty
				: $"<{tagName}>{innerHtml}</{tagName}>";
		}

		private bool TryGetDocumentIndexForPoint(Vector2 point, out int selectionIndex)
		{
			if (!orderedTextWidgets.Any())
			{
				selectionIndex = 0;
				return false;
			}

			foreach (var textWidget in orderedTextWidgets)
			{
				var bounds = textWidget.TransformToParentSpace(this, textWidget.LocalBounds);
				if (bounds.Contains(point))
				{
					selectionIndex = GetDocumentIndexAtWidgetPoint(textWidget, point);
					return true;
				}
			}

			selectionIndex = 0;
			return false;
		}

		private bool TryGetDocumentIndexNearPoint(Vector2 point, out int selectionIndex)
		{
			if (TryGetDocumentIndexForPoint(point, out selectionIndex))
			{
				return true;
			}

			if (!orderedTextWidgets.Any())
			{
				selectionIndex = 0;
				return false;
			}

			var nearest = orderedTextWidgets
				.OrderBy(textWidget => DistanceToBounds(textWidget.TransformToParentSpace(this, textWidget.LocalBounds), point))
				.First();
			selectionIndex = GetDocumentIndexAtWidgetPoint(nearest, point);
			return true;
		}

		private static double DistanceToBounds(RectangleDouble bounds, Vector2 point)
		{
			var x = Math.Max(bounds.Left, Math.Min(point.X, bounds.Right));
			var y = Math.Max(bounds.Bottom, Math.Min(point.Y, bounds.Top));
			return new Vector2(point.X - x, point.Y - y).LengthSquared;
		}

		private int GetDocumentIndexAtWidgetPoint(MarkdownTextWidget textWidget, Vector2 point)
		{
			var localPoint = textWidget.TransformFromParentSpace(this, point);
			var localIndex = textWidget.Printer.GetCharacterIndexToStartBefore(localPoint);
			if (localIndex < 0)
			{
				localIndex = localPoint.X >= textWidget.LocalBounds.Right
					? textWidget.Text.Length
					: 0;
			}

			return Math.Max(textWidget.DocumentStartIndex, Math.Min(textWidget.DocumentStartIndex + localIndex, textWidget.DocumentTextEndExclusive));
		}

		private static IEnumerable<MarkdownTextWidget> EnumerateMarkdownTextWidgets(GuiWidget widget)
		{
			if (widget is MarkdownTextWidget markdownTextWidget)
			{
				yield return markdownTextWidget;
			}

			foreach (var child in widget.Children)
			{
				foreach (var descendant in EnumerateMarkdownTextWidgets(child))
				{
					yield return descendant;
				}
			}
		}

		private static string GetSeparatorBetween(MarkdownTextWidget current, MarkdownTextWidget next)
		{
			if (next == null)
			{
				return string.Empty;
			}

			var currentBlock = GetBlockInfo(current);
			var nextBlock = GetBlockInfo(next);
			if (ReferenceEquals(currentBlock.Widget, nextBlock.Widget))
			{
				if (currentBlock.Kind == MarkdownBlockKind.Code
					&& ReferenceEquals(current.Parent, currentBlock.Widget)
					&& ReferenceEquals(next.Parent, nextBlock.Widget))
				{
					return "\n";
				}

				if (currentBlock.Kind == MarkdownBlockKind.ListItem
					&& ReferenceEquals(current.Parent, currentBlock.Widget)
					&& !ReferenceEquals(next.Parent, current.Parent))
				{
					return " ";
				}

				return NeedsInlineSeparator(current.Text, next.Text) ? " " : string.Empty;
			}

			return "\n";
		}

		private static MarkdownBlockInfo GetBlockInfo(GuiWidget widget)
		{
			GuiWidget current = widget;
			while (current != null)
			{
				switch (current)
				{
					case HeadingRowX heading:
						return new MarkdownBlockInfo(current, MarkdownBlockKind.Heading, heading.Level);

					case CodeBlockX:
						return new MarkdownBlockInfo(current, MarkdownBlockKind.Code, 0);

					case ListItemX:
						return new MarkdownBlockInfo(current, MarkdownBlockKind.ListItem, 0);

					case AggTableCell:
						return new MarkdownBlockInfo(current, MarkdownBlockKind.TableCell, 0);

					case ParagraphX:
						return new MarkdownBlockInfo(current, MarkdownBlockKind.Paragraph, 0);
				}

				current = current.Parent;
			}

			return new MarkdownBlockInfo(widget, MarkdownBlockKind.Paragraph, 0);
		}

		private static string EscapeHtml(string text)
		{
			return WebUtility.HtmlEncode(text ?? string.Empty)
				.Replace("\r\n", "\n", StringComparison.Ordinal)
				.Replace("\n", "<br/>", StringComparison.Ordinal);
		}

		private static string StripListMarker(string text)
		{
			var trimmed = (text ?? string.Empty).TrimStart();
			if (trimmed.StartsWith("-", StringComparison.Ordinal))
			{
				return trimmed.Substring(1).TrimStart();
			}

			var markerEnd = trimmed.IndexOf('.');
			if (markerEnd > 0 && trimmed.Take(markerEnd).All(char.IsDigit))
			{
				return trimmed.Substring(markerEnd + 1).TrimStart();
			}

			return trimmed;
		}

		private static bool NeedsInlineSeparator(string currentText, string nextText)
		{
			if (string.IsNullOrEmpty(currentText) || string.IsNullOrEmpty(nextText))
			{
				return false;
			}

			var currentChar = currentText[currentText.Length - 1];
			var nextChar = nextText[0];
			return char.IsLetterOrDigit(currentChar) && char.IsLetterOrDigit(nextChar);
		}

		private enum MarkdownBlockKind
		{
			Paragraph,
			Heading,
			Code,
			ListItem,
			TableCell
		}

		private sealed class SelectedBlockFragment
		{
			public SelectedBlockFragment(MarkdownBlockInfo blockInfo)
			{
				BlockInfo = blockInfo;
			}

			public MarkdownBlockInfo BlockInfo { get; }

			public StringBuilder Text { get; } = new StringBuilder();
		}

		private readonly struct MarkdownBlockInfo
		{
			public MarkdownBlockInfo(GuiWidget widget, MarkdownBlockKind kind, int headingLevel)
			{
				Widget = widget;
				Kind = kind;
				HeadingLevel = headingLevel;
			}

			public GuiWidget Widget { get; }

			public MarkdownBlockKind Kind { get; }

			public int HeadingLevel { get; }
		}
	}
}
