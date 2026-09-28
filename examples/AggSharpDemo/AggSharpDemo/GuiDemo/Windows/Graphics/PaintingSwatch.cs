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

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's ColorSwatch (animation/painting.rs): a 20 x 20 colour square that sets the painting's stroke
	/// colour when pressed. The selected swatch has a thicker accent border.
	/// </summary>
	public class PaintingSwatch : GuiWidget
	{
		private const double Size = 20;

		private readonly DemoTheme demoTheme;

		private readonly PaintingCanvas canvas;

		public PaintingSwatch(Color color, PaintingCanvas canvas, DemoTheme demoTheme)
			: base(Size * DeviceScale, Size * DeviceScale)
		{
			this.Color = color;
			this.canvas = canvas;
			this.demoTheme = demoTheme;
		}

		/// <summary>The colour this swatch picks.</summary>
		public Color Color { get; }

		/// <summary>True when this swatch's colour is the stroke colour.</summary>
		public bool Selected => this.canvas.StrokeColor == this.Color;

		public override void OnDraw(Graphics2D graphics2D)
		{
			double s = DeviceScale;
			graphics2D.FillRectangle(this.LocalBounds, this.Color);
			bool selected = this.Selected;
			Color border = selected ? DemoTheme.ColorOf(this.demoTheme.Accent) : this.demoTheme.Palette.WidgetStroke;
			double width = (selected ? 2.5 : 1) * s;
			double inset = width / 2;
			graphics2D.Render(new Stroke(new RoundedRect(inset, inset, this.Width - inset, this.Height - inset, 0), width), border);
			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				this.canvas.StrokeColor = this.Color;

				// Every swatch's border and the painting change colour.
				this.Parent?.Invalidate();
				this.canvas.Invalidate();
			}

			base.OnMouseDown(mouseEvent);
		}
	}
}
