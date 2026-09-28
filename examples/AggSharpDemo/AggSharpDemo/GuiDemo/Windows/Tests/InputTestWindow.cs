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
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>
	/// agg-gui's "Input Test" window (tests/basic/input_probe, its counterpart of egui's input_test.rs): a note, a
	/// Clear button and an "Include hover events" checkbox over four probe areas side by side, each handling a
	/// different set of mouse events and listing what it made of them.
	/// </summary>
	public class InputTestWindow : FlowLayoutWidget
	{
		private const double Gap = 8;

		private readonly List<InputProbe> probes = new List<InputProbe>();

		public InputTestWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(10);
			double s = DeviceScale;

			var kit = new MiscDemoKit(demoTheme);
			WrappedTextWidget note = kit.Wrapped(
				"This tests how widgets classify raw mouse events. Each probe below handles a different set of events. "
				+ "Try clicking, double-clicking, triple-clicking, and dragging each one with any mouse button.",
				11.5);
			note.HAnchor = HAnchor.Stretch;
			note.Margin = new BorderDouble(bottom: Gap);
			this.AddChild(note);

			var controls = new FlowLayoutWidget { HAnchor = HAnchor.Left | HAnchor.Fit, VAnchor = VAnchor.Fit, Margin = new BorderDouble(bottom: Gap) };
			ThemedTextButton clear = kit.Button("Input Test Clear", "Clear");
			clear.HAnchor = HAnchor.Absolute;
			clear.VAnchor = VAnchor.Absolute | VAnchor.Center;
			clear.Size = new Vector2(70 * s, 28 * s);
			clear.Margin = new BorderDouble(right: 12);
			clear.Click += (sender, e) => this.Clear();
			controls.AddChild(clear);

			this.IncludeHover = kit.CheckBox("Input Test Include Hover", "Include hover events", false, 12);
			this.IncludeHover.VAnchor = VAnchor.Center;
			this.IncludeHover.CheckedStateChanged += (sender, e) =>
			{
				foreach (InputProbe probe in this.probes)
				{
					probe.IncludeHover = this.IncludeHover.Checked;
				}
			};
			controls.AddChild(this.IncludeHover);
			this.AddChild(controls);

			var separator = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				Height = Math.Max(1, Math.Round(s)),
				Margin = new BorderDouble(bottom: Gap),
			};
			this.AddChild(separator);

			// Four probes side by side, each an equal share of the width.
			var probeRow = new FlowLayoutWidget { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
			foreach (ProbeKind kind in new[] { ProbeKind.Hover, ProbeKind.Click, ProbeKind.Drag, ProbeKind.ClickAndDrag })
			{
				var probe = new InputProbe(demoTheme, kind)
				{
					Name = "Input Test Probe " + InteractionClassifier.Title(kind),
					Margin = new BorderDouble(left: this.probes.Count == 0 ? 0 : Gap),
				};
				probeRow.AddChild(probe);
				this.probes.Add(probe);
			}

			this.AddChild(probeRow);

			void Recolor(object sender, EventArgs e)
			{
				this.BackgroundColor = demoTheme.Palette.PanelFill;
				separator.BackgroundColor = demoTheme.Palette.Separator;
				kit.Recolor();
				foreach (InputProbe probe in this.probes)
				{
					probe.Invalidate();
				}
			}

			Recolor(null, null);
			demoTheme.ThemeChanged += Recolor;
			this.Closed += (sender, e) => demoTheme.ThemeChanged -= Recolor;
		}

		public CheckBox IncludeHover { get; }

		/// <summary>The Hover, Click, Drag and Click + Drag probes, in that order.</summary>
		public IReadOnlyList<InputProbe> Probes => this.probes;

		/// <summary>Empties every probe's history and forgets any gesture in progress.</summary>
		public void Clear()
		{
			foreach (InputProbe probe in this.probes)
			{
				probe.Clear();
			}
		}
	}
}
