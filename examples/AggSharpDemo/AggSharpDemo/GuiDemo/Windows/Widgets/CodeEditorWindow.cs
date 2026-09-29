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

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The "Code Editor" window, a port of agg-gui's demo-ui/src/windows/code_editor_demo.rs (itself egui's
	/// code_editor demo): a description and source link over an editable, Rust-highlighted buffer with a line
	/// number gutter, filling the window.
	/// </summary>
	/// <remarks>
	/// Deviations: agg-gui's gutter is a sibling widget reading the TextArea's published scroll; here it is part
	/// of <see cref="CodeEditor"/>, which owns the scroll. The code font is Liberation Mono rather than Cascadia Code.
	/// </remarks>
	public class CodeEditorWindow : FlowLayoutWidget
	{
		/// <summary>agg-gui's SAMPLE: exercises every token class the highlighter knows.</summary>
		public const string Sample = "// A tiny agg-gui example\n"
			+ "fn main() {\n"
			+ "    let greeting = \"Hello, agg-gui!\";\n"
			+ "    println!(\"{}\", greeting);\n"
			+ "\n"
			+ "    let values: Vec<f64> = (0..10)\n"
			+ "        .map(|i| i as f64 * 0.1)\n"
			+ "        .collect();\n"
			+ "\n"
			+ "    for (i, v) in values.iter().enumerate() {\n"
			+ "        println!(\"[{i}] {v:.2}\");\n"
			+ "    }\n"
			+ "}";

		private const string SourceUrl = "https://github.com/MatterHackers/agg-sharp/blob/main/examples/AggSharpDemo/AggSharpDemo/GuiDemo/Windows/Widgets/CodeEditorWindow.cs";

		private readonly DemoTheme demoTheme;
		private readonly TextWidget description;
		private readonly GuiWidget separator;

		public CodeEditorWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.demoTheme = demoTheme;
			ThemeConfig theme = demoTheme.Theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			// Header row: description + source link, on one line as egui has them.
			var header = new FlowLayoutWidget { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Fit, Padding = new BorderDouble(8, 4) };
			this.description = new TextWidget("An example of syntax highlighting in an editable TextEdit.", pointSize: DemoText.Points(12))
			{
				Name = "Code Editor Description",
				VAnchor = VAnchor.Center,
				AutoExpandBoundsToText = true,
			};
			header.AddChild(this.description);
			this.SourceLink = new Hyperlink("(source code)", theme, SourceUrl)
			{
				Name = "Code Editor Source Link",
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 8),
			};
			header.AddChild(this.SourceLink);
			this.AddChild(header);

			this.separator = new GuiWidget { HAnchor = HAnchor.Stretch, Height = Math.Max(1, Math.Round(DeviceScale)) };
			this.AddChild(this.separator);

			// The editor takes all the height left, growing and shrinking with the window; it scrolls itself. Like
			// agg-gui's TextArea it wraps to its width and edits with agg-gui's Home, Enter and Tab.
			// agg-gui's EDITOR_FONT_SIZE: a 13px em of Cascadia Code.
			this.Editor = new CodeEditor(Sample, CodeFont.TypeFace, DemoText.Points(13))
			{
				Name = "Code Editor Editor",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Language = SyntaxLanguage.Rust,
				WordWrap = true,
				AggGuiEditing = true,
			};
			this.AddChild(this.Editor);
			this.AddChild(new GuiWidget { HAnchor = HAnchor.Stretch, Height = 8 * DeviceScale });

			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		public CodeEditor Editor { get; }

		public Hyperlink SourceLink { get; }

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		private void OnThemeChanged(object sender, EventArgs e)
		{
			this.Recolor();
			this.Invalidate();
		}

		/// <summary>agg-gui's colours: panel_fill behind the header and in the gutter, text_dim for the description
		/// and line numbers, widget_bg under the code, and the syntax palette for a dark or light editor.</summary>
		private void Recolor()
		{
			DemoPalette palette = this.demoTheme.Palette;
			this.BackgroundColor = palette.PanelFill;
			this.description.TextColor = palette.TextDim;
			this.separator.BackgroundColor = palette.Separator;

			CodeEditor editor = this.Editor;
			editor.BackgroundColor = palette.WidgetBackground;
			editor.TextColor = palette.TextColor;
			editor.CaretColor = palette.TextColor;
			editor.GutterColor = palette.PanelFill;
			editor.LineNumberColor = palette.TextDim;
			editor.SelectionColor = this.demoTheme.Theme.PrimaryAccentColor.WithAlpha(90);
			editor.Palette = SyntaxPalette.For(palette.IsDark);
		}
	}
}
