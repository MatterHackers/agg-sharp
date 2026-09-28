//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2026 Lars Brubaker
//                  larsbrubaker@gmail.com
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------

using System;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The title bar chevron of a <see cref="WindowWidget.Collapsible"/> window: points down while the window is
	/// open and right while it is folded, and a click toggles <see cref="WindowWidget.Collapsed"/>.
	/// </summary>
	internal class CollapseChevron : GuiWidget
	{
		private const double SizeUnits = 16;

		private readonly WindowWidget window;

		private readonly Func<Color> color;

		public CollapseChevron(WindowWidget window, Func<Color> color)
			: base(SizeUnits * DeviceScale, SizeUnits * DeviceScale)
		{
			this.window = window;
			this.color = color;
			Margin = new BorderDouble(0, 0, 4, 0);
			Cursor = Cursors.Hand;
			ToolTipText = "Collapse";
		}

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			window.Collapsed = !window.Collapsed;
			base.OnClick(mouseEvent);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double half = 3.5 * DeviceScale;
			double cx = Width / 2;
			double cy = Height / 2;
			var arrow = new VertexStorage();
			if (window.Collapsed)
			{
				arrow.MoveTo(cx - half / 2, cy + half);
				arrow.LineTo(cx + half, cy);
				arrow.LineTo(cx - half / 2, cy - half);
			}
			else
			{
				arrow.MoveTo(cx - half, cy + half / 2);
				arrow.LineTo(cx + half, cy + half / 2);
				arrow.LineTo(cx, cy - half);
			}

			arrow.ClosePolygon();
			graphics2D.Render(arrow, color());
			base.OnDraw(graphics2D);
		}
	}
}
