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

using System.Globalization;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// agg-rust's gsv_text demo (AGG has no gsv_text.cpp; the C++ reference is tools/cpp-renderer's
	/// demo_gsv_text.cpp): AGG's built-in gsv_text vector font, stroked with round caps and joins - a title,
	/// a subtitle, the alphabet and digits in five colours, a large "Aa Bb Cc" and a size readout.
	/// </summary>
	/// <remarks>
	/// The layout is y down (the title's baseline at y = 40), so the demo <see cref="DrawsYDown"/>, the glyphs
	/// are flipped (<see cref="gsv_text.flip"/>) and the sliders get flip = true, as a flip_y = false C++ demo's.
	/// The GPU strokes the same outlines with its own anti-aliasing.
	/// </remarks>
	public class GsvTextDemo : AggDemo
	{
		private const double XOffset = 20;
		private const double YOffset = 40;

		private static readonly (string Text, Color Color)[] Samples =
		{
			("ABCDEFGHIJKLM", new Color(200, 0, 0)),
			("NOPQRSTUVWXYZ", new Color(0, 150, 0)),
			("abcdefghijklm", new Color(0, 0, 200)),
			("nopqrstuvwxyz", new Color(150, 100, 0)),
			("0123456789 !@#$%", new Color(0, 100, 150)),
		};

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		public GsvTextDemo()
		{
			this.SizeSlider = new SliderCtrl(10, this.Height - 40.0, this.Width - 10.0, this.Height - 33.0, true)
			{
				Label = "Text Size={0:F0}",
			};
			this.SizeSlider.SetRange(8, 64);
			this.SizeSlider.Value = 24;

			this.StrokeSlider = new SliderCtrl(10, this.Height - 20.0, this.Width - 10.0, this.Height - 13.0, true)
			{
				Label = "Stroke Width={0:F1}",
			};
			this.StrokeSlider.SetRange(0.3, 4);
			this.StrokeSlider.Value = 1;

			this.ctrls.Add(this.SizeSlider);
			this.ctrls.Add(this.StrokeSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>The body text height; the title, subtitle and "Aa Bb Cc" scale with it.</summary>
		public SliderCtrl SizeSlider { get; }

		/// <summary>The body stroke width; the other lines' strokes scale with it.</summary>
		public SliderCtrl StrokeSlider { get; }

		public override string Name => "gsv_text";

		public override string Category => "Vector Graphics";

		public override string Description => "AGG's built-in vector font, stroked. Drag the sliders to change the text size and stroke width.";

		public override int Width => 600;

		public override int Height => 500;

		public override bool DrawsYDown => true;

		public override void Draw(Graphics2D graphics)
		{
			graphics.Clear(Color.White);

			double textSize = this.SizeSlider.Value;
			double strokeWidth = this.StrokeSlider.Value;

			DrawLine(graphics, "AGG for C# - GSV Text", textSize * 1.5, XOffset, YOffset, strokeWidth * 1.5, new Color(0, 50, 120));
			DrawLine(graphics, "Built-in vector font - no dependencies", textSize * 0.7, XOffset, YOffset + (textSize * 2.0), strokeWidth * 0.7, new Color(100, 100, 100));

			double baseY = YOffset + (textSize * 4.0);
			for (int i = 0; i < Samples.Length; i++)
			{
				double y = baseY + (i * (textSize * 1.5));
				if (y + textSize > this.Height)
				{
					break;
				}

				DrawLine(graphics, Samples[i].Text, textSize, XOffset, y, strokeWidth, Samples[i].Color);
			}

			double largeY = baseY + (Samples.Length * (textSize * 1.5)) + textSize;
			if (largeY + (textSize * 3.0) < this.Height)
			{
				DrawLine(graphics, "Aa Bb Cc", textSize * 2.5, XOffset, largeY, strokeWidth * 2.0, new Color(30, 30, 30));
			}

			string label = string.Format(CultureInfo.InvariantCulture, "Size: {0:F0}px  Stroke: {1:F1}", textSize, strokeWidth);
			DrawLine(graphics, label, 12.0, this.Width - 200.0, 20.0, 0.8, new Color(140, 140, 140, 200));

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

		// One line of flipped gsv_text through a round-capped, round-joined stroke.
		private static void DrawLine(Graphics2D graphics, string text, double size, double x, double y, double strokeWidth, Color color)
		{
#pragma warning disable CS0618 // gsv_text is obsolete for UI text; this demo shows exactly this font.
			var gsv = new gsv_text();
#pragma warning restore CS0618
			gsv.flip(true);
			gsv.size(size, 0.0);
			gsv.start_point(x, y);
			gsv.text(text);
			graphics.Render(new Stroke(gsv, strokeWidth) { LineCap = LineCap.Round, LineJoin = LineJoin.Round }, color);
		}
	}
}
