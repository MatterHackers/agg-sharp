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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// The scrolled content of a <see cref="RichMarkdownEditWidget"/>: one cached layout per block stacked top to
	/// bottom, Raw blocks hosted as rendered child widgets, and painting of only the blocks in view. Its height is
	/// the document's, so the scrollable parent scrolls it.
	/// </summary>
	internal sealed class RichDocumentView : GuiWidget
	{
		private readonly RichMarkdownEditWidget editor;
		private readonly Dictionary<RichBlock, GuiWidget> rawHosts = new();
		private readonly RichImageCache images;
		private List<Entry> entries = new();
		private RichLayoutStyle style;
		private long styleEpoch = -1;
		private double layoutWidth = -1;

		public RichDocumentView(RichMarkdownEditWidget editor)
		{
			this.editor = editor;
			images = new RichImageCache(ImageLoaded);
			HAnchor = HAnchor.Stretch;
			VAnchor = VAnchor.Absolute;
			Selectable = false;
		}

		public RichLayoutStyle Style => style;

		public Func<string, string> ResolveImageUrl
		{
			get => images.ResolveUrl;
			set => images.ResolveUrl = value;
		}

		public int LayoutCount { get; private set; }

		public (int First, int Last) DrawnBlocks { get; private set; } = (0, -1);

		/// <summary>
		/// The block's box in this widget's coordinates, from the top of its text to the top of the next block.
		/// </summary>
		public RectangleDouble BlockBounds(int index)
		{
			var entry = entries[index];
			double origin = Height - entry.Top - entry.Layout.Height;
			return new RectangleDouble(0, origin + entry.Trim, layoutWidth, origin + entry.Layout.Height);
		}

		public IRichBlockLayout LayoutOf(int index) => entries[index].Layout;

		/// <summary>
		/// How many blocks have a layout: fewer than the document's before the first width, or between an edit and
		/// its relayout.
		/// </summary>
		public int LaidOutCount => entries.Count;

		public double OriginOf(int index) => Height - entries[index].Top - entries[index].Layout.Height;

		public GuiWidget RawHost(int index)
		{
			return rawHosts.TryGetValue(entries[index].Block, out var host) ? host : null;
		}

		/// <summary>
		/// Lays every block out again: for a new document, a new width, or a new style.
		/// </summary>
		public void RelayoutAll()
		{
			// Hosts of blocks the document no longer has go now, even when there is no width to lay out to yet.
			CloseRawHosts(new HashSet<RichBlock>(editor.Document.Blocks));
			Sync(null);
		}

		/// <summary>
		/// Re-lays out the given blocks after an edit. Blocks are matched to their cached layouts by reference, so
		/// blocks an edit inserted are laid out and removed ones dropped, while every other block keeps its layout.
		/// </summary>
		public void Relayout(IEnumerable<RichBlock> changed)
		{
			Sync(new HashSet<RichBlock>(changed));
		}

		private bool StyleIsCurrent => style != null && style.Scale == DeviceScale && styleEpoch == TextStyleSettings.Epoch;

		/// <summary>
		/// Forgets the style, so the next layout rebuilds it (a theme change).
		/// </summary>
		public void InvalidateStyle()
		{
			style = null;
		}

		/// <param name="changed">The blocks to lay out again, or null for all of them.</param>
		private void Sync(HashSet<RichBlock> changed)
		{
			if (Width <= 0)
			{
				// Not sized yet; the first width change lays everything out.
				entries.Clear();
				return;
			}

			var anchor = ScrollAnchor();
			if (!StyleIsCurrent)
			{
				// Raw hosts were built with the old theme and scale; they are rebuilt as their blocks lay out.
				CloseRawHosts(new HashSet<RichBlock>());
				BuildStyle();
				changed = null;
			}

			if (Math.Abs(Width - layoutWidth) > .001)
			{
				layoutWidth = Width;
				changed = null;
			}

			var blocks = editor.Document.Blocks;
			var cached = new Dictionary<RichBlock, Entry>();
			foreach (var entry in entries)
			{
				cached[entry.Block] = entry;
			}

			var numbers = ListNumbers(blocks);
			var next = new List<Entry>(blocks.Count);
			for (int i = 0; i < blocks.Count; i++)
			{
				var block = blocks[i];
				if (!cached.TryGetValue(block, out var entry)
					|| changed == null
					|| changed.Contains(block)
					|| entry.Number != numbers[i]
					|| entry.EndsList != EndsList(blocks, i)
					|| entry.Inputs != LayoutInputs.Of(block))
				{
					// A renumbered ordered item re-lays out too: an edit above it moved its number. So does a block an
					// edit reshaped without naming it - list children lifted a level when their parent left the list.
					bool endsList = EndsList(blocks, i);
					entry = new Entry(block, LayoutBlock(block, numbers[i], endsList), numbers[i], endsList);
				}

				next.Add(entry);
			}

			entries = next;
			Restack();
			RestoreScrollAnchor(anchor);
		}

		/// <summary>
		/// The block at the top of the view and how far into it the view starts, so a relayout can keep that
		/// content still: a block above the view growing (or the one being typed in at the top) must not scroll the
		/// text under the reader's eyes, and ScrollableWidget on its own keeps the bottom fixed instead.
		/// </summary>
		private (RichBlock Block, double Into, double ScrollTop)? ScrollAnchor()
		{
			if (entries.Count == 0 || Parent == null)
			{
				return null;
			}

			double scrollTop = editor.ScrollOffsetFromTop();
			int index = Math.Min(FirstEntryEndingBelow(scrollTop), entries.Count - 1);
			return (entries[index].Block, scrollTop - entries[index].Top, scrollTop);
		}

		private void RestoreScrollAnchor((RichBlock Block, double Into, double ScrollTop)? anchor)
		{
			if (anchor == null || Parent == null)
			{
				return;
			}

			// The scroll area fits this widget only when laid out; lay it out now so the offset is clamped to the
			// new height rather than the old one.
			editor.PerformLayout();
			var (block, into, scrollTop) = anchor.Value;
			var entry = entries.Find(e => e.Block == block);
			editor.SetScrollOffsetFromTop(entry != null ? entry.Top + into : scrollTop);
		}

		private void CloseRawHosts(HashSet<RichBlock> keep)
		{
			foreach (var (block, host) in new List<KeyValuePair<RichBlock, GuiWidget>>(rawHosts))
			{
				if (!keep.Contains(block))
				{
					rawHosts.Remove(block);
					host.Close();
				}
			}
		}

		private IRichBlockLayout LayoutBlock(RichBlock block, int number, bool endsList)
		{
			LayoutCount++;
			return block.Kind == RichBlockKind.Table
				? RichTableLayout.Layout(block, layoutWidth, style)
				: RichBlockLayout.Layout(block, layoutWidth, style, number, endsList);
		}

		/// <summary>
		/// Whether the block at <paramref name="index"/> is a list's last item: a list item followed by the document's
		/// end, a block that is not a list item, or an item of another list. Its layout takes a paragraph's space below
		/// so the list does not run into what follows (a bullet list straight into a numbered one).
		/// </summary>
		internal static bool EndsList(List<RichBlock> blocks, int index)
		{
			var block = blocks[index];
			if (block.Kind != RichBlockKind.ListItem)
			{
				return false;
			}

			return index + 1 >= blocks.Count
				|| blocks[index + 1].Kind != RichBlockKind.ListItem
				|| blocks[index + 1].ListGroup != block.ListGroup
				|| (blocks[index + 1].List?.Ordered ?? false) != (block.List?.Ordered ?? false) && (blocks[index + 1].List?.Depth ?? 0) == (block.List?.Depth ?? 0);
		}

		// Ordered items count up from their list's start at each depth; a shallower item restarts the deeper count.
		private static int[] ListNumbers(List<RichBlock> blocks)
		{
			var numbers = new int[blocks.Count];
			var counters = new Dictionary<int, int>();
			RichListGroup group = null;
			bool inList = false;
			for (int i = 0; i < blocks.Count; i++)
			{
				var block = blocks[i];
				if (block.Kind != RichBlockKind.ListItem)
				{
					inList = false;
					numbers[i] = 1;
					continue;
				}

				if (!inList || block.ListGroup != group)
				{
					counters.Clear();
				}

				inList = true;
				group = block.ListGroup;
				int depth = block.List?.Depth ?? 0;
				foreach (int deeper in new List<int>(counters.Keys))
				{
					if (deeper > depth)
					{
						counters.Remove(deeper);
					}
				}

				counters[depth] = counters.TryGetValue(depth, out int count) ? count + 1 : block.List?.StartNumber ?? 1;

				// Only an ordered item shows its number, so a bullet's never changes and never forces a relayout.
				numbers[i] = block.List?.Ordered == true ? counters[depth] : 1;
			}

			return numbers;
		}

		private void BuildStyle()
		{
			var theme = editor.Theme;
			style = new RichLayoutStyle(theme)
			{
				ImageSize = ImageSize,
				RawBlockSize = RawBlockSize,
			};
			styleEpoch = TextStyleSettings.Epoch;
		}

		/// <summary>
		/// Recomputes each block's top (a prefix sum of heights) and the total height, and moves the Raw hosts.
		/// Consecutive blocks of one quote are stacked without the space after each, so they read as one quote with
		/// one unbroken bar.
		/// </summary>
		private void Restack()
		{
			double top = 0;
			for (int i = 0; i < entries.Count; i++)
			{
				var entry = entries[i];
				var block = entry.Block;
				bool joined = i + 1 < entries.Count
					&& block.Kind == RichBlockKind.Quote
					&& block.QuoteGroup != null
					&& entries[i + 1].Block.Kind == RichBlockKind.Quote
					&& entries[i + 1].Block.QuoteGroup == block.QuoteGroup;
				entry.Top = top;
				entry.Trim = joined ? style.BlockSpacing(block).After : 0;
				top += entry.Layout.Height - entry.Trim;
			}

			// Set the height before placing hosts: the stack is measured from the top, positions from the bottom.
			Height = Math.Max(top, 1);

			var live = new HashSet<RichBlock>();
			for (int i = 0; i < entries.Count; i++)
			{
				if (entries[i].Layout is RichBlockLayout { Block.Kind: RichBlockKind.Raw } layout && rawHosts.TryGetValue(layout.Block, out var host))
				{
					live.Add(layout.Block);
					var box = layout.Lines[0].Fragments[0];
					host.OriginRelativeParent = new Vector2(box.X, Height - entries[i].Top - layout.Height + box.Baseline);
				}
			}

			CloseRawHosts(live);
			Invalidate();
		}

		private Vector2 RawBlockSize(RichBlock block, double available)
		{
			if (!rawHosts.TryGetValue(block, out var host))
			{
				host = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Fit,
					Selectable = false,
				};
				new AggMarkdownDocument { Markdown = block.OriginalSource }.Parse(editor.Theme, host);
				rawHosts[block] = host;
				AddChild(host);
			}

			host.Width = available;
			host.PerformLayout();
			return new Vector2(available, Math.Max(host.Height, 1));
		}

		private Vector2 ImageSize(InlineAtom atom)
		{
			var image = images.Loaded(atom);
			if (image == null)
			{
				// The layout's own placeholder size: a square one body line tall.
				var face = style.BlockFace(new RichBlock());
				double line = face.AscentInPixels + Math.Abs(face.DescentInPixels);
				return new Vector2(line, line);
			}

			// Like the viewer, an image never draws wider than the column; it scales down keeping its shape.
			double scale = Math.Min(1, Math.Max(layoutWidth, 1) / image.Width);
			return new Vector2(image.Width * scale, image.Height * scale);
		}

		private void ImageLoaded(string url)
		{
			// The load can land after the editor closed (a help page navigated away from mid-download).
			if (HasBeenClosed || editor.HasBeenClosed)
			{
				return;
			}

			var showing = new List<RichBlock>();
			foreach (var entry in entries)
			{
				if (ShowsImage(entry.Block, url))
				{
					showing.Add(entry.Block);
				}
			}

			if (showing.Count > 0)
			{
				Relayout(showing);
			}
		}

		private bool ShowsImage(RichBlock block, string url)
		{
			bool Shows(List<RichInline> inlines) => inlines.Exists(inline => inline is InlineAtom { Kind: InlineAtomKind.Image } atom && images.UrlOf(atom) == url);
			if (block.Kind != RichBlockKind.Table)
			{
				return Shows(block.Inlines);
			}

			return block.TableRows.Exists(row => row.Exists(cell => Shows(cell.Inlines)));
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			base.OnBoundsChanged(e);
			if (Width > 0 && Math.Abs(Width - layoutWidth) > .001)
			{
				RelayoutAll();
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (entries.Count == 0 || !StyleIsCurrent)
			{
				// A DeviceScale or text-size change since the last layout: every block re-lays out.
				RelayoutAll();
			}

			if (entries.Count > 0)
			{
				DrawBlocks(graphics2D);
			}

			base.OnDraw(graphics2D);
		}

		private void DrawBlocks(Graphics2D graphics2D)
		{
			// Only the blocks in view are painted, so a long help document costs what a screenful does.
			var visible = Parent != null ? TransformFromParentSpace(editor, editor.LocalBounds) : LocalBounds;
			double fromTop = Height - visible.Top;
			double toTop = Height - visible.Bottom;
			int first = FirstEntryEndingBelow(fromTop);
			int last = first - 1;
			var colors = editor.Colors;
			var selection = editor.Selection;
			bool focused = editor.ContainsFocus;
			for (int i = first; i < entries.Count && entries[i].Top < toTop; i++)
			{
				last = i;
				var entry = entries[i];
				var layout = entry.Layout;
				double origin = Height - entry.Top - layout.Height;
				RichBlockPainter.DrawSelection(graphics2D, layout, origin, selection, i, layoutWidth, colors.Selection);
				RichBlockPainter.DrawBlock(graphics2D, layout, origin, style, colors, images.Loaded);
				if (focused && editor.CaretShowing && selection.IsEmpty && i == selection.Caret.BlockIndex)
				{
					var caret = selection.Caret;
					RichBlockPainter.DrawCaret(graphics2D, layout, origin, new RichCaret(caret.Offset, editor.CaretAtLineEnd, caret.Row, caret.Column), style, colors.Caret);
				}
			}

			DrawnBlocks = (first, last);

			if (!focused && editor.IsEmpty && !string.IsNullOrEmpty(editor.EmptyHint))
			{
				var layout = (RichBlockLayout)entries[0].Layout;
				var line = layout.Lines[0];
				double origin = Height - entries[0].Top - layout.Height;
				new TypeFacePrinter(editor.EmptyHint, style.BlockFace(layout.Block), new Vector2(line.Left, origin + line.Baseline)).Render(graphics2D, colors.Hint);
			}
		}

		private int FirstEntryEndingBelow(double fromTop)
		{
			int low = 0;
			int high = entries.Count - 1;
			int result = entries.Count;
			while (low <= high)
			{
				int middle = (low + high) / 2;
				if (entries[middle].Top + entries[middle].Layout.Height > fromTop)
				{
					result = middle;
					high = middle - 1;
				}
				else
				{
					low = middle + 1;
				}
			}

			return result;
		}

		/// <summary>
		/// A block's shape as its layout sees it: kind, heading level, list level and marker, alignment.
		/// </summary>
		private readonly record struct LayoutInputs(RichBlockKind Kind, int HeadingLevel, int Depth, bool Ordered, char Marker, RichAlignment Alignment)
		{
			public static LayoutInputs Of(RichBlock block)
			{
				var list = block.List;
				return new LayoutInputs(block.Kind, block.HeadingLevel, list?.Depth ?? 0, list?.Ordered ?? false, list?.Marker ?? '\0', block.Alignment);
			}
		}

		private sealed class Entry
		{
			public Entry(RichBlock block, IRichBlockLayout layout, int number, bool endsList)
			{
				Block = block;
				Layout = layout;
				Number = number;
				EndsList = endsList;
				Inputs = LayoutInputs.Of(block);
			}

			public RichBlock Block { get; }

			/// <summary>
			/// What the layout was built from besides the text, to notice a block changed in place by an edit that
			/// did not name it.
			/// </summary>
			public LayoutInputs Inputs { get; }

			public IRichBlockLayout Layout { get; }

			public int Number { get; }

			/// <summary>
			/// Whether the layout was built as its list's last item, with a paragraph's space below.
			/// </summary>
			public bool EndsList { get; }

			/// <summary>
			/// Distance from the document's top to the top of this block's box.
			/// </summary>
			public double Top { get; set; }

			/// <summary>
			/// Space at the bottom of the layout the next block overlaps (the gap inside a multi-paragraph quote).
			/// </summary>
			public double Trim { get; set; }
		}
	}
}
