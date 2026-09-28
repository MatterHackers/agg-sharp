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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>
	/// agg-gui's "SVG Test" window (tests/svg.rs), a progress viewer for agg-sharp's SVG renderer: every sample from
	/// resvg-test-suite beside its reference.png, each marked pass or fail by <see cref="SvgCompare"/>, with a
	/// count of how many pass and 50% / 100% / 200% zoom buttons over a view that scrolls both ways.
	/// </summary>
	public class SvgTestWindow : FlowLayoutWidget
	{
		private readonly List<(ThemedTextButton Button, double Zoom)> zoomButtons = new List<(ThemedTextButton, double)>();

		public SvgTestWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(10);

			var kit = new MiscDemoKit(demoTheme);
			WrappedTextWidget note = kit.Wrapped(
				"Each resvg-test-suite sample's reference.png beside agg-sharp's render at the same pixel size. "
				+ "Hold the mouse down on a render to see how it differs from the reference.",
				11.5);
			note.HAnchor = HAnchor.Stretch;
			this.AddChild(note);

			List<SvgTestSample> samples = SvgTestSample.LoadAll();
			this.Canvas = new SvgTestCanvas(demoTheme, samples) { Name = "SVG Test Canvas" };

			var controls = new FlowLayoutWidget { HAnchor = HAnchor.Stretch | HAnchor.Fit, VAnchor = VAnchor.Fit, Margin = new BorderDouble(0, 6) };
			foreach (double zoom in new[] { .5, 1, 2 })
			{
				ThemedTextButton button = kit.Button($"SVG Test Zoom {zoom * 100:0}", $"{zoom * 100:0}%");
				button.Margin = new BorderDouble(right: 6);
				button.Click += (sender, e) => this.Canvas.Zoom = zoom;
				controls.AddChild(button);
				this.zoomButtons.Add((button, zoom));
			}

			this.PassCount = samples.Count(sample => sample.Passes);
			this.Summary = kit.Label($"{this.PassCount} of {samples.Count} samples match their reference", 12);
			this.Summary.Name = "SVG Test Summary";
			this.Summary.VAnchor = VAnchor.Center;
			this.Summary.Margin = new BorderDouble(left: 12);
			controls.AddChild(this.Summary);
			this.AddChild(controls);

			this.Scroll = new ScrollableWidget(autoScroll: true)
			{
				Name = "SVG Test Scroll",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				HorizontalScroll = true,
			};
			this.Scroll.AddChild(this.Canvas);
			this.AddChild(this.Scroll);

			void Recolor(object sender, EventArgs e)
			{
				this.BackgroundColor = demoTheme.Palette.PanelFill;
				kit.Recolor();
				this.Canvas.Invalidate();
			}

			Recolor(null, null);
			demoTheme.ThemeChanged += Recolor;
			this.Closed += (sender, e) => demoTheme.ThemeChanged -= Recolor;
		}

		public SvgTestCanvas Canvas { get; }

		public ScrollableWidget Scroll { get; }

		public TextWidget Summary { get; }

		/// <summary>How many samples <see cref="SvgCompare"/> passes.</summary>
		public int PassCount { get; }
	}
}
