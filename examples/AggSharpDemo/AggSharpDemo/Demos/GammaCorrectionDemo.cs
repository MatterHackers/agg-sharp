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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's gamma_correction.cpp: five thin concentric ellipses over dark, light and red backgrounds,
	/// drawn through a gamma-correcting pixel format, with the gamma's power curve above them. Drag anywhere to
	/// resize the ellipses.
	/// </summary>
	public class GammaCorrectionDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private double rx;

		private double ry;

		public GammaCorrectionDemo()
		{
			// gamma_correction.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.ThicknessSlider = new SliderCtrl(5, 5, 400 - 5, 11, false) { Label = "Thickness={0:F2}" };
			this.ThicknessSlider.SetRange(0.0, 3.0);
			this.ThicknessSlider.Value = 1.0;

			this.ContrastSlider = new SliderCtrl(5, 5 + 15, 400 - 5, 11 + 15, false) { Label = "Contrast" };
			this.ContrastSlider.SetRange(0.0, 1.0);
			this.ContrastSlider.Value = 1.0;

			this.GammaSlider = new SliderCtrl(5, 5 + 30, 400 - 5, 11 + 30, false) { Label = "Gamma={0:F2}" };
			this.GammaSlider.SetRange(0.5, 3.0);
			this.GammaSlider.Value = 1.0;

			this.ctrls.Add(this.ThicknessSlider);
			this.ctrls.Add(this.ContrastSlider);
			this.ctrls.Add(this.GammaSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			// C++ on_init.
			this.rx = this.Width / 3.0;
			this.ry = this.Height / 3.0;
		}

		/// <summary>C++ <c>m_thickness</c>: the ellipses' stroke width.</summary>
		public SliderCtrl ThicknessSlider { get; }

		/// <summary>C++ <c>m_contrast</c>: how far apart the dark and light backgrounds are.</summary>
		public SliderCtrl ContrastSlider { get; }

		/// <summary>C++ <c>m_gamma</c>: the pixel format's gamma.</summary>
		public SliderCtrl GammaSlider { get; }

		public override string Name => "gamma_correction";

		public override string Category => "Rendering";

		public override string Description => "Thin ellipses blended through a gamma-correcting pixel format. Change the thickness, contrast and gamma, or drag to resize the ellipses.";

		public override int Width => 400;

		public override int Height => 320;

		/// <summary>C++ <c>m_rx</c>, <c>m_ry</c>: the outer ellipse's radii.</summary>
		public void SetRadii(double newRx, double newRy)
		{
			this.rx = newRx;
			this.ry = newRy;
			this.Invalidate();
		}

		public override void Draw(Graphics2D graphics)
		{
			double g = this.GammaSlider.Value;

			// C++ draws everything through pixfmt_bgr24_gamma. On a software image the port swaps the frame's
			// blender for the same blend for the length of the draw; elsewhere (the GPU, whose DestImage is a
			// separate CPU layer) it draws without it. DestImage is the graphics' clipping proxy, which passes the
			// blender through to the frame.
			IImageByte image = graphics.Rasterizer != null && graphics.DestImage?.BitDepth == 32 ? graphics.DestImage : null;
			IRecieveBlenderByte previousBlender = image?.GetRecieveBlender();
			image?.SetRecieveBlender(new BlenderGammaBGRA(new GammaLookUpTable(g)));
			try
			{
				this.DrawScene(graphics, g);
			}
			finally
			{
				image?.SetRecieveBlender(previousBlender);
			}
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button))
			{
				return;
			}

			if (button.HasFlag(AggInputFlags.MouseLeft))
			{
				this.SetRadii(Math.Abs((this.Width / 2.0) - x), Math.Abs((this.Height / 2.0) - y));
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
				this.SetRadii(Math.Abs((this.Width / 2.0) - x), Math.Abs((this.Height / 2.0) - y));
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

		private void DrawScene(Graphics2D graphics, double g)
		{
			int width = this.Width;
			int height = this.Height;
			graphics.FillRectangle(0, 0, width, height, Color.White);

			double dark = 1.0 - this.ContrastSlider.Value;
			double light = this.ContrastSlider.Value;

			// C++ copy_bar takes inclusive corners, clipped to the image.
			CopyBar(graphics, 0, 0, width / 2, height, Rgba8.FromRgba(dark, dark, dark));
			CopyBar(graphics, (width / 2) + 1, 0, width, height, Rgba8.FromRgba(light, light, light));
			CopyBar(graphics, 0, (height / 2) + 1, width, height, Rgba8.FromRgba(1.0, dark, dark));

			// The gamma's power curve.
			var curve = new VertexStorage();
			double x = (width - 256.0) / 2.0;
			double y = 50.0;
			var power = new gamma_power(g);
			for (int i = 0; i < 256; i++)
			{
				double dy = power.GetGamma(i / 255.0) * 255.0;
				if (i == 0)
				{
					curve.MoveTo(x + i, y + dy);
				}
				else
				{
					curve.LineTo(x + i, y + dy);
				}
			}

			graphics.Render(new Stroke(curve, 2.0), SrgbLut.FromSrgba8(80, 127, 80));

			double thickness = this.ThicknessSlider.Value;
			StrokeEllipse(graphics, this.rx, this.ry, thickness, SrgbLut.FromSrgba8(255, 0, 0));
			StrokeEllipse(graphics, this.rx - 5.0, this.ry - 5.0, thickness, SrgbLut.FromSrgba8(0, 255, 0));
			StrokeEllipse(graphics, this.rx - 10.0, this.ry - 10.0, thickness, SrgbLut.FromSrgba8(0, 0, 255));
			StrokeEllipse(graphics, this.rx - 15.0, this.ry - 15.0, thickness, SrgbLut.FromSrgba8(0, 0, 0));
			StrokeEllipse(graphics, this.rx - 20.0, this.ry - 20.0, thickness, SrgbLut.FromSrgba8(255, 255, 255));

			this.ctrls.Render(graphics);
		}

		private void StrokeEllipse(Graphics2D graphics, double radiusX, double radiusY, double thickness, Color color)
		{
			graphics.Render(new Stroke(new Ellipse(this.Width / 2.0, this.Height / 2.0, radiusX, radiusY, 150), thickness), color);
		}

		private static void CopyBar(Graphics2D graphics, int x1, int y1, int x2, int y2, Color color)
		{
			graphics.FillRectangle(x1, y1, x2 + 1, y2 + 1, color);
		}
	}
}
