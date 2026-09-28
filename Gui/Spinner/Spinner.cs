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
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>Spinner diameters, matching agg-gui's (SwiftUI's) control sizes.</summary>
	public enum SpinnerSize
	{
		/// <summary>16 design units - fits on a line of text.</summary>
		Small,

		/// <summary>32 design units.</summary>
		Regular,
	}

	/// <summary>
	/// An indeterminate busy indicator - the agg-gui Spinner (the macOS spinning progress look). Twelve
	/// radial spokes, the head at full ink and those behind it fading; the head steps one spoke every
	/// <see cref="StepMilliseconds"/>, so the ring turns about once a second. Show it while something of
	/// unknown length runs and hide it when that is done.
	/// </summary>
	/// <remarks>
	/// Nothing blocks and no timer runs on its own: each draw works out the head from the clock and asks
	/// for one more draw a step later. A spinner that is hidden or closed is not drawn, so it stops asking
	/// and costs nothing - the same re-arm-on-paint scheme agg-gui uses.
	/// </remarks>
	public class Spinner : GuiWidget
	{
		/// <summary>Number of spokes around the ring.</summary>
		public const int SpokeCount = 12;

		/// <summary>Time the head stays on one spoke.</summary>
		public const int StepMilliseconds = 80;

		/// <summary>Alpha of the oldest spoke, as a fraction of the head's.</summary>
		public const double TailAlpha = .12;

		private readonly long startMs;
		private bool redrawPending;

		private readonly ThemeConfig theme;

		/// <summary>Creates a spinner whose spokes default to the theme's text colour.</summary>
		public Spinner(ThemeConfig theme, SpinnerSize size = SpinnerSize.Regular)
		{
			Size = size;
			this.theme = theme;
			startMs = UiThread.CurrentTimerMs;
			Selectable = false;
			HAnchor = HAnchor.Absolute;
			VAnchor = VAnchor.Absolute;

			var diameter = Diameter(size) * DeviceScale;
			LocalBounds = new RectangleDouble(0, 0, diameter, diameter);
		}

		/// <summary>The preset this spinner was made at.</summary>
		public SpinnerSize Size { get; }

		/// <summary>
		/// Colour of the head spoke (the others are it faded), or null - the default - for the theme's text
		/// colour, read at every draw so a live theme switch reaches the spinner.
		/// </summary>
		public Color? SpokeColor { get; set; }

		/// <summary>The spoke currently at full ink, from how long this spinner has existed.</summary>
		public int HeadSpoke => HeadSpokeAt(UiThread.CurrentTimerMs - startMs);

		/// <summary>Outer diameter of a <paramref name="size"/> spinner in design units.</summary>
		public static double Diameter(SpinnerSize size) => size == SpinnerSize.Small ? 16 : 32;

		/// <summary>
		/// The head spoke <paramref name="elapsedMilliseconds"/> after the spinner started: one step per
		/// <see cref="StepMilliseconds"/>, wrapping after <see cref="SpokeCount"/>.
		/// </summary>
		public static int HeadSpokeAt(long elapsedMilliseconds)
		{
			var steps = Math.Max(0, elapsedMilliseconds) / StepMilliseconds;
			return (int)(steps % SpokeCount);
		}

		/// <summary>
		/// Alpha fraction of spoke <paramref name="spoke"/> when the head is at <paramref name="head"/>: 1 at
		/// the head, falling linearly to <see cref="TailAlpha"/> at the spoke just ahead of it.
		/// </summary>
		public static double SpokeAlpha(int spoke, int head)
		{
			var behind = (head - spoke + SpokeCount) % SpokeCount;
			var t = behind / (double)(SpokeCount - 1);
			return 1 - t * (1 - TailAlpha);
		}

		/// <summary>
		/// Direction of spoke <paramref name="spoke"/> from the centre: spoke 0 at twelve o'clock, the rest
		/// clockwise (y up).
		/// </summary>
		public static Vector2 SpokeDirection(int spoke)
		{
			var angle = Math.PI / 2 - spoke * 2 * Math.PI / SpokeCount;
			return new Vector2(Math.Cos(angle), Math.Sin(angle));
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var bounds = LocalBounds;
			var diameter = Diameter(Size) * DeviceScale;
			var center = bounds.Center;
			var outerRadius = diameter / 2;
			var innerRadius = diameter * .27;
			var lineWidth = Math.Max(1, diameter * .085);
			var head = HeadSpoke;
			var ink = SpokeColor ?? theme.TextColor;

			for (int i = 0; i < SpokeCount; i++)
			{
				var direction = SpokeDirection(i);
				var spoke = new VertexStorage();
				spoke.MoveTo(center + direction * innerRadius);
				spoke.LineTo(center + direction * (outerRadius - lineWidth / 2));
				var stroke = new Stroke(spoke, lineWidth)
				{
					LineCap = LineCap.Round,
				};
				var alpha = (int)Math.Round(ink.alpha * SpokeAlpha(i, head));
				graphics2D.Render(stroke, ink.WithAlpha(alpha));
			}

			base.OnDraw(graphics2D);

			// Ask for the next step. One request at a time, so extra draws (hover, a parent's repaint)
			// do not pile up timers.
			if (!redrawPending)
			{
				redrawPending = true;
				UiThread.RunOnIdle(() =>
				{
					redrawPending = false;
					if (!HasBeenClosed)
					{
						Invalidate();
					}
				}, StepMilliseconds / 1000.0);
			}
		}
	}
}
