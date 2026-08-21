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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg.VertexSource;
using System;

namespace MatterHackers.Agg.UI
{
	public class ProgressBar : GuiWidget
	{
		private double ratioComplete;

		public ProgressBar()
		{
			this.BorderColor = Color.Black;
		}

		public ProgressBar(double width, double height)
			: base(width, height)
		{
		}

		public event EventHandler ProgressChanged;

		public Color FillColor { get; set; }

		public int PercentComplete
		{
			get => (int)(ratioComplete * 100 + .5);

			// RatioComplete already does the changed-check and the event, and it is the only place the
			// ratio should be written. Guarding on the percent here instead compared the incoming value
			// against the square of the current ratio and fired ProgressChanged for changes that were not.
			set => RatioComplete = value / 100.0;
		}

		public double RatioComplete
		{
			get => ratioComplete;

			set
			{
				if (value != ratioComplete)
				{
					// assign before announcing, or a handler that reads RatioComplete (or PercentComplete)
					// sees the value the bar just moved off of
					ratioComplete = value;
					ProgressChanged?.Invoke(this, null);
					Invalidate();
				}
			}
		}

		/// <summary>
		/// Draws only the progress fill. The background and the outline come from BackgroundColor,
		/// BackgroundRadius, BackgroundOutlineWidth and BorderColor exactly as they do for any other
		/// GuiWidget, and the framework has already painted them through OnDrawBackground by the time
		/// this runs - drawing them again here painted both of them twice.
		/// </summary>
		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			// the fill goes down after the outline, so it has to stop where the background does - inset by
			// the full stroke width - or a bar near 100% paints straight over its own outline ring
			var inset = (BorderColor.Alpha0To255 > 0 && BackgroundOutlineWidth > 0)
				? BackgroundOutlineWidth * DeviceScale
				: 0;

			var bounds = LocalBounds;
			var left = bounds.Left + inset;
			var bottom = bounds.Bottom + inset;
			var top = bounds.Top - inset;

			// Restrict fill to valid values, measured across the room left inside the outline
			var room = Math.Max(0, bounds.Width - inset * 2);
			var fillWidth = Math.Max(0, Math.Min(room, room * RatioComplete));

			if (fillWidth > 0 && top > bottom)
			{
				if (BackgroundRadius == 0)
				{
					graphics2D.FillRectangle(left, bottom, left + fillWidth, top, FillColor);
				}
				else
				{
					// the corners lose the inset the same way GuiWidget.InsetRoundedRect takes it off the
					// background's, so the arcs still fit the box that has to hold them
					var fill = new RoundedRect(left, bottom, left + fillWidth, top, Math.Max(BackgroundRadius.NW - inset, 0));

					// once the fill is narrower than the corner diameter the two bottom arcs (and the two
					// top arcs) sweep past each other and paint a blob well to the right of the progress.
					// RoundedRect leaves radii alone unless asked, so ask.
					fill.normalize_radius();

					graphics2D.Render(fill, FillColor);
				}
			}

			// the legacy sharp rect border for zero-outline bars. RenderBackground draws a border only when
			// the outline width is greater than zero, so nothing else paints this one - and it goes down
			// after the fill (as it always has) so a bar near 100% cannot cover it
			if (BorderColor.Alpha0To255 > 0 && BackgroundOutlineWidth <= 0)
			{
				graphics2D.Rectangle(LocalBounds, BorderColor);
			}
		}
	}
}