/*
Copyright (c) 2026, Lars Brubaker
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
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
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
using System.Linq;
using MatterHackers.Agg.Font;

namespace MatterHackers.Agg.UI.RichText
{
	public enum RichCommandKind
	{
		ToggleBold,
		ToggleItalic,
		ToggleUnderline,
		ToggleStrikethrough,
		SetFontFamily,
		SetFontSize,
		SetTextColor,
		SetHighlight,
		SetAlign,
		SetList,
		Indent,
		Outdent,
	}

	/// <summary>
	/// One formatting command (agg-gui's RichCommand). Build them with the static members; only the payload field
	/// that matches <see cref="Kind"/> is meaningful. Undo is not a command - <see cref="RichEditCore"/> snapshots the
	/// document instead, so commands stay plain forward mutations.
	/// </summary>
	public sealed record RichCommand(RichCommandKind Kind)
	{
		public static RichCommand ToggleBold { get; } = new RichCommand(RichCommandKind.ToggleBold);

		public static RichCommand ToggleItalic { get; } = new RichCommand(RichCommandKind.ToggleItalic);

		public static RichCommand ToggleUnderline { get; } = new RichCommand(RichCommandKind.ToggleUnderline);

		public static RichCommand ToggleStrikethrough { get; } = new RichCommand(RichCommandKind.ToggleStrikethrough);

		public static RichCommand Indent { get; } = new RichCommand(RichCommandKind.Indent);

		public static RichCommand Outdent { get; } = new RichCommand(RichCommandKind.Outdent);

		public string FontFamily { get; init; }

		public double FontSize { get; init; }

		/// <summary>Gets the colour for SetTextColor and SetHighlight; null clears a highlight.</summary>
		public Color? Color { get; init; }

		public Justification Align { get; init; }

		public ListKind List { get; init; }

		/// <summary>Gets a value indicating whether this formats characters (as opposed to whole paragraphs).</summary>
		public bool IsInline => this.Kind <= RichCommandKind.SetHighlight;

		public static RichCommand SetFontFamily(string family) => new RichCommand(RichCommandKind.SetFontFamily) { FontFamily = family };

		public static RichCommand SetFontSize(double size) => new RichCommand(RichCommandKind.SetFontSize) { FontSize = size };

		public static RichCommand SetTextColor(Color color) => new RichCommand(RichCommandKind.SetTextColor) { Color = color };

		public static RichCommand SetHighlight(Color? color) => new RichCommand(RichCommandKind.SetHighlight) { Color = color };

		public static RichCommand SetAlign(Justification align) => new RichCommand(RichCommandKind.SetAlign) { Align = align };

		/// <summary>Sets the list kind of every touched paragraph, or turns it off when they all already have it.</summary>
		public static RichCommand SetList(ListKind list) => new RichCommand(RichCommandKind.SetList) { List = list };

		/// <summary>Folds an inline command into one style: toggles flip, setters overwrite. Block commands pass through.</summary>
		public InlineStyle ApplyTo(InlineStyle style)
		{
			return this.Kind switch
			{
				RichCommandKind.ToggleBold => style with { Bold = !style.Bold },
				RichCommandKind.ToggleItalic => style with { Italic = !style.Italic },
				RichCommandKind.ToggleUnderline => style with { Underline = !style.Underline },
				RichCommandKind.ToggleStrikethrough => style with { Strikethrough = !style.Strikethrough },
				RichCommandKind.SetFontFamily => style with { FontFamily = this.FontFamily },
				RichCommandKind.SetFontSize => style with { FontSize = this.FontSize },
				RichCommandKind.SetTextColor => style with { TextColor = this.Color },
				RichCommandKind.SetHighlight => style with { Highlight = this.Color },
				_ => style,
			};
		}
	}

	/// <summary>
	/// What a selection's runs agree on, for toolbar state. Each Is* flag is null when the runs disagree (mixed).
	/// For the inheritable attributes, <c>Has*</c> false means mixed; otherwise the value is the shared one (null =
	/// all inherit the default).
	/// </summary>
	public sealed class CommonStyle
	{
		public bool? Bold { get; set; }

		public bool? Italic { get; set; }

		public bool? Underline { get; set; }

		public bool? Strikethrough { get; set; }

		public bool FontFamilyAgrees { get; set; } = true;

		public string FontFamily { get; set; }

		public bool FontSizeAgrees { get; set; } = true;

		public double? FontSize { get; set; }

		public bool TextColorAgrees { get; set; } = true;

		public Color? TextColor { get; set; }

		public bool HighlightAgrees { get; set; } = true;

		public Color? Highlight { get; set; }

		/// <summary>Gets or sets the alignment every touched paragraph shares, or null when mixed.</summary>
		public Justification? Align { get; set; }

		/// <summary>Gets or sets the list kind every touched paragraph shares, or null when mixed.</summary>
		public ListKind? List { get; set; }

		/// <summary>The summary of a single uniform style (e.g. the pending caret style), with no block attributes.</summary>
		public static CommonStyle Of(InlineStyle style)
		{
			return new CommonStyle()
			{
				Bold = style.Bold,
				Italic = style.Italic,
				Underline = style.Underline,
				Strikethrough = style.Strikethrough,
				FontFamily = style.FontFamily,
				FontSize = style.FontSize,
				TextColor = style.TextColor,
				Highlight = style.Highlight,
			};
		}

		/// <summary>Folds the alignment and list kind of every paragraph <paramref name="range"/> touches in.</summary>
		public void MergeBlocks(RichDoc doc, DocRange range)
		{
			bool first = true;
			for (int i = range.Min.Block; i <= range.Max.Block && i < doc.Blocks.Count; i++)
			{
				var block = doc.Blocks[i];
				if (first)
				{
					this.Align = block.Align;
					this.List = block.List;
					first = false;
				}
				else
				{
					this.Align = this.Align == block.Align ? this.Align : null;
					this.List = this.List == block.List ? this.List : null;
				}
			}
		}

		internal void Merge(InlineStyle style)
		{
			this.Bold = this.Bold == style.Bold ? this.Bold : null;
			this.Italic = this.Italic == style.Italic ? this.Italic : null;
			this.Underline = this.Underline == style.Underline ? this.Underline : null;
			this.Strikethrough = this.Strikethrough == style.Strikethrough ? this.Strikethrough : null;
			this.FontFamilyAgrees &= this.FontFamily == style.FontFamily;
			this.FontSizeAgrees &= this.FontSize == style.FontSize;
			this.TextColorAgrees &= this.TextColor == style.TextColor;
			this.HighlightAgrees &= this.Highlight == style.Highlight;
		}
	}

	/// <summary>
	/// The command engine (agg-gui's rich_text::commands). Inline commands split runs at the exact range edges and
	/// change only the covered runs, with Word toggle semantics: if the whole range already has the attribute the
	/// toggle clears it, otherwise it sets it everywhere. Block commands apply to every paragraph the range touches.
	/// </summary>
	public static class RichTextCommands
	{
		/// <summary>The deepest indent <see cref="RichCommand.Indent"/> reaches.</summary>
		public const int MaxIndent = 8;

		/// <summary>
		/// The style a character typed at <paramref name="pos"/> takes: that of the character before it (Word caret
		/// semantics), the first run's at a paragraph start, or the default in an empty paragraph.
		/// </summary>
		public static InlineStyle StyleAt(RichDoc doc, DocPos pos)
		{
			if (pos.Block < 0 || pos.Block >= doc.Blocks.Count || doc.Blocks[pos.Block].Runs.Count == 0)
			{
				return InlineStyle.Default;
			}

			var runs = doc.Blocks[pos.Block].Runs;
			if (pos.Offset == 0)
			{
				return runs[0].Style;
			}

			int start = 0;
			foreach (var run in runs)
			{
				if (pos.Offset - 1 < start + run.Text.Length)
				{
					return run.Style;
				}

				start += run.Text.Length;
			}

			return runs[runs.Count - 1].Style;
		}

		/// <summary>Summarises the styles across <paramref name="range"/>; an empty range reports the caret style.</summary>
		public static CommonStyle RangeCommonStyle(RichDoc doc, DocRange range)
		{
			var styles = range.IsEmpty ? new List<InlineStyle>() : StylesInRange(doc, range);
			var common = CommonStyle.Of(styles.Count > 0 ? styles[0] : StyleAt(doc, range.Min));
			foreach (var style in styles.Skip(1))
			{
				common.Merge(style);
			}

			common.MergeBlocks(doc, range);
			return common;
		}

		/// <summary>Applies <paramref name="command"/> to <paramref name="doc"/> over <paramref name="range"/>.</summary>
		public static void Apply(RichDoc doc, DocRange range, RichCommand command)
		{
			switch (command.Kind)
			{
				case RichCommandKind.ToggleBold:
					ToggleFlag(doc, range, command, RangeCommonStyle(doc, range).Bold);
					break;
				case RichCommandKind.ToggleItalic:
					ToggleFlag(doc, range, command, RangeCommonStyle(doc, range).Italic);
					break;
				case RichCommandKind.ToggleUnderline:
					ToggleFlag(doc, range, command, RangeCommonStyle(doc, range).Underline);
					break;
				case RichCommandKind.ToggleStrikethrough:
					ToggleFlag(doc, range, command, RangeCommonStyle(doc, range).Strikethrough);
					break;
				case RichCommandKind.SetList:
					SetList(doc, range, command.List);
					break;
				case RichCommandKind.SetAlign:
					ForEachBlock(doc, range, b => b.Align = command.Align);
					break;
				case RichCommandKind.Indent:
					ForEachBlock(doc, range, b => b.Indent = Math.Min(b.Indent + 1, MaxIndent));
					break;
				case RichCommandKind.Outdent:
					ForEachBlock(doc, range, b => b.Indent = Math.Max(b.Indent - 1, 0));
					break;
				default:
					ForEachRun(doc, range, command.ApplyTo);
					break;
			}
		}

		private static void ToggleFlag(RichDoc doc, DocRange range, RichCommand command, bool? allSet)
		{
			// Every run gets one target value - "all set" clears, anything else sets - rather than each run flipping.
			bool target = allSet != true;
			ForEachRun(doc, range, style => command.Kind switch
			{
				RichCommandKind.ToggleBold => style with { Bold = target },
				RichCommandKind.ToggleItalic => style with { Italic = target },
				RichCommandKind.ToggleUnderline => style with { Underline = target },
				_ => style with { Strikethrough = target },
			});
		}

		private static void SetList(RichDoc doc, DocRange range, ListKind kind)
		{
			bool allSame = kind != ListKind.None
				&& Enumerable.Range(range.Min.Block, range.Max.Block - range.Min.Block + 1)
					.Where(i => i < doc.Blocks.Count)
					.All(i => doc.Blocks[i].List == kind);
			var target = allSame ? ListKind.None : kind;
			ForEachBlock(doc, range, b => b.List = target);
		}

		private static List<InlineStyle> StylesInRange(RichDoc doc, DocRange range)
		{
			var styles = new List<InlineStyle>();
			var a = range.Min;
			var b = range.Max;
			for (int i = a.Block; i <= b.Block && i < doc.Blocks.Count; i++)
			{
				var block = doc.Blocks[i];
				int low = i == a.Block ? a.Offset : 0;
				int high = i == b.Block ? b.Offset : block.TextLength;
				int start = 0;
				foreach (var run in block.Runs)
				{
					int end = start + run.Text.Length;
					if (run.Text.Length > 0 && end > low && start < high)
					{
						styles.Add(run.Style);
					}

					start = end;
				}
			}

			return styles;
		}

		// Splits at the range edges in each block and restyles the runs between; an empty range styles nothing
		// (RichEditCore arms a pending caret style for that case instead).
		private static void ForEachRun(RichDoc doc, DocRange range, Func<InlineStyle, InlineStyle> restyle)
		{
			var a = range.Min;
			var b = range.Max;
			if (a == b)
			{
				return;
			}

			for (int i = a.Block; i <= b.Block && i < doc.Blocks.Count; i++)
			{
				var block = doc.Blocks[i];
				int low = i == a.Block ? a.Offset : 0;
				int high = i == b.Block ? b.Offset : block.TextLength;
				if (low >= high)
				{
					continue;
				}

				int startRun = block.EnsureBoundary(low);
				int endRun = block.EnsureBoundary(high);
				for (int r = startRun; r < endRun; r++)
				{
					block.Runs[r] = block.Runs[r] with { Style = restyle(block.Runs[r].Style) };
				}

				block.Normalize();
			}
		}

		// Every paragraph from the range's first to its last, even for an empty range.
		private static void ForEachBlock(RichDoc doc, DocRange range, Action<Block> change)
		{
			for (int i = range.Min.Block; i <= range.Max.Block && i < doc.Blocks.Count; i++)
			{
				change(doc.Blocks[i]);
			}
		}
	}
}
