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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;

namespace MatterHackers.AggSharpDemo
{
	/// <summary>
	/// The whole site as one widget: a sidebar listing the AGG demos and the GUI demo, and the page for
	/// whichever is selected. A head only has to put this in a window.
	/// </summary>
	/// <remarks>
	/// The site is one app with one theme: the GUI demo's light/dark/system preference and accent
	/// (<see cref="DemoTheme"/>) colour this chrome too, as agg-gui's theme is its whole app's. The theme
	/// outlives the GUI demo page, which is rebuilt each time it is selected.
	/// </remarks>
	public class AggSharpDemoApp : FlowLayoutWidget
	{
		private readonly IDemoStateStore guiDemoStateStore;

		/// <summary>True when this app owns <see cref="GuiWidget.DeviceScale"/> - see the constructor.</summary>
		private readonly bool followDisplayScale;

		private ScrollableWidget sidebarScroll;

		private GuiWidget content;

		private readonly List<TextWidget> sectionHeaders = new List<TextWidget>();

		/// <summary>The sidebar rows by the page name they show.</summary>
		private readonly Dictionary<string, ThemedTextButton> entries = new Dictionary<string, ThemedTextButton>();

		private string selectedEntry;

		/// <summary>Following the window's display scale (<see cref="UiScale.Follow"/>), once loaded.</summary>
		private IDisposable displayScaleFollowing;

		/// <summary>Recolours the open AGG demo page's header; null while the GUI demo page is open (it
		/// recolours itself).</summary>
		private Action recolorPage;

		/// <summary>The sidebar name of the GUI demo page; passing it as initialDemo opens on that page.</summary>
		public const string GuiDemoName = "GUI Demo";

		/// <param name="initialDemo">The name of the AGG demo to open on (as the sidebar lists it), or
		/// <see cref="GuiDemoName"/>; null or an unknown name opens on the first AGG demo.</param>
		/// <param name="guiDemoStateStore">Where the GUI demo keeps its windows and settings between runs; null
		/// starts it fresh every time.</param>
		/// <param name="followDisplayScale">
		/// True for a head: the app sets <see cref="GuiWidget.DeviceScale"/> to the display's scale (the Retina
		/// factor, or the browser's devicePixelRatio) before it builds anything, and rebuilds itself at the new
		/// scale when its window reports a different one. The platform hosts only report
		/// <see cref="SystemWindow.DisplayScale"/>; turning it into DeviceScale is the application's call
		/// (MatterCAD folds a user text size into it; <see cref="UiScale"/> is the shared policy), so without this the UI is laid out one device pixel per
		/// design unit - half size on a 2x display. False (tests) leaves the process-wide DeviceScale alone.
		/// </param>
		public AggSharpDemoApp(string initialDemo = null, IDemoStateStore guiDemoStateStore = null, bool followDisplayScale = false)
			: base(FlowDirection.LeftToRight)
		{
			this.AnchorAll();
			this.guiDemoStateStore = guiDemoStateStore;
			this.followDisplayScale = followDisplayScale;

			if (followDisplayScale)
			{
				// The display the app starts on, until its window says which one it is really on.
				UiScale.ApplyAtStartup();
			}

			// agg-gui's demo chains Noto Emoji behind its main font; the site's text is drawn in the default
			// faces, so they get the same fallback (the Misc Demos label shows it off).
			EmojiFont.ChainOnto(AggContext.DefaultFont);
			EmojiFont.ChainOnto(AggContext.DefaultFontBold);

			this.DemoTheme = new DemoTheme(systemPrefersDark: () => SystemAppearance.PrefersDark ?? true);
			if (guiDemoStateStore != null)
			{
				// Open in the saved theme even when the first page is an AGG demo.
				DemoStatePersistence.ApplyTheme(DemoState.Parse(guiDemoStateStore.Load()), this.DemoTheme);
			}

			this.BuildUi(initialDemo);
			this.DemoTheme.ThemeChanged += this.DemoTheme_ThemeChanged;
		}

