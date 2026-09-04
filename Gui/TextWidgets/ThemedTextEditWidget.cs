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
		private double leadingLabelWidth;
		private double undecoratedMinimumWidth;

		/// <summary>An accent label inside the field, like the axis label in ThemedNumberEdit.</summary>
		public string LeadingLabel
		{
			get => leadingLabel?.Text ?? "";
			set
			{
				using (LayoutLock())
				{
					if (leadingLabel == null)
					{
						undecoratedMinimumWidth = ActualTextEditWidget.MinimumSize.X;
						leadingLabel = new TextWidget("", pointSize: theme.DefaultFontSize - 2, textColor: theme.PrimaryAccentColor)
						{
							Margin = new BorderDouble(left: 2), HAnchor = HAnchor.Left, VAnchor = VAnchor.Center,
							Selectable = false, AutoExpandBoundsToText = true
						};
						AddChild(leadingLabel);
					}
					leadingLabel.Text = value ?? "";
					leadingLabel.Visible = !string.IsNullOrEmpty(value);
					var reserve = leadingLabel.Visible ? leadingLabel.Width + 4 * DeviceScale : 0;
					// Reserve space INSIDE the existing width so decorated and plain fields align.
					var width = ActualTextEditWidget.Width + leadingLabelWidth - reserve;
					ActualTextEditWidget.MinimumSize = new Vector2(Math.Max(0, undecoratedMinimumWidth - reserve), ActualTextEditWidget.MinimumSize.Y);
					ActualTextEditWidget.Margin = ActualTextEditWidget.Margin.Clone(left: reserve / DeviceScale);
					ActualTextEditWidget.Width = Math.Max(0, width);
					leadingLabelWidth = reserve;
				}
				Invalidate();
			}
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
				if (leadingLabel != null) leadingLabel.TextColor = internalWidget.Focused
					? theme.PrimaryAccentColor.WithContrast(theme.EditFieldColors.Focused.BackgroundColor, 3).ToColor()
					: theme.PrimaryAccentColor;
			};

			this.ActualTextEditWidget.InternalTextEditWidget.BackgroundColor = Color.Transparent;

			this.ActualTextEditWidget.MinimumSize = new Vector2(Math.Max(ActualTextEditWidget.MinimumSize.X, pixelWidth), Math.Max(ActualTextEditWidget.MinimumSize.Y, pixelHeight));
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

		public override HAnchor HAnchor
		{
			get => base.HAnchor;
			set
			{
				base.HAnchor = value;
				if (ActualTextEditWidget != null)
				{
					ActualTextEditWidget.HAnchor = value;
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
