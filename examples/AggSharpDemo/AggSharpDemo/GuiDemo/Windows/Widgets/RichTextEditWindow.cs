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
using System.Globalization;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.RichText;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The "RichTextEdit" window, a port of agg-gui's demo-ui/src/windows/rich_text_demo: agg-gui's two-row
	/// Font Awesome formatting toolbar over a <see cref="RichTextEdit"/> seeded with the same headings and lists.
	/// </summary>
	/// <remarks>
	/// Deviation: the family combo lists the text families this demo carries (Liberation Sans) rather than the
	/// system catalog. The size combo shows agg-gui's pixel sizes; <see cref="EditorSize"/> maps them to the
	/// editor's points, 16 being the body size.
	/// </remarks>
	public class RichTextEditWindow : FlowLayoutWidget
	{
		/// <summary>agg-gui's size dropdown (toolbar.rs FONT_SIZES), in its pixel sizes; 16 is the body size.</summary>
		public static readonly double[] FontSizes = { 8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32 };

		/// <summary>The families the family combo offers: the text faces this demo carries.</summary>
		public static readonly string[] FontFamilies = { "Liberation Sans" };

		private const string SourceUrl = "https://github.com/MatterHackers/agg-sharp/blob/main/examples/AggSharpDemo/AggSharpDemo/GuiDemo/Windows/Widgets/RichTextEditWindow.cs";

		// The editor's body size in points, standing for agg-gui's 16px default.
		private const double BodyPointSize = 12;
		private const double AggGuiBodySize = 16;

		// agg-gui's toolbar glyphs (toolbar.rs ICON_*).
		private const string IconBold = "";
		private const string IconItalic = "";
		private const string IconUnderline = "";
		private const string IconStrike = "";
		private const string IconAlignLeft = "";
		private const string IconAlignCenter = "";
		private const string IconAlignRight = "";
		private const string IconListOrdered = "";
		private const string IconListBullet = "";
		private const string IconOutdent = "";
		private const string IconIndent = "";
		private const string IconTextColor = "";
		private const string IconHighlight = "";
		private const string IconEraser = "";

		// agg-gui's starting colours when the selection has no colour of its own, or a mixed one.
		private static readonly Color DefaultTextColor = new Color(51, 115, 224);
		private static readonly Color DefaultHighlight = new Color(255, 235, 59);

		private readonly DemoTheme demoTheme;

		// Every toolbar button with its glyph, re-rendered in the right colour as the theme and on states change.
		private readonly List<(ThemedIconButton Button, string Glyph)> buttons = new List<(ThemedIconButton, string)>();

		// agg-gui's with_active_fn: each toggle's on test over the selection's CommonStyle, re-read on every change.
		private readonly List<(ThemedIconButton Button, Func<CommonStyle, bool> IsOn)> toggles = new List<(ThemedIconButton, Func<CommonStyle, bool>)>();

		private readonly ThemedIconButton textColorButton;
		private readonly ThemedIconButton highlightButton;

		// Set while the combos are moved to follow the selection, so following it does not format it.
		private bool reflecting;

		public RichTextEditWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.demoTheme = demoTheme;
			var theme = demoTheme.Theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(8);

			this.Editor = new RichTextEdit(SeedDoc(), BodyPointSize)
			{
				Name = "RichTextEdit Editor",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				MenuTheme = theme,
				Margin = new BorderDouble(top: 24),
			};

			// agg-gui's toolbar row 1: character formatting, family and size, colours.
			var row1 = this.ToolbarRow();
			this.Toggle(row1, IconBold, "Bold", RichCommand.ToggleBold, c => c.Bold == true);
			this.Toggle(row1, IconItalic, "Italic", RichCommand.ToggleItalic, c => c.Italic == true);
			this.Toggle(row1, IconUnderline, "Underline", RichCommand.ToggleUnderline, c => c.Underline == true);
			this.Toggle(row1, IconStrike, "Strikethrough", RichCommand.ToggleStrikethrough, c => c.Strikethrough == true);

			this.FamilyCombo = this.Combo(row1, "Font family", FontFamilies, 0, 180);
			this.FamilyCombo.SelectionChanged += (s, e) => this.FromCombo(this.FamilyCombo, () => RichCommand.SetFontFamily(FontFamilies[this.FamilyCombo.SelectedIndex]));
			this.SizeCombo = this.Combo(row1, "Font size", FontSizes.Select(f => f.ToString(CultureInfo.InvariantCulture)), Array.IndexOf(FontSizes, AggGuiBodySize), 64);
			this.SizeCombo.SelectionChanged += (s, e) => this.FromCombo(this.SizeCombo, () => RichCommand.SetFontSize(EditorSize(FontSizes[this.SizeCombo.SelectedIndex])));

			// agg-gui's colour buttons: each opens a floating colour dialog that previews on the selection.
			this.textColorButton = this.Tool(row1, IconTextColor, "Text color", () => this.OpenColorDialog(highlight: false), focusEditor: false);
			this.highlightButton = this.Tool(row1, IconHighlight, "Highlight color", () => this.OpenColorDialog(highlight: true), focusEditor: false);
			this.Tool(row1, IconEraser, "Remove highlight", () => this.Editor.Exec(RichCommand.SetHighlight(null)));
			this.AddChild(row1);

			// Row 2: paragraph formatting and history.
			var row2 = this.ToolbarRow();
			this.Toggle(row2, IconAlignLeft, "Align left", RichCommand.SetAlign(Justification.Left), c => c.Align == Justification.Left);
			this.Toggle(row2, IconAlignCenter, "Align center", RichCommand.SetAlign(Justification.Center), c => c.Align == Justification.Center);
			this.Toggle(row2, IconAlignRight, "Align right", RichCommand.SetAlign(Justification.Right), c => c.Align == Justification.Right);
			this.Toggle(row2, IconListOrdered, "Numbered list", RichCommand.SetList(ListKind.Ordered), c => c.List == ListKind.Ordered);
			this.Toggle(row2, IconListBullet, "Bulleted list", RichCommand.SetList(ListKind.Bullet), c => c.List == ListKind.Bullet);
			this.Tool(row2, IconOutdent, "Decrease indent", () => this.Editor.Exec(RichCommand.Outdent));
			this.Tool(row2, IconIndent, "Increase indent", () => this.Editor.Exec(RichCommand.Indent));
			string modifier = OperatingSystem.IsMacOS() ? "Cmd" : "Ctrl";
			this.UndoButton = this.Tool(row2, IconFont.Undo, $"Undo ({modifier}+Z)", this.Editor.Undo);
			this.RedoButton = this.Tool(row2, IconFont.Redo, $"Redo ({modifier}+Y)", this.Editor.Redo);
			this.AddChild(row2);

			this.AddChild(this.Editor);

			// agg-gui's source_link sits under the content, at the right.
			this.AddChild(new Hyperlink("(source code)", theme, SourceUrl)
			{
				Name = "RichTextEdit Source Link",
				HAnchor = HAnchor.Right,
				Margin = new BorderDouble(top: 5),
			});

			this.Recolor();
			this.Editor.Changed += (s, e) => this.RefreshToggles();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		public RichTextEdit Editor { get; }

		/// <summary>The font family dropdown; picking a family sets it on the selection.</summary>
		public DropDownList FamilyCombo { get; }

		/// <summary>The font size dropdown, in agg-gui's pixel sizes; picking a size sets it on the selection.</summary>
		public DropDownList SizeCombo { get; }

		/// <summary>The Undo button, greyed out when there is nothing to undo.</summary>
		public ThemedIconButton UndoButton { get; }

		/// <summary>The Redo button, greyed out when there is nothing to redo.</summary>
		public ThemedIconButton RedoButton { get; }

		/// <summary>Gets the open colour dialog, or null when none is open.</summary>
		public ColorDialog ColorDialog { get; private set; }

		/// <summary>The editor's point size for agg-gui pixel size <paramref name="aggGuiSize"/>, 16 being the body size.</summary>
		public static double EditorSize(double aggGuiSize) => aggGuiSize * BodyPointSize / AggGuiBodySize;

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
			static Block Heading(string text) => new Block(new TextRun(text, InlineStyle.Default with { FontSize = EditorSize(24) }));
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

		// agg-gui's FlexRow with_gap(4), rows 6 apart.
		private FlowLayoutWidget ToolbarRow() => new FlowLayoutWidget() { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Fit, Margin = new BorderDouble(0, 3) };

		// A Font Awesome toolbar button named after agg-gui's tooltip, so tests and the Inspector find it by what it does.
		private ThemedIconButton Tool(FlowLayoutWidget row, string glyph, string tooltip, Action action, bool focusEditor = true)
		{
			var theme = this.demoTheme.Theme;
			var button = new ThemedIconButton(this.RenderGlyph(glyph, theme.TextColor), theme)
			{
				Name = "RichTextEdit " + tooltip,
				ToolTipText = tooltip,
				Width = 40 * DeviceScale,
				Height = 22 * DeviceScale,
				Margin = new BorderDouble(right: 4),
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
			this.buttons.Add((button, glyph));
			row.AddChild(button);
			return button;
		}

		// agg-gui's ComboBox: a fixed-width dropdown of the labels, starting at selectedIndex.
		private DropDownList Combo(FlowLayoutWidget row, string tooltip, IEnumerable<string> labels, int selectedIndex, double designWidth)
		{
			var combo = new DropDownList(tooltip, this.demoTheme.Theme.TextColor, pointSize: this.demoTheme.Theme.DefaultFontSize * 10 / 12)
			{
				Name = "RichTextEdit " + tooltip,
				ToolTipText = tooltip,
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Center,
				Width = designWidth * DeviceScale,
				Margin = new BorderDouble(right: 4),
			};
			foreach (string label in labels)
			{
				combo.AddItem(label);
			}

			combo.SelectedIndex = selectedIndex;
			row.AddChild(combo);
			return combo;
		}

		// A combo pick formats the selection, unless the combo is only following the selection or was cleared.
		private void FromCombo(DropDownList combo, Func<RichCommand> command)
		{
			if (!this.reflecting && combo.SelectedIndex >= 0)
			{
				this.Editor.Exec(command());
				this.Editor.Focus();
			}
		}

		private ImageBuffer RenderGlyph(string glyph, Color color)
		{
			int pixels = (int)Math.Round(this.demoTheme.Theme.DefaultFontSize * 11 / 12 * 96 / 72 * DeviceScale);
			return GlyphIcon.Render(glyph, IconFont.TypeFace, color, pixels);
		}

		/// <summary>
		/// Whether the toolbar button named for <paramref name="tooltip"/> shows its on state - agg-gui's active
		/// accent fill: a toggle is on when every selected run (or paragraph, for alignment and lists) agrees.
		/// </summary>
		public bool IsToolOn(string tooltip) => this.toggles.Any(t => t.Button.ToolTipText == tooltip && t.IsOn(this.Editor.Core.CommonStyleOfSelection()));

		// A toolbar button that also shows an on state, like agg-gui's with_active_fn toggles.
		private void Toggle(FlowLayoutWidget row, string glyph, string tooltip, RichCommand command, Func<CommonStyle, bool> isOn)
		{
			var button = this.Tool(row, glyph, tooltip, () => this.Editor.Exec(command));
			this.toggles.Add((button, isOn));
		}

		private void RefreshToggles()
		{
			var common = this.Editor.Core.CommonStyleOfSelection();
			var theme = this.demoTheme.Theme;
			var accent = DemoTheme.ColorOf(this.demoTheme.Accent);
			foreach (var (button, glyph) in this.buttons)
			{
				// The colour buttons wear the accent like agg-gui's; a toggle does while it is on.
				bool on = button == this.textColorButton || button == this.highlightButton || this.toggles.Any(t => t.Button == button && t.IsOn(common));
				button.BackgroundColor = on ? accent : theme.ButtonBackgroundColor;
				button.SetIcon(this.RenderGlyph(glyph, on ? Color.White : theme.TextColor));
			}

			// agg-gui's enabled_fn: undo and redo grey out when there is nothing to take back or bring back.
			this.UndoButton.Enabled = this.Editor.Core.CanUndo;
			this.RedoButton.Enabled = this.Editor.Core.CanRedo;

			// agg-gui's family combo follows a selection that agrees on a family (a mixed one leaves it as it is);
			// its size combo keeps the last size picked.
			if (common.FontFamilyAgrees)
			{
				this.reflecting = true;
				this.FamilyCombo.SelectedIndex = Math.Max(0, Array.IndexOf(FontFamilies, common.FontFamily ?? FontFamilies[0]));
				this.reflecting = false;
			}
		}

		/// <summary>
		/// The selection's text colour (the editor's default when it has none) or its highlight; clear when the
		/// selection is mixed or has no highlight.
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

		private void OnThemeChanged(object sender, EventArgs e) => this.Recolor();

		private void Recolor()
		{
			DemoPalette palette = this.demoTheme.Palette;
			this.Editor.BackgroundColor = palette.WidgetBackground;
			this.Editor.TextColor = palette.TextColor;
			this.Editor.CaretColor = palette.TextColor;
			this.Editor.SelectionColor = new Color(DemoTheme.ColorOf(this.demoTheme.Accent), 90);
			this.Editor.Invalidate();
			this.FamilyCombo.TextColor = palette.TextColor;
			this.SizeCombo.TextColor = palette.TextColor;
			this.RefreshToggles();
		}
	}
}
