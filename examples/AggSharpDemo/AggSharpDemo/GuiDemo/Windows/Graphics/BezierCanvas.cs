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
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's BezierCanvas (animation/bezier.rs, egui's PaintBezier): a quadratic or cubic curve whose control
	/// points drag. The closed curve is filled translucent and stroked, its visual bounds boxed, and a thin
	/// polyline joins the active control points. Points are widget pixels (Y-up); agg-gui's logical sizes are
	/// scaled by DeviceScale.
	/// </summary>
	public class BezierCanvas : GuiWidget
	{
		/// <summary>How near (logical pixels) a press must land to grab a point.</summary>
		public const double SnapRadius = 12;

		private const double HandleRadius = 8;

		private const double EndpointRadius = 10;

		private const double CurveWidth = 1.5;

		// egui PaintBezier's defaults; linear_multiply(0.25) makes the fill, aux and box colours ~25% alpha.
		private static readonly Color CurveStroke = new Color(25, 200, 100);

		private static readonly Color FillColor = new Color(50, 100, 150, 64);

		private static readonly Color AuxColor = new Color(255, 0, 0, 64);

		private static readonly Color BoundsColor = new Color(144, 238, 144, 64);

		private readonly DemoTheme demoTheme;

		public BezierCanvas(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			double s = DeviceScale;

			// bezier.rs's start: the curve opens upward in the middle of a 360 x 290 canvas.
			this.Points = new[] { new Vector2(80, 90) * s, new Vector2(140, 210) * s, new Vector2(220, 210) * s, new Vector2(280, 90) * s };
		}

		/// <summary>The four control points in widget pixels; quadratic mode uses the first three.</summary>
		public Vector2[] Points { get; }

		/// <summary>True for the cubic curve (4 points, the default), false for the quadratic (3 points).</summary>
		public bool Cubic { get; set; } = true;

		/// <summary>The index of the point being dragged, or -1.</summary>
		public int Dragging { get; private set; } = -1;

		/// <summary>The number of active control points.</summary>
		public int Degree => this.Cubic ? 4 : 3;

		/// <summary>The nearest active control point within <see cref="SnapRadius"/> of <paramref name="position"/>, or -1.</summary>
		public int Nearest(Vector2 position)
		{
			int best = -1;
			double bestDistance = SnapRadius * DeviceScale;
			for (int i = 0; i < this.Degree; i++)
			{
				double distance = (this.Points[i] - position).Length;
				if (distance <= bestDistance)
				{
					best = i;
					bestDistance = distance;
				}
			}

			return best;
		}

		/// <summary>The curve at <paramref name="t"/> in [0, 1] for the current degree.</summary>
		public Vector2 Evaluate(double t)
		{
			double mt = 1 - t;
			Vector2[] p = this.Points;
			if (!this.Cubic)
			{
				return (mt * mt * p[0]) + (2 * mt * t * p[1]) + (t * t * p[2]);
			}

			return (mt * mt * mt * p[0]) + (3 * mt * mt * t * p[1]) + (3 * mt * t * t * p[2]) + (t * t * t * p[3]);
		}

		/// <summary>The sampled curve's bounds grown by half the stroke width (egui's visual_bounding_rect).</summary>
		public RectangleDouble CurveBounds()
		{
			const int Samples = 64;
			var bounds = RectangleDouble.ZeroIntersection;
			for (int i = 0; i <= Samples; i++)
			{
				bounds.ExpandToInclude(this.Evaluate(i / (double)Samples));
			}

			bounds.Inflate(CurveWidth * DeviceScale * 0.5);
			return bounds;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double s = DeviceScale;
			DemoPalette palette = this.demoTheme.Palette;
			Vector2[] p = this.Points;
			graphics2D.FillRectangle(this.LocalBounds, palette.BackgroundColor);

			// The closed curve: close_path adds the straight run back to P0 for both fill and stroke.
			var curve = new VertexStorage();
			curve.MoveTo(p[0]);
			if (this.Cubic)
			{
				curve.Curve4(p[1], p[2], p[3]);
			}
			else
			{
				curve.Curve3(p[1], p[2]);
			}

			curve.ClosePolygon();
			var flat = new FlattenCurves(curve);
			graphics2D.Render(flat, FillColor);
			graphics2D.Render(new Stroke(flat, CurveWidth * s), CurveStroke);

			// egui's bounding-box stroke defaults to width 0 (hidden until widened in a Colors panel agg-gui
			// omits), so agg-gui draws it at 1 to show the feature.
			graphics2D.Rectangle(this.CurveBounds(), BoundsColor, s);

			var aux = new VertexStorage();
			for (int i = 0; i < this.Degree; i++)
			{
				if (i == 0)
				{
					aux.MoveTo(p[i]);
				}
				else
				{
					aux.LineTo(p[i]);
				}
			}

			graphics2D.Render(new Stroke(aux, s), AuxColor);

			Color accent = DemoTheme.ColorOf(this.demoTheme.Accent);

			// agg-gui's widget_bg_hovered, which DemoPalette does not carry.
			Color hovered = palette.IsDark ? DemoPalette.Rgb(0.28, 0.28, 0.33) : DemoPalette.Rgb(0.92, 0.93, 0.95);
			for (int i = 0; i < this.Degree; i++)
			{
				bool endpoint = i == 0 || i == this.Degree - 1;
				var circle = new Ellipse(p[i], (endpoint ? EndpointRadius : HandleRadius) * s);
				graphics2D.Render(circle, i == this.Dragging ? hovered : accent);
				graphics2D.Render(new Stroke(circle, 1.5 * s), palette.WindowFill);
			}

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				this.Dragging = this.Nearest(mouseEvent.Position);
				if (this.Dragging >= 0)
				{
					this.Invalidate();
				}
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (this.Dragging >= 0)
			{
				var next = new Vector2(Math.Clamp(mouseEvent.X, 0, this.Width), Math.Clamp(mouseEvent.Y, 0, this.Height));
				if (next != this.Points[this.Dragging])
				{
					this.Points[this.Dragging] = next;
					this.Invalidate();
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (this.Dragging >= 0)
			{
				this.Dragging = -1;
				this.Invalidate();
			}

			base.OnMouseUp(mouseEvent);
		}
	}
}
