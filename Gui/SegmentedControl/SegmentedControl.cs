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
	/// Every segment is as wide as the widest label, the macOS default.
	/// </remarks>
	public class SegmentedControl : GuiWidget
	{
		private const double DesignHeight = 24;
		private const double DesignPadding = 12;
		private const double DesignCornerRadius = 6;

		/// <summary>Gap between the track edge and the selected fill, so it reads as a raised pill.</summary>
		private const double DesignFillInset = 1;

		private const double DesignDividerInset = 4;

		private readonly ThemeConfig theme;
		private readonly List<GuiWidget> segments = new List<GuiWidget>();
		private readonly List<TextWidget> labelWidgets = new List<TextWidget>();
		private int selectedIndex;

		/// <summary>Creates a control with one segment per label and <paramref name="selectedIndex"/> selected.</summary>
		public SegmentedControl(IEnumerable<string> labels, ThemeConfig theme, int selectedIndex = 0)
		{
			this.theme = theme;
			Labels = labels.ToList();
			this.selectedIndex = Labels.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, Labels.Count - 1);
			HAnchor = HAnchor.Absolute;
			VAnchor = VAnchor.Absolute;
			TabStop = true;

			var scale = DeviceScale;
			var height = DesignHeight * scale;

			for (int i = 0; i < Labels.Count; i++)
			{
				var label = new TextWidget(Labels[i], pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
				{
					HAnchor = HAnchor.Center,
					VAnchor = VAnchor.Center,
					Selectable = false,
				};
				labelWidgets.Add(label);
			}

			var segmentWidth = labelWidgets.Count == 0 ? 0 : labelWidgets.Max(l => l.Width) + 2 * DesignPadding * scale;
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

			LocalBounds = new RectangleDouble(0, 0, segmentWidth * Labels.Count, height);
			UpdateLabelColors();
		}

		/// <summary>Raised when <see cref="SelectedIndex"/> changes, by click, keyboard or code.</summary>
		public event EventHandler SelectedIndexChanged;

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

		private void UpdateLabelColors()
		{
			// Ink on the accent has to stay readable whatever accent the theme picks.
			var selectedText = theme.TextColor.WithContrast(theme.PrimaryAccentColor, 3).ToColor();
			for (int i = 0; i < labelWidgets.Count; i++)
			{
				labelWidgets[i].TextColor = i == selectedIndex ? selectedText : theme.TextColor;
			}
		}
	}
}
