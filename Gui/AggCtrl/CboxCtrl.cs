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

using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// C++ AGG's <c>cbox_ctrl</c>, drawn and driven exactly as C++ does: a square border, the label (gsv_text
	/// stroked with round joins and caps) and, while checked, the X inside the square. A press anywhere in the
	/// square toggles it.
	/// </summary>
	public class CboxCtrl : AggCtrl
	{
		public CboxCtrl(double x, double y, string label, bool flipY = false)
			: base(x, y, x + (9.0 * 1.5), y + (9.0 * 1.5), flipY)
		{
			this.Label = label;
		}

		public string Label { get; set; }

		/// <summary>C++ <c>status</c>: whether the box is checked.</summary>
		public bool Checked { get; set; }

		public double TextThickness { get; set; } = 1.5;

		public double TextHeight { get; private set; } = 9.0;

		/// <summary>0 lets gsv_text pick the width from the height.</summary>
		public double TextWidth { get; private set; }

		public Color TextColor { get; set; } = Rgba8.FromRgba(0.0, 0.0, 0.0);

		public Color InactiveColor { get; set; } = Rgba8.FromRgba(0.0, 0.0, 0.0);

		public Color ActiveColor { get; set; } = Rgba8.FromRgba(0.4, 0.0, 0.0);

		public override int NumPaths => 3;

		/// <summary>C++ <c>text_size</c>.</summary>
		public void SetTextSize(double height, double width = 0.0)
		{
			this.TextHeight = height;
			this.TextWidth = width;
		}

		public override Color PathColor(int index)
		{
			switch (index)
			{
				case 0: return this.InactiveColor;
				case 1: return this.TextColor;
				default: return this.ActiveColor;
			}
		}

		public override bool InRect(double x, double y)
		{
			this.InverseTransformXY(ref x, ref y);
			return x >= this.X1 && y >= this.Y1 && x <= this.X2 && y <= this.Y2;
		}

		public override bool OnMouseButtonDown(double x, double y)
		{
			this.InverseTransformXY(ref x, ref y);
			if (x >= this.X1 && y >= this.Y1 && x <= this.X2 && y <= this.Y2)
			{
				this.Checked = !this.Checked;
				return true;
			}

			return false;
		}

		public override bool OnMouseMove(double x, double y, bool buttonFlag) => false;

		public override bool OnMouseButtonUp(double x, double y) => false;

		public override bool OnArrowKeys(bool left, bool right, bool down, bool up) => false;

		/// <summary>C++ <c>cbox_ctrl_impl::rewind</c> + <c>vertex</c> for one path.</summary>
		protected override IVertexSource Path(int index)
		{
			var storage = new VertexStorage();
			double t = this.TextThickness;
			switch (index)
			{
				case 1: // text
					return StrokedText(this.Label, this.X1 + (this.TextHeight * 2.0), this.Y1 + (this.TextHeight / 5.0), this.TextHeight, this.TextWidth, t);

				case 2: // active item: the X, only while checked
					if (this.Checked)
					{
						double d2 = (this.Y2 - this.Y1) / 2.0;
						double t15 = t * 1.5;
						storage.MoveTo(this.X1 + t, this.Y1 + t);
						storage.LineTo(this.X1 + d2, this.Y1 + d2 - t15);
						storage.LineTo(this.X2 - t, this.Y1 + t);
						storage.LineTo(this.X1 + d2 + t15, this.Y1 + d2);
						storage.LineTo(this.X2 - t, this.Y2 - t);
						storage.LineTo(this.X1 + d2, this.Y1 + d2 + t15);
						storage.LineTo(this.X1 + t, this.Y2 - t);
						storage.LineTo(this.X1 + d2 - t15, this.Y1 + d2);
					}

					return storage;

				default: // border: the outer square and, wound the other way, the inner one
					storage.MoveTo(this.X1, this.Y1);
					storage.LineTo(this.X2, this.Y1);
					storage.LineTo(this.X2, this.Y2);
					storage.LineTo(this.X1, this.Y2);
					storage.MoveTo(this.X1 + t, this.Y1 + t);
					storage.LineTo(this.X1 + t, this.Y2 - t);
					storage.LineTo(this.X2 - t, this.Y2 - t);
					storage.LineTo(this.X2 - t, this.Y1 + t);
					return storage;
			}
		}
	}
}
