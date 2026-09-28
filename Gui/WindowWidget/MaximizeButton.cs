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
	/// The title bar button of a <see cref="WindowWidget.Maximizable"/> window, beside the close button as in
	/// agg-gui: one square while the window is at its own size, two overlapping squares (restore) while it
	/// fills its parent. A click toggles <see cref="WindowWidget.Maximized"/>.
	/// </summary>
	internal class MaximizeButton : GuiWidget
	{
		private const double SizeUnits = 16;

		private readonly WindowWidget window;

		private readonly Func<Color> color;

		public MaximizeButton(WindowWidget window, Func<Color> color)
			: base(SizeUnits * DeviceScale, SizeUnits * DeviceScale)
		{
			this.window = window;
			this.color = color;
			Margin = new BorderDouble(0, 0, 4, 0);
			Cursor = Cursors.Hand;
			ToolTipText = "Maximize";
		}

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			window.Maximized = !window.Maximized;
			base.OnClick(mouseEvent);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double half = 4 * DeviceScale;
			double cx = Width / 2;
			double cy = Height / 2;
			double line = Math.Max(1, DeviceScale);
			if (window.Maximized)
			{
				// restore: a back square peeking out above and right of the front one
				double step = 2 * DeviceScale;
				DrawSquare(graphics2D, new RectangleDouble(cx - half + step, cy - half + step, cx + half, cy + half), line);
				DrawSquare(graphics2D, new RectangleDouble(cx - half, cy - half, cx + half - step, cy + half - step), line);
			}
			else
			{
				DrawSquare(graphics2D, new RectangleDouble(cx - half, cy - half, cx + half, cy + half), line);
			}

			base.OnDraw(graphics2D);
		}

		private void DrawSquare(Graphics2D graphics2D, RectangleDouble rect, double line)
		{
			graphics2D.Rectangle(rect, color(), line);
		}
	}
}
