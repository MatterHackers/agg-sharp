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

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// Carries the GUI demo page's <see cref="DemoState"/> across runs, as agg-gui's demo does with state.rs and
	/// persistence.rs: <see cref="Restore"/> applies the saved state to a freshly built page, and every change
	/// after <see cref="Start"/> schedules one save a short while later, so a window dragged across the canvas
	/// writes once rather than once per mouse move. <see cref="Stop"/> (the page closing) writes straight away.
	/// </summary>
	public class DemoStatePersistence
	{
		/// <summary>How long after a change the save happens; later changes ride on the pending save.</summary>
		public const double SaveDelaySeconds = 0.5;

		private readonly GuiDemoShell shell;

		private readonly Action<Action, double> scheduleDelayed;

		private bool started;

		/// <summary>The inspector state last restored, kept for when the Inspector window is first built.</summary>
		private InspectorSavedState inspectorState;

		/// <summary>The Inspector panel whose state this follows, once its window has been built.</summary>
		private InspectorPanel inspector;

		/// <summary>The System window's tab last restored, for when that window is first built.</summary>
		private int systemTab;

		/// <summary>The System window whose settings and tab this follows, once it has been built.</summary>
		private Windows.Tools.SystemTypographyWindow systemWindow;

		/// <param name="scheduleDelayed">Runs an action on the UI thread after a delay in seconds; null for
		/// <see cref="UiThread.RunOnIdle(Action, double)"/>. Tests pass their own to run the save when they choose.</param>
		public DemoStatePersistence(GuiDemoShell shell, IDemoStateStore store, Action<Action, double> scheduleDelayed = null)
		{
			this.shell = shell;
			this.Store = store;
			this.scheduleDelayed = scheduleDelayed ?? UiThread.RunOnIdle;
		}

		public IDemoStateStore Store { get; }

		/// <summary>A change is waiting for its delayed save.</summary>
		public bool SavePending { get; private set; }

		/// <summary>Every window the page can show: the specs.rs list, About and the Inspector.</summary>
		private static IEnumerable<DemoSpec> AllSpecs => GuiDemoSpecs.All.Append(GuiDemoSpecs.About).Append(GuiDemoSpecs.Inspector);

		/// <summary>The page's current state.</summary>
		public DemoState Capture()
		{
			DemoWindowHost windows = this.shell.Windows;
			var state = new DemoState
			{
				Theme = this.shell.DemoTheme.Preference.ToString(),
				Accent = this.shell.DemoTheme.Accent.ToString(),
				SnapEnabled = this.shell.TopBar.SnapEnabled,
				BackendPanelOpen = this.shell.TopBar.BackendPanelOpen,
				ZOrder = windows.ZOrder.Select(spec => spec.Title).ToList(),
				Inspector = this.AttachInspector()?.SavedState ?? this.inspectorState,
				SystemSettings = SystemSettingsState.Capture(this.AttachSystem()?.Tabs.SelectedIndex ?? this.systemTab),
			};

			// The desktop window the page is shown in (none in a test, or before it is shown).
			if (this.shell.Parents<SystemWindow>().FirstOrDefault() is SystemWindow osWindow
				&& osWindow.Width > 0
				&& osWindow.Height > 0)
			{
				state.OsWindowWidth = osWindow.Width / GuiWidget.DeviceScale;
				state.OsWindowHeight = osWindow.Height / GuiWidget.DeviceScale;
			}

			foreach (DemoSpec spec in AllSpecs)
			{
				RectangleDouble? rect = windows.GetVisibleRect(spec);
				bool open = windows.IsOpen(spec);

				// Windows never opened keep their defaults; saving them would pin today's tiling.
				if (rect == null && !open)
				{
					continue;
				}

				var windowState = new DemoWindowState { Title = spec.Title, Open = open, Maximized = open && windows.IsMaximized(spec) };
				if (rect is RectangleDouble r)
				{
					// Design units; see DemoWindowState.
					double scale = GuiWidget.DeviceScale;
					windowState.X = r.Left / scale;
					windowState.Y = r.Bottom / scale;
					windowState.Width = r.Width / scale;
					windowState.Height = r.Height / scale;
				}

				state.Windows.Add(windowState);
			}

			return state;
		}

		/// <summary>Applies the store's saved state (defaults if there is none or it cannot be read).</summary>
		public void Restore() => this.Apply(DemoState.Parse(this.Store.Load()));

		/// <summary>
		/// Applies only the saved theme and accent of <paramref name="state"/> to <paramref name="demoTheme"/>;
		/// values the demo does not know are ignored. The app uses this to open in the saved theme before the GUI
		/// demo page (which restores the rest) has been built.
		/// </summary>
		public static void ApplyTheme(DemoState state, DemoTheme demoTheme)
		{
			if (Enum.TryParse(state.Theme, ignoreCase: true, out ThemePreference preference)
				&& Enum.IsDefined(preference))
			{
				demoTheme.SetPreference(preference);
			}

			if (Enum.TryParse(state.Accent, ignoreCase: true, out AccentColor accent)
				&& Enum.IsDefined(accent))
			{
				demoTheme.SetAccent(accent);
			}
		}

		/// <summary>
		/// Applies <paramref name="state"/> to the page: theme, accent, snapping, backend panel, then each listed
		/// window's rectangle and open flag, then the stacking order. Titles the demo does not have are ignored.
		/// </summary>
		public void Apply(DemoState state)
		{
			ApplyTheme(state, this.shell.DemoTheme);

			this.shell.TopBar.SetSnapEnabled(state.SnapEnabled);
			this.shell.TopBar.SetBackendPanelOpen(state.BackendPanelOpen);

			this.inspectorState = state.Inspector;
			this.inspector?.ApplySavedState(state.Inspector);

			state.SystemSettings?.Apply();
			this.systemTab = state.SystemSettings?.Tab ?? 0;
			this.ApplySystemTab();

			Dictionary<string, DemoSpec> byTitle = AllSpecs.ToDictionary(spec => spec.Title);
			DemoWindowHost windows = this.shell.Windows;
			foreach (DemoWindowState windowState in state.Windows)
			{
				if (!byTitle.TryGetValue(windowState.Title, out DemoSpec spec))
				{
					continue;
				}

				// The rectangle first, so a window built by opening it is placed there rather than on its tile.
				if (windowState.HasBounds)
				{
					double scale = GuiWidget.DeviceScale;
					windows.RestoreRect(spec, new RectangleDouble(
						windowState.X * scale,
						windowState.Y * scale,
						(windowState.X + windowState.Width) * scale,
						(windowState.Y + windowState.Height) * scale));
				}

				windows.SetOpen(spec, windowState.Open);

				// Only an open window comes back maximized; agg-gui restores a window when it is reopened.
				windows.SetMaximized(spec, windowState.Open && windowState.Maximized);
			}

			// Raising back to front leaves the last one on top; windows not listed stay behind, as in state.rs.
			foreach (string title in state.ZOrder)
			{
				if (byTitle.TryGetValue(title, out DemoSpec spec))
				{
					windows.Raise(spec);
				}
			}

			this.AttachInspector();
			this.AttachSystem();
		}

		/// <summary>
		/// Puts <paramref name="shell"/>'s windows and theme back as a first run has them, as agg-gui's
		/// on_reset_all does: the default windows (<see cref="GuiDemoSpecs.DefaultOpen"/>) open, tiled and stacked
		/// as a first run has them, every other window closed, the System theme with the Blue accent, and snapping on. The backend panel is left alone.
		/// </summary>
		public static void ApplyDefaults(GuiDemoShell shell)
		{
			shell.DemoTheme.SetPreference(ThemePreference.System);
			shell.DemoTheme.SetAccent(AccentColor.Blue);
			shell.TopBar.SetSnapEnabled(true);

			shell.Windows.ResetToDefaultLayout();
		}

		/// <summary>"Reset all state": <see cref="ApplyDefaults"/>, then forgets the saved state, so a run started
		/// now begins from the defaults. A save the reset itself scheduled is dropped; later changes save as usual.</summary>
		public void ResetAll()
		{
			ApplyDefaults(this.shell);
			this.SavePending = false;
			this.Store.Clear();
		}

		/// <summary>Starts saving on every change to the page.</summary>
		public void Start()
		{
			if (this.started)
			{
				return;
			}

			this.started = true;
			this.shell.Windows.LayoutChanged += this.Page_Changed;
			this.shell.DemoTheme.ThemeChanged += this.Page_Changed;
			this.shell.TopBar.SnapToggled += this.Page_Changed;
			this.shell.TopBar.BackendPanelToggled += this.Page_Changed;
			this.shell.Windows.OpenChanged += this.Windows_OpenChanged;
		}

		/// <summary>Stops following the page and saves now, so nothing waiting on the delay is lost.</summary>
		public void Stop()
		{
			if (!this.started)
			{
				return;
			}

			this.started = false;
			this.shell.Windows.LayoutChanged -= this.Page_Changed;
			this.shell.DemoTheme.ThemeChanged -= this.Page_Changed;
			this.shell.TopBar.SnapToggled -= this.Page_Changed;
			this.shell.TopBar.BackendPanelToggled -= this.Page_Changed;
			this.shell.Windows.OpenChanged -= this.Windows_OpenChanged;
			this.SaveNow();
		}

		/// <summary>Schedules a save unless one is already waiting.</summary>
		public void RequestSave()
		{
			if (this.SavePending)
			{
				return;
			}

			this.SavePending = true;
			this.scheduleDelayed(this.SaveIfPending, SaveDelaySeconds);
		}

		/// <summary>Writes the page's state to the store now.</summary>
		public void SaveNow()
		{
			this.SavePending = false;
			this.Store.Save(this.Capture().Serialize());
		}

		/// <summary>The delayed save; <see cref="SaveNow"/> may already have written it (the page closed).</summary>
		private void SaveIfPending()
		{
			if (this.SavePending)
			{
				this.SaveNow();
			}
		}

		private void Page_Changed(object sender, EventArgs e) => this.RequestSave();

		private void Windows_OpenChanged(object sender, DemoSpec spec)
		{
			this.AttachInspector();
			this.AttachSystem();
		}

		/// <summary>
		/// The System window, if it has been built. The first time it is found it gets the restored tab, and its
		/// setting and tab changes start scheduling saves.
		/// </summary>
		private Windows.Tools.SystemTypographyWindow AttachSystem()
		{
			DemoSpec spec = GuiDemoSpecs.All.First(s => s.Title == "System");
			var window = this.shell.Windows.GetWindow(spec)?.FindDescendant(spec.ContentName) as Windows.Tools.SystemTypographyWindow;
			if (window != null && window != this.systemWindow)
			{
				this.systemWindow = window;
				this.ApplySystemTab();
				window.FontTab.SettingChanged += this.System_Changed;
				window.Tabs.SelectedIndexChanged += this.System_Changed;
			}

			return window;
		}

		private void ApplySystemTab()
		{
			if (this.systemWindow != null
				&& this.systemTab >= 0
				&& this.systemTab < this.systemWindow.Tabs.Pages.Count)
			{
				this.systemWindow.Tabs.SelectedIndex = this.systemTab;
			}
		}

		private void System_Changed(object sender, EventArgs e)
		{
			if (this.started)
			{
				this.RequestSave();
			}
		}

		/// <summary>
		/// The Inspector panel, if its window has been built. The first time it is found it gets the restored state
		/// (agg-gui's apply_saved_state) and its changes start scheduling saves, as the tree, selection and split
		/// are part of the page's state (state.rs inspector).
		/// </summary>
		private InspectorPanel AttachInspector()
		{
			var panel = this.shell.Windows.GetWindow(GuiDemoSpecs.Inspector)?.FindDescendant(GuiDemoSpecs.Inspector.ContentName) as InspectorPanel;
			if (panel != null && panel != this.inspector)
			{
				this.inspector = panel;
				panel.ApplySavedState(this.inspectorState);
				panel.SavedStateChanged += this.Inspector_SavedStateChanged;
			}

			return panel;
		}

		private void Inspector_SavedStateChanged(object sender, EventArgs e)
		{
			if (this.started)
			{
				this.RequestSave();
			}
		}
	}
}
