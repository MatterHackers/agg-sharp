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
	/// The draggable split between the inspector's tree and its properties pane (agg-gui's h-split). The widget is
	/// agg-gui's <see cref="LogicalGap"/>-unit gap between the panes, with the split line <see cref="LogicalLine"/>
	/// above its bottom under a 4-unit tinted bar. A press within <see cref="LogicalGrab"/> of the line grabs it, which
	/// reaches past the gap into both panes (the properties pane gives up its top edge for it). Dragging moves the line
	/// to the pointer.
	/// </summary>
	public class InspectorSplitBar : GuiWidget
	{
		/// <summary>agg-gui's SPLIT_HIT: how far either side of the line a press grabs it, in logical units.</summary>
		public const double LogicalGrab = 5;

		/// <summary>agg-gui's gap between the properties pane's top (line - 2) and the tree's bottom (line + 4).</summary>
		public const double LogicalGap = 6;

		/// <summary>How far the split line sits above the gap's bottom.</summary>
		public const double LogicalLine = 2;

		private readonly InspectorPanel panel;

		private bool dragging;

		public InspectorSplitBar(InspectorPanel panel)
		{
			this.panel = panel;
			this.Name = "Inspector Split";
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;
			this.Height = LogicalGap * DeviceScale;
			this.Cursor = Cursors.SizeNS;
		}

		/// <summary>The grab zone: <see cref="LogicalGrab"/> either side of the line, past the gap's own bounds.</summary>
		public override bool PositionWithinLocalBounds(double x, double y)
		{
			double line = LogicalLine * DeviceScale;
			double grab = LogicalGrab * DeviceScale;
			return x >= 0 && x < this.Width && y >= line - grab && y <= line + grab;
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			this.dragging = mouseEvent.Button == MouseButtons.Left;
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (this.dragging && this.MouseCaptured)
			{
				// the line follows the pointer: its height above the panel's bottom is the new props_h
				double yInPanel = this.Position.Y + mouseEvent.Y - this.panel.Padding.Bottom;
				this.panel.PropertiesHeight = this.panel.ClampPropertiesHeight(yInPanel / DeviceScale);
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			this.dragging = false;
			base.OnMouseUp(mouseEvent);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double scale = DeviceScale;
			double center = LogicalLine * scale;
			Color background = this.panel.Style.Background;
			bool dark = 0.299 * background.red + 0.587 * background.green + 0.114 * background.blue < 128;
			graphics2D.FillRectangle(0, center - 2 * scale, this.Width, center + 2 * scale, dark ? new Color(255, 255, 255, 26) : new Color(0, 0, 0, 26));
			graphics2D.FillRectangle(0, center - scale / 2, this.Width, center + scale / 2, this.panel.Style.Separator);
			base.OnDraw(graphics2D);
		}
	}
}
