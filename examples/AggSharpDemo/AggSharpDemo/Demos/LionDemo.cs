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

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's lion.cpp: the filled lion, drawn at the alpha the slider sets. Left-drag rotates and scales
	/// it about the middle of the window, right-drag skews it.
	/// </summary>
	/// <remarks>
	/// One deliberate difference: C++ clears to white only on resize, so every redraw of its semi-transparent
	/// lion lands on the last one and the image darkens as you drag. This port fills white each frame, which
	/// is what C++ shows on its first frame (and what the golden is).
	/// </remarks>
	public class LionDemo : AggDemo
	{
		private readonly LionShape lion = new LionShape();

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private double angle;

		private double scale = 1.0;

		private double skewX;

		private double skewY;

		public LionDemo()
		{
			// lion.cpp runs with flip_y = true and gives its slider !flip_y.
			this.AlphaSlider = new SliderCtrl(5, 5, 512 - 5, 12, false)
			{
				Label = "Alpha{0,3:F3}",
				Value = 0.1,
			};
			this.ctrls.Add(this.AlphaSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>The alpha slider along the bottom, C++ <c>m_alpha_slider</c>.</summary>
		public SliderCtrl AlphaSlider { get; }

		public override string Name => "lion";

		public override string Category => "Shapes";

		public override string Description => "The classic AGG lion. Left-drag to rotate and scale it, right-drag to skew it; the slider sets its alpha.";

		public override int Width => 512;

		public override int Height => 400;

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			this.lion.Render(
				graphics,
				this.lion.GetDemoTransform(this.Width, this.Height, this.angle, this.scale, this.skewX, this.skewY),
				(byte)(this.AlphaSlider.Value * 255));

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			// C++ hands on_mouse_button_down the pressed button (X11 sends nothing else held), so a press
			// rotates or skews by that button alone.
			if (!this.ctrls.OnMouseDown(x, y, button))
			{
				this.Manipulate(x, y, button);
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (!this.ctrls.OnMouseMove(x, y, flags))
			{
				this.Manipulate(x, y, flags);
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		/// <summary>lion.cpp on_mouse_button_down, which its on_mouse_move also calls.</summary>
		private void Manipulate(int x, int y, AggInputFlags flags)
		{
			if (flags.HasFlag(AggInputFlags.MouseLeft))
			{
				double dx = x - (this.Width / 2.0);
				double dy = y - (this.Height / 2.0);
				this.angle = Math.Atan2(dy, dx);
				this.scale = Math.Sqrt((dy * dy) + (dx * dx)) / 100.0;
				this.Invalidate();
			}

			if (flags.HasFlag(AggInputFlags.MouseRight))
			{
				this.skewX = x;
				this.skewY = y;
				this.Invalidate();
			}
		}
	}
}
