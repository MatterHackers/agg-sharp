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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// One sidebar group's header row, after agg-gui's widgets/collapsing_header.rs: a full-width band tinted with
	/// the text colour (deeper on hover), a 1px separator along its top, a vector open/closed triangle and the
	/// group name. Clicking it raises <see cref="GuiWidget.Click"/>; the sidebar flips the group's collapsed state.
	/// </summary>
	/// <remarks>
	/// The triangle is a path rather than a ▼/► glyph: the demo's default Nunito has neither, so the glyph drew
	/// as a missing-glyph mark. Both agg-gui and agg-sharp are y-up, so collapsing_header.rs's coordinates are
	/// used unflipped (the open triangle's apex is below its base).
	/// </remarks>
	public class SidebarGroupHeader : GuiWidget
	{
		/// <summary>HEADER_H: the row height, in design units.</summary>
		public const double HeaderHeight = 22;

		/// <summary>INDENT: where the triangle starts.</summary>
		public const double TriangleX = 12;

		/// <summary>TRIANGLE_SIZE.</summary>
		public const double TriangleSize = 6;

		/// <summary>INDENT + TRIANGLE_SIZE * 2 + 4: where the name starts.</summary>
		public const double LabelX = TriangleX + TriangleSize * 2 + 4;

		/// <summary>The band's alpha over the text colour, resting and hovered.</summary>
		public const double BandAlpha = 0.06;

		public const double HoverBandAlpha = 0.10;

		private readonly TextWidget label;

		private bool isOpen = true;

		private bool hovered;

		public SidebarGroupHeader(string group)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;
			this.Height = HeaderHeight * DeviceScale;

			// CollapsingHeader's label is 13px
			this.label = new TextWidget(group, pointSize: DemoText.Points(13))
			{
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(LabelX, 0, 0, 0),
				Selectable = false,
			};
			this.AddChild(this.label);
		}

		/// <summary>Whether the group is expanded; the triangle points down when it is.</summary>
		public bool IsOpen
		{
			get => this.isOpen;
			set
			{
				if (value != this.isOpen)
				{
					this.isOpen = value;
					this.Invalidate();
				}
			}
		}

		public Color TextColor
		{
			get => this.label.TextColor;
			set => this.label.TextColor = value;
		}

		/// <summary>The triangle's fill (agg-gui's text_dim).</summary>
		public Color TriangleColor { get; set; }

		/// <summary>The top line's colour (agg-gui's separator).</summary>
		public Color SeparatorColor { get; set; }

		/// <summary>The band's fill: the text colour at <see cref="BandAlpha"/>, or <see cref="HoverBandAlpha"/>
		/// under the mouse.</summary>
		public Color BandColor => this.TextColor.WithAlpha(this.hovered ? HoverBandAlpha : BandAlpha);

		/// <summary>The triangle in this widget's coordinates, as collapsing_header.rs builds it for the current
		/// <see cref="IsOpen"/>.</summary>
		public VertexStorage TrianglePath()
		{
			double s = DeviceScale;
			double tx = TriangleX * s;
			double cy = this.LocalBounds.Center.Y;
			double ts = TriangleSize * 0.5 * s;
			var path = new VertexStorage();
			if (this.isOpen)
			{
				path.MoveTo(tx, cy + ts * 0.5);
				path.LineTo(tx + ts * 2, cy + ts * 0.5);
				path.LineTo(tx + ts, cy - ts * 0.8);
			}
			else
			{
				path.MoveTo(tx, cy + ts);
				path.LineTo(tx, cy - ts);
				path.LineTo(tx + ts * 1.6, cy);
			}

			path.ClosePolygon();
			return path;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			RectangleDouble bounds = this.LocalBounds;
			double line = DeviceScale;

			// The band stops a line short of the top so the separator stays crisp
			graphics2D.FillRectangle(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top - line, this.BandColor);
			graphics2D.FillRectangle(bounds.Left, bounds.Top - line, bounds.Right, bounds.Top, this.SeparatorColor);
			graphics2D.Render(this.TrianglePath(), this.TriangleColor);

			base.OnDraw(graphics2D);
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
	}
}
