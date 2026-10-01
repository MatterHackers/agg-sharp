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
	/// C++ AGG's gradient_focal.cpp: a reflected radial gradient with a movable focal point, its colors built
	/// through a gamma LUT and the whole frame put back through the inverse gamma. Click or drag to move the
	/// focus; the slider sets the gamma.
	/// </summary>
	/// <remarks>
	/// C++ also prints how long the gradient took ("%3.2f ms"); it differs every frame, so the port leaves it out.
	/// </remarks>
	public class GradientFocalDemo : AggDemo
	{
		private const double Radius = 100;

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();
		private readonly GammaLookUpTable gammaLut = new GammaLookUpTable();
		private gradient_lut gradientLut;
		private double builtGamma = double.NaN;

		public GradientFocalDemo()
		{
			// gradient_focal.cpp runs with flip_y = true and gives its ctrl !flip_y.
			this.GammaSlider = new SliderCtrl(5.0, 5.0, 340.0, 12.0, false) { Label = "Gamma = {0:F3}" };
			this.GammaSlider.SetRange(0.5, 2.5);
			this.GammaSlider.Value = 1.0;
			this.ctrls.Add(this.GammaSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			// C++ on_init: the focus starts at the window center.
			this.FocusX = this.Width / 2;
			this.FocusY = this.Height / 2;
		}

		/// <summary>C++ <c>m_gamma</c>.</summary>
		public SliderCtrl GammaSlider { get; }

		/// <summary>C++ <c>m_mouse_x</c>: the focal point, in demo coordinates.</summary>
		public double FocusX { get; set; }

		public double FocusY { get; set; }

		public override string Name => "gradient_focal";

		public override string Category => "Color & Gradients";

		public override string Description => "A radial gradient with a focal point, built through a gamma table so its colors blend evenly. Click or drag to move the focus; the slider changes the gamma.";

		public override int Width => 600;

		public override int Height => 400;

		public override void Draw(Graphics2D graphics)
		{
			// When the gamma changes, rebuild the gamma and gradient LUTs.
			if (this.builtGamma != this.GammaSlider.Value)
			{
				this.BuildGradientLut();
			}

			graphics.Clear(Rgba8.FromRgba(1, 1, 1));

			double cx = this.Width / 2;
			double cy = this.Height / 2;
			var gradientFunction = new gradient_reflect_adaptor(new gradient_radial_focus(Radius, this.FocusX - cx, this.FocusY - cy));
			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-window CPU layer.
			Affine transform = graphics.GetTransform();
			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			if (rasterizer != null)
			{
				// An image's Graphics2D draws through a clipping proxy; the gamma pass needs the image under it.
				ImageBuffer destination = graphics.DestImage as ImageBuffer ?? (graphics.DestImage as ImageProxy)?.LinkedImage as ImageBuffer;
				if (destination != null)
				{
					// The software reference, as C++: the gradient over the whole frame, the circle, the slider,
					// then the inverse gamma over every pixel.
					Affine frameToGradient = Affine.NewTranslation(cx, cy) * transform;
					frameToGradient.invert();
					var spanGenerator = new span_gradient(new span_interpolator_linear(frameToGradient), gradientFunction, new LinearFromSrgb(this.gradientLut), 0, Radius);
					FillWithSpans(rasterizer, destination, spanGenerator);

					this.DrawBoundaryAndSlider(graphics, cx, cy);
					destination.ApplyGammaInv(this.gammaLut);
				}
			}
			else if (graphics is IGradientFillGraphics gradientGraphics)
			{
				// A GPU surface: the same span_gradient evaluated per pixel over the frame, the circle and the
				// slider, then the inverse gamma over the frame (IGammaGraphics.MapChannels). A surface without
				// that pass gets the inverse gamma baked into the gradient's colors, and the rest drawn without it.
				bool mapsChannels = graphics is IGammaGraphics;
				Affine screenToGradient = Affine.NewTranslation(cx, cy) * transform;
				screenToGradient.invert();
				var fill = new GradientFill
				{
					Shape = GradientShape.RadialFocus,
					Spread = GradientSpread.Reflect,
					D1 = 0,
					D2 = Radius,
					FocusRadius = Radius,
					FocusX = this.FocusX - cx,
					FocusY = this.FocusY - cy,
					ScreenToGradient = screenToGradient,
					Colors = mapsChannels ? new LinearFromSrgb(this.gradientLut) : new LinearFromSrgb(this.gradientLut, this.gammaLut),
				};
				gradientGraphics.FillPathWithGradient(new RoundedRect(0, 0, this.Width, this.Height, 0), fill);
				this.DrawBoundaryAndSlider(graphics, cx, cy);
				GammaFill.ApplyGammaInv(graphics, this.gammaLut, this.Width, this.Height);
			}
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button) || button != AggInputFlags.MouseLeft)
			{
				return;
			}

			this.FocusX = x;
			this.FocusY = y;
			this.Invalidate();
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseMove(x, y, flags) || !flags.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			this.FocusX = x;
			this.FocusY = y;
			this.Invalidate();
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
		/// C++ <c>build_gradient_lut</c>. Each stop is an srgba8 made linear, put through the gamma, then stored
		/// back as srgba8 - the LUT is a <c>gradient_lut&lt;color_interpolator&lt;srgba8&gt;, 1024&gt;</c>, so it
		/// blends the sRGB bytes with rgba8::gradient.
		/// </summary>
		private void BuildGradientLut()
		{
			this.builtGamma = this.GammaSlider.Value;
			this.gammaLut.SetGamma(this.builtGamma);
			this.gradientLut = new gradient_lut(1024, fastInterpolator: false);
			this.gradientLut.add_color(0.0, this.SrgbStop(0, 255, 0));
			this.gradientLut.add_color(0.2, this.SrgbStop(120, 0, 0));
			this.gradientLut.add_color(0.7, this.SrgbStop(120, 120, 0));
			this.gradientLut.add_color(1.0, this.SrgbStop(0, 0, 255));
			this.gradientLut.build_lut();
		}

		/// <summary>C++ <c>srgba8(rgba8_gamma_dir(srgba8(r, g, b), gamma))</c>.</summary>
		private Color SrgbStop(int r, int g, int b)
		{
			Color linear = SrgbLut.FromSrgba8(r, g, b);
			return new Color(
				SrgbLut.SrgbFromLinear(this.gammaLut.dir(linear.red)),
				SrgbLut.SrgbFromLinear(this.gammaLut.dir(linear.green)),
				SrgbLut.SrgbFromLinear(this.gammaLut.dir(linear.blue)),
				linear.alpha);
		}

		/// <summary>C++ on_draw's gradient pass: the whole of <paramref name="destination"/> filled by <paramref name="spanGenerator"/>.</summary>
		private static void FillWithSpans(ScanlineRasterizer rasterizer, ImageBuffer destination, span_gradient spanGenerator)
		{
			rasterizer.reset();
			rasterizer.move_to_d(0, 0);
			rasterizer.line_to_d(destination.Width, 0);
			rasterizer.line_to_d(destination.Width, destination.Height);
			rasterizer.line_to_d(0, destination.Height);
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), destination, new span_allocator(), spanGenerator);
			destination.MarkImageChanged();
		}

		private void DrawBoundaryAndSlider(Graphics2D graphics, double cx, double cy)
		{
			// The circle that shows the gradient's boundary.
			graphics.Render(new Stroke(new Ellipse(cx, cy, Radius, Radius)), Rgba8.FromRgba(1, 1, 1));
			this.ctrls.Render(graphics);
		}

		/// <summary>
		/// The srgba8 LUT read as the rgba8 the span needs (C++ converts on assignment), optionally through the
		/// inverse gamma as well for a GPU surface without a whole-frame gamma pass.
		/// </summary>
		private sealed class LinearFromSrgb : IColorFunction
		{
			private readonly Color[] colors;

			public LinearFromSrgb(gradient_lut srgbLut, GammaLookUpTable inverseGamma = null)
			{
				this.colors = new Color[srgbLut.size()];
				for (int i = 0; i < this.colors.Length; i++)
				{
					Color srgb = srgbLut[i];
					Color linear = SrgbLut.FromSrgba8(srgb.red, srgb.green, srgb.blue, srgb.alpha);
					this.colors[i] = inverseGamma == null
						? linear
						: new Color(inverseGamma.inv(linear.red), inverseGamma.inv(linear.green), inverseGamma.inv(linear.blue), linear.alpha);
				}
			}

			public Color this[int v] => this.colors[v];

			public int size() => this.colors.Length;
		}
	}
}
