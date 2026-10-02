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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's rasterizer_compound.cpp: a stroked ellipse, the ellipse, a stroked glyph "a" and the glyph,
	/// each a style of one compound rasterizer, drawn as layers over a gradient and two triangles. Each pixel's
	/// coverage goes to the top layer first, so a translucent layer shows the background, not the layers
	/// under it. The sliders set the stroke width and each layer's alpha; the check box flips the layer order.
	/// </summary>
	/// <remarks>
	/// On the GPU the same compound rasterizer runs on the CPU and its pixels are drawn through a
	/// <see cref="Graphics2DSpanImage"/>; the background triangles are vectors there.
	/// </remarks>
	public class RasterizerCompoundDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly VertexStorage glyph = GlyphA.Create();

		public RasterizerCompoundDemo()
		{
			// rasterizer_compound.cpp runs with flip_y = true and gives its sliders !flip_y.
			this.WidthSlider = NewSlider(180 + 10.0, 5.0, 130 + 300.0, 12, -20.0, 50.0, 10.0, "Width={0:F2}");
			this.Alpha1Slider = NewSlider(5, 5, 180, 12, 0, 1, 1, "Alpha1={0:F3}");
			this.Alpha2Slider = NewSlider(5, 25, 180, 32, 0, 1, 1, "Alpha2={0:F3}");
			this.Alpha3Slider = NewSlider(5, 45, 180, 52, 0, 1, 1, "Alpha3={0:F3}");
			this.Alpha4Slider = NewSlider(5, 65, 180, 72, 0, 1, 1, "Alpha4={0:F3}");
			this.InvertOrderBox = new CboxCtrl(190, 25, "Invert Z-Order");

			this.ctrls.Add(this.WidthSlider);
			this.ctrls.Add(this.Alpha1Slider);
			this.ctrls.Add(this.Alpha2Slider);
			this.ctrls.Add(this.Alpha3Slider);
			this.ctrls.Add(this.Alpha4Slider);
			this.ctrls.Add(this.InvertOrderBox);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_width</c>: the glyph's stroke width (the ellipse's is half), -20 to 50.</summary>
		public SliderCtrl WidthSlider { get; }

		/// <summary>C++ <c>m_alpha1</c>: the stroked ellipse's alpha (style 3).</summary>
		public SliderCtrl Alpha1Slider { get; }

		/// <summary>C++ <c>m_alpha2</c>: the ellipse's alpha (style 2).</summary>
		public SliderCtrl Alpha2Slider { get; }

		/// <summary>C++ <c>m_alpha3</c>: the stroked glyph's alpha (style 1).</summary>
		public SliderCtrl Alpha3Slider { get; }

		/// <summary>C++ <c>m_alpha4</c>: the glyph's alpha (style 0).</summary>
		public SliderCtrl Alpha4Slider { get; }

		/// <summary>C++ <c>m_invert_order</c>: layer_inverse (style 0 on top) instead of layer_direct.</summary>
		public CboxCtrl InvertOrderBox { get; }

		public override string Name => "rasterizer_compound";

		public override string Category => "Rendering";

		public override string Description => "Four shapes drawn as layers by one compound rasterizer, where each pixel's coverage goes to the top layer first. Change the stroke width and each layer's alpha, or invert the layer order.";

		public override int Width => 440;

		public override int Height => 330;

		public override void Draw(Graphics2D graphics)
		{
			int width = this.Width;
			int height = this.Height;

			var triangle1 = new VertexStorage();
			triangle1.MoveTo(0, 0);
			triangle1.LineTo(width, 0);
			triangle1.LineTo(width, height);

			var triangle2 = new VertexStorage();
			triangle2.MoveTo(0, 0);
			triangle2.LineTo(0, height);
			triangle2.LineTo(width, 0);

			// C++ names the colors srgba8 and draws into a linear rgba8 pixel format, which converts them.
			Color yellow = new Color(255, 255, 0);
			Color cyan = new Color(0, 255, 255);

			// Styles 0 to 3 as C++ lists them: the glyph, its stroke, the ellipse, its stroke.
			Color[] straight =
			{
				WithOpacity(SrgbLut.FromSrgba8(0, 0, 255), this.Alpha4Slider.Value),
				WithOpacity(SrgbLut.FromSrgba8(143, 90, 6), this.Alpha3Slider.Value),
				WithOpacity(SrgbLut.FromSrgba8(51, 0, 151), this.Alpha2Slider.Value),
				WithOpacity(SrgbLut.FromSrgba8(255, 0, 108), this.Alpha1Slider.Value),
			};

			IVertexSource[] shapes = this.StyleShapes();

			// The background: a gradient across the window, interpolated in sRGB, stored linear.
			var gradient = new Color[width];
			for (int i = 0; i < width; i++)
			{
				Color srgb = Rgba8.Gradient(yellow, cyan, (double)i / width);
				gradient[i] = SrgbLut.FromSrgba8(srgb.red, srgb.green, srgb.blue, srgb.alpha);
			}

			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-screen CPU layer. The reference frame has no transform, so pixels are frame pixels.
			if (graphics.Rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				for (int y = 0; y < height; y++)
				{
					destination.copy_color_hspan(0, y, width, gradient, 0);
				}

				graphics.Render(triangle1, SrgbLut.FromSrgba8(0, 100, 0));
				graphics.Render(triangle2, SrgbLut.FromSrgba8(0, 100, 100));

				// C++ renders the layers through pixfmt_bgra32_pre and a clipping renderer_base.
				var preView = new ImageBuffer();
				preView.Attach(destination, new BlenderPreMultBGRA());
				this.FillLayers(new ImageClippingProxy(preView), shapes, straight, colorSpans: false);
				destination.MarkImageChanged();
			}
			else
			{
				for (int i = 0; i < width; i++)
				{
					graphics.FillRectangle(i, 0, i + 1, height, gradient[i]);
				}

				graphics.Render(triangle1, SrgbLut.FromSrgba8(0, 100, 0));
				graphics.Render(triangle2, SrgbLut.FromSrgba8(0, 100, 100));

				// The mix where layers meet is premultiplied, and the span image blends straight colors, so every
				// layer goes through color spans, which PremultipliedColorSpans hands on straight.
				var spans = new Graphics2DSpanImage(graphics, width, height);
				this.FillLayers(new PremultipliedColorSpans(new ImageClippingProxy(spans)), shapes, straight, colorSpans: true);
				spans.Flush();
			}

			this.ctrls.Render(graphics);
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
		/// The layers, one compound rasterizer style each, into <paramref name="target"/>, which blends premultiplied
		/// colors; with <paramref name="colorSpans"/> every layer is a color span rather than a solid color.
		/// </summary>
		private void FillLayers(IImageByte target, IVertexSource[] shapes, Color[] straight, bool colorSpans)
		{
			var rasc = new rasterizer_compound_aa();
			rasc.layer_order(this.InvertOrderBox.Checked ? LayerOrder.Inverse : LayerOrder.Direct);
			for (int style = 3; style >= 0; style--)
			{
				rasc.styles(style, -1);
				rasc.add_path(shapes[style]);
			}

			var premultiplied = new Color[4];
			for (int i = 0; i < 4; i++)
			{
				premultiplied[i] = Premultiply(straight[i]);
			}

			new ScanlineRenderer().RenderCompoundLayered(rasc, new scanline_unpacked_8(), target, new SolidStyles(premultiplied, colorSpans));
		}

		/// <summary>C++ <c>rgba8::opacity(a)</c> for a in 0..1: alpha = uround(a * 255).</summary>
		private static Color WithOpacity(Color color, double opacity)
		{
			return new Color(color.red, color.green, color.blue, (int)((opacity * 255) + 0.5));
		}

		/// <summary>C++ <c>rgba8::premultiply()</c>.</summary>
		private static Color Premultiply(Color color)
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

		private SliderCtrl NewSlider(double x1, double y1, double x2, double y2, double min, double max, double value, string label)
		{
			var slider = new SliderCtrl(x1, y1, x2, y2, false) { Label = label };
			slider.SetRange(min, max);
			slider.Value = value;
			return slider;
		}

		/// <summary>The shape of each style: 0 the glyph, 1 its stroke, 2 the ellipse, 3 its stroke.</summary>
		private IVertexSource[] StyleShapes()
		{
			// C++ mtx: scaled by 4, then moved to (150, 100); conv_curve after the transform.
			IVertexSource curve = new FlattenCurves(new VertexSourceApplyTransform(this.glyph, Affine.NewScaling(4.0) * Affine.NewTranslation(150, 100)));
			var ellipse = new Ellipse(220.0, 180.0, 120.0, 10.0, 128, false);
			return new IVertexSource[]
			{
				curve,
				new Stroke(curve, this.WidthSlider.Value),
				ellipse,
				new Stroke(ellipse, this.WidthSlider.Value / 2),
			};
		}

		/// <summary>
		/// The example's style_handler: a solid premultiplied color per style, transparent past them. With
		/// <c>asColorSpans</c> each style is the same color as a generated span instead, the same pixels through
		/// blend_color_hspan.
		/// </summary>
		private sealed class SolidStyles : IStyleHandler
		{
			private readonly Color[] colors;

			private readonly bool asColorSpans;

			public SolidStyles(Color[] colors, bool asColorSpans)
			{
				this.colors = colors;
				this.asColorSpans = asColorSpans;
			}

			public Color color(int style) => style >= 0 && style < this.colors.Length ? this.colors[style] : new Color(0, 0, 0, 0);

			public void GenerateSpan(Color[] span, int spanIndex, int x, int y, int len, int style)
			{
				System.Array.Fill(span, this.color(style), spanIndex, len);
			}

			public bool IsSolid(int style) => !this.asColorSpans;
		}
	}
}
