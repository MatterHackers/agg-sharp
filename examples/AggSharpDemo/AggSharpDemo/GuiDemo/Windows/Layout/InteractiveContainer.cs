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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout
{
	/// <summary>
	/// agg-gui's InteractiveContainer (misc/interactive_container.rs): a clickable canvas frame that counts clicks
	/// on its background and holds two real buttons, "Reset" and "+ 100", whose clicks run only their own action.
	/// </summary>
	/// <remarks>
	/// agg-sharp hands a press to every widget on the path down to the one under the mouse, so the frame sees the
	/// buttons' presses too; it ignores those by geometry, as agg-gui's hover test does, and so a press in the gap
	/// between the buttons still counts. The count is a TextWidget, which is not selectable, so a click on the
	/// number reaches the frame as agg-gui's painted label does.
	/// </remarks>
	public class InteractiveContainer : GuiWidget
	{
		private const double MarginUnits = 10;
		private const double SpacingUnits = 32;

		private readonly DemoTheme demoTheme;
		private bool pressed;
		private bool hovered;
		private int count;

		public InteractiveContainer(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;
			ThemeConfig theme = demoTheme.Theme;
			double buttonSize = DemoText.Points(13);

			this.ResetButton = new ThemedTextButton("Reset", theme, buttonSize) { Name = "Interactive Container Reset" };
			this.ResetButton.Click += (s, e) => this.Count = 0;
			this.PlusButton = new ThemedTextButton("+ 100", theme, buttonSize) { Name = "Interactive Container Plus 100" };
			this.PlusButton.Click += (s, e) => this.Count += 100;
			this.CountText = new TextWidget("0", pointSize: DemoText.Points(32), textColor: theme.TextColor)
			{
				Name = "Interactive Container Count",
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Absolute,
				AutoExpandBoundsToText = true,
			};

			// "+ 100" follows Reset with agg-gui's 8 unit gap, on a row at the bottom margin.
			double m = MarginUnits * DeviceScale;
			double spacing = SpacingUnits * DeviceScale;
			this.PlusButton.Margin = new BorderDouble(left: 8);
			var buttonRow = new FlowLayoutWidget
			{
				HAnchor = HAnchor.Fit | HAnchor.Left,
				VAnchor = VAnchor.Fit | VAnchor.Bottom,
				Margin = new BorderDouble(MarginUnits, MarginUnits, 0, 0),
			};
			buttonRow.AddChild(this.ResetButton);
			buttonRow.AddChild(this.PlusButton);
			this.AddChild(buttonRow);
			this.AddChild(this.CountText);

			// Margin, buttons, spacing, number, spacing, margin - bottom up, as agg-gui lays it out.
			double buttonHeight = Math.Max(this.ResetButton.Height, this.PlusButton.Height);
			this.CountText.OriginRelativeParent = new Vector2(0, m + buttonHeight + spacing);
			this.Height = m + buttonHeight + spacing + this.CountText.Height + spacing + m;
		}

		/// <summary>Background clicks counted so far; Reset zeroes it and "+ 100" adds 100.</summary>
		public int Count
		{
			get => this.count;
			set
			{
				this.count = value;
				this.CountText.Text = value.ToString();
				this.Invalidate();
			}
		}

		public ThemedTextButton ResetButton { get; }

		public ThemedTextButton PlusButton { get; }

		public TextWidget CountText { get; }

		/// <summary>Whether the frame is lit: the mouse is over the background, not over a nested button
		/// (egui's response.hovered, which is false over a child).</summary>
		public bool Hovered => this.hovered;

		/// <summary>Whether a background press is held.</summary>
		public bool Pressed => this.pressed;

		public override void OnDraw(Graphics2D graphics2D)
		{
			// egui's Frame::canvas under style.interact: a translucent fill that lights on hover and takes the
			// pressed accent while held, and an accent outline while either.
			ThemeConfig theme = this.demoTheme.Theme;
			DemoPalette palette = this.demoTheme.Palette;
			Color fill = this.pressed
				? theme.PrimaryAccentColor.AdjustLightness(.8).ToColor()
				: this.hovered ? palette.WidgetBackground.AdjustLightness(1.2).ToColor() : palette.WidgetBackground;
			double radius = 6 * DeviceScale;
			RectangleDouble r = this.LocalBounds;
			graphics2D.Render(new RoundedRect(r, radius), fill.WithAlpha(77));
			var outline = new RoundedRect(r.Left + .5, r.Bottom + .5, r.Right - .5, r.Top - .5, radius);
			graphics2D.Render(new Stroke(outline, 1), this.hovered || this.pressed ? theme.PrimaryAccentColor : palette.WidgetStroke);
			base.OnDraw(graphics2D);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);
			this.SetHovered(this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y) && !this.OverButton(mouseEvent.Position));
		}

		public override void OnMouseLeave(MouseEventArgs mouseEvent)
		{
			base.OnMouseLeave(mouseEvent);
			this.SetHovered(false);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			// A press on a nested button is that button's alone; anywhere else in the frame counts.
			if (mouseEvent.Button == MouseButtons.Left && !this.OverButton(mouseEvent.Position))
			{
				this.pressed = true;
				this.Invalidate();
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			bool wasPressed = this.pressed;
			this.pressed = false;
			base.OnMouseUp(mouseEvent);
			if (wasPressed)
			{
				if (this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
				{
					this.Count++;
				}

				this.Invalidate();
			}
		}

		/// <summary>Pushes the current theme into the widgets that copied their colours when they were built.</summary>
		public void Recolor()
		{
			ThemeConfig theme = this.demoTheme.Theme;
			this.CountText.TextColor = theme.TextColor;
			foreach (ThemedTextButton button in new[] { this.ResetButton, this.PlusButton })
			{
				this.demoTheme.StyleButton(button);
			}

			this.Invalidate();
		}

		/// <summary>Whether <paramref name="position"/>, in this widget's space, is over Reset or "+ 100" (agg-gui's
		/// point_over_child).</summary>
		private bool OverButton(Vector2 position)
		{
			foreach (GuiWidget button in new GuiWidget[] { this.ResetButton, this.PlusButton })
			{
				if (button.TransformToParentSpace(this, button.LocalBounds).Contains(position))
				{
					return true;
				}
			}

			return false;
		}

		private void SetHovered(bool value)
		{
			if (this.hovered != value)
			{
				this.hovered = value;
				this.Invalidate();
			}
		}
	}
}
