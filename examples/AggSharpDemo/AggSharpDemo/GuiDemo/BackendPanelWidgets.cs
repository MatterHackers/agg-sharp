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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// A one-line label whose text is read each time it is drawn - agg-gui's unbuffered labels for the backend
	/// panel's live values (screen size, mean CPU, FPS).
	/// </summary>
	/// <remarks>
	/// Drawn rather than a <see cref="TextWidget"/> on purpose: setting a TextWidget's text invalidates it, so a
	/// value that changes every frame would keep asking for the next frame and turn Reactive into Continuous.
	/// This shows whatever is current whenever something else repaints, and asks for nothing.
	/// </remarks>
	public class LiveText : GuiWidget
	{
		private readonly Func<string> text;

		public LiveText(Func<string> text, double pointSize = 9)
		{
			this.text = text;
			this.PointSize = pointSize;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;
			this.Height = Math.Ceiling(pointSize * 1.8 * DeviceScale);
			this.Selectable = false;
		}

		public double PointSize { get; }

		public Color TextColor { get; set; } = Color.Black;

		/// <summary>What it shows now.</summary>
		public string CurrentText => this.text() ?? string.Empty;

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.DrawString(this.CurrentText, 0, this.Height / 2, this.PointSize * DeviceScale, Justification.Left, Baseline.BoundsCenter, this.TextColor);
			base.OnDraw(graphics2D);
		}
	}

	/// <summary>
	/// agg-gui's frame-time sparkline (performance.rs paint_sparkline): the <see cref="FrameHistory"/> as a line,
	/// fast frames high, over a rounded track, with an orange line at 16.7 ms (60 fps). The y range never
	/// shrinks below that line, so a run of fast frames does not zoom in on noise.
	/// </summary>
	public class FrameSparkline : GuiWidget
	{
		/// <summary>One frame at 60 fps.</summary>
		public const double ReferenceMs = 16.7;

		private readonly FrameHistory history;

		public FrameSparkline(FrameHistory history, double height = 48)
		{
			this.history = history;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;
			this.Height = height * DeviceScale;
			this.Selectable = false;
		}

		public Color TrackColor { get; set; } = new Color(0, 0, 0, 30);

		public Color LineColor { get; set; } = Color.Blue;

		public override void OnDraw(Graphics2D graphics2D)
		{
			RectangleDouble bounds = this.LocalBounds;
			graphics2D.Render(new RoundedRect(bounds, 4 * DeviceScale), this.TrackColor);

			double[] samples = this.history.Samples.ToArray();
			if (samples.Length >= 2)
			{
				double maxMs = Math.Max(samples.Max(), ReferenceMs);
				double inset = 2 * DeviceScale;
				double Y(double ms) => bounds.Bottom + inset + (1 - ms / maxMs) * (bounds.Height - 2 * inset);

				var line = new VertexStorage();
				for (int i = 0; i < samples.Length; i++)
				{
					double x = bounds.Left + i * bounds.Width / (samples.Length - 1);
					if (i == 0)
					{
						line.MoveTo(x, Y(samples[i]));
					}
					else
					{
						line.LineTo(x, Y(samples[i]));
					}
				}

				graphics2D.Render(new Stroke(line, 1.5 * DeviceScale), this.LineColor);
				graphics2D.Line(bounds.Left, Y(ReferenceMs), bounds.Right, Y(ReferenceMs), new Color(255, 153, 0, 178), DeviceScale);
			}

			base.OnDraw(graphics2D);
		}
	}

	/// <summary>
	/// backend_panel.rs's TogglePill: a full-width subtle button that shows one on/off setting, accent filled and
	/// white lettered while on - the sidebar rows' look, rather than a checkbox beside the Mode segments.
	/// </summary>
	/// <remarks>
	/// Clicking raises Click only; the owner flips its setting and sets <see cref="IsOn"/> back, so the pill
	/// always shows the setting however it was changed.
	/// </remarks>
	public class TogglePill : IconTextButton
	{
		/// <summary>backend_panel/widgets.rs's H: the pill's height.</summary>
		public const double PillHeight = 26;

		private readonly DemoTheme demoTheme;

		private bool isOn;

		public TogglePill(string text, string iconGlyph, DemoTheme demoTheme)
			: base(text, iconGlyph, demoTheme.Theme, 9)
		{
			this.demoTheme = demoTheme;
			this.HAnchor = HAnchor.Stretch;
			this.TextHAnchor = HAnchor.Left;
			this.Height = PillHeight;

			// widgets.rs's SIDE_GUTTER (12) either side, 2 above and below
			this.Margin = new BorderDouble(12, 2);
			this.TextPadding = new BorderDouble(8, 0);
			this.ApplyTheme();
		}

		public bool IsOn
		{
			get => this.isOn;
			set
			{
				if (value != this.isOn)
				{
					this.isOn = value;
					this.ApplyTheme();
				}
			}
		}

		/// <summary>Recolours the pill from the theme and <see cref="IsOn"/>; the owner calls it on a theme change.</summary>
		public void ApplyTheme()
		{
			Color accent = DemoTheme.ColorOf(this.demoTheme.Accent);
			this.BackgroundColor = this.isOn ? accent : this.demoTheme.Palette.WidgetBackground;
			this.TextColor = this.isOn ? Color.White : this.demoTheme.Palette.TextColor;
			this.HoverColor = this.isOn ? accent : this.demoTheme.Theme.SlightShade;
			this.Invalidate();
		}
	}
}
