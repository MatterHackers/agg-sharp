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
	/// The "AGG Demos" tab, laid out as the GUI demo page (<see cref="GuiDemoShell"/>) is: a menu bar across the
	/// top, the selected AGG demo filling the area below it, and the demo list (<see cref="AggDemoSidebar"/>) docked
	/// on the right. Narrower than <see cref="GuiDemoShell.MobileBreakpoint"/> the list is a drawer, hidden until
	/// the bar's drawer button opens it, and demos are picked from the Demos menu - as the GUI demo's windows are.
	/// </summary>
	public class AggDemosPage : FlowLayoutWidget
	{
		private readonly DemoTheme demoTheme;

		private readonly IReadOnlyList<AggDemo> demos = DemoRegistry.CreateAggDemos();

		/// <summary>Recolours what the open demo's header copied from the theme.</summary>
		private Action recolorDemo;

		/// <param name="initialDemo">The demo to open on; null or an unknown name opens on
		/// <see cref="DemoRegistry.DefaultDemoName"/>.</param>
		public AggDemosPage(DemoTheme demoTheme, string initialDemo = null)
			: base(FlowDirection.TopToBottom)
		{
			this.demoTheme = demoTheme;
			this.Name = "AGG Demos Page";
			this.AnchorAll();

			this.TopBar = new AggDemoTopBar(this.demos, demoTheme);
			this.TopBar.DemoRequested += demo => this.ShowDemo(demo.Name);
			this.TopBar.SidebarDrawerToggled += (s, e) => this.UpdateSidebarLayout();
			this.AddChild(this.TopBar);

			var body = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.AddChild(body);

			this.Content = new GuiWidget()
			{
				Name = "AGG Demo Content",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			body.AddChild(this.Content);

			this.Sidebar = new AggDemoSidebar(this.demos, demoTheme);
			this.Sidebar.DemoSelected += demo => this.ShowDemo(demo.Name);
			body.AddChild(this.Sidebar);

			this.ApplyTheme();
			demoTheme.ThemeChanged += this.DemoTheme_ThemeChanged;

			this.ShowDemo(initialDemo);
		}

		/// <summary>Raised after a different demo is shown; the app remembers it.</summary>
		public event EventHandler SelectedDemoChanged;

		public AggDemoTopBar TopBar { get; }

		/// <summary>The area left of the sidebar the selected demo fills.</summary>
		public GuiWidget Content { get; }

		/// <summary>The demo list docked on the right.</summary>
		public AggDemoSidebar Sidebar { get; }

		/// <summary>The demo shown in <see cref="Content"/>.</summary>
		public AggDemo SelectedDemo { get; private set; }

		/// <summary>Whether the page is narrower than <see cref="GuiDemoShell.MobileBreakpoint"/>, so the list is a drawer.</summary>
		public bool IsNarrow => this.Width < GuiDemoShell.MobileBreakpoint * DeviceScale;

		/// <summary>Shows the demo named <paramref name="name"/> (the default one when it is not a demo), lights
		/// its row, and closes the drawer so a narrow page shows the demo rather than the list.</summary>
		public void ShowDemo(string name)
		{
			AggDemo demo = this.demos.FirstOrDefault(d => d.Name == name)
				?? this.demos.FirstOrDefault(d => d.Name == DemoRegistry.DefaultDemoName)
				?? this.demos[0];

			this.TopBar.SetSidebarDrawerOpen(false);
			if (demo == this.SelectedDemo)
			{
				return;
			}

			this.Content.CloseChildren();
			this.SelectedDemo = demo;
			this.Content.AddChild(this.CreateDemoView(demo, out this.recolorDemo));
			this.recolorDemo();
			this.Sidebar.SetSelectedDemo(demo.Name);
			this.SelectedDemoChanged?.Invoke(this, EventArgs.Empty);
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			base.OnBoundsChanged(e);
			this.UpdateSidebarLayout();
		}

		public override void OnClosed(EventArgs e)
		{
			this.demoTheme.ThemeChanged -= this.DemoTheme_ThemeChanged;
			base.OnClosed(e);
		}

		/// <summary>As <see cref="GuiDemoShell"/>'s sidebar: docked at <see cref="GuiDemoShell.SidebarWidth"/> on a
		/// wide page; on a narrow one hidden, or <see cref="GuiDemoShell.MobileSidebarWidth"/> wide while the drawer
		/// is open. The drawer button only shows on a narrow page.</summary>
		private void UpdateSidebarLayout()
		{
			bool narrow = this.IsNarrow;
			if (this.TopBar.SidebarDrawerButton.Visible != narrow)
			{
				this.TopBar.SidebarDrawerButton.Visible = narrow;
			}

			bool visible = !narrow || this.TopBar.SidebarDrawerOpen;
			double width = narrow
				? Math.Min(GuiDemoShell.MobileSidebarWidth * DeviceScale, this.Width)
				: GuiDemoShell.SidebarWidth * DeviceScale;

			// Only on a change: setting a width lays the body out again, which moves these bounds no further.
			if (this.Sidebar.Visible != visible)
			{
				this.Sidebar.Visible = visible;
			}

			if (this.Sidebar.Width != width)
			{
				this.Sidebar.Width = width;
			}
		}

		/// <summary>One AGG demo: its name, description, the render mode toggle, and the demo.</summary>
		private GuiWidget CreateDemoView(AggDemo demo, out Action recolor)
		{
			var page = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Padding = 8,
			};

			var view = new AggDemoView(demo)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};

			// The name and the render mode toggle share the first row and the description wraps on its own row
			// beneath, so a long description on a narrow (phone) page can neither run off the edge nor push the
			// toggle out of reach.
			var titleRow = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(0, 0, 0, 8),
			};
			var name = new TextWidget(demo.Name, pointSize: 14, bold: true)
			{
				Name = "AGG Demo Name",
			};
			titleRow.AddChild(name);
			titleRow.AddChild(new HorizontalSpacer());

			var softwareToggle = new CheckBox("Software (AGG reference)")
			{
				Name = "AGG Demo Software Toggle",
				VAnchor = VAnchor.Center,
			};
			softwareToggle.CheckedStateChanged += (sender, e) =>
				view.RenderMode = softwareToggle.Checked ? AggDemoRenderMode.Software : AggDemoRenderMode.Gpu;
			titleRow.AddChild(softwareToggle);

			var description = new WrappedTextWidget(demo.Description, pointSize: 10)
			{
				Name = "AGG Demo Description",
				Margin = new BorderDouble(0, 6, 0, 2),
			};

			page.AddChild(titleRow);
			page.AddChild(description);
			page.AddChild(view);

			recolor = () =>
			{
				Color textColor = this.demoTheme.Palette.TextColor;
				name.TextColor = textColor;
				description.TextColor = textColor;
				softwareToggle.TextColor = textColor;
			};
			return page;
		}

		private void DemoTheme_ThemeChanged(object sender, EventArgs e) => this.ApplyTheme();

		/// <summary>The demo area takes agg-gui's bg_color, as the GUI demo's canvas does; the top bar and sidebar
		/// recolour themselves.</summary>
		private void ApplyTheme()
		{
			this.Content.BackgroundColor = this.demoTheme.Palette.BackgroundColor;
			this.recolorDemo?.Invoke();
			this.Invalidate();
		}
	}
}
