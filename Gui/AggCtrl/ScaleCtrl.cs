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
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// C++ AGG's <c>scale_ctrl</c>, drawn and driven exactly as C++ does: a range [<see cref="Value1"/>,
	/// <see cref="Value2"/>] inside [0, 1], shown as a bar between two pointers. Drag a pointer to move that
	/// end, or the bar to move both. Laid out along whichever side of its box is longer.
	/// </summary>
	public class ScaleCtrl : AggCtrl
	{
		private double borderThickness = 1.0;

		private double borderExtra;

		private double value1 = 0.3;

		private double value2 = 0.7;

		private double xs1;

		private double ys1;

		private double xs2;

		private double ys2;

		private double pdx;

		private double pdy;

		private MoveWhat moveWhat = MoveWhat.Nothing;

		public ScaleCtrl(double x1, double y1, double x2, double y2, bool flipY = false)
			: base(x1, y1, x2, y2, flipY)
		{
			this.borderExtra = this.IsHorizontal ? (y2 - y1) / 2 : (x2 - x1) / 2;
			this.CalcBox();
		}

		private enum MoveWhat
		{
			Nothing,
			Value1,
			Value2,
			Slider,
		}

		public Color BackgroundColor { get; set; } = Rgba8.FromRgba(1.0, 0.9, 0.8);

		public Color BorderColor { get; set; } = Rgba8.FromRgba(0.0, 0.0, 0.0);

		public Color PointersColor { get; set; } = Rgba8.FromRgba(0.8, 0.0, 0.0, 0.8);

		public Color SliderColor { get; set; } = Rgba8.FromRgba(0.2, 0.1, 0.0, 0.6);

		/// <summary>C++ <c>min_delta</c>: the smallest gap a drag leaves between the two values.</summary>
		public double MinDelta { get; set; } = 0.01;

		/// <summary>The low end, in [0, 1]. Setting clamps it to [0, 1] and below <see cref="Value2"/> by
		/// <see cref="MinDelta"/>, as C++ <c>value1</c> does.</summary>
		public double Value1
		{
			get => this.value1;
			set
			{
				if (value < 0.0)
				{
					value = 0.0;
				}

				if (value > 1.0)
				{
					value = 1.0;
				}

				if (this.value2 - value < this.MinDelta)
				{
					value = this.value2 - this.MinDelta;
				}

				this.value1 = value;
			}
		}

		/// <summary>The high end, in [0, 1]. Setting clamps it to [0, 1]; the <see cref="MinDelta"/> check is
		/// C++'s own, <c>value1 + value &lt; min_d</c>, which only bites when both are near 0 - set
		/// <see cref="Value2"/> before <see cref="Value1"/> when narrowing.</summary>
		public double Value2
		{
			get => this.value2;
			set
			{
				if (value < 0.0)
				{
					value = 0.0;
				}

				if (value > 1.0)
				{
					value = 1.0;
				}

				if (this.value1 + value < this.MinDelta)
				{
					value = this.value1 + this.MinDelta;
				}

				this.value2 = value;
			}
		}

		public override int NumPaths => 5;

		// C++ tests fabs(x2 - x1) > fabs(y2 - y1) everywhere it chooses between the two layouts.
		private bool IsHorizontal => Math.Abs(this.X2 - this.X1) > Math.Abs(this.Y2 - this.Y1);

		/// <summary>C++ <c>border_thickness</c>: the border's width and how far the background reaches past the box.</summary>
		public void SetBorderThickness(double thickness, double extra = 0.0)
		{
			this.borderThickness = thickness;
			this.borderExtra = extra;
			this.CalcBox();
		}

		/// <summary>C++ <c>move</c>: shifts both values by <paramref name="d"/>, keeping the range inside [0, 1].</summary>
		public void Move(double d)
		{
			this.value1 += d;
			this.value2 += d;
			if (this.value1 < 0.0)
			{
				this.value2 -= this.value1;
				this.value1 = 0.0;
			}

			if (this.value2 > 1.0)
			{
				this.value1 -= this.value2 - 1.0;
				this.value2 = 1.0;
			}
		}

		public override Color PathColor(int index)
		{
			switch (index)
			{
				case 0: return this.BackgroundColor;
				case 1: return this.BorderColor;
				case 2:
				case 3: return this.PointersColor;
				default: return this.SliderColor;
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

			if (this.IsHorizontal)
			{
				double xp1 = this.xs1 + ((this.xs2 - this.xs1) * this.value1);
				double xp2 = this.xs1 + ((this.xs2 - this.xs1) * this.value2);
				double barY1 = this.Y1 - (this.borderExtra / 2.0);
				double barY2 = this.Y2 + (this.borderExtra / 2.0);
				double yp = (this.ys1 + this.ys2) / 2.0;

				if (x > xp1 && y > barY1 && x < xp2 && y < barY2)
				{
					this.pdx = xp1 - x;
					this.moveWhat = MoveWhat.Slider;
					return true;
				}

				if (Distance(x, y, xp1, yp) <= this.Y2 - this.Y1)
				{
					this.pdx = xp1 - x;
					this.moveWhat = MoveWhat.Value1;
					return true;
				}

				if (Distance(x, y, xp2, yp) <= this.Y2 - this.Y1)
				{
					this.pdx = xp2 - x;
					this.moveWhat = MoveWhat.Value2;
					return true;
				}
			}
			else
			{
				double barX1 = this.X1 - (this.borderExtra / 2.0);
				double barX2 = this.X2 + (this.borderExtra / 2.0);
				double yp1 = this.ys1 + ((this.ys2 - this.ys1) * this.value1);
				double yp2 = this.ys1 + ((this.ys2 - this.ys1) * this.value2);
				double xp = (this.xs1 + this.xs2) / 2.0;

				if (x > barX1 && y > yp1 && x < barX2 && y < yp2)
				{
					this.pdy = yp1 - y;
					this.moveWhat = MoveWhat.Slider;
					return true;
				}

				if (Distance(x, y, xp, yp1) <= this.X2 - this.X1)
				{
					this.pdy = yp1 - y;
					this.moveWhat = MoveWhat.Value1;
					return true;
				}

				if (Distance(x, y, xp, yp2) <= this.X2 - this.X1)
				{
					this.pdy = yp2 - y;
					this.moveWhat = MoveWhat.Value2;
					return true;
				}
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

			double xp = x + this.pdx;
			double yp = y + this.pdy;

			switch (this.moveWhat)
			{
				case MoveWhat.Value1:
					this.value1 = this.PositionToValue(xp, yp);
					if (this.value1 < 0.0)
					{
						this.value1 = 0.0;
					}

					if (this.value1 > this.value2 - this.MinDelta)
					{
						this.value1 = this.value2 - this.MinDelta;
					}

					return true;

				case MoveWhat.Value2:
					this.value2 = this.PositionToValue(xp, yp);
					if (this.value2 > 1.0)
					{
						this.value2 = 1.0;
					}

					if (this.value2 < this.value1 + this.MinDelta)
					{
						this.value2 = this.value1 + this.MinDelta;
					}

					return true;

				case MoveWhat.Slider:
					double dv = this.value2 - this.value1;
					this.value1 = this.PositionToValue(xp, yp);
					this.value2 = this.value1 + dv;
					if (this.value1 < 0.0)
					{
						dv = this.value2 - this.value1;
						this.value1 = 0.0;
						this.value2 = this.value1 + dv;
					}

					if (this.value2 > 1.0)
					{
						dv = this.value2 - this.value1;
						this.value2 = 1.0;
						this.value1 = this.value2 - dv;
					}

					return true;

				default:
					return false;
			}
		}

		public override bool OnMouseButtonUp(double x, double y)
		{
			this.moveWhat = MoveWhat.Nothing;
			return false;
		}

		/// <summary>C++ leaves its arrow-key handling commented out: the keys do nothing.</summary>
		public override bool OnArrowKeys(bool left, bool right, bool down, bool up)
		{
			return false;
		}

		/// <summary>C++ <c>scale_ctrl_impl::rewind</c> + <c>vertex</c> for one path.</summary>
		protected override IVertexSource Path(int index)
		{
			var storage = new VertexStorage();
			switch (index)
			{
				case 0: // background
					storage.MoveTo(this.X1 - this.borderExtra, this.Y1 - this.borderExtra);
					storage.LineTo(this.X2 + this.borderExtra, this.Y1 - this.borderExtra);
					storage.LineTo(this.X2 + this.borderExtra, this.Y2 + this.borderExtra);
					storage.LineTo(this.X1 - this.borderExtra, this.Y2 + this.borderExtra);
					return storage;

				case 1: // border: the box, then the box shrunk by the border wound the other way, so a frame
					storage.MoveTo(this.X1, this.Y1);
					storage.LineTo(this.X2, this.Y1);
					storage.LineTo(this.X2, this.Y2);
					storage.LineTo(this.X1, this.Y2);
					storage.MoveTo(this.X1 + this.borderThickness, this.Y1 + this.borderThickness);
					storage.LineTo(this.X1 + this.borderThickness, this.Y2 - this.borderThickness);
					storage.LineTo(this.X2 - this.borderThickness, this.Y2 - this.borderThickness);
					storage.LineTo(this.X2 - this.borderThickness, this.Y1 + this.borderThickness);
					return storage;

				case 2: // pointer1
					return this.Pointer(this.value1);

				case 3: // pointer2
					return this.Pointer(this.value2);

				default: // slider, the bar between the pointers
					if (this.IsHorizontal)
					{
						double barX1 = this.xs1 + ((this.xs2 - this.xs1) * this.value1);
						double barX2 = this.xs1 + ((this.xs2 - this.xs1) * this.value2);
						storage.MoveTo(barX1, this.Y1 - (this.borderExtra / 2.0));
						storage.LineTo(barX2, this.Y1 - (this.borderExtra / 2.0));
						storage.LineTo(barX2, this.Y2 + (this.borderExtra / 2.0));
						storage.LineTo(barX1, this.Y2 + (this.borderExtra / 2.0));
					}
					else
					{
						double barY1 = this.ys1 + ((this.ys2 - this.ys1) * this.value1);
						double barY2 = this.ys1 + ((this.ys2 - this.ys1) * this.value2);
						storage.MoveTo(this.X1 - (this.borderExtra / 2.0), barY1);
						storage.LineTo(this.X1 - (this.borderExtra / 2.0), barY2);
						storage.LineTo(this.X2 + (this.borderExtra / 2.0), barY2);
						storage.LineTo(this.X2 + (this.borderExtra / 2.0), barY1);
					}

					return storage;
			}
		}

		// C++ calc_distance(x1, y1, x2, y2).
		private static double Distance(double x1, double y1, double x2, double y2)
		{
			double dx = x2 - x1;
			double dy = y2 - y1;
			return Math.Sqrt((dx * dx) + (dy * dy));
		}

		private void CalcBox()
		{
			this.xs1 = this.X1 + this.borderThickness;
			this.ys1 = this.Y1 + this.borderThickness;
			this.xs2 = this.X2 - this.borderThickness;
			this.ys2 = this.Y2 - this.borderThickness;
		}

		// Where along the bar a pointer at (x, y) sits, 0 to 1 - unclamped.
		private double PositionToValue(double x, double y)
		{
			return this.IsHorizontal ? (x - this.xs1) / (this.xs2 - this.xs1) : (y - this.ys1) / (this.ys2 - this.ys1);
		}

		// The 32-step circle at the value's place along the bar, as wide as the box is thin.
		private IVertexSource Pointer(double value)
		{
			if (this.IsHorizontal)
			{
				return new Ellipse(this.xs1 + ((this.xs2 - this.xs1) * value), (this.ys1 + this.ys2) / 2.0, this.Y2 - this.Y1, this.Y2 - this.Y1, 32);
			}

			return new Ellipse((this.xs1 + this.xs2) / 2.0, this.ys1 + ((this.ys2 - this.ys1) * value), this.X2 - this.X1, this.X2 - this.X1, 32);
		}
	}
}
