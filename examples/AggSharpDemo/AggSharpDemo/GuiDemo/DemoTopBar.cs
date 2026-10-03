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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// The GUI demo's menu bar, after agg-gui's demo-ui/src/top_bar.rs: Demos (one submenu per sidebar group),
	/// View (Backend Panel, Window Snapping, Theme, Color) and Help (View on GitHub).
	/// </summary>
	/// <remarks>
	/// Every item's automation name is agg-gui's action id (view.theme.dark, help.github, ...) - demos use
	/// demo.&lt;title&gt; rather than agg-gui's positional demo.&lt;index&gt; so the name survives list edits.
	/// Theme and accent are applied to the <see cref="DemoTheme"/>; the rest only flip a flag and raise an
	/// event, since the windows, the backend panel and snapping are wired up by later steps.
	/// Icons are top_bar.rs's Font Awesome codepoints (<see cref="IconFont"/>); the Color items carry a swatch
	/// of their accent instead. Check and radio items keep the menu open, as agg-gui's keep_open() rows do
	/// (<see cref="MenuItemModel.CloseMenuOnPick"/> stays false).
	/// </remarks>
	public class DemoTopBar : GuiWidget
	{
		/// <summary>Where Help > View on GitHub goes.</summary>
		public const string GitHubUrl = "https://github.com/larsbrubaker/agg-sharp";

		private readonly DemoTheme demoTheme;

		public DemoTopBar(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.Name = "GuiDemo Top Bar";
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;
			this.Height = GuiDemoShell.TopBarHeight * DeviceScale;

			this.Menus = this.CreateMenus();
			this.MenuBar = new MenuBarWidget(this.Menus, demoTheme.Theme)
			{
				Name = "GuiDemo Menu Bar",
				VAnchor = VAnchor.Center,
			};
			this.AddChild(this.MenuBar);
			SizeTitles(this.MenuBar, this.Menus);

			// The narrow page's sidebar drawer toggle (FA bars), at the bar's right end; the shell shows it
			// below its breakpoint.
			this.SidebarDrawerButton = new IconTextButton("", "\uF0C9", demoTheme.Theme)
			{
				Name = "GuiDemo Sidebar Toggle",
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

		/// <summary>Raised by Demos > group > title: agg-gui opens that window.</summary>
		public event Action<DemoSpec> DemoRequested;

		/// <summary>Raised after View > Backend Panel flipped <see cref="BackendPanelOpen"/>.</summary>
		public event EventHandler BackendPanelToggled;

		/// <summary>Raised after the drawer button flipped <see cref="SidebarDrawerOpen"/>.</summary>
		public event EventHandler SidebarDrawerToggled;

		/// <summary>Raised after View > Window Snapping flipped <see cref="SnapEnabled"/>.</summary>
		public event EventHandler SnapToggled;

		/// <summary>Raised by Help > View on GitHub with <see cref="GitHubUrl"/>. A host that listens decides how
		/// to show it; with no listener the url goes to <see cref="UrlLauncher"/> (the user's browser).</summary>
		public event Action<string> OpenUrlRequested;

		/// <summary>The menu model the bar is built from (top level: Demos, View, Help).</summary>
		public IReadOnlyList<MenuItemModel> Menus { get; }

		public MenuBarWidget MenuBar { get; }

		/// <summary>Opens and closes the sidebar drawer on a narrow page (shell.rs's mobile_menu_open).</summary>
		public IconTextButton SidebarDrawerButton { get; }

		/// <summary>Whether the narrow page's sidebar drawer is open. Ignored on a wide page, where the sidebar is
		/// always docked.</summary>
		public bool SidebarDrawerOpen { get; private set; }

		/// <summary>Whether <see cref="SidebarDrawerButton"/> shows; the shell sets it from its width.</summary>
		public bool SidebarDrawerButtonVisible
		{
			get => this.SidebarDrawerButton.Visible;
			set
			{
				if (value != this.SidebarDrawerButton.Visible)
				{
					this.SidebarDrawerButton.Visible = value;
				}
			}
		}

		public bool BackendPanelOpen { get; private set; }

		/// <summary>agg-gui starts with snapping on.</summary>
		public bool SnapEnabled { get; private set; } = true;

		/// <summary>Sets <see cref="BackendPanelOpen"/> (as the menu item does, or restored state), raising
		/// <see cref="BackendPanelToggled"/> if it changed.</summary>
		public void SetBackendPanelOpen(bool open)
		{
			if (open != this.BackendPanelOpen)
			{
				this.BackendPanelOpen = open;
				this.BackendPanelToggled?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>Sets <see cref="SidebarDrawerOpen"/>, raising <see cref="SidebarDrawerToggled"/> if it changed.</summary>
		public void SetSidebarDrawerOpen(bool open)
		{
			if (open != this.SidebarDrawerOpen)
			{
				this.SidebarDrawerOpen = open;
				this.SidebarDrawerToggled?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>Sets <see cref="SnapEnabled"/>, raising <see cref="SnapToggled"/> if it changed.</summary>
		public void SetSnapEnabled(bool enabled)
		{
			if (enabled != this.SnapEnabled)
			{
				this.SnapEnabled = enabled;
				this.SnapToggled?.Invoke(this, EventArgs.Empty);
			}
		}

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the page (a new shell is built each time the page is shown)
			this.demoTheme.ThemeChanged -= this.DemoTheme_ThemeChanged;
			base.OnClosed(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
			DrawBottomHairline(this, graphics2D, this.demoTheme.Palette.Separator);
		}

		/// <summary>One device pixel of <paramref name="color"/> along <paramref name="bar"/>'s bottom, on a whole
		/// pixel row (the bar's origin can be fractional at a fractional DeviceScale), as the app's TabView draws
		/// its strip's separator: the menu row is set off from the content under it in the tabs' own line colour.</summary>
		internal static void DrawBottomHairline(GuiWidget bar, Graphics2D graphics2D, Color color)
		{
			RectangleDouble bounds = bar.LocalBounds;
			double ty = graphics2D.GetTransform().ty;
			double bottom = Math.Ceiling(bounds.Bottom + ty - 1e-6) - ty;
			graphics2D.FillRectangle(bounds.Left, bottom, bounds.Right, bottom + 1, color);
		}

		private void DemoTheme_ThemeChanged(object sender, EventArgs e) => this.ApplyTheme();

		/// <summary>The bar's titles copied the text colour when they were built, so they are recoloured here;
		/// the popups read the theme each time they open.</summary>
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

		/// <summary>top_bar.rs's MenuBar is 13px, and each title a fixed slot of max(chars * 8 + 22, 52) scaled by
		/// 13 / 14 (menu/widget/mod.rs), its text 9px in from the slot's left; the library's titles fit their text
		/// in the theme's button padding, so they are resized here. agg-gui counts the icon prefix ("\u{F009} ") as
		/// two characters. The AGG demos page's bar (<see cref="AggDemoTopBar"/>) is sized the same way.</summary>
		internal static void SizeTitles(MenuBarWidget menuBar, IReadOnlyList<MenuItemModel> menus)
		{
			const double FontPixels = 13;
			const double TextInset = 9;
			foreach ((ThemedTextButton title, MenuItemModel menu) in menuBar.Children.OfType<ThemedTextButton>().Zip(menus))
			{
				bool hasIcon = !string.IsNullOrEmpty(menu.IconGlyph);
				int characters = menu.Text.Length + (hasIcon ? 2 : 0);
				double slot = Math.Max(characters * 8.0 + 22, 52) * FontPixels / 14;
				title.Children.OfType<TextWidget>().Single().PointSize = DemoText.Points(FontPixels);
				title.HAnchor = HAnchor.Absolute;
				title.Width = slot * DeviceScale;
				title.TextHAnchor = HAnchor.Left;

				// The slots abut; ThemedButton's default 3px side margins would add 6px between titles
				title.Margin = new BorderDouble(0);

				// MenuBarTitle paints its icon in the left padding, IconSize + IconGap (20) wide
				title.Padding = new BorderDouble(TextInset + (hasIcon ? 20 : 0), 0, 0, 0);
			}
		}

		private IReadOnlyList<MenuItemModel> CreateMenus()
		{
			return new[]
			{
				// top_bar.rs titles it "\u{F009} Demos"; the bar draws the glyph ahead of the text.
				WithIcon(
					new MenuItemModel
					{
						Text = "Demos",
						SubMenuItems = this.DemosItems,
					},
					DemosGroupIcon),
				new MenuItemModel
				{
					Text = "View",
					SubMenuItems = this.ViewItems,
				},
				new MenuItemModel
				{
					Text = "Help",
					SubMenuItems = () => new[]
					{
						WithIcon(Item("View on GitHub", "help.github", () => this.OpenUrl(GitHubUrl)), GitHubIcon),
					},
				},
			};
		}

		private IReadOnlyList<MenuItemModel> DemosItems()
		{
			// The sidebar's groups, rows and order (app_builder.rs mirrors its sidebar_groups into this menu), so the
			// Inspector is listed under Tools here too.
			return GuiDemoSpecs.Groups.Select(group => WithIcon(
				new MenuItemModel
				{
					Text = group,
					AutomationName = "demos." + group,
					SubMenuItems = () => SidebarFilter.EntriesOf(group)
						.Select(spec => WithIcon(Item(spec.Title, "demo." + spec.Title, () => this.DemoRequested?.Invoke(spec)), spec.Icon))
						.ToList(),
				},
				DemosGroupIcon)).ToList();
		}

		private IReadOnlyList<MenuItemModel> ViewItems()
		{
			return new[]
			{
				Check(
					WithIcon(Item("Backend Panel", "view.backend", () => this.SetBackendPanelOpen(!this.BackendPanelOpen)), BackendIcon),
					() => this.BackendPanelOpen),
				Check(
					WithIcon(Item("Window Snapping", "view.snap", () => this.SetSnapEnabled(!this.SnapEnabled)), SnapIcon),
					() => this.SnapEnabled),
				new MenuItemModel { IsSeparator = true },
				WithIcon(
					new MenuItemModel
					{
						Text = "Theme",
						AutomationName = "view.theme",
						SubMenuItems = () => ((ThemePreference[])Enum.GetValues(typeof(ThemePreference)))
							.Select(preference => Radio(
								WithIcon(
									Item(
										preference.ToString(),
										"view.theme." + DemoTheme.KeyOf(preference),
										() => this.demoTheme.SetPreference(preference)),
									ThemeIconOf(preference)),
								() => this.demoTheme.Preference == preference))
							.ToList(),
					},
					ThemeIcon),
				WithIcon(
					new MenuItemModel
					{
						Text = "Color",
						AutomationName = "view.accent",
						SubMenuItems = () => ((AccentColor[])Enum.GetValues(typeof(AccentColor)))
							.Select(accent =>
							{
								MenuItemModel item = Item(
									accent.ToString(),
									"view.accent." + DemoTheme.KeyOf(accent),
									() => this.demoTheme.SetAccent(accent));
								item.Icon = Swatch(DemoTheme.ColorOf(accent));
								return Radio(item, () => this.demoTheme.Accent == accent);
							})
							.ToList(),
					},
					ColorIcon),
			};
		}

		// top_bar.rs's icons (Font Awesome codepoints), drawn from agg's Font Awesome 6 Free Solid. GitHub's mark
		// (U+F09B) is a Brands glyph, not in the Solid font, so its row shows the external-link arrow instead.
		internal const string DemosGroupIcon = "\uF009";
		internal const string BackendIcon = "\uF109";
		internal const string SnapIcon = "\uF076";
		internal const string ThemeIcon = "\uF042";
		internal const string ColorIcon = "\uF53F";
		internal const string GitHubIcon = "\uF08E";

		/// <summary>top_bar.rs's Theme submenu icons: sun, moon, desktop.</summary>
		internal static string ThemeIconOf(ThemePreference preference)
		{
			return preference switch
			{
				ThemePreference.Light => "\uF185",
				ThemePreference.Dark => "\uF186",
				_ => "\uF108",
			};
		}

		/// <summary>Gives <paramref name="item"/> the icon font glyph <paramref name="glyph"/>; null or empty leaves
		/// it without an icon (the Window Resize Test entries have none).</summary>
		internal static MenuItemModel WithIcon(MenuItemModel item, string glyph)
		{
			if (string.IsNullOrEmpty(glyph))
			{
				return item;
			}

			item.IconGlyph = glyph;
			item.IconTypeFace = IconFont.TypeFace;
			return item;
		}

		/// <summary>agg-gui's MenuItem::swatch: a rounded square of the accent where the icon goes, the size of
		/// the menu's glyph icons.</summary>
		private static ImageBuffer Swatch(Color color)
		{
			int size = (int)Math.Round(16 * GuiWidget.DeviceScale);
			var image = new ImageBuffer(size, size);
			double inset = 2 * GuiWidget.DeviceScale;
			image.NewGraphics2D().Render(
				new RoundedRect(inset, inset, size - inset, size - inset, 3 * GuiWidget.DeviceScale),
				color);
			return image;
		}

		internal static MenuItemModel Item(string text, string id, Action action)
		{
			return new MenuItemModel { Text = text, AutomationName = id, Action = action };
		}

		/// <summary>agg-gui's check items: the mark follows <paramref name="isChecked"/> each time the menu opens.</summary>
		private static MenuItemModel Check(MenuItemModel item, Func<bool> isChecked)
		{
			item.IsChecked = isChecked;
			return item;
		}

		/// <summary>agg-gui's radio items: one of the adjacent radio items is on.</summary>
		private static MenuItemModel Radio(MenuItemModel item, Func<bool> isChecked)
		{
			item.IsRadio = true;
			return Check(item, isChecked);
		}

		private void OpenUrl(string url)
		{
			if (this.OpenUrlRequested != null)
			{
				this.OpenUrlRequested(url);
			}
			else
			{
				UrlLauncher.Open(url);
			}
		}
	}
}
