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
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// agg-gui's BoxPainter (misc_demos.rs "Test box rendering"): up to eight rounded boxes of a set size,
	/// corner radius and stroke, wrapped into rows across the width. Its height follows the boxes so they
	/// are never clipped (egui lays them out with horizontal_wrapped). Lengths are design units.
	/// </summary>
	public class MiscBoxPainter : GuiWidget
	{
		private const double Gap = 8;

		private readonly ThemeConfig theme;
		private double boxWidth = 64;
		private double boxHeight = 32;
		private double cornerRadius = 5;
		private double strokeWidth = 2;
		private int boxCount = 1;

		public MiscBoxPainter(ThemeConfig theme)
		{
			this.theme = theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;
			this.UpdateHeight();
		}

		public double BoxWidth { get => this.boxWidth; set => this.Change(ref this.boxWidth, value); }

		public double BoxHeight { get => this.boxHeight; set => this.Change(ref this.boxHeight, value); }

		public double CornerRadius { get => this.cornerRadius; set => this.Change(ref this.cornerRadius, value); }

		public double StrokeWidth { get => this.strokeWidth; set => this.Change(ref this.strokeWidth, value); }

		public int BoxCount
		{
			get => this.boxCount;
			set
			{
				if (this.boxCount != value)
				{
					this.boxCount = value;
					this.UpdateHeight();
					this.Invalidate();
				}
			}
		}

		/// <summary>How many boxes fit on a row <paramref name="width"/> design units wide (at least one).</summary>
		public static int BoxesPerRow(double width, double boxWidth)
		{
			return Math.Max(1, (int)Math.Floor((width + Gap) / (Math.Max(1, boxWidth) + Gap)));
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			base.OnBoundsChanged(e);
			this.UpdateHeight();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double scale = DeviceScale;
			double width = Math.Max(1, this.boxWidth);
			double height = Math.Max(1, this.boxHeight);
			int perRow = BoxesPerRow(this.Width / scale, width);
			for (int i = 0; i < this.boxCount; i++)
			{
				int column = i % perRow;
				int row = i / perRow;
				double x = column * (width + Gap);

				// y is up: the first row sits at the top.
				double y = this.Height / scale - Gap - (row + 1) * height - row * Gap;
				var box = new RoundedRect(x * scale, y * scale, (x + width) * scale, (y + height) * scale, this.cornerRadius * scale);
				graphics2D.Render(box, this.theme.TextColor.WithAlpha(89));
				if (this.strokeWidth > 0)
				{
					graphics2D.Render(new Stroke(box, this.strokeWidth * scale), this.theme.TextColor);
				}
			}

			base.OnDraw(graphics2D);
		}

		private void Change(ref double field, double value)
		{
			if (field != value)
			{
				field = value;
				this.UpdateHeight();
				this.Invalidate();
			}
		}

		private void UpdateHeight()
		{
			double scale = DeviceScale;
			int perRow = BoxesPerRow(this.Width / scale, this.boxWidth);
			int rows = this.boxCount == 0 ? 0 : (this.boxCount + perRow - 1) / perRow;
			double height = Math.Max(8, rows * (Math.Max(1, this.boxHeight) + Gap) + Gap) * scale;
			if (this.Height != height)
			{
				this.Height = height;
			}
		}
	}

	/// <summary>agg-gui's ManyCirclesWidget: 100 filled circles of growing radius in rows of 20.</summary>
	public class MiscManyCircles : GuiWidget
	{
		private const int Columns = 20;
		private const double Cell = 18;
		private readonly ThemeConfig theme;

		public MiscManyCircles(ThemeConfig theme)
		{
			this.theme = theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;
			this.Height = (100 / Columns * Cell + 4) * DeviceScale;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double scale = DeviceScale;
			for (int i = 0; i < 100; i++)
			{
				double radius = Math.Min(i * .5 + .5, Cell * .45);
				double x = (i % Columns) * Cell + Cell * .5;
				double y = this.Height / scale - (i / Columns) * Cell - Cell * .5;
				graphics2D.Circle(x * scale, y * scale, radius * scale, this.theme.TextColor);
			}

			base.OnDraw(graphics2D);
		}
	}

	/// <summary>agg-gui's SwatchRow: a rounded colour swatch beside the colour's name.</summary>
	public class MiscSwatchRow : FlowLayoutWidget
	{
		public MiscSwatchRow(string name, Color color, TextWidget label)
		{
			this.Name = "Misc Swatch " + name;
			this.HAnchor = HAnchor.Left | HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;
			this.Color = color;
			this.AddChild(new GuiWidget(28 * DeviceScale, 22 * DeviceScale) { Selectable = false });
			label.VAnchor = VAnchor.Center;
			this.AddChild(label);
		}

		public Color Color { get; }

		public override void OnDraw(Graphics2D graphics2D)
		{
			double scale = DeviceScale;
			double bottom = (this.Height / scale - 16) / 2;
			graphics2D.Render(new RoundedRect(0, bottom * scale, 20 * scale, (bottom + 16) * scale, 3 * scale), this.Color);
			base.OnDraw(graphics2D);
		}
	}

	/// <summary>egui's "paint your own small icons": a 16 unit circle cut by a diameter and two radii.</summary>
	public class MiscPaintedIcon : GuiWidget
	{
		public MiscPaintedIcon()
			: base(16 * DeviceScale, 16 * DeviceScale)
		{
			this.Name = "Misc Painted Icon";
			this.VAnchor = VAnchor.Center;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double scale = DeviceScale;
			double center = 8 * scale;
			double radius = 7 * scale;
			var color = new ColorF(.5, .5, .5).ToColor();
			graphics2D.Render(new Stroke(new Ellipse(center, center, radius, radius), scale), color);

			var lines = new VertexStorage();
			lines.MoveTo(center, center - radius);
			lines.LineTo(center, center + radius);
			foreach (double angle in new[] { Math.PI * 2 / 8, Math.PI * 2 * 3 / 8 })
			{
				lines.MoveTo(center, center);
				lines.LineTo(center + radius * Math.Cos(angle), center + radius * Math.Sin(angle));
			}

			graphics2D.Render(new Stroke(lines, scale), color);
			base.OnDraw(graphics2D);
		}
	}
}
