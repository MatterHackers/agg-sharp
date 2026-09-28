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

// Ported from agg-gui's snap/overlay.rs: same colours, 1 unit lines on pixel centres, 4 unit end ticks on
// the spacing markers (agg-gui draws them solid, not dashed). agg-gui's overlay reads a thread-local guide
// buffer; here whoever computes the guides (SnapCoordinator) hands them over through Guides.

using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Draws a drag's <see cref="SnapGuide"/>s over its siblings: cyan alignment lines and pink spacing
	/// markers. Purely visual - it never takes the mouse - and draws nothing while <see cref="Guides"/> is
	/// empty, which is always the case between drags.
	/// </summary>
	/// <remarks>
	/// Meant to fill the container the dragged rectangles live in, so the guides (in that container's
	/// coordinates) land where they belong; any offset of the overlay within the container is undone when it
	/// draws. It paints after its siblings (<see cref="GuiWidget.DrawOnTopOfSiblings"/>) without changing the
	/// container's child order, so raising a window never covers the guides.
	/// </remarks>
	public class SnapGuideOverlay : GuiWidget
	{
		/// <summary>agg-gui's alignment_color: a cyan that reads as a guide without competing with content.</summary>
		public static readonly Color AlignmentColor = new ColorF(0.15, 0.70, 0.95, 0.95).ToColor();

		/// <summary>agg-gui's spacing_color: pink, so an equal-spacing snap is told apart from an alignment at a glance.</summary>
		public static readonly Color SpacingColor = new ColorF(0.95, 0.35, 0.55, 0.95).ToColor();

		// agg-gui's line width and tick half length, both in its logical pixels - design units here.
		private const double LineWidth = 1;

		private const double TickHalfLength = 4;

		private IReadOnlyList<SnapGuide> guides = Array.Empty<SnapGuide>();

		public SnapGuideOverlay()
		{
			Name = "Snap Guide Overlay";
			HAnchor = HAnchor.Stretch;
			VAnchor = VAnchor.Stretch;
			Selectable = false;
			DrawOnTopOfSiblings = true;
		}

		/// <summary>
		/// The guides to draw, in the parent's coordinates. Setting it redraws; set it empty (or null) to clear.
		/// </summary>
		public IReadOnlyList<SnapGuide> Guides
		{
			get => guides;
			set
			{
				var newGuides = value ?? Array.Empty<SnapGuide>();
				if (guides.Count == 0 && newGuides.Count == 0)
				{
					return;
				}

				guides = newGuides;
				Invalidate();
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			if (guides.Count == 0)
			{
				return;
			}

			// the guides are in the parent's space; this widget's origin may not be the parent's
			double ox = -OriginRelativeParent.X;
			double oy = -OriginRelativeParent.Y;
			double tick = TickHalfLength * DeviceScale;
			double width = LineWidth * DeviceScale;

			// An odd device width is centred on a pixel centre (agg-gui's round() + 0.5) so it lands on one
			// crisp column; an even one on a pixel edge, for the same reason.
			double centre = Math.Round(width) % 2 == 1 ? 0.5 : 0;

			foreach (SnapGuide guide in guides)
			{
				switch (guide.Kind)
				{
					case SnapGuideKind.VLine:
						{
							double x = Math.Round(guide.At + ox) + centre;
							graphics2D.Line(x, guide.From + oy, x, guide.To + oy, AlignmentColor, width);
						}

						break;

					case SnapGuideKind.HLine:
						{
							double y = Math.Round(guide.At + oy) + centre;
							graphics2D.Line(guide.From + ox, y, guide.To + ox, y, AlignmentColor, width);
						}

						break;

					case SnapGuideKind.HSpacing:
						{
							double y = Math.Round(guide.At + oy) + centre;
							double x0 = guide.From + ox;
							double x1 = guide.To + ox;
							graphics2D.Line(x0, y, x1, y, SpacingColor, width);

							// end ticks, so the gap reads as a dimension and not a stray line
							graphics2D.Line(x0, y - tick, x0, y + tick, SpacingColor, width);
							graphics2D.Line(x1, y - tick, x1, y + tick, SpacingColor, width);
						}

						break;

					case SnapGuideKind.VSpacing:
						{
							double x = Math.Round(guide.At + ox) + centre;
							double y0 = guide.From + oy;
							double y1 = guide.To + oy;
							graphics2D.Line(x, y0, x, y1, SpacingColor, width);
							graphics2D.Line(x - tick, y0, x + tick, y0, SpacingColor, width);
							graphics2D.Line(x - tick, y1, x + tick, y1, SpacingColor, width);
						}

						break;
				}
			}
		}
	}
}
