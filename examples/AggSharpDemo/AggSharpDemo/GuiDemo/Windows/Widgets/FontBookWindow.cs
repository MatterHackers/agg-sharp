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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The "Font Book" window, agg-gui's font_book (demo-ui/src/windows/font_book/mod.rs, itself egui's
	/// font_book.rs): how many characters the chosen font has, a font picker, a character-or-hex filter and a
	/// scrolling <see cref="FontBookGlyphGrid"/> of the glyphs. agg-gui picks among its system fonts; agg-sharp's
	/// picker lists the faces this demo carries, its icon and emoji fonts included.
	/// </summary>
	public class FontBookWindow : FlowLayoutWidget
	{
		public const string SourceUrl = "https://github.com/larsbrubaker/agg-gui/blob/main/demo-ui/src/windows/font_book/mod.rs";

		/// <summary>The faces the picker offers, in its order.</summary>
		public static readonly IReadOnlyList<(string Name, Func<TypeFace> Face)> FontOptions = new (string, Func<TypeFace>)[]
		{
			("Liberation Sans", () => LiberationSansFont.Instance),
			("Liberation Sans Bold", () => LiberationSansBoldFont.Instance),
			("Font Awesome", () => IconFont.TypeFace),
			("Noto Emoji", () => EmojiFont.TypeFace),
			("Nunito", () => DemoText.Nunito),
		};

		private const double Gap = 8;

		private readonly MiscDemoKit kit;
		private readonly DemoTheme demoTheme;
		private readonly List<GuiWidget> separators = new List<GuiWidget>();

		public FontBookWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.demoTheme = demoTheme;
			this.kit = new MiscDemoKit(demoTheme);
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(12);

			GuiWidget Spaced(GuiWidget widget)
			{
				widget.Margin = new BorderDouble(bottom: Gap);
				return widget;
			}

			this.AddChild(Spaced(new Hyperlink("(source code)", this.kit.Theme, SourceUrl) { Name = "Font Book Source Link" }));

			this.CountLabel = this.kit.Label(string.Empty, 12);
			this.CountLabel.Name = "Font Book Count";
			this.AddChild(Spaced(this.CountLabel));

			WrappedTextWidget note = this.kit.Wrapped("Click a glyph to copy it. Pick a different font below to see its characters.", 12);
			note.HAnchor = HAnchor.Stretch;
			this.AddChild(Spaced(note));
			this.AddChild(this.Separator());

			// agg-gui's font picker is a combo as wide as the window, so it reads as a field with the font's name
			// (its arrow runs off past the window's edge); ours stretches to the edge and keeps its arrow in view.
			FlowLayoutWidget fontRow = this.kit.Row(Gap);
			fontRow.HAnchor = HAnchor.Stretch;
			fontRow.AddChild(this.kit.Label("Font:", 13));
			this.FontPicker = new DropDownList("Font", this.kit.Theme.TextColor, pointSize: this.kit.FontSize(13))
			{
				Name = "Font Book Font",
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(left: Gap),
			};
			foreach ((string name, _) in FontOptions)
			{
				this.FontPicker.AddItem(name);
			}

			fontRow.AddChild(this.FontPicker);
			this.AddChild(fontRow);

			FlowLayoutWidget filterRow = this.kit.Row(Gap);
			filterRow.AddChild(this.kit.Label("Filter:", 13));
			this.FilterField = new ThemedTextEditWidget(string.Empty, this.kit.Theme, pixelWidth: 160 * GuiWidget.DeviceScale, messageWhenEmptyAndNotSelected: "char or hex, e.g. 20AC")
			{
				Name = "Font Book Filter",
				Margin = new BorderDouble(left: Gap, right: Gap),
			};
			filterRow.AddChild(this.FilterField);
			this.ClearFilterButton = this.kit.Button("Font Book Clear Filter", "x");
			this.ClearFilterButton.Click += (s, e) => this.FilterField.Text = string.Empty;
			filterRow.AddChild(this.ClearFilterButton);
			this.AddChild(filterRow);
			this.AddChild(this.Separator());

			this.Grid = new FontBookGlyphGrid(this.kit.Theme, FontOptions[0].Face())
			{
				Name = "Font Book Grid",
			};
			this.GridScroll = new ScrollableWidget(autoScroll: true)
			{
				Name = "Font Book Scroll",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.GridScroll.ScrollArea.HAnchor = HAnchor.Stretch;

			// Room for the scroll bar, which is drawn over the scroll area, so the last column is not under it.
			this.GridScroll.ScrollArea.Margin = new BorderDouble(right: 15);
			this.GridScroll.AddChild(this.Grid);
			this.AddChild(this.GridScroll);

			this.FilterField.TextChanged += (s, e) => this.Grid.Filter = this.FilterField.Text;
			this.FontPicker.SelectionChanged += (s, e) =>
			{
				if (this.FontPicker.SelectedIndex >= 0)
				{
					this.Grid.TypeFace = FontOptions[this.FontPicker.SelectedIndex].Face();
					this.GridScroll.ScrollPositionFromTop = VectorMath.Vector2.Zero;
					this.UpdateCount();
				}
			};
			this.FontPicker.SelectedIndex = 0;
			this.UpdateCount();

			this.Recolor(null, null);
			demoTheme.ThemeChanged += this.Recolor;
		}

		public TextWidget CountLabel { get; }

		public DropDownList FontPicker { get; }

		public ThemedTextEditWidget FilterField { get; }

		public ThemedTextButton ClearFilterButton { get; }

		public ScrollableWidget GridScroll { get; }

		public FontBookGlyphGrid Grid { get; }

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.Recolor;
			base.OnClosed(e);
		}

		private GuiWidget Separator()
		{
			var separator = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				Height = Math.Max(1, Math.Round(DeviceScale)),
				Margin = new BorderDouble(bottom: Gap),
			};
			this.separators.Add(separator);
			return separator;
		}

		private void UpdateCount()
		{
			this.CountLabel.Text = $"The selected font supports {this.Grid.CharacterCount} characters.";
		}

		private void Recolor(object sender, EventArgs e)
		{
			DemoPalette palette = this.demoTheme.Palette;
			this.BackgroundColor = palette.PanelFill;
			this.kit.Recolor();
			foreach (GuiWidget separator in this.separators)
			{
				separator.BackgroundColor = palette.Separator;
			}

			this.FontPicker.TextColor = palette.TextColor;
			this.FilterField.ActualTextEditWidget.TextColor = palette.TextColor;
			this.FilterField.BackgroundColor = palette.WidgetBackground;
			// agg-gui's plain Button: the accent fill with white lettering.
			Color accent = DemoTheme.ColorOf(this.demoTheme.Accent);
			this.ClearFilterButton.BackgroundColor = accent;
			this.ClearFilterButton.TextColor = Color.White;
			this.ClearFilterButton.HoverColor = new Color(accent, 220);
			this.ClearFilterButton.MouseDownColor = new Color(accent, 180);

			// The grid reads the theme as it draws.
			this.Grid.Invalidate();
		}
	}
}
