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
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>What flash_rasterizer.cpp and flash_rasterizer2.cpp draw alike: the palette, the outlines, the help
	/// text, and the view keys.</summary>
	public static class FlashDrawing
	{
		/// <summary>
		/// The examples' m_colors: 100 srgba8(rand() &amp; 0xFF, rand() &amp; 0xFF, rand() &amp; 0xFF, 230), straight
		/// alpha, linear. rand() is MSVC's, its values taken red, green, blue (C++ leaves the order unspecified).
		/// </summary>
		public static Color[] Palette()
		{
			var rand = new MsvcRand();
			var colors = new Color[100];
			for (int i = 0; i < colors.Length; i++)
			{
				int r = rand.Next() & 0xFF;
				int g = rand.Next() & 0xFF;
				int b = rand.Next() & 0xFF;
				colors[i] = SrgbLut.FromSrgba8(r, g, b, 230);
			}

			return colors;
		}

		/// <summary>C++ <c>rgba8::premultiply()</c>.</summary>
		public static Color Premultiply(Color color)
		{
			if (color.alpha == 255)
			{
				return color;
			}

			if (color.alpha == 0)
			{
				return new Color(0, 0, 0, 0);
			}

			return new Color(
				Rgba8Math.Multiply(color.red, color.alpha),
				Rgba8Math.Multiply(color.green, color.alpha),
				Rgba8Math.Multiply(color.blue, color.alpha),
				color.alpha);
		}

		/// <summary>The examples draw everything through pixfmt_bgra32_pre and a clipping renderer_base, after
		/// clearing to rgba(1, 1, 0.95).</summary>
		public static ImageClippingProxy PremultipliedTarget(IImageByte destination)
		{
			var preView = new ImageBuffer();
			preView.Attach(destination, new BlenderPreMultBGRA());
			var target = new ImageClippingProxy(preView);
			target.clear(new Color(255, 255, 242));
			return target;
		}

		/// <summary>
		/// C++ on_key's zoom (+ in by 1.1, - out) and rotation (right pi/20, left -pi/20), each about the mouse
		/// pointer (<paramref name="x"/>, <paramref name="y"/>); null for any other key.
		/// </summary>
		public static Affine? ApplyViewKey(Keys key, double x, double y, Affine view)
		{
			Affine step;
			switch (key)
			{
				case Keys.Add:
				case Keys.Oemplus:
					step = Affine.NewScaling(1.1);
					break;
				case Keys.Subtract:
				case Keys.OemMinus:
					step = Affine.NewScaling(1 / 1.1);
					break;
				case Keys.Left:
					step = Affine.NewRotation(-Math.PI / 20.0);
					break;
				case Keys.Right:
					step = Affine.NewRotation(Math.PI / 20.0);
					break;
				default:
					return null;
			}

			// m_scale *= translation(-x, -y); m_scale *= step; m_scale *= translation(x, y).
			return view * Affine.NewTranslation(-x, -y) * step * Affine.NewTranslation(x, y);
		}

		/// <summary>
		/// The examples' outlines and help text, through a rasterizer_sl_clip_dbl rasterizer clipped to the
		/// window: each path with a line style stroked sqrt(view scale) wide with round joins and caps in
		/// srgba8(0, 0, 0, 128), then the help text.
		/// </summary>
		public static void RenderStrokesAndHelp(FlashShape shape, Affine view, int width, int height, IImageByte target)
		{
			var ras = new ScanlineRasterizer(new VectorClipperDouble());
			var scanline = new scanline_unpacked_8();
			var renderer = new ScanlineRenderer();
			ras.SetVectorClipBox(0, 0, width, height);
			for (int i = 0; i < shape.Paths; i++)
			{
				ras.reset();
				if (shape.Style(i).Line >= 0)
				{
					ras.add_path(Outline(shape, view, i));
					renderer.RenderSolid(target, ras, scanline, new Color(0, 0, 0, 128));
				}
			}

			ras.add_path(HelpText());
			renderer.RenderSolid(target, ras, scanline, new Color(0, 0, 0));
		}

		/// <summary>The GPU frame's outlines and help text, as vectors over the fills.</summary>
		public static void RenderStrokesAndHelpOnGpu(Graphics2D graphics, FlashShape shape, Affine view)
		{
			for (int i = 0; i < shape.Paths; i++)
			{
				if (shape.Style(i).Line >= 0)
				{
					graphics.Render(Outline(shape, view, i), new Color(0, 0, 0, 128));
				}
			}

			graphics.Render(HelpText(), new Color(0, 0, 0));
		}

		/// <summary>The examples' conv_stroke of path <paramref name="i"/>: sqrt(view scale) wide, round joins and caps.</summary>
		public static IVertexSource Outline(FlashShape shape, Affine view, int i)
		{
			return new Stroke(shape.Path(i, view), Math.Sqrt(view.GetScale()))
			{
				LineJoin = LineJoin.Round,
				LineCap = LineCap.Round,
			};
		}

		/// <summary>
		/// The examples' help text: gsv_text 8 high, flipped for the y-down window, from (10, 20), stroked 1.6 wide
		/// with round caps. C++ puts a "Fill=..ms (..FPS) Stroke=.." timing line first; it differs every frame, so
		/// it is left out, but its line breaks are kept so the help lines stay where C++ draws them.
		/// </summary>
		public static IVertexSource HelpText()
		{
#pragma warning disable CS0618 // gsv_text is obsolete for UI text; the examples draw their help with exactly this font.
			var text = new gsv_text();
#pragma warning restore CS0618
			text.size(8.0, 0.0);
			text.flip(true);
			text.start_point(10.0, 20.0);
			text.text("\n\nSpace: Next Shape\n\n+/- : ZoomIn/ZoomOut (with respect to the mouse pointer)");

			var outline = new VertexStorage();
			foreach (VertexData vertex in text.Vertices())
			{
				if (vertex.IsStop)
				{
					break;
				}

				outline.Add(vertex.Position.X, vertex.Position.Y, vertex.Command);
			}

			return new Stroke(outline, 1.6) { LineCap = LineCap.Round };
		}
	}
}
