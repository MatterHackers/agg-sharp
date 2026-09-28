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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's trans_polar.cpp: three sliders, and the first of them drawn a second time wrapped around a
	/// circle by a polar transform. The slider's paths are cut into unit-length pieces (conv_segmentator) so its
	/// straight edges bend. The spiral slider turns the circle into a spiral; base y sets its radius.
	/// </summary>
	public class TransPolarDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		public TransPolarDemo()
		{
			// trans_polar.cpp runs with flip_y = true and gives its sliders !flip_y.
			this.ValueSlider = new SliderCtrl(10, 10, 600 - 10, 17, false) { Label = "Some Value={0:F0}", NumSteps = 5 };
			this.ValueSlider.SetRange(0.0, 100.0);
			this.ValueSlider.Value = 32.0;

			this.SpiralSlider = new SliderCtrl(10, 10 + 20, 600 - 10, 17 + 20, false) { Label = "Spiral={0:F3}" };
			this.SpiralSlider.SetRange(-0.1, 0.1);
			this.SpiralSlider.Value = 0.0;

			this.BaseYSlider = new SliderCtrl(10, 10 + 40, 600 - 10, 17 + 40, false) { Label = "Base Y={0:F3}" };
			this.BaseYSlider.SetRange(50.0, 200.0);
			this.BaseYSlider.Value = 120.0;

			this.ctrls.Add(this.ValueSlider);
			this.ctrls.Add(this.SpiralSlider);
			this.ctrls.Add(this.BaseYSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_slider1</c>: a slider with nothing behind it, the one drawn wrapped around the circle.</summary>
		public SliderCtrl ValueSlider { get; }

		/// <summary>C++ <c>m_slider_spiral</c>: how far the radius grows along the circle.</summary>
		public SliderCtrl SpiralSlider { get; }

		/// <summary>C++ <c>m_slider_base_y</c>: the circle's radius, added to every y before the transform.</summary>
		public SliderCtrl BaseYSlider { get; }

		public override string Name => "trans_polar";

		public override string Category => "Transformations";

		public override string Description => "A slider wrapped around a circle by a polar transform. Drag the top slider to move its wrapped twin; the other sliders set the spiral and the radius.";

		public override int Width => 600;

		public override int Height => 400;

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			this.ctrls.Render(graphics);

			var polar = new TransPolar
			{
				BaseAngle = 2.0 * Math.PI / -600.0,
				BaseScale = -1.0,
				BaseY = this.BaseYSlider.Value,
				TranslationX = this.Width / 2.0,
				TranslationY = (this.Height / 2.0) + 30.0,
				Spiral = -this.SpiralSlider.Value,
			};

			// C++ transformed_control: the slider's own paths and colors, through conv_segmentator and the polar
			// transform.
			for (int i = 0; i < this.ValueSlider.NumPaths; i++)
			{
				var pieces = new Segmentator(this.ValueSlider.PathSource(i));
				graphics.Render(new VertexSourceApplyTransform(pieces, polar), this.ValueSlider.PathColor(i));
			}
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseDown(x, y, button);
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			this.ctrls.OnMouseMove(x, y, flags);
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		/// <summary>
		/// trans_polar.cpp's trans_polar: x becomes an angle (times <see cref="BaseAngle"/>) and y a radius, the
		/// radius growing by <see cref="Spiral"/> per unit of x.
		/// </summary>
		private sealed class TransPolar : ITransform
		{
			public double BaseAngle { get; set; } = 1.0;

			public double BaseScale { get; set; } = 1.0;

			public double BaseX { get; set; }

			public double BaseY { get; set; }

			public double TranslationX { get; set; }

			public double TranslationY { get; set; }

			public double Spiral { get; set; }

			public void Transform(ref double x, ref double y)
			{
				double x1 = (x + this.BaseX) * this.BaseAngle;
				double y1 = ((y + this.BaseY) * this.BaseScale) + (x * this.Spiral);
				x = (Math.Cos(x1) * y1) + this.TranslationX;
				y = (Math.Sin(x1) * y1) + this.TranslationY;
			}
		}
	}
}
