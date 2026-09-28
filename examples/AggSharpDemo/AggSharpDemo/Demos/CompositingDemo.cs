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
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's compositing.cpp, built AGG_BGRA128: a picture, a shadowed yellow circle and a blue rounded rectangle
	/// drawn with the chosen SVG compositing operator, all in a float (rgba32) layer that is blended onto a
	/// checkerboard. The sliders set the source and destination alpha.
	/// </summary>
	/// <remarks>
	/// The software path renders the whole window as C++ does, into an <see cref="ImageBufferFloat"/>, and shows
	/// it as C++ shows it, each pixel through <see cref="SrgbLut.Srgba8FromRgba32"/>. The controls are the one
	/// deviation, matched by demo_compositing.cpp: C++ rasterizes them into the float window with rgba32 colors, but
	/// the ctrls here carry 8-bit colors, and rgba(1, 0.9, 0.8) as 230/255 comes out a level off after srgba8 across
	/// the whole slider background. So they are drawn as 8-bit ctrls onto the converted frame, as compositing2 draws
	/// them. The "%3.2f ms" render time C++ draws is left out: it changes every frame.
	/// The GPU path draws the same scene into a linear-light retained layer - the picture at its cover, the gradients
	/// through <see cref="IGradientFillGraphics"/>, the rounded rectangle through the chosen operator
	/// (<see cref="ICompOpGraphics"/>) - and composites the layer onto the checkerboard. The layer holds linear light
	/// in floats (<see cref="Graphics2D.CreateRetainedLayer(bool)"/>), so colours mix as they do in C++'s rgba32
	/// window, and the result is encoded to sRGB as srgba8 does.
	/// </remarks>
	public class CompositingDemo : AggDemo
	{
		private static readonly string[] OperatorNames =
		{
			"clear", "src", "dst", "src-over", "dst-over", "src-in", "dst-in", "src-out", "dst-out", "src-atop",
			"dst-atop", "xor", "plus", "multiply", "screen", "overlay", "darken", "lighten", "color-dodge",
			"color-burn", "hard-light", "soft-light", "difference", "exclusion",
		};

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private ImageBufferFloat picture;

		private ImageBuffer gpuPicture;

		/// <summary>The alpha <see cref="gpuPicture"/> was built with: the destination alpha as a byte.</summary>
		private int gpuPictureAlpha;

		/// <summary>The GPU stand-in's transparent scene layer (C++ img 0), kept across frames.</summary>
		private IRetainedLayer gpuLayer;

		public CompositingDemo()
		{
			// compositing.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.SrcAlphaSlider = new SliderCtrl(5, 5, 400, 11, false) { Label = "Src Alpha={0:F2}" };
			this.SrcAlphaSlider.Value = 0.75;

			this.DstAlphaSlider = new SliderCtrl(5, 5 + 15, 400, 11 + 15, false) { Label = "Dst Alpha={0:F2}" };
			this.DstAlphaSlider.Value = 1.0;

			this.OperatorRbox = new RboxCtrl(420, 5.0, 420 + 170.0, 340.0, false);
			this.OperatorRbox.SetTextSize(6.8);
			foreach (string name in OperatorNames)
			{
				this.OperatorRbox.AddItem(name);
			}

			this.OperatorRbox.CurrentItem = (int)CompOp.SrcOver;

			this.ctrls.Add(this.SrcAlphaSlider);
			this.ctrls.Add(this.DstAlphaSlider);
			this.ctrls.Add(this.OperatorRbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_alpha_src</c>: the rounded rectangle's alpha.</summary>
		public SliderCtrl SrcAlphaSlider { get; }

		/// <summary>C++ <c>m_alpha_dst</c>: the picture's cover and the circle's alpha.</summary>
		public SliderCtrl DstAlphaSlider { get; }

		/// <summary>C++ <c>m_comp_op</c>: the operator the rounded rectangle is drawn with, in <see cref="CompOp"/> order.</summary>
		public RboxCtrl OperatorRbox { get; }

		public override string Name => "compositing";

		public override string Category => "Compositing";

		public override string Description => "A picture and a circle with a rounded rectangle combined over them by the SVG compositing operators, in float color. Pick an operator; the sliders set the source and destination alpha.";

		public override int Width => 600;

		public override int Height => 400;

		public override void Draw(Graphics2D graphics)
		{
			if (graphics.Rasterizer != null && graphics.DestImage is IImageByte frame && frame.BitDepth == 32)
			{
				this.DrawFloatWindow(frame);

				// On the converted frame (see the remarks), in place of C++'s render_ctrl_rs into the float window.
				this.ctrls.Render(graphics);
			}
			else
			{
				this.DrawGpuStandIn(graphics);
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
		/// C++ <c>load_img(1, "compositing")</c> in a flip_y = true AGG_BGRA128 example: art/compositing.ppm, row 0
		/// the picture's bottom, each sRGB pixel converted to rgba32 as <c>rgba32(srgba8(r, g, b, 255))</c>.
		/// </summary>
		public static ImageBufferFloat LoadPicture()
		{
			byte[] ppm = ReadPpm(out int width, out int height, out int position);
			var image = new ImageBufferFloat(width, height, 128, new BlenderBGRAFloat());
			var pixelFormat = new PixelFormatBGRAFloat(image, new BlenderRgbaFloat());
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int i = position + (((y * width) + x) * 3);
					pixelFormat.CopyHline(x, height - 1 - y, 1, SrgbLut.Rgba32FromSrgba8(ppm[i], ppm[i + 1], ppm[i + 2]));
				}
			}

			return image;
		}

		/// <summary>C++ on_draw and render_scene, into a float window shown through srgba8.</summary>
		private void DrawFloatWindow(IImageByte frame)
		{
			this.picture ??= LoadPicture();
			double srcAlpha = this.SrcAlphaSlider.Value;
			double dstAlpha = this.DstAlphaSlider.Value;

			var window = new ImageBufferFloat(this.Width, this.Height, 128, new BlenderBGRAFloat());
			var pixf = new PixelFormatBGRAFloat(window, new BlenderRgbaFloat());
			pixf.Clear(SrgbLut.Rgba32FromSrgba8(255, 255, 255));
			ColorF check = SrgbLut.Rgba32FromSrgba8(0xdf, 0xdf, 0xdf);
			for (int y = 0; y < this.Height; y += 8)
			{
				for (int x = ((y >> 3) & 1) << 3; x < this.Width; x += 16)
				{
					pixf.CopyBar(x, y, x + 7, y + 7, check);
				}
			}

			// render_scene into a transparent layer (C++ img 0).
			var layer = new ImageBufferFloat(this.Width, this.Height, 128, new BlenderBGRAFloat());
			var pixf2 = new PixelFormatBGRAFloat(layer, new BlenderRgbaFloat());
			pixf2.Clear(SrgbLut.Rgba32FromSrgba8(0, 0, 0, 0));

			// C++ cover_type(alpha * cover_full) truncates.
			pixf2.BlendFrom(new PixelFormatBGRAFloat(this.picture, new BlenderRgbaFloat()), 250, 180, (byte)(dstAlpha * 255));

			int dstA = (int)(dstAlpha * 255);
			Circle(
				new RendererBaseFloat(pixf2),
				SrgbLut.Rgba32FromSrgba8(0xFD, 0xF0, 0x6F, dstA),
				SrgbLut.Rgba32FromSrgba8(0xFE, 0x9F, 0x34, dstA),
				70 * 3,
				100 + (24 * 3),
				37 * 3,
				100 + (79 * 3),
				dstAlpha);

			int srcA = (int)(srcAlpha * 255);
			SrcShape(
				new RendererBaseFloat(new PixelFormatCompOpBGRAFloat(layer, (CompOp)this.OperatorRbox.CurrentItem)),
				SrgbLut.Rgba32FromSrgba8(0x7F, 0xC1, 0xFF, srcA),
				SrgbLut.Rgba32FromSrgba8(0x05, 0x00, 0x5F, srcA),
				300 + 50,
				100 + (24 * 3),
				107 + 50,
				100 + (79 * 3));

			// rb_pre.blend_from(pixf2): the premultiplied layer onto the window.
			new PixelFormatBGRAFloat(window, new BlenderRgbaPreFloat()).BlendFrom(pixf2, 0, 0);

			byte[] buffer = frame.GetBuffer();
			for (int y = 0; y < this.Height; y++)
			{
				int o = frame.GetBufferOffsetXY(0, y);
				for (int x = 0; x < this.Width; x++)
				{
					Color c = SrgbLut.Srgba8FromRgba32(pixf.Pixel(x, y));
					buffer[o + ImageBuffer.OrderR] = c.red;
					buffer[o + ImageBuffer.OrderG] = c.green;
					buffer[o + ImageBuffer.OrderB] = c.blue;
					buffer[o + ImageBuffer.OrderA] = c.alpha;
					o += 4;
				}
			}

			frame.MarkImageChanged();
		}

		/// <summary>
		/// The GPU stand-in: the same picture, shadow, circle and rounded rectangle, drawn as C++ render_scene does
		/// into a transparent layer, with the rounded rectangle through the chosen operator when the GPU does it.
		/// </summary>
		private void DrawGpuStandIn(Graphics2D graphics)
		{
			graphics.Clear(Color.White);
			var check = new Color(0xdf, 0xdf, 0xdf);
			for (int y = 0; y < this.Height; y += 8)
			{
				for (int x = ((y >> 3) & 1) << 3; x < this.Width; x += 16)
				{
					graphics.FillRectangle(x, y, x + 8, y + 8, check);
				}
			}

			double dstAlpha = this.DstAlphaSlider.Value;
			int dstA = (int)(dstAlpha * 255);

			// C++ blends the picture at the destination alpha as its cover (truncated, as there); Graphics2D has no
			// cover on an image draw, so the picture carries it as its alpha, rebuilt when the slider moves.
			if (this.gpuPicture == null || this.gpuPictureAlpha != dstA)
			{
				byte[] ppm = ReadPpm(out int width, out int height, out int position);
				this.gpuPicture = new ImageBuffer(width, height);
				this.gpuPictureAlpha = dstA;
				for (int y = 0; y < height; y++)
				{
					for (int x = 0; x < width; x++)
					{
						int i = position + (((y * width) + x) * 3);
						this.gpuPicture.SetPixel(x, height - 1 - y, new Color(ppm[i], ppm[i + 1], ppm[i + 2], dstA));
					}
				}
			}

			int srcA = (int)(this.SrcAlphaSlider.Value * 255);
			// C++ renders the scene into a transparent layer, so an operator such as clear or src-in reads the
			// layer's alpha, not the checkerboard's. Without retained layers it goes straight onto the frame.
			if (graphics.SupportsRetainedLayers)
			{
				if (this.gpuLayer == null || !this.gpuLayer.BelongsTo(graphics))
				{
					this.gpuLayer?.Dispose();
					// Linear light where the surface has it, as C++'s rgba32 layer; sRGB bytes otherwise.
					this.gpuLayer = graphics.CreateRetainedLayer(linearLight: true) ?? graphics.CreateRetainedLayer();
				}

				using (var paint = this.gpuLayer.Begin(this.Width, this.Height))
				{
					this.DrawGpuScene(paint.Graphics, dstAlpha, dstA, srcA);
				}

				graphics.RenderRetainedLayer(this.gpuLayer, 0, 0);
			}
			else
			{
				this.DrawGpuScene(graphics, dstAlpha, dstA, srcA);
			}

			this.ctrls.Render(graphics);
		}

		private void DrawGpuScene(Graphics2D graphics, double dstAlpha, int dstA, int srcA)
		{
			graphics.Render(this.gpuPicture, 250, 180);

			double x1 = 70 * 3, y1 = 100 + (24 * 3), x2 = 37 * 3, y2 = 100 + (79 * 3);
			double r = Math.Sqrt(((x2 - x1) * (x2 - x1)) + ((y2 - y1) * (y2 - y1))) / 2;
			graphics.Render(new Ellipse(((x1 + x2) / 2) + 5, ((y1 + y2) / 2) - 3, r, r, 100), new Color(0xcb, 0xcb, 0xcb, (int)(0.7 * dstAlpha * 255)));
			var circle = new Ellipse((x1 + x2) / 2, (y1 + y2) / 2, r, r, 100);
			var circleColors = new GpuGradientColors(SrgbLut.Rgba32FromSrgba8(0xFD, 0xF0, 0x6F, dstA), SrgbLut.Rgba32FromSrgba8(0xFE, 0x9F, 0x34, dstA));
			FillGradient(graphics, circle, circleColors, x1, y1, x2, y2, new Color(0xFE, 0xC8, 0x52, dstA));

			double sx1 = 300 + 50, sy1 = 100 + (24 * 3), sx2 = 107 + 50, sy2 = 100 + (79 * 3);
			var shape = new RoundedRect(sx1, sy1, sx2, sy2, 40);
			var shapeColors = new GpuGradientColors(SrgbLut.Rgba32FromSrgba8(0x7F, 0xC1, 0xFF, srcA), SrgbLut.Rgba32FromSrgba8(0x05, 0x00, 0x5F, srcA));
			void DrawShape() => FillGradient(graphics, shape, shapeColors, sx1, sy1, sx2, sy2, new Color(0x42, 0x60, 0xAF, srcA));
			var op = (CompOp)this.OperatorRbox.CurrentItem;
			if (graphics is ICompOpGraphics compositing && compositing.SupportsCompOp(op))
			{
				compositing.DrawWithCompOp(op, DrawShape);
			}
			else
			{
				DrawShape();
			}
		}

		/// <summary>
		/// The GPU's C++ LinearGradient: the same gradient_x across (x1, y1)-(x2, y2) through
		/// <see cref="IGradientFillGraphics"/>, or <paramref name="fallback"/> flat where the surface has no gradients.
		/// </summary>
		private static void FillGradient(Graphics2D graphics, IVertexSource path, IColorFunction colors, double x1, double y1, double x2, double y2, Color fallback)
		{
			if (graphics is IGradientFillGraphics gradientGraphics)
			{
				gradientGraphics.FillPathWithGradient(path, new GradientFill
				{
					Shape = GradientShape.X,
					D1 = 0,
					D2 = 100,
					ScreenToGradient = GradientAffine(x1, y1, x2, y2, 100),
					Colors = colors,
				});
			}
			else
			{
				graphics.Render(path, fallback);
			}
		}

		/// <summary>
		/// C++'s rgba32 gradient_linear_color, each step shown as the float window shows it (srgba8): the GPU layer
		/// holds sRGB bytes, so the colours are interpolated in linear light as C++ does and only then encoded.
		/// </summary>
		private sealed class GpuGradientColors : IColorFunction
		{
			private readonly Color[] colors;

			public GpuGradientColors(ColorF c1, ColorF c2)
			{
				var linear = new GradientLinearColorFloat(c1, c2);
				this.colors = new Color[linear.size()];
				for (int i = 0; i < this.colors.Length; i++)
				{
					this.colors[i] = SrgbLut.Srgba8FromRgba32(linear[i]);
				}
			}

			public Color this[int v] => this.colors[v];

			public int size() => this.colors.Length;
		}

		/// <summary>C++ gradient_affine: the matrix that maps (x1, y1)-(x2, y2) onto gradient_x's 0 to <paramref name="d2"/>.</summary>
		private static Affine GradientAffine(double x1, double y1, double x2, double y2, double d2 = 100.0)
		{
			double dx = x2 - x1;
			double dy = y2 - y1;
			Affine mtx = Affine.NewIdentity();
			mtx *= Affine.NewScaling(Math.Sqrt((dx * dx) + (dy * dy)) / d2);
			mtx *= Affine.NewRotation(Math.Atan2(dy, dx));
			mtx *= Affine.NewTranslation(x1, y1);
			mtx.invert();
			return mtx;
		}

		private static SpanGradientFloat LinearGradient(ColorF c1, ColorF c2, double x1, double y1, double x2, double y2)
		{
			return new SpanGradientFloat(
				new span_interpolator_linear(GradientAffine(x1, y1, x2, y2, 100)),
				new gradient_x(),
				new GradientLinearColorFloat(c1, c2),
				0,
				100);
		}

		/// <summary>C++ circle: a gray shadow, then the circle across (x1, y1)-(x2, y2) with a linear gradient from c1 to c2.</summary>
		private static void Circle(RendererBaseFloat renderer, ColorF c1, ColorF c2, double x1, double y1, double x2, double y2, double shadowAlpha)
		{
			double dx = x2 - x1;
			double dy = y2 - y1;
			double r = Math.Sqrt((dx * dx) + (dy * dy)) / 2;

			var rasterizer = new ScanlineRasterizer();
			var scanline = new scanline_unpacked_8();
			rasterizer.add_path(new Ellipse(((x1 + x2) / 2) + 5, ((y1 + y2) / 2) - 3, r, r, 100));
			renderer.RenderScanlinesAaSolid(rasterizer, scanline, new ColorF(0.6, 0.6, 0.6, 0.7 * shadowAlpha));

			rasterizer.reset();
			rasterizer.add_path(new Ellipse((x1 + x2) / 2, (y1 + y2) / 2, r, r, 100));
			renderer.RenderScanlinesAa(rasterizer, scanline, LinearGradient(c1, c2, x1, y1, x2, y2));
		}

		/// <summary>C++ src_shape: a rounded rectangle, radius 40, with a linear gradient from c1 to c2.</summary>
		private static void SrcShape(RendererBaseFloat renderer, ColorF c1, ColorF c2, double x1, double y1, double x2, double y2)
		{
			var rasterizer = new ScanlineRasterizer();
			rasterizer.add_path(new RoundedRect(x1, y1, x2, y2, 40));
			renderer.RenderScanlinesAa(rasterizer, new scanline_unpacked_8(), LinearGradient(c1, c2, x1, y1, x2, y2));
		}

		// Reads only what compositing.ppm is: a binary (P6) PPM, 8 bits a channel, a comment allowed in the header.
		private static byte[] ReadPpm(out int width, out int height, out int position)
		{
			const string resourceName = "MatterHackers.AggSharpDemo.Images.compositing.ppm";
			using var stream = typeof(CompositingDemo).Assembly.GetManifestResourceStream(resourceName)
				?? throw new InvalidOperationException(
					$"The image resource '{resourceName}' is missing; AggSharpDemo.csproj embeds it from Images/compositing.ppm.");
			using var memory = new MemoryStream();
			stream.CopyTo(memory);
			byte[] ppm = memory.ToArray();

			position = 0;
			if (NextToken(ppm, ref position) != "P6")
			{
				throw new InvalidDataException("compositing.ppm is expected to be a binary (P6) PPM.");
			}

			width = int.Parse(NextToken(ppm, ref position));
			height = int.Parse(NextToken(ppm, ref position));
			NextToken(ppm, ref position);
			position++; // the one whitespace byte before the pixels
			return ppm;
		}

		private static string NextToken(byte[] data, ref int position)
		{
			while (true)
			{
				while (position < data.Length && char.IsWhiteSpace((char)data[position]))
				{
					position++;
				}

				if (position < data.Length && data[position] == '#')
				{
					while (position < data.Length && data[position] != '\n')
					{
						position++;
					}

					continue;
				}

				break;
			}

			int start = position;
			while (position < data.Length && !char.IsWhiteSpace((char)data[position]))
			{
				position++;
			}

			return System.Text.Encoding.ASCII.GetString(data, start, position - start);
		}
	}
}
