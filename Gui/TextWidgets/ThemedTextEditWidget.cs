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

using MatterHackers.Agg.Font;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.Agg.UI
{
	public class ThemedTextEditWidget : GuiWidget
	{
		public readonly TextWidget NoContentFieldDescription = null;
		private ThemeConfig theme;
		private bool mouseInBounds = false;
		private TextWidget leadingLabel;
		private TextWidget trailingLabel;
		private double? undecoratedMinimumWidth;

		/// <summary>
		/// The width the constructor gave the text field, before any label took room out of it. Every width the field
		/// is given outright is worked out from this rather than from the width it currently has, because the width it
		/// currently has may have come from a stretch, and a width layout gave it must never be recorded as reserved.
		/// A field constructed with pixelWidth 0 is sized by its text, so what gets reserved is the width its first text
		/// happened to need; a consumer that later took such a field off Stretch would get that initial width back rather
		/// than a width that suits the text it holds by then. Nothing does that today.
		/// </summary>
		private readonly double reservedUndecoratedWidth;

		/// <summary>
		/// The width the text field was last given outright: its undecorated width less the room its labels take out
		/// of it. Layout only ever sizes the field while the field itself stretches (or is MinFitOrStretch), so this is
		/// what the field has to go back to when it stops being sized by layout.
		/// </summary>
		private double reservedFieldWidth;

		/// <summary>An accent prefix inside the field, such as an axis label.</summary>
		public string LeadingLabel
		{
			get => leadingLabel?.Text ?? "";
			set => SetDecorator(ref leadingLabel, value, HAnchor.Left);
		}

		/// <summary>An accent suffix inside the field, such as a measurement unit.</summary>
		public string TrailingLabel
		{
			get => trailingLabel?.Text ?? "";
			set => SetDecorator(ref trailingLabel, value, HAnchor.Right);
		}

		private void SetDecorator(ref TextWidget label, string text, HAnchor anchor)
		{
			using (LayoutLock())
			{
				undecoratedMinimumWidth ??= ActualTextEditWidget.MinimumSize.X;
				if (label == null)
				{
					label = new TextWidget("", pointSize: theme.DefaultFontSize - 2, textColor: theme.PrimaryAccentColor)
					{
						Margin = anchor == HAnchor.Left ? new BorderDouble(left: 2) : new BorderDouble(right: 2),
						HAnchor = anchor, VAnchor = VAnchor.Center,
						Selectable = false, AutoExpandBoundsToText = true
					};
					AddChild(label);
				}
				label.Text = text ?? "";
				label.Visible = !string.IsNullOrEmpty(text);
				var left = leadingLabel?.Visible == true ? leadingLabel.Width + 4 * DeviceScale : 0;
				var right = trailingLabel?.Visible == true ? trailingLabel.Width + 4 * DeviceScale : 0;
				var reserve = left + right;
				if ((ActualTextEditWidget.HAnchor & (HAnchor.Left | HAnchor.Center | HAnchor.Right)) == 0)
					ActualTextEditWidget.HAnchor |= HAnchor.Left;
				// Reserve both labels INSIDE the width the field was built with, so decorated and plain fields align.
				reservedFieldWidth = Math.Max(0, reservedUndecoratedWidth - reserve);
				ActualTextEditWidget.MinimumSize = new Vector2(Math.Max(0, undecoratedMinimumWidth.Value - reserve), ActualTextEditWidget.MinimumSize.Y);
				ActualTextEditWidget.Margin = ActualTextEditWidget.Margin.Clone(left: left / DeviceScale, right: right / DeviceScale);
				ActualTextEditWidget.Width = reservedFieldWidth;
			}
			PerformLayout();
			Invalidate();
		}

		public ThemedTextEditWidget(string text, ThemeConfig theme, double pixelWidth = 0, double pixelHeight = 0, bool multiLine = false, int tabIndex = 0, string messageWhenEmptyAndNotSelected = "", TypeFace typeFace = null)
		{
			this.Padding = new BorderDouble(3);
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;
			this.Border = 1;

			this.theme = theme;

			this.ActualTextEditWidget = new TextEditWidget(text, 0, 0, theme.DefaultFontSize, pixelWidth, pixelHeight, multiLine, tabIndex: tabIndex, typeFace: typeFace)
			{
				VAnchor = VAnchor.Top,
				BackgroundColor = Color.Transparent
			};

			this.ActualTextEditWidget.TextChanged += (s, e) =>
			{
				this.OnTextChanged(e);
			};

			var internalWidget = this.ActualTextEditWidget.InternalTextEditWidget;
			internalWidget.TextColor = theme.EditFieldColors.Inactive.TextColor;
			internalWidget.FocusChanged += (s, e) =>
			{
				internalWidget.TextColor = internalWidget.Focused ? theme.EditFieldColors.Focused.TextColor : theme.EditFieldColors.Inactive.TextColor;
				NoContentFieldDescription.TextColor = internalWidget.Focused ? theme.EditFieldColors.Focused.LightTextColor : theme.EditFieldColors.Inactive.LightTextColor;
				if (trailingLabel != null) trailingLabel.TextColor = internalWidget.Focused
					? theme.PrimaryAccentColor.WithContrast(theme.EditFieldColors.Focused.BackgroundColor, 3).ToColor()
					: theme.PrimaryAccentColor;
				if (leadingLabel != null) leadingLabel.TextColor = internalWidget.Focused
					? theme.PrimaryAccentColor.WithContrast(theme.EditFieldColors.Focused.BackgroundColor, 3).ToColor()
					: theme.PrimaryAccentColor;
			};

			this.ActualTextEditWidget.InternalTextEditWidget.BackgroundColor = Color.Transparent;

			this.ActualTextEditWidget.MinimumSize = new Vector2(Math.Max(ActualTextEditWidget.MinimumSize.X, pixelWidth), Math.Max(ActualTextEditWidget.MinimumSize.Y, pixelHeight));
			this.reservedUndecoratedWidth = this.reservedFieldWidth = this.ActualTextEditWidget.Width;
			this.AddChild(this.ActualTextEditWidget);

			this.AddChild(NoContentFieldDescription = new TextWidget(messageWhenEmptyAndNotSelected, pointSize: theme.DefaultFontSize, textColor: theme.EditFieldColors.Focused.LightTextColor)
			{
				VAnchor = VAnchor.Top,
				AutoExpandBoundsToText = true
			});

			SetNoContentFieldDescriptionVisibility();
		}

        public TextEditWidget ActualTextEditWidget { get; }

		public override Color BackgroundColor
		{
			get
			{
				if (base.BackgroundColor != Color.Transparent)
				{
					return base.BackgroundColor;
				}
				else if (this.ContainsFocus)
				{
					return theme.EditFieldColors.Focused.BackgroundColor;
				}
				else if (this.mouseInBounds)
				{
					return theme.EditFieldColors.Hovered.BackgroundColor;
				}
				else
				{
					return theme.EditFieldColors.Inactive.BackgroundColor;
				}
			}
			set => base.BackgroundColor = value;
		}

		public override Color BorderColor
		{
			get
			{
				if (base.BorderColor != Color.Transparent)
				{
					return base.BackgroundColor;
				}
				else if (this.ContainsFocus)
				{
					return theme.EditFieldColors.Focused.BorderColor;
				}
				else if (this.mouseInBounds)
				{
					return theme.EditFieldColors.Hovered.BorderColor;
				}
				else
				{
					return theme.EditFieldColors.Inactive.BorderColor;
				}
			}
			set => base.BorderColor = value;
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = true;
			base.OnMouseEnterBounds(mouseEvent);

			this.Invalidate();
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = false;
			base.OnMouseLeaveBounds(mouseEvent);

			this.Invalidate();
		}

		/// <summary>
		/// The frame's anchoring, which is passed on to the text field inside it so that stretching the frame
		/// stretches the field with it rather than leaving it at its pixel width.
		/// </summary>
		/// <remarks>
		/// Fit is deliberately not passed on. The field scrolls its text; a field that fitted its text would
		/// grow instead of scrolling, and the growth lands outside the frame, which is the thing that clips -
		/// anchored Right it hangs off the frame's left edge, so the head of a long value is cut off and no
		/// caret move can bring it back. The frame fits the field; the field scrolls its text. That does mean
		/// MaxFitOrStretch degrades to plain Stretch for the field inside, which is a visible change for any
		/// consumer that set it on a themed field expecting a field that grew with its text.
		/// </remarks>
		public override HAnchor HAnchor
		{
			get => base.HAnchor;
			set
			{
				base.HAnchor = value;
				if (ActualTextEditWidget != null)
				{
					var fieldAnchor = value & ~HAnchor.Fit;
					ActualTextEditWidget.HAnchor = fieldAnchor;

					// A field anchor that does not stretch holds the field in place without ever measuring it again,
					// so a width an earlier stretch gave it would live on - wider than the frame, and hanging out of
					// whichever edge it is not held to. Give that width back the moment layout stops owning it. Fit
					// need not be tested: the mask above cleared it. MinFitOrStretch does survive that mask and is
					// still a size layout works out, so it counts as layout owning the field just as Stretch does.
					if ((fieldAnchor & HAnchor.Stretch) != HAnchor.Stretch
						&& (fieldAnchor & HAnchor.MinFitOrStretch) != HAnchor.MinFitOrStretch)
					{
						ActualTextEditWidget.Width = reservedFieldWidth;
					}
				}
			}
		}

		private void SetNoContentFieldDescriptionVisibility()
		{
			if (NoContentFieldDescription != null)
			{
				NoContentFieldDescription.Visible = Text == "";
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			SetNoContentFieldDescriptionVisibility();
			base.OnDraw(graphics2D);
		}

		public override string Text
		{
			get => ActualTextEditWidget.Text;
			set => ActualTextEditWidget.Text = value;
		}

		public bool SelectAllOnFocus
		{
			get => ActualTextEditWidget.InternalTextEditWidget.SelectAllOnFocus;
			set => ActualTextEditWidget.InternalTextEditWidget.SelectAllOnFocus = value;
		}

		public bool ReadOnly
		{
			get => ActualTextEditWidget.ReadOnly;
			set => ActualTextEditWidget.ReadOnly = value;
		}

		public void DrawFromHintedCache()
		{
			ActualTextEditWidget.Printer.DrawFromHintedCache = true;
			ActualTextEditWidget.DoubleBuffer = false;
		}

		public void SetTextAsUndoBaseline(string text, int charIndex = 0)
		{
			ActualTextEditWidget.SetTextAsUndoBaseline(text, charIndex);
		}

		public override void Focus()
		{
			ActualTextEditWidget.Focus();
		}
	}
}
