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

namespace MatterHackers.Agg.UI
{
	public class TextEditWidget : ScrollableWidget
	{
		internal InternalTextEditWidget internalTextEditWidget;

		public InternalTextEditWidget InternalTextEditWidget
		{
			get { return internalTextEditWidget; }
		}

		public event KeyEventHandler EnterPressed;

		public event EventHandler EditComplete;

		private int borderWidth = 0;
		private int borderRadius = 0;

		public static event EventHandler ShowSoftwareKeyboard;

		public static event EventHandler HideSoftwareKeyboard;

		public static event EventHandler KeyboardCollapsed;

		public Color TextColor
		{
			get
			{
				return internalTextEditWidget.TextColor;
			}
			set
			{
				internalTextEditWidget.TextColor = value;
			}
		}

		public override int TabIndex
		{
			get
			{
				return base.TabIndex;
			}
			set
			{
				internalTextEditWidget.TabIndex = value;
			}
		}

		public Color CursorColor
		{
			get
			{
				return internalTextEditWidget.cursorColor;
			}
			set
			{
				internalTextEditWidget.cursorColor = value;
			}
		}

		public new bool DoubleBuffer
		{
			get
			{
				return InternalTextEditWidget.DoubleBuffer;
			}

			set
			{
				InternalTextEditWidget.DoubleBuffer = value;
			}
		}

		public void ClearUndoHistory()
		{
			internalTextEditWidget.ClearUndoHistory();
		}

		public Color HighlightColor
		{
			get
			{
				return internalTextEditWidget.highlightColor;
			}
			set
			{
				internalTextEditWidget.highlightColor = value;
			}
		}

		public override Color BorderColor
		{
			get
			{
				return internalTextEditWidget.borderColor;
			}
			set
			{
				internalTextEditWidget.borderColor = value;
			}
		}

		public int BorderWidth
		{
			get
			{
				return borderWidth;
			}
			set
			{
				this.borderWidth = value;
				internalTextEditWidget.BorderWidth = this.borderWidth;
			}
		}

		public bool Selecting
		{
			get { return internalTextEditWidget.Selecting; }
			set { internalTextEditWidget.Selecting = value; }
		}

		public bool Multiline
		{
			get { return internalTextEditWidget.Multiline; }
			set
			{
				internalTextEditWidget.Multiline = value;
				if (Multiline == true)
				{
					AutoScroll = true;
					VerticalScrollBar.Show = ScrollBar.ShowState.WhenRequired;
				}
				else
				{
					AutoScroll = false;
					VerticalScrollBar.Show = ScrollBar.ShowState.Never;
				}
			}
		}

		public int SelectionIndexToStartBefore
		{
			get { return internalTextEditWidget.SelectionIndexToStartBefore; }
			set { internalTextEditWidget.SelectionIndexToStartBefore = value; }
		}

		public int CharIndexToInsertBefore
		{
			get { return internalTextEditWidget.CharIndexToInsertBefore; }
			set { internalTextEditWidget.CharIndexToInsertBefore = value; }
		}

		public override string Text
		{
			get
			{
				return internalTextEditWidget.Text;
			}
			set
			{
				internalTextEditWidget.Text = value;
			}
		}

		public string Selection
		{
			get
			{
				return internalTextEditWidget.Selection;
			}
		}

		internal TextEditWidget()
		{
		}

		public TextEditWidget(string text = "", double x = 0, double y = 0, double pointSize = 12, double pixelWidth = 0, double pixelHeight = 0, bool multiLine = false, int tabIndex = 0, TypeFace typeFace = null)
		{
			internalTextEditWidget = new InternalTextEditWidget(text, pointSize, multiLine, tabIndex, typeFace: typeFace);
			HookUpToInternalWidget(pixelWidth, pixelHeight);
			OriginRelativeParent = new Vector2(x, y);

			HAnchor = HAnchor.Stretch;

			Multiline = multiLine;
		}

		public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
		{
			throw new Exception("You cannot add children to a TextEdit widget.");
		}

		protected void HookUpToInternalWidget(double pixelWidth, double pixelHeight)
		{
			Cursor = Cursors.IBeam;

			internalTextEditWidget.EditComplete += new EventHandler(internalTextEditWidget_EditComplete);
			internalTextEditWidget.EnterPressed += new KeyEventHandler(internalTextEditWidget_EnterPressed);
			if (pixelWidth == 0)
			{
				pixelWidth = internalTextEditWidget.Width;
			}
			if (pixelHeight == 0)
			{
				pixelHeight = internalTextEditWidget.Height;
			}
			this.LocalBounds = new RectangleDouble(0, 0, pixelWidth, pixelHeight);
			internalTextEditWidget.InsertBarPositionChanged += new EventHandler(internalTextEditWidget_InsertBarPositionChanged);
			internalTextEditWidget.FocusChanged += new EventHandler(internalTextEditWidget_FocusChanged);
			internalTextEditWidget.TextChanged += new EventHandler(internalTextEditWidget_TextChanged);
			base.AddChild(internalTextEditWidget);
		}

		private void internalTextEditWidget_TextChanged(object sender, EventArgs e)
		{
			OnTextChanged(e);
		}

