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
using System.Diagnostics;
using System.Linq;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI.RichText
{
	/// <summary>
	/// An editable rich-text view over a <see cref="RichEditCore"/> (agg-gui's RichTextEdit): wrapped per-run
	/// layout, caret, selection, typing, keyboard and mouse editing, and plain-text copy and paste. A formatting
	/// toolbar drives it through <see cref="Exec"/> and follows <see cref="Changed"/> for its button states.
	/// </summary>
	public class RichTextEdit : GuiWidget
	{
		// agg-sharp has no italic face, so italic runs are drawn slanted by this much (x per unit of y).
		private const double ItalicSkew = 0.2;

		private static readonly Stopwatch Clock = Stopwatch.StartNew();

		private readonly Func<InlineStyle, double, StyledTypeFace> resolver;
		private readonly RichTextLayoutCache layoutCache = new RichTextLayoutCache();
		private DocLayout layout;
		private double layoutWidth = -1;

		// The core's DocVersion the layout was built from: caret and selection moves keep the layout.
		private int layoutVersion = -1;
		private bool mouseSelecting;

		// The scroll bar: pointer within this many pixels of the right edge grabs it.
		private const double ScrollBarWidth = 10;
		private bool thumbDragging;
		private double thumbDragStartY;
		private double thumbDragStartScroll;
		private Vector2 lastDrag;
		private bool autoScrolling;

		// agg-gui's select_granularity and select_pivot: a double- or triple-click drag extends by whole words or
		// paragraphs, keeping the unit first clicked selected.
		private int selectClicks;
		private DocRange selectPivot;

		public RichTextEdit(RichDoc doc = null, double defaultFontSize = 12, Func<InlineStyle, double, StyledTypeFace> resolver = null)
		{
			this.Core = new RichEditCore(doc ?? new RichDoc());
			this.DefaultFontSize = defaultFontSize;
			this.resolver = resolver ?? RichTextLayout.DefaultResolver();
			this.Padding = new BorderDouble(6);
			this.Cursor = Cursors.IBeam;
			this.Core.Changed += (s, e) =>
			{
				this.ScrollCaretIntoView();
				this.Changed?.Invoke(this, EventArgs.Empty);
				this.Invalidate();
			};
			this.Core.FeedUndo(Clock.Elapsed.TotalSeconds);
		}

		/// <summary>
		/// Gets or sets whether word motion uses Option (Alt) as on macOS rather than Ctrl or Alt. Defaults to
		/// <see cref="InternalTextEditWidget.UseMacKeyBindings"/>.
		/// </summary>
		public bool UseMacKeyBindings { get; set; } = InternalTextEditWidget.UseMacKeyBindings;

		/// <summary>Gets or sets the theme of the right-click menu; null uses <see cref="ThemeConfig.DefaultTheme"/>.</summary>
		public ThemeConfig MenuTheme { get; set; }

		/// <summary>Raised after any change to the document, caret or selection - the toolbar's cue to refresh.</summary>
		public event EventHandler Changed;

		public RichEditCore Core { get; }

		public double DefaultFontSize { get; }

		public Color TextColor { get; set; } = Color.Black;

		public Color SelectionColor { get; set; } = new Color(51, 153, 255, 90);

		public Color CaretColor { get; set; } = Color.Black;

		/// <summary>Gets the current layout, rebuilt when the document or the width changed.</summary>
		public DocLayout Layout
		{
			get
			{
				double width = Math.Max(this.Width - this.Padding.Width, 1);
				if (this.layout == null || this.layoutWidth != width || this.layoutVersion != this.Core.DocVersion)
				{
					this.layout = RichTextLayout.Layout(this.Core.Doc, width, this.DefaultFontSize, this.resolver, this.layoutCache);
					this.layoutWidth = width;
					this.layoutVersion = this.Core.DocVersion;
				}

				return this.layout;
			}
		}

		/// <summary>Runs a formatting command as its own undo step. The toolbar's entry point.</summary>
		public void Exec(RichCommand command)
		{
			this.Core.AddUndoPoint();
			this.Core.Exec(command);
			this.Core.AddUndoPoint();
		}

		public void Undo()
		{
			this.Core.Undo();
		}

		public void Redo()
		{
			this.Core.Redo();
		}

		/// <summary>The selected text as plain text, paragraphs joined by '\n'.</summary>
		public string SelectedPlainText => string.Join("\n", RichTextEdits.ExtractRange(this.Core.Doc, this.Core.Selection).Select(b => b.Text));

		/// <summary>
		/// Copies the selection: plain text to the system clipboard for other apps, and the styled fragment to
		/// <see cref="RichClipboard"/> so a paste into any RichTextEdit in this process keeps its formatting.
		/// Returns false, touching neither clipboard, when nothing is selected.
		/// </summary>
		public bool Copy()
		{
			if (this.Core.Selection.IsEmpty)
			{
				return false;
			}

			var fragment = RichTextEdits.ExtractRange(this.Core.Doc, this.Core.Selection);
			string text = string.Join("\n", fragment.Select(b => b.Text));
			RichClipboard.Set(text, fragment);
			Clipboard.Instance?.SetText(text);
			return true;
		}

		/// <summary>Copies the selection, then deletes it as one undo step.</summary>
		public void Cut()
		{
			if (this.Copy())
			{
				this.Edit(this.Core.Backspace);
			}
		}

		/// <summary>
		/// Pastes at the caret: the styled fragment while the system clipboard still holds the text of our last Copy,
		/// else the system clipboard's plain text in the caret's style.
		/// </summary>
		public void Paste()
		{
			if (Clipboard.Instance?.ContainsText != true)
			{
				return;
			}

			string text = Clipboard.Instance.GetText();
			var fragment = RichClipboard.Matching(text);
			this.Edit(() =>
			{
				if (fragment != null)
				{
					this.Core.InsertFragment(fragment);
				}
				else
				{
					this.Core.Insert(text);
				}
			});
		}

		/// <summary>Converts a point in this widget's coordinates to the document position under it.</summary>
		public DocPos PositionAt(double x, double y) => RichTextGeometry.HitTest(this.Layout, x - this.Padding.Left, this.ContentTop - y);

		/// <summary>The caret rectangle in this widget's coordinates (agg's y-up).</summary>
		public RectangleDouble CaretBounds(DocPos pos)
		{
			var caret = RichTextGeometry.CaretRect(this.Layout, pos);
			double left = this.Padding.Left + caret.X;
			double top = this.ContentTop - caret.Top;
			return new RectangleDouble(left, top - caret.Height, left + 1, top);
		}

		// Scrolling moves the document up under the fixed padding box.
		private double ContentTop => this.Height - this.Padding.Top + this.scrollOffset;

		private double ViewHeight => this.Height - this.Padding.Height;

		private double scrollOffset;

		/// <summary>
		/// Gets or sets how far the document is scrolled up, in pixels from its top; clamped to 0..<see cref="MaxScroll"/>.
		/// </summary>
		public double ScrollOffset
		{
			get => this.scrollOffset;
			set
			{
				double clamped = Math.Clamp(value, 0, this.MaxScroll);
				if (clamped != this.scrollOffset)
				{
					this.scrollOffset = clamped;
					this.Invalidate();
				}
			}
		}

		/// <summary>Gets how far the document can scroll: how much taller it is than the editor's text area.</summary>
		public double MaxScroll => Math.Max(0, this.Layout.Height - this.ViewHeight);

		/// <summary>Scrolls the least distance that shows the whole caret line (agg-gui's scroll-to-caret).</summary>
		public void ScrollCaretIntoView()
		{
			if (this.ViewHeight <= 0 || this.Width - this.Padding.Width < 1)
			{
				// Not laid out yet: there is no meaningful view to scroll.
				return;
			}

			var caret = RichTextGeometry.CaretRect(this.Layout, this.Core.Caret);
			if (caret.Top < this.scrollOffset)
			{
				this.ScrollOffset = caret.Top;
			}
			else if (caret.Top + caret.Height > this.scrollOffset + this.ViewHeight)
			{
				this.ScrollOffset = caret.Top + caret.Height - this.ViewHeight;
			}
			else
			{
				// An edit that shortened the document can leave the old offset past the new end.
				this.ScrollOffset = this.scrollOffset;
			}
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			base.OnBoundsChanged(e);
			this.ScrollOffset = this.scrollOffset;
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			base.OnMouseWheel(mouseEvent);
			double old = this.scrollOffset;
			this.ScrollOffset -= mouseEvent.WheelDelta / 120.0 * 3 * this.DefaultFontSize * RichTextLayout.LineSpacing;

			// Consumed only when it moved the view, so a wheel at an end still scrolls whatever holds the editor.
			if (this.scrollOffset != old)
			{
				mouseEvent.WheelDelta = 0;
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var layout = this.Layout;
			var (selectionMin, selectionMax) = (this.Core.Selection.Min, this.Core.Selection.Max);

			// Scrolled text stays inside the padding box rather than drawing over it.
			// The clipping rect is in the destination's coordinates, not the widget's, so the padding box is
			// moved there before the two meet; intersected as local coordinates, an editor away from the origin
			// lost that much of its top.
			var oldClip = graphics2D.GetClippingRect();
			var textClip = new RectangleDouble(0, this.Padding.Bottom, this.Width, this.Height - this.Padding.Top);
			graphics2D.GetTransform().transform(ref textClip);
			textClip.IntersectWithRectangle(oldClip);
			graphics2D.SetClippingRect(textClip);
			double blockTop = this.ContentTop;
			for (int b = 0; b < layout.Blocks.Count; b++)
			{
				var block = layout.Blocks[b];
				double lineTop = blockTop;
				for (int l = 0; l < block.Lines.Count; l++)
				{
					var line = block.Lines[l];
					if (lineTop - line.Height > this.Height || lineTop < 0)
					{
						// Off screen: skip the glyph work, which is most of a long document's draw time.
						lineTop -= line.Height;
						continue;
					}

					double baseline = lineTop - line.BaselineFromTop;
					double originX = this.Padding.Left + block.TextLeft + line.AlignDx;
					if (l == 0 && block.Marker != null)
					{
						new TypeFacePrinter(block.Marker, block.MarkerFace, new Vector2(this.Padding.Left + block.MarkerX, baseline)).Render(graphics2D, this.TextColor);
					}

					this.DrawSelection(graphics2D, b, line, originX, lineTop, selectionMin, selectionMax);
					foreach (var fragment in line.Fragments)
					{
						this.DrawFragment(graphics2D, fragment, originX + fragment.X, baseline, lineTop, line.Height);
					}

					lineTop -= line.Height;
				}

				blockTop -= block.Height;
			}

			if (this.Focused && this.Core.Selection.IsEmpty)
			{
				graphics2D.FillRectangle(this.CaretBounds(this.Core.Caret), this.CaretColor);
			}

			graphics2D.SetClippingRect(oldClip);
			this.DrawScrollThumb(graphics2D);

			base.OnDraw(graphics2D);

			// Feeding every frame is what lets a burst of typing settle into one undo step.
			if (this.Core.FeedUndo(Clock.Elapsed.TotalSeconds))
			{
				UiThread.RunOnIdle(this.Invalidate, 0.5);
			}
		}

		// A thin thumb at the right edge when the document is taller than the editor, sized to the visible share.
		private void DrawScrollThumb(Graphics2D graphics2D)
		{
			if (this.MaxScroll > 0)
			{
				var (top, thumb) = this.ThumbGeometry();
				double left = this.Width - (this.thumbDragging ? ScrollBarWidth : 5);
				graphics2D.FillRectangle(left, top - thumb, this.Width - 2, top, new Color(this.TextColor, this.thumbDragging ? 140 : 90));
			}
		}

		// The thumb's top (in widget y) and height: its share of the track is the visible share of the document.
		private (double Top, double Height) ThumbGeometry()
		{
			double track = this.Height - 4;
			double thumb = Math.Min(track, Math.Max(20, track * this.ViewHeight / this.Layout.Height));
			double max = this.MaxScroll;
			double top = this.Height - 2 - (max > 0 ? (track - thumb) * this.scrollOffset / max : 0);
			return (top, thumb);
		}

		// Scroll offset per pixel of thumb travel.
		private double ScrollPerThumbPixel()
		{
			var (_, thumb) = this.ThumbGeometry();
			double travel = this.Height - 4 - thumb;
			return travel > 0 ? this.MaxScroll / travel : 0;
		}

		/// <summary>Gets whether (<paramref name="x"/>, <paramref name="y"/>) is on the scroll bar, which only shows when the document overflows.</summary>
		public bool IsOnScrollBar(double x, double y) => this.MaxScroll > 0 && x >= this.Width - ScrollBarWidth && y >= 0 && y <= this.Height;

		// While a drag-selection is held outside the text area, scroll toward the pointer and select up to the edge
		// line; the pointer's distance past the edge sets the speed. Rescheduled until the pointer comes back or the
		// button is released, so holding still keeps scrolling.
		private void AutoScrollStep()
		{
			if (!this.mouseSelecting)
			{
				this.autoScrolling = false;
				return;
			}

			double top = this.Height - this.Padding.Top;
			double bottom = this.Padding.Bottom;
			double over = this.lastDrag.Y > top ? this.lastDrag.Y - top : this.lastDrag.Y < bottom ? this.lastDrag.Y - bottom : 0;
			if (over == 0 || (over > 0 ? this.scrollOffset <= 0 : this.scrollOffset >= this.MaxScroll))
			{
				this.autoScrolling = false;
				return;
			}

			this.ScrollOffset -= Math.Clamp(over / 2, -this.ViewHeight, this.ViewHeight);
			this.ExtendMouseSelection(this.PositionAt(this.lastDrag.X, Math.Clamp(this.lastDrag.Y, bottom + 1, top - 1)));
			this.autoScrolling = true;
			UiThread.RunOnIdle(this.AutoScrollStep, 0.05);
		}

		private void DrawSelection(Graphics2D graphics2D, int block, LineLayout line, double originX, double lineTop, DocPos min, DocPos max)
		{
			if (min == max || block < min.Block || block > max.Block)
			{
				return;
			}

			int from = Math.Max(block == min.Block ? min.Offset : 0, line.StartOffset);
			int to = Math.Min(block == max.Block ? max.Offset : int.MaxValue, line.EndOffset);
			bool runsOn = block < max.Block && to == line.EndOffset;
			if (from > to || (from == to && !runsOn))
			{
				return;
			}

			double left = originX + XAt(line, from);
			double right = originX + XAt(line, to) + (runsOn ? 4 : 0);
			graphics2D.FillRectangle(left, lineTop - line.Height, right, lineTop, this.SelectionColor);
		}

		private static double XAt(LineLayout line, int offset)
		{
			foreach (var fragment in line.Fragments)
			{
				if (offset <= fragment.StartOffset + fragment.Text.Length)
				{
					return fragment.X + fragment.PrefixWidth(offset - fragment.StartOffset);
				}
			}

			return line.Width;
		}

		private void DrawFragment(Graphics2D graphics2D, LineFragment fragment, double x, double baseline, double lineTop, double lineHeight)
		{
			var style = fragment.Style;
			if (style.Highlight is Color highlight)
			{
				graphics2D.FillRectangle(x, lineTop - lineHeight, x + fragment.Width, lineTop, highlight);
			}

			var color = style.TextColor ?? this.TextColor;
			var printer = new TypeFacePrinter(fragment.Text, fragment.Face, new Vector2(x, baseline));
			if (style.Italic)
			{
				// Skew about the baseline so the slant leans right from where the letters sit.
				var slant = Affine.NewTranslation(-x, -baseline) * Affine.NewSkewing(ItalicSkew, 0) * Affine.NewTranslation(x, baseline);
				graphics2D.Render(new VertexSourceApplyTransform(printer, slant), color);
			}
			else
			{
				printer.Render(graphics2D, color);
			}

			double thickness = Math.Max(1, fragment.Face.EmSizeInPixels / 14);
			if (style.Underline)
			{
				graphics2D.FillRectangle(x, baseline - (thickness * 2), x + fragment.Width, baseline - thickness, color);
			}

			if (style.Strikethrough)
			{
				double middle = baseline + (fragment.Face.AscentInPixels * 0.3);
				graphics2D.FillRectangle(x, middle, x + fragment.Width, middle + thickness, color);
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);
			if (mouseEvent.Button == MouseButtons.Left && this.IsOnScrollBar(mouseEvent.X, mouseEvent.Y))
			{
				// A press on the track off the thumb jumps the thumb's middle there; then it drags from where it is.
				var (top, thumb) = this.ThumbGeometry();
				if (mouseEvent.Y > top || mouseEvent.Y < top - thumb)
				{
					this.ScrollOffset += (top - (thumb / 2) - mouseEvent.Y) * this.ScrollPerThumbPixel();
				}

				this.thumbDragging = true;
				this.thumbDragStartY = mouseEvent.Y;
				this.thumbDragStartScroll = this.scrollOffset;
				this.Invalidate();
			}
			else if (mouseEvent.Button == MouseButtons.Left)
			{
				this.Focus();
				var target = this.PositionAt(mouseEvent.X, mouseEvent.Y);
				this.selectClicks = Keyboard.IsKeyDown(Keys.Shift) ? 1 : Math.Min(mouseEvent.Clicks, 3);
				this.selectPivot = this.selectClicks switch
				{
					2 => RichTextWords.WordRange(this.Core.Doc, target),
					3 => RichTextWords.BlockRange(this.Core.Doc, target),
					_ => new DocRange(target, target),
				};
				if (this.selectClicks > 1)
				{
					this.Core.SetSelection(this.selectPivot.Start, this.selectPivot.End);
				}
				else
				{
					this.Core.SetCaret(target, extend: Keyboard.IsKeyDown(Keys.Shift));
				}

				this.mouseSelecting = true;
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);
			if (this.thumbDragging)
			{
				this.ScrollOffset = this.thumbDragStartScroll + ((this.thumbDragStartY - mouseEvent.Y) * this.ScrollPerThumbPixel());
			}
			else if (this.mouseSelecting)
			{
				this.lastDrag = mouseEvent.Position;
				if (mouseEvent.Y > this.Height - this.Padding.Top || mouseEvent.Y < this.Padding.Bottom)
				{
					if (!this.autoScrolling)
					{
						this.AutoScrollStep();
					}
				}
				else
				{
					this.ExtendMouseSelection(this.PositionAt(mouseEvent.X, mouseEvent.Y));
				}
			}
		}

		// Drag-selection to pos: by characters, or by whole words or paragraphs after a double or triple click.
		private void ExtendMouseSelection(DocPos pos)
		{
			if (this.selectClicks > 1)
			{
				// Grow by whole units away from the pivot unit, which stays selected.
				var unit = this.selectClicks == 2 ? RichTextWords.WordRange(this.Core.Doc, pos) : RichTextWords.BlockRange(this.Core.Doc, pos);
				var (anchor, caret) = pos >= this.selectPivot.End ? (this.selectPivot.Start, unit.End) : (this.selectPivot.End, unit.Start);
				if (anchor != this.Core.Anchor || caret != this.Core.Caret)
				{
					this.Core.SetSelection(anchor, caret);
				}
			}
			else if (pos != this.Core.Caret)
			{
				this.Core.SetCaret(pos, extend: true);
			}
		}

		/// <summary>
		/// The right-click menu (agg-gui's rich_text context_menu): Cut, Copy, Paste and Select All, going through the
		/// same styled clipboard as the keyboard chords.
		/// </summary>
		public PopupMenu CreateContextMenu()
		{
			var menu = new PopupMenu(this.MenuTheme ?? ThemeConfig.DefaultTheme());
			bool hasSelection = !this.Core.Selection.IsEmpty;
			var cut = menu.CreateMenuItem("Cut".Localize());
			cut.Enabled = hasSelection;
			cut.Click += (s, e) => this.Cut();
			var copy = menu.CreateMenuItem("Copy".Localize());
			copy.Enabled = hasSelection;
			copy.Click += (s, e) => this.Copy();
			var paste = menu.CreateMenuItem("Paste".Localize());
			paste.Enabled = Clipboard.Instance?.ContainsText == true;
			paste.Click += (s, e) => this.Paste();
			menu.CreateSeparator();
			var selectAll = menu.CreateMenuItem("Select All".Localize());
			selectAll.Enabled = this.Core.Doc.EndPos != default;
			selectAll.Click += (s, e) => this.Core.SelectAll();
			return menu;
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Right)
			{
				// A right-click inside the selection keeps it for Cut/Copy; elsewhere it moves the caret first, as
				// word processors do.
				var pos = this.PositionAt(mouseEvent.X, mouseEvent.Y);
				if (this.Core.Selection.IsEmpty || pos < this.Core.Selection.Min || pos > this.Core.Selection.Max)
				{
					this.Core.SetCaret(pos);
				}

				this.Focus();
				this.CreateContextMenu().ShowMenu(this, mouseEvent);
			}

			this.mouseSelecting = false;
			if (this.thumbDragging)
			{
				this.thumbDragging = false;
				this.Invalidate();
			}

			base.OnMouseUp(mouseEvent);
		}

		public override void OnKeyPress(KeyPressEventArgs keyPressEvent)
		{
			base.OnKeyPress(keyPressEvent);
			if (!keyPressEvent.Handled && keyPressEvent.KeyChar >= 32 && keyPressEvent.KeyChar != 127)
			{
				this.Core.Insert(keyPressEvent.KeyChar.ToString());
				this.Core.FeedUndo(Clock.Elapsed.TotalSeconds);
				keyPressEvent.Handled = true;
			}
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			base.OnKeyDown(keyEvent);
			if (keyEvent.Handled)
			{
				return;
			}

			if (this.HandleKey(keyEvent))
			{
				keyEvent.Handled = true;
				keyEvent.SuppressKeyPress = true;
			}
		}

		// Every key the editor acts on; returns false for the ones it leaves to its parents.
		private bool HandleKey(KeyEventArgs keyEvent)
		{
			bool shift = keyEvent.Shift;
			var caret = this.Core.Caret;
			var doc = this.Core.Doc;

			// Word motion and delete: Option on a Mac (Cmd arrives as Control there), Ctrl or Alt elsewhere.
			bool word = this.UseMacKeyBindings ? keyEvent.Alt : keyEvent.Control || keyEvent.Alt;
			bool macCommand = this.UseMacKeyBindings && keyEvent.Control;
			if (macCommand && (keyEvent.KeyCode == Keys.Left || keyEvent.KeyCode == Keys.Right))
			{
				// Cmd+Left/Right go to the ends of the wrapped line on screen, as in CodeEditor and macOS text views.
				var (lineStart, lineEnd) = RichTextGeometry.LineRange(this.Layout, caret);
				this.MoveTo(new DocPos(caret.Block, keyEvent.KeyCode == Keys.Left ? lineStart : lineEnd), shift);
				return true;
			}

			if (macCommand && (keyEvent.KeyCode == Keys.Up || keyEvent.KeyCode == Keys.Down))
			{
				// Cmd+Up/Down go to the document's start and end.
				this.MoveTo(keyEvent.KeyCode == Keys.Up ? default : doc.EndPos, shift);
				return true;
			}

			if (keyEvent.Control)
			{
				switch (keyEvent.KeyCode)
				{
					case Keys.B:
						this.Exec(RichCommand.ToggleBold);
						return true;
					case Keys.I:
						this.Exec(RichCommand.ToggleItalic);
						return true;
					case Keys.U:
						this.Exec(RichCommand.ToggleUnderline);
						return true;
					case Keys.A:
						this.Core.SelectAll();
						return true;
					case Keys.Z:
						if (shift)
						{
							this.Redo();
						}
						else
						{
							this.Undo();
						}

						return true;
					case Keys.Y:
						this.Redo();
						return true;
					case Keys.C:
						this.Copy();
						return true;
					case Keys.X:
						this.Cut();
						return true;
					case Keys.V:
						this.Paste();
						return true;
					case Keys.Home:
						this.Core.SetCaret(default, shift);
						return true;
					case Keys.End:
						this.Core.SetCaret(doc.EndPos, shift);
						return true;
				}
			}

			switch (keyEvent.KeyCode)
			{
				case Keys.Back:
					this.Edit(word ? () => this.DeleteWord(-1) : this.Core.Backspace);
					return true;
				case Keys.Delete:
					this.Edit(word ? () => this.DeleteWord(1) : this.Core.DeleteForward);
					return true;
				case Keys.Enter:
					this.Edit(this.Core.Split);
					return true;
				case Keys.Tab:
					this.Exec(shift ? RichCommand.Outdent : RichCommand.Indent);
					return true;
				case Keys.Left:
					this.MoveTo(!shift && !this.Core.Selection.IsEmpty ? this.Core.Selection.Min : word ? RichTextWords.WordTarget(doc, caret, -1) : Step(doc, caret, -1), shift);
					return true;
				case Keys.Right:
					this.MoveTo(!shift && !this.Core.Selection.IsEmpty ? this.Core.Selection.Max : word ? RichTextWords.WordTarget(doc, caret, 1) : Step(doc, caret, 1), shift);
					return true;
				case Keys.Up:
				case Keys.Down:
					var rect = RichTextGeometry.CaretRect(this.Layout, caret);
					double y = keyEvent.KeyCode == Keys.Up ? rect.Top - 1 : rect.Top + rect.Height + 1;
					this.MoveTo(y < 0 ? default : RichTextGeometry.HitTest(this.Layout, rect.X, y), shift);
					return true;
				case Keys.Home:
					this.MoveTo(new DocPos(caret.Block, 0), shift);
					return true;
				case Keys.End:
					this.MoveTo(new DocPos(caret.Block, doc.Blocks[caret.Block].TextLength), shift);
					return true;
			}

			return false;
		}

		// Ctrl/Alt+Backspace/Delete: a selection goes as usual; otherwise the span a word move would cross.
		private void DeleteWord(int direction)
		{
			if (this.Core.Selection.IsEmpty)
			{
				var target = RichTextWords.WordTarget(this.Core.Doc, this.Core.Caret, direction);
				if (target == this.Core.Caret)
				{
					return;
				}

				this.Core.SetSelection(this.Core.Caret, target);
			}

			this.Core.Backspace();
		}

		private void MoveTo(DocPos pos, bool extend) => this.Core.SetCaret(pos, extend);

		// Structural edits (Enter, Delete, paste) are undo steps of their own; typing coalesces via FeedUndo.
		private void Edit(Action edit)
		{
			this.Core.AddUndoPoint();
			edit();
			this.Core.AddUndoPoint();
		}

		// One character left or right, crossing paragraph ends.
		private static DocPos Step(RichDoc doc, DocPos pos, int direction)
		{
			int offset = pos.Offset + direction;
			if (offset < 0)
			{
				return pos.Block > 0 ? new DocPos(pos.Block - 1, doc.Blocks[pos.Block - 1].TextLength) : pos;
			}

			if (offset > doc.Blocks[pos.Block].TextLength)
			{
				return pos.Block + 1 < doc.Blocks.Count ? new DocPos(pos.Block + 1, 0) : pos;
			}

			return new DocPos(pos.Block, offset);
		}
	}
}
