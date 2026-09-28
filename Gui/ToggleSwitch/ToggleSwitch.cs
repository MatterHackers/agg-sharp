/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A checkbox drawn as a sliding switch. Every distance in here - the width and height handed in, the
	/// thumb, the insets - is in the caller's units, so a caller that wants the switch to keep its physical
	/// size on a high density panel passes a width and height already multiplied by
	/// <see cref="GuiWidget.DeviceScale"/>. Nothing inside scales itself, which is what keeps the parts in
	/// proportion to each other whatever size the caller asks for.
	/// It is drawn as agg-gui's toggle switch: a pill track in interiorColor when on (the theme's neutral
	/// stroke from ThemeConfig.Current when off) with a round thumbColor knob. backgroundColor, textColor
	/// (for the switch itself) and borderColor are accepted for compatibility; agg-gui's switch has no border.
	/// </summary>
	public class ToggleSwitchView : CheckBoxViewStates
	{
		public ToggleSwitchView(string onText, string offText, double width, double height, Color backgroundColor, Color interiorColor, Color thumbColor, Color textColor, Color borderColor)
		{
			GuiWidget normal = createState(offText, false, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor, borderColor);
			GuiWidget normalHover = createState(offText, false, hovered: true, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor, borderColor);
			GuiWidget switchNormalToPressed = createState(onText, true, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor, borderColor);
			GuiWidget pressed = createState(onText, true, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor, borderColor);
			GuiWidget pressedHover = createState(onText, true, hovered: true, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor, borderColor);
			GuiWidget switchPressedToNormal = createState(offText, false, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor, borderColor);
			GuiWidget disabled = new TextWidget("disabled");

			SetViewStates(normal, normalHover, switchNormalToPressed, pressed, pressedHover, switchPressedToNormal, disabled);

			this.VAnchor = VAnchor.Fit;
		}

		private GuiWidget createState(string word, bool isChecked, double width, double height, ref Color backgroundColor, ref Color interiorColor, ref Color thumbColor, ref Color textColor, Color borderColor)
			=> createState(word, isChecked, false, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor, borderColor);

		private GuiWidget createState(string word, bool isChecked, bool hovered, double width, double height, ref Color backgroundColor, ref Color interiorColor, ref Color thumbColor, ref Color textColor, Color borderColor)
		{
			GuiWidget switchNormalToPressed = new FlowLayoutWidget(FlowDirection.LeftToRight);

			if (!string.IsNullOrEmpty(word))
			{
				switchNormalToPressed.AddChild(new TextWidget(word, pointSize: 10, textColor: textColor)
				{
					VAnchor = VAnchor.Center,
					Margin = new BorderDouble(right: 5)
				});
			}

			switchNormalToPressed.AddChild(
				new SwitchView(width, height, isChecked, backgroundColor, interiorColor, thumbColor, textColor, borderColor)
				{
					VAnchor = VAnchor.Center,
					Hovered = hovered,
				});

			return switchNormalToPressed;
		}

		internal class SwitchView : GuiWidget
		{
			private bool Checked { get; }

			private RectangleDouble switchBounds { get; }

			internal SwitchView(double width, double height, bool startValue, Color backgroundColor, Color interiorColor, Color thumbColor, Color exteriorColor, Color borderColor)
			{
				this.Checked = startValue;

				InteriorColor = interiorColor;
				ExteriorColor = exteriorColor;
				ThumbColor = thumbColor;
				LocalBounds = new RectangleDouble(0, 0, width, height);

				this.switchBounds = new RectangleDouble(0, 0, width, height);
			}

			public Color ExteriorColor { get; set; }

			public Color InteriorColor { get; set; }

			public Color ThumbColor { get; set; }

			/// <summary>Whether this is the hover state's copy, drawn with agg-gui's hovered track colour.</summary>
			public bool Hovered { get; set; }

			/// <summary>
			/// agg-gui's pill: the track fills the switch, InteriorColor when on and the theme's neutral stroke
			/// when off, with a round ThumbColor knob at the matching end. ExteriorColor and the border colour
			/// are no longer drawn; agg-gui's switch has no border.
			/// </summary>
			public override void OnDraw(Graphics2D graphics2D)
			{
				graphics2D.FillRectangle(switchBounds, this.BackgroundColor);
				base.OnDraw(graphics2D);

				SelectionControlStyle.DrawSwitch(graphics2D, switchBounds, this.Checked, this.Hovered, this.Enabled, this.InteriorColor, this.ThumbColor, ThemeConfig.Current);
			}
		}
	}
}