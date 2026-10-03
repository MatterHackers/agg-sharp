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

using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// The URL a link edit starts from: the link at the caret or selection, or null when there is none.
	/// </summary>
	public class MarkdownLinkRequestEventArgs : EventArgs
	{
		public MarkdownLinkRequestEventArgs(string currentUrl)
		{
			CurrentUrl = currentUrl;
		}

		public string CurrentUrl { get; }
	}

	/// <summary>
	/// The rich markdown editor's formatting strip: character styles, text size, alignment, lists and quote,
	/// then link, code block and table, each run of related buttons an outlined <see cref="MarkdownFormatGroup"/> of
	/// Font Awesome icons. It wraps onto more rows when narrow, a whole group at a time. Every button acts through
	/// <see cref="IRichEditCommands"/> (see that interface for the seam) and shows the state at the caret or
	/// selection: a style button is pressed when all the selection has the style and shaded when only part does,
	/// the size list is blank over mixed sizes, and alignment is disabled in lists and quotes, where it is not
	/// offered.
	/// </summary>
	public class MarkdownFormatToolbar : FlowLayoutWidget
	{
		private static readonly string[] SizeNames = { "Normal", "Heading 1", "Heading 2", "Heading 3" };

		private readonly ThemeConfig theme;

		private readonly FlowLeftRightWithWrapping strip;

		private readonly List<MarkdownFormatGroup> groups = new List<MarkdownFormatGroup>();

		private IRichEditCommands commands;

		/// <summary>
		/// True while <see cref="Refresh"/> sets the size list, whose SelectionChanged must not read as a user pick.
		/// </summary>
		private bool refreshing;

		/// <summary>
		/// True while a toolbar edit runs: its DocumentChanged / SelectionChanged pair is answered by one Refresh
		/// after it, not one per event.
		/// </summary>
		private bool editing;

		/// <summary>
		/// The selection the link row opened on. The row's box takes the focus, and the link must land on the text
		/// that was selected when the user pressed Link.
		/// </summary>
		private RichSelection linkSelection;

		/// <summary>
		/// The heading level of the extra size-list entry ("Heading 4" - "Heading 6"), or 0 when there is none. Those
		/// levels are shown so the list never goes blank on a real heading, but not offered for new headings.
		/// </summary>
		private int extraSizeLevel;

		public MarkdownFormatToolbar(ThemeConfig theme, IRichEditCommands commands)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			HAnchor = HAnchor.Stretch;
			VAnchor = VAnchor.Fit;

			AddChild(strip = new FlowLeftRightWithWrapping
			{
				HAnchor = HAnchor.Stretch,
				RowPadding = new BorderDouble(0, 1),
				RowMargin = new BorderDouble(0),
			});

			var styles = AddGroup();
			BoldButton = AddStyleButton(styles, IconFont.Bold, RichInlineStyle.Bold, "Bold (" + Shortcut("B") + ")");
			ItalicButton = AddStyleButton(styles, IconFont.Italic, RichInlineStyle.Italic, "Italic (" + Shortcut("I") + ")");
			StrikeButton = AddStyleButton(styles, IconFont.Strikethrough, RichInlineStyle.Strike, "Strikethrough");
			CodeButton = AddStyleButton(styles, IconFont.Code, RichInlineStyle.Code, "Inline code");

			// Blank when the selection spans different sizes, rather than claiming one of them. It stands on its own
			// between the groups, as tall as they are (its border included) and spaced like them.
			SizeList = new DropDownList("", theme.TextColor, pointSize: theme.DefaultFontSize)
			{
				ToolTipText = "Text size",
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(MarkdownFormatGroup.DesignSpacing / 2, 0),
			};
			foreach (var name in SizeNames)
			{
				SizeList.AddItem(name);
			}

			// As tall as a group, border included. Set after the items: adding one raises the list's minimum to its
			// size at the time, which starts out a few units taller than a group.
			SizeList.MinimumSize = new Vector2(100 * DeviceScale, GroupHeight - SizeList.DeviceBorder.Height);
			SizeList.Height = SizeList.MinimumSize.Y;

			SizeList.SelectionChanged += (s, e) =>
			{
				if (!refreshing && SizeList.SelectedIndex >= 0)
				{
					int level = SizeList.SelectedIndex < SizeNames.Length ? SizeList.SelectedIndex : extraSizeLevel;
					Edit((d, sel) =>
					{
						RichBlockOperations.SetBlockKind(d, sel, level);
						return sel;
					});
				}
			};
			strip.AddChild(SizeList);

			var alignment = AddGroup();
			AlignLeftButton = AddAlignButton(alignment, IconFont.AlignLeft, RichAlignment.Left, "Align left");
			AlignCenterButton = AddAlignButton(alignment, IconFont.AlignCenter, RichAlignment.Center, "Center");
			AlignRightButton = AddAlignButton(alignment, IconFont.AlignRight, RichAlignment.Right, "Align right");

			var blocks = AddGroup();
			BulletListButton = AddBlockButton(blocks, IconFont.ListUl, "Bulleted list", (d, s) => RichBlockOperations.ToggleList(d, s, ordered: false));
			NumberedListButton = AddBlockButton(blocks, IconFont.ListOl, "Numbered list", (d, s) => RichBlockOperations.ToggleList(d, s, ordered: true));
			QuoteButton = AddBlockButton(blocks, IconFont.QuoteLeft, "Quote", RichBlockOperations.ToggleQuote);

			var inserts = AddGroup();
			LinkButton = AddButton(inserts, new MarkdownFormatButton(theme, IconFont.Link, "Add or edit a link"), ShowLinkEditor);
			CodeBlockButton = AddButton(
				inserts,
				new MarkdownFormatButton(theme, IconFont.FileCode, "Code block"),
				() => Edit(RichCodeBlockOperations.ToggleCodeBlock));
			TableButton = AddButton(
				inserts,
				new MarkdownFormatButton(theme, IconFont.Table, "Insert a table"),
				() => Edit((d, s) => RichTableCodeOperations.InsertTable(d, s.End, bodyRows: 1, columns: 2)));

			AddChild(LinkEditor = new MarkdownLinkEditor(theme)
			{
				Visible = false,
			});
			LinkEditor.Accepted += (s, url) => ApplyLink(url, linkSelection);
			LinkEditor.Removed += (s, e) => Edit(RichStyleOperations.RemoveLink, linkSelection);
			LinkEditor.Cancelled += (s, e) => commands?.FocusText();

			Commands = commands;
		}

		/// <summary>
		/// Raised by the Link button when a host handles link editing itself (its own dialog), with the current
		/// link's URL; the host then calls <see cref="ApplyLink"/> or <see cref="RemoveLink"/>. With no handler the
		/// toolbar shows its own <see cref="LinkEditor"/> row instead.
		/// </summary>
		public event EventHandler<MarkdownLinkRequestEventArgs> LinkRequested;

		/// <summary>
		/// The outlined button groups, left to right: character styles, alignment, lists and quote, then link, code
		/// block and table. The size list stands alone between the first two.
		/// </summary>
		public IReadOnlyList<MarkdownFormatGroup> Groups => groups;

		public MarkdownFormatButton BoldButton { get; }

		public MarkdownFormatButton ItalicButton { get; }

		public MarkdownFormatButton StrikeButton { get; }

		public MarkdownFormatButton CodeButton { get; }

		public DropDownList SizeList { get; }

		public MarkdownFormatButton AlignLeftButton { get; }

		public MarkdownFormatButton AlignCenterButton { get; }

		public MarkdownFormatButton AlignRightButton { get; }

		public MarkdownFormatButton BulletListButton { get; }

		public MarkdownFormatButton NumberedListButton { get; }

		public MarkdownFormatButton QuoteButton { get; }

		public MarkdownFormatButton LinkButton { get; }

		public MarkdownFormatButton CodeBlockButton { get; }

		public MarkdownFormatButton TableButton { get; }

		/// <summary>
		/// The built-in URL row under the strip, shown by the Link button when nobody handles
		/// <see cref="LinkRequested"/>.
		/// </summary>
		public MarkdownLinkEditor LinkEditor { get; }

		/// <summary>
		/// What the toolbar edits. Can be replaced (for example when the editor is rebuilt); the toolbar follows
		/// the new commands' events and stops listening to the old ones.
		/// </summary>
		public IRichEditCommands Commands
		{
			get => commands;
			set
			{
				if (commands != null)
				{
					commands.SelectionChanged -= Commands_SelectionChanged;
					commands.DocumentChanged -= Commands_DocumentChanged;
				}

				// The row's captured selection belongs to the old document.
				LinkEditor.Close();
				commands = value;
				if (commands != null)
				{
					commands.SelectionChanged += Commands_SelectionChanged;
					commands.DocumentChanged += Commands_DocumentChanged;
				}

				Refresh();
			}
		}

		/// <summary>
		/// A tooltip's shortcut spelled in words ("Command+B" on a Mac, "Ctrl+B" elsewhere): the UI font has no
		/// modifier-key symbols.
		/// </summary>
		public static string Shortcut(string key) => (InternalTextEditWidget.UseMacKeyBindings ? "Command+" : "Ctrl+") + key;

		/// <summary>
		/// Links the selection (or the link at the caret) to <paramref name="url"/>; an empty URL removes the link,
		/// as clearing the box in a link dialog does elsewhere.
		/// </summary>
		public void ApplyLink(string url) => ApplyLink(url, null);

		public void RemoveLink() => Edit(RichStyleOperations.RemoveLink);

		/// <summary>
		/// Brings every control in line with the caret or selection. Runs on each selection and document change.
		/// </summary>
		public void Refresh()
		{
			bool hasDocument = commands?.Document != null && commands.Document.Blocks.Count > 0;
			var document = commands?.Document;
			var selection = hasDocument ? Clamp(document, commands.Selection) : default;

			var style = hasDocument ? RichStyleOperations.StyleAt(document, selection, commands.PendingStyle) : default;
			var block = hasDocument ? RichBlockOperations.BlockStateAt(document, selection) : default;
			bool styled = hasDocument && Touches(document, selection, tables: true);
			bool textBlocks = hasDocument && Touches(document, selection, tables: false);

			foreach (var (button, flag) in new[]
			{
				(BoldButton, RichInlineStyle.Bold),
				(ItalicButton, RichInlineStyle.Italic),
				(StrikeButton, RichInlineStyle.Strike),
				(CodeButton, RichInlineStyle.Code),
			})
			{
				button.Enabled = styled;
				button.Coverage = styled ? style.Coverage(flag) : RichStyleCoverage.None;
			}

			refreshing = true;
			try
			{
				SizeList.Enabled = textBlocks;
				ShowSize(textBlocks ? block.HeadingLevel : 0);
			}
			finally
			{
				refreshing = false;
			}

			foreach (var (button, alignment) in new[]
			{
				(AlignLeftButton, RichAlignment.Left),
				(AlignCenterButton, RichAlignment.Center),
				(AlignRightButton, RichAlignment.Right),
			})
			{
				button.Enabled = block.CanAlign;
				button.Coverage = block.CanAlign && block.Alignment == alignment ? RichStyleCoverage.All : RichStyleCoverage.None;
			}

			BulletListButton.Coverage = ListCoverage(block.List, RichListKind.Bullet);
			NumberedListButton.Coverage = ListCoverage(block.List, RichListKind.Numbered);
			QuoteButton.Coverage = block.Quote ? RichStyleCoverage.All : RichStyleCoverage.None;
			BulletListButton.Enabled = NumberedListButton.Enabled = QuoteButton.Enabled = textBlocks;

			LinkButton.Enabled = styled || (hasDocument && selection.IsEmpty && RichStyleOperations.TypingStyle(document, selection.Caret) != null);
			LinkButton.Coverage = style.LinkUrl != null ? RichStyleCoverage.All : RichStyleCoverage.None;
			CodeBlockButton.Enabled = hasDocument && RichCodeBlockOperations.CanToggleCodeBlock(document, selection);
			CodeBlockButton.Coverage = hasDocument && document.Blocks[selection.Start.BlockIndex].Kind == RichBlockKind.CodeBlock
				? RichStyleCoverage.All : RichStyleCoverage.None;
			TableButton.Enabled = hasDocument;
		}

		public override void OnClosed(EventArgs e)
		{
			Commands = null;
			base.OnClosed(e);
		}

		/// <summary>
		/// The Link button: a host's dialog if it asked for one, otherwise the built-in URL row.
		/// </summary>
		public void ShowLinkEditor()
		{
			if (commands?.Document == null || commands.Document.Blocks.Count == 0)
			{
				return;
			}

			linkSelection = Clamp(commands.Document, commands.Selection);
			var url = RichStyleOperations.StyleAt(commands.Document, linkSelection).LinkUrl;
			if (LinkRequested != null)
			{
				LinkRequested(this, new MarkdownLinkRequestEventArgs(url));
				return;
			}

			LinkEditor.Open(url);
		}

		/// <summary>
		/// A list button shows mixed when the selection holds some items but not all of that kind.
		/// </summary>
		private static RichStyleCoverage ListCoverage(RichListKind state, RichListKind kind)
		{
			return state == kind ? RichStyleCoverage.All
				: state == RichListKind.Mixed ? RichStyleCoverage.Mixed
				: RichStyleCoverage.None;
		}

		/// <summary>
		/// Whether the selection touches a paragraph-like block (or, with <paramref name="tables"/>, a table cell).
		/// Code and Raw blocks have no character or block styles, so those buttons are disabled there rather than
		/// doing nothing when pressed.
		/// </summary>
		private static bool Touches(RichDocument document, RichSelection selection, bool tables)
		{
			for (int i = selection.Start.BlockIndex; i <= selection.End.BlockIndex && i < document.Blocks.Count; i++)
			{
				var block = document.Blocks[i];
				if (block.IsTextBlock || (tables && block.Kind == RichBlockKind.Table))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// A selection kept inside the document, so a stale one (an implementation that raised its events before
		/// storing the new selection, or a document replaced under us) never indexes past the blocks.
		/// </summary>
		private static RichSelection Clamp(RichDocument document, RichSelection selection)
		{
			DocPosition ClampPosition(DocPosition position)
			{
				int index = Math.Clamp(position.BlockIndex, 0, document.Blocks.Count - 1);
				var block = document.Blocks[index];
				int row = 0;
				int column = 0;
				if (block.Kind == RichBlockKind.Table)
				{
					if (block.TableRows.Count == 0 || block.TableRows[0].Count == 0)
					{
						return new DocPosition(index, 0);
					}

					row = Math.Clamp(position.Row, 0, block.TableRows.Count - 1);
					column = Math.Clamp(position.Column, 0, block.TableRows[row].Count - 1);
				}

				return new DocPosition(index, Math.Clamp(position.Offset, 0, block.TextLength(row, column)), row, column);
			}

			return selection with { Anchor = ClampPosition(selection.Anchor), Caret = ClampPosition(selection.Caret) };
		}

		private void Commands_SelectionChanged(object sender, EventArgs e)
		{
			if (!editing)
			{
				// The caret moved under an open link row (a click in the text): the row no longer matches it.
				LinkEditor.Close();
				Refresh();
			}
		}

		private void Commands_DocumentChanged(object sender, EventArgs e)
		{
			if (!editing)
			{
				Refresh();
			}
		}

		private void ApplyLink(string url, RichSelection? on)
		{
			url = MarkdownLinkEditor.WithScheme(url);
			if (string.IsNullOrEmpty(url))
			{
				Edit(RichStyleOperations.RemoveLink, on);
				return;
			}

			Edit((d, s) => RichStyleOperations.SetLink(d, s, url), on);
		}

		/// <summary>
		/// Runs one toolbar edit through the seam on <paramref name="on"/> (or the current selection), reporting a
		/// change only when the markdown it writes differs, so a click that did nothing leaves no undo step. Writing
		/// the document twice costs little next to the relayout every edit does. Refreshes once afterwards, rather
		/// than once per event the edit raises, and hands the focus back to the text.
		/// </summary>
		private void Edit(Func<RichDocument, RichSelection, RichSelection> edit, RichSelection? on = null)
		{
			if (commands?.Document == null || commands.Document.Blocks.Count == 0)
			{
				return;
			}

			editing = true;
			try
			{
				commands.ApplyEdit((document, selection) =>
				{
					string before = RichMarkdownWriter.Write(document);
					var next = edit(document, Clamp(document, on ?? selection));
					return (next, RichMarkdownWriter.Write(document) != before);
				});
			}
			finally
			{
				editing = false;
			}

			Refresh();
			commands.FocusText();
		}

		/// <summary>
		/// Selects the size list's entry for <paramref name="level"/>: blank for null (mixed sizes), and a
		/// "Heading N" entry added for levels 4-6 so a real heading never reads as blank.
		/// </summary>
		private void ShowSize(int? level)
		{
			int extra = level is int deep && deep >= SizeNames.Length ? deep : 0;
			if (extra != extraSizeLevel)
			{
				SizeList.SelectedIndex = -1;
				if (extraSizeLevel != 0)
				{
					SizeList.MenuItems.RemoveAt(SizeNames.Length);
				}

				if (extra != 0)
				{
					SizeList.AddItem("Heading " + extra);
				}

				extraSizeLevel = extra;
			}

			SizeList.SelectedIndex = level is int shown ? Math.Min(shown, SizeNames.Length) : -1;
		}

		private void ToggleStyle(RichInlineStyle style)
		{
			if (commands == null)
			{
				return;
			}

			// With nothing selected the style applies to what is typed next, as in any word processor.
			if (commands.Selection.IsEmpty)
			{
				// Setting it raises SelectionChanged, which refreshes the buttons.
				commands.PendingStyle = commands.PendingStyle.Toggle(style);
				commands.FocusText();
				return;
			}

			Edit((d, s) => RichStyleOperations.ToggleStyle(d, s, style));
		}

		/// <summary>
		/// A group's outer height in device pixels: a button plus the outline above and below it, or the theme's
		/// field height when that is taller. The size list is at least that field height whatever it is asked
		/// for (DropDownList reads it from ThemeConfig.Current), so the groups grow to it to stay level with the
		/// list; their icons stay the same size, centred in the taller buttons.
		/// </summary>
		public static double GroupHeight => Math.Max(MarkdownFormatButton.DesignHeight + 2, ThemeConfig.Current.FieldDesignHeight) * DeviceScale;

		private MarkdownFormatButton AddStyleButton(MarkdownFormatGroup group, string icon, RichInlineStyle style, string toolTip)
		{
			return AddButton(group, new MarkdownFormatButton(theme, icon, toolTip), () => ToggleStyle(style));
		}

		private MarkdownFormatButton AddAlignButton(MarkdownFormatGroup group, string icon, RichAlignment alignment, string toolTip)
		{
			return AddButton(group, new MarkdownFormatButton(theme, icon, toolTip), () => Edit((d, s) =>
			{
				RichBlockOperations.SetAlignment(d, s, alignment);
				return s;
			}));
		}

		private MarkdownFormatButton AddBlockButton(MarkdownFormatGroup group, string icon, string toolTip, Action<RichDocument, RichSelection> op)
		{
			return AddButton(group, new MarkdownFormatButton(theme, icon, toolTip), () => Edit((d, s) =>
			{
				op(d, s);
				return s;
			}));
		}

		private MarkdownFormatButton AddButton(MarkdownFormatGroup group, MarkdownFormatButton button, Action click)
		{
			button.Click += (s, e) => click();
			button.Height = GroupHeight - 2 * DeviceScale;
			return group.AddButton(button);
		}

		/// <summary>
		/// A new outlined group at the end of the strip. Each group is one item of the wrapping strip, so it wraps
		/// whole and the strip's ContentWidth (which a host sizes its window by) counts it as one.
		/// </summary>
		private MarkdownFormatGroup AddGroup()
		{
			var group = new MarkdownFormatGroup(theme);
			groups.Add(group);
			strip.AddChild(group);
			return group;
		}
	}
}
