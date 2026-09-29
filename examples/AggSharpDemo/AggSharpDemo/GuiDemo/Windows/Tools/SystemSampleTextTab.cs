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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tools
{
	/// <summary>
	/// The System window's "Sample Text" tab, a port of agg-gui's demo-ui/src/windows/lcd_sample_text.rs: four
	/// reference paragraphs (from C++ AGG's truetype_test_02_win) in the current system font, a live preview of the
	/// Font tab's controls. <see cref="Rebuild"/> re-creates the paragraphs, because a text widget takes its face when
	/// it is built.
	/// </summary>
	public class SystemSampleTextTab : ScrollableWidget
	{
		public static readonly string[] Paragraphs =
		{
			"A single pixel on a color LCD is made of three colored elements ordered (on various displays) either as blue, "
				+ "green, and red (BGR), or as red, green, and blue (RGB). These pixel components, sometimes called sub-pixels, "
				+ "appear as a single color to the human eye because of blurring by the optics and spatial integration by nerve "
				+ "cells in the eye.",
			"The components are easily visible, however, when viewed with a small magnifying glass, such as a loupe. Over a "
				+ "certain resolution range the colors in the sub-pixels are not visible, but the relative intensity of the "
				+ "components shifts the apparent position or orientation of a line. Methods that take this interaction between "
				+ "the display technology and the human visual system into account are called subpixel rendering algorithms.",
			"The resolution at which colored sub-pixels go unnoticed differs, however, with each user some users are distracted "
				+ "by the colored \"fringes\" resulting from sub-pixel rendering. Subpixel rendering is better suited to some "
				+ "display technologies than others. The technology is well-suited to LCDs, but less so for CRTs. In a CRT the "
				+ "light from the pixel components often spread across pixels, and the outputs of adjacent pixels are not "
				+ "perfectly independent.",
			"If a designer knew precisely a great deal about the display's electron beams and aperture grille, subpixel "
				+ "rendering might have some advantage. But the properties of the CRT components, coupled with the alignment "
				+ "variations that are part of the production process, make subpixel rendering less effective for these "
				+ "displays. The technique should have good application to organic light emitting diodes and other display "
				+ "technologies.",
		};

		private readonly MiscDemoKit kit;
		private readonly FlowLayoutWidget column;
		private readonly FlowLayoutWidget paragraphs;
		private readonly GuiWidget separator;

		internal SystemSampleTextTab(MiscDemoKit kit)
			: base(autoScroll: true)
		{
			this.kit = kit;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.ScrollArea.HAnchor = HAnchor.Stretch;

			this.column = kit.Column();
			this.column.Name = "System Sample Text";
			this.column.Margin = new BorderDouble(14);
			this.AddChild(this.column);

			this.column.AddChild(kit.Wrapped("Reference paragraphs rendered with the current system font.  Adjust the controls on the Font tab to preview their effect.", 13));
			this.separator = new GuiWidget { HAnchor = HAnchor.Stretch, Height = GuiWidget.DeviceScale, Margin = new BorderDouble(0, 6) };
			this.column.AddChild(this.separator);
			this.paragraphs = kit.Column();
			this.column.AddChild(this.paragraphs);
			this.Rebuild();
		}

		/// <summary>Re-creates the paragraphs so they pick up the current default font.</summary>
		public void Rebuild()
		{
			this.paragraphs.CloseChildren();
			foreach (string text in Paragraphs)
			{
				this.paragraphs.AddChild(new WrappedTextWidget(text, this.kit.FontSize(14), textColor: this.kit.Theme.TextColor)
				{
					Margin = new BorderDouble(0, 5),
					LineSpacing = DemoText.LineHeightFactor,
				});
			}

			this.Recolor();
		}

		/// <summary>Pushes the current theme into the paragraphs and the separator (the intro is the kit's).</summary>
		public void Recolor()
		{
			DemoPalette palette = this.kit.DemoTheme.Palette;
			this.BackgroundColor = palette.PanelFill;
			this.separator.BackgroundColor = palette.Separator;
			foreach (GuiWidget child in this.paragraphs.Children)
			{
				if (child is WrappedTextWidget text)
				{
					text.TextColor = palette.TextColor;
				}
			}
		}
	}
}
