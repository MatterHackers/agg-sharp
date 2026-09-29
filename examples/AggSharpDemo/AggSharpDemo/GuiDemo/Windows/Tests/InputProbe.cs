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
using System.Collections.Generic;
using System.Diagnostics;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>
	/// One probe area of the Input Test (agg-gui's ProbeWidget in tests/basic/input_probe/mod.rs): a box that
	/// feeds its mouse events to an <see cref="InteractionClassifier"/> of its <see cref="ProbeKind"/> and draws
	/// the classified interactions newest first, repeats coalesced with an ×N counter.
	/// </summary>
	public class InputProbe : GuiWidget
	{
		/// <summary>agg-gui's PROBE_HEADER_H: the title, profile and contains-pointer lines above the history.</summary>
		public const double HeaderHeight = 64;

		/// <summary>agg-gui's PROBE_LINE_H.</summary>
		public const double LineHeight = 16;

		private readonly DemoTheme demoTheme;
		private readonly Stopwatch clock = Stopwatch.StartNew();

		public InputProbe(DemoTheme demoTheme, ProbeKind kind)
		{
			this.demoTheme = demoTheme;
			this.Kind = kind;
			this.Classifier = new InteractionClassifier(kind);
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
		}

		public ProbeKind Kind { get; }

		public InteractionClassifier Classifier { get; private set; }

		/// <summary>The classified interactions, newest first. agg-gui caps this at 200 rows; it shares the 1000 of <see cref="EventHistory"/>.</summary>
		public EventHistory History { get; private set; } = new EventHistory();

		/// <summary>Whether the pointer is over the probe right now, shown in the header and never logged.</summary>
		public bool ContainsPointer { get; private set; }

		/// <summary>Whether hover enter/leave go into the history (the window's "Include hover events" checkbox).</summary>
		public bool IncludeHover { get; set; }

		private DemoPalette Palette => this.demoTheme.Palette;

		// agg-gui sizes are pixel ems; this turns one into DrawString's points.
		private double FontScale => DemoText.Points(1);

		/// <summary>Forgets every gesture and the whole history (the window's Clear button).</summary>
		public void Clear()
		{
			this.Classifier = new InteractionClassifier(this.Kind);
			this.History = new EventHistory();
			this.ContainsPointer = false;
			this.Invalidate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			DemoPalette palette = this.Palette;
			RectangleDouble bounds = this.LocalBounds;
			double s = DeviceScale;
			double h = bounds.Top;
			graphics2D.FillRectangle(bounds, palette.WidgetBackground);

			// A live contains-pointer highlight in the accent colour, so the hover probe visibly reacts even though
			// it handles nothing.
			if (this.ContainsPointer)
			{
				graphics2D.Rectangle(bounds.Left + s, bounds.Bottom + s, bounds.Right - s, bounds.Top - s, this.demoTheme.Theme.PrimaryAccentColor, 2 * s);
			}
			else
			{
				graphics2D.Rectangle(bounds.Left + (0.5 * s), bounds.Bottom + (0.5 * s), bounds.Right - (0.5 * s), bounds.Top - (0.5 * s), palette.WidgetStroke, s);
			}

			graphics2D.DrawString(InteractionClassifier.Title(this.Kind), 8 * s, h - (18 * s), 12.5 * this.FontScale, color: palette.TextColor);
			graphics2D.DrawString(InteractionClassifier.Profile(this.Kind), 8 * s, h - (33 * s), 9.5 * this.FontScale, color: palette.TextDim);
			graphics2D.DrawString("contains pointer: " + (this.ContainsPointer ? "yes" : "no"), 8 * s, h - (48 * s), 9.5 * this.FontScale, color: palette.TextDim);

			double headerY = h - (HeaderHeight * s);
			graphics2D.Line(0, headerY, bounds.Right, headerY, palette.Separator, s);

			double fontSize = 11 * this.FontScale;
			IReadOnlyList<EventHistoryEntry> entries = this.History.Entries;
			if (entries.Count == 0)
			{
				graphics2D.DrawString("(interact here)", 8 * s, headerY - (16 * s), fontSize, color: palette.TextDim);
			}

			for (int i = 0; i < entries.Count; i++)
			{
				double y = headerY - ((i + 1) * LineHeight * s);
				if (y < 0)
				{
					break;
				}

				EventHistoryEntry entry = entries[i];
				graphics2D.DrawString(entry.Full, 8 * s, y + (2 * s), fontSize, color: palette.TextColor);
				if (entry.Count >= 2)
				{
					double width = new TypeFacePrinter(entry.Full, fontSize).LocalBounds.Width;
					graphics2D.DrawString(" ×" + entry.Count, (8 * s) + width + (2 * s), y + (2 * s), fontSize, color: palette.TextDim);
				}
			}

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			this.Log(this.Classifier.OnDown(mouseEvent.Button, mouseEvent.Position, this.NowMs));
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			// While a button is held the probe keeps getting moves even outside its bounds, as agg-gui's capture.
			this.Moved(mouseEvent.Position, this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y));
			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			this.Log(this.Classifier.OnUp(mouseEvent.Button, mouseEvent.Position, this.NowMs));

			// A release outside ends the capture, and no further move comes here to report the leave.
			if (!this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				this.Moved(mouseEvent.Position, false);
			}

			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			this.Moved(mouseEvent?.Position ?? Vector2.Zero, true);
			base.OnMouseEnterBounds(mouseEvent);
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			// The pointer is gone, so no move will come to say so: agg-gui gets that move through its Scene.
			this.Moved(mouseEvent?.Position ?? Vector2.Zero, false);
			base.OnMouseLeaveBounds(mouseEvent);
		}

		private double NowMs => this.clock.Elapsed.TotalMilliseconds;

		private void Moved(Vector2 position, bool inside)
		{
			IReadOnlyList<Interaction> interactions = this.Classifier.OnMove(position, inside, this.NowMs);
			bool changed = this.ContainsPointer != inside;
			this.ContainsPointer = inside;
			this.Log(interactions);
			if (changed)
			{
				this.Invalidate();
			}
		}

		private void Log(IReadOnlyList<Interaction> interactions)
		{
			bool logged = false;
			foreach (Interaction interaction in interactions)
			{
				if (interaction.IsHover && !this.IncludeHover)
				{
					continue;
				}

				(string summary, string full) = InteractionClassifier.Describe(interaction);
				this.History.Add(summary, full);
				logged = true;
			}

			if (logged)
			{
				this.Invalidate();
			}
		}
	}
}
