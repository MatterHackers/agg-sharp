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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's "Painting" window (animation/painting.rs painting): the stroke editor row (Stroke: width, five
	/// colour swatches, a separator and Clear Painting), the "Paint with your mouse/touch!" hint and the canvas,
	/// on the panel fill.
	/// </summary>
	public class PaintingWindow : FlowLayoutWidget
	{
		// painting.rs's palette: egui's default green, red, blue, yellow and near-white.
		private static readonly Color[] Palette =
		{
			PaintingCanvas.DefaultStrokeColor,
			new Color(200, 50, 50),
			new Color(50, 110, 220),
			new Color(240, 200, 40),
			new Color(240, 240, 240),
		};

		public PaintingWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(6);
			this.BackgroundColor = demoTheme.Palette.PanelFill;

			var kit = new MiscDemoKit(demoTheme);
			this.Canvas = new PaintingCanvas(demoTheme)
			{
				Name = "Painting Canvas",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};

			FlowLayoutWidget row = kit.Row(8);
			TextWidget strokeLabel = kit.Label("Stroke:");
			strokeLabel.Margin = new BorderDouble(right: 8);
			row.AddChild(strokeLabel);

			this.WidthDrag = new DragValue(this.Canvas.StrokeWidth, 1, 20, kit.Theme)
			{
				Name = "Painting Stroke Width",
				Decimals = 0,
				Step = 1,
				Margin = new BorderDouble(right: 8),
			};
			this.WidthDrag.Width = Math.Max(this.WidthDrag.Width, 70 * DeviceScale);
			this.WidthDrag.ValueChanged += (s, e) =>
			{
				this.Canvas.StrokeWidth = this.WidthDrag.Value;
				this.Canvas.Invalidate();
			};
			row.AddChild(this.WidthDrag);

			var swatches = new List<PaintingSwatch>();
			for (int i = 0; i < Palette.Length; i++)
			{
				var swatch = new PaintingSwatch(Palette[i], this.Canvas, demoTheme)
				{
					Name = $"Painting Swatch {i}",
					Margin = new BorderDouble(right: 8),
				};
				swatches.Add(swatch);
				row.AddChild(swatch);
			}

			this.Swatches = swatches;

			var separator = new GuiWidget(1 * DeviceScale, 20 * DeviceScale)
			{
				BackgroundColor = demoTheme.Palette.Separator,
				Margin = new BorderDouble(right: 8),
			};
			row.AddChild(separator);

			this.ClearButton = kit.Button("Painting Clear", "Clear Painting");
			this.ClearButton.Click += (s, e) => this.Canvas.Clear();
			row.AddChild(this.ClearButton);
			this.AddChild(row);

			TextWidget hint = kit.Label("Paint with your mouse/touch!");
			hint.Margin = new BorderDouble(0, 6, 0, 3);
			this.AddChild(hint);
			this.AddChild(this.Canvas);

			void Recolor(object sender, EventArgs e)
			{
				this.BackgroundColor = demoTheme.Palette.PanelFill;
				separator.BackgroundColor = demoTheme.Palette.Separator;
				kit.Recolor();
			}

			demoTheme.ThemeChanged += Recolor;
			this.Closed += (s, e) => demoTheme.ThemeChanged -= Recolor;
		}

		/// <summary>The freehand canvas.</summary>
		public PaintingCanvas Canvas { get; }

		/// <summary>The stroke width, 1 to 20.</summary>
		public DragValue WidthDrag { get; }

		/// <summary>The five stroke-colour swatches, the default green first.</summary>
		public IReadOnlyList<PaintingSwatch> Swatches { get; }

		/// <summary>Removes every stroke.</summary>
		public ThemedTextButton ClearButton { get; }
	}
}
