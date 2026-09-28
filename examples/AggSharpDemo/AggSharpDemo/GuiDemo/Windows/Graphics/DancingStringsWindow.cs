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
	/// agg-gui's "Dancing Strings" window (animation/dancing.rs dancing_strings): a Colored check box above the
	/// animated strings, on the panel fill.
	/// </summary>
	public class DancingStringsWindow : FlowLayoutWidget
	{
		public DancingStringsWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(8);
			this.BackgroundColor = demoTheme.Palette.PanelFill;

			var kit = new MiscDemoKit(demoTheme);
			this.ColoredCheckBox = kit.CheckBox("Dancing Strings Colored", "Colored", false);
			this.ColoredCheckBox.HAnchor = HAnchor.Left;
			this.ColoredCheckBox.Margin = new BorderDouble(bottom: 8);
			this.AddChild(this.ColoredCheckBox);

			this.View = new DancingStringsView(demoTheme)
			{
				Name = "Dancing Strings View",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.AddChild(this.View);

			this.ColoredCheckBox.CheckedStateChanged += (s, e) =>
			{
				this.View.Colored = this.ColoredCheckBox.Checked;
				this.View.Invalidate();
			};

			void Recolor(object sender, EventArgs e)
			{
				this.BackgroundColor = demoTheme.Palette.PanelFill;
				kit.Recolor();
			}

			demoTheme.ThemeChanged += Recolor;
			this.Closed += (s, e) => demoTheme.ThemeChanged -= Recolor;
		}

		/// <summary>Switches the strings to the centre-to-edge gradient.</summary>
		public CheckBox ColoredCheckBox { get; }

		/// <summary>The animated strings.</summary>
		public DancingStringsView View { get; }
	}
}
