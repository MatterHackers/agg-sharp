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
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's alpha_mask.cpp: the lion drawn through a gray8 alpha mask of ten random ellipses, each a random
	/// gray at a random alpha. Left-drag rotates and scales the lion about the middle, right-drag skews it.
	/// </summary>
	/// <remarks>
	/// The ellipses come from <see cref="MsvcRand"/> rather than the platform's <c>rand()</c>, as they do in the
	/// C++ reference render (demo_alpha_mask.cpp). C++ regenerates the mask on every resize, carrying on the
	/// random sequence; the demo has a fixed size, so the mask is made once.
	/// <para>
	/// The GPU path draws the lion through the same gray8 mask with <see cref="IAlphaMaskGraphics.DrawMasked"/>; a
	/// surface with neither a byte back buffer nor alpha masks draws it unmasked.
	/// </para>
	/// </remarks>
	public class AlphaMaskDemo : AggDemo
	{
		private readonly LionShape lion = new LionShape();

		private ImageBuffer mask;

		private double angle;

		private double scale = 1.0;

		private double skewX;

		private double skewY;

		public override string Name => "alpha_mask";

		public override string Category => "Vector Graphics";

		public override string Description => "The lion seen through an alpha mask of random gray ellipses. Left-drag to rotate and scale it, right-drag to skew it.";

		public override int Width => 512;

		public override int Height => 400;

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			var transform = this.lion.GetDemoTransform(this.Width, this.Height, this.angle, this.scale, this.skewX, this.skewY);

			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-screen CPU layer.
			if (graphics.Rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				// C++ multiplies the mask into the scanline covers (scanline_u8_am); pixfmt_amask_adaptor does the
				// same product on the way to the pixels, so the covers - and the image - are the same.
				var maskedImage = new ImageClippingProxy(new AlphaMaskAdaptor(destination, new AlphaMaskByteClipped(this.GetMask(), 1, 0)));
				var masked = new ImageGraphics2D(maskedImage, new ScanlineRasterizer(), new ScanlineCachePacked8());
				masked.SetTransform(graphics.GetTransform());
				this.lion.Render(masked, transform, 255);
				destination.MarkImageChanged();
			}
			else if (graphics is IAlphaMaskGraphics maskGraphics)
			{
				// The mask is in frame pixels, which DrawMasked places through the demo's transform.
				maskGraphics.DrawMasked(this.GetMask(), () => this.lion.Render(graphics, transform, 255));
			}
			else
			{
				this.lion.Render(graphics, transform, 255);
			}
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.Manipulate(x, y, button);
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			this.Manipulate(x, y, flags);
		}

		/// <summary>
		/// alpha_mask.cpp generate_alpha_mask: ten ellipses of random position, radii, gray and alpha, drawn
		/// into a cleared gray8 buffer the size of the demo.
		/// </summary>
		private ImageBuffer GetMask()
		{
			if (this.mask != null)
			{
				return this.mask;
			}

			// C++ pixfmt_sgray8 blends with gray8::lerp, which BlenderGrayExact is.
			this.mask = new ImageBuffer(this.Width, this.Height, 8, new BlenderGrayExact(1));
			Graphics2D maskGraphics = this.mask.NewGraphics2D();

			// C++ clips only in the renderer, not the rasterizer.
			maskGraphics.Rasterizer.reset_clipping();

			var random = new MsvcRand();
			for (int i = 0; i < 10; i++)
			{
				int x = random.Next() % this.Width;
				int y = random.Next() % this.Height;
				int rx = (random.Next() % 100) + 20;
				int ry = (random.Next() % 100) + 20;
				int gray = random.Next() & 0xFF;
				int alpha = random.Next() & 0xFF;
				maskGraphics.Render(new Ellipse(x, y, rx, ry, 100), new Color(gray, gray, gray, alpha));
			}

			return this.mask;
		}

		/// <summary>alpha_mask.cpp on_mouse_button_down, which its on_mouse_move also calls.</summary>
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
