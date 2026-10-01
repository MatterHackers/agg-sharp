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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's lion_lens.cpp: the lion seen through a magnifying lens (trans_warp_magnifier). The lion is cut
	/// into unit-length pieces (conv_segmentator) so its edges bend around the lens. Left-drag moves the lens;
	/// the sliders set its magnification and on-screen radius.
	/// </summary>
	public class LionLensDemo : AggDemo
	{
		private readonly LionShape lion = new LionShape();

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		// lion_lens.cpp on_init puts the lens at 200, 150.
		private double lensX = 200;

		private double lensY = 150;

		public LionLensDemo()
		{
			// lion_lens.cpp runs with flip_y = true and gives its sliders !flip_y.
			this.MagnificationSlider = new SliderCtrl(5, 5, 495, 12, false)
			{
				Label = "Scale={0,3:F2}",
			};
			this.MagnificationSlider.SetRange(0.01, 4.0);
			this.MagnificationSlider.Value = 3.0;

			this.RadiusSlider = new SliderCtrl(5, 20, 495, 27, false)
			{
				Label = "Radius={0,3:F2}",
			};
			this.RadiusSlider.SetRange(0.0, 100.0);
			this.RadiusSlider.Value = 70.0;

			this.ctrls.Add(this.MagnificationSlider);
			this.ctrls.Add(this.RadiusSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_magn_slider</c>: the lens magnification.</summary>
		public SliderCtrl MagnificationSlider { get; }

		/// <summary>C++ <c>m_radius_slider</c>: the lens radius on screen (the source radius is this over the magnification).</summary>
		public SliderCtrl RadiusSlider { get; }

		public override string Name => "lion_lens";

		public override string Category => "Transforms";

		public override string Description => "The lion under a magnifying lens that bends the plane around it. Left-drag to move the lens; the sliders set its magnification and radius.";

		public override int Width => 500;

		public override int Height => 600;

		/// <summary>Moves the lens, as a left press or drag does.</summary>
		public void SetLensCenter(double x, double y)
		{
			this.lensX = x;
			this.lensY = y;
			this.Invalidate();
		}

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			var lens = new TransWarpMagnifier
			{
				Magnification = this.MagnificationSlider.Value,
				Radius = this.RadiusSlider.Value / this.MagnificationSlider.Value,
			};
			lens.SetCenter(this.lensX, this.lensY);

			// Unlike lion.cpp, lion_lens.cpp neither scales nor skews, and centers on width / 2 as a double.
			Affine transform = Affine.NewIdentity();
			transform *= Affine.NewTranslation(-this.lion.Center.X, -this.lion.Center.Y);
			transform *= Affine.NewRotation(System.Math.PI);
			transform *= Affine.NewTranslation(this.Width / 2.0, this.Height / 2.0);

			foreach (var shape in this.lion.Shapes)
			{
				var pieces = new Segmentator(shape.VertexStorage);
				graphics.Render(new VertexSourceApplyTransform(new VertexSourceApplyTransform(pieces, transform), lens), shape.Color);
			}

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
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

		/// <summary>
		/// lion_lens.cpp on_mouse_button_down, which its on_mouse_move also calls. Its right button stores a
		/// point nothing reads, so only the left does anything.
		/// </summary>
		private void Manipulate(int x, int y, AggInputFlags flags)
		{
			if (flags.HasFlag(AggInputFlags.MouseLeft))
			{
				this.SetLensCenter(x, y);
			}
		}
	}
}
