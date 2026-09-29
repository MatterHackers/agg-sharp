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
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The "Widget Gallery" window: one of every interactive widget, a port of agg-gui's
	/// demo-ui/src/windows/gallery.rs (itself egui's widget gallery). A two column grid of
	/// "doc link | widget" rows, then the Visible / Interactive / Opacity bar and a docs footer, all in a
	/// vertical scroll.
	/// </summary>
	/// <remarks>
	/// Shared state is kept as gallery.rs keeps it: one bool (Button, Link and Checkbox), one selection
	/// (RadioButton, SegmentedControl, SelectableLabel and ComboBox), one scalar 0..360 (Slider, DragValue and
	/// ProgressBar). Each group is pushed to every widget showing it from one setter, guarded so the
	/// widgets' own change events do not bounce back.
	/// </remarks>
	public class WidgetGalleryWindow : ScrollableWidget
	{
		/// <summary>gallery.rs's left column width and the gap before the widget column.</summary>
		private const double LabelColumnWidth = 132;

		private const double ColumnGap = 40;

		private const string RepoUrl = "https://github.com/MatterHackers/agg-sharp";

		private static readonly string[] Choices = { "First", "Second", "Third" };

		private readonly DemoTheme demoTheme;
		private readonly ThemeConfig theme;

		// Widgets that copy a colour when built (agg-sharp's older widgets) and so are recoloured on ThemeChanged.
		private readonly List<TextWidget> texts = new List<TextWidget>();
		private readonly List<GuiWidget> stripedRows = new List<GuiWidget>();
		private readonly List<ThemedTextButton> selectableButtons = new List<ThemedTextButton>();
		private readonly List<RadioButton> radioButtons = new List<RadioButton>();
		private readonly List<GuiWidget> separators = new List<GuiWidget>();

		private readonly GuiWidget scope;
		private readonly GuiWidget grid;
		private readonly TextWidget progressText;
		private readonly TextWidget sliderText;
		private readonly GuiWidget imagePlaceholder;

		private int selectedChoice;
		private double scalar = 42;
		private bool syncing;

		/// <summary>The theme colours the current toggle switch was built with.</summary>
		private (Color, Color, Color) toggleColors;

		public WidgetGalleryWindow(DemoTheme demoTheme)
			: base(autoScroll: true)
		{
			this.demoTheme = demoTheme;
			this.theme = demoTheme.Theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.ScrollArea.HAnchor = HAnchor.Stretch;

			var outer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
			this.AddChild(outer);

			// The scope carries Visible / Interactive / Opacity for the whole grid, as GalleryScope does.
			this.scope = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Name = "Gallery Scope",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
			outer.AddChild(this.scope);

			this.grid = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Name = "Gallery Grid",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(16, 14),
			};
			this.scope.AddChild(this.grid);

			this.AddRow("Label", "label", this.Text("Welcome to the widget gallery!"));
			this.AddRow("Hyperlink", "Hyperlink", new Hyperlink("agg-sharp on GitHub", this.theme, RepoUrl));

			this.TextField = new ThemedTextEditWidget("", this.theme, pixelWidth: 180 * GuiWidget.DeviceScale, messageWhenEmptyAndNotSelected: "Write something here")
			{
				Name = "Gallery TextEdit",
				HAnchor = HAnchor.Stretch,
			};
			this.AddRow("TextEdit", "TextEdit", this.TextField);

			this.Button = new ThemedTextButton("Click me!", this.theme)
			{
				Name = "Gallery Button",
				Height = 28 * DeviceScale,
				Margin = 0,
			};
			this.Button.Click += (s, e) => this.ToggleBoolean();
			this.AddRow("Button", "button", this.Button);

			this.Link = new Hyperlink("Click me!", this.theme) { Name = "Gallery Link" };
			this.Link.Activated += (s, e) => this.ToggleBoolean();
			this.AddRow("Link", "link", this.Link);

			this.CheckBox = new CheckBox("Checkbox", this.theme.TextColor, DemoText.Points(DemoText.BodyPixels))
			{
				Name = "Gallery Checkbox",
			};
			this.AddRow("Checkbox", "checkbox", this.CheckBox);

			// agg-gui's RadioGroup stacks its options vertically.
			var radioColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
			for (int i = 0; i < Choices.Length; i++)
			{
				int index = i;
				var radio = new RadioButton(Choices[i], this.theme.TextColor, this.theme.DefaultFontSize)
				{
					Name = "Gallery Radio " + Choices[i],
					Checked = i == this.selectedChoice,
					Margin = new BorderDouble(0, 2),
				};
				radio.CheckedStateChanged += (s, e) =>
				{
					if (radio.Checked)
					{
						this.SetSelectedChoice(index);
					}
				};
				this.radioButtons.Add(radio);
				radioColumn.AddChild(radio);
			}

			this.AddRow("RadioButton", "radio", radioColumn);

			// The segmented control shares the radio's selection; the compact S/M/L strip has its own.
			// agg-sharp's SegmentedControl has no per-segment enable, so agg-gui's disabled "M" is not shown.
			var segmentedRow = new FlowLayoutWidget();
			this.SegmentedControl = new SegmentedControl(Choices, this.theme, this.selectedChoice) { Name = "Gallery Segmented" };
			this.SegmentedControl.SelectedIndexChanged += (s, e) => this.SetSelectedChoice(this.SegmentedControl.SelectedIndex);
			segmentedRow.AddChild(this.SegmentedControl);
			segmentedRow.AddChild(new SegmentedControl(new[] { "S", "M", "L" }, this.theme, 1)
			{
				Name = "Gallery Segmented Size",
				Margin = new BorderDouble(left: 12),
			});
			this.AddRow("SegmentedControl", "SegmentedControl", segmentedRow);

			var selectableRow = new FlowLayoutWidget();
			for (int i = 0; i < Choices.Length; i++)
			{
				int index = i;
				var button = new ThemedTextButton(Choices[i], this.theme, 10)
				{
					Name = "Gallery Selectable " + Choices[i],
					Height = 24 * DeviceScale,
					Padding = new BorderDouble(8, 0),
					Margin = new BorderDouble(right: 6),
				};
				button.Click += (s, e) => this.SetSelectedChoice(index);
				this.selectableButtons.Add(button);
				selectableRow.AddChild(button);
			}

			this.AddRow("SelectableLabel", "SelectableLabel", selectableRow);

			var comboRow = new FlowLayoutWidget();
			comboRow.AddChild(this.Text("Take your pick", VAnchor.Center, HAnchor.Absolute));
			this.ComboBox = new DropDownList("", this.theme.TextColor, pointSize: DemoText.Points(DemoText.BodyPixels))
			{
				Name = "Gallery ComboBox",
				Margin = new BorderDouble(left: 8),
			};
			this.ColorComboMenu();
			foreach (string choice in Choices)
			{
				this.ComboBox.AddItem(choice);
			}

			this.ComboBox.SelectedIndex = this.selectedChoice;
			this.ComboBox.SelectionChanged += (s, e) => this.SetSelectedChoice(this.ComboBox.SelectedIndex);
			comboRow.AddChild(this.ComboBox);
			this.AddRow("ComboBox", "ComboBox", comboRow);

			// agg-gui's slider shows its value beside the track; agg-sharp's draws only the track, so the value
			// is a label here.
			var sliderRow = new FlowLayoutWidget();
			this.Slider = new Slider(new Vector2(0, 0), 150 * DeviceScale, 0, 360)
			{
				Name = "Gallery Slider",
				VAnchor = VAnchor.Center,
			};
			this.Slider.Value = this.scalar;
			this.Slider.ValueChanged += (s, e) => this.SetScalar(Math.Round(this.Slider.Value));
			sliderRow.AddChild(this.Slider);
			this.sliderText = this.Text("", VAnchor.Center, HAnchor.Absolute);
			this.sliderText.Margin = new BorderDouble(left: 8);
			this.sliderText.AutoExpandBoundsToText = true;
			sliderRow.AddChild(this.sliderText);
			this.AddRow("Slider", "Slider", sliderRow);

			this.DragValue = new DragValue(this.scalar, 0, 360, this.theme) { Name = "Gallery DragValue", Decimals = 0 };
			this.DragValue.ValueChanged += (s, e) => this.SetScalar(this.DragValue.Value);
			this.AddRow("DragValue", "DragValue", this.DragValue);

			this.ProgressBar = new ProgressBar(200 * DeviceScale, 20 * DeviceScale)
			{
				Name = "Gallery ProgressBar",
				HAnchor = HAnchor.Stretch,
				BackgroundRadius = 10 * DeviceScale,
				ToolTipText = "The progress bar can be animated!",
			};
			this.progressText = new TextWidget("", pointSize: 9)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
				AutoExpandBoundsToText = true,
				Selectable = false,
			};
			this.texts.Add(this.progressText);
			this.ProgressBar.AddChild(this.progressText);
			this.AddRow("ProgressBar", "ProgressBar", this.ProgressBar);

			var spinnerRow = new FlowLayoutWidget();
			spinnerRow.AddChild(new Spinner(this.theme, SpinnerSize.Small) { Name = "Gallery Spinner Small", VAnchor = VAnchor.Center });
			spinnerRow.AddChild(new Spinner(this.theme) { Name = "Gallery Spinner", Margin = new BorderDouble(left: 12) });
			this.AddRow("Spinner", "Spinner", spinnerRow);

			// agg-gui's default: rgba(0.35, 0.55, 0.90, 0.50).
			this.ColorPicker = new ColorPicker(new ColorF(.35, .55, .90, .50).ToColor(), this.theme) { Name = "Gallery ColorPicker" };
			this.AddRow("Color picker", "ColorPicker", this.ColorPicker);

			// gallery.rs's "Color wheel" shares the colour picker's colour, so the row above follows the wheel
			// live, and Select with No Color ticked makes it transparent.
			this.ColorWheel = new ColorWheelPicker(this.ColorPicker.Color, this.theme, allowNone: true) { Name = "Gallery Color Wheel" };
			this.ColorWheel.ColorChanged += (s, e) =>
			{
				if (!this.ColorWheel.PassThrough)
				{
					this.ColorPicker.Color = this.ColorWheel.Color;
				}
			};
			this.ColorWheel.Selected += (s, e) => this.ColorPicker.Color = this.ColorWheel.Color;
			this.AddRow("Color wheel", "ColorWheelPicker", this.ColorWheel);

			// agg-gui's ImageView with no image paints a placeholder box of this height.
			this.imagePlaceholder = new GuiWidget
			{
				Name = "Gallery Image",
				HAnchor = HAnchor.Stretch,
				Height = 72 * DeviceScale,
				BackgroundRadius = 4 * DeviceScale,
				BackgroundOutlineWidth = 1,
			};
			this.imagePlaceholder.AddChild(this.Text("Image widget", VAnchor.Center, HAnchor.Center));
			this.AddRow("Image", "ImageWidget", this.imagePlaceholder);

			var imageButton = new ThemedTextButton("Image + text", this.theme)
			{
				Name = "Gallery Image Button",
				Height = 28 * DeviceScale,
				Margin = 0,
			};
			imageButton.Click += (s, e) => this.ToggleBoolean();
			this.AddRow("Button with image", "ImageButton", imageButton);

			this.AddRow("Separator", "separator", this.Separator(0));

			this.CollapsingHeader = new CollapsingHeader("Click to see what is hidden!", this.theme, expanded: false)
			{
				Name = "Gallery CollapsingHeader",
			};
			this.CollapsingHeader.Body.AddChild(this.Text("It's a custom toggle switch:"));
			this.AddRow("CollapsingHeader", "CollapsingHeader", this.CollapsingHeader);

			this.ToggleSwitch = this.CreateToggleSwitch();
			this.AddRow(new Hyperlink("Custom widget", this.theme, RepoUrl), this.ToggleSwitch);

			outer.AddChild(this.Separator(0));
			outer.AddChild(this.CreateControls());
			outer.AddChild(this.Separator(0));

			var footer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(16, 22, 16, 16),
			};
			footer.AddChild(new Hyperlink(RepoUrl, this.theme, RepoUrl) { HAnchor = HAnchor.Left, Margin = new BorderDouble(bottom: 4) });
			TextWidget hint = this.Text("Click widget names to search the agg-sharp source.");
			hint.PointSize = 8;
			footer.AddChild(hint);
			outer.AddChild(footer);

			this.SetScalar(this.scalar);
			this.SetSelectedChoice(this.selectedChoice);
			this.Recolor();

			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		public ThemedTextEditWidget TextField { get; }

		public ThemedTextButton Button { get; }

		public Hyperlink Link { get; }

		public CheckBox CheckBox { get; }

		public IReadOnlyList<RadioButton> RadioButtons => this.radioButtons;

		public SegmentedControl SegmentedControl { get; }

		public IReadOnlyList<ThemedTextButton> SelectableButtons => this.selectableButtons;

		public DropDownList ComboBox { get; }

		public Slider Slider { get; }

		public DragValue DragValue { get; }

		public ProgressBar ProgressBar { get; }

		public ColorPicker ColorPicker { get; }

		public ColorWheelPicker ColorWheel { get; }

		public CollapsingHeader CollapsingHeader { get; }

		/// <summary>The custom-widget row's switch. Replaced (keeping its state) when the theme changes.</summary>
		public CheckBox ToggleSwitch { get; private set; }

		public CheckBox VisibleCheckBox { get; private set; }

		public CheckBox InteractiveCheckBox { get; private set; }

		public DragValue OpacityValue { get; private set; }

		/// <summary>gallery.rs's shared selection: which of First/Second/Third is chosen.</summary>
		public int SelectedChoice => this.selectedChoice;

		/// <summary>gallery.rs's shared scalar, 0..360, shown by the slider, drag value and progress bar.</summary>
		public double Scalar => this.scalar;

		/// <summary>The grid's Visible / Interactive / Opacity scope, GalleryScope in gallery.rs.</summary>
		public GuiWidget Scope => this.scope;

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		/// <summary>Moves the shared selection and shows it in every widget bound to it.</summary>
		public void SetSelectedChoice(int index)
		{
			if (this.syncing || index < 0 || index >= Choices.Length)
			{
				return;
			}

			this.syncing = true;
			try
			{
				this.selectedChoice = index;
				for (int i = 0; i < this.radioButtons.Count; i++)
				{
					this.radioButtons[i].Checked = i == index;
				}

				this.SegmentedControl.SelectedIndex = index;
				this.ComboBox.SelectedIndex = index;
				this.ColorSelectableButtons();
			}
			finally
			{
				this.syncing = false;
			}
		}

		/// <summary>Moves the shared scalar (clamped to 0..360) and shows it in every widget bound to it.</summary>
		public void SetScalar(double value)
		{
			if (this.syncing)
			{
				return;
			}

			this.syncing = true;
			try
			{
				this.scalar = Math.Clamp(value, 0, 360);
				this.Slider.Value = this.scalar;
				this.DragValue.Value = this.scalar;
				this.sliderText.Text = this.scalar.ToString("0");
				this.ProgressBar.RatioComplete = this.scalar / 360;
				this.progressText.Text = $"{this.ProgressBar.PercentComplete}%";
			}
			finally
			{
				this.syncing = false;
			}
		}

		private void ToggleBoolean() => this.CheckBox.Checked = !this.CheckBox.Checked;

		/// <summary>A grid row: the doc link in a fixed left column, then the widget.</summary>
		private void AddRow(string title, string searchTerm, GuiWidget widget)
		{
			var docLink = new Hyperlink(title, this.theme, $"{RepoUrl}/search?q={Uri.EscapeDataString(searchTerm)}")
			{
				Name = "Gallery Doc " + title,
			};
			this.AddRow(docLink, widget);
		}

		private void AddRow(GuiWidget left, GuiWidget widget)
		{
			var row = new FlowLayoutWidget
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(0, 2),
				Padding = new BorderDouble(0, 2),
				BackgroundRadius = 2 * DeviceScale,
			};

			var labelCell = new GuiWidget
			{
				Width = LabelColumnWidth * DeviceScale,
				VAnchor = VAnchor.Fit | VAnchor.Top,
				Margin = new BorderDouble(right: ColumnGap),
			};
			left.HAnchor = HAnchor.Left;
			left.VAnchor = VAnchor.Top;
			labelCell.AddChild(left);
			row.AddChild(labelCell);

			var widgetCell = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
			if (widget.HAnchor != HAnchor.Stretch)
			{
				widget.HAnchor = HAnchor.Left;
			}

			widgetCell.AddChild(widget);
			row.AddChild(widgetCell);

			// egui's Grid::striped(true): every second row gets a faint band.
			if (this.grid.Children.Count % 2 == 1)
			{
				this.stripedRows.Add(row);
			}

			this.grid.AddChild(row);
		}

		/// <summary>egui's bottom bar: Visible, and while visible, Interactive and Opacity, each with its tooltip.</summary>
		private GuiWidget CreateControls()
		{
			var bar = new FlowLayoutWidget
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(16),
			};

			this.VisibleCheckBox = new CheckBox("Visible", this.theme.TextColor, DemoText.Points(DemoText.BodyPixels))
			{
				Name = "Gallery Visible",
				Checked = true,
				VAnchor = VAnchor.Center,
				ToolTipText = "Uncheck to hide all the widgets.",
			};
			bar.AddChild(this.VisibleCheckBox);

			var conditional = new FlowLayoutWidget { VAnchor = VAnchor.Center | VAnchor.Fit };
			bar.AddChild(conditional);

			this.InteractiveCheckBox = new CheckBox("Interactive", this.theme.TextColor, DemoText.Points(DemoText.BodyPixels))
			{
				Name = "Gallery Interactive",
				Checked = true,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 12),
				ToolTipText = "Uncheck to inspect how the widgets look when disabled.",
			};
			conditional.AddChild(this.InteractiveCheckBox);

			this.OpacityValue = new DragValue(1, 0, 1, this.theme)
			{
				Name = "Gallery Opacity",
				Speed = .01,
				Decimals = 2,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 12),
				ToolTipText = "Reduce this value to make widgets semi-transparent.",
			};
			conditional.AddChild(this.OpacityValue);
			TextWidget opacityLabel = this.Text("Opacity", VAnchor.Center, HAnchor.Absolute);
			opacityLabel.Margin = new BorderDouble(left: 6);
			opacityLabel.ToolTipText = this.OpacityValue.ToolTipText;
			conditional.AddChild(opacityLabel);

			this.VisibleCheckBox.CheckedStateChanged += (s, e) =>
			{
				bool visible = this.VisibleCheckBox.Checked;

				// agg-gui keeps the hidden grid's space so the window does not jump.
				this.scope.MinimumSize = visible ? Vector2.Zero : new Vector2(0, this.scope.Height);
				this.grid.Visible = visible;
				conditional.Visible = visible;
				this.ApplyScope();
			};
			this.InteractiveCheckBox.CheckedStateChanged += (s, e) => this.ApplyScope();
			this.OpacityValue.ValueChanged += (s, e) => this.ApplyScope();

			return bar;
		}

		/// <summary>
		/// GalleryScope: a non-interactive grid takes no input (Enabled off) and is dimmed to 40%; the opacity
		/// fades the grid as one image, which in agg-sharp is a backbuffer composited at an opacity - only
		/// turned on while there is something to fade, as agg-gui only asks for a layer then.
		/// </summary>
		private void ApplyScope()
		{
			bool interactive = this.InteractiveCheckBox.Checked;
			double alpha = Math.Clamp(this.OpacityValue.Value, 0, 1) * (interactive ? 1 : .4);
			this.grid.Enabled = interactive;
			bool fade = alpha < .999 && this.grid.Visible;
			this.scope.DoubleBuffer = fade;
			this.scope.BackbufferOpacity = fade ? alpha : 1;
		}

		/// <summary>agg-gui's ToggleSwitch: a pill switch, accent when on.</summary>
		private CheckBox CreateToggleSwitch()
		{
			this.toggleColors = this.ToggleColors();
			var toggle = new CheckBox(new ToggleSwitchView(
				"",
				"",
				40 * DeviceScale,
				20 * DeviceScale,
				this.demoTheme.Palette.WidgetBackground,
				this.theme.PrimaryAccentColor,
				Color.White,
				this.theme.TextColor,
				this.demoTheme.Palette.Separator))
			{
				Name = "Gallery ToggleSwitch",
			};
			return toggle;
		}

		private (Color, Color, Color) ToggleColors() => (this.demoTheme.Palette.WidgetBackground, this.theme.PrimaryAccentColor, this.theme.TextColor);

		private TextWidget Text(string text, VAnchor vAnchor = VAnchor.Absolute, HAnchor hAnchor = HAnchor.Left)
		{
			var widget = new TextWidget(text, pointSize: DemoText.Points(DemoText.BodyPixels), textColor: this.theme.TextColor)
			{
				HAnchor = hAnchor,
				VAnchor = vAnchor,
			};
			this.texts.Add(widget);
			return widget;
		}

		private GuiWidget Separator(double horizontalMargin)
		{
			var separator = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				Height = Math.Max(1, Math.Round(DeviceScale)),
				Margin = new BorderDouble(horizontalMargin, 6),
			};
			this.separators.Add(separator);
			return separator;
		}

		/// <summary>egui's SelectableLabel: only the chosen value has the accent surface.</summary>
		private void ColorSelectableButtons()
		{
			for (int i = 0; i < this.selectableButtons.Count; i++)
			{
				ThemedTextButton button = this.selectableButtons[i];
				bool selected = i == this.selectedChoice;
				button.BackgroundColor = selected ? this.theme.PrimaryAccentColor : Color.Transparent;
				button.TextColor = selected
					? this.theme.TextColor.WithContrast(this.theme.PrimaryAccentColor, 3).ToColor()
					: this.theme.TextColor;
				button.HoverColor = this.theme.SlightShade;
				button.MouseDownColor = this.theme.MinimalShade;
			}
		}

		private void ColorComboMenu()
		{
			// The field and chevron follow ThemeConfig.Current on their own; the text and popup rows are
			// copied into the list when it is built, so a theme change still has to push them.
			this.ComboBox.TextColor = this.theme.TextColor;
			this.ComboBox.MenuItemsBackgroundColor = this.theme.BackgroundColor;
			this.ComboBox.MenuItemsTextColor = this.theme.TextColor;
			this.ComboBox.MenuItemsBorderColor = this.demoTheme.Palette.WindowStroke;
			this.ComboBox.MenuItemsBackgroundHoverColor = this.theme.SlightShade;
			this.ComboBox.MenuItemsTextHoverColor = this.theme.TextColor;
		}

		private void OnThemeChanged(object sender, EventArgs e)
		{
			this.Recolor();
			this.Invalidate();
		}

		/// <summary>Pushes the current theme into the widgets that copied their colours when they were built.</summary>
		private void Recolor()
		{
			DemoPalette palette = this.demoTheme.Palette;
			foreach (TextWidget text in this.texts)
			{
				text.TextColor = this.theme.TextColor;
			}

			var stripe = new Color(palette.TextColor, (int)Math.Round(.05 * 255));
			foreach (GuiWidget row in this.stripedRows)
			{
				row.BackgroundColor = stripe;
			}

			foreach (GuiWidget separator in this.separators)
			{
				separator.BackgroundColor = palette.Separator;
			}

			foreach (RadioButton radio in this.radioButtons)
			{
				radio.TextColor = this.theme.TextColor;
			}

			this.CheckBox.TextColor = this.theme.TextColor;
			this.VisibleCheckBox.TextColor = this.theme.TextColor;
			this.InteractiveCheckBox.TextColor = this.theme.TextColor;

			foreach (ThemedTextButton button in new[] { this.Button, (ThemedTextButton)this.FindDescendant("Gallery Image Button") })
			{
				this.demoTheme.StyleButton(button);
			}

			this.ColorSelectableButtons();
			this.ColorComboMenu();

			this.TextField.ActualTextEditWidget.TextColor = this.theme.TextColor;
			this.TextField.BackgroundColor = palette.WidgetBackground;

			this.Slider.View.TextColor = this.theme.TextColor;

			this.ProgressBar.BackgroundColor = palette.WidgetBackground;
			this.ProgressBar.FillColor = this.theme.PrimaryAccentColor;
			this.ProgressBar.BorderColor = Color.Transparent;
			this.progressText.TextColor = this.theme.TextColor.WithContrast(this.theme.PrimaryAccentColor, 3).ToColor();

			this.imagePlaceholder.BackgroundColor = palette.WidgetBackground;
			this.imagePlaceholder.BorderColor = palette.Separator;

			// ToggleSwitchView bakes its colours into its state views (and only its CheckBox parent can pick the
			// visible state), so a theme change swaps in a new switch carrying the same state.
			if (this.ToggleSwitch.Parent is GuiWidget toggleCell && this.toggleColors != this.ToggleColors())
			{
				CheckBox old = this.ToggleSwitch;
				this.ToggleSwitch = this.CreateToggleSwitch();
				this.ToggleSwitch.Checked = old.Checked;
				toggleCell.ReplaceChild(old, this.ToggleSwitch);
			}
		}
	}
}
