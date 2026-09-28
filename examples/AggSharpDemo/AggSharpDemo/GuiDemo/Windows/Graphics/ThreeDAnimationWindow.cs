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
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's "3D Animation" window (windows.rs cube_content): an SSAA segmented row above the animated
	/// bar grid. The segments are agg-gui's Off / 4x / 9x / 16x samples per pixel, linear factors 1 to 4.
	/// </summary>
	public class ThreeDAnimationWindow : FlowLayoutWidget
	{
		/// <summary>The segment labels, index i being linear factor i + 1.</summary>
		public static readonly string[] SsaaLabels = { "Off", "4×", "9×", "16×" };

		public ThreeDAnimationWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			this.BarGrid = new BarGridWidget(demoTheme)
			{
				Name = "3D Animation Bar Grid",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				SoftwarePlaceholder = "3D needs the GPU renderer",
			};

			var row = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(6, 4),
			};
			var label = new TextWidget("SSAA", pointSize: 10, textColor: demoTheme.Palette.TextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(right: 6),
			};
			row.AddChild(label);

			this.Ssaa = new SegmentedControl(SsaaLabels, demoTheme.Theme, this.BarGrid.SsaaFactor - 1) { Name = "3D Animation SSAA" };
			this.Ssaa.SelectedIndexChanged += (s, e) => this.BarGrid.SsaaFactor = this.Ssaa.SelectedIndex + 1;
			row.AddChild(this.Ssaa);

			this.AddChild(row);
			this.AddChild(this.BarGrid);

			void Recolor(object sender, System.EventArgs e) => label.TextColor = demoTheme.Palette.TextColor;
			demoTheme.ThemeChanged += Recolor;
			this.Closed += (s, e) => demoTheme.ThemeChanged -= Recolor;
		}

		/// <summary>The animated bars.</summary>
		public BarGridWidget BarGrid { get; }

		/// <summary>The SSAA selector: Off, 4x, 9x, 16x.</summary>
		public SegmentedControl Ssaa { get; }

		/// <summary>The bar grid's linear SSAA factor (1 to 4), kept in step with the selector: setting it
		/// moves the selector, and picking a segment sets it.</summary>
		public int SsaaFactor
		{
			get => this.BarGrid.SsaaFactor;
			set => this.Ssaa.SelectedIndex = value - 1;
		}
	}
}
