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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class InternalTextEditWidget : GuiWidget, IIgnoredPopupChild
	{
		private static HashSet<char> WordBreakChars = new HashSet<char>(new char[] 
		{ 
			' ', '\t', // white space characters
			'\'', '"', '`', // quotes
			',', '.', '?', '!', '@', '&', // punctuation
			'(', ')', '<', '>', '[', ']', '{', '}', // parents (or equivalent)
			'-', '+', '*', '/', '=', '\\', '#', '$', '^', '|', '°', '²', '³'// math symbols
		});

        private char? maskChar = null;
        private string actualText = ""; 
		
		private static HashSet<char> WordBreakCharsAndCR
		{
			get
			{
				var withCR = new HashSet<char>(WordBreakChars);
				withCR.Add('\n');

				return withCR;
			}
		}

		/// <summary>
		/// Gets or sets whether caret motion and delete use Mac conventions (Option for word-wise, Command
		/// for line- and document-wise) instead of the Windows ones (Control for word-wise, Control+Home/End
		/// for document-wise).
		/// </summary>
		/// <remarks>
		/// Defaults to the running OS. It is settable only so tests can exercise both branches from either
		/// host - a hard-coded <c>IsMacOS()</c> would leave one of the two paths untestable on any given CI
		/// leg. <see cref="System.OperatingSystem.IsMacOS"/> rather than <c>AggContext.OperatingSystem</c>
		/// because the latter reflection-loads a platform assembly, which a bare widget unit test has no
		/// reason to have initialized.
		/// <para/>
		/// Being settable makes it process-wide mutable state, which is why every test class that flips it
		/// (MacTextEditKeyBindingTests, and TextEditTests, KeyboardTests and MacModifierFlagsTests, which
		/// share the same down-state keyboard) carries
		/// <c>[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]</c>. That attribute is
		/// load bearing: it is the only thing serializing these flips, and dropping it lets one class run
		/// with the other's key bindings and fail in a way that looks like a caret bug.
		/// </remarks>
		public static bool UseMacKeyBindings { get; set; } = System.OperatingSystem.IsMacOS();

		public static Action<InternalTextEditWidget, MouseEventArgs> DefaultRightClick;

		// Guards the one-time wiring of the default right-click menu so concurrent constructors
		// cannot both see null and double-subscribe. A consumer that pre-seeds DefaultRightClick
		// before creating any widget still suppresses the default wiring.
		private static readonly object defaultRightClickLocker = new object();

		public static void AddTextWidgetRightClickMenu(ThemeConfig theme)
		{
			InternalTextEditWidget.DefaultRightClick += (s, e) =>
			{
				var textEditWidget = s as InternalTextEditWidget;
				var popupMenu = new PopupMenu(theme);

				var cut = popupMenu.CreateMenuItem("Cut".Localize());
				cut.Enabled = !string.IsNullOrEmpty(s.Selection);
				cut.Click += (s2, e2) =>
				{
					textEditWidget?.CopySelection();
					textEditWidget?.DeleteSelection();
				};

				var copy = popupMenu.CreateMenuItem("Copy".Localize());
				copy.Enabled = !string.IsNullOrEmpty(s.Selection);
				copy.Click += (s2, e2) =>
				{
					textEditWidget?.CopySelection();
				};

				var paste = popupMenu.CreateMenuItem("Paste".Localize());
				paste.Enabled = Clipboard.Instance.ContainsText;
				paste.Click += (s2, e2) =>
				{
					textEditWidget?.PasteFromClipboard();
				};

				popupMenu.CreateSeparator();

				var selectAll = popupMenu.CreateMenuItem("Select All".Localize());
				selectAll.Enabled = !string.IsNullOrEmpty(textEditWidget.Text);
				selectAll.Click += (s2, e2) =>
				{
					textEditWidget?.SelectAll();
				};

				textEditWidget.KeepMenuOpen = true;
				popupMenu.Closed += (s3, e3) =>
				{
					textEditWidget.KeepMenuOpen = false;
				};

				popupMenu.ShowMenu(s, e);
			};
		}

		public event KeyEventHandler EnterPressed;

		public event EventHandler AllSelected;

		private UndoBuffer undoBuffer = new UndoBuffer();

		private bool mouseIsDownLeft = false;
		private bool showingRightClickMenu = false;
		private bool _selecting;

		public bool Selecting
		{
			get
			{
				return _selecting;
			}

			set
			{
				if (_selecting != value)
				{
					_selecting = value;
					Invalidate();
				}
			}
		}

		public bool KeepMenuOpen { get; set; } = false;

		public int SelectionIndexToStartBefore { get; set; }

		private int _charIndexToInsertBefore;

		public int CharIndexToInsertBefore
		{
			get
			{
				if (!string.IsNullOrEmpty(this.Text))
				{
					_charIndexToInsertBefore = Math.Min(this.Text.Length, _charIndexToInsertBefore);
				}
				else
				{
					_charIndexToInsertBefore = 0;
				}

				return _charIndexToInsertBefore;
			}

			set
			{
				_charIndexToInsertBefore = value;
			}
		}

		private int charIndexToAcceptAsMerging;

		private double desiredBarX;

		private readonly TextWidget internalTextWidget;

		public bool MergeTypingDuringUndo { get; set; }

		public event EventHandler InsertBarPositionChanged;

		/// <summary>
		/// This event fires when the user has finished editing the control.
		/// Fired on leave event after editing, or on enter key for non-multi line controls.
		/// </summary>
		public event EventHandler EditComplete;

		private Vector2 insertBarPosition;

		public new bool DoubleBuffer
		{
			get
			{
				return internalTextWidget.DoubleBuffer;
			}

			set
			{
				internalTextWidget.DoubleBuffer = value;
			}
		}

		public Vector2 InsertBarPosition
		{
			get
			{
				return insertBarPosition;
			}

			set
			{
				if (insertBarPosition != value)
				{
					insertBarPosition = value;
					OnInsertBarPositionChanged(null);
				}
			}
		}

		public TypeFacePrinter Printer
		{
			get
			{
				return internalTextWidget.Printer;
			}
		}

        public char? MaskChar
        {
            get => maskChar;
            set
            {
                if (maskChar != value)
                {
                    maskChar = value;
                    UpdateDisplayText();
                }
            }
        }

        public string GetActualText()
        {
            return actualText;
        }

        public void SetActualTextAndUpdate(string text)
        {
			actualText = NormalizeLineEndings(text);
            UpdateDisplayText();
        }

		private static string NormalizeLineEndings(string text)
		{
			return string.IsNullOrEmpty(text)
				? ""
				: text.Replace("\r\n", "\n").Replace('\r', '\n');
		}

		private static string NormalizeLineEndings(string text, int charIndex, out int normalizedCharIndex)
		{
			if (string.IsNullOrEmpty(text))
			{
				normalizedCharIndex = 0;
				return "";
			}

			int rawLimit = Math.Max(0, Math.Min(charIndex, text.Length));
			int normalizedIndex = 0;
			var builder = new StringBuilder(text.Length);

			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] == '\r')
				{
					builder.Append('\n');
					if (i < rawLimit)
					{
						normalizedIndex++;
					}

					if (i + 1 < text.Length && text[i + 1] == '\n')
					{
						i++;
					}
				}
				else
				{
					builder.Append(text[i]);
					if (i < rawLimit)
					{
						normalizedIndex++;
					}
				}
			}

			normalizedCharIndex = normalizedIndex;
			return builder.ToString();
		}

        private void UpdateDisplayText()
        {
            if (maskChar.HasValue)
            {
                internalTextWidget.Text = new string(maskChar.Value, actualText.Length);
            }
            else
            {
                internalTextWidget.Text = actualText;
            }
        }

        /// <summary>
        /// This is called when the user has modified the text control.  It will
        /// be triggered when the control looses focus or enter is pressed on non-multi-line control.
        /// </summary>
        /// <param name="e">The event args to pass on to EditComplete</param>
        public virtual void OnEditComplete(EventArgs e)
		{
			EditComplete?.Invoke(this, e);
			textWhenGotFocus = Text;
		}

		private void OnInsertBarPositionChanged(EventArgs e)
		{
			InsertBarPositionChanged?.Invoke(this, e);
		}

		public string Selection
		{
			get
			{
				if (Selecting)
				{
					// make local copies to make sure we aren't affected by any threading issues
					var text = Text;
					var charIndexToInsertBefore = Math.Max(0, Math.Min(text.Length, CharIndexToInsertBefore));
					var selectionIndexToStartBefore = Math.Max(0, Math.Min(text.Length, SelectionIndexToStartBefore));
					if (charIndexToInsertBefore < selectionIndexToStartBefore)
					{
						return text.Substring(charIndexToInsertBefore, selectionIndexToStartBefore - charIndexToInsertBefore);
					}
					else
					{
						return text.Substring(selectionIndexToStartBefore, charIndexToInsertBefore - selectionIndexToStartBefore);
					}
				}

				return "";
			}
		}

        public override string Text
        {
            get => actualText;
            set
            {
				var normalizedText = NormalizeLineEndings(value);
                if (actualText != normalizedText)
                {
                    CharIndexToInsertBefore = 0;
                    actualText = normalizedText;
                    UpdateDisplayText();
                    OnTextChanged(null);
                    Invalidate();
                }
            }
        }

        public InternalTextEditWidget(string text, double pointSize, bool multiLine, int tabIndex, TypeFace typeFace = null)
        {
            lock (defaultRightClickLocker)
            {
                if (DefaultRightClick == null)
                {
                    var menuTheme = ThemeConfig.DefaultMenuTheme();
                    InternalTextEditWidget.AddTextWidgetRightClickMenu(menuTheme);
                }
            }

            TabIndex = tabIndex;
            TabStop = true;
            MergeTypingDuringUndo = true;

            actualText = NormalizeLineEndings(text);
            internalTextWidget = new TextWidget("", pointSize: pointSize, ellipsisIfClipped: false, textColor: _textColor, typeFace: typeFace);
            internalTextWidget.Selectable = false;
            internalTextWidget.AutoExpandBoundsToText = true;
            AddChild(internalTextWidget);

            UpdateDisplayText();

            UpdateLocalBounds();

            Multiline = multiLine;

            FixBarPosition(DesiredXPositionOnLine.Set);

            var newUndoData = new TextWidgetUndoCommand(this);
            undoBuffer.Add(newUndoData);

            Cursor = Cursors.IBeam;

            internalTextWidget.TextChanged += new EventHandler(InternalTextWidget_TextChanged);
            internalTextWidget.BoundsChanged += new EventHandler(InternalTextWidget_BoundsChanged);
        }

        private void UpdateLocalBounds()
		{
			// double padding = 5;
			double width = Math.Max(internalTextWidget.Width + 2, 3);
			double height = Math.Max(internalTextWidget.Height, internalTextWidget.Printer.TypeFaceStyle.EmSizeInPixels);
			// LocalBounds = new RectangleDouble(this.BorderWidth - padding, this.BorderWidth - padding, width + this.BorderWidth + padding, height + this.BorderWidth + padding);
			LocalBounds = new RectangleDouble(-1, 0, width, height);
			// TODO: text widget should have some padding rather than the 1 on the x below.  LBB 2013/02/03
			internalTextWidget.OriginRelativeParent = new Vector2(1, -internalTextWidget.LocalBounds.Bottom);
		}

		private void InternalTextWidget_BoundsChanged(object sender, EventArgs e)
		{
			UpdateLocalBounds();
		}

		private void InternalTextWidget_TextChanged(object sender, EventArgs e)
		{
			OnTextChanged(e);
		}

		public bool Multiline { get; set; } = true;

		private Stopwatch timeSinceTurnOn = new Stopwatch();
		private double barOnTime = .6;
		private double barOffTime = .6;

		/// <summary>
		/// Which chain of idle callbacks is the live one. Every focus starts its own chain, and a field can be
		/// focused again while an earlier chain is still queued - a reparented editor (one popped out into a
		/// window of its own) loses and retakes the keyboard, and so does anything else that calls Focus twice.
		/// The stale chain would otherwise go on firing against the clock the new one restarted.
		/// </summary>
		private int barFlashGeneration;

		/// <summary>
		/// Whether the caret is in the showing half of its blink. Read from where the clock has got to within
		/// the on-then-off cycle rather than from how many callbacks have arrived, so a callback that lands
		/// early or late moves nothing but when the field is repainted.
		/// </summary>
		private bool BarIsShowing => timeSinceTurnOn.ElapsedMilliseconds % ((barOnTime + barOffTime) * 1000) < barOnTime * 1000;

		/// <summary>
		/// Asks for the field to be repainted. The blink drives itself and has done since it was made to run
		/// one chain of callbacks at a time; calling this cannot start a second one.
		/// </summary>
		[Obsolete("The caret blinks on its own while the field is focused. Call Invalidate if you want a repaint.")]
		public void OnIdle()
		{
			Invalidate();
		}

		/// <summary>
		/// One step of the blink: the cycle has moved on since the repaint that asked for this, so ask for
		/// another one and book the next step. Only ever reached through <see cref="ScheduleBarFlash"/>, which
		/// is what keeps there being exactly one chain of these in flight.
		/// </summary>
		private void BlinkTick()
		{
			if (this.Focused
				&& !HasBeenClosed)
			{
				Invalidate();
				ScheduleBarFlash(barFlashGeneration);
			}
			else
			{
				timeSinceTurnOn.Stop();
			}
		}

		/// <summary>
		/// Puts the caret back to the start of its cycle - showing - and takes over the blink from any chain of
		/// callbacks an earlier focus left behind.
		/// </summary>
		private void RestartBarFlash()
		{
			timeSinceTurnOn.Restart();
			ScheduleBarFlash(++barFlashGeneration);
			Invalidate();
		}

		/// <summary>
		/// Asks to be woken at the next edge of the blink - the moment the caret has to be painted the other
		/// way - and no more often than that.
		/// </summary>
		private void ScheduleBarFlash(int generation)
		{
			double cycleMs = (barOnTime + barOffTime) * 1000;
			double positionInCycleMs = timeSinceTurnOn.ElapsedMilliseconds % cycleMs;
			double untilNextEdgeMs = positionInCycleMs < barOnTime * 1000
				? (barOnTime * 1000) - positionInCycleMs
				: cycleMs - positionInCycleMs;

			UiThread.RunOnIdle(
				() =>
				{
					// a later focus has its own chain running; this one is finished
					if (generation == barFlashGeneration)
					{
						BlinkTick();
					}
				},
				Math.Max(untilNextEdgeMs, 1) / 1000);
		}

		public bool SelectAllOnFocus { get; set; }

		private bool selectAllOnMouseUpIfNoSelection = false;

		private string textWhenGotFocus;

		public override void OnFocusChanged(EventArgs e)
		{
			if (Focused)
			{
				if (!showingRightClickMenu)
				{
					// don't change the focus text if we were showing the right click menu
					textWhenGotFocus = Text;
				}

				showingRightClickMenu = false;
				RestartBarFlash();
				if (SelectAllOnFocus)
				{
					selectAllOnMouseUpIfNoSelection = true;
				}
			}
			else
			{
				// do not lose selection on focus changed
				Invalidate();
				if (TextHasChanged())
				{
					OnEditComplete(e);
				}
				else if (SelectAllOnFocus
					&& selectedAllDueToFocus
					&& !showingRightClickMenu)
				{
					// if we select all on focus and the selection happened due to focus and no change
					Selecting = false;
				}
			}

			base.OnFocusChanged(e);
		}

		public void MarkAsStartingState()
		{
			textWhenGotFocus = Text;
		}

		public bool TextHasChanged()
		{
			return textWhenGotFocus != Text;
		}

		/// <summary>
		/// The colour of the insert caret. <see cref="TextColor"/> derives it whenever it is set, and this
		/// initializer is that same derivation for the default text colour - the default of a Color is
		/// transparent, so a field nobody had given a colour to used to draw no caret at all.
		/// </summary>
		public Color CursorColor { get; set; } = DefaultTextColor.WithAlpha(CursorAlpha);

		/// <summary>
		/// The colour of the band behind selected text. Derived from <see cref="TextColor"/> the same way
		/// <see cref="CursorColor"/> is, and defaulted here for the same reason - an unthemed field used to
		/// paint its selection in transparent, so selecting text in one showed nothing at all.
		/// </summary>
		public Color HighlightColor { get; set; } = DefaultTextColor.WithAlpha(HighlightAlpha);

		private const int CursorAlpha = 175;

		private const int HighlightAlpha = 100;

		private static readonly Color DefaultTextColor = Color.Black;

		private Color _textColor = DefaultTextColor;

		private int _borderWidth = 0;
		private bool selectedAllDueToFocus;

		public int BorderRadius { get; set; } = 0;

		public int BorderWidth
		{
			get
			{
				return this._borderWidth;
			}

			set
			{
				this._borderWidth = value;
				UpdateLocalBounds();
			}
		}

		public Color TextColor
		{
			get
			{
				return _textColor;
			}

			set
			{
				this._textColor = value;
				internalTextWidget.TextColor = this._textColor;
				CursorColor = value.WithAlpha(CursorAlpha);
				HighlightColor = value.WithAlpha(HighlightAlpha);
            }
		}

		public bool ReadOnly { get; set; }

        public override void OnDraw(Graphics2D graphics2D)
        {
            double fontHeight = internalTextWidget.Printer.TypeFaceStyle.EmSizeInPixels;

            if (Selecting
                && SelectionIndexToStartBefore != CharIndexToInsertBefore)
            {
                Vector2 selectPosition = internalTextWidget.Printer.GetOffsetLeftOfCharacterIndex(SelectionIndexToStartBefore);

                if (selectPosition.Y == InsertBarPosition.Y)
                {
                    var bar = new RectangleDouble(Math.Ceiling(selectPosition.X),
                                            Math.Ceiling(internalTextWidget.Height + selectPosition.Y),
                                            Math.Ceiling(InsertBarPosition.X + 1),
                                            Math.Ceiling(internalTextWidget.Height + InsertBarPosition.Y - fontHeight));

                    var selectCursorRect = new RoundedRect(bar, 0);
                    graphics2D.Render(selectCursorRect, this.HighlightColor);
                }
                else
                {
                    int firstCharToHighlight = Math.Min(CharIndexToInsertBefore, SelectionIndexToStartBefore);
                    int lastCharToHighlight = Math.Max(CharIndexToInsertBefore, SelectionIndexToStartBefore);
                    int lineStart = firstCharToHighlight;
                    Vector2 lineStartPos = internalTextWidget.Printer.GetOffsetLeftOfCharacterIndex(lineStart);
                    int lineEnd = lineStart + 1;
                    Vector2 lineEndPos = internalTextWidget.Printer.GetOffsetLeftOfCharacterIndex(lineEnd);
                    if (lineEndPos.Y != lineStartPos.Y)
                    {
                        lineEndPos = lineStartPos;
                    }

                    bool firstCharOfLine = false;
                    for (int i = lineEnd; i < lastCharToHighlight + 1; i++)
                    {
                        Vector2 nextPos = internalTextWidget.Printer.GetOffsetLeftOfCharacterIndex(i);
                        if (firstCharOfLine)
                        {
                            if (lineEndPos.Y != lineStartPos.Y)
                            {
                                lineEndPos = lineStartPos;
                            }

                            firstCharOfLine = false;
                        }

                        if (nextPos.Y != lineStartPos.Y)
                        {
                            if (lineEndPos.X == lineStartPos.X)
                            {
                                lineEndPos.X += Printer.TypeFaceStyle.GetAdvanceForCharacter(' ');
                            }

                            var bar = new RectangleDouble(Math.Ceiling(lineStartPos.X),
                                                    Math.Ceiling(internalTextWidget.Height + lineStartPos.Y),
                                                    Math.Ceiling(lineEndPos.X + 1),
                                                    Math.Ceiling(internalTextWidget.Height + lineEndPos.Y - fontHeight));

                            var selectCursorRect = new RoundedRect(bar, 0);
                            graphics2D.Render(selectCursorRect, this.HighlightColor);
                            lineStartPos = nextPos;
                            firstCharOfLine = true;
                        }
                        else
                        {
                            lineEndPos = nextPos;
                        }
                    }

                    if (lineEndPos.X != lineStartPos.X)
                    {
                        var bar = new RectangleDouble(Math.Ceiling(lineStartPos.X),
                                                Math.Ceiling(internalTextWidget.Height + lineStartPos.Y),
                                                Math.Ceiling(lineEndPos.X + 1),
                                                Math.Ceiling(internalTextWidget.Height + lineEndPos.Y - fontHeight));

                        var selectCursorRect = new RoundedRect(bar, 0);
                        graphics2D.Render(selectCursorRect, this.HighlightColor);
                    }
                }
            }

            if (this.Focused && BarIsShowing)
            {
                double xFraction = graphics2D.GetTransform().tx;
                xFraction = xFraction - (int)xFraction;

                // the caret is the only piece of the field nothing else sizes, so it carries the display scale
                // itself - a fixed hardware pixel is half the weight of the thinnest stroke of the text it sits
                // in on a Retina panel, which reads as a flicker rather than as a caret
                double barWidth = Math.Max(1, Math.Round(DeviceScale, MidpointRounding.AwayFromZero));

                var bar2 = new RectangleDouble(Math.Ceiling(InsertBarPosition.X) - xFraction,
                                        Math.Ceiling(internalTextWidget.Height + InsertBarPosition.Y - fontHeight),
                                        Math.Ceiling(InsertBarPosition.X) + barWidth - xFraction,
                                        Math.Ceiling(internalTextWidget.Height + InsertBarPosition.Y));
                var cursorRect = new RoundedRect(bar2, 0);
                graphics2D.Render(cursorRect, this.CursorColor);
            }

            base.OnDraw(graphics2D);
        }

        public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				StartSelectionIfRequired(null);
				CharIndexToInsertBefore = internalTextWidget.Printer.GetCharacterIndexToStartBefore(new Vector2(mouseEvent.X, mouseEvent.Y));

				if (mouseEvent.Clicks < 2 || ShiftKeyIsDown(null))
				{
					if (CharIndexToInsertBefore == -1)
					{
						// we could not find any characters when looking for mouse click position
						CharIndexToInsertBefore = 0;
					}

					if (!ShiftKeyIsDown(null))
					{
						SelectionIndexToStartBefore = CharIndexToInsertBefore;
						Selecting = false;
					}

					mouseIsDownLeft = true;
				}
				else if (IsDoubleClick(mouseEvent) && Text.Length > 0)
				{
					while (CharIndexToInsertBefore >= 0
						&& (CharIndexToInsertBefore >= Text.Length
							|| (CharIndexToInsertBefore > -1 && !WordBreakCharsAndCR.Contains(Text[CharIndexToInsertBefore]))))
					{
						CharIndexToInsertBefore--;
					}

					CharIndexToInsertBefore++;
					SelectionIndexToStartBefore = CharIndexToInsertBefore + 1;
					while (SelectionIndexToStartBefore < Text.Length && !WordBreakCharsAndCR.Contains(Text[SelectionIndexToStartBefore]))
					{
						SelectionIndexToStartBefore++;
					}

					Selecting = true;
				}

				RestartBarFlash();
				FixBarPosition(DesiredXPositionOnLine.Set);
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (mouseIsDownLeft)
			{
				StartSelectionIfRequired(null);
				CharIndexToInsertBefore = internalTextWidget.Printer.GetCharacterIndexToStartBefore(new Vector2(mouseEvent.X, mouseEvent.Y));
				if (CharIndexToInsertBefore < 0)
				{
					CharIndexToInsertBefore = 0;
				}

				if (CharIndexToInsertBefore != SelectionIndexToStartBefore)
				{
					Selecting = true;
				}

				Invalidate();
				FixBarPosition(DesiredXPositionOnLine.Set);
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (SelectAllOnFocus
				&& selectAllOnMouseUpIfNoSelection
				&& Selecting == false)
			{
				SelectAll();
				selectedAllDueToFocus = true;
			}
			else
			{
				selectedAllDueToFocus = false;
			}

			if (mouseEvent.Button == MouseButtons.Left)
			{
				mouseIsDownLeft = false;
				showingRightClickMenu = false;
			}
			else if (mouseEvent.Button == MouseButtons.Right)
			{
				if (DefaultRightClick != null)
				{
					showingRightClickMenu = true;
					DefaultRightClick?.Invoke(this, mouseEvent);
				}
			}

			selectAllOnMouseUpIfNoSelection = false;
			base.OnMouseUp(mouseEvent);
		}

        public override string ToString()
        {
            return actualText;
        }
        protected enum DesiredXPositionOnLine
		{
			Maintain,
			Set
		}

		protected void FixBarPosition(DesiredXPositionOnLine desiredXPositionOnLine)
		{
			InsertBarPosition = internalTextWidget.Printer.GetOffsetLeftOfCharacterIndex(CharIndexToInsertBefore);
			if (desiredXPositionOnLine == DesiredXPositionOnLine.Set)
			{
				desiredBarX = InsertBarPosition.X;
			}

			Invalidate();
		}

        private void DeleteIndexRange(int startIndexInclusive, int endIndexInclusive)
        {
            startIndexInclusive = Math.Max(0, Math.Min(startIndexInclusive, actualText.Length));
            endIndexInclusive = Math.Max(startIndexInclusive, Math.Min(endIndexInclusive, actualText.Length));
            int lengthToDelete = endIndexInclusive + 1 - startIndexInclusive;
            if (lengthToDelete > 0 && actualText.Length - startIndexInclusive >= lengthToDelete)
            {
                var stringBuilder = new StringBuilder(actualText);
                stringBuilder.Remove(startIndexInclusive, lengthToDelete);
                actualText = stringBuilder.ToString();
                UpdateDisplayText();
                OnTextChanged(null);
                Invalidate();
            }
        }

        public void DeleteSelection(bool createUndoMarker = true)
        {
            if (ReadOnly)
            {
                return;
            }

            if (Selecting)
            {
                if (CharIndexToInsertBefore < SelectionIndexToStartBefore)
                {
                    DeleteIndexRange(CharIndexToInsertBefore, SelectionIndexToStartBefore - 1);
                }
                else
                {
                    DeleteIndexRange(SelectionIndexToStartBefore, CharIndexToInsertBefore - 1);
                    CharIndexToInsertBefore = SelectionIndexToStartBefore;
                }

                if (createUndoMarker)
                {
                    var newUndoDeleteData = new TextWidgetUndoCommand(this);
                    undoBuffer.Add(newUndoDeleteData);
                }

                Selecting = false;
            }
        }

        public void SetSelection(int firstIndexSelected, int lastIndexSelected)
		{
			firstIndexSelected = Math.Max(0, Math.Min(firstIndexSelected, Text.Length - 1));
			lastIndexSelected = Math.Max(0, Math.Min(lastIndexSelected, Text.Length));

			SelectionIndexToStartBefore = firstIndexSelected;
			CharIndexToInsertBefore = lastIndexSelected + 1;
			Selecting = true;
			FixBarPosition(DesiredXPositionOnLine.Set);
		}

		private void StartSelectionIfRequired(KeyEventArgs keyEvent)
		{
			if (!Selecting && ShiftKeyIsDown(keyEvent))
			{
				Selecting = true;
				SelectionIndexToStartBefore = CharIndexToInsertBefore;
			}
		}

		private bool ShiftKeyIsDown(KeyEventArgs keyEvent)
		{
			return Keyboard.IsKeyDown(Keys.Shift)
				|| (keyEvent != null && keyEvent.Shift);
		}

		/// <summary>
		/// Whether this key event is asking to move (or delete) a whole word at a time.
		/// </summary>
		/// <remarks>
		/// The mac platform layer folds Command <em>and</em> physical Control both onto
		/// <see cref="Keys.Control"/> (see <c>MacSystemWindow.TranslateModifiers</c>), which is exactly what
		/// makes Command-A/X/C/V/Z arrive here correct with no Mac-specific code. The price is that Control
		/// is already spoken for on Mac, so word-wise motion moves to Option - and Control (that is,
		/// Command) has to mean start/end of line, because that is what a Mac user pressing Command-Left is
		/// actually asking for.
		/// </remarks>
		private static bool WordJumpRequested(KeyEventArgs keyEvent)
		{
			return UseMacKeyBindings ? keyEvent.Alt : keyEvent.Control;
		}

		/// <summary>
		/// Whether this key event is a Mac Command chord, which on the arrow keys means line-wise
		/// (left/right) or document-wise (up/down) motion. Always false off Mac - Windows spells those
		/// Home/End and Control+Home/End, and those cases are untouched.
		/// </summary>
		private static bool MacCommandRequested(KeyEventArgs keyEvent)
		{
			return UseMacKeyBindings && keyEvent.Control;
		}

		/// <summary>The four keys that move the caret, and so the four a Mac Command chord turns into
		/// line-wise or document-wise motion.</summary>
		private static bool IsArrowKey(Keys keyCode)
		{
			return keyCode == Keys.Left
				|| keyCode == Keys.Right
				|| keyCode == Keys.Up
				|| keyCode == Keys.Down;
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			// this must be called first to ensure we get the correct Handled state
			base.OnKeyDown(keyEvent);

			if (!keyEvent.Handled)
			{
				RestartBarFlash();

				bool setDesiredBarPosition = true;
				bool turnOffSelection = false;

				if (!ShiftKeyIsDown(keyEvent))
				{
					if (keyEvent.Control)
					{
						// don't let control keys get into the stream
						keyEvent.Handled = true;

						// A Mac Command-arrow is a plain caret motion, so unshifted it ends the selection
						// exactly as an unmodified arrow does. Only the arrows: every other Control chord
						// (copy, select all) deliberately keeps the selection. Windows never reaches this,
						// and must not - Control+Left there is a word jump, which does not end a selection.
						if (Selecting
							&& MacCommandRequested(keyEvent)
							&& IsArrowKey(keyEvent.KeyCode))
						{
							turnOffSelection = true;
						}
					}
					else if (Selecting)
					{
						turnOffSelection = true;
					}
				}

				switch (keyEvent.KeyCode)
				{
					case Keys.Escape:
						if (Selecting)
						{
							turnOffSelection = true;
							keyEvent.SuppressKeyPress = true;
							keyEvent.Handled = true;
						}

						break;

					case Keys.Left:
						StartSelectionIfRequired(keyEvent);
						if (WordJumpRequested(keyEvent))
						{
							CharIndexToInsertBefore = IndexOfPreviousToken(internalTextWidget.Text, CharIndexToInsertBefore);
						}
						else if (MacCommandRequested(keyEvent))
						{
							CharIndexToInsertBefore = GotoStartOfCurrentLine(internalTextWidget.Text, CharIndexToInsertBefore);
						}
						else if (CharIndexToInsertBefore > 0)
						{
							if (turnOffSelection)
							{
								CharIndexToInsertBefore = Math.Min(CharIndexToInsertBefore, SelectionIndexToStartBefore);
							}
							else
							{
								CharIndexToInsertBefore--;
							}
						}

						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						break;

					case Keys.Right:
						StartSelectionIfRequired(keyEvent);
						if (WordJumpRequested(keyEvent))
						{
							CharIndexToInsertBefore = IndexOfNextToken(internalTextWidget.Text, CharIndexToInsertBefore);
						}
						else if (MacCommandRequested(keyEvent))
						{
							// Note GotoEndOfCurrentLine works from actualText while the Left/Home side works
							// from internalTextWidget.Text, which is the masked text. Those two agree today
							// for everything that ships, but a masked *multiline* field would disagree about
							// where a line ends. Left as found rather than unified here.
							GotoEndOfCurrentLine();
						}
						else if (CharIndexToInsertBefore < internalTextWidget.Text.Length)
						{
							if (turnOffSelection)
							{
								CharIndexToInsertBefore = Math.Max(CharIndexToInsertBefore, SelectionIndexToStartBefore);
							}
							else
							{
								CharIndexToInsertBefore++;
							}
						}

						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						break;

					case Keys.Up:
						StartSelectionIfRequired(keyEvent);
						if (MacCommandRequested(keyEvent))
						{
							// Command-Up is the Mac spelling of Control+Home
							CharIndexToInsertBefore = 0;
						}
						else
						{
							if (turnOffSelection)
							{
								CharIndexToInsertBefore = Math.Min(CharIndexToInsertBefore, SelectionIndexToStartBefore);
							}

							GotoLineAbove();
							setDesiredBarPosition = false;
						}

						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						break;

					case Keys.Down:
						StartSelectionIfRequired(keyEvent);
						if (MacCommandRequested(keyEvent))
						{
							// Command-Down is the Mac spelling of Control+End
							CharIndexToInsertBefore = internalTextWidget.Text.Length;
						}
						else
						{
							if (turnOffSelection)
							{
								CharIndexToInsertBefore = Math.Max(CharIndexToInsertBefore, SelectionIndexToStartBefore);
							}

							GotoLineBelow();
							setDesiredBarPosition = false;
						}

						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						break;

					case Keys.Space:
						keyEvent.Handled = true;
						break;

					case Keys.PageDown:
						StartSelectionIfRequired(keyEvent);
						{
							var scrollParent = Parent?.Parent;
							if (scrollParent != null)
							{
								var downLines = (int)(scrollParent.Height / internalTextWidget.Printer.TypeFaceStyle.EmSizeInPixels);
								// try to find downlines worth of cr and try to keep the same distance into the line
								for (int i = 0; i < downLines; i++)
								{
									GotoLineBelow();
								}
							}
						}

						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						break;

					case Keys.PageUp:
						StartSelectionIfRequired(keyEvent);
						{
							var scrollParent = Parent?.Parent;
							if (scrollParent != null)
							{
								var upLines = (int)(scrollParent.Height / internalTextWidget.Printer.TypeFaceStyle.EmSizeInPixels);
								// try to find downlines worth of cr and try to keep the same distance into the line
								for (int i = 0; i < upLines; i++)
								{
									GotoLineAbove();
								}
							}
						}

						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						break;

					case Keys.End:
						StartSelectionIfRequired(keyEvent);
						if (keyEvent.Control)
						{
							CharIndexToInsertBefore = internalTextWidget.Text.Length;
						}
						else
						{
							GotoEndOfCurrentLine();
						}

						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						break;

					case Keys.Home:
						StartSelectionIfRequired(keyEvent);
						if (keyEvent.Control)
						{
							CharIndexToInsertBefore = 0;
						}
						else
						{
							CharIndexToInsertBefore = GotoStartOfCurrentLine(internalTextWidget.Text, CharIndexToInsertBefore);
						}

						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						break;

					case Keys.Back:
						if (!Selecting
							&& CharIndexToInsertBefore > 0)
						{
							// Deleting is always "select back to here, then delete the selection", so the
							// Mac word/line variants only have to choose a different anchor. Windows has no
							// Control+Backspace binding here today and does not gain one.
							if (UseMacKeyBindings && keyEvent.Alt)
							{
								SelectionIndexToStartBefore = IndexOfPreviousToken(internalTextWidget.Text, CharIndexToInsertBefore);
							}
							else if (MacCommandRequested(keyEvent))
							{
								SelectionIndexToStartBefore = GotoStartOfCurrentLine(internalTextWidget.Text, CharIndexToInsertBefore);
							}
							else
							{
								SelectionIndexToStartBefore = CharIndexToInsertBefore - 1;
							}

							Selecting = true;
						}

						DeleteSelection();

						keyEvent.Handled = true;
						keyEvent.SuppressKeyPress = true;
						break;

					case Keys.Delete:
						if (ShiftKeyIsDown(keyEvent))
						{
							CopySelection();
							DeleteSelection();
							keyEvent.SuppressKeyPress = true;
						}
						else
						{
							if (!Selecting
							&& CharIndexToInsertBefore < internalTextWidget.Text.Length)
							{
								SelectionIndexToStartBefore = CharIndexToInsertBefore + 1;
								Selecting = true;
							}

							DeleteSelection();
						}

						turnOffSelection = true;
						keyEvent.Handled = true;
						keyEvent.SuppressKeyPress = true;
						break;

					case Keys.Enter:
						if (!Multiline)
						{
							// TODO: do the right thing.
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;

							if (EnterPressed != null)
							{
								EnterPressed(this, keyEvent);
							}

							if (TextHasChanged())
							{
								OnEditComplete(keyEvent);
							}
						}

						break;

					case Keys.Insert:
						if (ShiftKeyIsDown(keyEvent))
						{
							turnOffSelection = true;
							PasteFromClipboard();
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}

						if (keyEvent.Control)
						{
							turnOffSelection = false;
							CopySelection();
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}

						break;

					case Keys.A:
						if (keyEvent.Control)
						{
							SelectAll();
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}

						break;

					case Keys.X:
						if (keyEvent.Control)
						{
							CopySelection();
							DeleteSelection();
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}

						break;

					case Keys.C:
						if (keyEvent.Control)
						{
							turnOffSelection = false;
							CopySelection();
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}

						break;

					case Keys.V:
						if (keyEvent.Control)
						{
							PasteFromClipboard();
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}

						break;

					case Keys.Z:
						if (keyEvent.Control)
						{
							if (keyEvent.Shift)
							{
								Redo();
							}
							else
							{
								Undo();
							}

							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}

						break;

					case Keys.Y:
						if (keyEvent.Control)
						{
							Redo();
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
						}

						break;
				}

				if (setDesiredBarPosition)
				{
					FixBarPosition(DesiredXPositionOnLine.Set);
				}
				else
				{
					FixBarPosition(DesiredXPositionOnLine.Maintain);
				}

				// if we are not going to type a character, and therefore replace the selection, turn off the selection now if needed.
				if (keyEvent.SuppressKeyPress && turnOffSelection)
				{
					Selecting = false;
				}

				Invalidate();
			}
		}

		public void Undo()
		{
			undoBuffer.Undo();
			FixBarPosition(DesiredXPositionOnLine.Set);
		}

		public void Redo()
		{
			undoBuffer.Redo();
			FixBarPosition(DesiredXPositionOnLine.Set);
		}

        public void CopySelection()
        {
            if (Selecting)
            {
                var text = actualText;
                var charIndexToInsertBefore = Math.Max(0, Math.Min(text.Length, CharIndexToInsertBefore));
                var selectionIndexToStartBefore = Math.Max(0, Math.Min(text.Length, SelectionIndexToStartBefore));
                if (charIndexToInsertBefore < selectionIndexToStartBefore)
                {
                    Clipboard.Instance.SetText(text.Substring(charIndexToInsertBefore, selectionIndexToStartBefore - charIndexToInsertBefore));
                }
                else
                {
                    Clipboard.Instance.SetText(text.Substring(selectionIndexToStartBefore, charIndexToInsertBefore - selectionIndexToStartBefore));
                }
            }
            else if (Multiline)
            {
                // copy the line?
            }
        }

        public void PasteFromClipboard()
        {
            if (ReadOnly)
            {
                return;
            }

            if (Clipboard.Instance.ContainsText)
            {
                if (Selecting)
                {
                    DeleteSelection(false);
                }

                var stringBuilder = new StringBuilder(actualText);
                string stringOnClipboard = Clipboard.Instance.GetText();
                if (!Multiline)
                {
                    stringOnClipboard = Regex.Replace(stringOnClipboard, @"\r\n?|\n", " ");
                }
				else
				{
					stringOnClipboard = NormalizeLineEndings(stringOnClipboard);
				}

                stringBuilder.Insert(CharIndexToInsertBefore, stringOnClipboard);
                CharIndexToInsertBefore += stringOnClipboard.Length;
                actualText = NormalizeLineEndings(stringBuilder.ToString());
                UpdateDisplayText();

                var newUndoCommand = new TextWidgetUndoCommand(this);
                undoBuffer.Add(newUndoCommand);
            }
        }

        public override void OnKeyPress(KeyPressEventArgs keyPressEvent)
        {
            base.OnKeyPress(keyPressEvent);

            if (!keyPressEvent.Handled)
            {
                if (keyPressEvent.KeyChar < 32
                    && keyPressEvent.KeyChar != 13
                    && keyPressEvent.KeyChar != 9)
                {
                    return;
                }

                if (ReadOnly)
                {
                    return;
                }

                if (Selecting)
                {
                    DeleteSelection();
                    Selecting = false;
                }

                var tempString = new StringBuilder(actualText);
                if (keyPressEvent.KeyChar == '\r')
                {
                    tempString.Insert(CharIndexToInsertBefore, "\n");
                }
                else
                {
                    tempString.Insert(CharIndexToInsertBefore, keyPressEvent.KeyChar.ToString());
                }

                keyPressEvent.Handled = true;
                CharIndexToInsertBefore++;
                actualText = tempString.ToString();
                UpdateDisplayText();

                FixBarPosition(DesiredXPositionOnLine.Set);

                var newUndoData = new TextWidgetUndoCommand(this);
                if (MergeTypingDuringUndo
                    && charIndexToAcceptAsMerging == CharIndexToInsertBefore - 1
                    && keyPressEvent.KeyChar != '\n' && keyPressEvent.KeyChar != '\r')
                {
                    undoBuffer.Add(newUndoData);
                }
                else
                {
                    undoBuffer.Add(newUndoData);
                }

                charIndexToAcceptAsMerging = CharIndexToInsertBefore;
            }
        }

        private int GetIndexOffset(int characterStartIndexInclusive, int maxCharacterEndIndexInclusive, double desiredPixelOffset)
		{
			int offsetIndex = 0;
			int endOffsetIndex = maxCharacterEndIndexInclusive - characterStartIndexInclusive;
			var offset = default(Vector2);
			var lastOffset = default(Vector2);
			while (true)
			{
				internalTextWidget.Printer.GetOffset(characterStartIndexInclusive, characterStartIndexInclusive + offsetIndex, out offset);
				offsetIndex++;
				if (offset.X >= desiredPixelOffset || offsetIndex >= endOffsetIndex)
				{
					if (Math.Abs(offset.Y) < .01
						&& Math.Abs(lastOffset.X - desiredPixelOffset) < Math.Abs(offset.X - desiredPixelOffset))
					{
						offsetIndex--;
					}

					break;
				}

				lastOffset = offset;
			}

			int maxLength = Math.Min(maxCharacterEndIndexInclusive - characterStartIndexInclusive, offsetIndex);
			return characterStartIndexInclusive + maxLength;
		}

        // the '\n' is always considered to be the end of the line.
        // if startIndexInclusive == endIndexInclusive, the line is empty (other than the return)
        private void GetStartAndEndIndexForLineContainingChar(int charToFindLineContaining, out int startIndexOfLineInclusive, out int endIndexOfLineInclusive)
        {
            startIndexOfLineInclusive = 0;
            endIndexOfLineInclusive = actualText.Length;
            if (endIndexOfLineInclusive == 0)
            {
                return;
            }

            charToFindLineContaining = Math.Max(Math.Min(charToFindLineContaining, actualText.Length), 0);

            if (charToFindLineContaining == actualText.Length
                || actualText[charToFindLineContaining] == '\n')
            {
                endIndexOfLineInclusive = charToFindLineContaining;
            }
            else
            {
                int endReturn = actualText.IndexOf('\n', charToFindLineContaining + 1);
                if (endReturn != -1)
                {
                    endIndexOfLineInclusive = endReturn;
                }
            }

            bool isIndex0AndNL = endIndexOfLineInclusive == 0 && actualText[endIndexOfLineInclusive] == '\n';
            if (isIndex0AndNL || actualText[endIndexOfLineInclusive - 1] == '\n')
            {
                startIndexOfLineInclusive = endIndexOfLineInclusive;
            }
            else
            {
                int returnAtStartOfCurrentLine = actualText.LastIndexOf('\n', endIndexOfLineInclusive - 1);
                if (returnAtStartOfCurrentLine != -1)
                {
                    startIndexOfLineInclusive = returnAtStartOfCurrentLine + 1;
                }
            }
        }

        private void GotoLineAbove()
		{
			GetStartAndEndIndexForLineContainingChar(CharIndexToInsertBefore, out int startIndexInclusive, out int endIndexInclusive);

			GetStartAndEndIndexForLineContainingChar(startIndexInclusive - 1, out int prevStartIndexInclusive, out int prevEndIndexInclusive);
			// we found the extents of the line above now put the cursor in the right place.
			CharIndexToInsertBefore = GetIndexOffset(prevStartIndexInclusive, prevEndIndexInclusive, desiredBarX);
		}

		private void GotoLineBelow()
		{
			GetStartAndEndIndexForLineContainingChar(CharIndexToInsertBefore, out int startIndexInclusive, out int endIndexInclusive);

			GetStartAndEndIndexForLineContainingChar(endIndexInclusive + 1, out int nextStartIndexInclusive, out int nextEndIndexInclusive);
			// we found the extents of the line above now put the cursor in the right place.
			CharIndexToInsertBefore = GetIndexOffset(nextStartIndexInclusive, nextEndIndexInclusive, desiredBarX);
		}

		public static int IndexOfNextToken(string text, int cursor)
		{
			var insert = cursor;
			var length = text.Length;
			if (insert == text.Length)
			{
				// If we are already at the end, return.
				return text.Length;
			}

			// if we are starting an a CR
			if (text[insert] == '\n')
			{
				// If we are on a CR advance one (goto next line)
				insert++;
				// and skip ' ' and '\t'
				while (insert < length 
					&& (text[insert] == ' ' || text[insert] == '\t'))
				{
					insert++;
				}

				return insert;
			}
			else if (WordBreakChars.Contains(text[insert]))
			{
				// we are starting on a work break char
				// while we are on the same char advance
				var current = text[insert];
				while (insert < length && text[insert]  == current)
				{
					insert++;
				}
			}
			else
			{
				// we are starting on a normal character
				while (insert < length && !WordBreakCharsAndCR.Contains(text[insert]))
				{
					insert++;
				}

				// and also skip ' ' and '\t'
				while (insert < length
					&& (text[insert] == ' ' || text[insert] == '\t'))
				{
					insert++;
				}
			}

			return insert;
		}

		public static int IndexOfPreviousToken(string text, int cursor)
		{
			if (cursor == 0)
			{
				return 0;
			}

			int prevToken = Math.Max(0, Math.Min(text.Length - 1, cursor - 1));
			var token = text[prevToken];

			if (text[prevToken] == '\n')
			{
				if (prevToken > 0
					&& text[prevToken - 1] == '\n')
				{
					return prevToken;
				}

				prevToken--;
			}
			else if (token == ' ' || token == '\t')
			{
				// the token to the left is a breaking character
				while (--prevToken >= 0
					&& (text[prevToken] == ' ' || text[prevToken] == '\t'))
				{
					// skip back the entire token
				}
			}
			else if (WordBreakChars.Contains(token))
			{
				// the token to the left is a breaking character
				while (--prevToken >= 0 && text[prevToken] == token)
				{
					// skip back the entire token
				}

				return prevToken + 1;
			}

			// the token to the left is normal character skip until a break
			while (prevToken >= 0 && !WordBreakCharsAndCR.Contains(text[prevToken]))
			{
				// skip back until we are on a word break
				prevToken--;
			}

			return prevToken + 1;
		}

        public void SelectAll()
        {
            CharIndexToInsertBefore = actualText.Length;
            SelectionIndexToStartBefore = 0;
            Selecting = true;
            FixBarPosition(DesiredXPositionOnLine.Set);
            if (AllSelected != null)
            {
                AllSelected(this, null);
            }
        }

        internal void GotoEndOfCurrentLine()
        {
            int indexOfReturn = actualText.IndexOf('\n', CharIndexToInsertBefore);
            if (indexOfReturn == -1)
            {
                CharIndexToInsertBefore = actualText.Length;
            }
            else
            {
                CharIndexToInsertBefore = indexOfReturn;
            }

            FixBarPosition(DesiredXPositionOnLine.Set);
        }

        public static int GotoStartOfCurrentLine(string text, int cursor)
		{
			if (cursor > 0)
			{
				int indexOfReturn = text.LastIndexOf('\n', cursor - 1);
				if (indexOfReturn == -1)
				{
					return 0;
				}
				else
				{
					var firstNonWhiteSpaceRegex = new Regex("[^\\t ]");
					Match firstNonWhiteSpace = firstNonWhiteSpaceRegex.Match(text, indexOfReturn + 1);
					if (firstNonWhiteSpace.Success)
					{
						if (firstNonWhiteSpace.Index < cursor
						   || text[cursor - 1] == '\n')
						{
							return firstNonWhiteSpace.Index;
						}
					}

					return indexOfReturn + 1;
				}
			}

			return 0;
		}

		public void ClearUndoHistory()
		{
			undoBuffer.ClearHistory();
			var newUndoData = new TextWidgetUndoCommand(this);
			undoBuffer.Add(newUndoData);
		}

		public void SetTextAsUndoBaseline(string text, int charIndex = 0)
		{
			actualText = NormalizeLineEndings(text, charIndex, out int normalizedCharIndex);
			UpdateDisplayText();
			OnTextChanged(null);
			SetCursorPosition(normalizedCharIndex);
			ClearUndoHistory();
			Invalidate();
		}

		public void SetCursorPosition(int charIndex)
		{
			CharIndexToInsertBefore = Math.Max(0, Math.Min(charIndex, Text.Length));
			SelectionIndexToStartBefore = CharIndexToInsertBefore;
			Selecting = false;
			FixBarPosition(DesiredXPositionOnLine.Set);
		}
	}
}