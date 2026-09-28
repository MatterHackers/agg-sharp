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
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout.Scrolling
{
	/// <summary>
	/// The Scrolling window's "Stick to end" tab, agg-gui's stick_to_end.rs: a row list that gains a row every frame
	/// it is drawn, in a <see cref="ScrollableWidget.StickToBottom"/> view that follows the new rows until the user
	/// scrolls away.
	/// </summary>
	public class StickToEndTab : GuiWidget
	{
		private bool rowRequested;

		internal StickToEndTab(DemoTheme demoTheme, MiscDemoKit kit)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Padding = new BorderDouble(10),
			};
			this.AddChild(column);

			WrappedTextWidget intro = kit.Wrapped(
				"Rows enter from the bottom every layout pass; the scrollbar stays glued to the end unless you scroll away.  "
				+ "Scroll up to detach; return to the bottom to re-attach.",
				11);
			intro.HAnchor = HAnchor.Stretch;
			column.AddChild(intro);

			this.List = new ScrollingRowList(demoTheme, 0, 18, i => $"This is row {i + 1}");
			this.Scroll = new ScrollableWidget(autoScroll: true)
			{
				Name = "Scrolling Stick To End List",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				StickToBottom = true,
				Margin = new BorderDouble(top: 6),
			};
			this.Scroll.ScrollArea.HAnchor = HAnchor.Stretch;
			this.Scroll.AddChild(this.List);
			column.AddChild(this.Scroll);
		}

		public ScrollableWidget Scroll { get; }

		public ScrollingRowList List { get; }

		/// <summary>Appends one row - what each drawn frame does, as agg-gui's CounterTicker does each layout.</summary>
		public void AddRow() => this.List.RowCount++;

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			// One row per frame, added on the next idle so it lands in a fresh layout and asks for the frame after.
			// Nothing is drawn while the tab is hidden, so rows only pile up while someone is watching.
			if (!this.rowRequested)
			{
				this.rowRequested = true;
				UiThread.RunOnIdle(() =>
				{
					this.rowRequested = false;
					this.AddRow();
				});
			}
		}
	}
}
