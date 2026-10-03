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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tools
{
	/// <summary>
	/// The "System" window, a port of agg-gui's demo-ui/src/windows/system.rs: a <see cref="TabView"/> ("System Tabs")
	/// of the process-wide typography controls (<see cref="SystemFontTab"/>) and a live preview of them
	/// (<see cref="SystemSampleTextTab"/>). The tabs are named "System Tab &lt;label&gt;".
	/// </summary>
	public class SystemTypographyWindow : GuiWidget
	{
		public const string SourceUrl = "https://github.com/larsbrubaker/agg-gui/blob/main/demo-ui/src/windows/system.rs";

		public static readonly string[] TabLabels = { "Font", "Sample Text" };

		private readonly DemoTheme demoTheme;
		private readonly MiscDemoKit kit;

		public SystemTypographyWindow(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.kit = new MiscDemoKit(demoTheme);
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			this.Tabs = new TabView { Name = "System Tabs" };
			this.AddChild(this.Tabs);

			this.FontTab = new SystemFontTab(this.kit);
			this.SampleText = new SystemSampleTextTab(this.kit);
			this.Tabs.AddTab(TabLabels[0], this.FontTab, TabName(TabLabels[0]));
			this.Tabs.AddTab(TabLabels[1], this.SampleText, TabName(TabLabels[1]));

			this.FontTab.SettingChanged += this.OnSettingChanged;

			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		public TabView Tabs { get; }

		public SystemFontTab FontTab { get; }

		public SystemSampleTextTab SampleText { get; }

		/// <summary>The name of the tab labelled <paramref name="label"/>.</summary>
		public static string TabName(string label) => "System Tab " + label;

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		private void OnSettingChanged(object sender, EventArgs e)
		{
			// The preview takes its face when built, so it is rebuilt. The invalidate asks for the next frame;
			// every other buffered label re-rasters in it on its own, because each setting here (hinting
			// included) bumps an epoch the backbuffers compare against.
			this.SampleText.Rebuild();
			this.Invalidate();
		}

		private void OnThemeChanged(object sender, EventArgs e)
		{
			this.Recolor();
			this.Invalidate();
		}

		/// <summary>Pushes the current theme into the widgets that copied their colours when they were built.</summary>
		private void Recolor()
		{
			this.kit.Recolor();
			DemoPalette palette = this.demoTheme.Palette;
			this.BackgroundColor = palette.PanelFill;
			// The selected tab is filled with the page's colour so it joins the page; the band behind the tabs is the
			// canvas tone, a step darker than the panel in both palettes.
			this.Tabs.PageColor = palette.PanelFill;
			this.Tabs.BarColor = palette.BackgroundColor;
			this.Tabs.SeparatorColor = palette.Separator;
			this.Tabs.TextColor = palette.TextColor;
			this.Tabs.TextDimColor = palette.TextDim;
			this.Tabs.HoverColor = palette.Separator;
			this.Tabs.PointSize = this.kit.FontSize(13);
			this.FontTab.Recolor();
			this.SampleText.Recolor();
		}
	}
}
