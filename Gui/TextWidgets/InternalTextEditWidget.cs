﻿/*
Copyright (c) 2014, Lars Brubaker
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

		public static Action<InternalTextEditWidget, MouseEventArgs> DefaultRightClick;

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
            actualText = text;
            UpdateDisplayText();
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
                if (actualText != value)
                {
                    CharIndexToInsertBefore = 0;
                    actualText = value ?? "";
                    UpdateDisplayText();
                    OnTextChanged(null);
                    Invalidate();
                }
            }
        }

        public InternalTextEditWidget(string text, double pointSize, bool multiLine, int tabIndex, TypeFace typeFace = null)
        {
            if (DefaultRightClick == null)
            {
                var menuTheme = ThemeConfig.DefaultMenuTheme();
                InternalTextEditWidget.AddTextWidgetRightClickMenu(menuTheme);
            }

            TabIndex = tabIndex;
            TabStop = true;
            MergeTypingDuringUndo = true;

            actualText = text ?? "";
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

		private bool BarIsShowing { get { return timeSinceTurnOn.ElapsedMilliseconds < barOnTime * 1000; } }

		public void OnIdle()
		{
			if (this.Focused
				&& timeSinceTurnOn.ElapsedMilliseconds >= barOnTime * 950
				&& !HasBeenClosed)
			{
				if (timeSinceTurnOn.ElapsedMilliseconds >= (barOnTime + barOffTime) * 950)
				{
					RestartBarFlash();
				}
				else
				{
					UiThread.RunOnIdle(OnIdle, barOffTime);
					Invalidate();
				}
			}
			else
			{
				timeSinceTurnOn.Stop();
			}
		}

		private void RestartBarFlash()
		{
			timeSinceTurnOn.Restart();
			UiThread.RunOnIdle(OnIdle, barOnTime);
			Invalidate();
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
				timeSinceTurnOn.Restart();
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

		public Color CursorColor { get; set; }

		public Color HighlightColor { get; set; }

		private Color _textColor = Color.Black;

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
				CursorColor = value.WithAlpha(175);
				HighlightColor = value.WithAlpha(100);
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
                var bar2 = new RectangleDouble(Math.Ceiling(InsertBarPosition.X) - xFraction,
                                        Math.Ceiling(internalTextWidget.Height + InsertBarPosition.Y - fontHeight),
                                        Math.Ceiling(InsertBarPosition.X + 1) - xFraction,
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
						if (keyEvent.Control)
						{
							CharIndexToInsertBefore = IndexOfPreviousToken(internalTextWidget.Text, CharIndexToInsertBefore);
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
						if (keyEvent.Control)
						{
							CharIndexToInsertBefore = IndexOfNextToken(internalTextWidget.Text, CharIndexToInsertBefore);
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
						if (turnOffSelection)
						{
							CharIndexToInsertBefore = Math.Min(CharIndexToInsertBefore, SelectionIndexToStartBefore);
						}

						GotoLineAbove();
						setDesiredBarPosition = false;
						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						break;

					case Keys.Down:
						StartSelectionIfRequired(keyEvent);
						if (turnOffSelection)
						{
							CharIndexToInsertBefore = Math.Max(CharIndexToInsertBefore, SelectionIndexToStartBefore);
						}

						GotoLineBelow();
						setDesiredBarPosition = false;
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
							SelectionIndexToStartBefore = CharIndexToInsertBefore - 1;
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

                stringBuilder.Insert(CharIndexToInsertBefore, stringOnClipboard);
                CharIndexToInsertBefore += stringOnClipboard.Length;
                actualText = stringBuilder.ToString();
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
	}
}