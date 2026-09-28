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

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout
{
	/// <summary>
	/// agg-gui's "Interactive Container" window (misc/interactive_container.rs, after egui's demo of the same
	/// name): an explanation, the clickable <see cref="InteractiveContainer"/>, a separator and a note.
	/// </summary>
	public class InteractiveContainerWindow : FlowLayoutWidget
	{
		private const double Gap = 12;

		public InteractiveContainerWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(16);

			var kit = new MiscDemoKit(demoTheme);
			GuiWidget Spaced(GuiWidget widget)
			{
				widget.HAnchor = HAnchor.Stretch;
				widget.Margin = new BorderDouble(bottom: Gap);
				return widget;
			}

			this.AddChild(Spaced(kit.Wrapped(
				"This demo shows how to use Widget::on_event to build interactive container widgets that may contain other widgets. Click the container to count clicks; the nested buttons run only their own action.",
				12)));

			this.Container = new InteractiveContainer(demoTheme) { Name = "Interactive Container" };
			this.AddChild(Spaced(this.Container));

			var separator = new GuiWidget
			{
				Height = Math.Max(1, Math.Round(DeviceScale)),
			};
			this.AddChild(Spaced(separator));

			this.AddChild(Spaced(kit.Wrapped("Clicking the background increments the count; clicking Reset or + 100 does not.", 11)));

			void Recolor(object sender, EventArgs e)
			{
				this.BackgroundColor = demoTheme.Palette.PanelFill;
				separator.BackgroundColor = demoTheme.Palette.Separator;
				kit.Recolor();
				this.Container.Recolor();
			}

			Recolor(null, null);
			demoTheme.ThemeChanged += Recolor;
			this.Closed += (s, e) => demoTheme.ThemeChanged -= Recolor;
		}

		public InteractiveContainer Container { get; }
	}
}
