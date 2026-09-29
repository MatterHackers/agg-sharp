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
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A row of joined segments of which exactly one is selected - the agg-gui SegmentedControl (the macOS
	/// segmented picker). One rounded track, segments butted together with hairline dividers, the selected
	/// segment filled with the theme accent. Left/Right/Home/End move the selection when it has focus.
	/// </summary>
	/// <remarks>
	/// Each segment is a child widget named "<c>label</c> Segment", so automation can click one by name.
	/// Two segments with the same label therefore share a name; a caller that needs both reachable by
	/// name gives them distinct labels (or finds them through <see cref="Segments"/>).
	/// Every segment is as wide as the widest label, the macOS default; a control laid out wider than that
	/// (HAnchor.Stretch, or a Width set outright) shares the width equally, like CSS <c>repeat(n, 1fr)</c>.
	/// <see cref="ThemeConfig.SegmentedStyle"/> picks the Pill look or the Strip look, read at draw time.
	/// </remarks>
	public class SegmentedControl : GuiWidget
	{
		private const double DesignPadding = 12;

		/// <summary>The Pill track's corner radius. The Strip look rounds by ThemeConfig.ButtonRadius instead.</summary>
		private const double DesignCornerRadius = 6;

		/// <summary>Gap between the track edge and the selected fill, so it reads as a raised pill.</summary>
		private const double DesignFillInset = 1;

		private const double DesignDividerInset = 4;

		private readonly ThemeConfig theme;
		private readonly List<GuiWidget> segments = new List<GuiWidget>();
		private readonly List<TextWidget> labelWidgets = new List<TextWidget>();
		private int selectedIndex;
		private readonly SegmentedStyle? style;

		/// <summary>Every segment's width at the control's natural size: the widest label plus padding, whole pixels.</summary>
		private double naturalSegmentWidth;

		/// <summary>
		/// Creates a control with one segment per label and <paramref name="selectedIndex"/> selected, drawn in
		/// <paramref name="style"/>, or in the theme's <see cref="ThemeConfig.SegmentedStyle"/> when that is null.
		/// </summary>
		/// <remarks>
		/// The style is fixed here rather than settable later because it decides the segment widths: Strip
		/// bolds the selected label, so its segments are measured to fit every label bold.
		/// </remarks>
		public SegmentedControl(IEnumerable<string> labels, ThemeConfig theme, int selectedIndex = 0, SegmentedStyle? style = null)
		{
			this.theme = theme;
			this.style = style;
			Labels = labels.ToList();
			this.selectedIndex = Labels.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, Labels.Count - 1);
			HAnchor = HAnchor.Absolute;
			VAnchor = VAnchor.Absolute;
			TabStop = true;

			var scale = DeviceScale;
			var height = theme.SegmentedHeight * scale;

			// The widest label decides every segment's width. Strip draws the selected label bold, so its
			// width is measured bold too - the segments must not change size when the selection moves.
			double widestLabel = 0;
			for (int i = 0; i < Labels.Count; i++)
			{
				var label = new TextWidget(Labels[i], pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
				{
					HAnchor = HAnchor.Center,
					VAnchor = VAnchor.Center,
					Selectable = false,
				};

				// Strip bolds the selected label, and bold is wider: without this the label keeps its regular
				// width and the bold text is cut to an ellipsis. Pill never changes the typeface, and is left
				// exactly as it was.
				if (Style == SegmentedStyle.Strip)
				{
					label.AutoExpandBoundsToText = true;
				}

				labelWidgets.Add(label);
				widestLabel = Math.Max(widestLabel, label.Width);
				if (Style == SegmentedStyle.Strip)
				{
					widestLabel = Math.Max(widestLabel, new TextWidget(Labels[i], pointSize: theme.DefaultFontSize, bold: true).Width);
				}
			}

			var segmentWidth = labelWidgets.Count == 0 ? 0 : widestLabel + 2 * DesignPadding * scale;
			segmentWidth = Math.Ceiling(segmentWidth);

			for (int i = 0; i < Labels.Count; i++)
			{
				int index = i;
				var segment = new GuiWidget(segmentWidth, height)
				{
					Name = Labels[i] + " Segment",
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Absolute,
					Position = new VectorMath.Vector2(i * segmentWidth, 0),
					Cursor = Cursors.Hand,
				};
				segment.AddChild(labelWidgets[i]);
				segment.Click += (s, e) =>
				{
					if (e.Button == MouseButtons.Left)
					{
						SelectedIndex = index;
					}
				};
				segment.MouseEnterBounds += (s, e) => Invalidate();
				segment.MouseLeaveBounds += (s, e) => Invalidate();
				segments.Add(segment);
				AddChild(segment);
			}

			naturalSegmentWidth = segmentWidth;
			LocalBounds = new RectangleDouble(0, 0, segmentWidth * Labels.Count, height);
			UpdateLabelColors();
		}

		/// <summary>
		/// Shares the control's width equally among the segments, so a control stretched wider than its labels
		/// need reads as n equal columns. Boundaries are rounded to whole pixels so every divider and selection
		/// edge is crisp, the leftover pixel going to whichever segments rounding hands it. Narrower than its
		/// natural width, the control keeps its natural segments and clips, as it always has - squeezing would
		/// cut every label instead of hiding the last segment's end. At the natural width this is exactly where
		/// the constructor put them.
		/// </summary>
		public override void OnBoundsChanged(EventArgs e)
		{
			if (segments.Count > 0)
			{
				var share = Math.Max(naturalSegmentWidth, Width / segments.Count);
				for (int i = 0; i < segments.Count; i++)
				{
					var left = Math.Round(i * share);
					var right = Math.Round((i + 1) * share);
					segments[i].Width = right - left;
					segments[i].Height = Height;
					segments[i].Position = new VectorMath.Vector2(left, 0);
				}
			}

			base.OnBoundsChanged(e);
		}

		/// <summary>Raised when <see cref="SelectedIndex"/> changes, by click, keyboard or code.</summary>
		public event EventHandler SelectedIndexChanged;

		/// <summary>The look this control draws in: its own style if it was given one, otherwise the theme's.</summary>
		public SegmentedStyle Style => style ?? theme.SegmentedStyle;

		/// <summary>The segment labels, in order.</summary>
		public IReadOnlyList<string> Labels { get; }

		/// <summary>The segment widgets, in order - for tests and automation.</summary>
		public IReadOnlyList<GuiWidget> Segments => segments;

		/// <summary>
		/// The selected segment. Setting an index outside the labels is ignored; setting a different valid
		/// one raises <see cref="SelectedIndexChanged"/>.
		/// </summary>
		public int SelectedIndex
		{
			get => selectedIndex;
			set
			{
				if (value < 0 || value >= Labels.Count || value == selectedIndex)
				{
					return;
				}

				selectedIndex = value;
				UpdateLabelColors();
				Invalidate();
				SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>The label of the selected segment, or null when there are none.</summary>
		public string SelectedLabel => Labels.Count == 0 ? null : Labels[selectedIndex];

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			int next = keyEvent.KeyCode switch
			{
				Keys.Left or Keys.Up => selectedIndex - 1,
				Keys.Right or Keys.Down => selectedIndex + 1,
				Keys.Home => 0,
				Keys.End => Labels.Count - 1,
				_ => -2,
			};

			if (next != -2 && Labels.Count > 0)
			{
				SelectedIndex = Math.Clamp(next, 0, Labels.Count - 1);

				// Consumed even at an end, so Left on the first segment does not scroll an enclosing view.
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

		public override void OnDraw(Graphics2D graphics2D)
		{
			var scale = DeviceScale;
			var bounds = LocalBounds;
			var radius = DesignCornerRadius * scale;
			var accent = theme.PrimaryAccentColor;

			// Colours are read from the theme every frame so a live theme switch reaches this widget.
			UpdateLabelColors();

			if (Style == SegmentedStyle.Strip)
			{
				DrawStrip(graphics2D);
				base.OnDraw(graphics2D);
				return;
			}

			// Track.
			graphics2D.Render(new RoundedRect(bounds, radius), theme.MinimalShade);

			if (segments.Count > 0)
			{
				// A hovered unselected segment gets a subtle fill; the selected one the accent, darker while pressed.
				for (int i = 0; i < segments.Count; i++)
				{
					var segment = segments[i];
					var underMouse = segment.UnderMouseState != UnderMouseState.NotUnderMouse && Enabled;
					if (i == selectedIndex)
					{
						var fill = underMouse && segment.MouseCaptured
							? accent.AdjustLightness(.9).ToColor()
							: (underMouse ? accent.AdjustLightness(1.05).ToColor() : accent);
						graphics2D.Render(SegmentFill(i), fill);
					}
					else if (underMouse)
					{
						graphics2D.Render(SegmentFill(i), theme.SlightShade);
					}
				}

				// Hairline dividers, skipped beside the selected segment so its fill reads as one pill.
				var dividerColor = theme.TextColor.WithAlpha(50);
				var dividerInset = DesignDividerInset * scale;
				for (int i = 1; i < segments.Count; i++)
				{
					if (i == selectedIndex || i - 1 == selectedIndex)
					{
						continue;
					}

					var x = Math.Round(segments[i].Position.X) + .5;
					graphics2D.Line(new VectorMath.Vector2(x, bounds.Bottom + dividerInset), new VectorMath.Vector2(x, bounds.Top - dividerInset), dividerColor, scale);
				}
			}

			if (Focused)
			{
				var inset = .75 * scale;
				var ring = new RoundedRect(bounds.Left + inset, bounds.Bottom + inset, bounds.Right - inset, bounds.Top - inset, radius);
				graphics2D.Render(new Stroke(ring, 1.5 * scale), theme.EditFieldColors.Focused.BorderColor);
			}

			base.OnDraw(graphics2D);
		}

		/// <summary>
		/// Segment <paramref name="index"/>'s fill shape: its rect inset from the track, rounded only on the
		/// corners it shares with the track.
		/// </summary>
		private IVertexSource SegmentFill(int index)
		{
			var scale = DeviceScale;
			var inset = DesignFillInset * scale;
			var segment = segments[index];
			bool first = index == 0;
			bool last = index == segments.Count - 1;
			var left = segment.Position.X + (first ? inset : 0);
			var right = segment.Position.X + segment.Width - (last ? inset : 0);
			var fill = new RoundedRect(left, inset, right, segment.Height - inset, 0);
			var radius = Math.Max(0, (DesignCornerRadius - DesignFillInset) * scale);
			var leftRadius = first ? radius : 0;
			var rightRadius = last ? radius : 0;

			// radius order is bottom-left, bottom-right, top-right, top-left.
			fill.radius(leftRadius, rightRadius, rightRadius, leftRadius);
			return fill;
		}

		/// <summary>
		/// The Strip look: a control-fill track in a 1 px rounded outline, square full height dividers, and the
		/// selected segment filled edge to edge with the accent, rounded only where it meets the outline.
		/// </summary>
		private void DrawStrip(Graphics2D graphics2D)
		{
			var scale = DeviceScale;
			var bounds = LocalBounds;
			var radius = theme.ButtonRadius * scale;
			// Whole device pixels, so the outline and dividers stay crisp at fractional scales like 1.5.
			var stroke = Math.Max(1, Math.Round(scale));
			var border = theme.ControlBorderColorIfSet ?? SelectionControlStyle.WidgetStroke(theme);
			var accent = theme.PrimaryAccentColor;

			graphics2D.Render(new RoundedRect(bounds, radius), theme.ResolvedControlFillColor);

			for (int i = 0; i < segments.Count; i++)
			{
				var segment = segments[i];
				var underMouse = segment.UnderMouseState != UnderMouseState.NotUnderMouse && Enabled;
				if (i == selectedIndex)
				{
					var fill = underMouse && segment.MouseCaptured
						? accent.AdjustLightness(.9).ToColor()
						: (underMouse ? accent.AdjustLightness(1.05).ToColor() : accent);
					graphics2D.Render(StripSegmentFill(i, radius), fill);
				}
				else if (underMouse)
				{
					graphics2D.Render(StripSegmentFill(i, radius), theme.MinimalShade);
				}
			}

			// Dividers run the full height and stay beside the selection too: the design draws each
			// segment's right edge (CSS border-right) as a plain 1 px line, so it ends on the boundary,
			// snapped to a whole pixel to stay crisp.
			for (int i = 1; i < segments.Count; i++)
			{
				var x = Math.Round(segments[i].Position.X);
				graphics2D.FillRectangle(x - stroke, bounds.Bottom, x, bounds.Top, border);
			}

			var outline = new RoundedRect(bounds.Left + stroke / 2, bounds.Bottom + stroke / 2, bounds.Right - stroke / 2, bounds.Top - stroke / 2, Math.Max(0, radius - stroke / 2));
			graphics2D.Render(new Stroke(outline, stroke), border);

			if (Focused)
			{
				var inset = .75 * scale;
				var ring = new RoundedRect(bounds.Left + inset, bounds.Bottom + inset, bounds.Right - inset, bounds.Top - inset, radius);
				graphics2D.Render(new Stroke(ring, 1.5 * scale), theme.EditFieldColors.Focused.BorderColor);
			}
		}

		/// <summary>
		/// Segment <paramref name="index"/>'s full-bleed fill for the Strip look, rounded by the outline's radius
		/// on the corners it shares with the control's ends so no square corner pokes past the outline.
		/// </summary>
		private IVertexSource StripSegmentFill(int index, double radius)
		{
			var segment = segments[index];
			var leftRadius = index == 0 ? radius : 0;
			var rightRadius = index == segments.Count - 1 ? radius : 0;
			var fill = new RoundedRect(segment.Position.X, 0, segment.Position.X + segment.Width, segment.Height, 0);

			// radius order is bottom-left, bottom-right, top-right, top-left.
			fill.radius(leftRadius, rightRadius, rightRadius, leftRadius);
			return fill;
		}

		private void UpdateLabelColors()
		{
			// Ink on the accent has to stay readable whatever accent the theme picks.
			var selectedText = theme.OnAccentTextColor;
			bool strip = Style == SegmentedStyle.Strip;
			for (int i = 0; i < labelWidgets.Count; i++)
			{
				bool selected = i == selectedIndex;
				labelWidgets[i].TextColor = selected ? selectedText : theme.TextColor;

				// Only Strip bolds the selection; the Pill look never touches the typeface.
				if (strip && labelWidgets[i].Bold != selected)
				{
					labelWidgets[i].Bold = selected;
				}
			}
		}
	}
}
