using MatterHackers.VectorMath;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# Port port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
using System;

namespace MatterHackers.Agg.UI
{
	public class CheckBoxViewText : GuiWidget
	{
		public static BorderDouble DefaultPadding; //= new BorderDouble(5);

		private double CheckBoxWidth = 10 * TextWidget.GlobalPointSizeScaleRatio;
		private RGBA_Bytes inactiveColor;
		private RGBA_Bytes activeColor;

		public RGBA_Bytes CheckColor = DefaultViewFactory.SelectBlue;

		protected TextWidget labelTextWidget;

		public CheckBoxViewText(string label, double textHeight = 12, RGBA_Bytes textColor = new RGBA_Bytes())
		{
			FlowLayoutWidget leftToRight = new FlowLayoutWidget();
			GuiWidget boxSpace = new GuiWidget(CheckBoxWidth * 2, 1);
			leftToRight.AddChild(boxSpace);

			labelTextWidget = new TextWidget(label, CheckBoxWidth, 0, textHeight);
			leftToRight.AddChild(labelTextWidget);

			AddChild(leftToRight);
			AnchorAll();

			Padding = DefaultPadding;
			HAnchor = UI.HAnchor.FitToChildren;
			VAnchor = UI.VAnchor.FitToChildren;

			if (textColor.Alpha0To1 > 0)
			{
				TextColor = textColor;
			}
		}

		public override void OnParentChanged(EventArgs e)
		{
			GuiWidget parentButton = Parent;

			parentButton.TextChanged += new EventHandler(parentButton_TextChanged);

			parentButton.MouseEnter += redrawButtonIfRequired;
			parentButton.MouseDown += redrawButtonIfRequired;
			parentButton.MouseUp += redrawButtonIfRequired;
			parentButton.MouseLeave += redrawButtonIfRequired;

			base.OnParentChanged(e);
		}

		private void parentButton_TextChanged(object sender, EventArgs e)
		{
			labelTextWidget.Text = ((GuiWidget)sender).Text;

			SetBoundsToEncloseChildren();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			CheckBox checkBox = (CheckBox)Parent;

			double bottom = 0;

			// the check
			if (checkBox.Checked)
			{
				graphics2D.Line(new Vector2(1, CheckBoxWidth + bottom), new Vector2(1 + CheckBoxWidth, 0 + bottom), this.TextColor);
				graphics2D.Line(new Vector2(1, 0 + bottom), new Vector2(1 + CheckBoxWidth, CheckBoxWidth + bottom), this.TextColor);
			}

			// the frame
			RectangleDouble clampedRect = new RectangleDouble(1, Math.Floor(0 + bottom), 1 + Math.Ceiling(CheckBoxWidth), Math.Ceiling(CheckBoxWidth + bottom));
			graphics2D.Rectangle(clampedRect, this.TextColor);

			// extra frame
			if (checkBox.MouseDownOnButton && checkBox.FirstWidgetUnderMouse)
			{
				clampedRect.Inflate(1);
				graphics2D.Rectangle(clampedRect, this.TextColor);
			}

			base.OnDraw(graphics2D);
		}

		public void inactive_color(IColorType c)
		{
			inactiveColor = c.GetAsRGBA_Bytes();
		}

		public void active_color(IColorType c)
		{
			activeColor = c.GetAsRGBA_Bytes();
		}

		public RGBA_Bytes TextColor
		{
			get
			{
				return labelTextWidget.TextColor;
			}

			set
			{
				labelTextWidget.TextColor = value;
			}
		}

		public void redrawButtonIfRequired(object sender, EventArgs e)
		{
			((GuiWidget)sender).Invalidate();
		}
	}
}