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
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's line_patterns.cpp: nine Bezier curves, each drawn with its own picture laid along it
	/// (renderer_outline_image), the picture's brightness made its transparency so its white background drops out.
	/// Drag the curves' points; Scale X stretches the pictures along the curves, Start X slides them.
	/// </summary>
	/// <remarks>
	/// The software mode is the C++ render, byte for byte, drawn into a BGR canvas as C++ draws through
	/// pixfmt_bgr24. The image renderer writes pixels itself; any other surface - the GPU - takes those same
	/// pixels as rectangles through a <see cref="Graphics2DSpanImage"/>.
	/// </remarks>
	public class LinePatternsDemo : AggDemo
	{
		private static readonly double[,] DefaultCurves =
		{
			{ 64, 19, 14, 126, 118, 266, 19, 265 },
			{ 112, 113, 178, 32, 200, 132, 125, 438 },
			{ 401, 24, 326, 149, 285, 11, 177, 77 },
			{ 188, 427, 129, 295, 19, 283, 25, 410 },
			{ 451, 346, 302, 218, 265, 441, 459, 400 },
			{ 454, 198, 14, 13, 220, 291, 483, 283 },
			{ 301, 398, 355, 231, 209, 211, 170, 353 },
			{ 484, 101, 222, 33, 486, 435, 487, 138 },
			{ 143, 147, 11, 45, 83, 427, 132, 197 },
		};

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly BrightnessToAlphaSource[] patterns = new BrightnessToAlphaSource[9];

		private ImageBuffer canvas;

		public LinePatternsDemo()
		{
			// C++ m_ctrl_color: an srgba8 of rgba(0, 0.3, 0.5, 0.3), handed to ctrls that draw rgba8.
			Color ctrlColor = SrgbLut.FromRgbaThroughSrgba8(0, 0.3, 0.5, 0.3);
			for (int i = 0; i < 9; i++)
			{
				var curve = new BezierCtrl { LineColor = ctrlColor };
				curve.SetCurve(DefaultCurves[i, 0], DefaultCurves[i, 1], DefaultCurves[i, 2], DefaultCurves[i, 3], DefaultCurves[i, 4], DefaultCurves[i, 5], DefaultCurves[i, 6], DefaultCurves[i, 7]);
				this.Curves[i] = curve;
				this.ctrls.Add(curve);
				this.patterns[i] = BrightnessToAlphaSource.Load(i + 1);
			}

			// line_patterns.cpp runs with flip_y = true and gives its sliders !flip_y.
			this.ScaleXSlider = new SliderCtrl(5.0, 5.0, 240.0, 12.0, false) { Label = "Scale X={0:F2}" };
			this.ScaleXSlider.SetRange(0.2, 3.0);
			this.ScaleXSlider.Value = 1.0;

			this.StartXSlider = new SliderCtrl(250.0, 5.0, 495.0, 12.0, false) { Label = "Start X={0:F2}" };
			this.StartXSlider.SetRange(0.0, 10.0);
			this.StartXSlider.Value = 0.0;

			this.ctrls.Add(this.ScaleXSlider);
			this.ctrls.Add(this.StartXSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_curve1</c> to <c>m_curve9</c>: the curves, each drawn with pattern 1 to 9.</summary>
		public BezierCtrl[] Curves { get; } = new BezierCtrl[9];

		/// <summary>C++ <c>m_scale_x</c>: how far the patterns are stretched along the curves, 0.2 to 3.</summary>
		public SliderCtrl ScaleXSlider { get; }

		/// <summary>C++ <c>m_start_x</c>: how far along the curves the patterns start, 0 to 10 pixels.</summary>
		public SliderCtrl StartXSlider { get; }

		public override string Name => "line_patterns";

		public override string Category => "Paths & Strokes";

		public override string Description => "Lines drawn with image patterns: nine Bezier curves, each with its own picture laid along it. Drag the points to bend the curves; Scale X stretches the pictures, Start X slides them.";

		public override int Width => 500;

		public override int Height => 450;

		public override void Draw(Graphics2D graphics)
		{
			if (graphics is ImageGraphics2D && graphics.DestImage is IImageByte)
			{
				this.DrawSoftware(graphics);
			}
			else
			{
				this.DrawSpans(graphics);
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

		private void DrawSoftware(Graphics2D graphics)
		{
			if (this.canvas == null)
			{
				this.canvas = new ImageBuffer(this.Width, this.Height, 24, new BlenderBGR());
			}

			Graphics2D canvasGraphics = this.canvas.NewGraphics2D();
			canvasGraphics.Clear(Rgba8.FromRgba(1.0, 1.0, 0.95));

			this.DrawCurves(new ImageClippingProxy(this.canvas));

			// The canvas is opaque and whole pixels, so this is a straight copy.
			graphics.Render(this.canvas, 0, 0);
		}

		/// <summary>Any other surface - the GPU - takes the image renderer's pixels as rectangles through a <see cref="Graphics2DSpanImage"/>.</summary>
		private void DrawSpans(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Rgba8.FromRgba(1.0, 1.0, 0.95));
			var spans = new Graphics2DSpanImage(graphics, this.Width, this.Height);
			this.DrawCurves(new ImageClippingProxy(spans));
			spans.Flush();
		}

		/// <summary>C++ draw_curve: one pattern, renderer and rasterizer shared by all nine curves.</summary>
		private void DrawCurves(ImageClippingProxy destination)
		{
			var pattern = new line_image_pattern(new pattern_filter_bilinear_RGBA_Bytes());
			var renderer = new ImageLineRenderer(destination, pattern);
			var rasterizer = new rasterizer_outline_aa(renderer);
			for (int i = 0; i < 9; i++)
			{
				pattern.create(this.patterns[i]);
				renderer.scale_x(this.ScaleXSlider.Value);
				renderer.start_x(this.StartXSlider.Value);
				rasterizer.add_path(this.Curves[i].Curve());
			}
		}
	}
}

