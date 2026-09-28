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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>
	/// The SVG Test window's scrolling body, agg-gui's SvgProgressBody: one row per sample, its name with a
	/// pass/fail mark and score over reference.png and agg-sharp's render side by side, both at the reference's
	/// pixel size times <see cref="Zoom"/>. Holding the mouse down on a render shows its difference from the
	/// reference instead.
	/// </summary>
	public class SvgTestCanvas : GuiWidget
	{
		public const int ColumnCount = 2;
		private const double Pad = 8;
		private const double Gap = 8;
		private const double TitleHeight = 22;
		private const double PanelInset = 8;
		private const double HeaderHeight = 20;
		private static readonly string[] ColumnTitles = { "reference.png", "agg-sharp render (hold for diff)" };

		private readonly DemoTheme demoTheme;
		private double zoom = .5;

		public SvgTestCanvas(DemoTheme demoTheme, IReadOnlyList<SvgTestSample> samples)
		{
			this.demoTheme = demoTheme;
			this.Samples = samples;
			this.UpdateSize();
		}

		public IReadOnlyList<SvgTestSample> Samples { get; }

		/// <summary>The row whose render is held down, and so shows its diff; -1 for none.</summary>
		public int HeldRow { get; private set; } = -1;

		/// <summary>Image pixels per reference pixel (before the device scale); agg-gui opens at 50%.</summary>
		public double Zoom
		{
			get => this.zoom;
			set
			{
				if (value != this.zoom)
				{
					this.zoom = value;
					this.UpdateSize();
					this.Invalidate();
				}
			}
		}

		private double S => DeviceScale;

		/// <summary>The top of the first row, under the column titles.</summary>
		private double ContentTop => this.LocalBounds.Top - (Pad + HeaderHeight) * this.S;

		private double ImageScale => this.zoom * this.S;

		private double MaxImageWidth => this.Samples.Select(s => s.Reference.Width).DefaultIfEmpty(200).Max() * this.ImageScale;

		private double MaxImageHeight => this.Samples.Select(s => s.Reference.Height).DefaultIfEmpty(200).Max() * this.ImageScale;

		private double ColumnWidth => Math.Max(120 * this.S, this.MaxImageWidth + PanelInset * 2 * this.S);

		public double RowHeight => Math.Max(60 * this.S, this.MaxImageHeight + PanelInset * 2 * this.S) + TitleHeight * this.S;

		/// <summary>The panel of <paramref name="column"/> (0 reference, 1 render) in <paramref name="row"/>, in local coordinates.</summary>
		public RectangleDouble PanelBounds(int row, int column)
		{
			double s = this.S;
			double top = this.ContentTop - row * this.RowHeight - TitleHeight * s;
			double left = Pad * s + column * (this.ColumnWidth + Gap * s);
			return new RectangleDouble(left, top - (this.RowHeight - TitleHeight * s) + 4 * s, left + this.ColumnWidth, top);
		}

		/// <summary>The sample's image in its panel, centred at its zoomed size.</summary>
		public RectangleDouble ImageBounds(int row, int column)
		{
			RectangleDouble panel = this.PanelBounds(row, column);
			ImageBuffer reference = this.Samples[row].Reference;
			double w = reference.Width * this.ImageScale;
			double h = reference.Height * this.ImageScale;
			double left = panel.Left + (panel.Width - w) / 2;
			double bottom = panel.Bottom + (panel.Height - h) / 2;
			return new RectangleDouble(left, bottom, left + w, bottom + h);
		}

		/// <summary>The row whose render image holds <paramref name="position"/>, or -1.</summary>
		public int RenderRowAt(Vector2 position)
		{
			for (int row = 0; row < this.Samples.Count; row++)
			{
				if (this.ImageBounds(row, 1).Contains(position))
				{
					return row;
				}
			}

			return -1;
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				this.HeldRow = this.RenderRowAt(mouseEvent.Position);
				this.Invalidate();
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (this.HeldRow != -1)
			{
				this.HeldRow = -1;
				this.Invalidate();
			}

			base.OnMouseUp(mouseEvent);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double s = this.S;
			DemoPalette palette = this.demoTheme.Palette;
			RectangleDouble bounds = this.LocalBounds;
			RectangleDouble clip = ScrollOffsets.LocalClippingRect(graphics2D);
			(int first, int end) = ScrollOffsets.VisibleRows(this.ContentTop, Math.Max(clip.Bottom, bounds.Bottom), Math.Min(clip.Top, bounds.Top), this.RowHeight, this.Samples.Count);
			for (int column = 0; column < ColumnCount; column++)
			{
				double left = this.PanelBounds(0, column).Left + 6 * s;
				graphics2D.DrawString(ColumnTitles[column], left, bounds.Top - (Pad + 14) * s, 10.5 * s, color: palette.TextColor);
			}

			for (int row = first; row < end; row++)
			{
				SvgTestSample sample = this.Samples[row];
				RectangleDouble panel = this.PanelBounds(row, 0);
				double baseline = panel.Top + 7 * s;
				double x = Pad * s + 6 * s;
				var name = new TypeFacePrinter(sample.Name + ".svg", 10 * s);
				graphics2D.DrawString(sample.Name + ".svg", x, baseline, 10 * s, color: palette.TextDim);
				x += name.LocalBounds.Width + 8 * s;

				SvgCompareResult score = sample.Score;
				graphics2D.Circle(x, baseline + 3.5 * s, 4.5 * s, score.Pass ? new Color(51, 191, 64) : new Color(217, 56, 56));
				x += 10 * s;
				string scoreText = (score.Pass ? "pass" : "fail") + $"  {score.Ratio * 100:0.00}% of pixels differ";
				if (sample.RenderError != null)
				{
					scoreText = "fail  " + sample.RenderError;
				}

				graphics2D.DrawString(scoreText, x, baseline, 10 * s, color: palette.TextDim);

				for (int column = 0; column < ColumnCount; column++)
				{
					RectangleDouble panelBounds = this.PanelBounds(row, column);
					graphics2D.FillRectangle(panelBounds, palette.WidgetBackground);
					graphics2D.Rectangle(panelBounds, palette.WidgetStroke, s);
					ImageBuffer image = column == 0 ? sample.Reference : this.HeldRow == row ? sample.Diff : sample.Render;
					if (image != null)
					{
						RectangleDouble imageBounds = this.ImageBounds(row, column);
						graphics2D.Render(image, imageBounds.Left, imageBounds.Bottom, imageBounds.Width, imageBounds.Height);
					}
				}
			}

			base.OnDraw(graphics2D);
		}

		private void UpdateSize()
		{
			double s = this.S;
			double width = Pad * 2 * s + ColumnCount * this.ColumnWidth + (ColumnCount - 1) * Gap * s;
			double height = (Pad * 2 + HeaderHeight) * s + this.Samples.Count * this.RowHeight;
			this.LocalBounds = new RectangleDouble(0, 0, width, height);
		}
	}
}
