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

using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// C++ AGG's <c>spline_ctrl</c>, drawn and driven exactly as C++ does: a bspline through 4 to 32 control
	/// points in a bordered box, sampled into 256 values clamped to 0-1 (<see cref="Spline"/>, and as bytes
	/// <see cref="Spline8"/>). Drag a point, or move the active one with the arrow keys by 0.001. Points keep
	/// their x order: the ends are pinned to 0 and 1, inner points stay 0.001 clear of their neighbours.
	/// </summary>
	public class SplineCtrl : AggCtrl
	{
		private const int MaxPoints = 32;

		private readonly int numPoints;
		private readonly double[] xp = new double[MaxPoints];
		private readonly double[] yp = new double[MaxPoints];
		private readonly bspline spline = new bspline();
		private readonly double[] splineValues = new double[256];
		private readonly byte[] splineValues8 = new byte[256];
		private double borderWidth = 1.0;
		private double borderExtra;
		private double xs1;
		private double ys1;
		private double xs2;
		private double ys2;
		private int movePoint = -1;
		private double pdx;
		private double pdy;

		public SplineCtrl(double x1, double y1, double x2, double y2, int numPoints, bool flipY = false)
			: base(x1, y1, x2, y2, flipY)
		{
			this.numPoints = System.Math.Clamp(numPoints, 4, MaxPoints);
			for (int i = 0; i < this.numPoints; i++)
			{
				this.xp[i] = (double)i / (this.numPoints - 1);
				this.yp[i] = 0.5;
			}

			this.CalcSplineBox();
			this.UpdateSpline();
		}

		public int NumPoints => this.numPoints;

		public double CurveWidth { get; set; } = 1.0;

		public double PointSize { get; set; } = 3.0;

		public double BorderWidth => this.borderWidth;

		public double BorderExtra => this.borderExtra;

		/// <summary>The point the arrow keys move and that draws in the active color; -1 (the default) is none.</summary>
		public int ActivePoint { get; set; } = -1;

		public Color BackgroundColor { get; set; } = Rgba8.FromRgba(1.0, 1.0, 0.9);

		public Color BorderColor { get; set; } = Rgba8.FromRgba(0.0, 0.0, 0.0);

		public Color CurveColor { get; set; } = Rgba8.FromRgba(0.0, 0.0, 0.0);

		public Color InactivePointColor { get; set; } = Rgba8.FromRgba(0.0, 0.0, 0.0);

		public Color ActivePointColor { get; set; } = Rgba8.FromRgba(1.0, 0.0, 0.0);

		/// <summary>Background, border, curve, inactive points, active point.</summary>
		public override int NumPaths => 5;

		/// <summary>C++ <c>spline()</c>: the curve at x = i / 255, clamped to 0-1, as of the last <see cref="UpdateSpline"/>.</summary>
		public double[] Spline => this.splineValues;

		/// <summary>C++ <c>spline8()</c>: <see cref="Spline"/> times 255, truncated.</summary>
		public byte[] Spline8 => this.splineValues8;

		/// <summary>C++ <c>border_width</c>; <paramref name="extra"/> grows the background past the border.</summary>
		public void SetBorderWidth(double width, double extra = 0.0)
		{
			this.borderWidth = width;
			this.borderExtra = extra;
			this.CalcSplineBox();
		}

		/// <summary>C++ <c>value(x)</c>: the curve at <paramref name="x"/>, clamped to 0-1.</summary>
		public double Value(double x)
		{
			x = this.spline.get(x);
			if (x < 0.0)
			{
				x = 0.0;
			}

			if (x > 1.0)
			{
				x = 1.0;
			}

			return x;
		}

		/// <summary>C++ <c>value(idx, y)</c>: sets point <paramref name="index"/>'s y, clamped; call <see cref="UpdateSpline"/> after.</summary>
		public void SetValue(int index, double y)
		{
			if (index >= 0 && index < this.numPoints)
			{
				this.SetYp(index, y);
			}
		}

		/// <summary>C++ <c>point(idx, x, y)</c>: moves point <paramref name="index"/> within the x order rules; call <see cref="UpdateSpline"/> after.</summary>
		public void SetPoint(int index, double x, double y)
		{
			if (index >= 0 && index < this.numPoints)
			{
				this.SetXp(index, x);
				this.SetYp(index, y);
			}
		}

		/// <summary>C++ <c>x(idx, x)</c>: sets the x unclamped.</summary>
		public void SetX(int index, double x) => this.xp[index] = x;

		/// <summary>C++ <c>y(idx, y)</c>: sets the y unclamped.</summary>
		public void SetY(int index, double y) => this.yp[index] = y;

		public double GetX(int index) => this.xp[index];

		public double GetY(int index) => this.yp[index];

		/// <summary>C++ <c>update_spline</c>: refits the bspline through the points and resamples <see cref="Spline"/>.</summary>
		public void UpdateSpline()
		{
			this.spline.init(this.numPoints, this.xp, this.yp);
			for (int i = 0; i < 256; i++)
			{
				double value = this.spline.get(i / 255.0);
				if (value < 0.0)
				{
					value = 0.0;
				}

				if (value > 1.0)
				{
					value = 1.0;
				}

				this.splineValues[i] = value;
				this.splineValues8[i] = (byte)(value * 255.0);
			}
		}

		public override Color PathColor(int index)
		{
			switch (index)
			{
				case 0: return this.BackgroundColor;
				case 1: return this.BorderColor;
				case 2: return this.CurveColor;
				case 3: return this.InactivePointColor;
				default: return this.ActivePointColor;
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
			for (int i = 0; i < this.numPoints; i++)
			{
				double px = this.CalcXp(i);
				double py = this.CalcYp(i);
				if (agg_math.CalcDistance(x, y, px, py) <= this.PointSize + 1)
				{
					this.pdx = px - x;
					this.pdy = py - y;
					this.ActivePoint = this.movePoint = i;
					return true;
				}
			}

			return false;
		}

		public override bool OnMouseButtonUp(double x, double y)
		{
			if (this.movePoint >= 0)
			{
				this.movePoint = -1;
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

			if (this.movePoint >= 0)
			{
				double px = x + this.pdx;
				double py = y + this.pdy;
				this.SetXp(this.movePoint, (px - this.xs1) / (this.xs2 - this.xs1));
				this.SetYp(this.movePoint, (py - this.ys1) / (this.ys2 - this.ys1));
				this.UpdateSpline();
				return true;
			}

			return false;
		}

		public override bool OnArrowKeys(bool left, bool right, bool down, bool up)
		{
			bool ret = false;
			double kx = 0.0;
			double ky = 0.0;
			if (this.ActivePoint >= 0)
			{
				kx = this.xp[this.ActivePoint];
				ky = this.yp[this.ActivePoint];
				if (left) { kx -= 0.001; ret = true; }
				if (right) { kx += 0.001; ret = true; }
				if (down) { ky -= 0.001; ret = true; }
				if (up) { ky += 0.001; ret = true; }
			}

			if (ret)
			{
				this.SetXp(this.ActivePoint, kx);
				this.SetYp(this.ActivePoint, ky);
				this.UpdateSpline();
			}

			return ret;
		}

		/// <summary>C++ <c>spline_ctrl_impl::rewind</c> + <c>vertex</c> for one path.</summary>
		protected override IVertexSource Path(int index)
		{
			var storage = new VertexStorage();
			double bw = this.borderWidth;
			double be = this.borderExtra;
			switch (index)
			{
				case 0: // background
					storage.MoveTo(this.X1 - be, this.Y1 - be);
					storage.LineTo(this.X2 + be, this.Y1 - be);
					storage.LineTo(this.X2 + be, this.Y2 + be);
					storage.LineTo(this.X1 - be, this.Y2 + be);
					return storage;

				case 1: // border: outer, then inner wound the other way
					storage.MoveTo(this.X1, this.Y1);
					storage.LineTo(this.X2, this.Y1);
					storage.LineTo(this.X2, this.Y2);
					storage.LineTo(this.X1, this.Y2);
					storage.MoveTo(this.X1 + bw, this.Y1 + bw);
					storage.LineTo(this.X1 + bw, this.Y2 - bw);
					storage.LineTo(this.X2 - bw, this.Y2 - bw);
					storage.LineTo(this.X2 - bw, this.Y1 + bw);
					return storage;

				case 2: // curve, the 256 samples as a stroked polyline
					storage.MoveTo(this.xs1, this.ys1 + ((this.ys2 - this.ys1) * this.splineValues[0]));
					for (int i = 1; i < 256; i++)
					{
						storage.LineTo(this.xs1 + ((this.xs2 - this.xs1) * i / 255.0), this.ys1 + ((this.ys2 - this.ys1) * this.splineValues[i]));
					}

					return new Stroke(storage, this.CurveWidth);

				case 3: // inactive points
					for (int i = 0; i < this.numPoints; i++)
					{
						if (i != this.ActivePoint)
						{
							storage.ConcatPath(new Ellipse(this.CalcXp(i), this.CalcYp(i), this.PointSize, this.PointSize, 32));
						}
					}

					return storage;

				default: // active point
					if (this.ActivePoint >= 0)
					{
						storage.ConcatPath(new Ellipse(this.CalcXp(this.ActivePoint), this.CalcYp(this.ActivePoint), this.PointSize, this.PointSize, 32));
					}

					return storage;
			}
		}

		private void CalcSplineBox()
		{
			this.xs1 = this.X1 + this.borderWidth;
			this.ys1 = this.Y1 + this.borderWidth;
			this.xs2 = this.X2 - this.borderWidth;
			this.ys2 = this.Y2 - this.borderWidth;
		}

		private double CalcXp(int index) => this.xs1 + ((this.xs2 - this.xs1) * this.xp[index]);

		private double CalcYp(int index) => this.ys1 + ((this.ys2 - this.ys1) * this.yp[index]);

		private void SetXp(int index, double value)
		{
			if (value < 0.0)
			{
				value = 0.0;
			}

			if (value > 1.0)
			{
				value = 1.0;
			}

			if (index == 0)
			{
				value = 0.0;
			}
			else if (index == this.numPoints - 1)
			{
				value = 1.0;
			}
			else
			{
				if (value < this.xp[index - 1] + 0.001)
				{
					value = this.xp[index - 1] + 0.001;
				}

				if (value > this.xp[index + 1] - 0.001)
				{
					value = this.xp[index + 1] - 0.001;
				}
			}

			this.xp[index] = value;
		}

		private void SetYp(int index, double value)
		{
			if (value < 0.0)
			{
				value = 0.0;
			}

			if (value > 1.0)
			{
				value = 1.0;
			}

			this.yp[index] = value;
		}
	}
}
