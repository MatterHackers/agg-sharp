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

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Interaction
{
	/// <summary>
	/// agg-gui's "Drag and Drop" window (interaction/drag_and_drop.rs drag_and_drop): two lines of help above a
	/// <see cref="DragAndDropBoard"/> of three columns, and the source link, on the panel fill.
	/// </summary>
	public class DragAndDropWindow : FlowLayoutWidget
	{
		public const string SourceUrl = "https://github.com/larsbrubaker/agg-gui/blob/main/demo-ui/src/windows/interaction/drag_and_drop.rs";

		private const double Gap = 10;

		public DragAndDropWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(12);
			this.BackgroundColor = demoTheme.Palette.PanelFill;

			var kit = new MiscDemoKit(demoTheme);
			TextWidget intro = kit.Label("This is a simple example of drag-and-drop in agg-gui.", 11.5);
			intro.Margin = new BorderDouble(bottom: Gap);
			this.AddChild(intro);
			TextWidget hint = kit.Label("Drag items between columns.", 11.5);
			hint.Margin = new BorderDouble(bottom: Gap);
			this.AddChild(hint);

			this.Board = new DragAndDropBoard(demoTheme)
			{
				Name = "Drag and Drop Board",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(bottom: Gap),
			};
			this.AddChild(this.Board);

			this.AddChild(new Hyperlink("(source code)", kit.Theme, SourceUrl)
			{
				Name = "Drag and Drop Source Link",
				HAnchor = HAnchor.Right,
			});

			void Recolor(object sender, EventArgs e)
			{
				this.BackgroundColor = demoTheme.Palette.PanelFill;
				kit.Recolor();
				this.Board.Invalidate();
			}

			demoTheme.ThemeChanged += Recolor;
			this.Closed += (s, e) => demoTheme.ThemeChanged -= Recolor;
		}

		/// <summary>The three columns of draggable items.</summary>
		public DragAndDropBoard Board { get; }
	}
}
