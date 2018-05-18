﻿/*
Copyright (c) 2014, MatterHackers, Inc.
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

namespace MatterHackers.Agg.UI
{
	public class ToggleSwitchView : CheckBoxViewStates
	{
		public ToggleSwitchView(string onText, string offText, double width, double height, Color backgroundColor, Color interiorColor, Color thumbColor, Color textColor, Color borderColor)
		{
			GuiWidget normal = createState(offText, false, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor, borderColor);
			GuiWidget normalHover = createState(offText, false, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor, borderColor);
			GuiWidget switchNormalToPressed = createState(onText, true, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor, borderColor);
			GuiWidget pressed = createState(onText, true, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor, borderColor);
			GuiWidget pressedHover = createState(onText, true, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor, borderColor);
			GuiWidget switchPressedToNormal = createState(offText, false, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor, borderColor);
			GuiWidget disabled = new TextWidget("disabled");

			SetViewStates(normal, normalHover, switchNormalToPressed, pressed, pressedHover, switchPressedToNormal, disabled);

			this.VAnchor = VAnchor.Fit;
		}

		private GuiWidget createState(string word, bool isChecked, double width, double height, ref Color backgroundColor, ref Color interiorColor, ref Color thumbColor, ref Color textColor, Color borderColor)
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
				new SwitchView(width, height, isChecked, backgroundColor, interiorColor, isChecked ? thumbColor : Color.Gray, textColor, borderColor)
				{
					VAnchor = VAnchor.Center
				});

			return switchNormalToPressed;
		}

		internal class SwitchView : GuiWidget
		{
			private bool Checked { get; }

			private RectangleDouble borderRect;

			private RectangleDouble innerRect;

			private RectangleDouble checkedThumbBounds;

			private RectangleDouble uncheckedThumbBounds;

			private RectangleDouble switchBounds { get; }

			private Color borderColor;

			private Color disabledBorderColor;

			internal SwitchView(double width, double height, bool startValue, Color backgroundColor, Color interiorColor, Color thumbColor, Color exteriorColor, Color borderColor)
			{
				this.Checked = startValue;

				var thumbHeight = height;
				var thumbWidth = 14;

				InteriorColor = interiorColor;
				ExteriorColor = exteriorColor;

				this.borderColor = borderColor;

				disabledBorderColor = new Color(borderColor, 50);

				ThumbColor = thumbColor;
				LocalBounds = new RectangleDouble(0, 0, width, height);

				this.switchBounds = new RectangleDouble(0, 0, width, height);

				innerRect = this.switchBounds;
				innerRect.Inflate(new BorderDouble(-3, -6));

				borderRect = this.switchBounds;
				borderRect.Inflate(new BorderDouble(0, -3));

				checkedThumbBounds = new RectangleDouble(width - thumbWidth, 0, width, thumbHeight);
				uncheckedThumbBounds = new RectangleDouble(0, 0, thumbWidth, thumbHeight);
			}

			public Color ExteriorColor { get; set; }

			public Color InteriorColor { get; set; }

			public Color ThumbColor { get; set; }

			public override void OnDraw(Graphics2D graphics2D)
			{
				graphics2D.FillRectangle(switchBounds, this.BackgroundColor);
				base.OnDraw(graphics2D);

				if (this.Checked)
				{
					graphics2D.FillRectangle(innerRect, this.InteriorColor);
				}

				// Draw border
				graphics2D.Rectangle(borderRect, (this.Enabled) ? borderColor : disabledBorderColor, 1);

				var thumbBounds = (this.Checked) ? checkedThumbBounds : uncheckedThumbBounds;
				graphics2D.FillRectangle(thumbBounds, this.ThumbColor);
				graphics2D.Rectangle(thumbBounds, new Color(255, 255, 255, 90), 1);
			}
		}
	}
}