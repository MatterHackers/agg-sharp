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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout
{
	/// <summary>
	/// agg-gui's "Window Options" window (text_demos/dialogs/basic.rs, window_options_with_cells, hosted by
	/// app_builder.rs): a title field and resizable / collapsible / auto-size check boxes that steer the real window
	/// it is shown in, the window's live size, and agg-gui's note on what it skips. Until it is attached to a window
	/// (<see cref="AttachHost"/>) the controls are inert, as agg-gui's fallback builder's are.
	/// </summary>
	public class WindowOptionsWindow : FlowLayoutWidget, IHostAwareDemoContent
	{
		public const string Note = "resizable, collapsible and auto-size drive the real host window. auto-size is agg-gui's own "
			+ "addition (not in egui). Skipped vs egui: title_bar / closable / constrain / hscroll / vscroll and the anchor "
			+ "controls - the window has no runtime hook for those yet. Editing title changes the visible title only; the "
			+ "window keeps its original identity so saved layout/z-order stay intact.";

		private readonly DemoTheme demoTheme;
		private readonly MiscDemoKit kit;
		private readonly List<GuiWidget> separators = new List<GuiWidget>();

		public WindowOptionsWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.demoTheme = demoTheme;
			this.kit = new MiscDemoKit(demoTheme);
			this.HAnchor = HAnchor.Stretch;

			// Fitted rather than stretched so auto-size has a content height to follow; the host's client area is
			// painted the panel colour (AttachHost) so the room below it matches.
			this.VAnchor = VAnchor.Fit | VAnchor.Top;
			this.Padding = new BorderDouble(16, 16, 16, 8);

			// agg-gui's column gap.
			double gap = 12;
			void Add(GuiWidget widget)
			{
				widget.Margin = new BorderDouble(widget.Margin.Left, gap / 2, widget.Margin.Right, gap / 2);
				this.AddChild(widget);
			}

			FlowLayoutWidget titleRow = this.kit.Row(0);
			titleRow.HAnchor = HAnchor.Stretch;
			TextWidget titleLabel = this.kit.Label("title:", 13);
			titleLabel.Margin = new BorderDouble(right: 8);
			titleRow.AddChild(titleLabel);
			this.TitleField = new ThemedTextEditWidget("Window Options", demoTheme.Theme, pixelWidth: 120 * GuiWidget.DeviceScale)
			{
				Name = "Window Options Title",
				HAnchor = HAnchor.Stretch,
			};
			this.TitleField.TextChanged += (s, e) => this.Apply(host => host.Title = this.TitleField.Text);
			titleRow.AddChild(this.TitleField);
			Add(titleRow);

			this.ResizableBox = this.kit.CheckBox("Window Options Resizable", "resizable", true, 13);
			this.ResizableBox.CheckedStateChanged += (s, e) => this.Apply(host => host.Resizable = this.ResizableBox.Checked);
			Add(this.ResizableBox);
			this.CollapsibleBox = this.kit.CheckBox("Window Options Collapsible", "collapsible", true, 13);
			this.CollapsibleBox.CheckedStateChanged += (s, e) => this.Apply(host => host.Collapsible = this.CollapsibleBox.Checked);
			Add(this.CollapsibleBox);
			this.AutoSizeBox = this.kit.CheckBox("Window Options Auto Size", "auto-size", false, 13);
			this.AutoSizeBox.CheckedStateChanged += (s, e) => this.Apply(host => host.AutoSize = this.AutoSizeBox.Checked);
			Add(this.AutoSizeBox);

			Add(this.Separator());
			this.SizeLabel = this.kit.Label("Current window size: —", 12);
			this.SizeLabel.Name = "Window Options Size";
			Add(this.SizeLabel);
			Add(this.Separator());

			WrappedTextWidget note = this.kit.Wrapped(Note, 11);
			note.HAnchor = HAnchor.Stretch;
			Add(note);

			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		/// <summary>The window these controls steer, or null until <see cref="AttachHost"/>.</summary>
		public WindowWidget Host { get; private set; }

		public ThemedTextEditWidget TitleField { get; }

		public CheckBox ResizableBox { get; }

		public CheckBox CollapsibleBox { get; }

		public CheckBox AutoSizeBox { get; }

		/// <summary>"Current window size: W × H", the visible window in design units.</summary>
		public TextWidget SizeLabel { get; }

		/// <summary>Takes <paramref name="host"/> as the window to steer and gives it the controls' current settings
		/// (agg-gui's WindowOptionCells::new: resizable, collapsible, not auto-sized, the window's own title).</summary>
		public void AttachHost(WindowWidget host)
		{
			this.Host = host;
			this.TitleField.Text = host.Title;
			host.Resizable = this.ResizableBox.Checked;
			host.Collapsible = this.CollapsibleBox.Checked;
			host.AutoSize = this.AutoSizeBox.Checked;
			host.SizeChanged += (s, e) => this.UpdateSizeLabel();
			this.UpdateSizeLabel();
			this.Recolor();
		}

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		private void Apply(Action<WindowWidget> change)
		{
			if (this.Host != null)
			{
				change(this.Host);
			}
		}

		private void UpdateSizeLabel()
		{
			// The visible card, without the grab border the widget carries around it.
			double border = DemoWindowHost.GrabBorder * 2;
			long width = (long)Math.Round(this.Host.Width / DeviceScale - border);
			long height = (long)Math.Round(this.Host.Height / DeviceScale - border);
			this.SizeLabel.Text = $"Current window size: {width} × {height}";
		}

		private GuiWidget Separator()
		{
			var separator = new GuiWidget { HAnchor = HAnchor.Stretch, Height = Math.Max(1, Math.Round(DeviceScale)) };
			this.separators.Add(separator);
			return separator;
		}

		private void OnThemeChanged(object sender, EventArgs e)
		{
			this.Recolor();
			this.Invalidate();
		}

		/// <summary>Pushes the current theme into the widgets that copied their colours when they were built.</summary>
		private void Recolor()
		{
			this.kit.Recolor();
			this.BackgroundColor = this.demoTheme.Palette.PanelFill;
			if (this.Host != null)
			{
				this.Host.ClientArea.BackgroundColor = this.demoTheme.Palette.PanelFill;
			}

			foreach (GuiWidget separator in this.separators)
			{
				separator.BackgroundColor = this.demoTheme.Palette.Separator;
			}
		}
	}
}
