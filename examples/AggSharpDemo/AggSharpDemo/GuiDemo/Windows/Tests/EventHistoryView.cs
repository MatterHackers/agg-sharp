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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>
	/// The recording box of the Input Event History window (agg-gui's EventHistoryWidget): every mouse, key and
	/// wheel event that reaches it goes into an <see cref="EventHistory"/>, drawn newest at the top. It is as tall
	/// as its rows or its scroll viewport, whichever is more, so the whole box records even when nearly empty.
	/// </summary>
	public class EventHistoryView : GuiWidget
	{
		/// <summary>agg-gui's EVENT_LINE_H.</summary>
		public const double LineHeight = 18;

		private readonly DemoTheme demoTheme;
		private double viewportHeight;

		public EventHistoryView(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;
			this.Selectable = true;
			this.Height = this.ContentHeight;
		}

		public EventHistory History { get; } = new EventHistory();

		/// <summary>Whether mouse moves are recorded (the window's checkbox; off by default, as in egui).</summary>
		public bool IncludeMovements { get; set; }

		private double ContentHeight => Math.Max((Math.Max(1, this.History.Entries.Count) * LineHeight + 12) * DeviceScale, this.viewportHeight);

		private double FontSize => DemoText.Points(11);

		/// <summary>Sets the height of the scroll viewport the view must at least fill.</summary>
		public void SetViewportHeight(double height)
		{
			this.viewportHeight = height;
			this.Height = this.ContentHeight;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			DemoPalette palette = this.demoTheme.Palette;
			RectangleDouble bounds = this.LocalBounds;
			graphics2D.FillRectangle(bounds, palette.WidgetBackground);

			double s = DeviceScale;
			if (this.History.Entries.Count == 0)
			{
				graphics2D.DrawString("Interact inside the box to record events…", 8 * s, bounds.Top - (22 * s), this.FontSize, color: palette.TextDim);
			}
			else
			{
				for (int i = 0; i < this.History.Entries.Count; i++)
				{
					double y = bounds.Top - ((i + 1) * LineHeight * s);
					if (y + (LineHeight * s) < 0)
					{
						break;
					}

					EventHistoryEntry entry = this.History.Entries[i];
					graphics2D.DrawString(entry.Summary, 6 * s, y + (4 * s), this.FontSize, color: palette.TextColor);
					if (entry.Count >= 2)
					{
						double width = new TypeFacePrinter(entry.Summary, this.FontSize).LocalBounds.Width;
						graphics2D.DrawString(" ×" + entry.Count, (6 * s) + width + (2 * s), y + (4 * s), this.FontSize, color: palette.TextDim);
					}
				}
			}

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			// Keys go to the focused widget, so a click in the box is what lets it record the keyboard.
			this.Focus();
			this.Record($"MouseDown {mouseEvent.Button}", $"MouseDown {mouseEvent.Button} ({mouseEvent.X:0}, {mouseEvent.Y:0})");
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			this.Record($"MouseUp {mouseEvent.Button}", $"MouseUp {mouseEvent.Button}");
			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (this.IncludeMovements && this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				this.Record("MouseMove", $"MouseMove ({mouseEvent.X:0}, {mouseEvent.Y:0})");
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			// Recorded but left unhandled, so the scroll view around the box still scrolls.
			this.Record("MouseWheel", $"MouseWheel {mouseEvent.WheelDelta:0.0}");
			base.OnMouseWheel(mouseEvent);
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			this.Record($"KeyDown {keyEvent.KeyCode}", $"KeyDown {keyEvent.KeyCode}");
			keyEvent.Handled = true;
			base.OnKeyDown(keyEvent);
		}

		public override void OnKeyUp(KeyEventArgs keyEvent)
		{
			this.Record($"KeyUp {keyEvent.KeyCode}", $"KeyUp {keyEvent.KeyCode}");
			keyEvent.Handled = true;
			base.OnKeyUp(keyEvent);
		}

		private void Record(string summary, string full)
		{
			this.History.Add(summary, full);
			this.Height = this.ContentHeight;
			this.Invalidate();
		}
	}
}
