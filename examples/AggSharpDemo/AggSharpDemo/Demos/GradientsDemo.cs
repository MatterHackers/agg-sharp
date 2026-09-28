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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's gradients.cpp: a circle filled with one of six reflected gradients whose colors come from four
	/// spline controls (red, green, blue, alpha) and whose color spacing comes from a gamma control - the
	/// "Mach bands compensation" profile. Drag to move the gradient, right-drag to scale and turn it, Ctrl-drag
	/// to stretch it.
	/// </summary>
	public class GradientsDemo : AggDemo
	{
		private const double CenterX = 350;
		private const double CenterY = 280;
		private const double EllipseRadius = 110;

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();
		/// <summary>The same gradients as <see cref="gradients"/>, item for item, for <see cref="IGradientFillGraphics"/>.</summary>
		private static readonly GradientShape[] GpuShapes =
		{
			GradientShape.Radial, GradientShape.Diamond, GradientShape.X, GradientShape.XY, GradientShape.SqrtXY, GradientShape.Conic,
		};

		private readonly IGradient[] gradients =
		{
			new gradient_reflect_adaptor(new gradient_radial()),
			new gradient_reflect_adaptor(new gradient_diamond()),
			new gradient_reflect_adaptor(new gradient_x()),
			new gradient_reflect_adaptor(new gradient_xy()),
			new gradient_reflect_adaptor(new gradient_sqrt_xy()),
			new gradient_reflect_adaptor(new gradient_conic()),
		};

		private bool mouseMove;
		private double pdx;
		private double pdy;
		private double prevScale = 1.0;
		private double prevAngle;
		private double prevScaleX = 1.0;
		private double prevScaleY = 1.0;

		public GradientsDemo()
		{
			// gradients.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.Profile = new GammaCtrl(10.0, 10.0, 200.0, 170.0 - 5.0, false);
			this.SplineR = new SplineCtrl(210, 10, 210 + 250, 5 + 40, 6, false);
			this.SplineG = new SplineCtrl(210, 10 + 40, 210 + 250, 5 + 80, 6, false);
			this.SplineB = new SplineCtrl(210, 10 + 80, 210 + 250, 5 + 120, 6, false);
			this.SplineA = new SplineCtrl(210, 10 + 120, 210 + 250, 5 + 160, 6, false);
			this.GradientRbox = new RboxCtrl(10.0, 180.0, 200.0, 300.0, false);
			this.ctrls.Add(this.Profile);
			this.ctrls.Add(this.SplineR);
			this.ctrls.Add(this.SplineG);
			this.ctrls.Add(this.SplineB);
			this.ctrls.Add(this.SplineA);
			this.ctrls.Add(this.GradientRbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			this.Profile.SetBorderWidth(2.0, 2.0);
			this.SplineR.BackgroundColor = Rgba8.FromRgba(1.0, 0.8, 0.8);
			this.SplineG.BackgroundColor = Rgba8.FromRgba(0.8, 1.0, 0.8);
			this.SplineB.BackgroundColor = Rgba8.FromRgba(0.8, 0.8, 1.0);
			this.SplineA.BackgroundColor = Rgba8.FromRgba(1.0, 1.0, 1.0);
			foreach (SplineCtrl spline in new[] { this.SplineR, this.SplineG, this.SplineB, this.SplineA })
			{
				spline.SetBorderWidth(1.0, 2.0);

				// The color splines fall from 1 to 0 in a straight line; alpha stays at 1.
				bool alpha = spline == this.SplineA;
				spline.SetPoint(0, 0.0, 1.0);
				for (int i = 1; i < 5; i++)
				{
					spline.SetPoint(i, i / 5.0, alpha ? 1.0 : 1.0 - (i / 5.0));
				}

				spline.SetPoint(5, 1.0, alpha ? 1.0 : 0.0);
				spline.UpdateSpline();
			}

			this.GradientRbox.SetBorderWidth(2.0, 2.0);
			this.GradientRbox.AddItem("Circular");
			this.GradientRbox.AddItem("Diamond");
			this.GradientRbox.AddItem("Linear");
			this.GradientRbox.AddItem("XY");
			this.GradientRbox.AddItem("sqrt(XY)");
			this.GradientRbox.AddItem("Conic");
			this.GradientRbox.CurrentItem = 0;
		}

		/// <summary>C++ <c>m_profile</c>: its gamma array maps the gradient's 256 steps onto the color table.</summary>
		public GammaCtrl Profile { get; }

		public SplineCtrl SplineR { get; }

		public SplineCtrl SplineG { get; }

		public SplineCtrl SplineB { get; }

		public SplineCtrl SplineA { get; }

		/// <summary>C++ <c>m_rbox</c>: Circular, Diamond, Linear, XY, sqrt(XY), Conic.</summary>
		public RboxCtrl GradientRbox { get; }

		/// <summary>C++ <c>m_center_x</c>: where the gradient is centered. C++ saves it (and the splines) in
		/// settings.dat; the port starts at the defaults.</summary>
		public double GradientCenterX { get; set; } = CenterX;

		public double GradientCenterY { get; set; } = CenterY;

		public double GradientScale { get; set; } = 1.0;

		/// <summary>C++ <c>m_angle</c>, in radians.</summary>
		public double GradientAngle { get; set; }

		public double GradientScaleX { get; set; } = 1.0;

		public double GradientScaleY { get; set; } = 1.0;

		public override string Name => "gradients";

		public override string Category => "Gradients";

		public override string Description => "Six gradient shapes colored by editable red, green, blue and alpha splines, with a gamma profile that evens out Mach bands. Drag to move the gradient, right-drag to turn and scale it, Ctrl-drag to stretch it.";

		public override int Width => 512;

		public override int Height => 400;

		public override void Draw(Graphics2D graphics)
		{
			graphics.Clear(Rgba8.FromRgba(0, 0, 0));

			this.Profile.SetTextSize(8.0);
			this.ctrls.Render(graphics);

			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-window CPU layer.
			Affine transform = graphics.GetTransform();
			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			if (rasterizer != null)
			{
				if (graphics.DestImage is IImageByte destination)
				{
					// The software reference: the circle's spans come straight from span_gradient, as C++ does.
					this.RenderGradient(destination, rasterizer, transform);
					destination.MarkImageChanged();
				}
			}
			else if (graphics is IGradientFillGraphics gradientGraphics)
			{
				// A GPU surface: the same span_gradient evaluated per pixel.
				Affine screenToGradient = this.GradientMatrix() * transform;
				screenToGradient.invert();
				int item = this.GradientRbox.CurrentItem;
				var fill = new GradientFill
				{
					Shape = GpuShapes[item >= 1 && item <= 5 ? item : 0],
					Spread = GradientSpread.Reflect,
					D1 = 0,
					D2 = 150,
					ScreenToGradient = screenToGradient,
					Colors = this.NewColorFunction(),
				};
				var circle = new Ellipse(CenterX, CenterY, EllipseRadius, EllipseRadius, 64);
				gradientGraphics.FillPathWithGradient(circle, fill);
			}
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button))
			{
				return;
			}

			this.mouseMove = true;
			this.pdx = this.GradientCenterX - x;
			this.pdy = this.GradientCenterY - y;
			this.prevScale = this.GradientScale;
			this.prevAngle = this.GradientAngle + Math.PI;
			this.prevScaleX = this.GradientScaleX;
			this.prevScaleY = this.GradientScaleY;
			this.Invalidate();
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseMove(x, y, flags) || !this.mouseMove)
			{
				return;
			}

			if (flags.HasFlag(AggInputFlags.Ctrl))
			{
				double dx = x - this.GradientCenterX;
				double dy = y - this.GradientCenterY;
				this.GradientScaleX = this.prevScaleX * dx / this.pdx;
				this.GradientScaleY = this.prevScaleY * dy / this.pdy;
				this.Invalidate();
				return;
			}

			if (flags.HasFlag(AggInputFlags.MouseLeft))
			{
				this.GradientCenterX = x + this.pdx;
				this.GradientCenterY = y + this.pdy;
				this.Invalidate();
			}

			if (flags.HasFlag(AggInputFlags.MouseRight))
			{
				double dx = x - this.GradientCenterX;
				double dy = y - this.GradientCenterY;
				this.GradientScale = this.prevScale * Math.Sqrt((dx * dx) + (dy * dy)) / Math.Sqrt((this.pdx * this.pdx) + (this.pdy * this.pdy));
				this.GradientAngle = this.prevAngle + Math.Atan2(dy, dx) - Math.Atan2(this.pdy, this.pdx);
				this.Invalidate();
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			this.mouseMove = false;
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		/// <summary>
		/// C++ on_draw's gradient pass: the 110-radius circle at (350, 280) filled by span_gradient (d 0 to 150)
		/// through the inverse of the gradient's matrix. <paramref name="frameTransform"/> takes demo coordinates
		/// to <paramref name="destination"/>'s pixels.
		/// </summary>
		private void RenderGradient(IImageByte destination, ScanlineRasterizer rasterizer, Affine frameTransform)
		{
			Affine gradientMatrix = this.GradientMatrix() * frameTransform;
			gradientMatrix.invert();
			span_gradient spanGenerator = this.NewSpanGenerator(new span_interpolator_linear(gradientMatrix));

			var circle = new Ellipse(0.0, 0.0, EllipseRadius, EllipseRadius, 64);
			rasterizer.reset();
			rasterizer.add_path(new VertexSourceApplyTransform(circle, Affine.NewTranslation(CenterX, CenterY) * frameTransform));
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), destination, new span_allocator(), spanGenerator);
		}

		/// <summary>C++ <c>mtx_g1</c> before its inversion: gradient space to demo space.</summary>
		private Affine GradientMatrix()
		{
			return Affine.NewScaling(this.GradientScale, this.GradientScale)
				* Affine.NewScaling(this.GradientScaleX, this.GradientScaleY)
				* Affine.NewRotation(this.GradientAngle)
				* Affine.NewTranslation(this.GradientCenterX, this.GradientCenterY);
		}

		/// <summary>C++ <c>span_gen</c>: the chosen gradient, reflected, through the splines' colors and the profile, d 0 to 150.</summary>
		private span_gradient NewSpanGenerator(span_interpolator_linear interpolator)
		{
			int item = this.GradientRbox.CurrentItem;
			IGradient gradient = this.gradients[item >= 1 && item <= 5 ? item : 0];
			return new span_gradient(interpolator, gradient, this.NewColorFunction(), 0, 150);
		}

		/// <summary>The splines' colors through the profile.</summary>
		private IColorFunction NewColorFunction()
		{
			var colors = new Color[256];
			for (int i = 0; i < 256; i++)
			{
				colors[i] = Rgba8.FromRgba(this.SplineR.Spline[i], this.SplineG.Spline[i], this.SplineB.Spline[i], this.SplineA.Spline[i]);
			}

			return new ProfileColors(colors, this.Profile.Gamma);
		}

		/// <summary>C++ <c>color_function_profile</c>: gradient step v is color <c>colors[profile[v]]</c>.</summary>
		private sealed class ProfileColors : IColorFunction
		{
			private readonly Color[] colors;
			private readonly byte[] profile;

			public ProfileColors(Color[] colors, byte[] profile)
			{
				this.colors = colors;
				this.profile = profile;
			}

			public Color this[int v] => this.colors[this.profile[v]];

			public int size() => 256;
		}
	}
}
