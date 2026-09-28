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

using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// C++ AGG's <c>bezier_ctrl</c>: a cubic Bezier the user edits by dragging its four points. It draws the two
	/// control lines and the curve (each a <see cref="LineWidth"/> <c>conv_stroke</c>) and a 20-step circle on every
	/// point, all in <see cref="LineColor"/>. Drag a point, or an edge of the control polygon to move both its ends;
	/// unlike <see cref="InteractivePolygon"/>'s default, a press inside the polygon grabs nothing.
	/// </summary>
	public class BezierCtrl : AggCtrl
	{
		private readonly InteractivePolygon poly = new InteractivePolygon(4, 5.0) { InPolygonCheck = false };

		public BezierCtrl()
			: base(0, 0, 1, 1, false)
		{
			this.SetCurve(100.0, 0.0, 100.0, 50.0, 50.0, 100.0, 0.0, 100.0);
		}

		/// <summary>C++ <c>line_color</c>: the color of every path; black by default.</summary>
		public Color LineColor { get; set; } = Color.Black;

		/// <summary>C++ <c>line_width</c>: how wide the control lines and the curve are stroked.</summary>
		public double LineWidth { get; set; } = 1.0;

		/// <summary>Control lines, curve, four points.</summary>
		public override int NumPaths => 7;

		/// <summary>Point <paramref name="index"/>, 0 to 3: start, first control, second control, end.</summary>
		public Vector2 GetPoint(int index) => this.poly.GetPoint(index);

		/// <summary>C++ <c>curve(x1, y1, ... x4, y4)</c>: places all four points.</summary>
		public void SetCurve(double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
		{
			this.poly.SetPoint(0, x1, y1);
			this.poly.SetPoint(1, x2, y2);
			this.poly.SetPoint(2, x3, y3);
			this.poly.SetPoint(3, x4, y4);
		}

		/// <summary>C++ <c>curve()</c>: the Bezier through the current points, flattened as C++ <c>curve4</c> is.</summary>
		public Curve4 Curve()
		{
			Vector2 p1 = this.GetPoint(0);
			Vector2 p2 = this.GetPoint(1);
			Vector2 p3 = this.GetPoint(2);
			Vector2 p4 = this.GetPoint(3);
			return new Curve4(p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y, p4.X, p4.Y);
		}

		public override Color PathColor(int index) => this.LineColor;

		public override bool InRect(double x, double y) => false;

		public override bool OnMouseButtonDown(double x, double y)
		{
			this.InverseTransformXY(ref x, ref y);
			return this.poly.OnMouseButtonDown(x, y);
		}

		public override bool OnMouseMove(double x, double y, bool buttonFlag)
		{
			this.InverseTransformXY(ref x, ref y);
			return this.poly.OnMouseMove(x, y);
		}

		public override bool OnMouseButtonUp(double x, double y) => this.poly.OnMouseButtonUp(x, y);

		/// <summary>C++ <c>polygon_ctrl::on_arrow_keys</c> does nothing, so neither does this.</summary>
		public override bool OnArrowKeys(bool left, bool right, bool down, bool up) => false;

		protected override IVertexSource Path(int index)
		{
			Vector2 p1 = this.GetPoint(0);
			Vector2 p2 = this.GetPoint(1);
			Vector2 p3 = this.GetPoint(2);
			Vector2 p4 = this.GetPoint(3);
			// C++ approximation_scale(scale()): a scaled control flattens its curves finer. curve4 flattens in init,
			// so the scale has to be set first.
			var curve = new Curve4();
			curve.approximation_scale(this.Transform is Affine transform ? transform.GetScale() : 1.0);
			switch (index)
			{
				case 0:
					// C++ draws each control line as a curve4 with both controls at the line's middle.
					curve.init(p1.X, p1.Y, (p1.X + p2.X) * 0.5, (p1.Y + p2.Y) * 0.5, (p1.X + p2.X) * 0.5, (p1.Y + p2.Y) * 0.5, p2.X, p2.Y);
					break;

				case 1:
					curve.init(p3.X, p3.Y, (p3.X + p4.X) * 0.5, (p3.Y + p4.Y) * 0.5, (p3.X + p4.X) * 0.5, (p3.Y + p4.Y) * 0.5, p4.X, p4.Y);
					break;

				case 2:
					curve.init(p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y, p4.X, p4.Y);
					break;

				default:
					Vector2 point = this.GetPoint(index - 3);
					return new Ellipse(point.X, point.Y, this.poly.PointRadius, this.poly.PointRadius, 20);
			}

			return new Stroke(curve, this.LineWidth);
		}
	}
}
