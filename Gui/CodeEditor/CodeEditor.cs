/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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
using System.Diagnostics;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// An editable, syntax-highlighted code view - agg-gui's code editor (a TextArea with a highlighter and a
	/// line-number gutter). Only the lines in view are measured, highlighted and drawn, so a long file costs no
	/// more per frame than a short one. The editing rules live in <see cref="Document"/>; this widget turns keys,
	/// mouse and wheel into calls on it and draws the result.
	/// </summary>
	/// <remarks>
	/// Keys: arrows, Home/End (Ctrl for the text's start/end), Page Up/Down, Backspace/Delete, Enter (keeps the
	/// indent), Tab/Shift+Tab (indent/outdent), Ctrl+A/C/X/V, Ctrl+Z undo, Ctrl+Shift+Z or Ctrl+Y redo - Ctrl is
	/// Command on the Mac, whose platform layer folds one onto the other. Word-wise moves and deletes are Ctrl (or
	/// Alt, as agg-gui has it) with the arrows, Backspace and Delete; on the Mac they are Option, and Command with
	/// the arrows goes to the line's or the text's ends, as in every Mac text field. Shift with any caret move
	/// extends the selection. A double-click selects a word, a triple-click the line, and a drag after either grows
	/// by words or lines. By default lines do not wrap and the view scrolls both ways, by wheel or by dragging the
	/// floating bars; with <see cref="WordWrap"/> they wrap to the width, as agg-gui's always do, and only scroll up
	/// and down. <see cref="AggGuiEditing"/> takes agg-gui's plainer Home, Enter and Tab. Geometry constants are design units, multiplied by <see cref="GuiWidget.DeviceScale"/>.
	/// </remarks>
	public class CodeEditor : GuiWidget
	{
		/// <summary>agg-gui's EDITOR_PADDING: the space around the text.</summary>
		public const double TextPadding = 8;

		/// <summary>agg-gui's TextArea line height, as a multiple of the font size.</summary>
		public const double LineSpacing = 1.35;

		/// <summary>Lines one wheel notch (120 units) scrolls.</summary>
		public const int LinesPerWheelNotch = 3;

		/// <summary>agg-gui's caret blink: shown for this long, then hidden for as long.</summary>
		public const int CaretBlinkHalfPeriodMs = 500;

		private readonly StyledTypeFace typeFace;
		private readonly CodeLayout layout;
		private readonly CodeEditorScrollbar verticalScrollbar;
		private readonly CodeEditorScrollbar horizontalScrollbar;

		// Restarted by every key, click and focus, so the caret shows solid while the user works and blinks when
		// they stop.
		private readonly Stopwatch blinkClock = new Stopwatch();

		// Which chain of blink callbacks is live; a restart starts a new one and the old one lapses.
		private int blinkGeneration;

		private bool mouseSelecting;

		// What the press that began the drag selected in, and what it selected (the pivot a word or line drag keeps).
		private SelectionUnit dragUnit;
		private int pivotStart;
		private int pivotEnd;
		private double scrollX;
		private double scrollY;
		private bool wordWrap;
		private HAnchor textHAnchor = HAnchor.Left;
		private VAnchor textVAnchor = VAnchor.Top;

		// The widest line in pixels, measured over the whole text; -1 until the next text change is measured.
		private double widestLine = -1;

		/// <param name="typeFace">The face the code is drawn in; a monospaced one lines columns up. Null for
		/// Liberation Sans.</param>
		/// <param name="pointSize">agg-gui's editor uses 13.</param>
		public CodeEditor(string text = "", TypeFace typeFace = null, double pointSize = 13)
		{
			this.typeFace = new StyledTypeFace(typeFace ?? LiberationSansFont.Instance, pointSize * DeviceScale)
			{
				// UI text: follows the System window's typography style.
				ApplyTextStyleSettings = true,
			};
			layout = new CodeLayout(Document, MeasureRun);
			Document.Text = text;
			Document.TextChanged += (s, e) =>
			{
				widestLine = -1;
				layout.Invalidate();
				OnTextChanged(e);
				Invalidate();
			};
			Document.CaretChanged += (s, e) => Invalidate();
			verticalScrollbar = new CodeEditorScrollbar(this, vertical: true);
			horizontalScrollbar = new CodeEditorScrollbar(this, vertical: false);
			TabStop = true;
			Cursor = Cursors.IBeam;
			BackgroundColor = Color.White;
		}

		/// <summary>The text, caret and selection, and the edits the keys make.</summary>
		public CodeDocument Document { get; } = new CodeDocument();

		public override string Text
		{
			get => Document.Text;
			set => Document.Text = value;
		}

		/// <summary>The language the lines are highlighted as, or null to draw everything in <see cref="TextColor"/>.</summary>
		public SyntaxLanguage Language { get; set; }

		/// <summary>The token colours; set it to <see cref="SyntaxPalette.Light"/> or <see cref="SyntaxPalette.Dark"/>
		/// to match the background.</summary>
		public SyntaxPalette Palette { get; set; } = SyntaxPalette.Light;

		public Color TextColor { get; set; } = Color.Black;

		public Color CaretColor { get; set; } = Color.Black;

		public Color SelectionColor { get; set; } = new Color(56, 115, 224, 90);

		public Color ScrollbarColor { get; set; } = new Color(0, 0, 0, 70);

		public Color ScrollbarDragColor { get; set; } = new Color(0, 0, 0, 130);

		/// <summary>Whether word moves are Option and Command+arrows go to line and text ends (the Mac's rules)
		/// rather than word moves on Ctrl. Defaults to <see cref="InternalTextEditWidget.UseMacKeyBindings"/>, so it
		/// follows the running OS; set per editor so a test can drive either without touching global state.</summary>
		public bool UseMacKeyBindings { get; set; } = InternalTextEditWidget.UseMacKeyBindings;

		/// <summary>Whether the caret is in the shown half of its blink - always, just after a key or click.</summary>
		public bool CaretShowing => CaretShowingAt(blinkClock.ElapsedMilliseconds);

		/// <summary>Whether lines wrap to the view's width - after the last word that fits, or mid-word for a word
		/// wider than the view - rather than running on past it. Up/Down, Home and End then go by wrapped row, the
		/// gutter numbers only a line's first row, and there is no horizontal scrolling.</summary>
		public bool WordWrap
		{
			get => wordWrap;
			set
			{
				wordWrap = value;
				ScrollX = 0;
				ScrollY = ScrollY;
				Invalidate();
			}
		}

		/// <summary>agg-gui's editing keys rather than the richer defaults: Home goes straight to the row's start
		/// (not first to the indent), Enter inserts a bare newline (not one carrying the indent) and Tab inserts
		/// <see cref="CodeDocument.TabSize"/> spaces (not up to the next tab stop).</summary>
		public bool AggGuiEditing { get; set; }

		/// <summary>Where each row sits across the text's width - Left (the default), Center or Right, every wrapped
		/// row on its own, as agg-gui's TextArea content alignment has it. With <see cref="ShowLineNumbers"/> off and no
		/// <see cref="Language"/> this is agg-gui's plain multi-line text edit.</summary>
		public HAnchor TextHAnchor
		{
			get => textHAnchor;
			set
			{
				textHAnchor = value;
				Invalidate();
			}
		}

		/// <summary>Where the rows sit, as one block, when they are shorter than the view: Top (the default), Center or
		/// Bottom.</summary>
		public VAnchor TextVAnchor
		{
			get => textVAnchor;
			set
			{
				textVAnchor = value;
				Invalidate();
			}
		}

		/// <summary>Drawn in <see cref="HintColor"/> where the text would be while there is none - agg-gui's hint text.</summary>
		public string HintText { get; set; } = "";

		public Color HintColor { get; set; } = new Color(0, 0, 0, 110);

		/// <summary>Whether the keys, cut and paste leave the text alone; the caret still moves, selects and copies.</summary>
		public bool ReadOnly { get; set; }

		/// <summary>Whether a gutter of line numbers runs down the left side.</summary>
		public bool ShowLineNumbers { get; set; } = true;

		public Color GutterColor { get; set; } = new Color(235, 235, 242);

		public Color LineNumberColor { get; set; } = new Color(20, 20, 26, 128);

		/// <summary>The clipboard copy, cut and paste use; null (the default) uses <see cref="Clipboard.Instance"/>.</summary>
		public ISystemClipboard SystemClipboard { get; set; }

		/// <summary>The height of one line in pixels.</summary>
		public double LineHeight => typeFace.EmSizeInPixels * LineSpacing;

		/// <summary>The width of the line-number gutter in pixels (0 without one): the widest number plus agg-gui's
		/// 8 units each side.</summary>
		public double GutterWidth => ShowLineNumbers
			? MeasureRun(Document.LineCount.ToString(), 0, Document.LineCount.ToString().Length) + 16 * DeviceScale
			: 0;

		/// <summary>How far, in pixels, the text is scrolled up; 0 shows the first line.</summary>
		public double ScrollY
		{
			get => scrollY;
			set
			{
				double clamped = Math.Clamp(value, 0, MaxScrollY);
				if (clamped != scrollY)
				{
					scrollY = clamped;
					Invalidate();
				}
			}
		}

		/// <summary>How far, in pixels, the text is scrolled left; 0 shows the start of every line.</summary>
		public double ScrollX
		{
			get => scrollX;
			set
			{
				double clamped = Math.Clamp(value, 0, MaxScrollX);
				if (clamped != scrollX)
				{
					scrollX = clamped;
					Invalidate();
				}
			}
		}

		public double MaxScrollY => Math.Max(0, Rows.RowCount * LineHeight + 2 * Padding() - Height);

		public double MaxScrollX => WordWrap ? 0 : Math.Max(0, WidestLine + 2 * Padding() - TextViewWidth);

		/// <summary>The first row (a line, or a wrapped piece of one) any part of which is in view.</summary>
		public int FirstVisibleRow => Math.Clamp((int)Math.Floor((ScrollY - Padding()) / LineHeight), 0, Rows.RowCount - 1);

		/// <summary>The last row any part of which is in view.</summary>
		public int LastVisibleRow => Math.Clamp((int)Math.Ceiling((ScrollY + Height - Padding()) / LineHeight) - 1, 0, Rows.RowCount - 1);

		/// <summary>The first line any part of which is in view.</summary>
		public int FirstVisibleLine => Rows.Row(FirstVisibleRow).Line;

		/// <summary>The last line any part of which is in view.</summary>
		public int LastVisibleLine => Rows.Row(LastVisibleRow).Line;

		/// <summary>The rows the last draw rendered, so tests can see only the visible band was drawn.</summary>
		internal int RowsDrawn { get; private set; }

		/// <summary>The text laid out into rows, at the current width and wrap setting.</summary>
		internal CodeLayout Rows
		{
			get
			{
				layout.Configure(WordWrap, Math.Max(1, TextViewWidth - 2 * Padding()));
				return layout;
			}
		}

		private double TextLeft => GutterWidth + Padding();

		/// <summary>The width of the text's part of the view, right of the gutter.</summary>
		internal double TextViewWidth => Math.Max(0, Width - GutterWidth);

		internal bool VerticalScrollbarVisible => MaxScrollY > 0;

		internal bool HorizontalScrollbarVisible => MaxScrollX > 0;

		/// <summary>The vertical bar's thumb, in local pixels, for tests to aim a drag at.</summary>
		internal RectangleDouble VerticalScrollbarThumb => verticalScrollbar.Thumb;

		/// <summary>The horizontal bar's thumb, in local pixels, for tests to aim a drag at.</summary>
		internal RectangleDouble HorizontalScrollbarThumb => horizontalScrollbar.Thumb;

		private double WidestLine
		{
			get
			{
				if (widestLine < 0)
				{
					widestLine = 0;
					for (int line = 0; line < Document.LineCount; line++)
					{
						string text = Document.LineText(line);
						widestLine = Math.Max(widestLine, MeasureRun(text, 0, text.Length));
					}
				}

				return widestLine;
			}
		}

		/// <summary>The top of <paramref name="line"/>'s first row in local pixels, after scrolling.</summary>
		public double LineTop(int line) => RowTop(Rows.FirstRowOfLine(line));

		/// <summary>The top of <paramref name="row"/> in local pixels, after scrolling.</summary>
		public double RowTop(int row) => Height - Padding() - BlockShift() - row * LineHeight + ScrollY;

		/// <summary>Where the caret sits for <paramref name="index"/>: the left edge of that character, at the bottom
		/// of its row, in local pixels.</summary>
		public Vector2 PositionOfIndex(int index)
		{
			int row = Rows.RowOfIndex(index);
			double x = TextLeft - ScrollX + RowShift(row) + Rows.XOfIndex(index);
			return new Vector2(x, RowTop(row) - LineHeight);
		}

		/// <summary>The text index nearest <paramref name="position"/> (local pixels): the row under it, clamped to
		/// the first and last, and the character boundary closest to it on that row.</summary>
		public int IndexAtPosition(Vector2 position)
		{
			int row = (int)Math.Floor((Height - Padding() - BlockShift() + ScrollY - position.Y) / LineHeight);
			if (row < 0)
			{
				return 0;
			}

			if (row >= Rows.RowCount)
			{
				return Document.Text.Length;
			}

			return Rows.IndexAtX(row, position.X - TextLeft + ScrollX - RowShift(row));
		}

		/// <summary>Scrolls the least distance that brings the caret fully into view.</summary>
		public void ScrollToCaret()
		{
			double top = Rows.RowOfIndex(Document.Caret) * LineHeight;
			if (top < ScrollY)
			{
				ScrollY = top;
			}
			else if (top + LineHeight + 2 * Padding() > ScrollY + Height)
			{
				ScrollY = top + LineHeight + 2 * Padding() - Height;
			}

			double x = Rows.XOfIndex(Document.Caret);
			double view = TextViewWidth - 2 * Padding();
			if (x < ScrollX)
			{
				ScrollX = x;
			}
			else if (x > ScrollX + view)
			{
				ScrollX = x - view;
			}
		}

		public void Copy()
		{
			if (Document.HasSelection)
			{
				(SystemClipboard ?? Clipboard.Instance)?.SetText(Document.SelectedText);
			}
		}

		public void Cut()
		{
			if (Document.HasSelection && !ReadOnly)
			{
				Copy();
				Document.Backspace();
			}
		}

		public void Paste()
		{
			string text = (SystemClipboard ?? Clipboard.Instance)?.GetText();
			if (!string.IsNullOrEmpty(text) && !ReadOnly)
			{
				Document.Insert(text);
			}
		}

		/// <summary>Whether the caret shows <paramref name="elapsedMs"/> after its blink restarted: agg-gui's 500 ms
		/// on, 500 ms off.</summary>
		public static bool CaretShowingAt(long elapsedMs) => elapsedMs / CaretBlinkHalfPeriodMs % 2 == 0;

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			RestartBlink();
			if (CodeEditorKeys.Handle(this, keyEvent))
			{
				keyEvent.Handled = true;
				keyEvent.SuppressKeyPress = true;
				ScrollToCaret();
			}
			else if (keyEvent.Control && !keyEvent.Alt)
			{
				// An unused Ctrl (Command on the Mac) chord must not type its letter. Ctrl+Alt is AltGr, which types.
				keyEvent.SuppressKeyPress = true;
			}

			base.OnKeyDown(keyEvent);
		}

		public override void OnKeyPress(KeyPressEventArgs keyPressEvent)
		{
			base.OnKeyPress(keyPressEvent);

			// Control chars (Enter, Backspace, Tab, Escape) are handled - or left alone - on key down. Surrogate
			// halves arrive one press each and are inserted as they come.
			if (!keyPressEvent.Handled && !ReadOnly && keyPressEvent.KeyChar >= ' ' && keyPressEvent.KeyChar != (char)127)
			{
				Document.Insert(keyPressEvent.KeyChar.ToString());
				ScrollToCaret();
				keyPressEvent.Handled = true;
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			// A press on a scroll bar grabs it and leaves the caret where it is.
			if (mouseEvent.Button == MouseButtons.Left
				&& !verticalScrollbar.BeginDrag(mouseEvent.Position)
				&& !horizontalScrollbar.BeginDrag(mouseEvent.Position))
			{
				RestartBlink();
				BeginPointerSelection(IndexAtPosition(mouseEvent.Position), mouseEvent.Clicks, Keyboard.IsKeyDown(Keys.Shift));
				mouseSelecting = true;
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (verticalScrollbar.DragTo(mouseEvent.Position) || horizontalScrollbar.DragTo(mouseEvent.Position))
			{
				// The bar has the drag.
			}
			else if (mouseSelecting)
			{
				// Past an edge the caret lands on the nearest line and ScrollToCaret drags the view along.
				Document.ExtendSelection(IndexAtPosition(mouseEvent.Position), dragUnit, pivotStart, pivotEnd);
				ScrollToCaret();
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			mouseSelecting = false;
			verticalScrollbar.EndDrag();
			horizontalScrollbar.EndDrag();
			Invalidate();
			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			base.OnMouseWheel(mouseEvent);
			double oldX = ScrollX, oldY = ScrollY;
			ScrollY -= mouseEvent.WheelDelta / 120.0 * LinesPerWheelNotch * LineHeight;
			ScrollX -= mouseEvent.WheelDeltaX / 120.0 * LinesPerWheelNotch * LineHeight;

			// Consumed only when it moved the view, so a wheel at an end still scrolls whatever holds the editor.
			if (ScrollY != oldY)
			{
				mouseEvent.WheelDelta = 0;
			}

			if (ScrollX != oldX)
			{
				mouseEvent.WheelDeltaX = 0;
			}
		}

		public override void OnFocusChanged(EventArgs e)
		{
			if (Focused)
			{
				RestartBlink();
			}

			Invalidate();
			base.OnFocusChanged(e);
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			// Re-clamp: a taller view may need less (or no) scroll. The base constructor sizes the widget before
			// the face exists, when there is nothing to clamp yet.
			if (typeFace != null)
			{
				ScrollY = ScrollY;
				ScrollX = ScrollX;
			}

			base.OnBoundsChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
			double gutter = GutterWidth;
			double lineHeight = LineHeight;
			int first = FirstVisibleRow;
			int last = LastVisibleRow;
			double descent = Math.Abs(typeFace.DescentInPixels);
			double baselineInLine = (lineHeight - (typeFace.AscentInPixels + descent)) / 2 + descent;

			if (gutter > 0)
			{
				graphics2D.FillRectangle(0, 0, gutter, Height, GutterColor);
				for (int row = first; row <= last; row++)
				{
					// A wrapped line is numbered on its first row only, as agg-gui's gutter has it.
					CodeRow codeRow = Rows.Row(row);
					if (codeRow.FirstInLine)
					{
						string number = (codeRow.Line + 1).ToString();
						double x = gutter - 8 * DeviceScale - MeasureRun(number, 0, number.Length);
						new TypeFacePrinter(number, typeFace, new Vector2(x, RowTop(row) - lineHeight + baselineInLine)).Render(graphics2D, LineNumberColor);
					}
				}
			}

			RectangleDouble savedClip = graphics2D.GetClippingRect();
			ClipTo(graphics2D, new RectangleDouble(gutter, 0, Width, Height), savedClip);

			RowsDrawn = 0;
			for (int row = first; row <= last; row++)
			{
				DrawRow(graphics2D, row, baselineInLine);
				RowsDrawn++;
			}

			if (Document.Text.Length == 0 && !string.IsNullOrEmpty(HintText))
			{
				double x = TextLeft - ScrollX + AlignShift(MeasureRun(HintText, 0, HintText.Length));
				new TypeFacePrinter(HintText, typeFace, new Vector2(x, RowTop(0) - lineHeight + baselineInLine)).Render(graphics2D, HintColor);
			}

			if (ContainsFocus && CaretShowing)
			{
				Vector2 caret = PositionOfIndex(Document.Caret);
				double width = Math.Max(1, Math.Round(DeviceScale));
				graphics2D.FillRectangle(caret.X, caret.Y, caret.X + width, caret.Y + lineHeight, CaretColor);
			}

			graphics2D.SetClippingRect(savedClip);
			verticalScrollbar.Draw(graphics2D);
			horizontalScrollbar.Draw(graphics2D);
		}

		private double Padding() => TextPadding * DeviceScale;

		/// <summary>How far right <see cref="TextHAnchor"/> moves <paramref name="row"/> from the text's left edge.</summary>
		internal double RowShift(int row)
		{
			if (textHAnchor != HAnchor.Center && textHAnchor != HAnchor.Right)
			{
				return 0;
			}

			CodeRow codeRow = Rows.Row(row);
			int lineStart = Document.LineStart(codeRow.Line);
			return AlignShift(MeasureRun(Document.LineText(codeRow.Line), codeRow.Start - lineStart, codeRow.End - lineStart));
		}

		/// <summary>How far right <see cref="TextHAnchor"/> puts a run <paramref name="width"/> pixels wide.</summary>
		private double AlignShift(double width)
		{
			double slack = Math.Max(0, TextViewWidth - 2 * Padding() - width);
			return textHAnchor == HAnchor.Right ? slack : textHAnchor == HAnchor.Center ? slack / 2 : 0;
		}

		/// <summary>How far down <see cref="TextVAnchor"/> moves the rows when they are shorter than the view.</summary>
		private double BlockShift()
		{
			double slack = Math.Max(0, Height - 2 * Padding() - Rows.RowCount * LineHeight);
			return textVAnchor == VAnchor.Bottom ? slack : textVAnchor == VAnchor.Center ? slack / 2 : 0;
		}

		/// <summary>
		/// A press in the text: one click places the caret (Shift extends to it), a second selects the word, a third
		/// the line, a fourth starts over - agg-gui's multi-click. Remembers what it selected so a drag from it grows
		/// in the same unit.
		/// </summary>
		private void BeginPointerSelection(int index, int clicks, bool extend)
		{
			int count = clicks <= 0 ? 1 : (clicks - 1) % 3 + 1;
			if (extend)
			{
				count = 1;
				Document.SetCaret(index, extend: true);
			}
			else if (count == 3)
			{
				Document.SelectLineAt(index);
			}
			else if (count == 2)
			{
				Document.SelectWordAt(index);
			}
			else
			{
				Document.SetCaret(index);
			}

			dragUnit = count == 3 ? SelectionUnit.Line : count == 2 ? SelectionUnit.Word : SelectionUnit.Character;
			pivotStart = Document.SelectionStart;
			pivotEnd = Document.SelectionEnd;
		}

		/// <summary>
		/// Shows the caret solid and starts its blink over. The blink is event driven: one idle callback booked for
		/// the next flip, which repaints and books the one after, while the editor keeps the focus - no timer, and
		/// no repaint in between.
		/// </summary>
		private void RestartBlink()
		{
			blinkClock.Restart();
			ScheduleBlink(++blinkGeneration);
			Invalidate();
		}

		private void ScheduleBlink(int generation)
		{
			long untilFlip = CaretBlinkHalfPeriodMs - blinkClock.ElapsedMilliseconds % CaretBlinkHalfPeriodMs;
			UiThread.RunOnIdle(
				() =>
				{
					// A later restart runs its own chain; a lost focus or a closed editor ends this one.
					if (generation == blinkGeneration && ContainsFocus && !HasBeenClosed)
					{
						Invalidate();
						ScheduleBlink(generation);
					}
				},
				Math.Max(untilFlip, 1) / 1000.0);
		}

		/// <summary>The pixel width of text[start..end). A tab is as wide as <see cref="CodeDocument.TabSize"/> spaces.</summary>
		private double MeasureRun(string text, int start, int end)
		{
			double width = 0;
			for (int i = start; i < end; i++)
			{
				width += text[i] == '\t'
					? Document.TabSize * typeFace.GetAdvanceForCharacter(' ')
					: typeFace.GetAdvanceForCharacter(text, i);
			}

			return width;
		}

		private void DrawRow(Graphics2D graphics2D, int row, double baselineInLine)
		{
			CodeRow codeRow = Rows.Row(row);
			string text = Document.LineText(codeRow.Line);
			int lineStart = Document.LineStart(codeRow.Line);
			int rowStart = codeRow.Start - lineStart;
			int rowEnd = codeRow.End - lineStart;

			// A soft row's selection covers the whitespace it broke at, up to the next row's start.
			int rowLimit = codeRow.NextStart - lineStart;
			double top = RowTop(row);
			double bottom = top - LineHeight;

			// Where the line's column 0 would be, so a wrapped row's text lands at the row's aligned start.
			double left = TextLeft - ScrollX + RowShift(row) - MeasureRun(text, 0, rowStart);

			// The selection band, running on past the line's end when the selection carries on to the next line.
			int selStart = Math.Max(Document.SelectionStart - lineStart, rowStart);
			int selEnd = Math.Min(Document.SelectionEnd - lineStart, rowLimit);
			if (Document.HasSelection && selStart <= rowLimit && selEnd >= rowStart && selStart <= selEnd)
			{
				double x0 = left + MeasureRun(text, 0, selStart);
				double x1 = left + MeasureRun(text, 0, selEnd);
				if (codeRow.LastInLine && Document.SelectionEnd > lineStart + text.Length)
				{
					x1 += typeFace.GetAdvanceForCharacter(' ');
				}

				graphics2D.FillRectangle(x0, bottom, x1, top, SelectionColor);
			}

			// Highlighted spans in the palette, the gaps between them in the text colour.
			List<SyntaxSpan> spans = Language == null ? new List<SyntaxSpan>() : SyntaxHighlighter.HighlightLine(text, Language);
			double baseline = bottom + baselineInLine;
			int drawn = rowStart;
			foreach (SyntaxSpan span in spans)
			{
				int spanStart = Math.Clamp(span.Start, rowStart, rowEnd);
				int spanEnd = Math.Clamp(span.End, rowStart, rowEnd);
				DrawRun(graphics2D, text, drawn, spanStart, left, baseline, TextColor);
				DrawRun(graphics2D, text, spanStart, spanEnd, left, baseline, Palette.ColorOf(span.Kind, TextColor));
				drawn = spanEnd;
			}

			DrawRun(graphics2D, text, drawn, rowEnd, left, baseline, TextColor);
		}

		/// <summary>Draws text[start..end) where it sits on its line, a piece at a time between tabs so each tab takes
		/// the width <see cref="MeasureRun"/> gives it.</summary>
		private void DrawRun(Graphics2D graphics2D, string text, int start, int end, double left, double baseline, Color color)
		{
			int pieceStart = start;
			for (int i = start; i <= end; i++)
			{
				if (i == end || text[i] == '\t')
				{
					if (i > pieceStart)
					{
						double x = left + MeasureRun(text, 0, pieceStart);
						if (x < Width && x + MeasureRun(text, pieceStart, i) > GutterWidth)
						{
							new TypeFacePrinter(text.Substring(pieceStart, i - pieceStart), typeFace, new Vector2(x, baseline)).Render(graphics2D, color);
						}
					}

					pieceStart = i + 1;
				}
			}
		}

		/// <summary>Clips drawing to <paramref name="local"/>, converted into the target's pixels, within the clip
		/// already set.</summary>
		private static void ClipTo(Graphics2D graphics2D, RectangleDouble local, RectangleDouble savedClip)
		{
			Affine transform = graphics2D.GetTransform();
			double x0 = local.Left, y0 = local.Bottom, x1 = local.Right, y1 = local.Top;
			transform.Transform(ref x0, ref y0);
			transform.Transform(ref x1, ref y1);
			var screen = new RectangleDouble(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));
			var clip = default(RectangleDouble);
			clip.IntersectRectangles(screen, savedClip);
			graphics2D.SetClippingRect(clip);
		}

		/// <summary>The whole rows in view: how far Page Up/Down moves.</summary>
		internal int PageLines() => Math.Max(1, (int)Math.Floor((Height - 2 * Padding()) / LineHeight));
	}
}
