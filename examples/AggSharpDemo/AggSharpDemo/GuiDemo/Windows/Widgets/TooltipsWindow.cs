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

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The "Tooltips" window, a port of agg-gui's tooltips demo (demo-ui/src/windows/basic.rs, itself egui's
	/// tooltip demo): a column of labels that each carry a tooltip beside a scroll area of 1000 hoverable lines
	/// that checks tooltips close on scroll. agg-sharp's tooltips are the SystemWindow's ToolTipManager
	/// showing a widget's ToolTipText, with its content tooltips (ToolTipManager.SetToolTipContent) for
	/// agg-gui's interactive one and its at-pointer placement (ToolTipManager.SetToolTipAtPointer) for the
	/// one that follows the mouse.
	/// </summary>
	public class TooltipsWindow : GuiWidget
	{
		public const string SourceUrl = "https://github.com/larsbrubaker/agg-gui/blob/main/demo-ui/src/windows/basic.rs";

		public const string ButtonTip = "This tooltip was created with\n.on_hover_ui(...)";

		public const string LinkUrl = "https://github.com/larsbrubaker/agg-gui";

		public const string NestedTip = "The tooltip has a tooltip in it!";

		public const string DisabledButtonTip = "A different tooltip when widget is disabled.\nThis tooltip was created with\n.on_disabled_hover_ui(...)";

		/// <summary>How many hoverable lines the scroll test holds, as agg-gui's does.</summary>
		public const int LineCount = 1000;

		private readonly DemoTheme demoTheme;
		private readonly ThemeConfig theme;

		// Widgets that copy a colour when built, recoloured on ThemeChanged.
		private readonly List<TextWidget> texts = new List<TextWidget>();
		private readonly List<WrappedTextWidget> wrappedTexts = new List<WrappedTextWidget>();
		private readonly List<GuiWidget> panels = new List<GuiWidget>();
		private readonly GuiWidget separator;

		public TooltipsWindow(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.theme = demoTheme.Theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			// agg-gui's Splitter: 62% to the tests, a 4-unit divider.
			this.Splitter = new Splitter
			{
				Name = "Tooltips Splitter",
				Orientation = Orientation.Vertical,
				SplitterSize = 4 * DeviceScale,
			};
			this.AddChild(this.Splitter);
			this.Splitter.Panel1Ratio = 0.62;

			var left = this.Panel(14);
			this.Splitter.Panel1.AddChild(left);

			var sourceLink = new Hyperlink("(source code)", this.theme, SourceUrl)
			{
				Name = "Tooltips Source Link",
				HAnchor = HAnchor.Center,
			};
			left.AddChild(sourceLink);

			left.AddChild(this.TipLabel("All labels in this demo have tooltips.", "Yes, even this one."));

			// agg-gui stacks a widget's tooltips as lines of one panel (Tooltip::with_text), as a multi-line
			// ToolTipText does.
			left.AddChild(this.TipLabel("Some widgets have multiple tooltips!", "The first tooltip.\nThe second tooltip."));

			this.InteractiveLabel = this.TipLabel("Tooltips can contain interactive widgets.", null);
			this.InteractiveLabel.Name = "Tooltips Interactive";
			ToolTipManager.SetToolTipContent(this.InteractiveLabel, this.InteractiveTip);
			left.AddChild(this.InteractiveLabel);

			// agg-gui's own "selectable" tips (this one and the scroll lines) are plain text tooltips too.
			left.AddChild(this.TipLabel("You can put selectable text in tooltips too.", "You can select this text."));

			this.AtPointerLabel = this.TipLabel("This tooltip shows at the mouse cursor.", "Move me around!!");
			this.AtPointerLabel.Name = "Tooltips At Pointer";
			ToolTipManager.SetToolTipAtPointer(this.AtPointerLabel, true);
			left.AddChild(this.AtPointerLabel);

			this.separator = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				Height = Math.Max(1, Math.Round(DeviceScale)),
				Margin = new BorderDouble(0, 4),
			};
			left.AddChild(this.separator);

			var enabledHeader = this.Wrapped(
				"You can have different tooltips depending on whether or not a widget is enabled:",
				"Check the tooltip of the button below, and see how it changes depending on whether or not it is enabled.");
			left.AddChild(enabledHeader);

			var row = new FlowLayoutWidget { HAnchor = HAnchor.Left | HAnchor.Fit, VAnchor = VAnchor.Fit, Margin = new BorderDouble(0, 4) };
			left.AddChild(row);

			this.EnabledCheckBox = new CheckBox("Enabled", this.theme.TextColor, this.theme.DefaultFontSize)
			{
				Name = "Tooltips Enabled",
				Checked = true,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(right: 8),
				ToolTipText = "Controls whether or not the following button is enabled.",
			};
			row.AddChild(this.EnabledCheckBox);

			this.SometimesClickableButton = new ThemedTextButton("Sometimes clickable", this.theme)
			{
				Name = "Tooltips Sometimes Clickable",
				VAnchor = VAnchor.Center,
				Margin = 0,
				ToolTipText = ButtonTip,
			};
			row.AddChild(this.SometimesClickableButton);

			// egui's on_disabled_hover_ui: the button speaks differently while it is greyed out.
			this.EnabledCheckBox.CheckedStateChanged += (s, e) =>
			{
				bool enabled = this.EnabledCheckBox.Checked;
				this.SometimesClickableButton.Enabled = enabled;
				this.SometimesClickableButton.ToolTipText = enabled ? ButtonTip : DisabledButtonTip;
			};

			var right = this.Panel(10);
			this.Splitter.Panel2.AddChild(right);
			right.AddChild(this.Wrapped(
				"The scroll area below has many labels with interactive tooltips. The purpose is to test that the tooltips close when you scroll.",
				"Try hovering a label below, then scroll!"));

			this.ScrollArea = new ScrollableWidget(autoScroll: true)
			{
				Name = "Tooltips Scroll",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.ScrollArea.ScrollArea.HAnchor = HAnchor.Stretch;
			right.AddChild(this.ScrollArea);

			var lines = new FlowLayoutWidget(FlowDirection.TopToBottom) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Fit };
			for (int i = 0; i < LineCount; i++)
			{
				TextWidget line = this.TipLabel($"This is line {i}", "This tooltip is interactive, because the text in it is selectable.");
				line.Name = $"Tooltips Line {i}";
				line.Margin = new BorderDouble(0, 1);
				lines.AddChild(line);
			}

			this.ScrollArea.AddChild(lines);

			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		public Splitter Splitter { get; }

		/// <summary>The label whose tooltip is a panel holding a link with its own tooltip.</summary>
		public TextWidget InteractiveLabel { get; }

		/// <summary>The label whose tooltip opens at, and follows, the mouse.</summary>
		public TextWidget AtPointerLabel { get; }

		public CheckBox EnabledCheckBox { get; }

		public ThemedTextButton SometimesClickableButton { get; }

		public ScrollableWidget ScrollArea { get; }

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		/// <summary>agg-gui's FlexColumn.with_panel_bg(): a padded column filling its splitter side.</summary>
		private FlowLayoutWidget Panel(double padding)
		{
			var panel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Padding = new BorderDouble(padding),
			};
			this.panels.Add(panel);
			return panel;
		}

		/// <summary>A label carrying a tooltip. TextWidgets are unselectable by default, which hides them from
		/// the tooltip hit walk, so these opt back in.</summary>
		private TextWidget TipLabel(string text, string tip)
		{
			var widget = new TextWidget(text, pointSize: this.theme.DefaultFontSize, textColor: this.theme.TextColor)
			{
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(0, 4),
				AutoExpandBoundsToText = true,
				Selectable = true,
				ToolTipText = tip,
			};
			this.texts.Add(widget);
			return widget;
		}

		/// <summary>
		/// agg-gui's interactive_link_tip: a label and a link that has its own tooltip, on a tooltip panel. Built
		/// each time the tooltip opens, so it wears the current theme.
		/// </summary>
		private GuiWidget InteractiveTip()
		{
			var palette = this.demoTheme.Palette;
			var panel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Name = "Tooltips Interactive Tip",
				Padding = new BorderDouble(8, 6),
				BackgroundColor = palette.WindowFill,
				BackgroundRadius = new RadiusCorners(5 * DeviceScale),
				BackgroundOutlineWidth = DeviceScale,
				BorderColor = palette.WidgetStroke,
			};

			panel.AddChild(new TextWidget("This tooltip contains a link:", pointSize: this.theme.DefaultFontSize, textColor: this.theme.TextColor)
			{
				Margin = new BorderDouble(bottom: 4),
			});

			var link = new Hyperlink("github.com/larsbrubaker/agg-gui", this.theme, LinkUrl)
			{
				Name = "Tooltips Interactive Link",
				ToolTipText = NestedTip,
			};
			panel.AddChild(link);
			return panel;
		}

		private WrappedTextWidget Wrapped(string text, string tip)
		{
			var widget = new WrappedTextWidget(text, this.theme.DefaultFontSize, textColor: this.theme.TextColor)
			{
				Margin = new BorderDouble(0, 4),
				ToolTipText = tip,
			};
			this.wrappedTexts.Add(widget);
			return widget;
		}

		private void OnThemeChanged(object sender, EventArgs e)
		{
			this.Recolor();
			this.Invalidate();
		}

		/// <summary>Pushes the current theme into the widgets that copied their colours when they were built.</summary>
		private void Recolor()
		{
			foreach (TextWidget text in this.texts)
			{
				text.TextColor = this.theme.TextColor;
			}

			foreach (WrappedTextWidget text in this.wrappedTexts)
			{
				text.TextColor = this.theme.TextColor;
			}

			foreach (GuiWidget panel in this.panels)
			{
				panel.BackgroundColor = this.demoTheme.Palette.PanelFill;
			}

			this.EnabledCheckBox.TextColor = this.theme.TextColor;
			this.separator.BackgroundColor = this.demoTheme.Palette.Separator;
			this.Splitter.SplitterBackground = this.demoTheme.Palette.Separator;
		}
	}
}
