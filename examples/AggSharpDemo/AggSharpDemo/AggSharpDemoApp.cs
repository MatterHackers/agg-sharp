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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;

namespace MatterHackers.AggSharpDemo
{
	/// <summary>
	/// The whole site as one widget: a tab strip across the top choosing between the classic AGG demos
	/// (<see cref="AggDemosPage"/>) and the agg-sharp GUI demo (<see cref="GuiDemoShell"/>), and below it the
	/// selected one. Both pages have the same shape - a menu bar, the content, and the selection bar docked on the
	/// right - so switching tabs changes what is browsed, not the app. A head only has to put this in a window.
	/// </summary>
	/// <remarks>
	/// The site is one app with one theme: the GUI demo's light/dark/system preference and accent
	/// (<see cref="DemoTheme"/>) colour the tab strip and the AGG page too, as agg-gui's theme is its whole app's.
	/// The theme outlives the pages, each of which is rebuilt every time its tab is selected (the GUI demo page
	/// saves its windows on closing and restores them when rebuilt).
	/// </remarks>
	public class AggSharpDemoApp : GuiWidget
	{
		/// <summary>Passing this as initialDemo opens on the agg-sharp Demos tab (the GUI demo page).</summary>
		public const string GuiDemoName = "GUI Demo";

		/// <summary>The saved <see cref="DemoState.AppTab"/> of the AGG Demos tab.</summary>
		public const string AggDemosTab = "agg";

		/// <summary>The saved <see cref="DemoState.AppTab"/> of the agg-sharp Demos tab.</summary>
		public const string AggSharpDemosTab = "agg-sharp";

		public const string AggDemosLabel = "AGG Demos";

		public const string AggSharpDemosLabel = "agg-sharp Demos";

		/// <summary>The tab strip's height in design units: the GUI demo's menu bar plus the band showing above the
		/// folder tabs.</summary>
		public const double TabBarHeight = 32;

		private readonly IDemoStateStore stateStore;

		/// <summary>True when this app owns <see cref="GuiWidget.DeviceScale"/> - see the constructor.</summary>
		private readonly bool followDisplayScale;

		/// <summary>Following the window's display scale (<see cref="UiScale.Follow"/>), once loaded.</summary>
		private IDisposable displayScaleFollowing;

		private GuiWidget aggDemosHost;

		private GuiWidget aggSharpDemosHost;

		/// <summary>The AGG demo to show on the AGG Demos tab; kept across tab switches and rebuilds.</summary>
		private string selectedAggDemo;

		/// <param name="initialDemo">The name of the AGG demo to open on, or <see cref="GuiDemoName"/> for the
		/// agg-sharp Demos tab; null opens where the last run left off (the saved tab and AGG demo), or on the AGG
		/// Demos tab's lion the first time. An unknown name opens on the lion.</param>
		/// <param name="guiDemoStateStore">Where the app keeps its tab, its AGG demo and the GUI demo's windows and
		/// settings between runs; null starts fresh every time.</param>
		/// <param name="followDisplayScale">
		/// True for a head: the app sets <see cref="GuiWidget.DeviceScale"/> to the display's scale (the Retina
		/// factor, or the browser's devicePixelRatio) before it builds anything, and rebuilds itself at the new
		/// scale when its window reports a different one. The platform hosts only report
		/// <see cref="SystemWindow.DisplayScale"/>; turning it into DeviceScale is the application's call
		/// (MatterCAD folds a user text size into it; <see cref="UiScale"/> is the shared policy), so without this the UI is laid out one device pixel per
		/// design unit - half size on a 2x display. False (tests) leaves the process-wide DeviceScale alone.
		/// </param>
		public AggSharpDemoApp(string initialDemo = null, IDemoStateStore guiDemoStateStore = null, bool followDisplayScale = false)
		{
			this.AnchorAll();
			this.stateStore = guiDemoStateStore;
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
			DemoState saved = guiDemoStateStore != null ? DemoState.Parse(guiDemoStateStore.Load()) : new DemoState();

			// Open in the saved theme even when the first page is the AGG one.
			DemoStatePersistence.ApplyTheme(saved, this.DemoTheme);

			// The first visit opens on the AGG Demos tab: the first thing a visitor sees should be AGG drawing.
			if (initialDemo == GuiDemoName)
			{
				this.SelectedTab = AggSharpDemosTab;
				this.selectedAggDemo = saved.AggDemo;
			}
			else if (initialDemo != null)
			{
				this.SelectedTab = AggDemosTab;
				this.selectedAggDemo = initialDemo;
			}
			else
			{
				this.SelectedTab = saved.AppTab == AggSharpDemosTab ? AggSharpDemosTab : AggDemosTab;
				this.selectedAggDemo = saved.AggDemo;
			}

			this.BuildUi();
			this.DemoTheme.ThemeChanged += this.DemoTheme_ThemeChanged;
		}

		/// <summary>The light/dark preference and accent of the whole site.</summary>
		public DemoTheme DemoTheme { get; }

		/// <summary>The tab strip and the two pages under it.</summary>
		public TabView Tabs { get; private set; }

		/// <summary><see cref="AggDemosTab"/> or <see cref="AggSharpDemosTab"/>.</summary>
		public string SelectedTab { get; private set; }

		/// <summary>The AGG Demos page while its tab is selected, else null.</summary>
		public AggDemosPage AggDemosPage => this.aggDemosHost?.Children.OfType<AggDemosPage>().FirstOrDefault();

