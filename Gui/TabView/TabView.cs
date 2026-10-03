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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A strip of folder tabs over one page at a time: each tab is sized to its label and they sit left-aligned in a
	/// recessed band (<see cref="BarColor"/>). The selected tab is filled with <see cref="PageColor"/>, rounded along its
	/// top and outlined in <see cref="SeparatorColor"/> on every side but the bottom, so it opens into the page under it;
	/// a hovered tab gets <see cref="HoverColor"/>. Each tab is a real child widget named by <see cref="AddTab"/> so
	/// automation can click it; only the selected page is visible.
	/// </summary>
	/// <remarks>
	/// <see cref="TabControl"/> bakes its colours into its tabs when they are built; this one reads its colours when it
	/// draws, so a theme change is a property set and an Invalidate. TabView paints no page background itself: the
	/// caller sets <see cref="PageColor"/> to whatever its pages paint.
	/// </remarks>
	public class TabView : GuiWidget
	{
		/// <summary>Design units from the strip's left edge to the first tab (and, when the tabs must share the width,
		/// from the last tab to the right edge).</summary>
		public const double TabInset = 8;

		/// <summary>Design units of the band showing above the tabs.</summary>
		public const double TabTopPadding = 8;

		/// <summary>Design units between neighbouring tabs.</summary>
		public const double TabGap = 2;

		/// <summary>Design units on each side of a tab's label.</summary>
		public const double TabLabelPadding = 16;

		/// <summary>The radius, in design units, of a tab's top corners.</summary>
		public const double TabCornerRadius = 7;

		private readonly List<TabViewHeader> headers = new List<TabViewHeader>();
		private readonly List<GuiWidget> pages = new List<GuiWidget>();
		private readonly GuiWidget strip;
		private readonly GuiWidget pageArea;
		private int selectedIndex = -1;
		private double pointSize = 12;

		/// <param name="barHeight">The tab strip's height in design units (agg-gui's 36).</param>
		public TabView(double barHeight = 36)
		{
			this.BarHeight = barHeight;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			this.strip = new TabStrip(this)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Top,
				Height = barHeight * DeviceScale,
			};
			this.pageArea = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(0, 0, 0, barHeight),
			};

			this.AddChild(this.pageArea);
			this.AddChild(this.strip);
			this.strip.BoundsChanged += (s, e) => this.LayoutHeaders();
		}

		public event EventHandler SelectedIndexChanged;

		public double BarHeight { get; }

		/// <summary>The labels' size in points at design scale. Setting it re-sizes the tabs to their labels.</summary>
		public double PointSize
		{
			get => this.pointSize;
			set
			{
				this.pointSize = value;
				this.LayoutHeaders();
				this.Invalidate();
			}
		}

		/// <summary>The recessed band behind the tabs, usually a step darker than <see cref="PageColor"/>.</summary>
		public Color BarColor { get; set; } = new Color(232, 232, 236);

		/// <summary>The colour the pages paint. The selected tab is filled with it so it reads as part of the page.</summary>
		public Color PageColor { get; set; } = Color.White;

		/// <summary>The line along the strip's bottom and the selected tab's outline.</summary>
		public Color SeparatorColor { get; set; } = new Color(0, 0, 0, 40);

		/// <remarks>The folder-tab style does not draw this; it is kept so callers that set it still compile.</remarks>
		public Color AccentColor { get; set; } = new Color(0, 110, 220);

		/// <summary>The selected and the hovered tab's label.</summary>
		public Color TextColor { get; set; } = Color.Black;

		/// <summary>An idle tab's label.</summary>
		public Color TextDimColor { get; set; } = new Color(110, 110, 110);

		/// <summary>The fill behind a hovered tab that is not selected.</summary>
		public Color HoverColor { get; set; } = new Color(0, 0, 0, 20);

		public IReadOnlyList<GuiWidget> Pages => this.pages;

		/// <summary>The tab widgets along the strip, in the order they were added.</summary>
		public IReadOnlyList<GuiWidget> Tabs => this.headers;

		/// <summary>The shown page's index, -1 before any tab is added. Setting it shows that page.</summary>
		public int SelectedIndex
		{
			get => this.selectedIndex;
			set
			{
				if (value < 0 || value >= this.pages.Count || value == this.selectedIndex)
				{
					return;
				}

				this.selectedIndex = value;
				for (int i = 0; i < this.pages.Count; i++)
				{
					this.pages[i].Visible = i == value;
				}

				this.SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
				this.Invalidate();
			}
		}

		public GuiWidget SelectedPage => this.selectedIndex < 0 ? null : this.pages[this.selectedIndex];

		/// <summary>Adds a tab labelled <paramref name="label"/> showing <paramref name="page"/>, named
		/// <paramref name="name"/> (the label when null). The first tab added is selected.</summary>
		/// <returns>The tab widget.</returns>
		public GuiWidget AddTab(string label, GuiWidget page, string name = null)
		{
			var header = new TabViewHeader(this, this.headers.Count, label) { Name = name ?? label };
			this.headers.Add(header);
			this.strip.AddChild(header);

			page.Visible = false;
			this.pages.Add(page);
			this.pageArea.AddChild(page);

			this.LayoutHeaders();
			if (this.selectedIndex < 0)
			{
				this.SelectedIndex = 0;
			}

			return header;
		}

		/// <summary>Sizes each tab to its label plus <see cref="TabLabelPadding"/> a side, left-aligned after
		/// <see cref="TabInset"/>; when they don't all fit, the width inside the insets is split equally and the labels
		/// are clipped with an ellipsis as they draw. Widths are whole pixels; the drawn edges are
		/// snapped to device pixels as they draw, since the strip's own origin can be fractional.</summary>
		private void LayoutHeaders()
		{
			if (this.headers.Count == 0)
			{
				return;
			}

			double inset = TabInset * DeviceScale;
			double gap = TabGap * DeviceScale;
			double tabHeight = Math.Round(Math.Max(0, this.strip.Height - TabTopPadding * DeviceScale));
			double gaps = gap * (this.headers.Count - 1);

			var widths = new double[this.headers.Count];
			double total = gaps;
			for (int i = 0; i < this.headers.Count; i++)
			{
				widths[i] = this.headers[i].TextWidth(this.pointSize * DeviceScale) + 2 * TabLabelPadding * DeviceScale;
				total += widths[i];
			}

			if (total > this.strip.Width - 2 * inset)
			{
				double equal = Math.Max(0, (this.strip.Width - 2 * inset - gaps) / this.headers.Count);
				Array.Fill(widths, equal);
			}

			double x = inset;
			for (int i = 0; i < this.headers.Count; i++)
			{
				double left = Math.Round(x);
				double right = Math.Round(x + widths[i]);
				this.headers[i].LocalBounds = new RectangleDouble(0, 0, right - left, tabHeight);
				this.headers[i].OriginRelativeParent = new VectorMath.Vector2(left, 0);
				x += widths[i] + gap;
			}
		}

		/// <summary>The local coordinate nearest <paramref name="local"/> that lands on a whole device pixel, given the
		/// draw transform's translation <paramref name="offset"/> (widgets draw under a pure translation; DeviceScale is
		/// already in their sizes).</summary>
		private static double Snap(double local, double offset) => Math.Round(local + offset) - offset;

		/// <summary>The first whole device pixel edge at or above <paramref name="local"/>, so a row started there stays
		/// inside the widget.</summary>
		private static double SnapUp(double local, double offset) => Math.Ceiling(local + offset - 1e-6) - offset;

		/// <summary>The recessed band behind the tabs, with the separator along its bottom.</summary>
		private sealed class TabStrip : GuiWidget
		{
			private readonly TabView owner;

			public TabStrip(TabView owner)
			{
				this.owner = owner;
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				RectangleDouble bounds = this.LocalBounds;
				graphics2D.FillRectangle(bounds, this.owner.BarColor);
				// One device pixel, on a whole pixel row: the strip's origin can be fractional (36 units at
				// DeviceScale 1.1 is 39.6 px), so the row is found in device space, not local space.
				double lineBottom = SnapUp(bounds.Bottom, graphics2D.GetTransform().ty);
				graphics2D.FillRectangle(bounds.Left, lineBottom, bounds.Right, lineBottom + 1, this.owner.SeparatorColor);
				// The tabs are children, so the selected one draws over the separator and opens into the page.
				base.OnDraw(graphics2D);
			}
		}

		private sealed class TabViewHeader : GuiWidget
		{
			private readonly TabView owner;
			private readonly int index;
			private bool hovered;

			public TabViewHeader(TabView owner, int index, string label)
			{
				this.owner = owner;
				this.index = index;
				this.Text = label;
				this.HAnchor = HAnchor.Absolute;
				this.VAnchor = VAnchor.Absolute;
			}

			/// <summary>The label's width at <paramref name="pointSize"/>, which is already in pixels.</summary>
			public double TextWidth(double pointSize) => new TypeFacePrinter(this.Text, pointSize).GetSize().X;

			public override void OnMouseDown(MouseEventArgs mouseEvent)
			{
				// Selected on press, as agg-gui's TabView does.
				if (mouseEvent.Button == MouseButtons.Left)
				{
					this.owner.SelectedIndex = this.index;
				}

				base.OnMouseDown(mouseEvent);
			}

			public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
			{
				this.hovered = true;
				this.Invalidate();
				base.OnMouseEnterBounds(mouseEvent);
			}

			public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
			{
				this.hovered = false;
				this.Invalidate();
				base.OnMouseLeaveBounds(mouseEvent);
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				RectangleDouble bounds = this.LocalBounds;
				bool active = this.owner.SelectedIndex == this.index;
				double radius = TabCornerRadius * DeviceScale;

				// The shape's edges land on whole device pixels wherever the strip happens to sit, so the one-pixel
				// outline is one crisp row and column. The bottom snaps the way the strip's separator does, onto the
				// same row.
				Affine transform = graphics2D.GetTransform();
				var shape = new RectangleDouble(
					Snap(bounds.Left, transform.tx),
					SnapUp(bounds.Bottom, transform.ty),
					Snap(bounds.Right, transform.tx),
					Snap(bounds.Top, transform.ty));
				if (active)
				{
					// The outline is the whole shape in the separator colour; the page fill then covers all of it but one
					// pixel on the left, top and right. The fill runs to the widget's bottom (below the snapped one, over
					// the strip's separator line and any part-pixel under it) so the tab has no bottom edge and joins
					// the page.
					graphics2D.Render(TopRounded(shape, radius), this.owner.SeparatorColor);
					var inner = new RectangleDouble(shape.Left + 1, bounds.Bottom, shape.Right - 1, shape.Top - 1);
					graphics2D.Render(TopRounded(inner, Math.Max(0, radius - 1)), this.owner.PageColor);
				}
				else if (this.hovered)
				{
					graphics2D.Render(TopRounded(shape, radius), this.owner.HoverColor);
				}

				Color color = active || this.hovered ? this.owner.TextColor : this.owner.TextDimColor;
				// When the tabs share the width equally a long label can be wider than its tab; it ends in an ellipsis
				// rather than running into its neighbour.
				double pointSize = this.owner.PointSize * DeviceScale;
				string label = VirtualTable.ClipTextToWidth(this.Text, pointSize, Math.Max(0, bounds.Width - 8 * DeviceScale));
				graphics2D.DrawString(label, bounds.Center.X, bounds.Center.Y, pointSize, Justification.Center, Baseline.BoundsCenter, color);
				base.OnDraw(graphics2D);
			}

			/// <summary><paramref name="bounds"/> with its top corners rounded and its bottom corners square.</summary>
			private static RoundedRect TopRounded(RectangleDouble bounds, double radius)
			{
				var shape = new RoundedRect(bounds, 0);
				shape.radius(0, 0, radius, radius);
				return shape;
			}
		}
	}
}
