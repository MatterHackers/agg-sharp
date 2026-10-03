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
using System.Diagnostics;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// The GUI Demo page, laid out as agg-gui's app_builder.rs does: a menu bar across the top, and below it the
	/// canvas the demo windows float over with the window list docked on the right.
	/// </summary>
	public class GuiDemoShell : FlowLayoutWidget
	{
		// The sizes below are design units, as agg-gui's are logical pixels: each is multiplied by
		// GuiWidget.DeviceScale where it becomes a Width or Height, so at 2x the frame grows with its text.

		/// <summary>agg-gui's menu BAR_H (menu/geometry.rs); its MenuBarStrip takes the bar's natural height.</summary>
		public const double TopBarHeight = 26;

		/// <summary>agg-gui's SidebarPane PANEL_W (shell.rs).</summary>
		public const double SidebarWidth = 220;

		/// <summary>shell.rs's MOBILE_BREAKPOINT: narrower than this, the sidebar is a drawer, hidden until the top
		/// bar's drawer button opens it.</summary>
		public const double MobileBreakpoint = 720;

		/// <summary>shell.rs's MOBILE_PANEL_W: the open drawer's width (never wider than the page).</summary>
		public const double MobileSidebarWidth = 300;

		// The GC count and allocated bytes when the previous draw started (-1: no draw yet), so each frame's
		// numbers cover the whole interval, including event handling and layout between draws.
		private int previousFrameGen0 = -1;

		private long previousFrameAllocated;

		/// <param name="demoTheme">The theme to show; null starts agg-gui's default (System, Blue), with System
		/// following <see cref="SystemAppearance"/> and falling back to dark, as agg-gui does, when the
		/// platform cannot tell.</param>
		/// <param name="stateStore">Where the page's layout, theme and settings are kept between runs; the saved
		/// state is applied here and every later change is saved back. Null remembers nothing.</param>
		/// <param name="clockMs">The time the windows fade in and out on, in milliseconds; null for
		/// <see cref="UiThread.CurrentTimerMs"/>. Tests pass their own to step a fade rather than wait for it.</param>
		public GuiDemoShell(DemoTheme demoTheme = null, IDemoStateStore stateStore = null, Func<long> clockMs = null)
			: base(FlowDirection.TopToBottom)
		{
			this.Name = "GuiDemo Shell";
			this.AnchorAll();
			this.DemoTheme = demoTheme ?? new DemoTheme(systemPrefersDark: () => SystemAppearance.PrefersDark ?? true);

			// The Mobile Keyboard window's on-screen keyboard. It does nothing until that window picks a mobile
			// input profile; the profile starts at Desktop.
			this.SoftwareKeyboard = new SoftwareKeyboardController();

			this.TopBar = new DemoTopBar(this.DemoTheme);
			this.AddChild(this.TopBar);

			var body = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.AddChild(body);

			this.Canvas = new GuiWidget()
			{
				Name = "GuiDemo Canvas",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			body.AddChild(this.Canvas);
			this.Windows = new DemoWindowHost(this.Canvas, this.DemoTheme, clockMs);

			// Demos > group > title toggles the window, as agg-gui's menu flips the same open cell the
			// sidebar row does.
			this.TopBar.DemoRequested += spec => this.Windows.SetOpen(spec, !this.Windows.IsOpen(spec));

			// View > Window Snapping flips agg-gui's snap flag
			this.Windows.Snap.Enabled = this.TopBar.SnapEnabled;
			this.TopBar.SnapToggled += (s, e) => this.Windows.Snap.Enabled = this.TopBar.SnapEnabled;

			this.Sidebar = new DemoSidebar(this.Windows, this.DemoTheme);
			body.AddChild(this.Sidebar);
			this.TopBar.SidebarDrawerToggled += (s, e) => this.UpdateSidebarLayout();

			// agg-gui's BackendPane: left of the canvas, shown while View > Backend Panel is checked.
			this.BackendPanel = new BackendPanel(this.Windows, this.DemoTheme)
			{
				Visible = this.TopBar.BackendPanelOpen,
			};
			body.AddChild(this.BackendPanel, 0);
			this.TopBar.BackendPanelToggled += (s, e) => this.BackendPanel.Visible = this.TopBar.BackendPanelOpen;
			this.BackendPanel.RunModeChanged += (s, e) => this.Invalidate();
			this.BackendPanel.ResetAllRequested += (s, e) => this.ResetAllState();

			// The backend panel's Inspector pill and the sidebar's Inspector row are one open state, as both
			// flip agg-gui's show_inspector cell. Each ignores an unchanged value, so the hookup does not loop.
			this.BackendPanel.InspectorToggled += (s, e) => this.Windows.SetOpen(GuiDemoSpecs.Inspector, this.BackendPanel.InspectorEnabled);
			this.Windows.OpenChanged += (s, spec) =>
			{
				if (spec == GuiDemoSpecs.Inspector)
				{
					this.BackendPanel.SetInspectorEnabled(this.Windows.IsOpen(spec));
				}
			};

			this.ApplyTheme();
			this.DemoTheme.ThemeChanged += this.DemoTheme_ThemeChanged;

			if (stateStore != null)
			{
				this.Persistence = new DemoStatePersistence(this, stateStore);
				this.Persistence.Restore();
				this.Persistence.Start();
			}
		}

		/// <summary>Saves and restores the page's state; null when the page was built without a store.</summary>
		public DemoStatePersistence Persistence { get; }

		/// <summary>The light/dark preference and accent the page is drawn with.</summary>
		public DemoTheme DemoTheme { get; }

		/// <summary>The menu bar across the top.</summary>
		public DemoTopBar TopBar { get; }

		public SoftwareKeyboardController SoftwareKeyboard { get; }

		/// <summary>The area the demo windows float over.</summary>
		public GuiWidget Canvas { get; }

		/// <summary>The demo windows floating over <see cref="Canvas"/>: which are open, and their order.</summary>
		public DemoWindowHost Windows { get; }

		/// <summary>The window list docked on the right.</summary>
		public DemoSidebar Sidebar { get; }

		/// <summary>The panel on the left (View > Backend Panel); hidden while the menu item is unchecked.</summary>
		public BackendPanel BackendPanel { get; }

		/// <summary>The 3D Animation window's content once it has been opened (the host keeps it across close and
		/// reopen), else null.</summary>
		public Windows.Graphics.ThreeDAnimationWindow ThreeDAnimation
		{
			get
			{
				var spec = System.Linq.Enumerable.First(GuiDemoSpecs.All, s => s.Title == "3D Animation");
				return this.Windows.GetWindow(spec)?.FindDescendant(spec.ContentName) as Windows.Graphics.ThreeDAnimationWindow;
			}
		}

		/// <summary>Whether a frame just drawn asks for the next one straight away: the backend panel's Continuous
		/// run mode. Reactive leaves it to input and animations, as agg-gui's RunMode does.</summary>
		public bool RedrawsContinuously => this.BackendPanel.RunMode == DemoRunMode.Continuous;

		/// <summary>
		/// "Reset all state": every window back to its default (open-by-default ones open and tiled, the rest
		/// closed), agg-gui's default theme, snapping on, the backend panel's settings and the 3D Animation
		/// window's SSAA back to theirs, and the saved state forgotten. The backend panel stays open, since that is
		/// where the button was pressed.
		/// </summary>
		public void ResetAllState()
		{
			this.BackendPanel.ResetSettings();

			if (this.Persistence != null)
			{
				this.Persistence.ResetAll();
			}
			else
			{
				DemoStatePersistence.ApplyDefaults(this);
			}
		}

		/// <summary>Times the page's draw into the backend panel's history (agg-gui's "Mean CPU usage": the CPU
		/// side of the frame; the GPU works on after it) and, in Continuous mode, asks for the next frame.
		/// Each frame also records the gen0 collections and bytes allocated since the previous draw started, so the
		/// panel can tell garbage collection pauses from other spikes.</summary>
		public override void OnDraw(Graphics2D graphics2D)
		{
			int gen0AtStart = GC.CollectionCount(0);
			long allocatedAtStart = GC.GetAllocatedBytesForCurrentThread();
			long start = Stopwatch.GetTimestamp();
			base.OnDraw(graphics2D);
			double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
			int gen0AtEnd = GC.CollectionCount(0);
			long allocatedAtEnd = GC.GetAllocatedBytesForCurrentThread();

			// The first frame has no previous start, so its interval is just its own draw.
			bool hasPrevious = this.previousFrameGen0 >= 0;
			int gen0Since = gen0AtEnd - (hasPrevious ? this.previousFrameGen0 : gen0AtStart);
			long allocatedSince = allocatedAtEnd - (hasPrevious ? this.previousFrameAllocated : allocatedAtStart);
			this.previousFrameGen0 = gen0AtStart;
			this.previousFrameAllocated = allocatedAtStart;
			this.BackendPanel.History.Push(new FrameSample(ms, gen0Since, gen0AtEnd != gen0AtStart, allocatedSince));

			if (this.RedrawsContinuously)
			{
				this.Invalidate();
			}
		}

		/// <summary>The page is built before its window is shown, which is when the platform installs the
		/// appearance provider, so System is asked again once the page is actually on screen.</summary>
		/// <summary>Whether the page is narrower than <see cref="MobileBreakpoint"/>, so the sidebar is a drawer.</summary>
		public bool IsNarrow => this.Width < MobileBreakpoint * DeviceScale;

		public override void OnBoundsChanged(EventArgs e)
		{
			base.OnBoundsChanged(e);
			this.UpdateSidebarLayout();
		}

		/// <summary>shell.rs's SidebarPane layout: docked at <see cref="SidebarWidth"/> on a wide page; on a narrow
		/// one hidden, or <see cref="MobileSidebarWidth"/> wide while the drawer is open. The drawer button only
		/// shows on a narrow page.</summary>
		private void UpdateSidebarLayout()
		{
			bool narrow = this.IsNarrow;
			this.TopBar.SidebarDrawerButtonVisible = narrow;
			bool visible = !narrow || this.TopBar.SidebarDrawerOpen;
			double width = narrow ? Math.Min(MobileSidebarWidth * DeviceScale, this.Width) : SidebarWidth * DeviceScale;

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

		public override void OnLoad(EventArgs args)
		{
			this.DemoTheme.RefreshSystemPreference();
			this.HookShortcuts();
			base.OnLoad(args);
		}

		/// <summary>A Ctrl+Shift+D draw report was written (<see cref="DemoShortcuts"/>); the text is the report.</summary>
		public event EventHandler<string> DrawReported;

		internal void OnDrawReport(string report) => this.DrawReported?.Invoke(this, report);

		/// <summary>The widget whose key events <see cref="DemoShortcuts"/> is listening to.</summary>
		private GuiWidget shortcutRoot;

		/// <summary>
		/// Listens for <see cref="DemoShortcuts"/> at the top of the widget tree rather than on the page: a key only
		/// travels down the focus chain, so the page would not hear it while nothing on it has focus, and agg-gui's
		/// shortcuts work whatever has focus. A key a focused widget took for itself is left alone.
		/// </summary>
		internal void HookShortcuts()
		{
			GuiWidget root = this;
			while (root.Parent != null)
			{
				root = root.Parent;
			}

			if (root != this.shortcutRoot)
			{
				this.UnhookShortcuts();
				this.shortcutRoot = root;
				root.KeyDown += this.Root_KeyDown;
			}
		}

		private void UnhookShortcuts()
		{
			if (this.shortcutRoot != null)
			{
				this.shortcutRoot.KeyDown -= this.Root_KeyDown;
				this.shortcutRoot = null;
			}
		}

		private void Root_KeyDown(object sender, KeyEventArgs keyEvent)
		{
			if (!keyEvent.Handled
				&& DemoShortcuts.Handle(this, keyEvent))
			{
				keyEvent.Handled = true;
				keyEvent.SuppressKeyPress = true;
			}
		}

		/// <summary>Saves while the windows are still on the canvas: closing takes them off.</summary>
		public override void OnClosing2(EventArgs eventArgs)
		{
			this.Persistence?.Stop();
			base.OnClosing2(eventArgs);
		}

		public override void OnClosed(EventArgs e)
		{
			this.DemoTheme.ThemeChanged -= this.DemoTheme_ThemeChanged;
			this.UnhookShortcuts();
			this.SoftwareKeyboard.Dispose();
			base.OnClosed(e);
		}

		private void DemoTheme_ThemeChanged(object sender, EventArgs e) => this.ApplyTheme();

		/// <summary>The canvas takes agg-gui's bg_color and the sidebar its panel_fill; the tone difference
		/// separates them without drawn lines (a Border would sit outside the widget and take a pixel from the
		/// canvas). The top bar, the sidebar and the windows recolour themselves.</summary>
		private void ApplyTheme()
		{
			DemoPalette palette = this.DemoTheme.Palette;
			this.Canvas.BackgroundColor = palette.BackgroundColor;
			this.Invalidate();
		}
	}
}