		/// <summary>
		/// Make this widget the focus of keyboard input.
		/// </summary>
		/// <returns></returns>
		public override void Focus()
		{
#if DEBUG
			if (Parent == null)
			{
				throw new Exception("Don't call Focus() until you have a Parent.\nCalling focus without a parent will not result in the focus chain pointing to the widget, so it will not work.");
			}
#endif

			internalTextEditWidget.Focus();
		}

		private void internalTextEditWidget_FocusChanged(object sender, EventArgs e)
		{
			if (ContainsFocus)
			{
				if (ShowSoftwareKeyboard != null)
				{
					UiThread.RunOnIdle(() =>
					{
						ShowSoftwareKeyboard(this, null);
					});
				}
			}
			else
			{
				OnHideSoftwareKeyboard();
			}
			OnFocusChanged(e);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			if (Focused)
			{
				OnHideSoftwareKeyboard();
			}
			base.OnClosed(e);
		}

		private void OnHideSoftwareKeyboard()
		{
			UiThread.RunOnIdle(() =>
			{
				HideSoftwareKeyboard?.Invoke(this, null);
				KeyboardCollapsed?.Invoke(this, null);
			});
		}

		public static void OnKeyboardCollapsed()
		{
			KeyboardCollapsed?.Invoke(null, null);
		}

		private void internalTextEditWidget_EnterPressed(object sender, KeyEventArgs keyEvent)
		{
			EnterPressed?.Invoke(this, keyEvent);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			ScrollBar scrollBar = this.VerticalScrollBar;
			double scrollBarX = mouseEvent.X;
			double scrollBarY = mouseEvent.Y;
			scrollBar.ParentToChildTransform.inverse_transform(ref scrollBarX, ref scrollBarY);
			bool clickIsOnScrollBar = scrollBar.Visible && scrollBar.PositionWithinLocalBounds(scrollBarX, scrollBarY);
			if (!clickIsOnScrollBar)
			{
				double scrollingAreaX = mouseEvent.X;
				double scrollingAreaY = mouseEvent.Y;
				ScrollArea.ParentToChildTransform.inverse_transform(ref scrollingAreaX, ref scrollingAreaY);

				if (scrollingAreaX > InternalTextEditWidget.LocalBounds.Right)
				{
					scrollingAreaX = InternalTextEditWidget.LocalBounds.Right - 1;
				}
				else if (scrollingAreaX < InternalTextEditWidget.LocalBounds.Left)
				{
					scrollingAreaX = InternalTextEditWidget.LocalBounds.Left;
				}
				if (scrollingAreaY > InternalTextEditWidget.LocalBounds.Top)
				{
					scrollingAreaY = InternalTextEditWidget.LocalBounds.Top - 1;
				}
				else if (scrollingAreaY < InternalTextEditWidget.LocalBounds.Bottom)
				{
					scrollingAreaY = InternalTextEditWidget.LocalBounds.Bottom;
				}
				ScrollArea.ParentToChildTransform.transform(ref scrollingAreaX, ref scrollingAreaY);
				mouseEvent.X = scrollingAreaX;
				mouseEvent.Y = scrollingAreaY;
			}

			base.OnMouseDown(mouseEvent);
			if (Focused)
			{
				throw new Exception("We should have moved the mouse so that it gave selection to the internal text edit widgte.");
			}
		}

		private void internalTextEditWidget_EditComplete(object sender, EventArgs e)
		{
			EditComplete?.Invoke(this, null);
		}

		public TypeFacePrinter Printer
		{
			get
			{
				return internalTextEditWidget.Printer;
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			RectangleDouble Bounds = LocalBounds;
			RoundedRect rectBorder = new RoundedRect(Bounds, this.borderRadius);

			graphics2D.Render(rectBorder, BorderColor);

			RectangleDouble insideBounds = Bounds;
			insideBounds.Inflate(-this.borderWidth);
			RoundedRect rectInside = new RoundedRect(insideBounds, Math.Max(this.borderRadius - this.borderWidth, 0));

			graphics2D.Render(rectInside, this.BackgroundColor);
			base.OnDraw(graphics2D);
		}

		private void internalTextEditWidget_InsertBarPositionChanged(object sender, EventArgs e)
		{
			double fontHeight = Printer.TypeFaceStyle.EmSizeInPixels;
			Vector2 barPosition = internalTextEditWidget.InsertBarPosition;
			// move the minimum amount required to keep the bar in view
			Vector2 currentOffsetInView = barPosition + TopLeftOffset;
			Vector2 requiredOffet = Vector2.Zero;
			if (currentOffsetInView.X > Width - 2)
			{
				requiredOffet.X = currentOffsetInView.X - Width + 2;
			}
			else if (currentOffsetInView.X < 0)
			{
				requiredOffet.X = currentOffsetInView.X;
			}
			if (currentOffsetInView.Y <= -(Height - fontHeight))
			{
				requiredOffet.Y = -(currentOffsetInView.Y + Height) + fontHeight;
			}
			else if (currentOffsetInView.Y > 0)
			{
				requiredOffet.Y = -currentOffsetInView.Y;
			}
			TopLeftOffset = new VectorMath.Vector2(TopLeftOffset.X - requiredOffet.X, TopLeftOffset.Y + requiredOffet.Y);
		}

		public bool SelectAllOnFocus 
		{
			get { return InternalTextEditWidget.SelectAllOnFocus; }
			set { InternalTextEditWidget.SelectAllOnFocus = value; }
		}
	}
}