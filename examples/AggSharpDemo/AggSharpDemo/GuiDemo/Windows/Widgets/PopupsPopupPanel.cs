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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The surface the Popups window opens: agg-gui's paint_popup (popups_demo.rs), a small rounded panel that
	/// reports the placement, gap and close behavior it was opened with. It is parented to the SystemWindow so it
	/// can hang outside the demo window, and the window places it.
	/// </summary>
	public class PopupsPopupPanel : FlowLayoutWidget
	{
		/// <summary>agg-gui's POPUP_W x POPUP_H.</summary>
		public const double PopupWidth = 234;

		public const double PopupHeight = 112;

		private readonly double pointSize;
		private readonly ImageWidget titleIcon;
		private readonly TextWidget title;
		private readonly TextWidget alignText;
		private readonly TextWidget gapText;
		private readonly TextWidget closeText;

		public PopupsPopupPanel(double pointSize)
			: base(FlowDirection.TopToBottom)
		{
			this.Name = "Popups Popup";
			this.HAnchor = HAnchor.Absolute;
			this.VAnchor = VAnchor.Absolute;
			this.Size = new VectorMath.Vector2(PopupWidth * DeviceScale, PopupHeight * DeviceScale);
			this.Padding = new BorderDouble(12, 8);
			this.BackgroundRadius = 6 * DeviceScale;
			this.BackgroundOutlineWidth = 1 * DeviceScale;

			this.pointSize = pointSize;

			// agg-gui's title reads "\u{F075}  Popup contents": Font Awesome's comment glyph, drawn at the
			// title's em, then two spaces' worth of gap. Recolor draws the glyph.
			var titleRow = new FlowLayoutWidget { Name = "Popups Popup Title", Margin = new BorderDouble(0, 2) };
			this.AddChild(titleRow);
			this.titleIcon = new ImageWidget(this.TitleIcon(Color.Black), false)
			{
				Name = "Popups Popup Title Icon",
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(right: Math.Round(pointSize * 0.5 * DeviceScale)),
			};
			titleRow.AddChild(this.titleIcon);
			this.title = new TextWidget("Popup contents", pointSize: pointSize)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center,
			};
			titleRow.AddChild(this.title);

			this.alignText = this.Line(pointSize - 1);
			this.gapText = this.Line(pointSize - 1);
			this.closeText = this.Line(pointSize - 1);
		}

		/// <summary>Writes the popup's configuration into its lines.</summary>
		public void Describe(RectAlign align, double gap, PopupCloseBehavior behavior)
		{
			this.alignText.Text = "Align: " + align;
			this.gapText.Text = $"Gap: {gap:0} px";
			this.closeText.Text = "Close: " + behavior;
		}

		/// <summary>Takes the panel colours from the demo palette.</summary>
		public void Recolor(DemoPalette palette)
		{
			this.BackgroundColor = palette.PanelFill;
			this.BorderColor = palette.WidgetStroke;
			this.title.TextColor = palette.TextColor;
			this.titleIcon.Image = this.TitleIcon(palette.TextColor);
			this.alignText.TextColor = palette.TextDim;
			this.gapText.TextColor = palette.TextDim;
			this.closeText.TextColor = palette.TextDim;
		}

		/// <summary>The comment glyph one em of the title's point size across, as text of that size draws it.</summary>
		private ImageBuffer TitleIcon(Color color)
		{
			int pixels = (int)Math.Round(this.pointSize * 96 / 72 * DeviceScale);
			return GlyphIcon.Render(IconFont.Comment, IconFont.TypeFace, color, pixels);
		}

		private TextWidget Line(double pointSize)
		{
			var line = new TextWidget("", pointSize: pointSize)
			{
				AutoExpandBoundsToText = true,
				Margin = new BorderDouble(0, 2),
			};
			this.AddChild(line);
			return line;
		}
	}
}
