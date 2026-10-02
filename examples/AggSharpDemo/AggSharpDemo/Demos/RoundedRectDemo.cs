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

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's rounded_rect.cpp: a 1-pixel stroked rounded rectangle between two draggable handles, with
	/// sliders for the corner radius and a subpixel offset and a white-on-black checkbox.
	/// </summary>
	public class RoundedRectDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly double[] handleX = { 100, 500 };

		private readonly double[] handleY = { 100, 350 };

		private double dragDx;

		private double dragDy;

		private int dragIndex = -1;

		public RoundedRectDemo()
		{
			// rounded_rect.cpp runs with flip_y = true and gives its sliders !flip_y; the cbox keeps its
			// default flip_y = false.
			this.RadiusSlider = new SliderCtrl(10, 10, 600 - 10, 19, false)
			{
				Label = "radius={0,4:F3}",
			};
			this.RadiusSlider.SetRange(0.0, 50.0);
			this.RadiusSlider.Value = 25.0;

			this.OffsetSlider = new SliderCtrl(10, 10 + 20, 600 - 10, 19 + 20, false)
			{
				Label = "subpixel offset={0,4:F3}",
			};
			this.OffsetSlider.SetRange(-2.0, 3.0);

			this.WhiteOnBlack = new CboxCtrl(10, 10 + 40, "White on black")
			{
				TextColor = SrgbLut.FromSrgba8(127, 127, 127),
				InactiveColor = SrgbLut.FromSrgba8(127, 127, 127),
			};

			this.ctrls.Add(this.RadiusSlider);
			this.ctrls.Add(this.OffsetSlider);
			this.ctrls.Add(this.WhiteOnBlack);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_radius</c>.</summary>
		public SliderCtrl RadiusSlider { get; }

		/// <summary>C++ <c>m_offset</c>: moves the rectangle by a fraction of a pixel.</summary>
		public SliderCtrl OffsetSlider { get; }

		/// <summary>C++ <c>m_white_on_black</c>.</summary>
		public CboxCtrl WhiteOnBlack { get; }

		public override string Name => "rounded_rect";

		public override string Category => "Shapes";

		public override string Description => "A 1-pixel rounded rectangle. Drag its two handles to resize it.";

		public override int Width => 600;

		public override int Height => 400;

		/// <summary>Moves handle <paramref name="index"/> (0 or 1) to (<paramref name="x"/>, <paramref name="y"/>).</summary>
		public void SetHandle(int index, double x, double y)
		{
			this.handleX[index] = x;
			this.handleY[index] = y;
			this.Invalidate();
		}

		public override void Draw(Graphics2D graphics)
		{
			bool whiteOnBlack = this.WhiteOnBlack.Checked;
			graphics.FillRectangle(0, 0, this.Width, this.Height, whiteOnBlack ? Color.Black : Color.White);

			// The two "control" circles.
			Color handleColor = SrgbLut.FromSrgba8(127, 127, 127);
			graphics.Render(new Ellipse(this.handleX[0], this.handleY[0], 3, 3, 16), handleColor);
			graphics.Render(new Ellipse(this.handleX[1], this.handleY[1], 3, 3, 16), handleColor);

			double d = this.OffsetSlider.Value;
			var rect = new RoundedRect(this.handleX[0] + d, this.handleY[0] + d, this.handleX[1] + d, this.handleY[1] + d, this.RadiusSlider.Value);
			rect.normalize_radius();
			graphics.Render(new Stroke(rect, 1.0), whiteOnBlack ? Color.White : Color.Black);

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button) || !button.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			for (int i = 0; i < 2; i++)
			{
				double dx = x - this.handleX[i];
				double dy = y - this.handleY[i];
				if (Math.Sqrt((dx * dx) + (dy * dy)) < 5.0)
				{
					this.dragDx = dx;
					this.dragDy = dy;
					this.dragIndex = i;
					break;
				}
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseMove(x, y, flags))
			{
				return;
			}

			if (flags.HasFlag(AggInputFlags.MouseLeft))
			{
				if (this.dragIndex >= 0)
				{
					this.SetHandle(this.dragIndex, x - this.dragDx, y - this.dragDy);
				}
			}
			else
			{
				this.dragIndex = -1;
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			this.dragIndex = -1;
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}
	}
}
