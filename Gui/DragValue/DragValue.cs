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
using System.Globalization;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A number the user changes by dragging left or right across it, or by clicking it and typing - the
	/// agg-gui (and egui) DragValue. A press that moves less than a few pixels is a click and opens an
	/// inline edit field; Enter or leaving the field commits, Escape cancels. The value is always kept
	/// inside [<see cref="Minimum"/>, <see cref="Maximum"/>].
	/// </summary>
	/// <remarks>
	/// Geometry constants are design units; they are multiplied by <see cref="GuiWidget.DeviceScale"/> where
	/// they meet the widget's device-pixel bounds and mouse positions, so a drag of one design unit moves
	/// the value by <see cref="Speed"/> whatever the panel density.
	/// </remarks>
	public class DragValue : GuiWidget
	{
		private const double DesignHeight = 24;
		private const double ArrowMargin = 8;
		private const double ArrowWidth = 6;
		private const double ArrowHalfHeight = 4;
		private const double LabelSidePad = 4;
		private const double CornerRadius = 4;

		/// <summary>Horizontal travel, in design units, before a press becomes a drag instead of a click.</summary>
		public const double DragThreshold = 3;

		/// <summary>Space kept clear on each side of the value text for an arrow and its padding.</summary>
		private const double LabelSideInset = ArrowMargin + ArrowWidth + LabelSidePad;

		private readonly ThemeConfig theme;
		private readonly TextWidget valueText;

		private double value;
		private int decimals = 2;
		private string prefix = "";
		private string suffix = "";

		/// <summary>The width SyncText last chose, so it can tell its own width from a caller's.</summary>
		private double autoWidth = double.NaN;

		private bool mousePressed;
		private bool dragging;
		private double pressX;
		private double dragStartValue;

		/// <summary>
		/// Creates a drag value showing <paramref name="value"/> clamped to [<paramref name="minimum"/>,
		/// <paramref name="maximum"/>], coloured from <paramref name="theme"/>.
		/// </summary>
		public DragValue(double value, double minimum, double maximum, ThemeConfig theme)
		{
			if (minimum > maximum)
			{
				throw new ArgumentException("minimum must not be greater than maximum", nameof(minimum));
			}

			this.theme = theme;
			Minimum = minimum;
			Maximum = maximum;
			this.value = Math.Clamp(value, minimum, maximum);
			Cursor = Cursors.SizeWE;
			TabStop = true;
			HAnchor = HAnchor.Absolute;
			VAnchor = VAnchor.Absolute;

			valueText = new TextWidget(DisplayText, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
				Selectable = false,
				AutoExpandBoundsToText = true,
			};
			AddChild(valueText);

			// The typing field is the stock TextEditWidget, shown only while editing, so selection, caret,
			// clipboard and undo all behave as they do in every other agg-sharp text field.
			EditField = new TextEditWidget(pointSize: theme.DefaultFontSize)
			{
				Name = "DragValue Edit Field",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(LabelSidePad, 0),
				Visible = false,
			};
			EditField.TextColor = theme.TextColor;
			EditField.EnterPressed += (s, e) => CommitEdit();
			EditField.InternalTextEditWidget.KeyDown += (s, e) =>
			{
				if (!IsEditing)
				{
					return;
				}

				if (e.KeyCode == Keys.Escape)
				{
					CancelEdit();
					e.Handled = true;
				}
				else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
				{
					// egui nudges while typing with Up/Down only; Left/Right stay caret keys.
					// this.value, not value: inside the constructor the parameter would be captured instead.
					var from = TryParseTyped(EditField.Text, out double typed) ? typed : this.value;
					Value = SnapAndClamp(from + (e.KeyCode == Keys.Up ? NudgeAmount : -NudgeAmount));
					EditField.Text = FormatNumber(this.value);
					e.Handled = true;
					e.SuppressKeyPress = true;
				}
			};
			EditField.InternalTextEditWidget.FocusChanged += (s, e) =>
			{
				// Clicking elsewhere commits, as egui does - only Escape throws the typing away.
				if (IsEditing && !EditField.InternalTextEditWidget.Focused)
				{
					CommitEdit();
				}
			};
			AddChild(EditField);

			autoWidth = IntrinsicMinimumWidth;
			LocalBounds = new RectangleDouble(0, 0, autoWidth, DesignHeight * DeviceScale);
		}

		/// <summary>Raised whenever <see cref="Value"/> changes, by drag, typing or code.</summary>
		public event EventHandler ValueChanged;

		/// <summary>The inline field used while typing. Hidden unless <see cref="IsEditing"/>.</summary>
		public TextEditWidget EditField { get; }

		/// <summary>The smallest value this control will hold.</summary>
		public double Minimum { get; }

		/// <summary>The largest value this control will hold.</summary>
		public double Maximum { get; }

		/// <summary>Value units per design unit of horizontal drag. Defaults to 1.</summary>
		public double Speed { get; set; } = 1;

		/// <summary>When greater than zero, dragged and typed values snap to the nearest multiple of it.</summary>
		public double Step { get; set; }

		/// <summary>True while the inline edit field is open.</summary>
		public bool IsEditing { get; private set; }

		/// <summary>True between a press that has crossed <see cref="DragThreshold"/> and its release.</summary>
		public bool IsDragging => dragging;

		/// <summary>
		/// How far one arrow key press moves the value: <see cref="Step"/> when snapping, otherwise
		/// <see cref="Speed"/> (egui's one-design-unit drag).
		/// </summary>
		public double NudgeAmount => Step > 0 ? Step : Speed;

		/// <summary>The current value. Setting it clamps to the range and raises <see cref="ValueChanged"/> on a change.</summary>
		public double Value
		{
			get => value;
			set
			{
				// NaN would survive Math.Clamp and poison every later drag and comparison.
				if (double.IsNaN(value))
				{
					return;
				}

				var clamped = Math.Clamp(value, Minimum, Maximum);
				if (clamped != this.value)
				{
					this.value = clamped;
					SyncText();
					ValueChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		/// <summary>Decimal places shown (and typed values are not rounded to them). Defaults to 2.</summary>
		public int Decimals
		{
			get => decimals;
			set
			{
				decimals = Math.Max(0, value);
				SyncText();
			}
		}

		/// <summary>Text shown before the number, such as "x: ". Not part of what the user types.</summary>
		public string Prefix
		{
			get => prefix;
			set
			{
				prefix = value ?? "";
				SyncText();
			}
		}

		/// <summary>Text shown after the number, such as "°" or " mm". Not part of what the user types.</summary>
		public string Suffix
		{
			get => suffix;
			set
			{
				suffix = value ?? "";
				SyncText();
			}
		}

		/// <summary>The text on show when not editing: prefix, the number to <see cref="Decimals"/> places, suffix.</summary>
		public string DisplayText => prefix + FormatNumber(value) + suffix;

		/// <summary>
		/// The narrowest this control can be without clipping its text: the display text plus one digit of
		/// headroom plus the arrow zones on each side (device pixels).
		/// </summary>
		public double IntrinsicMinimumWidth
		{
			get
			{
				var printer = valueText.Printer;
				return printer.GetSize(DisplayText).X + printer.GetSize("0").X + 2 * LabelSideInset * DeviceScale;
			}
		}

		/// <summary>
		/// The value a drag of <paramref name="dragDistance"/> design units (positive to the right) from
		/// <paramref name="startValue"/> produces, after <see cref="Step"/> snapping and clamping.
		/// </summary>
		public double ValueForDrag(double startValue, double dragDistance)
		{
			return SnapAndClamp(startValue + dragDistance * Speed);
		}

		/// <summary>Rounds <paramref name="raw"/> to the nearest <see cref="Step"/> (if any) and clamps it to the range.</summary>
		public double SnapAndClamp(double raw)
		{
			var snapped = Step > 0 ? Math.Round(raw / Step) * Step : raw;
			return Math.Clamp(snapped, Minimum, Maximum);
		}

		/// <summary>
		/// Reads typed text as a number. Surrounding spaces and a leading <see cref="Prefix"/> or trailing
		/// <see cref="Suffix"/> are ignored, and '.' is always the decimal point. The result is not clamped.
		/// </summary>
		public bool TryParseTyped(string text, out double parsed)
		{
			var trimmed = (text ?? "").Trim();
			if (prefix.Length > 0 && trimmed.StartsWith(prefix.Trim(), StringComparison.Ordinal))
			{
				trimmed = trimmed.Substring(prefix.Trim().Length).Trim();
			}

			if (suffix.Length > 0 && trimmed.EndsWith(suffix.Trim(), StringComparison.Ordinal))
			{
				trimmed = trimmed.Substring(0, trimmed.Length - suffix.Trim().Length).Trim();
			}

			return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
				&& !double.IsNaN(parsed)
				&& !double.IsInfinity(parsed);
		}

		/// <summary>Opens the inline edit field holding the bare number, all selected, with keyboard focus.</summary>
		public void BeginEdit()
		{
			if (IsEditing)
			{
				return;
			}

			IsEditing = true;
			valueText.Visible = false;
			EditField.Text = FormatNumber(value);
			EditField.Visible = true;
			EditField.Focus();
			EditField.InternalTextEditWidget.SelectAll();
			Invalidate();
		}

		/// <summary>
		/// Closes the edit field and takes what was typed: a number is snapped, clamped and set; anything
		/// else leaves the value as it was.
		/// </summary>
		public void CommitEdit()
		{
			if (!IsEditing)
			{
				return;
			}

			var typed = EditField.Text;
			EndEdit();
			if (TryParseTyped(typed, out double parsed))
			{
				Value = SnapAndClamp(parsed);
			}
		}

		/// <summary>Closes the edit field without changing the value.</summary>
		public void CancelEdit()
		{
			if (IsEditing)
			{
				EndEdit();
			}
		}

		/// <summary>
		/// With focus and no edit field open: Enter or Space opens the field, Up/Right nudge the value up
		/// and Down/Left nudge it down by <see cref="NudgeAmount"/>. Keys typed into the open field are
		/// handled by the field itself.
		/// </summary>
		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			if (!IsEditing)
			{
				switch (keyEvent.KeyCode)
				{
					case Keys.Enter:
					case Keys.Space:
						BeginEdit();
						keyEvent.Handled = true;
						keyEvent.SuppressKeyPress = true;
						break;

					case Keys.Up:
					case Keys.Right:
						Value = SnapAndClamp(value + NudgeAmount);
						keyEvent.Handled = true;
						keyEvent.SuppressKeyPress = true;
						break;

					case Keys.Down:
					case Keys.Left:
						Value = SnapAndClamp(value - NudgeAmount);
						keyEvent.Handled = true;
						keyEvent.SuppressKeyPress = true;
						break;
				}
			}

			base.OnKeyDown(keyEvent);
		}

		public override void OnFocusChanged(EventArgs e)
		{
			Invalidate();
			base.OnFocusChanged(e);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			// Children (the edit field) receive their clicks through base.OnMouseDown, so a press while
			// editing must not also start a drag.
			if (!IsEditing && mouseEvent.Button == MouseButtons.Left)
			{
				mousePressed = true;
				dragging = false;
				pressX = mouseEvent.X;
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (mousePressed && !IsEditing)
			{
				// Distances are converted to design units so Speed means the same on every panel.
				var dragDistance = (mouseEvent.X - pressX) / DeviceScale;
				if (!dragging && Math.Abs(dragDistance) >= DragThreshold)
				{
					// Anchor at the press point, not where the threshold was crossed, so the value does
					// not jump by the dead zone.
					dragging = true;
					dragStartValue = value;
					Invalidate();
				}

				if (dragging)
				{
					Value = ValueForDrag(dragStartValue, dragDistance);
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			bool wasClick = mousePressed && !dragging && mouseEvent.Button == MouseButtons.Left;
			bool wasDrag = dragging;
			mousePressed = false;
			dragging = false;

			base.OnMouseUp(mouseEvent);

			if (wasClick && !IsEditing)
			{
				BeginEdit();
			}
			else if (wasDrag)
			{
				Invalidate();
			}
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			Invalidate();
			base.OnMouseEnterBounds(mouseEvent);
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			Invalidate();
			base.OnMouseLeaveBounds(mouseEvent);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var bounds = LocalBounds;
			var scale = DeviceScale;
			var accent = theme.PrimaryAccentColor;

			// Colours are read from the theme every frame so a live theme switch reaches this widget.
			valueText.TextColor = theme.TextColor;
			EditField.TextColor = theme.TextColor;
			var radius = CornerRadius * scale;
			var outline = new RoundedRect(bounds, radius);

			if (IsEditing)
			{
				graphics2D.Render(outline, accent.WithAlpha(26));
				graphics2D.Render(new Stroke(new RoundedRect(Inset(bounds, .75 * scale), radius), 1.5 * scale), accent.WithAlpha(204));
			}
			else
			{
				// The fill deepens from resting to hover to drag, as agg-gui's does.
				int fillAlpha = dragging ? 56 : (UnderMouseState != UnderMouseState.NotUnderMouse ? 36 : 20);
				graphics2D.Render(outline, accent.WithAlpha(fillAlpha));
				graphics2D.Render(new Stroke(new RoundedRect(Inset(bounds, .5 * scale), radius), scale), accent.WithAlpha(89));

				var arrowColor = accent.WithAlpha(115);
				var middle = bounds.Center.Y;
				var margin = ArrowMargin * scale;
				var width = ArrowWidth * scale;
				var halfHeight = ArrowHalfHeight * scale;
				graphics2D.Render(Triangle(bounds.Left + margin, middle, bounds.Left + margin + width, halfHeight), arrowColor);
				graphics2D.Render(Triangle(bounds.Right - margin, middle, bounds.Right - margin - width, halfHeight), arrowColor);

				if (Focused)
				{
					// Keyboard focus ring, the colour ThemedButton uses for its own.
					graphics2D.Render(new Stroke(new RoundedRect(Inset(bounds, .75 * scale), radius), 1.5 * scale), theme.EditFieldColors.Focused.BorderColor);
				}
			}

			base.OnDraw(graphics2D);
		}

		private static RectangleDouble Inset(RectangleDouble bounds, double amount)
		{
			return new RectangleDouble(bounds.Left + amount, bounds.Bottom + amount, bounds.Right - amount, bounds.Top - amount);
		}

		/// <summary>A triangle with its point at (tipX, middle) and its flat side at baseX.</summary>
		private static VertexStorage Triangle(double tipX, double middle, double baseX, double halfHeight)
		{
			var triangle = new VertexStorage();
			triangle.MoveTo(tipX, middle);
			triangle.LineTo(baseX, middle - halfHeight);
			triangle.LineTo(baseX, middle + halfHeight);
			triangle.ClosePolygon();
			return triangle;
		}

		private string FormatNumber(double number)
		{
			return number.ToString("F" + decimals, CultureInfo.InvariantCulture);
		}

		private void EndEdit()
		{
			IsEditing = false;
			EditField.Visible = false;
			valueText.Visible = true;
			SyncText();
		}

		private void SyncText()
		{
			// Called from property setters, some of which the constructor reaches before valueText exists.
			if (valueText == null)
			{
				return;
			}

			valueText.Text = DisplayText;
			// Follow the text both ways while the width is the one this widget chose; a width the caller
			// set wider is kept, and is only ever grown to stop the text clipping.
			var minimumWidth = IntrinsicMinimumWidth;
			if (Width < minimumWidth || Width == autoWidth)
			{
				Width = minimumWidth;
			}

			autoWidth = minimumWidth;

			Invalidate();
		}
	}
}
