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
using System.Globalization;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// C++ AGG's <c>slider_ctrl</c>, drawn and driven exactly as C++ does: a background box, the value
	/// triangle, the label (gsv_text stroked 1 wide with round joins and caps), a preview pointer that follows
	/// the drag and the pointer at the committed value, plus tick marks when <see cref="NumSteps"/> is set.
	/// </summary>
	public class SliderCtrl : AggCtrl
	{
		private readonly double borderWidth = 1.0;

		private readonly double borderExtra;

		private readonly double xs1;

		private readonly double ys1;

		private readonly double xs2;

		private readonly double ys2;

		private double value = 0.5;

		private double previewValue = 0.5;

		private double pdx;

		private bool mouseMove;

		public SliderCtrl(double x1, double y1, double x2, double y2, bool flipY = false)
			: base(x1, y1, x2, y2, flipY)
		{
			this.borderExtra = (y2 - y1) / 2;
			this.xs1 = x1 + this.borderWidth;
			this.ys1 = y1 + this.borderWidth;
			this.xs2 = x2 - this.borderWidth;
			this.ys2 = y2 - this.borderWidth;
		}

		public Color BackgroundColor { get; set; } = Rgba8.FromRgba(1.0, 0.9, 0.8);

		public Color TriangleColor { get; set; } = Rgba8.FromRgba(0.7, 0.6, 0.6);

		public Color TextColor { get; set; } = Rgba8.FromRgba(0.0, 0.0, 0.0);

		public Color PointerPreviewColor { get; set; } = Rgba8.FromRgba(0.6, 0.4, 0.4, 0.4);

		public Color PointerColor { get; set; } = Rgba8.FromRgba(0.8, 0.0, 0.0, 0.6);

		public double Min { get; private set; }

		public double Max { get; private set; } = 1.0;

		/// <summary>0 for a continuous slider; otherwise the value snaps to that many equal steps.</summary>
		public int NumSteps { get; set; }

		public bool Descending { get; set; }

		public double TextThickness { get; set; } = 1.0;

		/// <summary>
		/// The label as a .NET composite format of the value: C++ <c>"Alpha%3.3f"</c> is <c>"Alpha{0,3:F3}"</c>.
		/// Empty draws no text.
		/// </summary>
		public string Label { get; set; } = string.Empty;

		/// <summary>The committed value in [<see cref="Min"/>, <see cref="Max"/>]; setting clamps and snaps as C++ does.</summary>
		public double Value
		{
			get => (this.value * (this.Max - this.Min)) + this.Min;
			set
			{
				this.previewValue = (value - this.Min) / (this.Max - this.Min);
				if (this.previewValue > 1.0)
				{
					this.previewValue = 1.0;
				}

				if (this.previewValue < 0.0)
				{
					this.previewValue = 0.0;
				}

				this.NormalizeValue(true);
			}
		}

		public override int NumPaths => 6;

		public void SetRange(double min, double max)
		{
			this.Min = min;
			this.Max = max;
		}

		public override Color PathColor(int index)
		{
			switch (index)
			{
				case 0: return this.BackgroundColor;
				case 1: return this.TriangleColor;
				case 3: return this.PointerPreviewColor;
				case 4: return this.PointerColor;
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

			double xp = this.xs1 + ((this.xs2 - this.xs1) * this.value);
			double yp = (this.ys1 + this.ys2) / 2.0;

			double dx = xp - x;
			double dy = yp - y;
			if (Math.Sqrt((dx * dx) + (dy * dy)) <= this.Y2 - this.Y1)
			{
				this.pdx = xp - x;
				this.mouseMove = true;
				return true;
			}

			return false;
		}

		public override bool OnMouseMove(double x, double y, bool buttonFlag)
		{
			this.InverseTransformXY(ref x, ref y);
			if (!buttonFlag)
			{
				this.OnMouseButtonUp(x, y);
				return false;
			}

			if (this.mouseMove)
			{
				double xp = x + this.pdx;
				this.previewValue = (xp - this.xs1) / (this.xs2 - this.xs1);
				if (this.previewValue < 0.0)
				{
					this.previewValue = 0.0;
				}

				if (this.previewValue > 1.0)
				{
					this.previewValue = 1.0;
				}

				return true;
			}

			return false;
		}

		public override bool OnMouseButtonUp(double x, double y)
		{
			this.mouseMove = false;
			this.NormalizeValue(true);
			return true;
		}

		public override bool OnArrowKeys(bool left, bool right, bool down, bool up)
		{
			double d = 0.005;
			if (this.NumSteps != 0)
			{
				d = 1.0 / this.NumSteps;
			}

			if (right || up)
			{
				this.previewValue += d;
				if (this.previewValue > 1.0)
				{
					this.previewValue = 1.0;
				}

				this.NormalizeValue(true);
				return true;
			}

			if (left || down)
			{
				this.previewValue -= d;
				if (this.previewValue < 0.0)
				{
					this.previewValue = 0.0;
				}

				this.NormalizeValue(true);
				return true;
			}

			return false;
		}

		/// <summary>C++ <c>slider_ctrl_impl::rewind</c> + <c>vertex</c> for one path.</summary>
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

				case 1: // triangle
					storage.MoveTo(this.X1, this.Y1);
					storage.LineTo(this.X2, this.Y1);
					if (this.Descending)
					{
						storage.LineTo(this.X1, this.Y2);
					}
					else
					{
						storage.LineTo(this.X2, this.Y2);
					}

					storage.LineTo(this.X1, this.Y1);
					return storage;

				case 2: // label
					string label = string.IsNullOrEmpty(this.Label) ? string.Empty : string.Format(CultureInfo.InvariantCulture, this.Label, this.Value);
					return StrokedText(label, this.X1, this.Y1, (this.Y2 - this.Y1) * 1.2, this.Y2 - this.Y1, this.TextThickness);

				case 3: // pointer preview
					return new Ellipse(this.xs1 + ((this.xs2 - this.xs1) * this.previewValue), (this.ys1 + this.ys2) / 2.0, this.Y2 - this.Y1, this.Y2 - this.Y1, 32);

				case 4: // pointer
					this.NormalizeValue(false);
					return new Ellipse(this.xs1 + ((this.xs2 - this.xs1) * this.value), (this.ys1 + this.ys2) / 2.0, this.Y2 - this.Y1, this.Y2 - this.Y1, 32);

				default: // ticks
					if (this.NumSteps != 0)
					{
						double d = (this.xs2 - this.xs1) / this.NumSteps;
						if (d > 0.004)
						{
							d = 0.004;
						}

						for (int i = 0; i < this.NumSteps + 1; i++)
						{
							double x = this.xs1 + ((this.xs2 - this.xs1) * i / this.NumSteps);
							storage.MoveTo(x, this.Y1);
							storage.LineTo(x - (d * (this.X2 - this.X1)), this.Y1 - this.borderExtra);
							storage.LineTo(x + (d * (this.X2 - this.X1)), this.Y1 - this.borderExtra);
						}
					}

					return storage;
			}
		}

		/// <summary>C++ <c>normalize_value</c>: snaps the preview to a step and commits it.</summary>
		private bool NormalizeValue(bool previewValueFlag)
		{
			bool changed = true;
			if (this.NumSteps != 0)
			{
				int step = (int)((this.previewValue * this.NumSteps) + 0.5);
				changed = this.value != step / (double)this.NumSteps;
				this.value = step / (double)this.NumSteps;
			}
			else
			{
				this.value = this.previewValue;
			}

			if (previewValueFlag)
			{
				this.previewValue = this.value;
			}

			return changed;
		}
	}
}
