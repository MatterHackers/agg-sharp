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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>
	/// agg-gui's "Flex Layout Test" window (tests/basic/layout.rs layout_test): a column of tinted boxes, three at
	/// fixed widths and one stretched across the row, over a note on fixed versus flexible children.
	/// </summary>
	public class FlexLayoutTestWindow : FlowLayoutWidget
	{
		/// <summary>The box labels, in order; agg-gui gives each a fixed width except Stretch, which fills the row.</summary>
		public static readonly string[] Labels = { "Left", "Center", "Right", "Stretch" };

		/// <summary>agg-gui's fixed box widths (the last, 0, stretches).</summary>
		private static readonly double[] Widths = { 80, 120, 100, 0 };

		// Translucent tints, so they read on the light and the dark panel alike; the theme leaves them alone.
		private static readonly Color[] Tints =
		{
			DemoPalette.Rgba(0.22, 0.45, 0.88, 0.25),
			DemoPalette.Rgba(0.18, 0.72, 0.42, 0.25),
			DemoPalette.Rgba(0.88, 0.25, 0.18, 0.25),
			DemoPalette.Rgba(0.86, 0.78, 0.40, 0.25),
		};

		private const double Gap = 12;

		private readonly List<GuiWidget> boxes = new List<GuiWidget>();

		public FlexLayoutTestWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(14);

			var kit = new MiscDemoKit(demoTheme);
			TextWidget heading = kit.Label("Alignment examples", 12);
			// A flow column only places Left/Center/Right/Stretch children inside its padding; an Absolute
			// child (a label's default) stays at x 0.
			heading.HAnchor = HAnchor.Left;
			heading.Margin = new BorderDouble(bottom: Gap);
			this.AddChild(heading);

			double s = DeviceScale;
			for (int i = 0; i < Labels.Length; i++)
			{
				var box = new GuiWidget
				{
					Name = "Flex Layout Test " + Labels[i],
					BackgroundColor = Tints[i],
					BorderColor = DemoPalette.Rgba(0, 0, 0, 0.15),
					Border = new BorderDouble(1),
					Padding = new BorderDouble(6),
					VAnchor = VAnchor.Fit,
					Margin = new BorderDouble(bottom: Gap),
				};
				if (Widths[i] > 0)
				{
					// A GuiWidget draws its Border ring outside its bounds (layout counts it with the margin), where
					// agg-gui's Container draws it inside; the bounds give up the ring so the drawn box is the width
					// agg-gui's SizedBox gives it.
					box.HAnchor = HAnchor.Left | HAnchor.Absolute;
					box.Width = Widths[i] * s - box.DeviceBorder.Width;
				}
				else
				{
					box.HAnchor = HAnchor.Stretch;
				}

				TextWidget label = kit.Label(Labels[i], 12);
				label.HAnchor = HAnchor.Left;
				label.VAnchor = VAnchor.Bottom;
				box.AddChild(label);
				this.boxes.Add(box);
				this.AddChild(box);
			}

			var separator = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				Height = Math.Max(1, Math.Round(s)),
				Margin = new BorderDouble(bottom: Gap),
			};
			this.AddChild(separator);

			WrappedTextWidget note = kit.Wrapped("FlowLayoutWidget rows and columns control alignment.\nA fixed-width child keeps its size; a stretched child fills the remaining space.", 11);
			note.HAnchor = HAnchor.Stretch;
			this.AddChild(note);

			void Recolor(object sender, EventArgs e)
			{
				this.BackgroundColor = demoTheme.Palette.PanelFill;
				separator.BackgroundColor = demoTheme.Palette.Separator;
				kit.Recolor();
			}

			Recolor(null, null);
			demoTheme.ThemeChanged += Recolor;
			this.Closed += (s, e) => demoTheme.ThemeChanged -= Recolor;
		}

		/// <summary>The four tinted boxes, in <see cref="Labels"/> order. Each box's Border ring is drawn outside its
		/// bounds, so its drawn width is Width plus DeviceBorder.Width.</summary>
		public IReadOnlyList<GuiWidget> Boxes => this.boxes;
	}
}
