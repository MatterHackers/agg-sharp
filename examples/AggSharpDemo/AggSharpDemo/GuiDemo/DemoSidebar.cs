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
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// The GUI demo's window list docked on the right, after agg-gui's demo-ui/src/sidebar.rs: a heading, an
	/// About row, a search box, one collapsible section per group with a toggle row per window, and an
	/// "Organize windows" button.
	/// </summary>
	/// <remarks>
	/// Rows are bound both ways to the <see cref="DemoWindowHost"/>: clicking one opens or closes its window,
	/// and the host's <see cref="DemoWindowHost.OpenChanged"/> (the title bar's close button, the Demos menu)
	/// relights the row. What is visible is decided by <see cref="Filter"/>; this class only applies it.
	/// Automation names: "Sidebar About", "Sidebar Search", "Sidebar Group &lt;group&gt;",
	/// "Sidebar &lt;title&gt;" and "Sidebar Organize".
	/// </remarks>
	public class DemoSidebar : FlowLayoutWidget
	{
		/// <summary>sidebar.rs's TB_HEIGHT: the height of one toggle row.</summary>
		private const double RowHeight = 22;

		private readonly DemoTheme demoTheme;

		private readonly DemoWindowHost host;

		private readonly TextWidget heading;

		private readonly List<GuiWidget> separators = new List<GuiWidget>();

		private readonly Dictionary<DemoSpec, ToggleRow> rows = new Dictionary<DemoSpec, ToggleRow>();

		private readonly Dictionary<string, ThemedTextButton> groupHeaders = new Dictionary<string, ThemedTextButton>();

		private readonly ThemedTextButton organizeButton;

		public DemoSidebar(DemoWindowHost host, DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.host = host;
			this.demoTheme = demoTheme;
			ThemeConfig theme = demoTheme.Theme;

			this.Name = "GuiDemo Sidebar";
			this.HAnchor = HAnchor.Absolute;
			this.Width = GuiDemoShell.SidebarWidth;
			this.VAnchor = VAnchor.Stretch;

			// HAnchor.Left, not the default Absolute: an Absolute child keeps its own X and ignores its Margin.
			this.heading = new TextWidget("agg-sharp Demo", pointSize: 12)
			{
				Name = "Sidebar Heading",
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(12, 4, 12, 12),
			};
			this.AddChild(this.heading);
			this.AddChild(this.Separator(6, 4));

			this.AboutRow = this.AddRow(this, GuiDemoSpecs.About, "About");
			this.AddChild(this.Separator(0, 6));

			this.Search = new ThemedTextEditWidget("", theme, messageWhenEmptyAndNotSelected: "Search...")
			{
				Name = "Sidebar Search",
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(10, 6, 10, 2),
			};
			this.Search.TextChanged += (s, e) => this.Filter.SetQuery(this.Search.Text);
			this.AddChild(this.Search);

			var scroll = new ScrollableWidget(autoScroll: true)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			scroll.ScrollArea.HAnchor = HAnchor.Stretch;
			this.AddChild(scroll);

			var list = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
			};
			scroll.AddChild(list);

			foreach (string group in GuiDemoSpecs.Groups)
			{
				var header = new ThemedTextButton("", theme)
				{
					Name = "Sidebar Group " + group,
					HAnchor = HAnchor.Stretch,
					TextHAnchor = HAnchor.Left,
					Height = RowHeight,
					Margin = new BorderDouble(4, 1, 5, 1),
					Padding = new BorderDouble(4, 0),
				};
				header.Click += (s, e) => this.Filter.SetCollapsed(group, !this.Filter.IsCollapsed(group));
				this.groupHeaders.Add(group, header);
				list.AddChild(header);

				foreach (DemoSpec spec in SidebarFilter.EntriesOf(group))
				{
					this.AddRow(list, spec, spec.Title);
				}
			}

			list.AddChild(this.Separator(8, 6));
			this.organizeButton = new ThemedTextButton("Organize windows", theme)
			{
				Name = "Sidebar Organize",
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(10, 12, 10, 4),
			};
			this.organizeButton.Click += (s, e) => this.host.Organize();
			list.AddChild(this.organizeButton);

			this.Filter.Changed += (s, e) => this.ApplyFilter();
			this.ApplyFilter();
			this.ApplyTheme();

			// The host and the theme outlive this widget only if the page is rebuilt, but unsubscribing keeps a
			// closed sidebar from being kept alive (and repainted into) by either.
			host.OpenChanged += this.Host_OpenChanged;
			demoTheme.ThemeChanged += this.DemoTheme_ThemeChanged;
		}

		/// <summary>The search text and collapsed groups, and from them what is shown.</summary>
		public SidebarFilter Filter { get; } = new SidebarFilter();

		public ThemedTextEditWidget Search { get; }

		/// <summary>The About toggle above the search box.</summary>
		public GuiWidget AboutRow { get; }

		/// <summary>The toggle row of <paramref name="spec"/>'s window (or of <see cref="GuiDemoSpecs.About"/>).</summary>
		public GuiWidget RowOf(DemoSpec spec) => spec == GuiDemoSpecs.About ? this.AboutRow : this.rows[spec];

		/// <summary>The collapsible header of <paramref name="group"/>.</summary>
		public GuiWidget HeaderOf(string group) => this.groupHeaders[group];

		/// <summary>The icon glyph drawn at the start of <paramref name="spec"/>'s row, or null for none.</summary>
		public string RowIconOf(DemoSpec spec) => ((IconTextButton)this.RowOf(spec)).IconGlyph;

		/// <summary>Where <paramref name="spec"/>'s row draws its icon, in the row's coordinates.</summary>
		public RectangleDouble RowIconBoundsOf(DemoSpec spec) => ((IconTextButton)this.RowOf(spec)).IconBounds;

		/// <summary>Whether <paramref name="spec"/>'s row is lit (its window is open).</summary>
		public bool IsRowOn(DemoSpec spec) => ((ToggleRow)this.RowOf(spec)).IsOn;

		public override void OnClosed(EventArgs e)
		{
			this.host.OpenChanged -= this.Host_OpenChanged;
			this.demoTheme.ThemeChanged -= this.DemoTheme_ThemeChanged;
			base.OnClosed(e);
		}

		private ToggleRow AddRow(GuiWidget parent, DemoSpec spec, string text)
		{
			var row = new ToggleRow(text, spec.Icon, this.demoTheme.Theme)
			{
				Name = spec == GuiDemoSpecs.About ? "Sidebar About" : "Sidebar " + spec.Title,
				IsOn = this.host.IsOpen(spec),
			};
			row.Click += (s, e) => this.host.SetOpen(spec, !this.host.IsOpen(spec));
			this.rows[spec] = row;
			parent.AddChild(row);
			return row;
		}

		private GuiWidget Separator(double above, double below)
		{
			var line = new HorizontalLine()
			{
				Margin = new BorderDouble(0, below, 0, above),
			};
			this.separators.Add(line);
			return line;
		}

		private void Host_OpenChanged(object sender, DemoSpec spec)
		{
			if (this.rows.TryGetValue(spec, out ToggleRow row))
			{
				row.IsOn = this.host.IsOpen(spec);
				this.ApplyTheme(row);
			}
		}

		private void DemoTheme_ThemeChanged(object sender, EventArgs e) => this.ApplyTheme();

		private void ApplyFilter()
		{
			foreach (KeyValuePair<string, ThemedTextButton> pair in this.groupHeaders)
			{
				// agg-gui's CollapsingHeader draws its open triangle before the name.
				pair.Value.Text = (this.Filter.IsCollapsed(pair.Key) ? "►  " : "▼  ") + pair.Key;
				pair.Value.Visible = this.Filter.IsGroupVisible(pair.Key);
			}

			foreach (KeyValuePair<DemoSpec, ToggleRow> pair in this.rows)
			{
				if (pair.Key != GuiDemoSpecs.About)
				{
					pair.Value.Visible = this.Filter.IsEntryVisible(pair.Key);
				}
			}

			this.Invalidate();
		}

		/// <summary>The rows and headers copied their colours when they were built, so they are recoloured
		/// from the palette; the hover shades follow the accent.</summary>
		private void ApplyTheme()
		{
			DemoPalette palette = this.demoTheme.Palette;
			this.BackgroundColor = palette.PanelFill;
			this.heading.TextColor = palette.TextColor;
			foreach (GuiWidget line in this.separators)
			{
				line.BackgroundColor = palette.Separator;
			}

			foreach (ThemedTextButton button in this.groupHeaders.Values)
			{
				button.TextColor = palette.TextColor;
				button.BackgroundColor = Color.Transparent;
				button.HoverColor = this.demoTheme.Theme.MinimalShade;
			}

			this.organizeButton.TextColor = palette.TextColor;
			this.organizeButton.BackgroundColor = palette.WidgetBackground;
			this.organizeButton.HoverColor = this.demoTheme.Theme.SlightShade;

			// The field's fill and border come from the theme's EditFieldColors every time it draws; its text and
			// hint colours are copied from them only on a focus change, so they are copied again here.
			ThemeConfig.StateColor fieldColors = this.Search.ActualTextEditWidget.InternalTextEditWidget.Focused
				? this.demoTheme.Theme.EditFieldColors.Focused
				: this.demoTheme.Theme.EditFieldColors.Inactive;
			this.Search.ActualTextEditWidget.TextColor = fieldColors.TextColor;
			this.Search.NoContentFieldDescription.TextColor = fieldColors.LightTextColor;

			foreach (ToggleRow row in this.rows.Values)
			{
				this.ApplyTheme(row);
			}

			this.Invalidate();
		}

		/// <summary>A lit row is filled with the accent and lettered white, as agg-gui's active ghost button is;
		/// an unlit one is transparent over the panel.</summary>
		private void ApplyTheme(ToggleRow row)
		{
			Color accent = DemoTheme.ColorOf(this.demoTheme.Accent);
			row.BackgroundColor = row.IsOn ? accent : Color.Transparent;
			row.TextColor = row.IsOn ? Color.White : this.demoTheme.Palette.TextColor;
			row.HoverColor = row.IsOn ? accent : this.demoTheme.Theme.MinimalShade;
			row.Invalidate();
		}

		/// <summary>sidebar.rs's ToggleButton: a full-width row that toggles one window. Its fill is inset from
		/// the panel edges (TB_BG_INSET_*) so consecutive lit rows do not fuse into one block.</summary>
		private class ToggleRow : IconTextButton
		{
			public ToggleRow(string text, string iconGlyph, ThemeConfig theme)
				: base(text, iconGlyph, theme, 10)
			{
				this.HAnchor = HAnchor.Stretch;
				this.TextHAnchor = HAnchor.Left;
				this.Height = RowHeight - 2;

				// TB_INDENT (22) less the fill's own left padding (5), so the label nests under the group
				// triangle; the About row uses the same indent, as agg-gui's does. The icon goes in the padding.
				this.Margin = new BorderDouble(17, 1, 5, 1);
				this.TextPadding = new BorderDouble(5, 0);
			}

			public bool IsOn { get; set; }
		}
	}
}
