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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's "Bézier Curve" window (animation/bezier.rs bezier_curve): the Quadratic / Cubic radios, the
	/// drag hint and the convex-fill note above the curve editor, on the panel fill.
	/// </summary>
	public class BezierWindow : FlowLayoutWidget
	{
		private const double Gap = 8;

		public BezierWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(8);
			this.BackgroundColor = demoTheme.Palette.PanelFill;

			var kit = new MiscDemoKit(demoTheme);

			// egui's default degree is cubic.
			this.QuadraticRadio = kit.Radio("Bezier Quadratic", "Quadratic Bézier", 13);
			this.CubicRadio = kit.Radio("Bezier Cubic", "Cubic Bézier", 13);
			this.CubicRadio.Checked = true;
			var radios = kit.Column();
			radios.Margin = new BorderDouble(bottom: Gap);
			radios.AddChild(this.QuadraticRadio);
			radios.AddChild(this.CubicRadio);
			this.AddChild(radios);

			var hint = kit.Label("Move the points by dragging them.");
			hint.Margin = new BorderDouble(bottom: Gap);
			this.AddChild(hint);

			var note = kit.Wrapped("Only convex curves can be accurately filled.", 10);
			note.HAnchor = HAnchor.Stretch;
			note.Margin = new BorderDouble(bottom: Gap);
			this.AddChild(note);

			this.Canvas = new BezierCanvas(demoTheme)
			{
				Name = "Bezier Canvas",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.AddChild(this.Canvas);

			this.CubicRadio.CheckedStateChanged += (s, e) =>
			{
				this.Canvas.Cubic = this.CubicRadio.Checked;
				this.Canvas.Invalidate();
			};

			void Recolor(object sender, EventArgs e)
			{
				this.BackgroundColor = demoTheme.Palette.PanelFill;
				kit.Recolor();
			}

			demoTheme.ThemeChanged += Recolor;
			this.Closed += (s, e) => demoTheme.ThemeChanged -= Recolor;
		}

		/// <summary>Picks the 3-point curve.</summary>
		public RadioButton QuadraticRadio { get; }

		/// <summary>Picks the 4-point curve (the default).</summary>
		public RadioButton CubicRadio { get; }

		/// <summary>The curve editor.</summary>
		public BezierCanvas Canvas { get; }
	}
}
