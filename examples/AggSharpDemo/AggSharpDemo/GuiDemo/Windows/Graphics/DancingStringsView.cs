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
using System.Diagnostics;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's DancingStrings (animation/dancing.rs, egui's dancing_strings.rs): three standing-wave harmonics,
	/// modes 2, 3 and 5, each <c>amp = sin(time * 1.5 * mode) / mode</c> times <c>sin(t * pi * mode)</c> across the
	/// width, stroked <c>10 / mode</c> thick in a translucent text tone - or, Colored, fading from the trans-flag
	/// teal at the centre to its pink at the edges.
	/// </summary>
	/// <remarks>
	/// Animates as the 3D Animation window's bar grid does: each draw asks for one more frame on the next idle
	/// through <see cref="UiThread"/>, and the request lapses once the view is closed or hidden.
	/// </remarks>
	public class DancingStringsView : GuiWidget
	{
		/// <summary>The segments each string is drawn with (dancing.rs's N).</summary>
		public const int Segments = 120;

		private const double Speed = 1.5;

		private static readonly int[] Modes = { 2, 3, 5 };

		private static readonly Color CenterColor = new Color(0x5B, 0xCE, 0xFA);

		private static readonly Color OuterColor = new Color(0xF5, 0xA9, 0xB8);

		private readonly Stopwatch clock = Stopwatch.StartNew();

		private readonly DemoTheme demoTheme;

		public DancingStringsView(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
		}

		/// <summary>Draw the strings in the centre-to-edge gradient rather than the text tone.</summary>
		public bool Colored { get; set; }

		/// <summary>When set, the strings show this many seconds in and do not advance - for tests and stills.
		/// Null (the default) follows the clock.</summary>
		public double? FixedTimeSeconds { get; set; }

		/// <summary>True while a next frame has been asked for and not yet delivered.</summary>
		public bool FrameRequested { get; private set; }

		/// <summary>Whether a requested frame should still be drawn: the view is open and visible on screen.</summary>
		public bool ShouldAnimate => !this.HasBeenClosed && this.FixedTimeSeconds == null && this.ActuallyVisibleOnScreen();

		/// <summary>String <paramref name="mode"/>'s point <paramref name="i"/> of <see cref="Segments"/> at
		/// <paramref name="time"/> seconds, in widget pixels (Y-up; y = 0 is the middle line's -1 edge).</summary>
		public Vector2 PointAt(int mode, int i, double time)
		{
			double t = i / (double)Segments;
			double amplitude = Math.Sin(time * Speed * mode) / mode;
			double y = amplitude * Math.Sin(t * Math.PI * mode);
			return new Vector2(t * this.Width, (1 - y) * 0.5 * this.Height);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			DemoPalette palette = this.demoTheme.Palette;
			double s = DeviceScale;

			// egui's Frame::canvas: the background with a thin border.
			graphics2D.FillRectangle(this.LocalBounds, palette.BackgroundColor);
			graphics2D.Rectangle(this.LocalBounds, palette.WidgetStroke, s);

			double time = this.FixedTimeSeconds ?? this.clock.Elapsed.TotalSeconds;
			Color baseColor = palette.IsDark ? new Color(255, 255, 255, 196) : new Color(0, 0, 0, 240);
			foreach (int mode in Modes)
			{
				double thickness = 10.0 / mode * s;
				if (this.Colored)
				{
					// One segment per colour, picked by its midpoint's distance from the centre.
					Vector2 previous = this.PointAt(mode, 0, time);
					for (int i = 1; i <= Segments; i++)
					{
						Vector2 next = this.PointAt(mode, i, time);
						double distance = Math.Abs(((previous.X + next.X) * 0.5 / Math.Max(this.Width, 1) * 2) - 1);
						graphics2D.Line(previous, next, CenterColor.gradient(OuterColor, distance), thickness);
						previous = next;
					}
				}
				else
				{
					var path = new VertexStorage();
					path.MoveTo(this.PointAt(mode, 0, time));
					for (int i = 1; i <= Segments; i++)
					{
						path.LineTo(this.PointAt(mode, i, time));
					}

					graphics2D.Render(new Stroke(path, thickness), baseColor);
				}
			}

			this.RequestNextFrame();
			base.OnDraw(graphics2D);
		}

		/// <summary>Asks for one more frame on the next idle, unless one is already pending or time is frozen.</summary>
		private void RequestNextFrame()
		{
			if (this.FrameRequested || this.FixedTimeSeconds != null)
			{
				return;
			}

			this.FrameRequested = true;
			UiThread.RunOnIdle(() =>
			{
				this.FrameRequested = false;
				if (this.ShouldAnimate)
				{
					this.Invalidate();
				}
			});
		}
	}
}
