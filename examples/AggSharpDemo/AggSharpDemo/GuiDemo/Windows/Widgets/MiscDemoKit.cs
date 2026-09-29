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

using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The Misc Demos window's widget factory: builds themed labels, check boxes, radios, buttons and
	/// sliders and remembers the ones that copy a colour when built, so <see cref="Recolor"/> can push a live
	/// theme change into them. agg-gui sizes text as an em in pixels; <see cref="FontSize"/>
	/// turns that into points (<see cref="DemoText.Points"/>).
	/// </summary>
	internal sealed class MiscDemoKit
	{
		private readonly List<(TextWidget Widget, bool Themed)> texts = new List<(TextWidget, bool)>();
		private readonly List<WrappedTextWidget> wrappedTexts = new List<WrappedTextWidget>();
		private readonly List<CheckBox> checkBoxes = new List<CheckBox>();
		private readonly List<RadioButton> radios = new List<RadioButton>();
		private readonly Dictionary<ThemedIconButton, (string Glyph, double Size)> glyphButtons = new Dictionary<ThemedIconButton, (string, double)>();

		public MiscDemoKit(DemoTheme demoTheme)
		{
			this.DemoTheme = demoTheme;
			this.Theme = demoTheme.Theme;
		}

		public DemoTheme DemoTheme { get; }

		public ThemeConfig Theme { get; }

		/// <summary>The point size that draws agg-gui's <paramref name="aggGuiSize"/> pixel em.</summary>
		public double FontSize(double aggGuiSize) => DemoText.Points(aggGuiSize);

		/// <summary>A label in the theme's text colour, or in a fixed <paramref name="color"/> that the theme leaves alone.</summary>
		public TextWidget Label(string text, double size = 12, Color? color = null)
		{
			var widget = new TextWidget(text, pointSize: this.FontSize(size), textColor: color ?? this.Theme.TextColor)
			{
				AutoExpandBoundsToText = true,
			};
			this.texts.Add((widget, color == null));
			return widget;
		}

		public WrappedTextWidget Wrapped(string text, double size = 12)
		{
			var widget = new WrappedTextWidget(text, this.FontSize(size), textColor: this.Theme.TextColor)
			{
				Margin = new BorderDouble(0, 2),
				LineSpacing = DemoText.LineHeightFactor,
			};
			this.wrappedTexts.Add(widget);
			return widget;
		}

		public CheckBox CheckBox(string name, string label, bool isChecked, double size = 12.5)
		{
			var widget = new CheckBox(label, this.Theme.TextColor, this.FontSize(size))
			{
				Name = name,
				Checked = isChecked,
			};
			this.checkBoxes.Add(widget);
			return widget;
		}

		public RadioButton Radio(string name, string label, double size = 12.5)
		{
			var widget = new RadioButton(label, this.Theme.TextColor, (int)System.Math.Round(this.FontSize(size)))
			{
				Name = name,
				Margin = new BorderDouble(1),
			};
			this.radios.Add(widget);
			return widget;
		}

		public ThemedTextButton Button(string name, string text)
		{
			return this.DemoTheme.AccentButton(new ThemedTextButton(text, this.Theme)
			{
				Name = name,
				Margin = new BorderDouble(0, 2),
			});
		}

		/// <summary>
		/// A button showing one Font Awesome <paramref name="glyph"/> at the em of <paramref name="size"/> point
		/// text, as agg-gui's Button::new(glyph) draws it. Recolor redraws it in the theme's text colour.
		/// </summary>
		public ThemedIconButton GlyphButton(string name, string glyph, double size = 12)
		{
			var button = new ThemedIconButton(this.RenderGlyph(glyph, size), this.Theme)
			{
				Name = name,
				Margin = new BorderDouble(0, 2),
			};
			this.glyphButtons[button] = (glyph, size);
			return button;
		}

		/// <summary>Swaps a <see cref="GlyphButton"/>'s glyph.</summary>
		public void SetGlyph(ThemedIconButton button, string glyph)
		{
			double size = this.glyphButtons[button].Size;
			this.glyphButtons[button] = (glyph, size);
			button.SetIcon(this.RenderGlyph(glyph, size));
		}

		private ImageBuffer RenderGlyph(string glyph, double size)
		{
			int pixels = (int)System.Math.Round(this.FontSize(size) * 96 / 72 * GuiWidget.DeviceScale);
			return GlyphIcon.Render(glyph, IconFont.TypeFace, this.Theme.TextColor, pixels);
		}

		/// <summary>An integer-or-stepped horizontal slider <paramref name="designLength"/> units long.</summary>
		public Slider Slider(string name, double value, double minimum, double maximum, double step, double designLength = 160)
		{
			return new Slider(Vector2.Zero, designLength * GuiWidget.DeviceScale, minimum, maximum)
			{
				Name = name,
				Step = step,
				Value = value,
				KeyboardStepping = true,
				Margin = new BorderDouble(0, 4, 6, 4),
			};
		}

		/// <summary>A left-to-right row that fits its children and centres them vertically, as egui's ui.horizontal does.</summary>
		public FlowLayoutWidget Row(double gap = 6)
		{
			return new CenteringRow
			{
				HAnchor = HAnchor.Left | HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(0, gap / 2),
			};
		}

		/// <summary>A top-to-bottom column filling its parent's width.</summary>
		public FlowLayoutWidget Column()
		{
			return new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
		}

		/// <summary>Pushes the current theme into every widget that copied a colour when built.</summary>
		public void Recolor()
		{
			foreach ((TextWidget widget, bool themed) in this.texts)
			{
				if (themed)
				{
					widget.TextColor = this.Theme.TextColor;
				}
			}

			foreach (WrappedTextWidget widget in this.wrappedTexts)
			{
				widget.TextColor = this.Theme.TextColor;
			}

			foreach (CheckBox widget in this.checkBoxes)
			{
				widget.TextColor = this.Theme.TextColor;
			}

			foreach (RadioButton widget in this.radios)
			{
				widget.TextColor = this.Theme.TextColor;
			}

			foreach (var (button, (glyph, size)) in this.glyphButtons)
			{
				button.SetIcon(this.RenderGlyph(glyph, size));
			}
		}

		/// <summary>
		/// A row that centres each child vertically. The kit's widgets carry no vertical anchor because a
		/// top-to-bottom column rejects Center; the row sets it as they arrive.
		/// </summary>
		private sealed class CenteringRow : FlowLayoutWidget
		{
			public override GuiWidget AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
			{
				childToAdd.VAnchor = VAnchor.Center;
				return base.AddChild(childToAdd, indexInChildrenList);
			}
		}
	}
}
