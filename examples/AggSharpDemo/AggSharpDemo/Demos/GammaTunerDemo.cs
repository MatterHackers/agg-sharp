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

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's gamma_tuner.cpp: a square of alternating translucent spans blended through a gamma-correcting
	/// pixel format beside solid strips of the gamma's own ramp, so the gamma that makes them match can be read
	/// off. Choose the color, the gamma and the span pattern.
	/// </summary>
	public class GammaTunerDemo : AggDemo
	{
		private const int SquareSize = 400;

		private const int VerticalStrips = 5;

		private static readonly byte[] FullCover = { 255 };

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		public GammaTunerDemo()
		{
			// gamma_tuner.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.RedSlider = new SliderCtrl(5, 5, 350 - 5, 11, false) { Label = "R={0:F2}" };
			this.RedSlider.Value = 1.0;

			this.GreenSlider = new SliderCtrl(5, 5 + 15, 350 - 5, 11 + 15, false) { Label = "G={0:F2}" };
			this.GreenSlider.Value = 1.0;

			this.BlueSlider = new SliderCtrl(5, 5 + 30, 350 - 5, 11 + 30, false) { Label = "B={0:F2}" };
			this.BlueSlider.Value = 1.0;

			this.GammaSlider = new SliderCtrl(5, 5 + 45, 350 - 5, 11 + 45, false) { Label = "Gamma={0:F2}" };
			this.GammaSlider.SetRange(0.5, 4.0);
			this.GammaSlider.Value = 2.2;

			// gamma_tuner.cpp defines AGG_SBGR24, so every ctrl is a ctrl<srgba8> drawn into an srgba8 pixel format:
			// the frame holds sRGB bytes, and each default color is the srgba8 conversion of the ctrl's rgba.
			this.PatternRbox = new RboxCtrl(355, 1, 495, 60, false)
			{
				BackgroundColor = SrgbLut.Srgba8FromRgba(1.0, 1.0, 0.9),
				BorderColor = SrgbLut.Srgba8FromRgba(0.0, 0.0, 0.0),
				TextColor = SrgbLut.Srgba8FromRgba(0.0, 0.0, 0.0),
				InactiveColor = SrgbLut.Srgba8FromRgba(0.0, 0.0, 0.0),
				ActiveColor = SrgbLut.Srgba8FromRgba(0.4, 0.0, 0.0),
			};
			foreach (SliderCtrl slider in new[] { this.RedSlider, this.GreenSlider, this.BlueSlider, this.GammaSlider })
			{
				slider.BackgroundColor = SrgbLut.Srgba8FromRgba(1.0, 0.9, 0.8);
				slider.TriangleColor = SrgbLut.Srgba8FromRgba(0.7, 0.6, 0.6);
				slider.TextColor = SrgbLut.Srgba8FromRgba(0.0, 0.0, 0.0);
				slider.PointerPreviewColor = SrgbLut.Srgba8FromRgba(0.6, 0.4, 0.4, 0.4);
				slider.PointerColor = SrgbLut.Srgba8FromRgba(0.8, 0.0, 0.0, 0.6);
			}

			this.PatternRbox.SetTextSize(8);
			this.PatternRbox.AddItem("Horizontal");
			this.PatternRbox.AddItem("Vertical");
			this.PatternRbox.AddItem("Checkered");
			this.PatternRbox.CurrentItem = 2;

			// C++ render_ctrl order: m_gamma, m_r, m_g, m_b, m_pattern.
			this.ctrls.Add(this.GammaSlider);
			this.ctrls.Add(this.RedSlider);
			this.ctrls.Add(this.GreenSlider);
			this.ctrls.Add(this.BlueSlider);
			this.ctrls.Add(this.PatternRbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_r</c>: the color's red.</summary>
		public SliderCtrl RedSlider { get; }

		/// <summary>C++ <c>m_g</c>: the color's green.</summary>
		public SliderCtrl GreenSlider { get; }

		/// <summary>C++ <c>m_b</c>: the color's blue.</summary>
		public SliderCtrl BlueSlider { get; }

		/// <summary>C++ <c>m_gamma</c>: the pixel format's gamma and the ramps' power.</summary>
		public SliderCtrl GammaSlider { get; }

		/// <summary>C++ <c>m_pattern</c>: Horizontal, Vertical or Checkered spans.</summary>
		public RboxCtrl PatternRbox { get; }

		public override string Name => "gamma_tuner";

		public override string Category => "Rendering";

		public override string Description => "Alternating translucent spans blended through a gamma-correcting pixel format beside solid strips. Change the gamma until they match.";

		public override int Width => 500;

		public override int Height => 500;

		public override void Draw(Graphics2D graphics)
		{
			double g = this.GammaSlider.Value;

			// C++ draws through pixfmt_sbgr24_gamma with copy_hline and blend_color_hspan. On a software image the
			// port writes into the graphics' clipping proxy (C++ renderer_base) with the frame's blender swapped
			// for the gamma blend for the length of the draw, ctrls included, as C++ render_ctrl blends them
			// through it too.
			if (graphics.Rasterizer != null && graphics.DestImage is ImageClippingProxy frame && frame.BitDepth == 32)
			{
				IRecieveBlenderByte previousBlender = frame.GetRecieveBlender();
				frame.SetRecieveBlender(new BlenderGammaBGRA(new GammaLookUpTable(g)));
				try
				{
					this.DrawPattern(
						g,
						(x1, y, x2, c) => frame.copy_hline(x1, y, x2 - x1 + 1, c),
						(x, y, colors) => frame.blend_color_hspan(x, y, colors.Length, colors, 0, FullCover, 0, true));
					this.ctrls.Render(graphics);
				}
				finally
				{
					frame.SetRecieveBlender(previousBlender);
				}
			}
			else
			{
				// The GPU draws the same rows and span pixels as rectangles, blended without the gamma: gamma blending on
				// the GPU is planned GPU work (a gamma-aware blend in Graphics2DGpu), not a CPU image blitted here.
				this.DrawPattern(
					g,
					(x1, y, x2, c) => graphics.FillRectangle(x1, y, x2 + 1, y + 1, c),
					(x, y, colors) =>
					{
						for (int i = 0; i < colors.Length; i++)
						{
							if (colors[i].alpha != 0)
							{
								graphics.FillRectangle(x + i, y, x + i + 1, y + 1, colors[i]);
							}
						}
					});
				this.ctrls.Render(graphics);
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
		/// C++ on_draw up to the ctrls, through <paramref name="copyHline"/> (C++ copy_hline, x1..x2 inclusive) and
		/// <paramref name="blendSpan"/> (C++ blend_color_hspan at full cover). Colors are srgba8 bytes, and the
		/// gradients run on them as C++ srgba8::gradient does (the same integer maths as rgba8).
		/// </summary>
		private void DrawPattern(double gamma, Action<int, int, int, Color> copyHline, Action<int, int, Color[]> blendSpan)
		{
			Color color = SrgbLut.Srgba8FromRgba(this.RedSlider.Value, this.GreenSlider.Value, this.BlueSlider.Value);
			var black = new Color(0, 0, 0, 255);
			var span1 = new Color[SquareSize];
			var span2 = new Color[SquareSize];

			// The background ramp, flat above and below the square.
			for (int i = 0; i < this.Height; i++)
			{
				double k = (i - 80) / (double)(SquareSize - 1);
				if (i < 80)
				{
					k = 0.0;
				}

				if (i >= 80 + SquareSize)
				{
					k = 1.0;
				}

				k = 1 - Math.Pow(k / 2, 1 / gamma);
				copyHline(0, i, this.Width - 1, Rgba8.Gradient(color, black, k));
			}

			// The spans' alphas, C++ i * full_value() / square_size.
			for (int i = 0; i < SquareSize; i++)
			{
				int rising = i * 255 / SquareSize;
				int falling = 255 - rising;
				bool odd = (i & 1) != 0;
				int a1;
				int a2;
				switch (this.PatternRbox.CurrentItem)
				{
					case 0:
						a1 = rising;
						a2 = falling;
						break;

					case 1:
						a1 = a2 = odd ? rising : falling;
						break;

					default:
						a1 = odd ? rising : falling;
						a2 = odd ? falling : rising;
						break;
				}

				span1[i] = new Color(color, a1);
				span2[i] = new Color(color, a2);
			}

			// C++ copy_bar.
			for (int y = 80; y < 80 + SquareSize; y++)
			{
				copyHline(50, y, 50 + SquareSize - 1, black);
			}

			// The pattern: each pair of rows in the ramp's color, the spans keeping their alphas.
			for (int i = 0; i < SquareSize; i += 2)
			{
				double k = i / (double)(SquareSize - 1);
				k = 1 - Math.Pow(k, 1 / gamma);
				Color c = Rgba8.Gradient(color, black, k);
				for (int j = 0; j < SquareSize; j++)
				{
					span1[j] = new Color(c, span1[j].alpha);
					span2[j] = new Color(c, span2[j].alpha);
				}

				blendSpan(50, i + 80, span1);
				blendSpan(50, i + 80 + 1, span2);
			}

			// The solid strips.
			for (int i = 0; i < SquareSize; i++)
			{
				double k = i / (double)(SquareSize - 1);
				k = 1 - Math.Pow(k / 2, 1 / gamma);
				Color c = Rgba8.Gradient(color, black, k);
				for (int j = 0; j < VerticalStrips; j++)
				{
					int xc = SquareSize * (j + 1) / (VerticalStrips + 1);
					copyHline(50 + xc - 10, i + 80, 50 + xc + 10, c);
				}
			}
		}
	}
}
