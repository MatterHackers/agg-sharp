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

using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's PaintCanvas (animation/painting.rs, egui's Painting): each left-button drag adds a freehand
	/// stroke. Points are stored normalised (egui's RectTransform over square_proportions), so the painting
	/// rescales with the window. Every stroke is drawn with the one shared width and colour, as in agg-gui.
	/// </summary>
	public class PaintingCanvas : GuiWidget
	{
		/// <summary>egui's default stroke colour.</summary>
		public static readonly Color DefaultStrokeColor = new Color(25, 200, 100);

		private readonly DemoTheme demoTheme;

		private bool painting;

		public PaintingCanvas(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
		}

		/// <summary>The strokes, each a list of normalised points.</summary>
		public List<List<Vector2>> Strokes { get; } = new List<List<Vector2>>();

		/// <summary>The stroke width in logical pixels (egui's default 1).</summary>
		public double StrokeWidth { get; set; } = 1;

		/// <summary>The colour every stroke draws in.</summary>
		public Color StrokeColor { get; set; } = DefaultStrokeColor;

		/// <summary>
		/// egui's Vec2::square_proportions of a <paramref name="width"/> x <paramref name="height"/> canvas: the
		/// normalised extent, which depends only on the aspect ratio so a stored point keeps its fractional place.
		/// </summary>
		public static Vector2 SquareProportions(double width, double height)
		{
			if (width <= 0 || height <= 0)
			{
				return new Vector2(1, 1);
			}

			return width > height ? new Vector2(width / height, 1) : new Vector2(1, height / width);
		}

		/// <summary>A normalised point in canvas pixels.</summary>
		public static Vector2 ToScreen(Vector2 normalized, double width, double height)
		{
			Vector2 extent = SquareProportions(width, height);
			return new Vector2(normalized.X / extent.X * width, normalized.Y / extent.Y * height);
		}

		/// <summary>A canvas pixel in normalised coordinates.</summary>
		public static Vector2 FromScreen(Vector2 position, double width, double height)
		{
			Vector2 extent = SquareProportions(width, height);
			return new Vector2(position.X / width * extent.X, position.Y / height * extent.Y);
		}

		/// <summary>Removes every stroke (the Clear Painting button).</summary>
		public void Clear()
		{
			this.Strokes.Clear();
			this.painting = false;
			this.Invalidate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double s = DeviceScale;
			DemoPalette palette = this.demoTheme.Palette;
			double w = this.Width;
			double h = this.Height;
			graphics2D.FillRectangle(this.LocalBounds, palette.WidgetBackground);
			graphics2D.Render(new Stroke(new RoundedRect(.5, .5, w - .5, h - .5, 0), s), palette.WidgetStroke);

			double width = System.Math.Max(this.StrokeWidth, .5) * s;
			foreach (List<Vector2> stroke in this.Strokes)
			{
				if (stroke.Count < 2)
				{
					continue;
				}

				var path = new VertexStorage();
				path.MoveTo(ToScreen(stroke[0], w, h));
				for (int i = 1; i < stroke.Count; i++)
				{
					path.LineTo(ToScreen(stroke[i], w, h));
				}

				graphics2D.Render(new Stroke(path, width), this.StrokeColor);
			}

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				this.painting = true;
				this.Strokes.Add(new List<Vector2> { FromScreen(mouseEvent.Position, this.Width, this.Height) });
				this.Invalidate();
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (this.painting && this.Strokes.Count > 0)
			{
				Vector2 next = FromScreen(mouseEvent.Position, this.Width, this.Height);
				List<Vector2> stroke = this.Strokes[this.Strokes.Count - 1];

				// Skip duplicate consecutive points, like egui.
				if (stroke[stroke.Count - 1] != next)
				{
					stroke.Add(next);
					this.Invalidate();
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				this.painting = false;
			}

			base.OnMouseUp(mouseEvent);
		}
	}
}
