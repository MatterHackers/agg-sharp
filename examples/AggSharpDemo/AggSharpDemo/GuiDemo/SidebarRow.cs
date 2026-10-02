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

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// sidebar.rs's ToggleButton: a full-width row of a demo sidebar. Its fill is inset from the panel edges
	/// (TB_BG_INSET_*) so consecutive lit rows do not fuse into one block. Both the GUI demo's window list
	/// (<see cref="DemoSidebar"/>) and the AGG demo list (<see cref="AggDemoSidebar"/>) are built from it, so the
	/// two bars look and behave as one component.
	/// </summary>
	public class SidebarRow : IconTextButton
	{
		/// <summary>sidebar.rs's TB_HEIGHT: the height of one row, in design units (times DeviceScale).</summary>
		public const double RowHeight = 22;

		/// <summary>collapsing_header.rs indents a group's content by INDENT / 2.</summary>
		public const double GroupContentInset = SidebarGroupHeader.TriangleX / 2;

		/// <param name="contentInset">Added to the row's TB_INDENT; see <see cref="GroupContentInset"/>.</param>
		public SidebarRow(string text, string iconGlyph, ThemeConfig theme, double contentInset)
			: base(text, iconGlyph, theme, 10)
		{
			this.HAnchor = HAnchor.Stretch;
			this.TextHAnchor = HAnchor.Left;
			this.Height = (RowHeight - 2) * DeviceScale;

			// The ghost button's pill (widgets/button.rs, radius 6)
			this.BackgroundRadius = 6 * DeviceScale;

			// TB_INDENT (22) less the fill's own left padding (5), so the label nests under the group
			// triangle; the About row uses the same indent, as agg-gui's does. The icon goes in the padding.
			this.Margin = new BorderDouble(contentInset + 17, 1, 5, 1);
			this.TextPadding = new BorderDouble(5, 0);
		}

		/// <summary>Whether the row is lit: its window is open, or its demo is the one shown.</summary>
		public bool IsOn { get; set; }

		/// <summary>A lit row is filled with the accent and lettered white, as agg-gui's active ghost button is;
		/// an unlit one is transparent over the panel.</summary>
		public void ApplyTheme(DemoTheme demoTheme)
		{
			Color accent = DemoTheme.ColorOf(demoTheme.Accent);
			this.BackgroundColor = this.IsOn ? accent : Color.Transparent;
			this.TextColor = this.IsOn ? Color.White : demoTheme.Palette.TextColor;
			this.HoverColor = this.IsOn ? accent : demoTheme.Theme.MinimalShade;
			this.Invalidate();
		}
	}
}
