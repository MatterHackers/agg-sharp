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
using MatterHackers.Agg.Image;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The "TextEdit" window, a port of agg-gui's demo-ui/src/windows/text_edit_demo.rs (itself egui's text_edit
	/// demo): a multi-line editor with hint text and alignment selectors, a clear button, a live selected-text
	/// readout, Ctrl/Cmd+Y to toggle the case of the selection, caret start/end buttons and a read-only field.
	/// </summary>
	/// <remarks>
	/// The editor is agg-gui's TextArea: a <see cref="CodeEditor"/> with no gutter or highlighting, wrapping, and
	/// aligning each wrapped row on its own. The search icon and clear button are Font Awesome glyphs, as agg-gui's.
	/// </remarks>
	public class TextEditWindow : ScrollableWidget
	{
		private const string DefaultText = "Edit this text";

		private const string SourceUrl = "https://github.com/MatterHackers/agg-sharp/blob/main/examples/AggSharpDemo/AggSharpDemo/GuiDemo/Windows/Widgets/TextEditWindow.cs";

		/// <summary>agg-gui's tint for inline `code` snippets.</summary>
		private static readonly Color CodeColor = new ColorF(0.92, 0.62, 0.34).ToColor();

		private readonly DemoTheme demoTheme;
		private readonly ThemeConfig theme;

		// Widgets that copy a colour when built, recoloured on ThemeChanged.
		private readonly List<TextWidget> texts = new List<TextWidget>();
		private readonly List<RadioButton> radios = new List<RadioButton>();
		private readonly List<AlignedTextEditWidget> fields = new List<AlignedTextEditWidget>();
		private readonly GuiWidget separator;
		private readonly IconGlyphWidget searchIcon;
		private readonly ThemedIconButton clear;
		private readonly TextWidget selectedText;
		private readonly WrappedTextWidget caseHint;

		public TextEditWindow(DemoTheme demoTheme)
			: base(autoScroll: true)
		{
			this.demoTheme = demoTheme;
			this.theme = demoTheme.Theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.ScrollArea.HAnchor = HAnchor.Stretch;

			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(16),
			};
			this.AddChild(column);

			column.AddChild(new Hyperlink("(source code)", this.theme, SourceUrl)
			{
				Name = "TextEdit Source Link",
				HAnchor = HAnchor.Center,
				Margin = new BorderDouble(0, 5),
			});

			var usage = this.Row();
			usage.AddChild(this.Label("Advanced usage of "));
			usage.AddChild(this.Label("TextEdit", CodeColor));
			usage.AddChild(this.Label("."));
			column.AddChild(usage);

			// The editor is built before the selectors, whose first check sets its alignment.
			this.Editor = new CodeEditor(DefaultText, pointSize: DemoText.Points(13))
			{
				Name = "TextEdit Editor",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute,
				Height = 130 * DeviceScale,
				Border = 1,
				ShowLineNumbers = false,
				WordWrap = true,
				AggGuiEditing = true,
				HintText = "Type something!",
			};
			this.Editor.Document.SetCaret(DefaultText.Length);

			this.HAlignRadios = this.Radios(column, "Horizontal align:", "TextEdit HAlign", new[] { "Left", "Center", "Right" }, i =>
				this.Editor.TextHAnchor = i == 0 ? HAnchor.Left : i == 1 ? HAnchor.Center : HAnchor.Right);
			this.VAlignRadios = this.Radios(column, "Vertical align:", "TextEdit VAlign", new[] { "Top", "Center", "Bottom" }, i =>
				this.Editor.TextVAnchor = i == 0 ? VAnchor.Top : i == 1 ? VAnchor.Center : VAnchor.Bottom);

			// The editor row: search icon, the editor, the clear button.
			var editorRow = new FlowLayoutWidget { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Fit, Margin = new BorderDouble(0, 5) };
			this.searchIcon = new IconGlyphWidget(IconFont.Search, this.theme.TextColor)
			{
				Name = "TextEdit Search Icon",
				VAnchor = VAnchor.Top,
				Margin = new BorderDouble(right: 6, top: 4),
			};
			editorRow.AddChild(this.searchIcon);
			editorRow.AddChild(this.Editor);
			this.clear = new ThemedIconButton(ClearGlyph(this.theme.TextColor), this.theme)
			{
				Name = "TextEdit Clear",
				VAnchor = VAnchor.Top,
				Margin = new BorderDouble(left: 6),
			};
			this.clear.Click += (s, e) => this.Clear();
			editorRow.AddChild(this.clear);
			column.AddChild(editorRow);

			var selectedRow = this.Row();
			selectedRow.AddChild(this.Label("Selected text: "));
			this.selectedText = this.Label("", CodeColor);
			this.selectedText.Name = "TextEdit Selected Text";
			selectedRow.AddChild(this.selectedText);
			column.AddChild(selectedRow);

			// Its colour is set by RefreshReadout: dimmed, as egui's add_enabled, until something is selected. It wraps
			// rather than clipping when the window is narrower than the sentence.
			this.caseHint = new WrappedTextWidget("Press ctrl+Y to toggle the case of selected text (cmd+Y on Mac)", DemoText.Points(DemoText.BodyPixels))
			{
				Name = "TextEdit Case Hint",
				Margin = new BorderDouble(0, 2),
			};
			column.AddChild(this.caseHint);

			var moveRow = this.Row();
			moveRow.Margin = new BorderDouble(0, 5);
			TextWidget moveLabel = this.Label("Move cursor to the:");
			moveLabel.VAnchor = VAnchor.Center;
			moveRow.AddChild(moveLabel);
			moveRow.AddChild(this.Button("start", "TextEdit Cursor Start", () => this.MoveCursor(0)));
			moveRow.AddChild(this.Button("end", "TextEdit Cursor End", () => this.MoveCursor(this.Editor.Text.Length)));
			column.AddChild(moveRow);

			// agg-gui's extra, kept from its original demo: a read-only field.
			this.separator = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				Height = Math.Max(1, Math.Round(DeviceScale)),
				Margin = new BorderDouble(0, 6),
			};
			column.AddChild(this.separator);
			TextWidget readOnlyLabel = this.Label("Read-only field");
			readOnlyLabel.HAnchor = HAnchor.Left;
			column.AddChild(readOnlyLabel);
			this.ReadOnlyField = this.Field("This field is read-only", multiLine: false, "");
			this.ReadOnlyField.Name = "TextEdit Read Only";
			this.ReadOnlyField.ReadOnly = true;
			this.ReadOnlyField.HAnchor = HAnchor.Stretch;
			this.ReadOnlyField.VAnchor = VAnchor.Absolute;
			this.ReadOnlyField.Height = 26 * DeviceScale;
			this.ReadOnlyField.TextVAnchor = VAnchor.Center;
			column.AddChild(this.ReadOnlyField);

			// The readout follows every caret, selection and text change.
			this.Editor.Document.CaretChanged += (s, e) => this.RefreshReadout();
			this.Editor.Document.TextChanged += (s, e) => this.RefreshReadout();

			this.HAlignRadios[0].Checked = true;
			this.VAlignRadios[0].Checked = true;
			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		/// <summary>The multi-line editor, seeded with egui's "Edit this text".</summary>
		public CodeEditor Editor { get; }

		public AlignedTextEditWidget ReadOnlyField { get; }

		public IReadOnlyList<RadioButton> HAlignRadios { get; }

		public IReadOnlyList<RadioButton> VAlignRadios { get; }

		/// <summary>What the "Selected text:" readout shows.</summary>
		public string SelectedTextReadout => this.selectedText.Text;

		/// <summary>egui's rule: an all-upper-case selection goes lower case, anything else goes upper case.</summary>
		public static string ToggleCase(string selection)
		{
			string upper = selection.ToUpperInvariant();
			return selection == upper ? selection.ToLowerInvariant() : upper;
		}

		/// <summary>Empties the editor, as the clear button does; the hint text shows again.</summary>
		public void Clear()
		{
			this.Editor.Text = "";
			this.Editor.Document.SetCaret(0);
			this.RefreshReadout();
		}

		/// <summary>Puts the caret at <paramref name="charIndex"/> and hands the keyboard back to the editor.</summary>
		public void MoveCursor(int charIndex)
		{
			this.Editor.Focus();
			this.Editor.Document.SetCaret(charIndex);
			this.Editor.ScrollToCaret();
			this.RefreshReadout();
		}

		/// <summary>
		/// Ctrl/Cmd+Y toggles the case of the editor's selection, keeping it selected so a second press toggles it
		/// back. Returns false, changing nothing, when there is no selection.
		/// </summary>
		public bool ToggleSelectionCase()
		{
			CodeDocument document = this.Editor.Document;
			if (!document.HasSelection)
			{
				return false;
			}

			// An insert over the selection, so Ctrl+Z takes the toggle back.
			int start = document.SelectionStart;
			string toggled = ToggleCase(document.SelectedText);
			document.Insert(toggled);
			document.Select(start, start + toggled.Length);
			this.RefreshReadout();
			return true;
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			// Ahead of the editor, which swallows every Control chord it does not know. The mac layer folds
			// Command onto Control, so this is Cmd+Y there.
			if (keyEvent.Control
				&& keyEvent.KeyCode == Keys.Y
				&& this.Editor.ContainsFocus)
			{
				this.ToggleSelectionCase();
				keyEvent.Handled = true;
				keyEvent.SuppressKeyPress = true;
				return;
			}

			base.OnKeyDown(keyEvent);
		}

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		private void RefreshReadout()
		{
			if (this.caseHint == null)
			{
				return;
			}

			string selection = this.Editor.Document.SelectedText;
			this.selectedText.Text = selection;
			this.caseHint.TextColor = selection.Length > 0 ? this.demoTheme.Palette.TextColor : this.demoTheme.Palette.TextDim;
		}

		private AlignedTextEditWidget Field(string text, bool multiLine, string hint)
		{
			var field = new AlignedTextEditWidget(text, DemoText.Points(DemoText.BodyPixels), multiLine, hint)
			{
				Border = 1,
			};
			this.fields.Add(field);
			return field;
		}

		private FlowLayoutWidget Row()
		{
			return new FlowLayoutWidget { HAnchor = HAnchor.Left | HAnchor.Fit, VAnchor = VAnchor.Fit };
		}

		private ThemedTextButton Button(string text, string name, Action click)
		{
			var button = this.demoTheme.AccentButton(new ThemedTextButton(text, this.theme)
			{
				Name = name,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 6),
			});
			button.Click += (s, e) => click();
			return button;
		}

		/// <summary>A labelled row of radio buttons, one chosen - agg-gui's selector buttons.</summary>
		private IReadOnlyList<RadioButton> Radios(GuiWidget parent, string label, string namePrefix, string[] options, Action<int> set)
		{
			var row = this.Row();
			row.Margin = new BorderDouble(0, 3);
			TextWidget caption = this.Label(label);
			caption.VAnchor = VAnchor.Center;
			row.AddChild(caption);
			var buttons = new List<RadioButton>();
			for (int i = 0; i < options.Length; i++)
			{
				int index = i;
				var radio = new RadioButton(options[i], this.theme.TextColor, this.theme.DefaultFontSize)
				{
					Name = namePrefix + " " + options[i],
					VAnchor = VAnchor.Center,
					Margin = new BorderDouble(left: 8),
				};
				radio.CheckedStateChanged += (s, e) =>
				{
					if (radio.Checked)
					{
						set(index);
					}
				};
				row.AddChild(radio);
				buttons.Add(radio);
				this.radios.Add(radio);
			}

			parent.AddChild(row);
			return buttons;
		}

		private TextWidget Label(string text, Color? fixedColor = null)
		{
			var widget = new TextWidget(text, pointSize: DemoText.Points(DemoText.BodyPixels), textColor: fixedColor ?? this.theme.TextColor)
			{
				Margin = new BorderDouble(0, 2),
				AutoExpandBoundsToText = true,
			};

			// The code-coloured snippets keep their tint in both themes, as agg-gui's do.
			if (fixedColor == null)
			{
				this.texts.Add(widget);
			}

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
			DemoPalette palette = this.demoTheme.Palette;
			foreach (TextWidget text in this.texts)
			{
				text.TextColor = palette.TextColor;
			}

			foreach (RadioButton radio in this.radios)
			{
				radio.TextColor = palette.TextColor;
			}

			foreach (AlignedTextEditWidget field in this.fields)
			{
				field.BackgroundColor = palette.WidgetBackground;
				field.BorderColor = palette.Separator;
				field.HintColor = palette.TextDim;
				field.Editor.TextColor = palette.TextColor;
				field.Editor.CursorColor = palette.TextColor;
				field.Editor.HighlightColor = this.theme.PrimaryAccentColor.WithAlpha(90);
			}

			this.Editor.BackgroundColor = palette.WidgetBackground;
			this.Editor.BorderColor = palette.Separator;
			this.Editor.HintColor = palette.TextDim;
			this.Editor.TextColor = palette.TextColor;
			this.Editor.CaretColor = palette.TextColor;
			this.Editor.SelectionColor = this.theme.PrimaryAccentColor.WithAlpha(90);
			this.Editor.ScrollbarColor = palette.TextColor.WithAlpha(70);
			this.Editor.ScrollbarDragColor = palette.TextColor.WithAlpha(130);
			this.separator.BackgroundColor = palette.Separator;
			this.searchIcon.Color = palette.TextColor;
			this.clear.SetIcon(ClearGlyph(palette.TextColor));
			this.RefreshReadout();
		}

		/// <summary>The clear button's Font Awesome xmark, the size of the search glyph beside it.</summary>
		private static ImageBuffer ClearGlyph(Color color)
		{
			return GlyphIcon.Render(IconFont.Clear, IconFont.TypeFace, color, (int)Math.Round(IconGlyphWidget.IconSize * DeviceScale));
		}
	}
}
