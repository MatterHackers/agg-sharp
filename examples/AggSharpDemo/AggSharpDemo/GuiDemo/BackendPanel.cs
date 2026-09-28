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
	/// <summary>agg-gui's RunMode: repaint only when something asks for it, or every frame.</summary>
	public enum DemoRunMode
	{
		Reactive,
		Continuous,
	}

	/// <summary>
	/// The GUI demo's backend panel on the left of the canvas (View > Backend Panel), after agg-gui's
	/// demo-ui/src/backend_panel.rs: the renderer and platform, the live screen size, the run mode with the
	/// frame-time sparkline and mean CPU time, the SSAA factor for the 3D background, the Inspector toggle,
	/// the open windows, and "Reset all state".
	/// </summary>
	/// <remarks>
	/// The panel only holds these settings and raises events; <see cref="GuiDemoShell"/> acts on them (redraws
	/// continuously, records frame times, resets the page). Automation names all start "Backend ".
	/// </remarks>
	public class BackendPanel : FlowLayoutWidget
	{
		/// <summary>backend_panel.rs's panel width.</summary>
		public const double PanelWidth = 240;

		/// <summary>The SSAA factors offered, in segment order.</summary>
		public static readonly IReadOnlyList<int> SsaaFactors = new[] { 1, 2, 3, 4 };

		private const string ReactiveDescription = "Only running UI code when there are animations or input.";

		private const string ContinuousDescription = "Running continuously as fast as possible.";

		private readonly DemoTheme demoTheme;

		private readonly DemoWindowHost host;

		private readonly List<TextWidget> labels = new List<TextWidget>();

		private readonly List<LiveText> liveTexts = new List<LiveText>();

		private readonly List<GuiWidget> separators = new List<GuiWidget>();

		private readonly FlowLayoutWidget openWindowsList;

		private readonly FrameSparkline sparkline;

		private readonly WrappedTextWidget runModeDescription;

		private readonly LiveText fpsText;

		private readonly ThemedTextButton resetButton;

		public BackendPanel(DemoWindowHost host, DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.host = host;
			this.demoTheme = demoTheme;
			ThemeConfig theme = demoTheme.Theme;

			this.Name = "GuiDemo Backend Panel";
			this.HAnchor = HAnchor.Absolute;
			this.Width = PanelWidth;
			this.VAnchor = VAnchor.Stretch;

			this.AddLabel("Backend", 12, new BorderDouble(12, 4, 12, 8), "Backend Heading");
			this.AddSeparator(4, 4);

			this.AddLive("Backend Running Inside", () => $"agg-sharp running inside {this.PlatformDescription}.");
			this.AddLive("Backend Renderer", () => "Renderer: " + this.RendererDescription);
			this.AddLive("Backend Platform", () => "Backend: " + this.PlatformDescription);
			this.AddLive("Backend Screen Size", () => this.ScreenSizeDescription);
			this.AddSeparator(4, 8);

			this.AddLabel("Mode", 9, new BorderDouble(12, 2, 12, 2));
			this.RunModeControl = new SegmentedControl(Enum.GetNames(typeof(DemoRunMode)), theme)
			{
				Name = "Backend Run Mode",
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(12, 4),
			};
			this.RunModeControl.SelectedIndexChanged += (s, e) => this.SetRunMode((DemoRunMode)this.RunModeControl.SelectedIndex);
			this.AddChild(this.RunModeControl);
			// Wrapped, and only rewritten when the mode changes; the per-frame numbers are LiveText below.
			this.runModeDescription = new WrappedTextWidget(ReactiveDescription, pointSize: 9)
			{
				Name = "Backend Run Mode Description",
				Margin = new BorderDouble(12, 2),
			};
			this.AddChild(this.runModeDescription);
			this.fpsText = this.AddLive("Backend FPS", () => $"FPS: {this.History.Fps:0.0}");
			this.fpsText.Visible = false;
			this.AddLive("Backend Mean CPU", () => $"Mean CPU usage: {this.History.MeanMs:0.00} ms / frame");
			this.sparkline = new FrameSparkline(this.History)
			{
				Name = "Backend Sparkline",
				Margin = new BorderDouble(12, 4),
			};
			this.AddChild(this.sparkline);
			this.AddSeparator(8, 8);

			this.AddLabel("3D background SSAA", 9, new BorderDouble(12, 2, 12, 2));
			this.SsaaControl = new SegmentedControl(SsaaFactors.Select(f => f + "x"), theme, SsaaFactors.ToList().IndexOf(DefaultSsaaFactor))
			{
				Name = "Backend SSAA",
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(12, 4),
			};
			this.SsaaControl.SelectedIndexChanged += (s, e) => this.SetSsaaFactor(SsaaFactors[this.SsaaControl.SelectedIndex]);
			this.AddChild(this.SsaaControl);
			this.AddSeparator(8, 8);

			this.AddLabel("agg-sharp windows:", 9, new BorderDouble(12, 2, 12, 0));

			// backend_panel.rs's System and Inspector pills (gear, search): the System one follows the System
			// window itself, as its sidebar row does.
			this.SystemPill = new TogglePill("System", "\uF013", demoTheme)
			{
				Name = "Backend System",
				IsOn = host.IsOpen(SystemSpec),
			};
			this.SystemPill.Click += (s, e) => this.host.SetOpen(SystemSpec, !this.host.IsOpen(SystemSpec));
			this.AddChild(this.SystemPill);
			this.InspectorPill = new TogglePill("Inspector", "\uF002", demoTheme)
			{
				Name = "Backend Inspector",
			};
			this.InspectorPill.Click += (s, e) => this.SetInspectorEnabled(!this.InspectorEnabled);
			this.AddChild(this.InspectorPill);
			this.AddSeparator(8, 8);

			this.AddLabel("Open windows:", 9, new BorderDouble(12, 2, 12, 0));
			this.openWindowsList = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Name = "Backend Open Windows",
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(20, 2, 12, 2),
			};
			this.AddChild(this.openWindowsList);
			this.AddSeparator(8, 8);

			this.resetButton = new ThemedTextButton("Reset all state", theme)
			{
				Name = "Backend Reset",
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(12, 12, 12, 4),
			};
			this.resetButton.Click += (s, e) => this.ResetAllRequested?.Invoke(this, EventArgs.Empty);
			this.AddChild(this.resetButton);

			this.RebuildOpenWindows();
			this.ApplyTheme();

			host.LayoutChanged += this.Host_LayoutChanged;
			host.OpenChanged += this.Host_OpenChanged;
			demoTheme.ThemeChanged += this.DemoTheme_ThemeChanged;
		}

		/// <summary>The SSAA factor until the user picks another.</summary>
		public const int DefaultSsaaFactor = 1;

		/// <summary>Raised after <see cref="RunMode"/> changed.</summary>
		public event EventHandler RunModeChanged;

		/// <summary>Raised after <see cref="SsaaFactor"/> changed; the 3D Animation window re-renders at the new factor.</summary>
		public event EventHandler SsaaFactorChanged;

		/// <summary>Raised after <see cref="InspectorEnabled"/> changed.</summary>
		public event EventHandler InspectorToggled;

		/// <summary>Raised by "Reset all state"; the shell does the resetting.</summary>
		public event EventHandler ResetAllRequested;

		/// <summary>Recent frame times; the shell records one per frame drawn.</summary>
		public FrameHistory History { get; } = new FrameHistory();

		public DemoRunMode RunMode { get; private set; } = DemoRunMode.Reactive;

		/// <summary>How many times the 3D background's width and height it renders at before downsampling.</summary>
		public int SsaaFactor { get; private set; } = DefaultSsaaFactor;

		public bool InspectorEnabled { get; private set; }

		public SegmentedControl RunModeControl { get; }

		public SegmentedControl SsaaControl { get; }

		/// <summary>Opens and closes the System window.</summary>
		public TogglePill SystemPill { get; }

		/// <summary>Shows and flips <see cref="InspectorEnabled"/>.</summary>
		public TogglePill InspectorPill { get; }

		/// <summary>The titles listed under "Open windows", in the order shown.</summary>
		public IEnumerable<string> OpenWindowTitles => this.openWindowsList.Children.Select(child => child.Text);

		/// <summary>The graphics API and adapter, e.g. "WebGPU / Metal Apple M5", once the platform has a device.</summary>
		public string RendererDescription
		{
			get
			{
				string adapter = ParseRenderStatus(ReadRenderStatus(this.PlatformWindow));
				return adapter == null ? "WebGPU" : "WebGPU / " + adapter;
			}
		}

		/// <summary>The platform host the page runs in: Mac, Win32, X11, Browser, or "none" off screen.</summary>
		public string PlatformDescription
		{
			get
			{
				if (OperatingSystem.IsBrowser())
				{
					return "Browser";
				}

				string name = this.PlatformWindow?.GetType().Name;
				return string.IsNullOrEmpty(name) ? "none" : name.Replace("SystemWindow", string.Empty);
			}
		}

		/// <summary>The window's size in pixels and the device scale.</summary>
		public string ScreenSizeDescription
		{
			get
			{
				GuiWidget top = this.TopmostParent();
				return $"Screen size: {top.Width:0} x {top.Height:0} @ {GuiWidget.DeviceScale:0.##}x";
			}
		}

		private IPlatformWindow PlatformWindow => (this.TopmostParent() as SystemWindow)?.PlatformWindow;

		/// <summary>
		/// The adapter out of a host's RenderStatusReport ("Metal Apple M5, presented 12" gives "Metal Apple M5"),
		/// or null when there is no report or the device is not up yet.
		/// </summary>
		public static string ParseRenderStatus(string status)
		{
			if (string.IsNullOrWhiteSpace(status) || status.StartsWith("webgpu not initialized", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}

			int presented = status.IndexOf(", presented", StringComparison.Ordinal);
			return (presented >= 0 ? status.Substring(0, presented) : status).Trim();
		}

		/// <summary>Sets <see cref="RunMode"/> (and its segment), raising <see cref="RunModeChanged"/> if it changed.</summary>
		public void SetRunMode(DemoRunMode runMode)
		{
			this.RunModeControl.SelectedIndex = (int)runMode;
			if (runMode != this.RunMode)
			{
				this.RunMode = runMode;
				bool continuous = runMode == DemoRunMode.Continuous;
				this.runModeDescription.Text = continuous ? ContinuousDescription : ReactiveDescription;

				// FPS only means something when frames are not waiting on input.
				this.fpsText.Visible = continuous;
				this.RunModeChanged?.Invoke(this, EventArgs.Empty);
				this.Invalidate();
			}
		}

		/// <summary>Sets <see cref="SsaaFactor"/> (one of <see cref="SsaaFactors"/>; others are ignored),
		/// raising <see cref="SsaaFactorChanged"/> if it changed.</summary>
		public void SetSsaaFactor(int factor)
		{
			int index = SsaaFactors.ToList().IndexOf(factor);
			if (index < 0)
			{
				return;
			}

			this.SsaaControl.SelectedIndex = index;
			if (factor != this.SsaaFactor)
			{
				this.SsaaFactor = factor;
				this.SsaaFactorChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>Sets <see cref="InspectorEnabled"/>, raising <see cref="InspectorToggled"/> if it changed.</summary>
		public void SetInspectorEnabled(bool enabled)
		{
			this.InspectorPill.IsOn = enabled;
			if (enabled != this.InspectorEnabled)
			{
				this.InspectorEnabled = enabled;
				this.InspectorToggled?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>Puts the panel's own settings back to their defaults (part of "Reset all state").</summary>
		public void ResetSettings()
		{
			this.SetRunMode(DemoRunMode.Reactive);
			this.SetSsaaFactor(DefaultSsaaFactor);
			this.SetInspectorEnabled(false);
		}

		public override void OnClosed(EventArgs e)
		{
			this.host.LayoutChanged -= this.Host_LayoutChanged;
			this.host.OpenChanged -= this.Host_OpenChanged;
			this.demoTheme.ThemeChanged -= this.DemoTheme_ThemeChanged;
			base.OnClosed(e);
		}

		/// <summary>The render status every platform window reports (and the smoke log prints). It is declared per
		/// host rather than on <see cref="IPlatformWindow"/>, so it is read by name, as AutomationRunner does.</summary>
		private static string ReadRenderStatus(IPlatformWindow platformWindow)
		{
			return platformWindow?.GetType().GetProperty("RenderStatusReport")?.GetValue(platformWindow) as string;
		}

		/// <summary>The System window's spec, which <see cref="SystemPill"/> opens.</summary>
		private static DemoSpec SystemSpec => GuiDemoSpecs.All.First(spec => spec.Title == "System");

		private void Host_LayoutChanged(object sender, EventArgs e) => this.RebuildOpenWindows();

		private void Host_OpenChanged(object sender, DemoSpec spec)
		{
			if (spec == SystemSpec)
			{
				this.SystemPill.IsOn = this.host.IsOpen(spec);
			}
		}

		private void DemoTheme_ThemeChanged(object sender, EventArgs e) => this.ApplyTheme();

		/// <summary>Lists the open windows front to back; rebuilt only when the host's layout changes, never per frame.</summary>
		private void RebuildOpenWindows()
		{
			string[] titles = this.host.ZOrder.Reverse().Select(spec => spec.Title).ToArray();
			if (titles.SequenceEqual(this.OpenWindowTitles))
			{
				return;
			}

			this.openWindowsList.CloseChildren();
			foreach (string title in titles)
			{
				this.openWindowsList.AddChild(new TextWidget(title, pointSize: 9, textColor: this.demoTheme.Palette.TextDim)
				{
					Name = "Backend Open " + title,
					HAnchor = HAnchor.Left,
					Margin = new BorderDouble(0, 1),
				});
			}

			if (titles.Length == 0)
			{
				this.openWindowsList.AddChild(new TextWidget("(none)", pointSize: 9, textColor: this.demoTheme.Palette.TextDim)
				{
					Name = "Backend Open None",
					HAnchor = HAnchor.Left,
				});
			}
		}

		private void AddLabel(string text, double pointSize, BorderDouble margin, string name = null)
		{
			var label = new TextWidget(text, pointSize: pointSize)
			{
				Name = name ?? "Backend Label " + text,

				// Left, not the default Absolute: an Absolute child keeps its own X and ignores its Margin.
				HAnchor = HAnchor.Left,
				Margin = margin,
			};
			this.labels.Add(label);
			this.AddChild(label);
		}

		private LiveText AddLive(string name, Func<string> text)
		{
			var live = new LiveText(text)
			{
				Name = name,
				Margin = new BorderDouble(12, 0),
			};
			this.liveTexts.Add(live);
			this.AddChild(live);
			return live;
		}

		private void AddSeparator(double above, double below)
		{
			var line = new HorizontalLine()
			{
				Margin = new BorderDouble(0, below, 0, above),
			};
			this.separators.Add(line);
			this.AddChild(line);
		}

		private void ApplyTheme()
		{
			DemoPalette palette = this.demoTheme.Palette;
			this.BackgroundColor = palette.PanelFill;
			foreach (TextWidget label in this.labels)
			{
				label.TextColor = palette.TextColor;
			}

			foreach (LiveText live in this.liveTexts)
			{
				live.TextColor = palette.TextColor;
			}

			foreach (GuiWidget line in this.separators)
			{
				line.BackgroundColor = palette.Separator;
			}

			foreach (TextWidget title in this.openWindowsList.Children.OfType<TextWidget>())
			{
				title.TextColor = palette.TextDim;
			}

			this.runModeDescription.TextColor = palette.TextColor;
			this.SystemPill.ApplyTheme();
			this.InspectorPill.ApplyTheme();
			this.sparkline.TrackColor = palette.WidgetBackground;
			this.sparkline.LineColor = DemoTheme.ColorOf(this.demoTheme.Accent);
			this.resetButton.TextColor = palette.TextColor;
			this.resetButton.BackgroundColor = palette.WidgetBackground;
			this.resetButton.HoverColor = this.demoTheme.Theme.SlightShade;
			this.Invalidate();
		}
	}
}
