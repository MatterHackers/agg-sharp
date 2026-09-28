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

using System.Globalization;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// C++ AGG's <c>gamma_ctrl</c>, drawn and driven exactly as C++ does: a <see cref="GammaSpline"/> curve in
	/// a bordered box with a grid, two draggable control points (the active one red) and the four values as
	/// text above. Arrow keys move the active point by 0.005. It is an <see cref="IGammaFunction"/>, so
	/// <c>rasterizer.gamma(ctrl)</c> uses its curve, as C++ <c>ras.gamma(g_ctrl)</c> does.
	/// </summary>
	public class GammaCtrl : AggCtrl, IGammaFunction
	{
		private readonly double xc1;
		private readonly double yc1;
		private readonly double xc2;
		private readonly double xt1;
		private readonly double yt2;
		private double borderWidth = 2.0;
		private double borderExtra;
		private double yc2;
		private double yt1;
		private double xs1;
		private double ys1;
		private double xs2;
		private double ys2;
		private double xp1;
		private double yp1;
		private double xp2;
		private double yp2;
		private int mousePoint;
		private double pdx;
		private double pdy;

		public GammaCtrl(double x1, double y1, double x2, double y2, bool flipY = false)
			: base(x1, y1, x2, y2, flipY)
		{
			this.xc1 = x1;
			this.yc1 = y1;
			this.xc2 = x2;
			this.yc2 = y2 - (this.TextHeight * 2.0);
			this.xt1 = x1;
			this.yt1 = y2 - (this.TextHeight * 2.0);
			this.yt2 = y2;
			this.CalcSplineBox();
		}

		/// <summary>The curve the control edits; C++ forwards a copy of its interface, which is below as well.</summary>
		public GammaSpline Spline { get; } = new GammaSpline();

		/// <summary>Whether point 1 (lower left) is the active one - the one the arrow keys move, drawn red.</summary>
		public bool Point1Active { get; set; } = true;

		public double CurveWidth { get; set; } = 2.0;

		public double GridWidth { get; set; } = 0.2;

		public double TextThickness { get; set; } = 1.5;

		public double PointSize { get; set; } = 5.0;

		public double TextHeight { get; private set; } = 9.0;

		/// <summary>0 lets gsv_text pick the width from the height.</summary>
		public double TextWidth { get; private set; }

		public double BorderWidth => this.borderWidth;

		public double BorderExtra => this.borderExtra;

		public Color BackgroundColor { get; set; } = Rgba8.FromRgba(1.0, 1.0, 0.9);

		public Color BorderColor { get; set; } = Rgba8.FromRgba(0.0, 0.0, 0.0);

		public Color CurveColor { get; set; } = Rgba8.FromRgba(0.0, 0.0, 0.0);

		public Color GridColor { get; set; } = Rgba8.FromRgba(0.2, 0.2, 0.0);

		public Color InactivePointColor { get; set; } = Rgba8.FromRgba(0.0, 0.0, 0.0);

		public Color ActivePointColor { get; set; } = Rgba8.FromRgba(1.0, 0.0, 0.0);

		public Color TextColor { get; set; } = Rgba8.FromRgba(0.0, 0.0, 0.0);

		/// <summary>Background, border, curve, grid, inactive point, active point, text.</summary>
		public override int NumPaths => 7;

		/// <summary>C++ <c>gamma()</c>.</summary>
		public byte[] Gamma => this.Spline.Gamma;

		/// <summary>C++ <c>values(kx1, ky1, kx2, ky2)</c>.</summary>
		public void SetValues(double kx1, double ky1, double kx2, double ky2) => this.Spline.SetValues(kx1, ky1, kx2, ky2);

		/// <summary>C++ <c>values(&amp;kx1, ...)</c>.</summary>
		public void GetValues(out double kx1, out double ky1, out double kx2, out double ky2) => this.Spline.GetValues(out kx1, out ky1, out kx2, out ky2);

		/// <summary>C++ <c>y(x)</c>.</summary>
		public double Y(double x) => this.Spline.Y(x);

		/// <summary>C++ <c>operator()</c>, what <c>rasterizer.gamma(ctrl)</c> samples.</summary>
		public double GetGamma(double x) => this.Spline.Y(x);

		/// <summary>C++ <c>text_size</c>: also moves the bar between the curve box and the text.</summary>
		public void SetTextSize(double height, double width = 0.0)
		{
			this.TextWidth = width;
			this.TextHeight = height;
			this.yc2 = this.Y2 - (this.TextHeight * 2.0);
			this.yt1 = this.Y2 - (this.TextHeight * 2.0);
			this.CalcSplineBox();
		}

		/// <summary>C++ <c>border_width</c>; <paramref name="extra"/> grows the background past the border.</summary>
		public void SetBorderWidth(double width, double extra = 0.0)
		{
			this.borderWidth = width;
			this.borderExtra = extra;
			this.CalcSplineBox();
		}

		/// <summary>C++ <c>change_active_point</c>.</summary>
		public void ChangeActivePoint() => this.Point1Active = !this.Point1Active;

		public override Color PathColor(int index)
		{
			switch (index)
			{
				case 0: return this.BackgroundColor;
				case 1: return this.BorderColor;
				case 2: return this.CurveColor;
				case 3: return this.GridColor;
				case 4: return this.InactivePointColor;
				case 5: return this.ActivePointColor;
				default: return this.TextColor;
			}
		}

		public override bool InRect(double x, double y)
		{
			this.InverseTransformXY(ref x, ref y);
			return x >= this.X1 && x <= this.X2 && y >= this.Y1 && y <= this.Y2;
		}

		public override bool OnMouseButtonDown(double x, double y)
		{
			this.InverseTransformXY(ref x, ref y);
			this.CalcPoints();

			if (agg_math.CalcDistance(x, y, this.xp1, this.yp1) <= this.PointSize + 1)
			{
				this.mousePoint = 1;
				this.pdx = this.xp1 - x;
				this.pdy = this.yp1 - y;
				this.Point1Active = true;
				return true;
			}

			if (agg_math.CalcDistance(x, y, this.xp2, this.yp2) <= this.PointSize + 1)
			{
				this.mousePoint = 2;
				this.pdx = this.xp2 - x;
				this.pdy = this.yp2 - y;
				this.Point1Active = false;
				return true;
			}

			return false;
		}

		public override bool OnMouseButtonUp(double x, double y)
		{
			if (this.mousePoint != 0)
			{
				this.mousePoint = 0;
				return true;
			}

			return false;
		}

		public override bool OnMouseMove(double x, double y, bool buttonFlag)
		{
			this.InverseTransformXY(ref x, ref y);
			if (!buttonFlag)
			{
				return this.OnMouseButtonUp(x, y);
			}

			if (this.mousePoint == 1)
			{
				this.xp1 = x + this.pdx;
				this.yp1 = y + this.pdy;
				this.CalcValues();
				return true;
			}

			if (this.mousePoint == 2)
			{
				this.xp2 = x + this.pdx;
				this.yp2 = y + this.pdy;
				this.CalcValues();
				return true;
			}

			return false;
		}

		public override bool OnArrowKeys(bool left, bool right, bool down, bool up)
		{
			this.Spline.GetValues(out double kx1, out double ky1, out double kx2, out double ky2);
			bool ret = false;
			if (this.Point1Active)
			{
				if (left) { kx1 -= 0.005; ret = true; }
				if (right) { kx1 += 0.005; ret = true; }
				if (down) { ky1 -= 0.005; ret = true; }
				if (up) { ky1 += 0.005; ret = true; }
			}
			else
			{
				if (left) { kx2 += 0.005; ret = true; }
				if (right) { kx2 -= 0.005; ret = true; }
				if (down) { ky2 += 0.005; ret = true; }
				if (up) { ky2 -= 0.005; ret = true; }
			}

			if (ret)
			{
				this.Spline.SetValues(kx1, ky1, kx2, ky2);
			}

			return ret;
		}

		/// <summary>C++ <c>gamma_ctrl_impl::rewind</c> + <c>vertex</c> for one path.</summary>
		protected override IVertexSource Path(int index)
		{
			var storage = new VertexStorage();
			double bw = this.borderWidth;
			double gw = this.GridWidth;
			switch (index)
			{
				case 0: // background
					storage.MoveTo(this.X1 - this.borderExtra, this.Y1 - this.borderExtra);
					storage.LineTo(this.X2 + this.borderExtra, this.Y1 - this.borderExtra);
					storage.LineTo(this.X2 + this.borderExtra, this.Y2 + this.borderExtra);
					storage.LineTo(this.X1 - this.borderExtra, this.Y2 + this.borderExtra);
					return storage;

				case 1: // border: outer, inner (wound the other way) and the bar between curve and text
					storage.MoveTo(this.X1, this.Y1);
					storage.LineTo(this.X2, this.Y1);
					storage.LineTo(this.X2, this.Y2);
					storage.LineTo(this.X1, this.Y2);
					storage.MoveTo(this.X1 + bw, this.Y1 + bw);
					storage.LineTo(this.X1 + bw, this.Y2 - bw);
					storage.LineTo(this.X2 - bw, this.Y2 - bw);
					storage.LineTo(this.X2 - bw, this.Y1 + bw);
					storage.MoveTo(this.xc1 + bw, this.yc2 - (bw * 0.5));
					storage.LineTo(this.xc2 - bw, this.yc2 - (bw * 0.5));
					storage.LineTo(this.xc2 - bw, this.yc2 + (bw * 0.5));
					storage.LineTo(this.xc1 + bw, this.yc2 + (bw * 0.5));
					return storage;

				case 2: // curve
					this.Spline.SetBox(this.xs1, this.ys1, this.xs2, this.ys2);
					return new Stroke(this.Spline.CurvePath(), this.CurveWidth);

				case 3: // grid: the centre cross, then an L from each side to its control point
					storage.MoveTo(this.xs1, ((this.ys1 + this.ys2) * 0.5) - (gw * 0.5));
					storage.LineTo(this.xs2, ((this.ys1 + this.ys2) * 0.5) - (gw * 0.5));
					storage.LineTo(this.xs2, ((this.ys1 + this.ys2) * 0.5) + (gw * 0.5));
					storage.LineTo(this.xs1, ((this.ys1 + this.ys2) * 0.5) + (gw * 0.5));
					storage.MoveTo(((this.xs1 + this.xs2) * 0.5) - (gw * 0.5), this.ys1);
					storage.LineTo(((this.xs1 + this.xs2) * 0.5) - (gw * 0.5), this.ys2);
					storage.LineTo(((this.xs1 + this.xs2) * 0.5) + (gw * 0.5), this.ys2);
					storage.LineTo(((this.xs1 + this.xs2) * 0.5) + (gw * 0.5), this.ys1);
					this.CalcPoints();
					storage.MoveTo(this.xs1, this.yp1 - (gw * 0.5));
					storage.LineTo(this.xp1 - (gw * 0.5), this.yp1 - (gw * 0.5));
					storage.LineTo(this.xp1 - (gw * 0.5), this.ys1);
					storage.LineTo(this.xp1 + (gw * 0.5), this.ys1);
					storage.LineTo(this.xp1 + (gw * 0.5), this.yp1 + (gw * 0.5));
					storage.LineTo(this.xs1, this.yp1 + (gw * 0.5));
					storage.MoveTo(this.xs2, this.yp2 + (gw * 0.5));
					storage.LineTo(this.xp2 + (gw * 0.5), this.yp2 + (gw * 0.5));
					storage.LineTo(this.xp2 + (gw * 0.5), this.ys2);
					storage.LineTo(this.xp2 - (gw * 0.5), this.ys2);
					storage.LineTo(this.xp2 - (gw * 0.5), this.yp2 - (gw * 0.5));
					storage.LineTo(this.xs2, this.yp2 - (gw * 0.5));
					return storage;

				case 4: // inactive point
				case 5: // active point
					this.CalcPoints();
					bool pointOne = (index == 5) == this.Point1Active;
					return pointOne
						? new Ellipse(this.xp1, this.yp1, this.PointSize, this.PointSize, 32)
						: new Ellipse(this.xp2, this.yp2, this.PointSize, this.PointSize, 32);

				default: // text, C++ sprintf("%5.3f %5.3f %5.3f %5.3f")
					this.Spline.GetValues(out double kx1, out double ky1, out double kx2, out double ky2);
					string text = string.Format(CultureInfo.InvariantCulture, "{0,5:F3} {1,5:F3} {2,5:F3} {3,5:F3}", kx1, ky1, kx2, ky2);
					return StrokedText(text, this.xt1 + (bw * 2.0), ((this.yt1 + this.yt2) * 0.5) - (this.TextHeight * 0.5), this.TextHeight, this.TextWidth, this.TextThickness);
			}
		}

		private void CalcSplineBox()
		{
			this.xs1 = this.xc1 + this.borderWidth;
			this.ys1 = this.yc1 + this.borderWidth;
			this.xs2 = this.xc2 - this.borderWidth;
			this.ys2 = this.yc2 - (this.borderWidth * 0.5);
		}

		private void CalcPoints()
		{
			this.Spline.GetValues(out double kx1, out double ky1, out double kx2, out double ky2);
			this.xp1 = this.xs1 + ((this.xs2 - this.xs1) * kx1 * 0.25);
			this.yp1 = this.ys1 + ((this.ys2 - this.ys1) * ky1 * 0.25);
			this.xp2 = this.xs2 - ((this.xs2 - this.xs1) * kx2 * 0.25);
			this.yp2 = this.ys2 - ((this.ys2 - this.ys1) * ky2 * 0.25);
		}

		private void CalcValues()
		{
			double kx1 = (this.xp1 - this.xs1) * 4.0 / (this.xs2 - this.xs1);
			double ky1 = (this.yp1 - this.ys1) * 4.0 / (this.ys2 - this.ys1);
			double kx2 = (this.xs2 - this.xp2) * 4.0 / (this.xs2 - this.xs1);
			double ky2 = (this.ys2 - this.yp2) * 4.0 / (this.ys2 - this.ys1);
			this.Spline.SetValues(kx1, ky1, kx2, ky2);
		}
	}
}
