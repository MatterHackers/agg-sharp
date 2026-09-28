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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout.Scrolling
{
	/// <summary>
	/// The Scrolling window's "Appearance" tab, agg-gui's appearance.rs (egui's ScrollAppearance): presets, a Details
	/// header of bar settings, the visibility policy and a content-length slider, all restyling the lorem-ipsum view
	/// at the bottom.
	/// </summary>
	/// <remarks>
	/// agg-gui writes these settings to a process-wide scroll style so every ScrollView restyles. Here they drive only
	/// this tab's view through <see cref="ScrollBar"/>'s per-bar options, so the rest of the app keeps its bars.
	/// </remarks>
	public class AppearanceTab : GuiWidget
	{
		public static readonly string[] PresetLabels = { "Solid", "Thin", "Floating" };

		public static readonly string[] TypeLabels = { "Solid", "Floating" };

		public static readonly string[] ColorLabels = { "Background", "Foreground" };

		public static readonly string[] VisibilityLabels = { "AlwaysHidden", "VisibleWhenNeeded", "AlwaysVisible" };

		// Each compact control's width, as appearance.rs's CTRL_W.
		private const double ControlWidth = 70;

		private readonly DemoTheme demoTheme;
		private readonly MiscDemoKit kit;
		private readonly GuiWidget[] rules;
		private readonly FlowLayoutWidget content;
		private bool loading;

		internal AppearanceTab(DemoTheme demoTheme, MiscDemoKit kit)
		{
			this.demoTheme = demoTheme;
			this.kit = kit;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Padding = new BorderDouble(10),
			};
			this.AddChild(column);

			// Presets: each replaces the whole style and picks the visibility that suits it.
			this.Presets = new SegmentedControl(PresetLabels, kit.Theme, 2) { Name = "Scrolling Appearance Presets" };
			this.Presets.SelectedIndexChanged += (s, e) => this.ApplyPreset(this.Presets.SelectedIndex);
			column.AddChild(ScrollToTab.Row(kit, "Presets:", this.Presets));

			// Details: one compact control per setting, collapsed at first.
			this.Details = new CollapsingHeader("Details", kit.Theme, expanded: false) { Name = "Scrolling Appearance Details" };
			GuiWidget body = this.Details.Body;
			this.Type = new SegmentedControl(TypeLabels, kit.Theme, 1) { Name = "Scrolling Appearance Type" };
			this.Type.SelectedIndexChanged += (s, e) => this.Apply();
			body.AddChild(ScrollToTab.Row(kit, "Type:", this.Type));

			this.MarginSame = kit.CheckBox("Scrolling Appearance Margin Same", "same", true, 12);
			this.MarginSame.CheckedStateChanged += (s, e) => this.Apply();
			this.ContentMargin = this.Drag("Scrolling Appearance Content Margin", 0, 50, 1, 0);
			body.AddChild(ScrollToTab.Row(kit, "Content margin:", this.MarginSame, this.ContentMargin));

			this.BarWidth = this.Drag("Scrolling Appearance Bar Width", 0, 50, 1, 0);
			body.AddChild(this.DragRow(this.BarWidth, "Full bar width"));
			this.FloatingWidth = this.Drag("Scrolling Appearance Floating Width", 0, 50, 1, 0);
			this.FloatingWidthRow = this.DragRow(this.FloatingWidth, "Thin bar width (on hover expands to full)");
			body.AddChild(this.FloatingWidthRow);
			this.HandleMin = this.Drag("Scrolling Appearance Handle Min", 0, 80, 1, 0);
			body.AddChild(this.DragRow(this.HandleMin, "Minimum handle length"));
			this.OuterMargin = this.Drag("Scrolling Appearance Outer Margin", 0, 40, 1, 0);
			body.AddChild(this.DragRow(this.OuterMargin, "Outer margin"));

			this.BarColor = new SegmentedControl(ColorLabels, kit.Theme, 1) { Name = "Scrolling Appearance Color" };
			this.BarColor.SelectedIndexChanged += (s, e) => this.Apply();
			body.AddChild(ScrollToTab.Row(kit, "Color:", this.BarColor));

			// Inner margin shows only for solid bars, as egui shows it only when the bar takes room.
			this.InnerMargin = this.Drag("Scrolling Appearance Inner Margin", 0, 40, 1, 0);
			this.InnerMarginRow = this.DragRow(this.InnerMargin, "Inner margin");
			body.AddChild(this.InnerMarginRow);

			GuiWidget detailsRule = this.Rule();
			body.AddChild(detailsRule);

			this.FadeStrength = this.Drag("Scrolling Appearance Fade Strength", 0, 1, 0.05, 2);
			body.AddChild(this.DragRow(this.FadeStrength, "Fade strength"));
			this.FadeSize = this.Drag("Scrolling Appearance Fade Size", 0, 200, 1, 0);
			body.AddChild(this.DragRow(this.FadeSize, "Fade size"));
			column.AddChild(this.Details);

			this.Visibility = new SegmentedControl(VisibilityLabels, kit.Theme, 1) { Name = "Scrolling Appearance Visibility" };
			this.Visibility.SelectedIndexChanged += (s, e) => this.Apply();
			column.AddChild(ScrollToTab.Row(kit, "ScrollBarVisibility:", this.Visibility));

			WrappedTextWidget hint = kit.Wrapped("When to show scroll bars; resize the window to see the effect.", 11);
			hint.HAnchor = HAnchor.Stretch;
			column.AddChild(hint);

			GuiWidget visibilityRule = this.Rule();
			column.AddChild(visibilityRule);

			this.ContentLength = kit.Slider("Scrolling Appearance Content Length", 2, 1, 100, 1);
			TextWidget lengthValue = kit.Label("2");
			this.ContentLength.ValueChanged += (s, e) =>
			{
				lengthValue.Text = this.ParagraphCount.ToString();
				this.FillContent();
			};
			column.AddChild(ScrollToTab.Row(kit, "Content length", this.ContentLength, lengthValue));

			GuiWidget contentRule = this.Rule();
			column.AddChild(contentRule);
			this.rules = new[] { detailsRule, visibilityRule, contentRule };

			this.content = new FlowLayoutWidget(FlowDirection.TopToBottom) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Fit };
			this.View = new ScrollableWidget(autoScroll: true)
			{
				Name = "Scrolling Appearance View",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.View.ScrollArea.HAnchor = HAnchor.Stretch;
			this.View.AddChild(this.content);
			column.AddChild(this.View);
			this.FillContent();

			this.ApplyPreset(this.Presets.SelectedIndex);
			this.Recolor();
		}

		public SegmentedControl Presets { get; }

		public CollapsingHeader Details { get; }

		public SegmentedControl Type { get; }

		public CheckBox MarginSame { get; }

		public DragValue ContentMargin { get; }

		public DragValue BarWidth { get; }

		public DragValue FloatingWidth { get; }

		public GuiWidget FloatingWidthRow { get; }

		public DragValue HandleMin { get; }

		public DragValue OuterMargin { get; }

		public SegmentedControl BarColor { get; }

		public DragValue InnerMargin { get; }

		public GuiWidget InnerMarginRow { get; }

		public DragValue FadeStrength { get; }

		public DragValue FadeSize { get; }

		public SegmentedControl Visibility { get; }

		public Slider ContentLength { get; }

		public ScrollableWidget View { get; }

		/// <summary>How many lorem-ipsum paragraphs the view holds.</summary>
		public int ParagraphCount => (int)Math.Round(this.ContentLength.Value);

		public bool IsFloating => this.Type.SelectedIndex == 1;

		public void Recolor()
		{
			foreach (GuiWidget rule in this.rules)
			{
				rule.BackgroundColor = this.demoTheme.Palette.Separator;
			}

			this.Apply();
		}

		/// <summary>
		/// Loads a preset's settings into the controls - the values of agg-gui's ScrollBarStyle::solid / thin /
		/// floating - and the visibility that suits it: thin is always visible (the thin bar is the point), the
		/// others show when needed.
		/// </summary>
		public void ApplyPreset(int preset)
		{
			this.loading = true;
			try
			{
				this.Type.SelectedIndex = preset == 0 ? 0 : 1;
				this.BarColor.SelectedIndex = preset == 2 ? 1 : 0;
				this.BarWidth.Value = preset == 0 ? 6 : 10;
				this.FloatingWidth.Value = 2;
				this.HandleMin.Value = 12;
				this.OuterMargin.Value = 0;
				this.InnerMargin.Value = 4;
				this.ContentMargin.Value = 0;
				this.MarginSame.Checked = true;
				this.FadeStrength.Value = 0.5;
				this.FadeSize.Value = 20;
				this.Visibility.SelectedIndex = preset == 1 ? 2 : 1;
			}
			finally
			{
				this.loading = false;
			}

			this.Apply();
		}

		/// <summary>Pushes every control's value into the view's bars, fade and content.</summary>
		public void Apply()
		{
			if (this.loading || this.View == null)
			{
				return;
			}

			double scale = DeviceScale;
			bool floating = this.IsFloating;
			this.FloatingWidthRow.Visible = floating;
			this.InnerMarginRow.Visible = !floating;

			ScrollBar bar = this.View.VerticalScrollBar;
			bar.Floating = floating;
			bar.BarWidth = this.BarWidth.Value * scale;
			bar.FloatingWidth = floating ? this.FloatingWidth.Value * scale : 0;
			bar.HandleMinLength = this.HandleMin.Value * scale;
			bar.OuterMargin = this.OuterMargin.Value * scale;
			bar.InnerMargin = floating ? 0 : this.InnerMargin.Value * scale;

			// Background: a neutral track under a brighter thumb. Foreground: no track, an accent thumb.
			DemoPalette palette = this.demoTheme.Palette;
			Color accent = this.kit.Theme.PrimaryAccentColor;
			bool foreground = this.BarColor.SelectedIndex == 1;
			bar.TrackColor = foreground ? Color.Transparent : palette.WidgetBackground;
			bar.ThumbColor = foreground ? accent.WithAlpha(170) : palette.TextDim.WithAlpha(140);
			bar.ThumbHoverColor = foreground ? accent : palette.TextDim;

			bar.Show = this.Visibility.SelectedIndex switch
			{
				0 => ScrollBar.ShowState.Never,
				2 => ScrollBar.ShowState.Always,
				_ => ScrollBar.ShowState.WhenRequired,
			};

			ScrollEdgeFade fade = this.View.EdgeFade;
			fade.Strength = this.FadeStrength.Value;
			fade.Size = this.FadeSize.Value * scale;
			fade.Color = palette.PanelFill;

			// agg-gui keeps one content margin for both axes; "same" off still applies it to both, as there.
			this.content.Margin = new BorderDouble(this.ContentMargin.Value);
			this.View.Invalidate();
		}

		private void FillContent()
		{
			int count = this.ParagraphCount;
			if (this.content.Children.Count == count)
			{
				return;
			}

			this.content.CloseChildren();
			for (int i = 0; i < count; i++)
			{
				WrappedTextWidget paragraph = this.kit.Wrapped(PanelsWindow.LoremIpsum, 12);
				paragraph.HAnchor = HAnchor.Stretch;
				paragraph.Margin = new BorderDouble(0, 4);
				this.content.AddChild(paragraph);
			}
		}

		private DragValue Drag(string name, double minimum, double maximum, double step, int decimals)
		{
			var drag = new DragValue(minimum, minimum, maximum, this.kit.Theme)
			{
				Name = name,
				Decimals = decimals,
				Step = step,
				Speed = Math.Max(step, 0.001),
			};
			drag.Width = Math.Max(drag.Width, ControlWidth * DeviceScale);
			drag.ValueChanged += (s, e) => this.Apply();
			return drag;
		}

		private GuiWidget DragRow(DragValue drag, string description) => ScrollToTab.Row(this.kit, null, drag, this.kit.Label(description));

		private GuiWidget Rule() => new GuiWidget(1, DeviceScale) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Absolute, Margin = new BorderDouble(0, 3) };
	}
}
