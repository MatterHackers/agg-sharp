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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.RichText;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The "RichTextEdit" window, a port of agg-gui's demo-ui/src/windows/rich_text_demo: agg-gui's two-row
	/// formatting toolbar over a <see cref="RichTextEdit"/> seeded with the same headings and lists.
	/// </summary>
	/// <remarks>
	/// Deviation: the font-family picker is left out (agg-sharp bundles one family).
	/// </remarks>
	public class RichTextEditWindow : FlowLayoutWidget
	{
		private const string SourceUrl = "https://github.com/MatterHackers/agg-sharp/blob/main/examples/AggSharpDemo/AggSharpDemo/GuiDemo/Windows/Widgets/RichTextEditWindow.cs";

		private static readonly double[] FontSizes = { 10, 12, 14, 18, 24, 32 };

		private readonly DemoTheme demoTheme;
		private readonly List<ThemedTextButton> buttons = new List<ThemedTextButton>();

		// agg-gui's with_active_fn: each toggle's on test over the selection's CommonStyle, re-read on every change.
		private readonly List<(ThemedTextButton Button, Func<CommonStyle, bool> IsOn)> toggles = new List<(ThemedTextButton, Func<CommonStyle, bool>)>();
		// agg-gui's starting colours when the selection has no colour of its own, or a mixed one.
		private static readonly Color DefaultTextColor = new Color(51, 115, 224);
		private static readonly Color DefaultHighlight = new Color(255, 235, 59);

		private int sizeIndex = 1;
		private ThemedTextButton textColorButton;
		private ThemedTextButton highlightButton;

		public RichTextEditWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.demoTheme = demoTheme;
			var theme = demoTheme.Theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(8);

			this.AddChild(new Hyperlink("(source code)", theme, SourceUrl)
			{
				Name = "RichTextEdit Source Link",
				HAnchor = HAnchor.Center,
				Margin = new BorderDouble(0, 5),
			});

			this.Editor = new RichTextEdit(SeedDoc(), 12)
			{
				Name = "RichTextEdit Editor",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				MenuTheme = theme,
			};

			// agg-gui's toolbar row 1: character formatting.
			var row1 = this.ToolbarRow();
			this.Toggle(row1, "B", "Bold", RichCommand.ToggleBold, c => c.Bold == true);
			this.Toggle(row1, "I", "Italic", RichCommand.ToggleItalic, c => c.Italic == true);
			this.Toggle(row1, "U", "Underline", RichCommand.ToggleUnderline, c => c.Underline == true);
			this.Toggle(row1, "S", "Strikethrough", RichCommand.ToggleStrikethrough, c => c.Strikethrough == true);
			this.Tool(row1, "A-", "Smaller", () => this.StepSize(-1));
			this.Tool(row1, "A+", "Larger", () => this.StepSize(1));

			// agg-gui's colour swatch buttons: each opens a floating colour dialog that previews on the selection.
			this.textColorButton = this.SwatchTool(row1, "Color", "Text color", highlight: false);
			this.highlightButton = this.SwatchTool(row1, "Mark", "Highlight color", highlight: true);
			this.Tool(row1, "Unmark", "Remove highlight", () => this.Editor.Exec(RichCommand.SetHighlight(null)));
			this.AddChild(row1);

			// Row 2: paragraph formatting and history.
			var row2 = this.ToolbarRow();
			this.Toggle(row2, "Left", "Align left", RichCommand.SetAlign(Justification.Left), c => c.Align == Justification.Left);
			this.Toggle(row2, "Center", "Align center", RichCommand.SetAlign(Justification.Center), c => c.Align == Justification.Center);
			this.Toggle(row2, "Right", "Align right", RichCommand.SetAlign(Justification.Right), c => c.Align == Justification.Right);
			this.Toggle(row2, "1.", "Numbered list", RichCommand.SetList(ListKind.Ordered), c => c.List == ListKind.Ordered);
			this.Toggle(row2, "•", "Bulleted list", RichCommand.SetList(ListKind.Bullet), c => c.List == ListKind.Bullet);
			this.Tool(row2, "Outdent", "Decrease indent", () => this.Editor.Exec(RichCommand.Outdent));
			this.Tool(row2, "Indent", "Increase indent", () => this.Editor.Exec(RichCommand.Indent));
			string modifier = OperatingSystem.IsMacOS() ? "Cmd" : "Ctrl";
			this.Tool(row2, "Undo", $"Undo ({modifier}+Z)", this.Editor.Undo);
			this.Tool(row2, "Redo", $"Redo ({modifier}+Y)", this.Editor.Redo);
			this.AddChild(row2);

			this.AddChild(this.Editor);
			this.Recolor();
			this.Editor.Changed += (s, e) => this.RefreshToggles();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		public RichTextEdit Editor { get; }

		/// <summary>Gets the open colour dialog, or null when none is open.</summary>
		public ColorDialog ColorDialog { get; private set; }

		/// <summary>
		/// Opens agg-gui's colour dialog for the text colour or the highlight: a colour wheel in a modal window,
		/// seeded with the selection's colour, that recolours the selection live. Select keeps the colour as one
		/// undo step; Cancel, × or Escape put the text back; a press outside keeps a change (one undo step) and
		/// leaves no trace otherwise.
		/// </summary>
		public ColorDialog OpenColorDialog(bool highlight)
		{
			this.ColorDialog?.CloseDialog();
			var theme = this.demoTheme.Theme;
			var core = this.Editor.Core;
			RichCommand Command(Color c) => highlight ? RichCommand.SetHighlight(c) : RichCommand.SetTextColor(c);

			// Undo feeding pauses for the whole dialog, so the drag frames collapse into one step (or none).
			core.BeginPreview();
			var common = core.CommonStyleOfSelection();
			Color? current = highlight ? (common.HighlightAgrees ? common.Highlight : null) : (common.TextColorAgrees ? common.TextColor : null);
			Color initial = current ?? (highlight ? DefaultHighlight : DefaultTextColor);

			// agg-gui leaves "No Color" off here: Remove highlight clears a highlight, and a text run always has a colour.
			var picker = new ColorWheelPicker(initial, theme) { Name = "RichTextEdit Color Picker" };

			// Subscribed before the dialog is built, so these run before the dialog closes itself on Select.
			bool selected = false;
			picker.ColorChanged += (s, e) => core.Exec(Command(picker.Color));
			picker.Selected += (s, e) =>
			{
				core.Exec(Command(picker.Color));
				selected = true;
			};

			var dialog = new ColorDialog(picker, theme, highlight ? "Highlight color" : "Text color")
			{
				Name = "RichTextEdit Color Dialog",
				CancelOnOutsidePress = false,
			};

			// Every way the dialog ends lands here exactly once: Select commits; a press outside commits a change
			// (and closes silently otherwise); Cancel, × and Escape put the text back.
			dialog.Closed += (s, e) =>
			{
				if (selected || (dialog.ClosedByOutsidePress && core.IsPreviewDirty))
				{
					core.CommitPreview();
				}
				else
				{
					core.CancelPreview();
				}

				if (this.ColorDialog == dialog)
				{
					this.ColorDialog = null;
				}

				this.RefreshToggles();
				this.Editor.Focus();
			};

			this.ColorDialog = dialog;
			this.ShowModal(dialog);
			return dialog;
		}

		/// <summary>
		/// Pushes <paramref name="dialog"/> on a see-through <see cref="ModalOverlay"/> over the SystemWindow - agg-gui's
		/// modal window, which grabs input without dimming what is beneath - and takes the overlay down with it.
		/// </summary>
		private void ShowModal(GuiWidget dialog)
		{
			var overlay = new ModalOverlay { Name = "RichTextEdit Color Overlay", BackgroundColor = Color.Transparent };
			overlay.LayersChanged += (s, e) =>
			{
				if (overlay.Layers.Count == 0)
				{
					overlay.Parent?.RemoveChild(overlay);
				}
			};

			GuiWidget host = this.Parents<SystemWindow>().FirstOrDefault() ?? this.TopmostParent();
			host.AddChild(overlay);
			overlay.Push(dialog);
		}

		/// <summary>agg-gui's seed document: two headings, a numbered list and a bullet.</summary>
		public static RichDoc SeedDoc()
		{
			static Block Heading(string text) => new Block(new TextRun(text, InlineStyle.Default with { Bold = true, FontSize = 24 }));
			static Block Item(string text, ListKind list)
			{
				var block = Block.Plain(text);
				block.List = list;
				return block;
			}

			return new RichDoc(new[]
			{
				Heading("Toolbar"),
				Item("Toggle bold, italic, underline and strikethrough.", ListKind.Ordered),
				Item("Choose a font family and size.", ListKind.Ordered),
				Item("Set the text colour or a highlight.", ListKind.Ordered),
				Heading("Links"),
				Item("Select some text and format it with the toolbar above.", ListKind.Bullet),
			});
		}

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window, and an open colour dialog hangs off the SystemWindow, not off this.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			this.ColorDialog?.CloseDialog();
			base.OnClosed(e);
		}

		private FlowLayoutWidget ToolbarRow() => new FlowLayoutWidget() { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Fit, Margin = new BorderDouble(0, 2) };

		// A toolbar button named after agg-gui's tooltip, so tests and the Inspector find it by what it does.
		private void Tool(FlowLayoutWidget row, string label, string tooltip, Action action, bool focusEditor = true)
		{
			var button = new ThemedTextButton(label, this.demoTheme.Theme)
			{
				Name = "RichTextEdit " + tooltip,
				ToolTipText = tooltip,
				Margin = new BorderDouble(right: 3),
			};
			button.Click += (s, e) =>
			{
				action();

				// The editor keeps the keyboard, so typing carries on after a toolbar click.
				if (focusEditor)
				{
					this.Editor.Focus();
				}
			};
			this.buttons.Add(button);
			row.AddChild(button);
		}

		/// <summary>
		/// Whether the toolbar button named for <paramref name="tooltip"/> shows its on state - agg-gui's active
		/// accent fill: a toggle is on when every selected run (or paragraph, for alignment and lists) agrees.
		/// </summary>
		public bool IsToolOn(string tooltip) => this.toggles.Any(t => t.Button.ToolTipText == tooltip && t.IsOn(this.Editor.Core.CommonStyleOfSelection()));

		// A toolbar button that also shows an on state, like agg-gui's with_active_fn toggles.
		private void Toggle(FlowLayoutWidget row, string label, string tooltip, RichCommand command, Func<CommonStyle, bool> isOn)
		{
			this.Tool(row, label, tooltip, () => this.Editor.Exec(command));
			this.toggles.Add((this.buttons[^1], isOn));
		}

		private void RefreshToggles()
		{
			this.textColorButton?.Invalidate();
			this.highlightButton?.Invalidate();
			var common = this.Editor.Core.CommonStyleOfSelection();
			var theme = this.demoTheme.Theme;
			var accent = DemoTheme.ColorOf(this.demoTheme.Accent);
			foreach (var (button, isOn) in this.toggles)
			{
				bool on = isOn(common);
				button.BackgroundColor = on ? accent : theme.ButtonBackgroundColor;
				button.TextColor = on ? Color.White : theme.TextColor;
			}
		}

		// A toolbar button with a strip of the selection's colour, opening the colour dialog.
		private ThemedTextButton SwatchTool(FlowLayoutWidget row, string label, string tooltip, bool highlight)
		{
			this.Tool(row, label, tooltip, () => this.OpenColorDialog(highlight), focusEditor: false);
			var button = this.buttons[^1];
			button.AfterDraw += (s, e) => e.Graphics2D.FillRectangle(4, 2, button.Width - 4, 5, this.SwatchColor(highlight));
			return button;
		}

		/// <summary>
		/// The colour a swatch strip shows: the selection's text colour (the editor's default when it has none) or
		/// its highlight; clear when the selection is mixed or has no highlight.
		/// </summary>
		public Color SwatchColor(bool highlight)
		{
			var common = this.Editor.Core.CommonStyleOfSelection();
			if (highlight)
			{
				return common.HighlightAgrees ? common.Highlight ?? Color.Transparent : Color.Transparent;
			}

			return common.TextColorAgrees ? common.TextColor ?? this.Editor.TextColor : Color.Transparent;
		}

		private void StepSize(int direction)
		{
			this.sizeIndex = Math.Clamp(this.sizeIndex + direction, 0, FontSizes.Length - 1);
			this.Editor.Exec(RichCommand.SetFontSize(FontSizes[this.sizeIndex]));
		}

		private void OnThemeChanged(object sender, EventArgs e) => this.Recolor();

		private void Recolor()
		{
			DemoPalette palette = this.demoTheme.Palette;
			this.Editor.BackgroundColor = palette.WidgetBackground;
			this.Editor.TextColor = palette.TextColor;
			this.Editor.CaretColor = palette.TextColor;
			this.Editor.SelectionColor = new Color(DemoTheme.ColorOf(this.demoTheme.Accent), 90);
			this.Editor.Invalidate();
			this.RefreshToggles();
		}
	}
}
