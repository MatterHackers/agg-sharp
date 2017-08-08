/*
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
		string onText = "";

		public ToggleSwitchView(string onText, string offText, double width, double height,
			RGBA_Bytes backgroundColor, RGBA_Bytes interiorColor, RGBA_Bytes thumbColor, RGBA_Bytes textColor)
		{
			this.onText = onText;
			GuiWidget normal = createState(offText, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor);
			GuiWidget normalHover = createState(offText, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor);
			GuiWidget switchNormalToPressed = createState(onText, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor);
			GuiWidget pressed = createState(onText, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor);
			GuiWidget pressedHover = createState(onText, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor);
			GuiWidget switchPressedToNormal = createState(offText, width, height, ref backgroundColor, ref interiorColor, ref thumbColor, ref textColor);
			GuiWidget disabled = new TextWidget("disabled");
			SetViewStates(normal, normalHover, switchNormalToPressed, pressed, pressedHover, switchPressedToNormal, disabled);
			this.VAnchor = VAnchor.Fit;
		}

		private GuiWidget createState(string word, double width, double height, ref RGBA_Bytes backgroundColor, ref RGBA_Bytes interiorColor, ref RGBA_Bytes thumbColor, ref RGBA_Bytes textColor)
		{
			TextWidget text = new TextWidget(word, pointSize: 10, textColor: textColor);
			text.VAnchor = VAnchor.Center;

			SwitchView switchGraphics = new SwitchView(width, height, word == onText, backgroundColor, interiorColor, thumbColor, textColor);
			switchGraphics.VAnchor = VAnchor.Center;
			switchGraphics.Margin = new BorderDouble(5, 0, 0, 0);

			GuiWidget switchNormalToPressed = new FlowLayoutWidget(FlowDirection.LeftToRight);
			switchNormalToPressed.AddChild(text);
			switchNormalToPressed.AddChild(switchGraphics);

			return switchNormalToPressed;
		}

		internal class SwitchView : GuiWidget
		{
			private double switchHeight;

			private double switchWidth;

			private double thumbHeight;

			private double thumbWidth;
			bool startValue;

			internal SwitchView(double width, double height, bool startValue, 
				RGBA_Bytes backgroundColor, RGBA_Bytes interiorColor, RGBA_Bytes thumbColor, RGBA_Bytes exteriorColor)
			{
				this.startValue = startValue;
				switchWidth = width;
				switchHeight = height;
				thumbHeight = height;
				thumbWidth = width / 4;
				InteriorColor = interiorColor;
				ExteriorColor = exteriorColor;
				ThumbColor = thumbColor;
				LocalBounds = new RectangleDouble(0, 0, width, height);
			}

			public RGBA_Bytes ExteriorColor { get; set; }

			public RGBA_Bytes InteriorColor { get; set; }
			public RGBA_Bytes ThumbColor { get; set; }

			public override void OnDraw(Graphics2D graphics2D)
			{
				graphics2D.FillRectangle(GetSwitchBounds(), BackgroundColor);
				base.OnDraw(graphics2D);
				if (startValue)
				{
					RectangleDouble interior = GetSwitchBounds();
					interior.Inflate(-6);
					graphics2D.FillRectangle(interior, InteriorColor);
				}
				RectangleDouble border = GetSwitchBounds();
				border.Inflate(-3);
				graphics2D.Rectangle(border, ExteriorColor, 1);
				graphics2D.FillRectangle(GetThumbBounds(), ThumbColor);
				graphics2D.Rectangle(GetThumbBounds(), new RGBA_Bytes(255, 255, 255, 90), 1);
			}

			private RectangleDouble GetSwitchBounds()
			{
				RectangleDouble switchBounds;
				switchBounds = new RectangleDouble(0, 0, switchWidth, switchHeight);
				return switchBounds;
			}

			private RectangleDouble GetThumbBounds()
			{
				RectangleDouble thumbBounds;
				if (startValue)
				{
					thumbBounds = new RectangleDouble(switchWidth - thumbWidth, 0, switchWidth, thumbHeight);
				}
				else
				{
					thumbBounds = new RectangleDouble(0, 0, thumbWidth, thumbHeight);
				}

				return thumbBounds;
			}
		}
	}
}