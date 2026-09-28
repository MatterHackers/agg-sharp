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
	/// <summary>
	/// Per-run character formatting (agg-gui's rich_text::InlineStyle). A null optional field means "inherit the
	/// widget default"; the four flags are absolute. A record, so two styles with the same values are equal - run
	/// normalization and the undo snapshots both rely on that.
	/// </summary>
	public sealed record InlineStyle
	{
		public static InlineStyle Default { get; } = new InlineStyle();

		public bool Bold { get; init; }

		public bool Italic { get; init; }

		public bool Underline { get; init; }

		public bool Strikethrough { get; init; }

		/// <summary>Gets the font family name, or null to inherit the widget default family.</summary>
		public string FontFamily { get; init; }

		/// <summary>Gets the font size in points, or null to inherit the widget default size.</summary>
		public double? FontSize { get; init; }

		/// <summary>Gets the text colour, or null to inherit the theme's text colour.</summary>
		public Color? TextColor { get; init; }

		/// <summary>Gets the background colour behind the run, or null for none.</summary>
		public Color? Highlight { get; init; }
	}

	/// <summary>A span of text sharing one <see cref="InlineStyle"/>. Never contains '\n' - newlines are block breaks.</summary>
	public sealed record TextRun(string Text, InlineStyle Style)
	{
		public static TextRun Plain(string text) => new TextRun(text, InlineStyle.Default);
	}

	/// <summary>List decoration applied to a whole <see cref="Block"/>.</summary>
	public enum ListKind
	{
		None,
		Bullet,
		Ordered,
	}

	/// <summary>
	/// A paragraph: styled runs plus block attributes (alignment, list decoration, indent depth). Mutable so the
	/// edit primitives work in place; <see cref="Clone"/> makes the deep copy an undo snapshot needs, and equality is
	/// by value.
	/// </summary>
	public sealed class Block : IEquatable<Block>
	{
		public Block()
		{
		}

		public Block(params TextRun[] runs)
		{
			this.Runs.AddRange(runs);
		}

		public List<TextRun> Runs { get; } = new List<TextRun>();

		public Justification Align { get; set; } = Justification.Left;

		public ListKind List { get; set; } = ListKind.None;

		/// <summary>Gets or sets the indent depth, 0 to <see cref="RichTextCommands.MaxIndent"/>.</summary>
		public int Indent { get; set; }

		public int TextLength => this.Runs.Sum(r => r.Text.Length);

		public string Text => string.Concat(this.Runs.Select(r => r.Text));

		/// <summary>A paragraph of default-style text (no runs when empty).</summary>
		public static Block Plain(string text) => string.IsNullOrEmpty(text) ? new Block() : new Block(TextRun.Plain(text));

		/// <summary>A deep copy (runs are immutable records, so copying the list is enough).</summary>
		public Block Clone()
		{
			var copy = this.CloneAttributes();
			copy.Runs.AddRange(this.Runs);
			return copy;
		}

		/// <summary>An empty paragraph with this one's alignment, list and indent - what a split starts from.</summary>
		public Block CloneAttributes() => new Block() { Align = this.Align, List = this.List, Indent = this.Indent };

		/// <summary>
		/// Makes a run boundary at character offset <paramref name="offset"/>, splitting the run that straddles it.
		/// Returns the index of the run that begins there (Runs.Count at or past the end). Styling or removing an
		/// arbitrary sub-range is: make both boundaries, then work on the runs between them.
		/// </summary>
		public int EnsureBoundary(int offset)
		{
			int start = 0;
			for (int i = 0; i < this.Runs.Count; i++)
			{
				if (offset == start)
				{
					return i;
				}

				var run = this.Runs[i];
				if (offset < start + run.Text.Length)
				{
					int split = offset - start;
					this.Runs[i] = run with { Text = run.Text.Substring(0, split) };
					this.Runs.Insert(i + 1, run with { Text = run.Text.Substring(split) });
					return i + 1;
				}

				start += run.Text.Length;
			}

			return this.Runs.Count;
		}

		/// <summary>Drops empty runs and merges neighbours with identical styles, so the run list stays canonical.</summary>
		public void Normalize()
		{
			this.Runs.RemoveAll(r => r.Text.Length == 0);
			for (int i = 0; i + 1 < this.Runs.Count;)
			{
				if (this.Runs[i].Style == this.Runs[i + 1].Style)
				{
					this.Runs[i] = this.Runs[i] with { Text = this.Runs[i].Text + this.Runs[i + 1].Text };
					this.Runs.RemoveAt(i + 1);
				}
				else
				{
					i++;
				}
			}
		}

		public bool Equals(Block other)
		{
			return other != null
				&& this.Align == other.Align
				&& this.List == other.List
				&& this.Indent == other.Indent
				&& this.Runs.SequenceEqual(other.Runs);
		}

		public override bool Equals(object obj) => this.Equals(obj as Block);

		public override int GetHashCode() => HashCode.Combine(this.Align, this.List, this.Indent, this.Runs.Count);
	}

	/// <summary>A rich-text document: its paragraphs in order, always at least one. Equality is by value.</summary>
	public sealed class RichDoc : IEquatable<RichDoc>
	{
		public RichDoc()
		{
			this.Blocks.Add(new Block());
		}

		public RichDoc(IEnumerable<Block> blocks)
		{
			this.Blocks.AddRange(blocks);
			if (this.Blocks.Count == 0)
			{
				this.Blocks.Add(new Block());
			}
		}

		public List<Block> Blocks { get; } = new List<Block>();

		/// <summary>Gets the position just past the last character of the last block.</summary>
		public DocPos EndPos => new DocPos(this.Blocks.Count - 1, this.Blocks[this.Blocks.Count - 1].TextLength);

		/// <summary>Gets the text with blocks separated by '\n'.</summary>
		public string PlainText => string.Join("\n", this.Blocks.Select(b => b.Text));

		public RichDoc Clone() => new RichDoc(this.Blocks.Select(b => b.Clone()));

		public bool Equals(RichDoc other) => other != null && this.Blocks.SequenceEqual(other.Blocks);

		public override bool Equals(object obj) => this.Equals(obj as RichDoc);

		public override int GetHashCode() => this.Blocks.Count;
	}

	/// <summary>A caret position: block index and character offset into that block's text. Orders by (block, offset).</summary>
	public readonly record struct DocPos(int Block, int Offset) : IComparable<DocPos>
	{
		public static bool operator <(DocPos a, DocPos b) => a.CompareTo(b) < 0;

		public static bool operator >(DocPos a, DocPos b) => a.CompareTo(b) > 0;

		public static bool operator <=(DocPos a, DocPos b) => a.CompareTo(b) <= 0;

		public static bool operator >=(DocPos a, DocPos b) => a.CompareTo(b) >= 0;

		public int CompareTo(DocPos other) => this.Block != other.Block ? this.Block.CompareTo(other.Block) : this.Offset.CompareTo(other.Offset);
	}

	/// <summary>A selection between two positions, in either order; <see cref="Min"/> and <see cref="Max"/> order them.</summary>
	public readonly record struct DocRange(DocPos Start, DocPos End)
	{
		public bool IsEmpty => this.Start == this.End;

		public DocPos Min => this.Start <= this.End ? this.Start : this.End;

		public DocPos Max => this.Start <= this.End ? this.End : this.Start;

		public static DocRange Collapsed(DocPos pos) => new DocRange(pos, pos);
	}
}
