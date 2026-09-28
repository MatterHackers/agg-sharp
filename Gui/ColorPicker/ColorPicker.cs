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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// An inline colour picker - the agg-gui ColorPicker. A swatch shows the colour; clicking it (or Enter /
	/// Space while focused) opens a panel below it with a hue bar, a saturation/value square, an alpha bar,
	/// a hex readout and Cancel / Select buttons. The colour follows the drag live; Cancel (or Escape)
	/// puts back the colour the panel opened with, Select keeps the new one.
	/// </summary>
	/// <remarks>
	/// Like agg-gui's, the panel grows the widget rather than floating over its neighbours, so siblings
	/// in a flow layout are pushed down. The gradients are small images stretched over their rectangles,
	/// rebuilt only when the hue (square) or the RGB (alpha bar) changes.
	/// agg-gui's optional "No Color (Pass Through)" checkbox (allow_none) is not implemented.
	/// </remarks>
	public class ColorPicker : GuiWidget
	{
		private const double DesignSwatchHeight = 22;
		private const double DesignPanelWidth = 228;
		private const double DesignPad = 8;
		private const double DesignRowGap = 6;
		private const double DesignHueHeight = 16;
		private const double DesignSvHeight = 140;
		private const double DesignAlphaHeight = 16;
		private const double DesignHexHeight = 20;
		private const double DesignButtonHeight = 26;

		private const int SvImageSize = 64;

		private readonly ThemeConfig theme;
		private readonly TextWidget hexText;
		private readonly ImageBuffer hueImage = new ImageBuffer(360, 1);
		private readonly ImageBuffer svImage = new ImageBuffer(SvImageSize, SvImageSize);
		private readonly ImageBuffer alphaImage = new ImageBuffer(256, 1);

		private Color color;
		private Color saved;
		private double savedHue;
		private double savedSaturation;
		private double savedValue;
		private bool isOpen;
		private double hue;
		private double saturation;
		private double value;
		private double svImageHue = double.NaN;
		private Color alphaImageColor;
		private DragTarget dragTarget;

		/// <summary>Creates a closed picker showing <paramref name="color"/>.</summary>
		public ColorPicker(Color color, ThemeConfig theme)
		{
			this.theme = theme;
			this.color = color;
			HsvColor.ToHsv(color, out hue, out saturation, out value);
			Name = "Color Picker";
			HAnchor = HAnchor.Absolute;
			VAnchor = VAnchor.Absolute;
			TabStop = true;
			Cursor = Cursors.Hand;

			for (int x = 0; x < hueImage.Width; x++)
			{
				hueImage.SetPixel(x, 0, HsvColor.FromHsv(x, 1, 1));
			}

			hexText = new TextWidget("", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				Selectable = false,
				Visible = false,
				AutoExpandBoundsToText = true,
			};
			AddChild(hexText);

			CancelButton = new ThemedTextButton("Cancel", theme) { Name = "Color Picker Cancel", Visible = false };
			CancelButton.Click += (s, e) => Cancel();
			AddChild(CancelButton);

			SelectButton = new ThemedTextButton("Select", theme) { Name = "Color Picker Select", Visible = false };
			SelectButton.Click += (s, e) => Select();
			AddChild(SelectButton);

			UpdateLayout();
		}

		private enum DragTarget
		{
			None,
			Hue,
			SaturationValue,
			Alpha,
		}

		/// <summary>Raised live whenever <see cref="Color"/> changes, by dragging, Cancel or code.</summary>
		public event EventHandler ColorChanged;

		/// <summary>
		/// Raised when the user commits the colour with Select (the button, Enter/Space or a swatch click
		/// while open) - agg-gui's on_select. Cancel does not raise it.
		/// </summary>
		public event EventHandler Selected;

		/// <summary>
		/// Raised when the user dismisses the open panel with Cancel (the button or Escape) - agg-gui's on_cancel.
		/// Raised even when the colour never changed, which <see cref="ColorChanged"/> is not.
		/// </summary>
		public event EventHandler Cancelled;

		/// <summary>The panel's Cancel button.</summary>
		public GuiWidget CancelButton { get; }

		/// <summary>The panel's Select button.</summary>
		public GuiWidget SelectButton { get; }

		/// <summary>
		/// The picked colour. Setting a different one updates the panel and raises <see cref="ColorChanged"/>;
		/// a grey keeps the current hue, so the hue bar does not jump.
		/// </summary>
		public Color Color
		{
			get => color;
			set
			{
				if (value == color)
				{
					return;
				}

				HsvColor.ToHsv(value, out var newHue, out saturation, out this.value);
				if (saturation > 0)
				{
					hue = newHue;
				}

				SetColor(value);
			}
		}

		/// <summary>Hue of the panel in degrees [0, 360).</summary>
		public double Hue => hue;

		/// <summary>Saturation of the panel in [0, 1].</summary>
		public double Saturation => saturation;

		/// <summary>Value (brightness) of the panel in [0, 1].</summary>
		public double Value => value;

		/// <summary>Whether the panel is showing.</summary>
		public bool IsOpen => isOpen;

		/// <summary>The hex readout, for tests.</summary>
		internal TextWidget HexText => hexText;

		/// <summary>The swatch, in local coordinates.</summary>
		public RectangleDouble SwatchBounds => Row(0, DesignSwatchHeight, inset: false);

		/// <summary>The hue bar, in local coordinates (hue 0 at the left).</summary>
		public RectangleDouble HueBounds => Row(DesignSwatchHeight + DesignPad, DesignHueHeight);

		/// <summary>The saturation/value square, in local coordinates (saturation rightwards, value upwards).</summary>
		public RectangleDouble SaturationValueBounds => Row(DesignSwatchHeight + DesignPad + DesignHueHeight + DesignRowGap, DesignSvHeight);

		/// <summary>The alpha bar, in local coordinates (transparent at the left).</summary>
		public RectangleDouble AlphaBounds => Row(DesignSwatchHeight + DesignPad + DesignHueHeight + DesignSvHeight + 2 * DesignRowGap, DesignAlphaHeight);

		private RectangleDouble HexBounds => Row(DesignSwatchHeight + DesignPad + DesignHueHeight + DesignSvHeight + DesignAlphaHeight + 3 * DesignRowGap, DesignHexHeight);

		private static double PanelBodyHeight => DesignPad + DesignHueHeight + DesignSvHeight + DesignAlphaHeight + DesignHexHeight
			+ 4 * DesignRowGap + DesignButtonHeight + DesignPad;

		/// <summary>Opens the panel, remembering the current colour for <see cref="Cancel"/>.</summary>
		public void Open()
		{
			if (!isOpen)
			{
				// The HSV is saved too, not re-derived from the colour on Cancel: a hue drag on black or a
				// grey shown at a remembered hue never changes the bytes, yet Cancel must put the markers back.
				saved = color;
				savedHue = hue;
				savedSaturation = saturation;
				savedValue = value;
				isOpen = true;
				UpdateLayout();
			}
		}

		/// <summary>Closes the panel and puts back the colour it opened with.</summary>
		public void Cancel()
		{
			if (isOpen)
			{
				Close();
				hue = savedHue;
				saturation = savedSaturation;
				value = savedValue;
				Invalidate();
				SetColor(saved);
				Cancelled?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>Closes the panel keeping the current colour and raises <see cref="Selected"/>.</summary>
		public void Select()
		{
			if (isOpen)
			{
				Close();
				Selected?.Invoke(this, EventArgs.Empty);
			}
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			bool handled = true;
			switch (keyEvent.KeyCode)
			{
				case Keys.Enter:
				case Keys.Space:
					if (isOpen)
					{
						Select();
					}
					else
					{
						Open();
					}

					break;

				case Keys.Escape when isOpen:
					Cancel();
					break;

				default:
					handled = false;
					break;
			}

			if (handled)
			{
				keyEvent.Handled = true;
				keyEvent.SuppressKeyPress = true;
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
			// The regions are tested before base.OnMouseDown routes the press on to the children. The
			// Cancel/Select buttons and the hex readout sit outside every region, so each press is handled
			// either here or by a child, never both; a swatch click that closes the panel hides the buttons
			// before routing, so it cannot also land on one.
			if (mouseEvent.Button == MouseButtons.Left)
			{
				var position = mouseEvent.Position;
				dragTarget = DragTarget.None;
				if (SwatchBounds.Contains(position))
				{
					if (isOpen)
					{
						Select();
					}
					else
					{
						Open();
					}
				}
				else if (isOpen)
				{
					if (HueBounds.Contains(position))
					{
						dragTarget = DragTarget.Hue;
					}
					else if (SaturationValueBounds.Contains(position))
					{
						dragTarget = DragTarget.SaturationValue;
					}
					else if (AlphaBounds.Contains(position))
					{
						dragTarget = DragTarget.Alpha;
					}

					DragTo(position);
				}
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (MouseCaptured)
			{
				DragTo(mouseEvent.Position);
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			dragTarget = DragTarget.None;
			base.OnMouseUp(mouseEvent);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var scale = DeviceScale;
			var radius = 4 * scale;

			// Read every frame so a live theme switch reaches the picker.
			hexText.TextColor = theme.TextColor;
			var border = theme.TextColor.WithAlpha(90);

			if (isOpen)
			{
				graphics2D.Render(new RoundedRect(LocalBounds, radius), theme.MinimalShade);

				var hueBounds = HueBounds;
				graphics2D.Render(hueImage, hueBounds.Left, hueBounds.Bottom, hueBounds.Width, hueBounds.Height);
				DrawVerticalMarker(graphics2D, hueBounds, hue / 360);

				var sv = SaturationValueBounds;
				UpdateSvImage();
				graphics2D.Render(svImage, sv.Left, sv.Bottom, sv.Width, sv.Height);
				var cross = new Vector2(sv.Left + saturation * sv.Width, sv.Bottom + value * sv.Height);
				graphics2D.Render(new Stroke(new Ellipse(cross, 4 * scale), 1.5 * scale), value > .5 ? Color.Black : Color.White);

				var alphaBounds = AlphaBounds;
				DrawChecker(graphics2D, alphaBounds, 4 * scale);
				UpdateAlphaImage();
				graphics2D.Render(alphaImage, alphaBounds.Left, alphaBounds.Bottom, alphaBounds.Width, alphaBounds.Height);
				DrawVerticalMarker(graphics2D, alphaBounds, color.alpha / 255.0);

				graphics2D.Render(new Stroke(new RoundedRect(HexBounds, 3 * scale), scale), border);
			}

			// The swatch: checker behind so a translucent colour reads as translucent.
			var swatch = SwatchBounds;
			DrawChecker(graphics2D, swatch, 5 * scale);
			graphics2D.FillRectangle(swatch, color);
			graphics2D.Render(new Stroke(new RoundedRect(swatch, radius), scale), border);

			if (Focused)
			{
				var inset = .75 * scale;
				var ring = new RoundedRect(swatch.Left + inset, swatch.Bottom + inset, swatch.Right - inset, swatch.Top - inset, radius);
				graphics2D.Render(new Stroke(ring, 1.5 * scale), theme.EditFieldColors.Focused.BorderColor);
			}

			base.OnDraw(graphics2D);
		}

		/// <summary>Sets the panel from a drag at <paramref name="position"/> on the region the press started in.</summary>
		private void DragTo(Vector2 position)
		{
			if (!double.IsFinite(position.X) || !double.IsFinite(position.Y))
			{
				return;
			}

			double alpha = color.alpha / 255.0;
			switch (dragTarget)
			{
				case DragTarget.Hue:
					var hueBounds = HueBounds;
					// Clamped just short of 360, which would wrap to red at the right end.
					hue = Math.Clamp((position.X - hueBounds.Left) / hueBounds.Width, 0, 1) * 359.999;
					break;

				case DragTarget.SaturationValue:
					var sv = SaturationValueBounds;
					saturation = Math.Clamp((position.X - sv.Left) / sv.Width, 0, 1);
					value = Math.Clamp((position.Y - sv.Bottom) / sv.Height, 0, 1);
					break;

				case DragTarget.Alpha:
					var alphaBounds = AlphaBounds;
					alpha = Math.Clamp((position.X - alphaBounds.Left) / alphaBounds.Width, 0, 1);
					break;

				default:
					return;
			}

			// The hue and SV markers move even when the byte colour does not (a hue drag on black).
			Invalidate();
			SetColor(HsvColor.FromHsv(hue, saturation, value, alpha));
		}

		private void SetColor(Color newColor)
		{
			if (newColor == color)
			{
				return;
			}

			color = newColor;
			UpdateHexText();
			Invalidate();
			ColorChanged?.Invoke(this, EventArgs.Empty);
		}

		private void Close()
		{
			if (isOpen)
			{
				isOpen = false;
				dragTarget = DragTarget.None;
				UpdateLayout();
			}
		}

		/// <summary>Sizes the widget for the open state and places the hex readout and buttons.</summary>
		private void UpdateLayout()
		{
			var scale = DeviceScale;
			var height = (DesignSwatchHeight + (isOpen ? PanelBodyHeight : 0)) * scale;
			LocalBounds = new RectangleDouble(0, 0, DesignPanelWidth * scale, height);

			hexText.Visible = isOpen;
			CancelButton.Visible = isOpen;
			SelectButton.Visible = isOpen;
			UpdateHexText();

			if (isOpen)
			{
				var pad = DesignPad * scale;
				var buttonWidth = (Width - 3 * pad) / 2;
				foreach (var button in new[] { CancelButton, SelectButton })
				{
					button.HAnchor = HAnchor.Absolute;
					button.VAnchor = VAnchor.Absolute;
					button.Size = new Vector2(buttonWidth, DesignButtonHeight * scale);
				}

				CancelButton.Position = new Vector2(pad, pad);
				SelectButton.Position = new Vector2(2 * pad + buttonWidth, pad);
			}

			Invalidate();
		}

		/// <summary>Shows the colour's hex and recentres it - adding or dropping the alpha digits changes its width.</summary>
		private void UpdateHexText()
		{
			hexText.Text = HsvColor.ToHex(color);
			var hex = HexBounds;
			hexText.Position = new Vector2(Math.Round(hex.Center.X - hexText.Width / 2), Math.Round(hex.Center.Y - hexText.Height / 2));
		}

		/// <summary>A full-width (less padding) row <paramref name="designTop"/> design units below the top.</summary>
		private RectangleDouble Row(double designTop, double designHeight, bool inset = true)
		{
			var scale = DeviceScale;
			var top = Height - designTop * scale;
			var pad = inset ? DesignPad * scale : 0;
			return new RectangleDouble(pad, top - designHeight * scale, Width - pad, top);
		}

		private void UpdateSvImage()
		{
			if (svImageHue == hue)
			{
				return;
			}

			svImageHue = hue;
			for (int y = 0; y < SvImageSize; y++)
			{
				for (int x = 0; x < SvImageSize; x++)
				{
					svImage.SetPixel(x, y, HsvColor.FromHsv(hue, x / (SvImageSize - 1.0), y / (SvImageSize - 1.0)));
				}
			}

			svImage.MarkImageChanged();
		}

		private void UpdateAlphaImage()
		{
			var opaque = new Color(color, 255);
			if (alphaImageColor == opaque)
			{
				return;
			}

			alphaImageColor = opaque;
			for (int x = 0; x < alphaImage.Width; x++)
			{
				alphaImage.SetPixel(x, 0, new Color(color, x));
			}

			alphaImage.MarkImageChanged();
		}

		private void DrawVerticalMarker(Graphics2D graphics2D, RectangleDouble bounds, double fraction)
		{
			var scale = DeviceScale;
			var x = bounds.Left + Math.Clamp(fraction, 0, 1) * bounds.Width;
			var marker = new RectangleDouble(x - 2 * scale, bounds.Bottom - scale, x + 2 * scale, bounds.Top + scale);
			graphics2D.Render(new Stroke(new RoundedRect(marker, scale), 1.5 * scale), theme.TextColor);
		}

		internal static void DrawChecker(Graphics2D graphics2D, RectangleDouble bounds, double tile)
		{
			graphics2D.FillRectangle(bounds, Color.White);
			var dark = new Color(204, 204, 204);
			int row = 0;
			for (var y = bounds.Bottom; y < bounds.Top; y += tile, row++)
			{
				int column = 0;
				for (var x = bounds.Left; x < bounds.Right; x += tile, column++)
				{
					if ((row + column) % 2 == 1)
					{
						graphics2D.FillRectangle(new RectangleDouble(x, y, Math.Min(x + tile, bounds.Right), Math.Min(y + tile, bounds.Top)), dark);
					}
				}
			}
		}
	}
}
