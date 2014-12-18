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

using System;

using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class ToggleSwitch : GuiWidget
	{
		TextWidget associatedToggleSwitchTextWidget;//points to optional text widget that can be passed in with associated T/F value
		internal string trueText;           		 //Text associated with switch on true value
		internal string falseText;					 //Text associated with switch on false value

		public RGBA_Bytes InteriorColor{ get; set; }
		public RGBA_Bytes ExteriorColor{ get; set; }
		public RGBA_Bytes ThumbColor { get; set; }

		double switchHeight;
		double switchWidth;
		double thumbWidth;
		double thumbHeight;

        public event EventHandler SwitchStateChanged;
        
        bool switchState;
        public bool SwitchState
        {
            get { return switchState; }
            set
            {
                if (switchState != value)
                {
                    switchState = value;
                    SetAssociatedTextWidgetText();
                    OnSwitchStateChanged(null);
                }
            }
        }

		bool mouseDownOnToggle;
		bool mouseMoveOnToggle;

		public string TrueText 
		{
			get{ return trueText; }
			set{ trueText = value; }
		}

		public string FalseText
		{
			get{ return falseText; }
			set{ falseText = value; }
		}

		public ToggleSwitch (double width = 50, double height = 20,bool startValue = false, RGBA_Bytes backgroundColor = new RGBA_Bytes(), RGBA_Bytes interiorColor = new RGBA_Bytes(), RGBA_Bytes thumbColor = new RGBA_Bytes(), RGBA_Bytes exteriorColor = new RGBA_Bytes())
			:base(width,height)
		{
			switchWidth = width;
			switchHeight = height;
			thumbHeight = height;
			thumbWidth = width / 4;
			InteriorColor = interiorColor;
			ExteriorColor = exteriorColor;
			ThumbColor = thumbColor;
			mouseDownOnToggle = false;
			mouseMoveOnToggle = false;
			this.SwitchState = startValue;
		}

		public ToggleSwitch (TextWidget associatedToggleSwitchTextWidget,string trueText = "On", string falseText = "Off", double width = 60, double height = 24,bool startValue = false, RGBA_Bytes backgroundColor = new RGBA_Bytes(), RGBA_Bytes interiorColor = new RGBA_Bytes(), RGBA_Bytes thumbColor = new RGBA_Bytes(), RGBA_Bytes exteriorColor = new RGBA_Bytes())
			:this(width,height,startValue,backgroundColor,interiorColor,thumbColor,exteriorColor)
		{
			this.associatedToggleSwitchTextWidget = associatedToggleSwitchTextWidget;
			this.trueText = trueText;
			this.falseText = falseText;
		}

		public ToggleSwitch ( ToggleSwitch toggleSwitch)
			:this(toggleSwitch.associatedToggleSwitchTextWidget, toggleSwitch.trueText, toggleSwitch.falseText, toggleSwitch.switchWidth, toggleSwitch.switchHeight,toggleSwitch.SwitchState ,toggleSwitch.BackgroundColor, toggleSwitch.InteriorColor, toggleSwitch.ThumbColor, toggleSwitch.ExteriorColor)
		{
		}

        void OnSwitchStateChanged(EventArgs e)
        {
            if (SwitchStateChanged != null)
            {
                SwitchStateChanged(this, e);
            }
        }

		RectangleDouble GetSwitchBounds()
		{
			RectangleDouble switchBounds;
			switchBounds = new RectangleDouble (0, 0, switchWidth, switchHeight);
			return switchBounds;
		}

		RectangleDouble GetThumbBounds()
		{
			RectangleDouble thumbBounds;
			if (SwitchState) 
			{
				thumbBounds = new RectangleDouble (switchWidth - thumbWidth,0,switchWidth,thumbHeight);
			}
			else 
			{
				thumbBounds = new RectangleDouble (0,0,thumbWidth,thumbHeight);
			}

			return thumbBounds;
		}

		public void DrawBeforeChildren(Graphics2D graphics2D)
		{
			graphics2D.FillRectangle (GetSwitchBounds (), BackgroundColor);
		}

		public void DrawAfterChildren(Graphics2D graphics2D)
		{

			if (SwitchState) 
			{
				RectangleDouble interior = GetSwitchBounds ();
				interior.Inflate (-6);			
				graphics2D.FillRectangle (interior, InteriorColor);
			}
			RectangleDouble border = GetSwitchBounds ();
			border.Inflate (-3);
			graphics2D.Rectangle (border, ExteriorColor,1);
			graphics2D.FillRectangle (GetThumbBounds(), ThumbColor);
            graphics2D.Rectangle(GetThumbBounds(), new RGBA_Bytes(255,255,255,90), 1);

		}

		public override void OnDraw (Graphics2D graphics2D)
		{
			base.OnDraw (graphics2D);
			DrawAfterChildren (graphics2D);
		}

		void SetAssociatedTextWidgetText()
		{
			if (associatedToggleSwitchTextWidget != null)
			{
				if (SwitchState) 
				{
					associatedToggleSwitchTextWidget.Text = trueText;
				}
				else 
				{
					associatedToggleSwitchTextWidget.Text = falseText;
				}
			}
		}

		public override void OnMouseDown (MouseEventArgs mouseEvent)
		{
			RectangleDouble switchBounds = GetSwitchBounds();
			Vector2 mousePosition = mouseEvent.Position;

			if(switchBounds.Contains (mousePosition)) 
			{
				mouseDownOnToggle = true;
			}

			base.OnMouseDown (mouseEvent);
		}

		public override void OnMouseMove (MouseEventArgs mouseEvent)
		{
			bool oldValue = SwitchState;
			if (mouseDownOnToggle) {
				mouseMoveOnToggle = true;
				RectangleDouble switchBounds = GetSwitchBounds();
				Vector2 mousePosition = mouseEvent.Position;
				SwitchState = switchBounds.XCenter < mousePosition.x;
				if (oldValue != SwitchState) 
				{
					if (SwitchStateChanged != null)
					{
						SwitchStateChanged(this, mouseEvent);
					}
					Invalidate();
				}
			}
			base.OnMouseMove (mouseEvent);
		}

		public override void OnMouseUp (MouseEventArgs mouseEvent)
		{
			if (!mouseMoveOnToggle) 
			{
				SwitchState = !SwitchState;
				if (SwitchStateChanged != null)
				{
					SwitchStateChanged(this, mouseEvent);
				}
				Invalidate();
			}

			mouseDownOnToggle = false;
			mouseMoveOnToggle = false;
			base.OnMouseUp (mouseEvent);
		}
	}
}

