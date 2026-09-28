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
	/// C++ AGG's polymorphic_renderer.cpp: one triangle drawn through a renderer picked at run time for the
	/// window's pixel format - 16-bit rgb555 by default, so the fill and its anti-aliased edges are quantised to
	/// 5 bits a channel.
	/// </summary>
	/// <remarks>
	/// C++ picks the format once, at startup; the port adds an rbox of the example's eight formats so the user
	/// can switch. The picked format is an <see cref="ImageBuffer"/> with that format's blender: the triangle
	/// and the rbox are drawn into it, and it is read back pixel by pixel (the blender's PixelToColor, C++
	/// pixf.pixel()) into the demo's frame, as demo_polymorphic_renderer.cpp writes its buffer out.
	/// The component order of the byte formats never changes a blended value, so rgb24 draws through the bgr24
	/// blender and argb32 / abgr32 through the bgra32 / rgba32 blenders; their pictures are the same as C++'s.
	/// </remarks>
	public class PolymorphicRendererDemo : AggDemo
	{
		private static readonly PixelFormat[] Formats =
		{
			new PixelFormat("rgb555", 16, () => new BlenderRgb555()),
			new PixelFormat("rgb565", 16, () => new BlenderRgb565()),
			new PixelFormat("rgb24", 24, () => new BlenderBGR()),
			new PixelFormat("bgr24", 24, () => new BlenderBGR()),
			new PixelFormat("rgba32", 32, () => new BlenderRGBA()),
			new PixelFormat("argb32", 32, () => new BlenderBGRA()),
			new PixelFormat("abgr32", 32, () => new BlenderRGBA()),
			new PixelFormat("bgra32", 32, () => new BlenderBGRA()),
		};

		// C++ m_x, m_y.
		private readonly double[] x = { 100, 369, 143 };

		private readonly double[] y = { 60, 170, 310 };

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private ImageBuffer window;

		private int windowFormat = -1;

		public PolymorphicRendererDemo()
		{
			// polymorphic_renderer.cpp runs with flip_y = true; a ctrl gets !flip_y.
			this.FormatRbox = new RboxCtrl(10, 10, 90, 170, false);
			foreach (PixelFormat format in Formats)
			{
				this.FormatRbox.AddItem(format.Name);
			}

			// C++ static pix_fmt = agg::pix_format_rgb555.
			this.FormatRbox.CurrentItem = 0;
			this.ctrls.Add(this.FormatRbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>The window's pixel format, one of C++'s eight in the order polymorphic_renderer.cpp lists them.</summary>
		public RboxCtrl FormatRbox { get; }

		public override string Name => "polymorphic_renderer";

		public override string Category => "Vector Graphics";

		public override string Description => "A triangle drawn through a renderer picked for the pixel format. Pick rgb555 or rgb565 to see 16-bit color.";

		public override int Width => 400;

		public override int Height => 330;

		public override void Draw(Graphics2D graphics)
		{
			PixelFormat format = Formats[Math.Clamp(this.FormatRbox.CurrentItem, 0, Formats.Length - 1)];

			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-window CPU layer.
			ImageBuffer destination = null;
			if (graphics.Rasterizer != null)
			{
				destination = graphics.DestImage as ImageBuffer ?? (graphics.DestImage as ImageProxy)?.LinkedImage as ImageBuffer;
			}

			if (destination == null)
			{
				// The GPU draws into its own RGBA target and cannot hold a packed pixel, so the finished frame is
				// quantised afterwards, every channel the way the format stores it and reads it back
				// (IGammaGraphics.MapChannels). The software format quantises at every blend instead, so where
				// translucent draws overlap the two can differ by a step. A surface without the pass shows the format
				// only through the fill and background colors.
				if (graphics is IGammaGraphics gammaGraphics)
				{
					this.DrawWindow(graphics, SrgbLut.FromSrgba8(255, 255, 255), SrgbLut.FromSrgba8(80, 30, 20));
					gammaGraphics.MapChannels(
						new RectangleDouble(0, 0, this.Width, this.Height),
						GammaFill.Table(v => format.Quantise(new Color(v, v, v)).red),
						GammaFill.Table(v => format.Quantise(new Color(v, v, v)).green),
						GammaFill.Table(v => format.Quantise(new Color(v, v, v)).blue));
				}
				else
				{
					this.DrawWindow(graphics, format.Quantise(SrgbLut.FromSrgba8(255, 255, 255)), format.Quantise(SrgbLut.FromSrgba8(80, 30, 20)));
				}

				return;
			}

			if (this.window == null || this.windowFormat != this.FormatRbox.CurrentItem)
			{
				this.window = new ImageBuffer(this.Width, this.Height, format.Bits, format.NewBlender());
				this.windowFormat = this.FormatRbox.CurrentItem;
			}

			// C++ platform_support's rasterizer has no clip box; neither does the reference frame's.
			Graphics2D windowGraphics = this.window.NewGraphics2D();
			windowGraphics.Rasterizer.reset_clipping();

			// The colors are C++ srgba8, taken into the pixel format's linear rgba8 as it converts them.
			this.DrawWindow(windowGraphics, SrgbLut.FromSrgba8(255, 255, 255), SrgbLut.FromSrgba8(80, 30, 20));

			for (int row = 0; row < this.Height; row++)
			{
				for (int column = 0; column < this.Width; column++)
				{
					destination.SetPixel(column, row, this.window.GetPixel(column, row));
				}
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

		// C++ on_draw: clear, then the triangle in one solid color - and the port's rbox on top.
		private void DrawWindow(Graphics2D graphics, Color background, Color fill)
		{
			var path = new VertexStorage();
			path.MoveTo(this.x[0], this.y[0]);
			path.LineTo(this.x[1], this.y[1]);
			path.LineTo(this.x[2], this.y[2]);
			path.ClosePolygon();

			graphics.Clear(background);
			graphics.Render(path, fill);
			this.ctrls.Render(graphics);
		}

		/// <summary>One of polymorphic_renderer.cpp's pixel formats: its name, bits per pixel and blender.</summary>
		private sealed class PixelFormat
		{
			public PixelFormat(string name, int bits, Func<IRecieveBlenderByte> newBlender)
			{
				this.Name = name;
				this.Bits = bits;
				this.NewBlender = newBlender;
			}

			public string Name { get; }

			public int Bits { get; }

			public Func<IRecieveBlenderByte> NewBlender { get; }

			/// <summary><paramref name="color"/> as one pixel of this format stores it and reads it back.</summary>
			public Color Quantise(Color color)
			{
				var pixel = new ImageBuffer(1, 1, this.Bits, this.NewBlender());
				pixel.SetPixel(0, 0, color);
				return pixel.GetPixel(0, 0);
			}
		}
	}
}
