/*
Copyright (c) 2026, Lars Brubaker, John Lewin
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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.UI
{
	public class ThemedNumberEdit : GuiWidget
	{
		private ThemeConfig theme;

		public NumberEdit ActuallNumberEdit { get; }

		public ThemedNumberEdit(double startingValue, ThemeConfig theme, char singleCharLabel = char.MaxValue, string unitsLabel = "", double pixelWidth = 0, double pixelHeight = 0, bool allowNegatives = false, bool allowDecimals = false, double minValue = int.MinValue, double maxValue = int.MaxValue, double increment = 1, int tabIndex = 0)
		{
			using (this.LayoutLock())
			{
				this.Padding = 3;
				this.HAnchor = HAnchor.Fit;
				this.VAnchor = VAnchor.Fit;
				this.Border = 1;
				this.theme = theme;

				this.ActuallNumberEdit = new NumberEdit(startingValue, 0, 0, theme.DefaultFontSize, pixelWidth, pixelHeight, allowNegatives, allowDecimals, minValue, maxValue, increment, tabIndex)
				{
					VAnchor = VAnchor.Bottom,
					HAnchor = HAnchor.Left,
				};

				TextWidget labelWidget = null;
				if (singleCharLabel != char.MaxValue)
				{
					labelWidget = new TextWidget(singleCharLabel.ToString(), pointSize: theme.DefaultFontSize - 2, textColor: theme.PrimaryAccentColor)
					{
						Margin = new BorderDouble(left: 2),
						HAnchor = HAnchor.Left,
						VAnchor = VAnchor.Center,
						Selectable = false
					};

					this.AddChild(labelWidget);

					// the label's width is device pixels, so its margin is added in device pixels before converting
					var labelWidth = labelWidget.Width + labelWidget.DeviceMargin.Left;
					ActuallNumberEdit.Margin = ActuallNumberEdit.Margin.Clone(left: labelWidth / DeviceScale + 2);
				}

				var internalWidget = this.ActuallNumberEdit.InternalTextEditWidget;
				TextWidget unitWidget = null;
				if (!string.IsNullOrEmpty(unitsLabel))
				{
					unitWidget = new TextWidget(unitsLabel, pointSize: theme.DefaultFontSize - 2, textColor: theme.PrimaryAccentColor)
					{
						Margin = new BorderDouble(right: 2), HAnchor = HAnchor.Right,
						VAnchor = VAnchor.Center, Selectable = false
					};
					AddChild(unitWidget);
					var reserve = unitWidget.Width + 4 * DeviceScale;
					var width = ActuallNumberEdit.Width;
					ActuallNumberEdit.MinimumSize = new MatterHackers.VectorMath.Vector2(
						System.Math.Max(0, ActuallNumberEdit.MinimumSize.X - reserve), ActuallNumberEdit.MinimumSize.Y);
					ActuallNumberEdit.Margin = ActuallNumberEdit.Margin.Clone(right: reserve / DeviceScale);
					ActuallNumberEdit.Width = System.Math.Max(0, width - reserve);
				}
				internalWidget.TextColor = theme.EditFieldColors.Inactive.TextColor;
				internalWidget.FocusChanged += (s, e) =>
				{
					internalWidget.TextColor = (internalWidget.Focused) ? theme.EditFieldColors.Focused.TextColor : theme.EditFieldColors.Inactive.TextColor;
					if (unitWidget != null)
					{
						unitWidget.TextColor = internalWidget.Focused
							? theme.PrimaryAccentColor.WithContrast(theme.EditFieldColors.Focused.BackgroundColor, 3).ToColor()
							: theme.PrimaryAccentColor;
					}

					if (labelWidget != null)
					{
						var labelDetailsColor = theme.PrimaryAccentColor.WithContrast(theme.EditFieldColors.Focused.BackgroundColor, 3).ToColor();
						labelWidget.TextColor = (internalWidget.Focused) ? labelDetailsColor : theme.PrimaryAccentColor;
					}
				};

				this.ActuallNumberEdit.InternalNumberEdit.MaxDecimalsPlaces = 5;
				this.AddChild(this.ActuallNumberEdit);
				RoundedFieldChrome.ApplyFieldHeight(this, theme, ActuallNumberEdit);
			}

			this.PerformLayout();
		}

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
					return base.BorderColor;
				}
				else if (this.ContainsFocus)
				{
					return theme.EditFieldColors.Focused.BorderColor;
				}
				else if (this.mouseInBounds && this.ContainsFirstUnderMouseRecursive())
				{
					return theme.EditFieldColors.Hovered.BorderColor;
				}
				else
				{
					return theme.ControlBorderColorIfSet ?? theme.EditFieldColors.Inactive.BorderColor;
				}
			}
			set => base.BorderColor = value;
		}

		/// <summary>The field's outer corner radius in device pixels: the theme's FieldRadius, 0 for the square field.</summary>
		private double FieldRadius => theme.FieldRadius * DeviceScale;

		/// <summary>
		/// A theme with a FieldRadius gets the rounded field DropDownList draws, in the same bounds; without one
		/// the square fill is left to GuiWidget as it always was.
		/// </summary>
		public override void OnDrawBackground(Graphics2D graphics2D)
		{
			if (FieldRadius > 0)
			{
				RoundedFieldChrome.DrawFill(graphics2D, LocalBounds, FieldRadius, DeviceBorder.Left, BackgroundColor);
				return;
			}

			base.OnDrawBackground(graphics2D);
		}

		/// <summary>Strokes the rounded outline in the Border band when the theme rounds fields.</summary>
		protected internal override bool DrawBorderRing(Graphics2D graphics2D, RectangleDouble boundsInParent, BorderDouble deviceBorder, Color borderColor)
		{
			return FieldRadius > 0
				&& RoundedFieldChrome.DrawRing(graphics2D, boundsInParent, deviceBorder.Left, FieldRadius, borderColor);
		}

		private bool mouseInBounds = false;

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

		public override int TabIndex
		{
			// TODO: This looks invalid - setter and getter should use same context
			get => base.TabIndex;
			set => this.ActuallNumberEdit.TabIndex = value;
		}

		public double Value
		{
			get => this.ActuallNumberEdit.Value;
			set => this.ActuallNumberEdit.Value = value;
		}

		public override string Text
		{
			get => this.ActuallNumberEdit.Text;
			set => this.ActuallNumberEdit.Text = value;
		}

		public bool SelectAllOnFocus
		{
			get => this.ActuallNumberEdit.InternalNumberEdit.SelectAllOnFocus;
			set => this.ActuallNumberEdit.InternalNumberEdit.SelectAllOnFocus = value;
		}
	}
}
