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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;

namespace MatterHackers.AggSharpDemo
{
	/// <summary>
	/// The AGG demo list docked on the right of the AGG demos page, built from the same parts as the GUI demo's
	/// <see cref="DemoSidebar"/> (heading, search box, collapsible groups of <see cref="SidebarRow"/>s) at the same
	/// width, so switching tabs changes what is listed, not how. One row per demo, grouped by
	/// <see cref="DemoRegistry.Groups"/> and alphabetical within a group; the shown demo's row is lit.
	/// </summary>
	/// <remarks>Automation names: "AGG Sidebar Search", "AGG Sidebar Group &lt;group&gt;" and
	/// <see cref="RowName"/> for a demo's row.</remarks>
	public class AggDemoSidebar : FlowLayoutWidget
	{
		private readonly DemoTheme demoTheme;

		private readonly TextWidget heading;

		private readonly GuiWidget separator;

		private readonly Dictionary<string, SidebarRow> rows = new Dictionary<string, SidebarRow>();

		/// <summary>Each row's group, for the filter.</summary>
		private readonly Dictionary<string, string> groupOfRow = new Dictionary<string, string>();

		private readonly Dictionary<string, SidebarGroupHeader> groupHeaders = new Dictionary<string, SidebarGroupHeader>();

		public AggDemoSidebar(IReadOnlyList<AggDemo> demos, DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.demoTheme = demoTheme;
			ThemeConfig theme = demoTheme.Theme;

			this.Name = "AGG Demo Sidebar";
			this.HAnchor = HAnchor.Absolute;
			this.Width = GuiDemoShell.SidebarWidth * DeviceScale;
			this.VAnchor = VAnchor.Stretch;

			// The same heading and separator as the GUI demo's sidebar.
			this.heading = new TextWidget("AGG Demos", pointSize: 12)
			{
				Name = "AGG Sidebar Heading",
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(12, 4, 12, 12),
			};
			this.AddChild(this.heading);
			this.separator = new HorizontalLine()
			{
				Margin = new BorderDouble(0, 4, 0, 6),
			};
			this.AddChild(this.separator);

			this.Search = new SidebarSearchBox("AGG Sidebar Search", theme);
			this.Search.TextChanged += (s, e) => this.Filter.SetQuery(this.Search.Text);
			this.AddChild(this.Search);

			var scroll = new ScrollableWidget(autoScroll: true)
			{
				Name = "AGG Sidebar Scroll",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			scroll.ScrollArea.HAnchor = HAnchor.Stretch;
			demoTheme.StyleScroll(scroll);
			this.AddChild(scroll);

			var list = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
			};
			scroll.AddChild(list);

			foreach (string group in DemoRegistry.Groups)
			{
				IReadOnlyList<AggDemo> entries = DemoRegistry.GroupEntries(demos, group);
				if (entries.Count == 0)
				{
					continue;
				}

				var header = new SidebarGroupHeader(group)
				{
					Name = "AGG Sidebar Group " + group,
					Margin = new BorderDouble(0, 0, 0, this.groupHeaders.Count == 0 ? 0 : 2),
				};
				header.Click += (s, e) => this.Filter.SetCollapsed(group, !this.Filter.IsCollapsed(group));
				this.groupHeaders.Add(group, header);
				list.AddChild(header);

				foreach (AggDemo demo in entries)
				{
					var row = new SidebarRow(demo.Name, null, theme, SidebarRow.GroupContentInset)
					{
						Name = RowName(demo.Name),
					};
					row.Click += (s, e) => this.DemoSelected?.Invoke(demo);
					this.rows.Add(demo.Name, row);
					this.groupOfRow.Add(demo.Name, group);
					list.AddChild(row);
				}
			}

			this.Filter.Changed += (s, e) => this.ApplyFilter();
			this.ApplyFilter();
			this.ApplyTheme();
			demoTheme.ThemeChanged += this.DemoTheme_ThemeChanged;
		}

		/// <summary>A row was clicked; the page shows that demo.</summary>
		public event Action<AggDemo> DemoSelected;

		/// <summary>The search text and collapsed groups, and from them what is shown.</summary>
		public SidebarFilter Filter { get; } = new SidebarFilter();

		public SidebarSearchBox Search { get; }

		/// <summary>The name of the demo whose row is lit; null for none.</summary>
		public string SelectedDemo { get; private set; }

		/// <summary>The automation name of the row for the demo <paramref name="demoName"/>.</summary>
		public static string RowName(string demoName) => "AGG Sidebar " + demoName;

		/// <summary>The row for the demo <paramref name="demoName"/>.</summary>
		public SidebarRow RowOf(string demoName) => this.rows[demoName];

		/// <summary>The collapsible header of <paramref name="group"/>.</summary>
		public SidebarGroupHeader HeaderOf(string group) => this.groupHeaders[group];

		/// <summary>Lights <paramref name="demoName"/>'s row and puts out the one lit before.</summary>
		public void SetSelectedDemo(string demoName)
		{
			this.SelectedDemo = demoName;
			foreach (KeyValuePair<string, SidebarRow> pair in this.rows)
			{
				pair.Value.IsOn = pair.Key == demoName;
				pair.Value.ApplyTheme(this.demoTheme);
			}
		}

		public override void OnClosed(EventArgs e)
		{
			this.demoTheme.ThemeChanged -= this.DemoTheme_ThemeChanged;
			base.OnClosed(e);
		}

		private void DemoTheme_ThemeChanged(object sender, EventArgs e) => this.ApplyTheme();

		private void ApplyFilter()
		{
			foreach (KeyValuePair<string, SidebarGroupHeader> pair in this.groupHeaders)
			{
				pair.Value.IsOpen = !this.Filter.IsCollapsed(pair.Key);
				pair.Value.Visible = this.Filter.IsGroupVisible(this.groupOfRow.Where(row => row.Value == pair.Key).Select(row => row.Key));
			}

			foreach (KeyValuePair<string, SidebarRow> pair in this.rows)
			{
				pair.Value.Visible = this.Filter.IsEntryVisible(this.groupOfRow[pair.Key], pair.Key);
			}

			this.Invalidate();
		}

		/// <summary>As <see cref="DemoSidebar"/>: the panel fill, heading, headers, search box and rows follow
		/// the palette and accent.</summary>
		private void ApplyTheme()
		{
			DemoPalette palette = this.demoTheme.Palette;
			this.BackgroundColor = palette.PanelFill;
			this.heading.TextColor = palette.TextColor;
			this.separator.BackgroundColor = palette.Separator;

			foreach (SidebarGroupHeader header in this.groupHeaders.Values)
			{
				header.TextColor = palette.TextColor;
				header.TriangleColor = palette.TextDim;
				header.SeparatorColor = palette.Separator;
			}

			this.Search.ApplyTheme(this.demoTheme);

			foreach (SidebarRow row in this.rows.Values)
			{
				row.ApplyTheme(this.demoTheme);
			}

			this.Invalidate();
		}
	}
}
