using System;

using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class ToggleSwitch : GuiWidget
	{
		TextWidget associatedToggleSwitchTextWidget;//points to optional text widget that can be passed in with associated T/F value
		internal string trueText;           		 //Text associated with switch on true value
		internal string falseText;					 //Text associated with switch on false value

	//	public RGBA_Bytes BackgroundColor { get; set; }
		public RGBA_Bytes InteriorColor{ get; set; }
		public RGBA_Bytes ExteriorColor{ get; set; }
		public RGBA_Bytes ThumbColor { get; set; }

		double switchHeight;
		double switchWidth;
		double thumbWidth;
		double thumbHeight;

		bool value;
		bool mouseDownOnToggle;
		bool mouseMoveOnToggle;

		public event EventHandler valueChanged;

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
		//	BackgroundColor = backgroundColor;
			InteriorColor = interiorColor;
			ExteriorColor = exteriorColor;
			ThumbColor = thumbColor;
			mouseDownOnToggle = false;
			mouseMoveOnToggle = false;
			valueChanged += new EventHandler(toggleSwitch_ValueChanged);
			this.value = startValue;
		}

		public ToggleSwitch (TextWidget associatedToggleSwitchTextWidget,string trueText = "On", string falseText = "Off", double width = 60, double height = 24,bool startValue = false, RGBA_Bytes backgroundColor = new RGBA_Bytes(), RGBA_Bytes interiorColor = new RGBA_Bytes(), RGBA_Bytes thumbColor = new RGBA_Bytes(), RGBA_Bytes exteriorColor = new RGBA_Bytes())
			:this(width,height,startValue,backgroundColor,interiorColor,thumbColor,exteriorColor)
		{
			this.associatedToggleSwitchTextWidget = associatedToggleSwitchTextWidget;
			this.trueText = trueText;
			this.falseText = falseText;
		}

		public ToggleSwitch ( ToggleSwitch toggleSwitch)
			:this(toggleSwitch.associatedToggleSwitchTextWidget, toggleSwitch.trueText, toggleSwitch.falseText, toggleSwitch.switchWidth, toggleSwitch.switchHeight,toggleSwitch.value ,toggleSwitch.BackgroundColor, toggleSwitch.InteriorColor, toggleSwitch.ThumbColor, toggleSwitch.ExteriorColor)
		{
		}

		RectangleDouble getSwitchBounds()
		{
			RectangleDouble switchBounds;
			switchBounds = new RectangleDouble (0, 0, switchWidth, switchHeight);
			return switchBounds;
		}

		RectangleDouble getThumbBounds()
		{
			RectangleDouble thumbBounds;
			if (value) 
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
			graphics2D.FillRectangle (getSwitchBounds (), BackgroundColor);
		}//endDrawBeforeChildren

		public void DrawAfterChildren(Graphics2D graphics2D)
		{

			if (value) 
			{
				RectangleDouble interior = getSwitchBounds ();
				interior.Inflate (-6);			
				graphics2D.FillRectangle (interior, InteriorColor);
			}
			RectangleDouble border = getSwitchBounds ();
			border.Inflate (-3);
			graphics2D.Rectangle (border, ExteriorColor,1);
			graphics2D.FillRectangle (getThumbBounds(), ThumbColor);
            graphics2D.Rectangle(getThumbBounds(), new RGBA_Bytes(255,255,255,90), 1);

		}//end DrawAfterChildren

		public override void OnDraw (Graphics2D graphics2D)
		{
		//	DrawBeforeChildren (graphics2D);
			base.OnDraw (graphics2D);
			DrawAfterChildren (graphics2D);
		}//end onDraw

		void setAssociatedTextWidgetText()
		{
			if (associatedToggleSwitchTextWidget != null)
			{
				if (value) 
				{
					associatedToggleSwitchTextWidget.Text = trueText;
				}
				else 
				{
					associatedToggleSwitchTextWidget.Text = falseText;
				}
			}
		}//end setAssociatedTextWidgetText

		public override void OnMouseDown (MouseEventArgs mouseEvent)
		{
			RectangleDouble switchBounds = getSwitchBounds();
			Vector2 mousePosition = mouseEvent.Position;

			if(switchBounds.Contains (mousePosition)) 
			{
				mouseDownOnToggle = true;
			}

			base.OnMouseDown (mouseEvent);
		}//end OnMouseDown

		public override void OnMouseMove (MouseEventArgs mouseEvent)
		{
			bool oldValue = value;
			if (mouseDownOnToggle) {
				mouseMoveOnToggle = true;
				RectangleDouble switchBounds = getSwitchBounds();
				Vector2 mousePosition = mouseEvent.Position;
				value = switchBounds.XCenter < mousePosition.x;
				if (oldValue != value) 
				{
					if (valueChanged != null)
					{
						valueChanged(this, mouseEvent);
					}
					Invalidate();
				}
			}
			base.OnMouseMove (mouseEvent);
		}//end OnMouseMove

		public override void OnMouseUp (MouseEventArgs mouseEvent)
		{
			if (!mouseMoveOnToggle) 
			{
				value = !value;
				if (valueChanged != null)
				{
					valueChanged(this, mouseEvent);
				}
				Invalidate();
			}

			mouseDownOnToggle = false;
			mouseMoveOnToggle = false;
			base.OnMouseUp (mouseEvent);
		}//end OnMouseUp

		public bool getValue()
		{
			return value;
		}

		void toggleSwitch_ValueChanged(object sender, EventArgs e)
		{

			ToggleSwitch toggleSwitch = (ToggleSwitch)sender;
			toggleSwitch.setAssociatedTextWidgetText ();
		}
		 
	}//end ToggleSwitch class
}//end namespace

