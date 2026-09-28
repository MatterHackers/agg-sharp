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

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The "Modals" window, a port of agg-gui's modals_demo (demo-ui/src/windows/text_demos/dialogs.rs, itself
	/// egui's modals demo): two buttons opening an Edit User modal and a Save modal. Save stacks a confirmation
	/// over the user modal, and confirming stacks a progress modal that closes them all when it fills. The
	/// modals live in a <see cref="ModalOverlay"/> over the whole SystemWindow, so they block the rest of the UI.
	/// </summary>
	public class ModalsWindow : FlowLayoutWidget
	{
		public const string SourceUrl = "https://github.com/larsbrubaker/agg-gui/blob/main/demo-ui/src/windows/text_demos/dialogs.rs";

		/// <summary>agg-gui's progress step, taken once per painted frame (there and here).</summary>
		public const double ProgressStep = 0.025;

		private static readonly string[] Roles = { "user", "admin" };

		private readonly DemoTheme demoTheme;
		private readonly ThemeConfig theme;
		private readonly List<TextWidget> texts = new List<TextWidget>();
		private readonly List<WrappedTextWidget> wrappedTexts = new List<WrappedTextWidget>();

		// What an open modal copied from the theme when it was built, and how to put the current theme back in.
		// Keyed by widget so a closed modal's entries can be dropped.
		private readonly List<(GuiWidget Widget, Action Recolor)> modalColors = new List<(GuiWidget, Action)>();

		public ModalsWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.demoTheme = demoTheme;
			this.theme = demoTheme.Theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(14);

			this.AddChild(this.Label("Modals demo"));

			var row = new FlowLayoutWidget { HAnchor = HAnchor.Left | HAnchor.Fit, VAnchor = VAnchor.Fit, Margin = new BorderDouble(0, 6) };
			this.OpenUserButton = this.Button("Open User Modal", "Modals Open User", this.OpenUserModal);
			this.OpenUserButton.Margin = new BorderDouble(right: 8);
			row.AddChild(this.OpenUserButton);
			this.OpenSaveButton = this.Button("Open Save Modal", "Modals Open Save", this.OpenSaveModal);
			row.AddChild(this.OpenSaveButton);
			this.AddChild(row);

			this.AddChild(this.Wrapped("Click one of the buttons to open a modal."));
			this.AddChild(this.Wrapped("Modals have a backdrop and prevent interaction with the rest of the UI."));
			this.AddChild(this.Wrapped("You can show modals on top of each other and close the topmost modal with escape or by clicking outside the modal."));

			this.AddChild(new Hyperlink("(source code)", this.theme, SourceUrl)
			{
				Name = "Modals Source Link",
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(0, 0, 0, 8),
			});

			this.Overlay = new ModalOverlay { Name = "Modals Overlay" };
			this.Overlay.LayersChanged += this.Overlay_LayersChanged;

			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		/// <summary>The modal stack. Hung on the SystemWindow while a modal is open.</summary>
		public ModalOverlay Overlay { get; }

		public ThemedTextButton OpenUserButton { get; }

		public ThemedTextButton OpenSaveButton { get; }

		/// <summary>The Edit User name, kept across openings as agg-gui's text cell is.</summary>
		public string UserName { get; private set; } = "";

		/// <summary>The Edit User role index into user/admin.</summary>
		public int RoleIndex { get; private set; }

		/// <summary>How far the save has got, 0..1, or null when no progress modal is open.</summary>
		public double? SaveProgress { get; private set; }

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window, and the modals hang off the SystemWindow, not off this.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			this.SaveProgress = null;
			this.Overlay.CloseAll();
			this.Overlay.Close();
			base.OnClosed(e);
		}

		public void OpenUserModal()
		{
			GuiWidget dialog = this.Dialog("Modals User Dialog", 250, 142);
			dialog.AddChild(this.Label("Edit User"));

			var nameField = new ThemedTextEditWidget(this.UserName, this.theme, pixelWidth: 170 * GuiWidget.DeviceScale)
			{
				Name = "Modals Name",
				VAnchor = VAnchor.Center,
			};
			this.KeepColored(nameField, () => nameField.ActualTextEditWidget.TextColor = this.theme.TextColor);
			nameField.ActualTextEditWidget.TextChanged += (s, e) => this.UserName = nameField.Text;
			dialog.AddChild(this.FieldRow("Name:", nameField));

			var roleList = new DropDownList("", this.theme.TextColor, pointSize: this.theme.DefaultFontSize)
			{
				Name = "Modals Role",
				VAnchor = VAnchor.Center,
			};
			foreach (string role in Roles)
			{
				roleList.AddItem(role);
			}

			this.KeepColored(roleList, () => roleList.TextColor = this.theme.TextColor);
			roleList.SelectedIndex = this.RoleIndex;
			roleList.SelectionChanged += (s, e) => this.RoleIndex = roleList.SelectedIndex;
			dialog.AddChild(this.FieldRow("Role:", roleList));

			dialog.AddChild(this.ButtonRow(
				this.Button("Save", "Modals User Save", this.OpenSaveModal),
				this.Button("Cancel", "Modals User Cancel", () => this.Overlay.CloseTop())));
			this.Show(dialog);
		}

		public void OpenSaveModal()
		{
			GuiWidget dialog = this.Dialog("Modals Save Dialog", 220, 112);
			dialog.AddChild(this.Label("Save? Are you sure?"));
			dialog.AddChild(this.Wrapped("This opens a progress modal."));
			dialog.AddChild(this.ButtonRow(
				this.Button("Yes Please", "Modals Yes Please", this.OpenProgressModal),
				this.Button("No Thanks", "Modals No Thanks", () => this.Overlay.CloseTop())));
			this.Show(dialog);
		}

		private void OpenProgressModal()
		{
			GuiWidget dialog = this.Dialog("Modals Progress Dialog", 120, 82);
			dialog.AddChild(this.Label("Saving..."));
			var bar = new ProgressBar(96 * DeviceScale, 14 * DeviceScale)
			{
				Name = "Modals Progress Bar",
				BorderColor = Color.Transparent,
				Margin = new BorderDouble(0, 8),
			};
			this.KeepColored(bar, () =>
			{
				bar.FillColor = this.theme.PrimaryAccentColor;
				bar.BackgroundColor = this.demoTheme.Palette.WidgetBackground;
			});
			dialog.AddChild(bar);

			// agg-gui advances the save once per painted frame, so it runs at the display's pace and stalls
			// with it; each paint of the bar takes one step and asks for the next frame.
			bar.AfterDraw += (s, e) => this.StepProgress(bar);
			this.SaveProgress = 0;
			this.Show(dialog);
		}

		/// <summary>One frame of agg-gui's save: the bar fills, and when it is full every modal closes.</summary>
		private void StepProgress(ProgressBar bar)
		{
			if (this.SaveProgress == null)
			{
				return;
			}

			double progress = Math.Min(1, this.SaveProgress.Value + ProgressStep);
			this.SaveProgress = progress;
			bar.RatioComplete = progress;

			// Both after this paint: invalidating from inside it can be swallowed when the frame finishes, and
			// the modals cannot be torn down while one of them is drawing.
			if (progress >= 1)
			{
				UiThread.RunOnIdle(() => this.Overlay.CloseAll());
			}
			else
			{
				UiThread.RunOnIdle(bar.Invalidate);
			}
		}

		private void Overlay_LayersChanged(object sender, EventArgs e)
		{
			// Escape or a press outside may close the progress modal before it fills.
			if (!this.Overlay.Layers.Any(layer => layer.Name == "Modals Progress Dialog"))
			{
				this.SaveProgress = null;
			}

			if (this.Overlay.Layers.Count == 0)
			{
				this.Overlay.Parent?.RemoveChild(this.Overlay);
			}
		}

		/// <summary>Hangs the overlay on the SystemWindow (covering every demo window and the shell) and pushes
		/// <paramref name="dialog"/>.</summary>
		private void Show(GuiWidget dialog)
		{
			if (this.Overlay.Parent == null)
			{
				GuiWidget host = this.Parents<SystemWindow>().FirstOrDefault() ?? (GuiWidget)this;
				host.AddChild(this.Overlay);
			}

			this.Overlay.Push(dialog);
		}

		/// <summary>agg-gui's modal panel: window fill, a stroke, rounded 8, at its fixed size.</summary>
		private GuiWidget Dialog(string name, double width, double height)
		{
			var dialog = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Name = name,

				// Not Fit (a FlowLayoutWidget's default), or the children would shrink it below agg-gui's size.
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
				Width = width * DeviceScale,
				Height = height * DeviceScale,
				Padding = new BorderDouble(12, 10),
				BackgroundOutlineWidth = 1,
				BackgroundRadius = 8 * DeviceScale,
			};
			this.KeepColored(dialog, () =>
			{
				dialog.BackgroundColor = this.demoTheme.Palette.WindowFill;
				dialog.BorderColor = this.demoTheme.Palette.WidgetStroke;
			});
			return dialog;
		}

		/// <summary>Colours <paramref name="widget"/> now and again on every theme change while it is open.</summary>
		private void KeepColored(GuiWidget widget, Action recolor)
		{
			recolor();
			this.modalColors.Add((widget, recolor));
		}

		private GuiWidget FieldRow(string label, GuiWidget field)
		{
			var row = new FlowLayoutWidget { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Fit, Margin = new BorderDouble(0, 2) };
			TextWidget text = this.Label(label);
			text.VAnchor = VAnchor.Center;
			text.HAnchor = HAnchor.Absolute;
			text.Width = 42 * DeviceScale;
			this.texts.Remove(text);
			this.KeepColored(text, () => text.TextColor = this.demoTheme.Palette.TextDim);
			row.AddChild(text);
			row.AddChild(field);
			return row;
		}

		/// <summary>The dialog's buttons, right aligned at its foot.</summary>
		private GuiWidget ButtonRow(ThemedTextButton first, ThemedTextButton second)
		{
			// A stretching spacer above pushes the row to the dialog's foot.
			var column = new FlowLayoutWidget(FlowDirection.TopToBottom) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
			column.AddChild(new GuiWidget { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch, Height = 1, MinimumSize = new VectorMath.Vector2(0, 1) });
			var row = new FlowLayoutWidget { HAnchor = HAnchor.Right | HAnchor.Fit, VAnchor = VAnchor.Fit };
			first.Margin = new BorderDouble(right: 6);
			second.Margin = 0;
			row.AddChild(first);
			row.AddChild(second);
			column.AddChild(row);
			return column;
		}

		private ThemedTextButton Button(string text, string name, Action click)
		{
			var button = new ThemedTextButton(text, this.theme) { Name = name, VAnchor = VAnchor.Center };
			button.Click += (s, e) => click();
			return button;
		}

		private TextWidget Label(string text)
		{
			var widget = new TextWidget(text, pointSize: this.theme.DefaultFontSize, textColor: this.theme.TextColor)
			{
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(0, 2),
				AutoExpandBoundsToText = true,
			};
			this.texts.Add(widget);
			return widget;
		}

		private WrappedTextWidget Wrapped(string text)
		{
			var widget = new WrappedTextWidget(text, this.theme.DefaultFontSize, textColor: this.theme.TextColor)
			{
				Margin = new BorderDouble(0, 2),
			};
			this.wrappedTexts.Add(widget);
			return widget;
		}

		private void OnThemeChanged(object sender, EventArgs e)
		{
			this.Recolor();
			this.Invalidate();
		}

		/// <summary>Pushes the current theme into the window's widgets and those of any open modal.</summary>
		private void Recolor()
		{
			this.BackgroundColor = this.demoTheme.Palette.PanelFill;
			this.texts.RemoveAll(t => t.HasBeenClosed);
			foreach (TextWidget text in this.texts)
			{
				text.TextColor = this.theme.TextColor;
			}

			this.wrappedTexts.RemoveAll(t => t.HasBeenClosed);
			foreach (WrappedTextWidget text in this.wrappedTexts)
			{
				text.TextColor = this.theme.TextColor;
			}

			this.modalColors.RemoveAll(entry => entry.Widget.HasBeenClosed);
			foreach ((GuiWidget _, Action recolor) in this.modalColors)
			{
				recolor();
			}
		}
	}
}