		/// <summary>Builds the sidebar and opens <paramref name="openName"/> (an AGG demo's name or
		/// <see cref="GuiDemoName"/>), all at the current <see cref="GuiWidget.DeviceScale"/>.</summary>
		private void BuildUi(string openName)
		{
			this.sectionHeaders.Clear();
			this.entries.Clear();

			// The demo list is taller than a typical window, so the sidebar scrolls to keep every demo reachable.
			this.sidebarScroll = new ScrollableWidget(autoScroll: true)
			{
				HAnchor = HAnchor.Absolute,
				Width = 200 * GuiWidget.DeviceScale,
				VAnchor = VAnchor.Stretch,
			};
			sidebarScroll.ScrollArea.HAnchor = HAnchor.Stretch;
			this.AddChild(sidebarScroll);

			var sidebar = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				Padding = 8,
			};
			sidebarScroll.AddChild(sidebar);

			this.content = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.AddChild(this.content);

			sidebar.AddChild(this.SectionHeader("AGG Demos"));
			AggDemo openDemo = null;
			foreach (AggDemo demo in DemoRegistry.CreateAggDemos())
			{
				if (openDemo == null || demo.Name == openName)
				{
					openDemo = demo;
				}

				sidebar.AddChild(this.SidebarEntry(demo.Name, () => this.ShowAggDemoPage(demo)));
			}

			sidebar.AddChild(this.SectionHeader("GUI Demo"));
			sidebar.AddChild(this.SidebarEntry(GuiDemoName, () => this.ShowPage(GuiDemoName, new GuiDemoShell(this.DemoTheme, this.guiDemoStateStore))));

			this.ApplyTheme();

			// Open on a demo rather than an empty page: the first thing a visitor sees should be AGG drawing.
			if (openDemo == null || openName == GuiDemoName)
			{
				this.ShowPage(GuiDemoName, new GuiDemoShell(this.DemoTheme, this.guiDemoStateStore));
			}
			else
			{
				this.ShowAggDemoPage(openDemo);
			}
		}

		/// <summary>The light/dark preference and accent of the whole site: this chrome and the GUI demo page.</summary>
		public DemoTheme DemoTheme { get; }

		/// <summary>The page is built before its window is shown, which is when the platform installs the
		/// appearance provider, so System is asked again once the app is on screen.</summary>
		public override void OnLoad(EventArgs args)
		{
			this.DemoTheme.RefreshSystemPreference();
			if (this.followDisplayScale && this.displayScaleFollowing == null
				&& this.Parents<SystemWindow>().FirstOrDefault() is SystemWindow window)
			{
				// The window moved to a display with another scale (or the browser zoomed): rebuild everything
				// at it. Closing the GUI demo page saves its state to the store in design units, so it closes
				// at the old scale; the new page restores it, and the open page is reopened by name, so a
				// visitor sees the same page and windows, sized for the new display.
				string openName = null;
				this.displayScaleFollowing = UiScale.Follow(
					window,
					rebuild: () => this.BuildUi(openName),
					beforeRescale: () =>
					{
						openName = this.selectedEntry;
						this.CloseChildren();
					});
			}

			base.OnLoad(args);
		}

		public override void OnClosed(EventArgs e)
		{
			this.displayScaleFollowing?.Dispose();
			this.displayScaleFollowing = null;

			this.DemoTheme.ThemeChanged -= this.DemoTheme_ThemeChanged;
			base.OnClosed(e);
		}

		private void DemoTheme_ThemeChanged(object sender, EventArgs e) => this.ApplyTheme();

		/// <summary>The sidebar takes agg-gui's panel_fill and the page area its bg_color, as the GUI demo's
		/// own sidebar and canvas do.</summary>
		private void ApplyTheme()
		{
			DemoPalette palette = this.DemoTheme.Palette;
			this.sidebarScroll.BackgroundColor = palette.PanelFill;
			this.content.BackgroundColor = palette.BackgroundColor;
			foreach (TextWidget header in this.sectionHeaders)
			{
				header.TextColor = palette.TextColor;
			}

			foreach (KeyValuePair<string, ThemedTextButton> entry in this.entries)
			{
				this.ApplyTheme(entry.Value, entry.Key == this.selectedEntry);
			}

			this.recolorPage?.Invoke();
			this.Invalidate();
		}

