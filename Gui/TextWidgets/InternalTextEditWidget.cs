/*
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

using MatterHackers.Agg.Font;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace MatterHackers.Agg.UI
{
	public class InternalTextEditWidget : GuiWidget
	{
		private static ReadOnlyCollection<char> defaultWordBreakChars;

		private static ReadOnlyCollection<char> DefaultWordBreakChars
		{
			get
			{
				if (defaultWordBreakChars == null)
				{
					char[] defaultList = new char[] { ' ', '\n', '(', ')' };
					defaultWordBreakChars = new ReadOnlyCollection<char>(defaultList);
				}

				return defaultWordBreakChars;
			}
		}

		public IList<char> WordBreakChars;

		public event KeyEventHandler EnterPressed;

		public event EventHandler AllSelected;

		private UndoBuffer undoBuffer = new UndoBuffer();

		private bool mouseIsDown = false;
		private bool selecting;

		public bool Selecting
		{
			get { return selecting; }
			set
			{
				if (selecting != value)
				{
					selecting = value;
					Invalidate();
				}
			}
		}

		private int selectionIndexToStartBefore;

		public int SelectionIndexToStartBefore
		{
			get { return selectionIndexToStartBefore; }
			set { selectionIndexToStartBefore = value; }
		}

		private int charIndexToInsertBefore;

		public int CharIndexToInsertBefore
		{
			get { return charIndexToInsertBefore; }
			set { charIndexToInsertBefore = value; }
		}

		private int charIndexToAcceptAsMerging;

		private double desiredBarX;

		private TextWidget internalTextWidget;
		private bool isMultiLine = true;

		public bool MergeTypingDuringUndo { get; set; }

		public event EventHandler InsertBarPositionChanged;

		/// <summary>
		/// This event fires when the user has finished editing the control.
		/// Fired on leave event after editing, or on enter key for non-multi line controls.
		/// </summary>
		public event EventHandler EditComplete;

		private Vector2 insertBarPosition;

		public Vector2 InsertBarPosition
		{
			get { return insertBarPosition; }
			set
			{
				if (insertBarPosition != value)
				{
					insertBarPosition = value;
					OnInsertBarPositionChanged();
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

		/// <summary>
		/// This is called when the user has modified the text control.  It will
		/// be triggered when the control looses focus or enter is pressed on non-multiline control.
		/// </summary>
		public virtual void OnEditComplete()
		{
			if (EditComplete != null)
			{
				EditComplete(this, null);
			}
			textWhenGotFocus = Text;
		}

		private void OnInsertBarPositionChanged()
		{
			if (InsertBarPositionChanged != null)
			{
				InsertBarPositionChanged(this, null);
			}
		}

		public string Selection
		{
			get
			{
				if (Selecting)
				{
					if (CharIndexToInsertBefore < SelectionIndexToStartBefore)
					{
						return Text.Substring(CharIndexToInsertBefore, (SelectionIndexToStartBefore - CharIndexToInsertBefore));
					}
					else
					{
						return Text.Substring(SelectionIndexToStartBefore, (CharIndexToInsertBefore - SelectionIndexToStartBefore));
					}
				}

				return "";
			}
		}

		public override String Text
		{
			get
			{
				return internalTextWidget.Text;
			}

			set
			{
				if (internalTextWidget.Text != value)
				{
					CharIndexToInsertBefore = 0;
					internalTextWidget.Text = value;
					OnTextChanged(null);
					Invalidate();
				}
			}
		}

		public InternalTextEditWidget(string text, double pointSize, bool multiLine, int tabIndex)
		{
			TabIndex = tabIndex;
			TabStop = true;
			WordBreakChars = DefaultWordBreakChars;
			MergeTypingDuringUndo = true;

			internalTextWidget = new TextWidget(text, pointSize: pointSize, ellipsisIfClipped: false, textColor: textColor);
			internalTextWidget.Selectable = false;
			internalTextWidget.AutoExpandBoundsToText = true;
			AddChild(internalTextWidget);

			UpdateLocalBounds();

			Multiline = multiLine;

			FixBarPosition(DesiredXPositionOnLine.Set);

			TextWidgetUndoData newUndoData = new TextWidgetUndoData(this);
			undoBuffer.Add(newUndoData, "Initial", UndoBuffer.MergeType.NotMergable);

			Cursor = Cursors.IBeam;

			internalTextWidget.TextChanged += new EventHandler(internalTextWidget_TextChanged);
			internalTextWidget.BoundsChanged += new EventHandler(internalTextWidget_BoundsChanged);
		}

		private void UpdateLocalBounds()
		{
			//double padding = 5;
			double width = Math.Max(internalTextWidget.Width + 2, 3);
			double height = Math.Max(internalTextWidget.Height, internalTextWidget.Printer.TypeFaceStyle.EmSizeInPixels);
			//LocalBounds = new RectangleDouble(this.BorderWidth - padding, this.BorderWidth - padding, width + this.BorderWidth + padding, height + this.BorderWidth + padding);
			LocalBounds = new RectangleDouble(-1, 0, width, height);
			// TODO: text widget should have some padding rather than the 1 on the x below.  LBB 2013/02/03
			internalTextWidget.OriginRelativeParent = new Vector2(1, -internalTextWidget.LocalBounds.Bottom);
		}

		private void internalTextWidget_BoundsChanged(object sender, EventArgs e)
		{
			UpdateLocalBounds();
		}

		private void internalTextWidget_TextChanged(object sender, EventArgs e)
		{
			OnTextChanged(e);
		}

		public bool Multiline
		{
			get
			{
				return isMultiLine;
			}

			set
			{
				isMultiLine = value;
			}
		}

		private Stopwatch timeSinceTurnOn = new Stopwatch();
		private double barOnTime = .6;
		private double barOffTime = .6;

		private bool BarIsShowing { get { return timeSinceTurnOn.ElapsedMilliseconds < barOnTime * 1000; } }

		public void OnIdle(object state)
		{
			if (this.Focused
				&& timeSinceTurnOn.ElapsedMilliseconds >= barOnTime * 1000
				&& !WidgetHasBeenClosed)
			{
				if (timeSinceTurnOn.ElapsedMilliseconds > (barOnTime + barOffTime) * 1000)
				{
					RestartBarFlash();
				}
				else
				{
					UiThread.RunOnIdle(OnIdle, barOffTime);
					Invalidate();
				}
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

		public override void OnGotFocus(EventArgs e)
		{
			RestartBarFlash();
			textWhenGotFocus = Text;
			timeSinceTurnOn.Restart();
			if (SelectAllOnFocus)
			{
				selectAllOnMouseUpIfNoSelection = true;
			}
			base.OnGotFocus(e);
		}

		public override void OnLostFocus(EventArgs e)
		{
			Selecting = false;
			Invalidate();
			if (TextHasChanged())
			{
				OnEditComplete();
			}
			base.OnLostFocus(e);
		}

		public bool TextHasChanged()
		{
			return textWhenGotFocus != Text;
		}

		public RGBA_Bytes cursorColor = RGBA_Bytes.DarkGray;
		public RGBA_Bytes highlightColor = RGBA_Bytes.Gray;
		public RGBA_Bytes borderColor = RGBA_Bytes.White;
		public RGBA_Bytes textColor = RGBA_Bytes.Black;
		public int borderWidth = 0;
		public int borderRadius = 0;

		public int BorderWidth
		{
			get
			{
				return this.borderWidth;
			}
			set
			{
				this.borderWidth = value;
				UpdateLocalBounds();
			}
		}

		public RGBA_Bytes TextColor
		{
			get
			{
				return textColor;
			}
			set
			{
				this.textColor = value;
				internalTextWidget.TextColor = this.textColor;
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double fontHeight = internalTextWidget.Printer.TypeFaceStyle.EmSizeInPixels;

			if (Selecting
				&& SelectionIndexToStartBefore != CharIndexToInsertBefore)
			{
				Vector2 selectPosition = internalTextWidget.Printer.GetOffsetLeftOfCharacterIndex(SelectionIndexToStartBefore);

				// for each selected line draw a rect for the chars of that line
				if (selectPosition.y == InsertBarPosition.y)
				{
					RectangleDouble bar = new RectangleDouble(Math.Ceiling(selectPosition.x),
											Math.Ceiling(internalTextWidget.Height + selectPosition.y),
											Math.Ceiling(InsertBarPosition.x + 1),
											Math.Ceiling(internalTextWidget.Height + InsertBarPosition.y - fontHeight));

					RoundedRect selectCursorRect = new RoundedRect(bar, 0);
					graphics2D.Render(selectCursorRect, this.highlightColor);
				}
				else
				{
					int firstCharToHighlight = Math.Min(CharIndexToInsertBefore, SelectionIndexToStartBefore);
					int lastCharToHighlight = Math.Max(CharIndexToInsertBefore, SelectionIndexToStartBefore);
					int lineStart = firstCharToHighlight;
					Vector2 lineStartPos = internalTextWidget.Printer.GetOffsetLeftOfCharacterIndex(lineStart);
					int lineEnd = lineStart + 1;
					Vector2 lineEndPos = internalTextWidget.Printer.GetOffsetLeftOfCharacterIndex(lineEnd);
					if (lineEndPos.y != lineStartPos.y)
					{
						// we are starting on a '\n', adjust so we will show the cr at the end of the line
						lineEndPos = lineStartPos;
					}
					bool firstCharOfLine = false;
					for (int i = lineEnd; i < lastCharToHighlight + 1; i++)
					{
						Vector2 nextPos = internalTextWidget.Printer.GetOffsetLeftOfCharacterIndex(i);
						if (firstCharOfLine)
						{
							if (lineEndPos.y != lineStartPos.y)
							{
								// we are starting on a '\n', adjust so we will show the cr at the end of the line
								lineEndPos = lineStartPos;
							}
							firstCharOfLine = false;
						}
						if (nextPos.y != lineStartPos.y)
						{
							if (lineEndPos.x == lineStartPos.x)
							{
								lineEndPos.x += Printer.TypeFaceStyle.GetAdvanceForCharacter(' ');
							}
							RectangleDouble bar = new RectangleDouble(Math.Ceiling(lineStartPos.x),
													Math.Ceiling(internalTextWidget.Height + lineStartPos.y),
													Math.Ceiling(lineEndPos.x + 1),
													Math.Ceiling(internalTextWidget.Height + lineEndPos.y - fontHeight));

							RoundedRect selectCursorRect = new RoundedRect(bar, 0);
							graphics2D.Render(selectCursorRect, this.highlightColor);
							lineStartPos = nextPos;
							firstCharOfLine = true;
						}
						else
						{
							lineEndPos = nextPos;
						}
					}
					if (lineEndPos.x != lineStartPos.x)
					{
						RectangleDouble bar = new RectangleDouble(Math.Ceiling(lineStartPos.x),
												Math.Ceiling(internalTextWidget.Height + lineStartPos.y),
												Math.Ceiling(lineEndPos.x + 1),
												Math.Ceiling(internalTextWidget.Height + lineEndPos.y - fontHeight));

						RoundedRect selectCursorRect = new RoundedRect(bar, 0);
						graphics2D.Render(selectCursorRect, this.highlightColor);
					}
				}
			}

			if (this.Focused && BarIsShowing)
			{
				double xFraction = graphics2D.GetTransform().tx;
				xFraction = xFraction - (int)xFraction;
				RectangleDouble bar2 = new RectangleDouble(Math.Ceiling(InsertBarPosition.x) - xFraction,
										Math.Ceiling(internalTextWidget.Height + InsertBarPosition.y - fontHeight),
										Math.Ceiling(InsertBarPosition.x + 1) - xFraction,
										Math.Ceiling(internalTextWidget.Height + InsertBarPosition.y));
				RoundedRect cursorRect = new RoundedRect(bar2, 0);
				graphics2D.Render(cursorRect, this.cursorColor);
			}

			RectangleDouble boundsPlusPoint5 = LocalBounds;
			boundsPlusPoint5.Inflate(-.5);
			RoundedRect borderRect = new RoundedRect(boundsPlusPoint5, 0);
			Stroke borderLine = new Stroke(borderRect);

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			CharIndexToInsertBefore = internalTextWidget.Printer.GetCharacterIndexToStartBefore(new Vector2(mouseEvent.X, mouseEvent.Y));
			if (mouseEvent.Clicks < 2)
			{
				if (CharIndexToInsertBefore == -1)
				{
					// we could not find any characters when looking for mouse click position
					CharIndexToInsertBefore = 0;
				}
				SelectionIndexToStartBefore = CharIndexToInsertBefore;
				Selecting = false;
				mouseIsDown = true;
			}
			else if (mouseEvent.Clicks == 2)
			{
				while (CharIndexToInsertBefore >= Text.Length || (CharIndexToInsertBefore > -1 && !WordBreakChars.Contains(Text[CharIndexToInsertBefore])))
				{
					CharIndexToInsertBefore--;
				}
				CharIndexToInsertBefore++;
				SelectionIndexToStartBefore = CharIndexToInsertBefore + 1;
				while (SelectionIndexToStartBefore < Text.Length && !WordBreakChars.Contains(Text[SelectionIndexToStartBefore]))
				{
					SelectionIndexToStartBefore++;
				}
				Selecting = true;
			}
			RestartBarFlash();
			FixBarPosition(DesiredXPositionOnLine.Set);

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (mouseIsDown)
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
			mouseIsDown = false;
			if (SelectAllOnFocus
				&& selectAllOnMouseUpIfNoSelection
				&& Selecting == false)
			{
				SelectAll();
			}
			selectAllOnMouseUpIfNoSelection = false;
			base.OnMouseUp(mouseEvent);
		}

		public override string ToString()
		{
			return internalTextWidget.Text;
		}

		protected enum DesiredXPositionOnLine { Maintain, Set };

		protected void FixBarPosition(DesiredXPositionOnLine desiredXPositionOnLine)
		{
			InsertBarPosition = internalTextWidget.Printer.GetOffsetLeftOfCharacterIndex(CharIndexToInsertBefore);
			if (desiredXPositionOnLine == DesiredXPositionOnLine.Set)
			{
				desiredBarX = InsertBarPosition.x;
			}
			Invalidate();
		}

		private void DeleteIndex(int startIndexInclusive)
		{
			DeleteIndexRange(startIndexInclusive, startIndexInclusive);
		}

		private void DeleteIndexRange(int startIndexInclusive, int endIndexInclusive)
		{
			// first make sure we are deleting something that exists
			startIndexInclusive = Math.Max(0, Math.Min(startIndexInclusive, internalTextWidget.Text.Length));
			endIndexInclusive = Math.Max(startIndexInclusive, Math.Min(endIndexInclusive, internalTextWidget.Text.Length));
			int LengthToDelete = (endIndexInclusive + 1) - startIndexInclusive;
			if (LengthToDelete > 0 && internalTextWidget.Text.Length - startIndexInclusive >= LengthToDelete)
			{
				StringBuilder stringBuilder = new StringBuilder(internalTextWidget.Text);
				stringBuilder.Remove(startIndexInclusive, LengthToDelete);
				internalTextWidget.Text = stringBuilder.ToString();
			}
		}

		private void DeleteSelection(bool createUndoMarker = true)
		{
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
					TextWidgetUndoData newUndoDeleteData = new TextWidgetUndoData(this);
					undoBuffer.Add(newUndoDeleteData, "Delete", UndoBuffer.MergeType.NotMergable);
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
			RestartBarFlash();

			bool SetDesiredBarPosition = true;
			bool turnOffSelection = false;

			if (!ShiftKeyIsDown(keyEvent))
			{
				if (keyEvent.Control)
				{
					// don't let control keys get into the stream
					keyEvent.SuppressKeyPress = true;
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
						GotoBeginingOfPreviousToken();
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
						GotoBeginingOfNextToken();
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
					SetDesiredBarPosition = false;
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
					SetDesiredBarPosition = false;
					keyEvent.SuppressKeyPress = true;
					keyEvent.Handled = true;
					break;

				case Keys.Space:
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
						GotoStartOfCurrentLine();
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
						keyEvent.Handled = true;
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
							OnEditComplete();
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
						Undo();
						keyEvent.Handled = true;
						keyEvent.SuppressKeyPress = true;
					}
					break;

				case Keys.Y:
					if (keyEvent.Control)
					{
						TextWidgetUndoData undoData = (TextWidgetUndoData)undoBuffer.GetNextRedoObject();
						if (undoData != null)
						{
							undoData.ExtractData(this);
						}
						keyEvent.Handled = true;
						keyEvent.SuppressKeyPress = true;
					}
					break;
			}

			base.OnKeyDown(keyEvent);

			if (SetDesiredBarPosition)
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

		public void Undo()
		{
			TextWidgetUndoData undoData = (TextWidgetUndoData)undoBuffer.GetPrevUndoObject();
			if (undoData != null)
			{
				undoData.ExtractData(this);
				FixBarPosition(DesiredXPositionOnLine.Set);
			}
		}

		private void CopySelection()
		{
			if (Selecting)
			{
				if (CharIndexToInsertBefore < SelectionIndexToStartBefore)
				{
#if SILVERLIGHT
                    throw new NotImplementedException();
#else
					Clipboard.SetText(internalTextWidget.Text.Substring(CharIndexToInsertBefore, SelectionIndexToStartBefore - CharIndexToInsertBefore));
#endif
				}
				else
				{
#if SILVERLIGHT
                    throw new NotImplementedException();
#else
					Clipboard.SetText(internalTextWidget.Text.Substring(SelectionIndexToStartBefore, CharIndexToInsertBefore - SelectionIndexToStartBefore));
#endif
				}
			}
			else if (Multiline)
			{
				// copy the line?
			}
		}

		private void PasteFromClipboard()
		{
#if SILVERLIGHT
                    throw new NotImplementedException();
#else
			if (Clipboard.ContainsText())
			{
				if (Selecting)
				{
					DeleteSelection(false);
				}

				StringBuilder stringBuilder = new StringBuilder(internalTextWidget.Text);
				String stringOnClipboard = Clipboard.GetText();
				stringBuilder.Insert(CharIndexToInsertBefore, stringOnClipboard);
				CharIndexToInsertBefore += stringOnClipboard.Length;
				internalTextWidget.Text = stringBuilder.ToString();

				TextWidgetUndoData newUndoData = new TextWidgetUndoData(this);
				undoBuffer.Add(newUndoData, "Paste", UndoBuffer.MergeType.NotMergable);
			}
#endif
		}

		public override void OnKeyPress(KeyPressEventArgs keyPressEvent)
		{
			if (Selecting)
			{
				DeleteSelection();
				Selecting = false;
			}

			StringBuilder tempString = new StringBuilder(internalTextWidget.Text);
			tempString.Insert(CharIndexToInsertBefore, keyPressEvent.KeyChar.ToString());
			keyPressEvent.Handled = true;
			CharIndexToInsertBefore++;
			internalTextWidget.Text = tempString.ToString();
			base.OnKeyPress(keyPressEvent);

			FixBarPosition(DesiredXPositionOnLine.Set);

			TextWidgetUndoData newUndoData = new TextWidgetUndoData(this);
			if (MergeTypingDuringUndo
				&& charIndexToAcceptAsMerging == CharIndexToInsertBefore - 1
				&& keyPressEvent.KeyChar != '\n')
			{
				undoBuffer.Add(newUndoData, "Typing", UndoBuffer.MergeType.Mergable);
			}
			else
			{
				undoBuffer.Add(newUndoData, "Typing", UndoBuffer.MergeType.NotMergable);
			}
			charIndexToAcceptAsMerging = CharIndexToInsertBefore;
		}

		private int GetIndexOffset(int CharacterStartIndexInclusive, int MaxCharacterEndIndexInclusive, double DesiredPixelOffset)
		{
			int OffsetIndex = 0;
			int EndOffsetIndex = MaxCharacterEndIndexInclusive - CharacterStartIndexInclusive;
			Vector2 offset = new Vector2();
			Vector2 lastOffset = new Vector2();
			while (true)
			{
				internalTextWidget.Printer.GetOffset(CharacterStartIndexInclusive, CharacterStartIndexInclusive + OffsetIndex, out offset);
				OffsetIndex++;
				if (offset.x >= DesiredPixelOffset || OffsetIndex >= EndOffsetIndex)
				{
					if (Math.Abs(offset.y) < .01
						&& Math.Abs(lastOffset.x - DesiredPixelOffset) < Math.Abs(offset.x - DesiredPixelOffset))
					{
						OffsetIndex--;
					}
					break;
				}
				lastOffset = offset;
			}

			int MaxLength = Math.Min(MaxCharacterEndIndexInclusive - CharacterStartIndexInclusive, OffsetIndex);
			return CharacterStartIndexInclusive + MaxLength;
		}

		// the '\n' is always considered to be the end of the line.
		// if startIndexInclusive == endIndexInclusive, the line is empty (other than the return)
		private void GetStartAndEndIndexForLineContainingChar(int charToFindLineContaining, out int startIndexOfLineInclusive, out int endIndexOfLineInclusive)
		{
			startIndexOfLineInclusive = 0;
			endIndexOfLineInclusive = internalTextWidget.Text.Length;
			if (endIndexOfLineInclusive == 0)
			{
				return;
			}

			charToFindLineContaining = Math.Max(Math.Min(charToFindLineContaining, internalTextWidget.Text.Length), 0);
			// first lets find the end of the line.  Check if we are on a '\n'
			if (charToFindLineContaining == internalTextWidget.Text.Length
				|| internalTextWidget.Text[charToFindLineContaining] == '\n')
			{
				// we are on the end of the line
				endIndexOfLineInclusive = charToFindLineContaining;
			}
			else
			{
				int endReturn = internalTextWidget.Text.IndexOf('\n', charToFindLineContaining + 1);
				if (endReturn != -1)
				{
					endIndexOfLineInclusive = endReturn;
				}
			}

			// check if the line is empty (the character to our left is the '\n' on the previous line
			bool isIndex0AndNL = endIndexOfLineInclusive == 0 && internalTextWidget.Text[endIndexOfLineInclusive] == '\n';
			if (isIndex0AndNL || internalTextWidget.Text[endIndexOfLineInclusive - 1] == '\n')
			{
				// the line is empty the start = the end.
				startIndexOfLineInclusive = endIndexOfLineInclusive;
			}
			else
			{
				int returnAtStartOfCurrentLine = internalTextWidget.Text.LastIndexOf('\n', endIndexOfLineInclusive - 1);
				if (returnAtStartOfCurrentLine != -1)
				{
					startIndexOfLineInclusive = returnAtStartOfCurrentLine + 1;
				}
			}
		}

		private void GotoLineAbove()
		{
			int startIndexInclusive;
			int endIndexInclusive;
			GetStartAndEndIndexForLineContainingChar(CharIndexToInsertBefore, out startIndexInclusive, out endIndexInclusive);

			int prevStartIndexInclusive;
			int prevEndIndexInclusive;
			GetStartAndEndIndexForLineContainingChar(startIndexInclusive - 1, out prevStartIndexInclusive, out prevEndIndexInclusive);
			// we found the extents of the line above now put the cursor in the right place.
			CharIndexToInsertBefore = GetIndexOffset(prevStartIndexInclusive, prevEndIndexInclusive, desiredBarX);
		}

		private void GotoLineBelow()
		{
			int startIndexInclusive;
			int endIndexInclusive;
			GetStartAndEndIndexForLineContainingChar(CharIndexToInsertBefore, out startIndexInclusive, out endIndexInclusive);

			int nextStartIndexInclusive;
			int nextEndIndexInclusive;
			GetStartAndEndIndexForLineContainingChar(endIndexInclusive + 1, out nextStartIndexInclusive, out nextEndIndexInclusive);
			// we found the extents of the line above now put the cursor in the right place.
			CharIndexToInsertBefore = GetIndexOffset(nextStartIndexInclusive, nextEndIndexInclusive, desiredBarX);
		}

		private void GotoBeginingOfNextToken()
		{
			if (CharIndexToInsertBefore == internalTextWidget.Text.Length)
			{
				return;
			}

			bool SkippedWiteSpace = false;
			if (internalTextWidget.Text[CharIndexToInsertBefore] == '\n')
			{
				CharIndexToInsertBefore++;
				SkippedWiteSpace = true;
			}
			else
			{
				Regex firstWhiteSpaceRegex = new Regex("\\s");
				Match firstWhiteSpace = firstWhiteSpaceRegex.Match(internalTextWidget.Text, CharIndexToInsertBefore);
				if (firstWhiteSpace.Success)
				{
					SkippedWiteSpace = true;
					CharIndexToInsertBefore = firstWhiteSpace.Index;
				}
			}

			if (SkippedWiteSpace)
			{
				Regex firstNonWhiteSpaceRegex = new Regex("[^\\t ]");
				Match firstNonWhiteSpace = firstNonWhiteSpaceRegex.Match(internalTextWidget.Text, CharIndexToInsertBefore);
				if (firstNonWhiteSpace.Success)
				{
					CharIndexToInsertBefore = firstNonWhiteSpace.Index;
				}
			}
			else
			{
				GotoEndOfCurrentLine();
			}
		}

		private void GotoBeginingOfPreviousToken()
		{
			if (CharIndexToInsertBefore == 0)
			{
				return;
			}

			Regex firstNonWhiteSpaceRegex = new Regex("[^\\t ]", RegexOptions.RightToLeft);
			Match firstNonWhiteSpace = firstNonWhiteSpaceRegex.Match(internalTextWidget.Text, CharIndexToInsertBefore);
			if (firstNonWhiteSpace.Success)
			{
				if (internalTextWidget.Text[firstNonWhiteSpace.Index] == '\n')
				{
					if (firstNonWhiteSpace.Index < CharIndexToInsertBefore - 1)
					{
						CharIndexToInsertBefore = firstNonWhiteSpace.Index;
						return;
					}
					else
					{
						firstNonWhiteSpaceRegex = new Regex("[^\\t\\n ]", RegexOptions.RightToLeft);
						firstNonWhiteSpace = firstNonWhiteSpaceRegex.Match(internalTextWidget.Text, CharIndexToInsertBefore);
						if (firstNonWhiteSpace.Success)
						{
							CharIndexToInsertBefore = firstNonWhiteSpace.Index;
						}
					}
				}
				else
				{
					CharIndexToInsertBefore = firstNonWhiteSpace.Index;
				}

				Regex firstWhiteSpaceRegex = new Regex("\\s", RegexOptions.RightToLeft);
				Match firstWhiteSpace = firstWhiteSpaceRegex.Match(internalTextWidget.Text, CharIndexToInsertBefore);
				if (firstWhiteSpace.Success)
				{
					CharIndexToInsertBefore = firstWhiteSpace.Index + 1;
				}
				else
				{
					GotoStartOfCurrentLine();
				}
			}
		}

		public void SelectAll()
		{
			CharIndexToInsertBefore = internalTextWidget.Text.Length;
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
			int indexOfReturn = internalTextWidget.Text.IndexOf('\n', CharIndexToInsertBefore);
			if (indexOfReturn == -1)
			{
				CharIndexToInsertBefore = internalTextWidget.Text.Length;
			}
			else
			{
				CharIndexToInsertBefore = indexOfReturn;
			}
			FixBarPosition(DesiredXPositionOnLine.Set);
		}

		internal void GotoStartOfCurrentLine()
		{
			if (CharIndexToInsertBefore > 0)
			{
				int indexOfReturn = internalTextWidget.Text.LastIndexOf('\n', CharIndexToInsertBefore - 1);
				if (indexOfReturn == -1)
				{
					CharIndexToInsertBefore = 0;
				}
				else
				{
					Regex firstNonWhiteSpaceRegex = new Regex("[^\\t ]");
					Match firstNonWhiteSpace = firstNonWhiteSpaceRegex.Match(internalTextWidget.Text, indexOfReturn + 1);
					if (firstNonWhiteSpace.Success)
					{
						if (firstNonWhiteSpace.Index < CharIndexToInsertBefore
						   || internalTextWidget.Text[CharIndexToInsertBefore - 1] == '\n')
						{
							CharIndexToInsertBefore = firstNonWhiteSpace.Index;
							return;
						}
					}

					CharIndexToInsertBefore = indexOfReturn + 1;
				}
			}
		}

		public void ClearUndoHistory()
		{
			undoBuffer.ClearHistory();
		}
	}
}