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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's gamma_ctrl.cpp: a gamma curve control whose curve is the rasterizer's anti-aliasing gamma for
	/// the stroked ellipses, the skewed text and the ring of arrows below it. Drag either control point, or
	/// move the active one with the arrow keys.
	/// </summary>
	public class GammaCtrlDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		public GammaCtrlDemo()
		{
			// gamma_ctrl.cpp runs with flip_y = true and gives its ctrl !flip_y.
			this.Gamma = new GammaCtrl(10.0, 10.0, 300.0, 200.0, false);
			this.ctrls.Add(this.Gamma);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>g_ctrl</c>. C++ loads and saves its values in gamma.txt; the port starts at the defaults.</summary>
		public GammaCtrl Gamma { get; }

		public override string Name => "gamma_ctrl";

		public override string Category => "Rendering";

		public override string Description => "Anti-aliasing gamma set by a spline. Drag the curve's control points (or use the arrow keys) and watch thin lines and text change weight.";

		public override int Width => 500;

		public override int Height => 400;

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			this.Gamma.SetTextSize(10.0, 12.0);
			this.ctrls.Render(graphics);

			// Every shape is filled through the curve: the software rasterizer's gamma, or the GPU's coverage gamma.
			this.DrawShapes(graphics);
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

		private void DrawShapes(Graphics2D graphics)
		{
			double ewidth = (this.Width / 2) - 10;
			double ecenter = this.Width / 2;

			// C++ keeps the color in an srgba8, so every color is converted to sRGB and back on its way to the
			// rgba8 pixel format.
			Color black = SrgbLut.FromSrgba8(0, 0, 0);
			void StrokeEllipse(Graphics2D graphics, double x, double y, double rx, double ry, double width, Color color)
			{
				GammaFill.Render(graphics, new Stroke(new Ellipse(x, y, rx, ry, 100), width), color, this.Gamma);
			}

			StrokeEllipse(graphics, ecenter, 220, ewidth, 15, 2.0, black);
			StrokeEllipse(graphics, ecenter, 220, 11, 11, 2.0, black);

			Color gray = SrgbLut.FromSrgba8(127, 127, 127);
			StrokeEllipse(graphics, ecenter, 260, ewidth, 15, 2.0, gray);
			StrokeEllipse(graphics, ecenter, 260, 11, 11, 2.0, gray);

			Color lightGray = SrgbLut.FromSrgba8(192, 192, 192);
			StrokeEllipse(graphics, ecenter, 300, ewidth, 15, 2.0, lightGray);
			StrokeEllipse(graphics, ecenter, 300, 11, 11, 2.0, lightGray);

			Color blue = SrgbLut.FromRgbaThroughSrgba8(0.0, 0.0, 0.4);
			StrokeEllipse(graphics, ecenter, 340, ewidth, 15.5, 1.0, blue);
			StrokeEllipse(graphics, ecenter, 340, 10.5, 10.5, 1.0, blue);
			StrokeEllipse(graphics, ecenter, 380, ewidth, 15.5, 0.4, blue);
			StrokeEllipse(graphics, ecenter, 380, 10.5, 10.5, 0.4, blue);
			StrokeEllipse(graphics, ecenter, 420, ewidth, 15.5, 0.1, blue);
			StrokeEllipse(graphics, ecenter, 420, 10.5, 10.5, 0.1, blue);

			// C++ gsv_text_outline: the text stroked 2 wide with round joins and caps, then skewed.
			var textOutline = new VertexStorage();
#pragma warning disable CS0618 // gsv_text is obsolete for UI text; C++ draws with exactly this font.
			var text = new gsv_text();
#pragma warning restore CS0618
			text.text("Text 2345");
			text.size(50, 20);
			text.start_point(320, 10);
			foreach (VertexData vertex in text.Vertices())
			{
				if (vertex.IsStop)
				{
					break;
				}

				textOutline.Add(vertex.Position.X, vertex.Position.Y, vertex.Command);
			}

			var strokedText = new Stroke(textOutline, 2.0) { LineJoin = LineJoin.Round, LineCap = LineCap.Round };
			GammaFill.Render(graphics, new VertexSourceApplyTransform(strokedText, Affine.NewSkewing(0.15, 0.0)), SrgbLut.FromRgbaThroughSrgba8(0.0, 0.5, 0.0), this.Gamma);

			// 35 two-part arrows in a ring around (400, 130); each open subpath is filled as the closed triangle.
			var arrow = new VertexStorage();
			arrow.MoveTo(30, -1.0);
			arrow.LineTo(60, 0.0);
			arrow.LineTo(30, 1.0);
			arrow.MoveTo(27, -1.0);
			arrow.LineTo(10, 0.0);
			arrow.LineTo(27, 1.0);
			Color red = SrgbLut.FromRgbaThroughSrgba8(0.5, 0.0, 0.0);
			for (int i = 0; i < 35; i++)
			{
				Affine mtx = Affine.NewRotation(i / 35.0 * Math.PI * 2.0) * Affine.NewTranslation(400, 130);
				GammaFill.Render(graphics, new VertexSourceApplyTransform(arrow, mtx), red, this.Gamma);
			}
		}
	}
}
