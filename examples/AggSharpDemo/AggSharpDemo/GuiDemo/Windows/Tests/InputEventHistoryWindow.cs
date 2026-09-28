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

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>
	/// agg-gui's "Input Event History" window (tests/basic/controls.rs input_event_history, after egui's
	/// input_event_history.rs): a note, an "Include pointer/mouse movements" checkbox and a scrolling box that
	/// records the raw input events it receives, repeats coalesced with an ×N counter.
	/// </summary>
	public class InputEventHistoryWindow : FlowLayoutWidget
	{
		public InputEventHistoryWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(10);

			var kit = new MiscDemoKit(demoTheme);
			WrappedTextWidget note = kit.Wrapped("Recent history of raw input events. Consecutive identical events coalesce with an ×N counter.", 11.5);
			note.HAnchor = HAnchor.Stretch;
			note.Margin = new BorderDouble(bottom: 8);
			this.AddChild(note);

			this.IncludeMovements = kit.CheckBox("Input Event History Include Movements", "Include pointer/mouse movements", false, 12);
			// Left, not the default Absolute, so the column places it inside its padding.
			this.IncludeMovements.HAnchor = HAnchor.Left;
			this.IncludeMovements.Margin = new BorderDouble(bottom: 8);
			this.AddChild(this.IncludeMovements);

			this.Recorder = new EventHistoryView(demoTheme) { Name = "Input Event History Recorder" };
			this.IncludeMovements.CheckedStateChanged += (s, e) => this.Recorder.IncludeMovements = this.IncludeMovements.Checked;

			this.Scroll = new ScrollableWidget(autoScroll: true)
			{
				Name = "Input Event History Scroll",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.Scroll.ScrollArea.HAnchor = HAnchor.Stretch;
			this.Scroll.AddChild(this.Recorder);
			this.Scroll.BoundsChanged += (s, e) => this.Recorder.SetViewportHeight(this.Scroll.Height);
			this.AddChild(this.Scroll);

			void Recolor(object sender, EventArgs e)
			{
				this.BackgroundColor = demoTheme.Palette.PanelFill;
				kit.Recolor();
				this.Recorder.Invalidate();
			}

			Recolor(null, null);
			demoTheme.ThemeChanged += Recolor;
			this.Closed += (s, e) => demoTheme.ThemeChanged -= Recolor;
		}

		public CheckBox IncludeMovements { get; }

		public ScrollableWidget Scroll { get; }

		public EventHistoryView Recorder { get; }
	}
}