		/// <summary>agg-gui's sidebar row: the selected one is filled with the accent and lettered white, the
		/// rest are transparent over the panel with a faint accent hover.</summary>
		private void ApplyTheme(ThemedTextButton entry, bool selected)
		{
			Color accent = DemoTheme.ColorOf(this.DemoTheme.Accent);
			entry.BackgroundColor = selected ? accent : Color.Transparent;
			entry.TextColor = selected ? Color.White : this.DemoTheme.Palette.TextColor;
			entry.HoverColor = selected ? accent : this.DemoTheme.Theme.MinimalShade;
			entry.Invalidate();
		}

		private GuiWidget SectionHeader(string text)
		{
			// HAnchor.Left so the sidebar's Padding and this Margin place it; the default Absolute ignores both.
			var header = new TextWidget(text, pointSize: 13, bold: true)
			{
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(2, 4, 0, 10),
			};
			this.sectionHeaders.Add(header);
			return header;
		}

		/// <summary>The sidebar row for one page; <paramref name="select"/> shows that page.</summary>
		private GuiWidget SidebarEntry(string text, Action select)
		{
			var entry = new ThemedTextButton(text, this.DemoTheme.Theme, 10)
			{
				Name = EntryName(text),
				HAnchor = HAnchor.Stretch,
				TextHAnchor = HAnchor.Left,
				Height = 20 * GuiWidget.DeviceScale,
				Margin = new BorderDouble(0, 1),
				Padding = new BorderDouble(5, 0),
			};
			entry.Click += (sender, e) => select();
			this.entries.Add(text, entry);
			return entry;
		}

		/// <summary>The automation name of the sidebar row for <paramref name="text"/> (an AGG demo's name or
		/// <see cref="GuiDemoName"/>).</summary>
		public static string EntryName(string text) => "App Sidebar " + text;

		/// <summary>The page for one AGG demo: its name, description, the render mode toggle, and the demo.</summary>
		private GuiWidget CreateAggDemoPage(AggDemo demo, out Action recolor)
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

			var header = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(0, 0, 0, 8),
			};
			var name = new TextWidget(demo.Name, pointSize: 14, bold: true);
			header.AddChild(name);
			var description = new TextWidget("  " + demo.Description, pointSize: 10)
			{
				VAnchor = VAnchor.Center,
			};
			header.AddChild(description);
			header.AddChild(new HorizontalSpacer());

			var softwareToggle = new CheckBox("Software (AGG reference)")
			{
				VAnchor = VAnchor.Center,
			};
			softwareToggle.CheckedStateChanged += (sender, e) =>
				view.RenderMode = softwareToggle.Checked ? AggDemoRenderMode.Software : AggDemoRenderMode.Gpu;
			header.AddChild(softwareToggle);

			page.AddChild(header);
			page.AddChild(view);

			recolor = () =>
			{
				Color textColor = this.DemoTheme.Palette.TextColor;
				name.TextColor = textColor;
				description.TextColor = textColor;
				softwareToggle.TextColor = textColor;
			};
			return page;
		}

		private void ShowAggDemoPage(AggDemo demo)
		{
			GuiWidget page = this.CreateAggDemoPage(demo, out Action recolor);
			this.ShowPage(demo.Name, page, recolor);
		}

		/// <param name="entryName">The sidebar row that shows <paramref name="page"/>; it is lit.</param>
		/// <param name="recolor">Recolours what <paramref name="page"/> copied from the theme; null for a page
		/// that follows the theme itself.</param>
		private void ShowPage(string entryName, GuiWidget page, Action recolor = null)
		{
			this.content.CloseChildren();
			this.selectedEntry = entryName;
			this.recolorPage = recolor;
			this.ApplyTheme();
			this.content.AddChild(page);
		}
	}
}
