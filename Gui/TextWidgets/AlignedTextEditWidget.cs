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
*/

using System;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A text field in a frame of its own size whose text sits left, centred or right and top, centred or bottom
	/// in that frame, with hint text shown while it is empty - agg-gui's TextArea alignment cells and hint text.
	/// </summary>
	/// <remarks>
	/// The alignment places the text as one block: the editor inside is sized to its text (up to the frame, past
	/// which it scrolls) and that block is anchored in the frame. Lines inside a multi-line block stay left-aligned
	/// to each other, because per-line alignment would live in InternalTextEditWidget's caret and selection maths,
	/// which is frozen. Clicking the frame outside the text focuses the editor with the caret at the end.
	/// </remarks>
	public class AlignedTextEditWidget : GuiWidget
	{
		private readonly TextWidget hint;
		private HAnchor textHAnchor = HAnchor.Left;
		private VAnchor textVAnchor = VAnchor.Top;
		private bool placing;

		public AlignedTextEditWidget(string text, double pointSize, bool multiLine = true, string hintText = "")
		{
			this.Padding = new BorderDouble(4);

			this.Editor = new TextEditWidget(text, pointSize: pointSize, multiLine: multiLine)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
				BackgroundColor = Color.Transparent,
			};
			this.Editor.InternalTextEditWidget.BackgroundColor = Color.Transparent;

			this.hint = new TextWidget(hintText ?? "", pointSize: pointSize)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
				AutoExpandBoundsToText = true,
				Selectable = false,
			};

			// The hint goes under the editor so a click on it still reaches the editor.
			this.AddChild(this.hint);
			this.AddChild(this.Editor);

			// The inner editor grows and shrinks with its text; follow it.
			this.Editor.InternalTextEditWidget.BoundsChanged += (s, e) => this.PlaceText();
			this.Editor.TextChanged += (s, e) =>
			{
				this.OnTextChanged(e);
				this.PlaceText();
			};
			this.PlaceText();
		}

		/// <summary>The editor itself: text, selection, caret, read-only and colours are all set on it.</summary>
		public TextEditWidget Editor { get; }

		/// <summary>Shown, dimmed, while the text is empty.</summary>
		public string HintText
		{
			get => this.hint.Text;
			set
			{
				this.hint.Text = value ?? "";
				this.PlaceText();
			}
		}

		public Color HintColor
		{
			get => this.hint.TextColor;
			set => this.hint.TextColor = value;
		}

		/// <summary>Where the text sits across the frame: Left (the default), Center or Right.</summary>
		public HAnchor TextHAnchor
		{
			get => this.textHAnchor;
			set
			{
				this.textHAnchor = value;
				this.PlaceText();
			}
		}

		/// <summary>Where the text sits down the frame: Top (the default), Center or Bottom.</summary>
		public VAnchor TextVAnchor
		{
			get => this.textVAnchor;
			set
			{
				this.textVAnchor = value;
				this.PlaceText();
			}
		}

		public override string Text
		{
			get => this.Editor?.Text ?? "";
			set => this.Editor.Text = value;
		}

		public bool ReadOnly
		{
			get => this.Editor.ReadOnly;
			set => this.Editor.ReadOnly = value;
		}

		/// <summary>
		/// Where a block of <paramref name="contentSize"/> goes in <paramref name="available"/>: clipped to it, then
		/// anchored by <paramref name="hAnchor"/> (Left, Center or Right; anything else is Left) and
		/// <paramref name="vAnchor"/> (Top, Center or Bottom; anything else is Top).
		/// </summary>
		public static RectangleDouble Place(Vector2 contentSize, RectangleDouble available, HAnchor hAnchor, VAnchor vAnchor)
		{
			// Whole-pixel sizes and positions, so the text is not resampled as the alignment changes.
			double width = Math.Max(0, Math.Min(Math.Ceiling(contentSize.X), available.Width));
			double height = Math.Max(0, Math.Min(Math.Ceiling(contentSize.Y), available.Height));

			double left;
			switch (hAnchor)
			{
				case HAnchor.Center:
					left = available.Left + (available.Width - width) / 2;
					break;

				case HAnchor.Right:
					left = available.Right - width;
					break;

				default:
					left = available.Left;
					break;
			}

			double bottom;
			switch (vAnchor)
			{
				case VAnchor.Center:
					bottom = available.Bottom + (available.Height - height) / 2;
					break;

				case VAnchor.Bottom:
					bottom = available.Bottom;
					break;

				default:
					bottom = available.Top - height;
					break;
			}

			left = Math.Round(left);
			bottom = Math.Round(bottom);
			return new RectangleDouble(left, bottom, left + width, bottom + height);
		}

		public override void Focus() => this.Editor.Focus();

		public override void OnBoundsChanged(EventArgs e)
		{
			base.OnBoundsChanged(e);
			this.PlaceText();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			this.hint.Visible = this.Editor.Text.Length == 0;
			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			// A click in the frame but off the text still means "edit this".
			if (mouseEvent.Button == MouseButtons.Left && !this.Editor.ContainsFocus)
			{
				this.Editor.Focus();
				this.Editor.InternalTextEditWidget.SetCursorPosition(this.Editor.Text.Length);
			}
		}

		/// <summary>Sizes the editor to its text (up to the frame) and anchors it, and the hint, in the frame.</summary>
		private void PlaceText()
		{
			if (this.Editor == null || this.placing)
			{
				return;
			}

			this.placing = true;
			try
			{
				RectangleDouble available = this.LocalBounds;
				available.Left += this.Padding.Left;
				available.Right -= this.Padding.Right;
				available.Bottom += this.Padding.Bottom;
				available.Top -= this.Padding.Top;

				InternalTextEditWidget inner = this.Editor.InternalTextEditWidget;
				// Two pixels of slack so a caret at the end of the text is inside the editor, not scrolled past it.
				var contentSize = new Vector2(inner.Width + 2, inner.Height);
				RectangleDouble placed = Place(contentSize, available, this.textHAnchor, this.textVAnchor);
				this.Editor.LocalBounds = new RectangleDouble(0, 0, placed.Width, placed.Height);
				this.Editor.OriginRelativeParent = new Vector2(placed.Left, placed.Bottom);

				// Text that fits needs no scrolling; one that was scrolled while it overflowed is shown from its start.
				if (contentSize.X <= available.Width && contentSize.Y <= available.Height)
				{
					this.Editor.TopLeftOffset = Vector2.Zero;
				}

				RectangleDouble hintPlace = Place(new Vector2(this.hint.Width, this.hint.Height), available, this.textHAnchor, this.textVAnchor);
				this.hint.OriginRelativeParent = new Vector2(hintPlace.Left - this.hint.LocalBounds.Left, hintPlace.Bottom - this.hint.LocalBounds.Bottom);
			}
			finally
			{
				this.placing = false;
			}

			this.Invalidate();
		}
	}
}
