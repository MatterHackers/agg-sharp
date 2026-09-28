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
	/// agg-gui's ColorWheelPicker: a hue ring around a saturation/value triangle, an alpha bar with a percent
	/// readout, an editable hex field, old | new preview swatches, an optional "No Color (Pass Through)" check
	/// box and Cancel / Select buttons. Always open - put it in a layout, or in a <see cref="ColorDialog"/>.
	/// </summary>
	/// <remarks>
	/// Dragging the ring, the triangle or the alpha bar and typing a hex all change the colour live
	/// (<see cref="ColorChanged"/>). Select raises <see cref="Selected"/>; Cancel puts back the colour the picker
	/// was made with, raises <see cref="ColorChanged"/> for that, then <see cref="Canceled"/>. The ring and the
	/// triangle are images rendered at the screen's pixel size, the triangle's rebuilt only when the hue moves.
	/// </remarks>
	public class ColorWheelPicker : GuiWidget
	{
		// agg-gui's layout constants, in design units (times DeviceScale).
		private const double Pad = 10;
		private const double RowGap = 6;
		private const double WheelSize = 200;
		private const double AlphaHeight = 22;
		private const double HexHeight = 32;
		private const double PreviewHeight = 32;
		private const double NoColorHeight = 20;
		private const double ButtonHeight = 24;
		private const double AlphaPercentWidth = 44;

		// Radii as fractions of half the wheel, NodeDesigner's 85 / 60 / 55 on a 95 half canvas.
		private const double OuterRatio = 85.0 / 95.0;
		private const double InnerRatio = 60.0 / 95.0;
		private const double TriangleRatio = 55.0 / 95.0;

		private readonly ThemeConfig theme;
		private readonly Color saved;
		private readonly double savedHue;
		private readonly TextWidget alphaPercent;
		private ImageBuffer wheelImage;
		private ImageBuffer triangleImage;
		private double triangleImageHue = double.NaN;
		private double hue;
		private double saturation;
		private double value;
		private double alpha;
		private bool passThrough;
		private bool updatingHex;
		private DragTarget dragTarget;

		/// <summary>
		/// Creates a picker showing <paramref name="color"/>. <paramref name="allowNone"/> adds the "No Color
		/// (Pass Through)" check box, ticked when <paramref name="color"/> is fully transparent;
		/// <paramref name="showAlpha"/> false hides the alpha bar.
		/// </summary>
		public ColorWheelPicker(Color color, ThemeConfig theme, bool allowNone = false, bool showAlpha = true)
		{
			this.theme = theme;
			this.saved = color;
			this.AllowNone = allowNone;
			this.ShowAlpha = showAlpha;
			Name = "Color Wheel Picker";
			HAnchor = HAnchor.Absolute;
			VAnchor = VAnchor.Absolute;
			SetFromColor(color);
			savedHue = hue;
			passThrough = allowNone && color.alpha == 0;

			var scale = DeviceScale;
			LocalBounds = new RectangleDouble(0, 0, (WheelSize + 2 * Pad) * scale, DesignHeight * scale);

			alphaPercent = new TextWidget("", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				Selectable = false,
				AutoExpandBoundsToText = true,
				Visible = showAlpha,
			};
			AddChild(alphaPercent);

			HexField = new ThemedTextEditWidget(HsvColor.ToHex(color), theme, pixelWidth: (WheelSize - 20) * scale)
			{
				Name = "Color Wheel Hex",
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
			};
			HexField.ActualTextEditWidget.TextChanged += (s, e) => HexTyped();
			AddChild(HexField);

			if (allowNone)
			{
				NoColorCheckBox = new CheckBox("No Color (Pass Through)", theme.TextColor, theme.DefaultFontSize)
				{
					Name = "Color Wheel No Color",
					Checked = passThrough,
				};
				NoColorCheckBox.CheckedStateChanged += (s, e) => SetPassThrough(NoColorCheckBox.Checked);
				AddChild(NoColorCheckBox);
			}

			CancelButton = new ThemedTextButton("Cancel", theme) { Name = "Color Wheel Cancel" };
			CancelButton.Click += (s, e) => Cancel();
			AddChild(CancelButton);

			SelectButton = new ThemedTextButton("Select", theme) { Name = "Color Wheel Select" };
			SelectButton.Click += (s, e) => Select();
			AddChild(SelectButton);

			PlaceChildren();
		}

		private enum DragTarget
		{
			None,
			Hue,
			SaturationValue,
			Alpha,
		}

		/// <summary>Raised live whenever <see cref="Color"/> or <see cref="PassThrough"/> changes.</summary>
		public event EventHandler ColorChanged;

		/// <summary>Raised when Select is clicked (agg-gui's on_select).</summary>
		public event EventHandler Selected;

		/// <summary>Raised when Cancel is clicked, after the colour has been put back (agg-gui's on_cancel).</summary>
		public event EventHandler Canceled;

		/// <summary>Whether the "No Color (Pass Through)" check box is shown.</summary>
		public bool AllowNone { get; }

		/// <summary>Whether the alpha bar is shown.</summary>
		public bool ShowAlpha { get; }

		/// <summary>The colour the picker was made with - the old swatch, and what Cancel puts back.</summary>
		public Color OriginalColor => saved;

		/// <summary>
		/// The working colour, or <see cref="Color.Transparent"/> while <see cref="PassThrough"/> is ticked.
		/// Setting it moves the markers (a grey keeps the current hue) and raises <see cref="ColorChanged"/>.
		/// </summary>
		public Color Color
		{
			get => passThrough ? Color.Transparent : HsvColor.FromHsv(hue, saturation, value, alpha);
			set
			{
				SetFromColor(value);
				passThrough = false;
				if (NoColorCheckBox != null)
				{
					NoColorCheckBox.Checked = false;
				}

				Changed(updateHex: true);
			}
		}

		/// <summary>Whether "No Color (Pass Through)" is ticked. Only ever true when <see cref="AllowNone"/> is.</summary>
		public bool PassThrough => passThrough;

		/// <summary>Hue of the ring marker in degrees [0, 360).</summary>
		public double Hue => hue;

		public ThemedTextEditWidget HexField { get; }

		/// <summary>The "No Color (Pass Through)" check box, or null without <see cref="AllowNone"/>.</summary>
		public CheckBox NoColorCheckBox { get; }

		public GuiWidget CancelButton { get; }

		public GuiWidget SelectButton { get; }

		/// <summary>The square the ring and triangle are drawn in, in local coordinates.</summary>
		public RectangleDouble WheelBounds => Row(Pad, WheelSize, (WheelSize + 2 * Pad - WheelSize) / 2);

		/// <summary>The centre of the ring and the triangle, in local coordinates.</summary>
		public Vector2 WheelCenter => WheelBounds.Center;

		/// <summary>The ring's inner and outer radius, in local units.</summary>
		public double RingInnerRadius => WheelBounds.Width / 2 * InnerRatio;

		public double RingOuterRadius => WheelBounds.Width / 2 * OuterRatio;

		/// <summary>The radius of the circle the triangle is inscribed in.</summary>
		public double TriangleRadius => WheelBounds.Width / 2 * TriangleRatio;

		/// <summary>The alpha bar (transparent at the left), in local coordinates; empty without <see cref="ShowAlpha"/>.</summary>
		public RectangleDouble AlphaBounds
		{
			get
			{
				if (!ShowAlpha)
				{
					return default;
				}

				var row = Row(Pad + WheelSize + RowGap, AlphaHeight, Pad);
				row.Right -= (AlphaPercentWidth + RowGap) * DeviceScale;
				return row;
			}
		}

		/// <summary>The old | new preview swatches, in local coordinates.</summary>
		public RectangleDouble PreviewBounds => Row(PreviewTop, PreviewHeight, Pad);

		private double AlphaRows => ShowAlpha ? AlphaHeight + RowGap : 0;

		private double HexTop => Pad + WheelSize + RowGap + AlphaRows;

		private double PreviewTop => HexTop + HexHeight + RowGap;

		private double DesignHeight => PreviewTop + PreviewHeight + RowGap + (AllowNone ? NoColorHeight + RowGap : 0) + ButtonHeight + Pad;

		/// <summary>Raises <see cref="Selected"/> with the current colour.</summary>
		public void Select()
		{
			Selected?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>Puts back <see cref="OriginalColor"/>, raises <see cref="ColorChanged"/> then <see cref="Canceled"/>.</summary>
		public void Cancel()
		{
			SetFromColor(saved);
			hue = savedHue;
			passThrough = AllowNone && saved.alpha == 0;
			if (NoColorCheckBox != null)
			{
				NoColorCheckBox.Checked = passThrough;
			}

			Changed(updateHex: true);
			Canceled?.Invoke(this, EventArgs.Empty);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			// The regions are tested before the press routes on to the children; none of the children sit on
			// the ring, triangle or alpha bar, so a press is handled here or by a child, never both.
			dragTarget = DragTarget.None;
			if (mouseEvent.Button == MouseButtons.Left)
			{
				var position = mouseEvent.Position;
				var offset = position - WheelCenter;
				if (ColorWheelMath.InRing(offset, RingInnerRadius, RingOuterRadius))
				{
					dragTarget = DragTarget.Hue;
				}
				else if (ColorWheelMath.InTriangle(position, WheelCenter, TriangleRadius, hue))
				{
					dragTarget = DragTarget.SaturationValue;
				}
				else if (ShowAlpha && AlphaBounds.Contains(position))
				{
					dragTarget = DragTarget.Alpha;
				}

				DragTo(position);
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (MouseCaptured && dragTarget != DragTarget.None)
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

			// Read every frame so a live theme switch reaches the picker.
			alphaPercent.TextColor = theme.TextColor;
			var border = theme.TextColor.WithAlpha(90);

			// agg-gui's panel: the window fill, outlined in the widget stroke (the idle edit-field border here;
			// the text-tinted border when a theme leaves that unset).
			var widgetStroke = theme.EditFieldColors?.Inactive?.BorderColor ?? default;
			if (widgetStroke.alpha == 0)
			{
				widgetStroke = border;
			}

			var panel = LocalBounds;
			graphics2D.Render(new RoundedRect(panel, 6 * scale), theme.BackgroundColor);
			panel.Inflate(-.5 * scale);
			graphics2D.Render(new Stroke(new RoundedRect(panel, 6 * scale), scale), widgetStroke);

			var wheel = WheelBounds;
			var pixels = (int)Math.Round(wheel.Width);
			if (wheelImage == null || wheelImage.Width != pixels)
			{
				wheelImage = RenderRing(pixels);
				triangleImageHue = double.NaN;
			}

			if (triangleImageHue != hue)
			{
				triangleImage = RenderTriangle(pixels, hue);
				triangleImageHue = hue;
			}

			graphics2D.Render(wheelImage, wheel.Left, wheel.Bottom);
			graphics2D.Render(triangleImage, wheel.Left, wheel.Bottom);

			if (ShowAlpha)
			{
				var alphaBounds = AlphaBounds;
				ColorPicker.DrawChecker(graphics2D, alphaBounds, 6 * scale);
				var opaque = HsvColor.FromHsv(hue, saturation, value);
				graphics2D.FillRectangle(alphaBounds, new Color(opaque, 0));
				for (int i = 0; i < 64; i++)
				{
					// 64 bands read as a smooth ramp at this width without an image to keep in step.
					var left = alphaBounds.Left + alphaBounds.Width * i / 64;
					var right = alphaBounds.Left + alphaBounds.Width * (i + 1) / 64;
					graphics2D.FillRectangle(new RectangleDouble(left, alphaBounds.Bottom, right, alphaBounds.Top), new Color(opaque, (int)Math.Round(255 * (i + .5) / 64)));
				}

				graphics2D.Render(new Stroke(new RoundedRect(alphaBounds, 0), scale), border);
			}

			var preview = PreviewBounds;
			ColorPicker.DrawChecker(graphics2D, preview, 6 * scale);
			var middle = preview.Left + preview.Width / 2;
			graphics2D.FillRectangle(new RectangleDouble(preview.Left, preview.Bottom, middle, preview.Top), saved);
			graphics2D.FillRectangle(new RectangleDouble(middle, preview.Bottom, preview.Right, preview.Top), Color);
			graphics2D.Render(new Stroke(new RoundedRect(preview, 0), scale), border);

			base.OnDraw(graphics2D);

			// The markers go over everything, as agg-gui paints them in paint_overlay.
			var ringRadius = (RingInnerRadius + RingOuterRadius) / 2;
			var hueAngle = hue * Math.PI / 180;
			DrawHandle(graphics2D, WheelCenter + new Vector2(Math.Cos(hueAngle), Math.Sin(hueAngle)) * ringRadius, 6 * scale);
			DrawHandle(graphics2D, ColorWheelMath.PointAt(saturation, value, WheelCenter, TriangleRadius, hue), 5 * scale);
			if (ShowAlpha)
			{
				var alphaBounds = AlphaBounds;
				var x = alphaBounds.Left + alpha * alphaBounds.Width;
				graphics2D.Line(new Vector2(x, alphaBounds.Bottom), new Vector2(x, alphaBounds.Top), Color.White, 3 * scale);
				graphics2D.Line(new Vector2(x, alphaBounds.Bottom), new Vector2(x, alphaBounds.Top), Color.Black, 1.5 * scale);
			}
		}

		private static void DrawHandle(Graphics2D graphics2D, Vector2 center, double radius)
		{
			var scale = DeviceScale;
			graphics2D.Render(new Stroke(new Ellipse(center, radius), 3 * scale), Color.White);
			graphics2D.Render(new Stroke(new Ellipse(center, radius), 1.5 * scale), Color.Black);
		}

		/// <summary>The hue ring, a pixel-sized image with a one pixel feather on both edges.</summary>
		private static ImageBuffer RenderRing(int size)
		{
			var image = new ImageBuffer(Math.Max(1, size), Math.Max(1, size));
			var half = size / 2.0;
			var outer = half * OuterRatio;
			var inner = half * InnerRatio;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					var offset = new Vector2(x + .5 - half, y + .5 - half);
					var radius = offset.Length;
					var coverage = Math.Clamp(Math.Min(outer - radius, radius - inner) + .5, 0, 1);
					if (coverage > 0)
					{
						var color = HsvColor.FromHsv(ColorWheelMath.HueFromOffset(offset), 1, 1);
						image.SetPixel(x, y, new Color(color, (int)Math.Round(coverage * 255)));
					}
				}
			}

			return image;
		}

		/// <summary>The saturation/value triangle for <paramref name="hue"/>, anti-aliased on its edges.</summary>
		private static ImageBuffer RenderTriangle(int size, double hue)
		{
			var image = new ImageBuffer(Math.Max(1, size), Math.Max(1, size));
			var center = new Vector2(size / 2.0, size / 2.0);
			var radius = size / 2.0 * TriangleRatio;
			// A barycentric weight crosses 1 over the triangle's height (1.5 radii), so this turns one into pixels.
			var pixelsPerWeight = 1.5 * radius;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					var point = new Vector2(x + .5, y + .5);
					var (s, v) = ColorWheelMath.SaturationValueAt(point, center, radius, hue);
					var inside = ColorWheelMath.InTriangle(point, center, radius, hue);
					var coverage = inside ? 1.0 : Math.Clamp(.5 - DistanceOutside(point, center, radius, hue, pixelsPerWeight), 0, 1);
					if (coverage > 0)
					{
						image.SetPixel(x, y, new Color(HsvColor.FromHsv(hue, s, v), (int)Math.Round(coverage * 255)));
					}
				}
			}

			return image;
		}

		/// <summary>Roughly how many pixels <paramref name="point"/> is outside the triangle (0 inside).</summary>
		private static double DistanceOutside(Vector2 point, Vector2 center, double radius, double hue, double pixelsPerWeight)
		{
			// The clamped saturation/value maps back to the nearest-ish point on the triangle.
			var (s, v) = ColorWheelMath.SaturationValueAt(point, center, radius, hue);
			var onEdge = ColorWheelMath.PointAt(s, v, center, radius, hue);
			return Math.Min((point - onEdge).Length, pixelsPerWeight);
		}

		private void DragTo(Vector2 position)
		{
			if (!double.IsFinite(position.X) || !double.IsFinite(position.Y))
			{
				return;
			}

			switch (dragTarget)
			{
				case DragTarget.Hue:
					hue = ColorWheelMath.HueFromOffset(position - WheelCenter);
					break;

				case DragTarget.SaturationValue:
					(saturation, value) = ColorWheelMath.SaturationValueAt(position, WheelCenter, TriangleRadius, hue);
					break;

				case DragTarget.Alpha:
					var alphaBounds = AlphaBounds;
					alpha = Math.Clamp((position.X - alphaBounds.Left) / alphaBounds.Width, 0, 1);
					break;

				default:
					return;
			}

			ClearPassThrough();
			Changed(updateHex: true);
		}

		private void HexTyped()
		{
			if (updatingHex
				|| !ColorWheelMath.TryParseHex(HexField.Text, out var typed)
				|| (typed == Color && !passThrough))
			{
				// A text set can report more than one change for the same text; the colour only changes once.
				return;
			}

			SetFromColor(typed);
			ClearPassThrough();
			Changed(updateHex: false);
		}

		/// <summary>Editing the colour means "this colour", so it unticks pass through, as agg-gui does.</summary>
		private void ClearPassThrough()
		{
			if (passThrough)
			{
				passThrough = false;
				NoColorCheckBox.Checked = false;
			}
		}

		private void SetPassThrough(bool on)
		{
			if (on != passThrough)
			{
				passThrough = on;
				Changed(updateHex: true);
			}
		}

		private void SetFromColor(Color color)
		{
			HsvColor.ToHsv(color, out var newHue, out saturation, out value);
			// A grey has no hue of its own; keeping the current one stops the triangle spinning to red.
			if (saturation > 0)
			{
				hue = newHue;
			}

			alpha = color.alpha / 255.0;
		}

		private void Changed(bool updateHex)
		{
			if (updateHex)
			{
				updatingHex = true;
				HexField.Text = HsvColor.ToHex(Color);
				updatingHex = false;
			}

			PlaceChildren();
			Invalidate();
			ColorChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>Places the hex field, alpha readout, check box and buttons in their rows.</summary>
		private void PlaceChildren()
		{
			var scale = DeviceScale;
			alphaPercent.Text = $"{Math.Round(alpha * 100)}%";
			if (ShowAlpha)
			{
				var bar = AlphaBounds;
				var cell = new RectangleDouble(bar.Right + RowGap * scale, bar.Bottom, Width - Pad * scale, bar.Top);
				alphaPercent.Position = new Vector2(Math.Round(cell.Center.X - alphaPercent.Width / 2), Math.Round(cell.Center.Y - alphaPercent.Height / 2));
			}

			var hex = Row(HexTop, HexHeight, Pad);
			HexField.Width = hex.Width;
			HexField.Position = new Vector2(hex.Left, Math.Round(hex.Center.Y - HexField.Height / 2));

			if (NoColorCheckBox != null)
			{
				var row = Row(PreviewTop + PreviewHeight + RowGap, NoColorHeight, Pad);
				NoColorCheckBox.Position = new Vector2(row.Left, Math.Round(row.Center.Y - NoColorCheckBox.Height / 2));
			}

			var pad = Pad * scale;
			var buttonWidth = (Width - 2 * pad - RowGap * scale) / 2;
			foreach (var button in new[] { CancelButton, SelectButton })
			{
				button.HAnchor = HAnchor.Absolute;
				button.VAnchor = VAnchor.Absolute;
				button.Size = new Vector2(buttonWidth, ButtonHeight * scale);
			}

			CancelButton.Position = new Vector2(pad, pad);
			SelectButton.Position = new Vector2(pad + buttonWidth + RowGap * scale, pad);
		}

		/// <summary>A row <paramref name="designTop"/> design units below the top, inset <paramref name="designInset"/> each side.</summary>
		private RectangleDouble Row(double designTop, double designHeight, double designInset)
		{
			var scale = DeviceScale;
			var top = Height - designTop * scale;
			return new RectangleDouble(designInset * scale, top - designHeight * scale, Width - designInset * scale, top);
		}
	}
}
