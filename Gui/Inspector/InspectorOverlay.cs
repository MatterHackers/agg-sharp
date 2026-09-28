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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Draws the inspector's highlight on top of the whole app, after agg-gui's paint_inspector_overlay
	/// (widgets/inspector/widget_impl.rs): the hovered widget gets Chrome DevTools' three bands - margin (amber),
	/// bounds (blue) and padding (green) - with a crisp outline; the selected widget keeps an outline of its own.
	/// </summary>
	/// <remarks>
	/// It draws from the inspected root's <see cref="GuiWidget.AfterDraw"/>, which runs after every child has
	/// painted, so the highlight sits above whichever window the widget lives in without being a widget itself (and
	/// so without taking the mouse). <see cref="InspectorNode.ScreenBounds"/> is in the root's coordinates, which is
	/// where AfterDraw draws.
	/// </remarks>
	public class InspectorOverlay
	{
		public static readonly Color MarginBand = new ColorF(0.99, 0.61, 0.20, 0.30).ToColor();

		public static readonly Color BoundsBand = new ColorF(0.42, 0.66, 1.0, 0.30).ToColor();

		public static readonly Color PaddingBand = new ColorF(0.55, 0.86, 0.55, 0.35).ToColor();

		public static readonly Color Outline = new ColorF(0.10, 0.45, 0.95, 0.90).ToColor();

		private readonly InspectorModel model;

		private GuiWidget root;

		public InspectorOverlay(InspectorModel model)
		{
			this.model = model;
			model.Changed += this.Model_Changed;
		}

		/// <summary>The widget the overlay draws over; null draws nothing. Moving it repaints both roots.</summary>
		public GuiWidget Root
		{
			get => this.root;
			set
			{
				if (value == this.root)
				{
					return;
				}

				if (this.root != null)
				{
					this.root.AfterDraw -= this.Root_AfterDraw;
					this.root.Invalidate();
				}

				this.root = value;
				if (value != null)
				{
					value.AfterDraw += this.Root_AfterDraw;
					value.Invalidate();
				}
			}
		}

		/// <summary>Draws the highlight for the model's hovered and selected nodes into <paramref name="graphics2D"/>,
		/// which is in the root's coordinates.</summary>
		public void Draw(Graphics2D graphics2D)
		{
			InspectorNode selected = this.model.Selected;
			if (selected != null && selected != this.model.Hovered)
			{
				graphics2D.Rectangle(selected.ScreenBounds, Outline, 2 * GuiWidget.DeviceScale);
			}

			InspectorNode hovered = this.model.Hovered;
			if (hovered == null)
			{
				return;
			}

			RectangleDouble bounds = hovered.ScreenBounds;
			BorderDouble margin = hovered.DeviceMargin;
			if (margin.Left != 0 || margin.Bottom != 0 || margin.Right != 0 || margin.Top != 0)
			{
				// The margin band is the ring between the bounds and the bounds grown by the margin.
				var outer = new RectangleDouble(bounds.Left - margin.Left, bounds.Bottom - margin.Bottom, bounds.Right + margin.Right, bounds.Top + margin.Top);
				FillRing(graphics2D, outer, bounds, MarginBand);
			}

			graphics2D.FillRectangle(bounds, BoundsBand);

			BorderDouble padding = hovered.DevicePadding;
			var inner = new RectangleDouble(bounds.Left + padding.Left, bounds.Bottom + padding.Bottom, bounds.Right - padding.Right, bounds.Top - padding.Top);
			if ((padding.Left != 0 || padding.Bottom != 0 || padding.Right != 0 || padding.Top != 0) && inner.Width > 0 && inner.Height > 0)
			{
				FillRing(graphics2D, bounds, inner, PaddingBand);
			}

			graphics2D.Rectangle(bounds, Outline, GuiWidget.DeviceScale);
		}

		/// <summary>Stops drawing: unhooks from the root and the model.</summary>
		public void Detach()
		{
			this.Root = null;
			this.model.Changed -= this.Model_Changed;
		}

		/// <summary>Fills the area inside <paramref name="outer"/> but outside <paramref name="inner"/> as four strips, so
		/// the translucent band never doubles up over the bounds band.</summary>
		private static void FillRing(Graphics2D graphics2D, RectangleDouble outer, RectangleDouble inner, Color color)
		{
			graphics2D.FillRectangle(outer.Left, outer.Bottom, outer.Right, inner.Bottom, color);
			graphics2D.FillRectangle(outer.Left, inner.Top, outer.Right, outer.Top, color);
			graphics2D.FillRectangle(outer.Left, inner.Bottom, inner.Left, inner.Top, color);
			graphics2D.FillRectangle(inner.Right, inner.Bottom, outer.Right, inner.Top, color);
		}

		private void Root_AfterDraw(object sender, DrawEventArgs e) => this.Draw(e.Graphics2D);

		/// <summary>The highlight lives on the root, so any change to what it shows repaints the root.</summary>
		private void Model_Changed(object sender, EventArgs e) => this.root?.Invalidate();
	}
}
