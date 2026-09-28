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
	/// agg-gui's "Multi Touch" window (text_demos/multi_touch.rs multi_touch): the heading, a separator, the
	/// gesture hint, the live input-source line, the <see cref="MultiTouchView"/> taking the rest, and the
	/// source link, on the panel fill.
	/// </summary>
	public class MultiTouchWindow : FlowLayoutWidget
	{
		public const string SourceUrl = "https://github.com/larsbrubaker/agg-gui/blob/main/demo-ui/src/windows/text_demos/multi_touch.rs";

		private const double Gap = 8;

		public MultiTouchWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(10);
			this.BackgroundColor = demoTheme.Palette.PanelFill;

			var kit = new MiscDemoKit(demoTheme);
			GuiWidget Spaced(GuiWidget widget)
			{
				widget.Margin = new BorderDouble(bottom: Gap);
				widget.HAnchor = HAnchor.Stretch;
				return widget;
			}

			// agg-gui draws this line strong; WrappedTextWidget has no bold, so it is the plain weight.
			this.AddChild(Spaced(kit.Wrapped("This demo only works on devices with multitouch support (e.g. mobiles, tablets, and trackpads).", 13)));

			var separator = new GuiWidget { Height = Math.Max(1, Math.Round(DeviceScale)) };
			this.AddChild(Spaced(separator));

			this.AddChild(Spaced(kit.Wrapped("Try touch gestures Pinch/Stretch, Rotation, and Pressure with 2+ fingers.", 11)));

			this.View = new MultiTouchView(demoTheme)
			{
				Name = "Multi Touch View",
				VAnchor = VAnchor.Stretch,
			};

			this.StatusLabel = kit.Label(this.View.InputSource, 12);
			this.StatusLabel.Name = "Multi Touch Status";
			this.StatusLabel.AutoExpandBoundsToText = true;
			this.StatusLabel.Margin = new BorderDouble(bottom: Gap);
			this.AddChild(this.StatusLabel);
			this.View.InputSourceChanged += (s, e) => this.StatusLabel.Text = this.View.InputSource;

			this.AddChild(Spaced(this.View));

			this.AddChild(new Hyperlink("(source code)", kit.Theme, SourceUrl)
			{
				Name = "Multi Touch Source Link",
				HAnchor = HAnchor.Right,
			});

			void Recolor(object sender, EventArgs e)
			{
				this.BackgroundColor = demoTheme.Palette.PanelFill;
				separator.BackgroundColor = demoTheme.Palette.Separator;
				kit.Recolor();
				this.View.Invalidate();
			}

			Recolor(null, null);
			demoTheme.ThemeChanged += Recolor;
			this.Closed += (s, e) => demoTheme.ThemeChanged -= Recolor;
		}

		/// <summary>The arrow canvas the gestures drive.</summary>
		public MultiTouchView View { get; }

		/// <summary>The "Input source: ..." line.</summary>
		public TextWidget StatusLabel { get; }
	}
}
