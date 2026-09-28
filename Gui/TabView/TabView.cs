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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A strip of equal-width tabs over one page at a time - agg-gui's TabView (agg-gui/src/widgets/tab_view.rs).
	/// The selected tab's label is in <see cref="AccentColor"/> under an accent bar along the strip's top; a hovered
	/// tab gets <see cref="HoverColor"/>. Each tab is a real child widget named by <see cref="AddTab"/> so automation
	/// can click it; only the selected page is visible.
	/// </summary>
	/// <remarks>
	/// <see cref="TabControl"/> bakes its colours into its tabs when they are built and sizes each tab to its text;
	/// this one reads its colours when it draws, so a theme change is a property set and an Invalidate.
	/// </remarks>
	public class TabView : GuiWidget
	{
		private readonly List<TabViewHeader> headers = new List<TabViewHeader>();
		private readonly List<GuiWidget> pages = new List<GuiWidget>();
		private readonly GuiWidget strip;
		private readonly GuiWidget pageArea;
		private int selectedIndex = -1;

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

		/// <summary>The labels' size in points at design scale.</summary>
		public double PointSize { get; set; } = 12;

		public Color BarColor { get; set; } = new Color(248, 248, 248);

		public Color SeparatorColor { get; set; } = new Color(0, 0, 0, 40);

		public Color AccentColor { get; set; } = new Color(0, 110, 220);

		/// <summary>A hovered tab's label.</summary>
		public Color TextColor { get; set; } = Color.Black;

		/// <summary>An idle tab's label.</summary>
		public Color TextDimColor { get; set; } = new Color(110, 110, 110);

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

		/// <summary>Splits the strip into equal widths, one per tab, as agg-gui does.</summary>
		private void LayoutHeaders()
		{
			if (this.headers.Count == 0)
			{
				return;
			}

			double width = this.strip.Width / this.headers.Count;
			for (int i = 0; i < this.headers.Count; i++)
			{
				this.headers[i].LocalBounds = new RectangleDouble(0, 0, width, this.strip.Height);
				this.headers[i].OriginRelativeParent = new VectorMath.Vector2(i * width, 0);
			}
		}

		/// <summary>The bar behind the tabs, with the separator along its bottom.</summary>
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
				graphics2D.FillRectangle(bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom + DeviceScale, this.owner.SeparatorColor);
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
				if (this.hovered && !active)
				{
					graphics2D.FillRectangle(bounds, this.owner.HoverColor);
				}

				if (active)
				{
					graphics2D.FillRectangle(bounds.Left, bounds.Top - 2.5 * DeviceScale, bounds.Right, bounds.Top, this.owner.AccentColor);
				}

				Color color = active ? this.owner.AccentColor : this.hovered ? this.owner.TextColor : this.owner.TextDimColor;
				// Equal widths can be narrower than a long label; it ends in an ellipsis rather than running into its
				// neighbour.
				double pointSize = this.owner.PointSize * DeviceScale;
				string label = VirtualTable.ClipTextToWidth(this.Text, pointSize, Math.Max(0, bounds.Width - 8 * DeviceScale));
				graphics2D.DrawString(label, bounds.Center.X, bounds.Center.Y, pointSize, Justification.Center, Baseline.BoundsCenter, color);
				base.OnDraw(graphics2D);
			}
		}
	}
}
