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

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout.Scrolling
{
	/// <summary>
	/// The "Scrolling" window, a port of agg-gui's scrolling demo (demo-ui/src/windows/scrolling/mod.rs, egui's
	/// Scrolling sample): a <see cref="TabView"/> of six sub-demos, each in its own file beside this one. The tabs
	/// are named "Scrolling Tab &lt;label&gt;", the tab view "Scrolling Tabs".
	/// </summary>
	public class ScrollingWindow : GuiWidget
	{
		public const string SourceUrl = "https://github.com/larsbrubaker/agg-gui/blob/main/demo-ui/src/windows/scrolling/mod.rs";

		public static readonly string[] TabLabels = { "Appearance", "Scroll to", "Scroll a lot of lines", "Scroll a large canvas", "Stick to end", "Bidirectional" };

		private readonly DemoTheme demoTheme;
		private readonly MiscDemoKit kit;

		public ScrollingWindow(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.kit = new MiscDemoKit(demoTheme);
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			this.Tabs = new TabView { Name = "Scrolling Tabs" };
			this.AddChild(this.Tabs);

			this.Appearance = new AppearanceTab(demoTheme, this.kit);
			this.ScrollTo = new ScrollToTab(demoTheme, this.kit);
			this.ManyLines = new ManyLinesTab(demoTheme, this.kit);
			this.LargeCanvas = new LargeCanvasTab(demoTheme, this.kit);
			this.StickToEnd = new StickToEndTab(demoTheme, this.kit);
			this.Bidirectional = new BidirectionalTab(demoTheme, this.kit);

			this.AddTab(TabLabels[0], this.Appearance);
			this.AddTab(TabLabels[1], this.ScrollTo);
			this.AddTab(TabLabels[2], this.ManyLines);
			this.AddTab(TabLabels[3], this.LargeCanvas);
			this.AddTab(TabLabels[4], this.StickToEnd);
			this.AddTab(TabLabels[5], this.Bidirectional);

			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		public TabView Tabs { get; }

		public AppearanceTab Appearance { get; }

		public ScrollToTab ScrollTo { get; }

		public ManyLinesTab ManyLines { get; }

		public LargeCanvasTab LargeCanvas { get; }

		public StickToEndTab StickToEnd { get; }

		public BidirectionalTab Bidirectional { get; }

		/// <summary>The name of the tab labelled <paramref name="label"/>.</summary>
		public static string TabName(string label) => "Scrolling Tab " + label;

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		private void AddTab(string label, GuiWidget page) => this.Tabs.AddTab(label, page, TabName(label));

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
			this.Tabs.PointSize = this.kit.FontSize(12);
			this.Appearance.Recolor();
			this.ScrollTo.Recolor();
			this.ManyLines.Recolor();
			this.LargeCanvas.Recolor();
			this.Bidirectional.Recolor();
		}
	}
}
