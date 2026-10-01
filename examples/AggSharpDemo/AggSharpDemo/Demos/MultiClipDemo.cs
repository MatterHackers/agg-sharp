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

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's multi_clip.cpp: the lion, 50 random Bresenham lines and markers, 50 anti-aliased lines with
	/// round caps and 50 small radial-gradient discs, all clipped to an N x N grid of boxes through
	/// <see cref="ImageMultiClipProxy"/> (C++ renderer_mclip). The slider sets N; left-drag rotates and scales
	/// the lion about the middle, right-drag skews it.
	/// </summary>
	/// <remarks>
	/// The random values come from <see cref="MsvcRand"/> seeded 1, as C's unseeded rand() is and as in the C++
	/// reference render (demo_multi_clip.cpp), each taken in argument order. C++ carries the sequence on across
	/// redraws, so its lines and markers jump on every redraw; here each frame replays the first frame's
	/// sequence, so they hold still while the lion moves.
	/// <para>
	/// On the GPU the scene is drawn once per box with the box as the clip rect, which clips as the boxes do.
	/// What is drawn is the GPU stand-in the alpha_mask2 port uses (<see cref="LionRandomShapes.DrawOnGraphics"/>):
	/// the Bresenham lines and markers as renderer_markers' own pixels, the anti-aliased lines as round-capped
	/// strokes and the discs through <see cref="IGradientFillGraphics"/>.
	/// </para>
	/// </remarks>
	public class MultiClipDemo : AggDemo
	{
		private readonly LionShape lion = new LionShape();

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private double angle;

		private double scale = 1.0;

		private double skewX;

		private double skewY;

		public MultiClipDemo()
		{
			// multi_clip.cpp runs with flip_y = true and gives its slider !flip_y. C++ never sets its value, so
			// it starts at the slider's middle, 6.
			this.NumBoxesSlider = new SliderCtrl(5, 5, 150, 12, false)
			{
				Label = "N={0:F2}",
			};
			this.NumBoxesSlider.SetRange(2, 10);
			this.NumBoxesSlider.Value = 6;

			this.ctrls.Add(this.NumBoxesSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_num_cb</c>: the grid of clip boxes is N x N.</summary>
		public SliderCtrl NumBoxesSlider { get; }

		public override string Name => "multi_clip";

		public override string Category => "Masks & Clipping";

		public override string Description => "The lion, random lines, markers and gradient dots clipped to a grid of boxes. Drag the slider to change the grid; left-drag to rotate and scale the lion, right-drag to skew it.";

		public override int Width => 512;

		public override int Height => 400;

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			Affine transform = this.lion.GetDemoTransform(this.Width, this.Height, this.angle, this.scale, this.skewX, this.skewY);

			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-screen CPU layer. The reference frame has no transform, so pixels are frame pixels.
			if (graphics.Rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				var clipped = new ImageMultiClipProxy(destination);
				clipped.reset_clipping(false);
				this.ForEachBox((x1, y1, x2, y2) => clipped.add_clip_box(x1, y1, x2, y2));

				LionRandomShapes.DrawThroughRenderer(clipped, this.lion, new MsvcRand(), transform, this.Width, this.Height);
				destination.MarkImageChanged();
			}
			else
			{
				// The clip rect is in screen space, so each box goes through the demo's transform; the boxes stay
				// inside the demo's own clip.
				RectangleDouble savedClip = graphics.GetClippingRect();
				Affine demoToScreen = graphics.GetTransform();
				this.ForEachBox((x1, y1, x2, y2) =>
				{
					// A clip box's corners are inclusive pixels; the clip rect's right and top are exclusive edges.
					double left = x1, bottom = y1, right = x2 + 1, top = y2 + 1;
					demoToScreen.Transform(ref left, ref bottom);
					demoToScreen.Transform(ref right, ref top);
					var box = new RectangleDouble(Math.Min(left, right), Math.Min(bottom, top), Math.Max(left, right), Math.Max(bottom, top));
					if (x2 >= x1 && y2 >= y1 && box.IntersectWithRectangle(savedClip))
					{
						graphics.SetClippingRect(box);
						LionRandomShapes.DrawOnGraphics(graphics, this.lion, new MsvcRand(), transform, this.Width, this.Height);
					}
				});
				graphics.SetClippingRect(savedClip);
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
		/// multi_clip.cpp's grid: for x and y from 0 while below N (a fractional N adds a part column and row that
		/// runs off the window), the cell's box inset 5 pixels, as inclusive pixel corners.
		/// </summary>
		private void ForEachBox(Action<int, int, int, int> addBox)
		{
			double n = this.NumBoxesSlider.Value;
			int width = this.Width;
			int height = this.Height;
			for (int x = 0; x < n; x++)
			{
				for (int y = 0; y < n; y++)
				{
					int x1 = (int)(width * x / n);
					int y1 = (int)(height * y / n);
					int x2 = (int)(width * (x + 1) / n);
					int y2 = (int)(height * (y + 1) / n);
					addBox(x1 + 5, y1 + 5, x2 - 5, y2 - 5);
				}
			}
		}

		/// <summary>multi_clip.cpp on_mouse_button_down, which its on_mouse_move also calls.</summary>
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
