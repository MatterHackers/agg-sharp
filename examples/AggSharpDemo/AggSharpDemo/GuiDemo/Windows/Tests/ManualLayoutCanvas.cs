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
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>
	/// agg-gui's AbsolutePlace (tests/basic/absolute_place.rs): a bounded canvas that puts its one child at a rect
	/// given from its top-left corner, so the Manual Layout Test's sliders read as a user expects (y grows down).
	/// The child stays a real widget - clickable or editable wherever it lands.
	/// </summary>
	public class ManualLayoutCanvas : GuiWidget
	{
		private readonly DemoTheme demoTheme;

		private double placedX;
		private double placedY;
		private double placedWidth;
		private double placedHeight;

		public ManualLayoutCanvas(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
		}

		/// <summary>The placed widget, or null before <see cref="SetPlaced"/>.</summary>
		public GuiWidget Placed { get; private set; }

		/// <summary>
		/// The rect, in a canvas <paramref name="containerHeight"/> tall, of a child <paramref name="yFromTop"/>
		/// below its top: GuiWidget is Y-up, so the child's bottom edge is the height less the offset and the
		/// child's own height (absolute_place.rs placed_child_rect).
		/// </summary>
		public static RectangleDouble PlacedChildRect(double containerHeight, double x, double yFromTop, double width, double height)
		{
			double bottom = containerHeight - yFromTop - height;
			return new RectangleDouble(x, bottom, x + width, bottom + height);
		}

		/// <summary>Swaps in <paramref name="placed"/>, closing the one before - agg-gui's Rebuilder on a type change.</summary>
		public void SetPlaced(GuiWidget placed)
		{
			this.Placed?.Close();
			this.Placed = placed;
			placed.HAnchor = HAnchor.Absolute;
			placed.VAnchor = VAnchor.Absolute;
			this.AddChild(placed);
			this.Place();
		}

		/// <summary>Moves and sizes the placed widget: a top-left offset and a size, in design units.</summary>
		public void SetRect(double x, double yFromTop, double width, double height)
		{
			this.placedX = x;
			this.placedY = yFromTop;
			this.placedWidth = width;
			this.placedHeight = height;
			this.Place();
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			this.Place();
			base.OnBoundsChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			// The canvas background and a one pixel outline, so the placement area is visible.
			RectangleDouble bounds = this.LocalBounds;
			graphics2D.FillRectangle(bounds, this.demoTheme.Palette.BackgroundColor);
			var outline = new RectangleDouble(bounds.Left + .5, bounds.Bottom + .5, bounds.Right - .5, bounds.Top - .5);
			graphics2D.Rectangle(outline, this.demoTheme.Palette.WidgetStroke);
			base.OnDraw(graphics2D);
		}

		private void Place()
		{
			if (this.Placed == null)
			{
				return;
			}

			double s = DeviceScale;
			RectangleDouble rect = PlacedChildRect(this.LocalBounds.Height, this.placedX * s, this.placedY * s, this.placedWidth * s, this.placedHeight * s);
			this.Placed.LocalBounds = new RectangleDouble(0, 0, rect.Width, rect.Height);
			this.Placed.OriginRelativeParent = new Vector2(this.LocalBounds.Left + rect.Left, this.LocalBounds.Bottom + rect.Bottom);
		}
	}
}
