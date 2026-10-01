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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's alpha_mask2.cpp: everything but the slider drawn through a gray8 alpha mask of N random light
	/// ellipses - the lion, then 50 random Bresenham lines and markers, 50 anti-aliased lines with round caps
	/// and 50 small radial-gradient discs. The slider sets N; left-drag rotates and scales the lion about the
	/// middle, right-drag skews it.
	/// </summary>
	/// <remarks>
	/// The random values come from <see cref="MsvcRand"/> seeded 1432, as in the C++ reference render
	/// (demo_alpha_mask2.cpp), each taken in argument order. C++ reseeds only when it rebuilds the mask and
	/// carries the sequence on across redraws, so its lines and markers jump on every redraw; here each frame
	/// replays the first frame's sequence, so they hold still while the lion moves.
	/// <para>
	/// On the GPU the scene is drawn through <see cref="IAlphaMaskGraphics.DrawMasked"/>: the Bresenham lines and
	/// markers as renderer_markers' own pixels, the anti-aliased lines as round-capped strokes and the discs
	/// through <see cref="IGradientFillGraphics"/> (see <see cref="LionRandomShapes.DrawOnGraphics"/>).
	/// </para>
	/// </remarks>
	public class AlphaMask2Demo : AggDemo
	{
		private readonly LionShape lion = new LionShape();

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private double angle;

		private double scale = 1.0;

		private double skewX;

		private double skewY;

		public AlphaMask2Demo()
		{
			// alpha_mask2.cpp runs with flip_y = true and gives its slider !flip_y.
			this.NumEllipsesSlider = new SliderCtrl(5, 5, 150, 12, false)
			{
				Label = "N={0:F2}",
			};
			this.NumEllipsesSlider.SetRange(5, 100);
			this.NumEllipsesSlider.Value = 10;

			this.ctrls.Add(this.NumEllipsesSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_num_cb</c>: how many ellipses make up the mask.</summary>
		public SliderCtrl NumEllipsesSlider { get; }

		public override string Name => "alpha_mask2";

		public override string Category => "Masks & Clipping";

		public override string Description => "The lion, random lines, markers and gradient dots seen through an alpha mask of random ellipses. Drag the slider to change the mask; left-drag to rotate and scale the lion, right-drag to skew it.";

		public override int Width => 512;

		public override int Height => 400;

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			var random = new MsvcRand(1432);
			ImageBuffer mask = this.GenerateMask(random);

			Affine transform = this.lion.GetDemoTransform(this.Width, this.Height, this.angle, this.scale, this.skewX, this.skewY);

			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-screen CPU layer. The reference frame has no transform, so pixels are frame pixels.
			if (graphics.Rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				var masked = new ImageClippingProxy(new AlphaMaskAdaptor(destination, new AlphaMaskByteUnclipped(mask, 1, 0)));
				LionRandomShapes.DrawThroughRenderer(masked, this.lion, random, transform, this.Width, this.Height);
				destination.MarkImageChanged();
			}
			else if (graphics is IAlphaMaskGraphics maskGraphics)
			{
				// The mask is in frame pixels, which DrawMasked places through the demo's transform.
				maskGraphics.DrawMasked(mask, () => LionRandomShapes.DrawOnGraphics(graphics, this.lion, random, transform, this.Width, this.Height));
			}
			else
			{
				LionRandomShapes.DrawOnGraphics(graphics, this.lion, random, transform, this.Width, this.Height);
			}

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			// C++ hands on_mouse_button_down the pressed button, so a press rotates or skews by that button alone.
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
		/// alpha_mask2.cpp generate_alpha_mask: N ellipses of random position and radii, each a random light gray
		/// at a random alpha of at least half, drawn into a cleared gray8 buffer the size of the demo.
		/// </summary>
		/// <remarks>
		/// Rebuilt every frame (C++ rebuilds it only when the slider moves) because the lines and markers carry
		/// on the random sequence the mask starts, and it costs far less than the frame.
		/// </remarks>
		private ImageBuffer GenerateMask(MsvcRand random)
		{
			// C++ pixfmt_sgray8 blends with gray8::lerp, which BlenderGrayExact is.
			var mask = new ImageBuffer(this.Width, this.Height, 8, new BlenderGrayExact(1));
			Graphics2D maskGraphics = mask.NewGraphics2D();

			// C++ clips only in the renderer, not the rasterizer.
			maskGraphics.Rasterizer.reset_clipping();

			int count = (int)this.NumEllipsesSlider.Value;
			for (int i = 0; i < count; i++)
			{
				int x = random.Next() % this.Width;
				int y = random.Next() % this.Height;
				int rx = (random.Next() % 100) + 20;
				int ry = (random.Next() % 100) + 20;
				int gray = (random.Next() & 127) + 128;
				int alpha = (random.Next() & 127) + 128;
				maskGraphics.Render(new Ellipse(x, y, rx, ry, 100), new Color(gray, gray, gray, alpha));
			}

			return mask;
		}

		/// <summary>alpha_mask2.cpp on_mouse_button_down, which its on_mouse_move also calls.</summary>
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
