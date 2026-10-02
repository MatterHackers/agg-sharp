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
	/// The AGG demos page's menu bar, built as the GUI demo's <see cref="DemoTopBar"/> is: a Demos menu with one
	/// submenu per <see cref="DemoRegistry.Groups"/> entry listing its demos alphabetically (the sidebar's groups
	/// and order), and at the right end the drawer button a narrow page shows its sidebar with.
	/// </summary>
	/// <remarks>Automation names: "demos.&lt;group&gt;" and "agg.&lt;demo name&gt;" for the menu items, "AGG Demo
	/// Sidebar Toggle" for the drawer button.</remarks>
	public class AggDemoTopBar : GuiWidget
	{
		private readonly DemoTheme demoTheme;

		private readonly IReadOnlyList<AggDemo> demos;

		public AggDemoTopBar(IReadOnlyList<AggDemo> demos, DemoTheme demoTheme)
		{
			this.demos = demos;
			this.demoTheme = demoTheme;
			this.Name = "AGG Demo Top Bar";
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;
			this.Height = GuiDemoShell.TopBarHeight * DeviceScale;

			this.Menus = new[]
			{
				DemoTopBar.WithIcon(
					new MenuItemModel
					{
						Text = "Demos",
						SubMenuItems = this.DemosItems,
					},
					DemoTopBar.DemosGroupIcon),
			};
			this.MenuBar = new MenuBarWidget(this.Menus, demoTheme.Theme)
			{
				Name = "AGG Demo Menu Bar",
				VAnchor = VAnchor.Center,
			};
			this.AddChild(this.MenuBar);
			DemoTopBar.SizeTitles(this.MenuBar, this.Menus);

			this.SidebarDrawerButton = new IconTextButton("", "\uF0C9", demoTheme.Theme)
			{
				Name = "AGG Demo Sidebar Toggle",
				ToolTipText = "Demos",
				HAnchor = HAnchor.Right,
				VAnchor = VAnchor.Center,
				Visible = false,
			};
			this.SidebarDrawerButton.TextPadding = new BorderDouble(4, 0, 0, 0);
			this.SidebarDrawerButton.Click += (s, e) => this.SetSidebarDrawerOpen(!this.SidebarDrawerOpen);
			this.AddChild(this.SidebarDrawerButton);

			this.ApplyTheme();
			demoTheme.ThemeChanged += this.DemoTheme_ThemeChanged;
		}

		/// <summary>Raised by Demos > group > name.</summary>
		public event Action<AggDemo> DemoRequested;

		/// <summary>Raised after the drawer button flipped <see cref="SidebarDrawerOpen"/>.</summary>
		public event EventHandler SidebarDrawerToggled;

		/// <summary>The menu model the bar is built from (top level: Demos).</summary>
		public IReadOnlyList<MenuItemModel> Menus { get; }

		public MenuBarWidget MenuBar { get; }

		public IconTextButton SidebarDrawerButton { get; }

		/// <summary>Whether the narrow page's sidebar drawer is open; ignored on a wide page.</summary>
		public bool SidebarDrawerOpen { get; private set; }

		public void SetSidebarDrawerOpen(bool open)
		{
			if (open != this.SidebarDrawerOpen)
			{
				this.SidebarDrawerOpen = open;
				this.SidebarDrawerToggled?.Invoke(this, EventArgs.Empty);
			}
		}

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the page, which is rebuilt each time its tab is shown
			this.demoTheme.ThemeChanged -= this.DemoTheme_ThemeChanged;
			base.OnClosed(e);
		}

		private IReadOnlyList<MenuItemModel> DemosItems()
		{
			return DemoRegistry.Groups
				.Where(group => DemoRegistry.GroupEntries(this.demos, group).Count > 0)
				.Select(group => DemoTopBar.WithIcon(
					new MenuItemModel
					{
						Text = group,
						AutomationName = "demos." + group,
						SubMenuItems = () => DemoRegistry.GroupEntries(this.demos, group)
							.Select(demo => DemoTopBar.Item(demo.Name, "agg." + demo.Name, () => this.DemoRequested?.Invoke(demo)))
							.ToList(),
					},
					DemoTopBar.DemosGroupIcon))
				.ToList();
		}

		private void DemoTheme_ThemeChanged(object sender, EventArgs e) => this.ApplyTheme();

		/// <summary>As <see cref="DemoTopBar"/>: the titles copied the text colour when they were built.</summary>
		private void ApplyTheme()
		{
			this.BackgroundColor = this.demoTheme.Palette.TopBarBackground;
			foreach (var title in this.MenuBar.Children.OfType<ThemedTextButton>())
			{
				title.TextColor = this.demoTheme.Theme.TextColor;
			}

			this.SidebarDrawerButton.TextColor = this.demoTheme.Theme.TextColor;
			this.SidebarDrawerButton.BackgroundColor = Color.Transparent;
			this.SidebarDrawerButton.HoverColor = this.demoTheme.Theme.SlightShade;
			this.Invalidate();
		}
	}
}