		/// <summary>The GUI demo page while the agg-sharp Demos tab is selected, else null.</summary>
		public GuiDemoShell GuiDemoShell => this.aggSharpDemosHost?.Children.OfType<GuiDemoShell>().FirstOrDefault();

		/// <summary>The automation name of the tab labelled <paramref name="label"/>.</summary>
		public static string TabName(string label) => "App Tab " + label;

		/// <summary>Selects the tab <paramref name="tab"/> (<see cref="AggDemosTab"/> or <see cref="AggSharpDemosTab"/>),
		/// as clicking it does.</summary>
		public void SelectTab(string tab) => this.Tabs.SelectedIndex = tab == AggSharpDemosTab ? 1 : 0;

		/// <summary>Builds the tab strip and opens <see cref="SelectedTab"/>, all at the current
		/// <see cref="GuiWidget.DeviceScale"/>.</summary>
		private void BuildUi()
		{
			this.Tabs = new TabView(TabBarHeight)
			{
				Name = "App Tabs",
			};

			this.aggDemosHost = new GuiWidget() { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
			this.aggSharpDemosHost = new GuiWidget() { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
			this.Tabs.AddTab(AggDemosLabel, this.aggDemosHost, TabName(AggDemosLabel));
			this.Tabs.AddTab(AggSharpDemosLabel, this.aggSharpDemosHost, TabName(AggSharpDemosLabel));
			this.AddChild(this.Tabs);
			this.ApplyTheme();

			this.Tabs.SelectedIndexChanged += (s, e) => this.ShowSelectedTab();

			// AddTab already selected the first tab, so selecting it again raises nothing; its page is opened here.
			int index = this.SelectedTab == AggSharpDemosTab ? 1 : 0;
			if (this.Tabs.SelectedIndex == index)
			{
				this.ShowSelectedTab();
			}
			else
			{
				this.Tabs.SelectedIndex = index;
			}
		}

		/// <summary>Closes the page of the tab left (the GUI demo page saves its state as it closes) and builds the
		/// selected tab's, then remembers the choice.</summary>
		private void ShowSelectedTab()
		{
			bool aggSelected = this.Tabs.SelectedIndex == 0;
			GuiWidget host = aggSelected ? this.aggDemosHost : this.aggSharpDemosHost;
			GuiWidget other = aggSelected ? this.aggSharpDemosHost : this.aggDemosHost;
			other.CloseChildren();

			if (host.Children.Count == 0)
			{
				if (aggSelected)
				{
					var page = new AggDemosPage(this.DemoTheme, this.selectedAggDemo);
					this.selectedAggDemo = page.SelectedDemo.Name;
					page.SelectedDemoChanged += (s, e) =>
					{
						this.selectedAggDemo = page.SelectedDemo.Name;
						this.SaveAppState();
					};
					host.AddChild(page);
				}
				else
				{
					host.AddChild(new GuiDemoShell(this.DemoTheme, this.stateStore));
				}
			}

			this.SelectedTab = aggSelected ? AggDemosTab : AggSharpDemosTab;
			this.SaveAppState();
		}

		/// <summary>Writes the tab and AGG demo into the shared state, leaving the GUI demo's part as it is.</summary>
		private void SaveAppState()
		{
			if (this.stateStore == null)
			{
				return;
			}

			DemoState state = DemoState.Parse(this.stateStore.Load());
			if (state.AppTab != this.SelectedTab || state.AggDemo != this.selectedAggDemo)
			{
				state.AppTab = this.SelectedTab;
				state.AggDemo = this.selectedAggDemo;
				this.stateStore.Save(state.Serialize());
			}
		}

		/// <summary>The pages are built before their window is shown, which is when the platform installs the
		/// appearance provider, so System is asked again once the app is on screen.</summary>
		public override void OnLoad(EventArgs args)
		{
			this.DemoTheme.RefreshSystemPreference();
			if (this.followDisplayScale && this.displayScaleFollowing == null
				&& this.Parents<SystemWindow>().FirstOrDefault() is SystemWindow window)
			{
				// The window moved to a display with another scale (or the browser zoomed): rebuild everything
				// at it. Closing the GUI demo page saves its state to the store in design units, so it closes
				// at the old scale; the new page restores it, and the selected tab and AGG demo are kept, so a
				// visitor sees the same page and windows, sized for the new display.
				this.displayScaleFollowing = UiScale.Follow(
					window,
					rebuild: this.BuildUi,
					beforeRescale: this.CloseChildren);
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

		/// <summary>Both pages start with a menu bar in the top bar's fill, so the selected tab takes that fill and joins
		/// it; the band behind the tabs is a step darker. The pages recolour themselves.</summary>
		private void ApplyTheme()
		{
			DemoPalette palette = this.DemoTheme.Palette;
			this.Tabs.PageColor = palette.TopBarBackground;
			// The dark canvas is a step under the dark top bar; the light palette has nothing darker than its top bar
			// (the canvas is lighter), so the light band is its top bar taken down a step.
			this.Tabs.BarColor = palette.IsDark ? palette.BackgroundColor : DemoPalette.Rgb(0.82, 0.82, 0.86);
			this.Tabs.SeparatorColor = palette.Separator;
			this.Tabs.TextColor = palette.TextColor;
			this.Tabs.TextDimColor = palette.TextDim;
			this.Tabs.HoverColor = this.DemoTheme.Theme.MinimalShade;
			this.Tabs.Invalidate();
		}
	}
}
