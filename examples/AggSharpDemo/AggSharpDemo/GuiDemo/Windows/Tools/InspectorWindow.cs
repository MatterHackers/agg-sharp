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

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tools
{
	/// <summary>
	/// The Inspector window, app_builder.rs's "Inspector" window around agg-gui's InspectorPanel: the library's
	/// <see cref="InspectorPanel"/> showing the page's own widget tree, coloured from the demo theme. Opened from the
	/// sidebar's Tools group or the backend panel's Inspector checkbox, which share one open state as they share
	/// agg-gui's show_inspector cell.
	/// </summary>
	public class InspectorWindow : InspectorPanel
	{
		private readonly DemoTheme demoTheme;

		public InspectorWindow(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.ApplyTheme();
			demoTheme.ThemeChanged += this.DemoTheme_ThemeChanged;
		}

		public override void OnClosed(EventArgs e)
		{
			this.demoTheme.ThemeChanged -= this.DemoTheme_ThemeChanged;
			base.OnClosed(e);
		}

		private void DemoTheme_ThemeChanged(object sender, EventArgs e) => this.ApplyTheme();

		private void ApplyTheme()
		{
			DemoPalette palette = this.demoTheme.Palette;
			Color accent = DemoTheme.ColorOf(this.demoTheme.Accent);
			bool dark = this.demoTheme.Theme.IsDarkTheme;
			this.Style = new InspectorStyle()
			{
				Background = palette.WindowFill,
				HeaderBackground = HeaderTint(palette.WindowFill, dark),
				Text = palette.TextColor,
				DimText = palette.TextDim,
				HoveredRow = palette.WidgetBackground,
				SelectedRow = accent.WithAlpha(110),
				Separator = palette.Separator,
			};
		}

		/// <summary>agg-gui's c_header_bg: the panel fill scaled to 80% on a dark theme, 94% on a light one.</summary>
		private static Color HeaderTint(Color fill, bool dark)
		{
			double f = dark ? .80 : .94;
			return new Color((int)Math.Round(fill.red * f), (int)Math.Round(fill.green * f), (int)Math.Round(fill.blue * f));
		}
	}
}
