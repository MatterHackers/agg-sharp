using MatterHackers.Agg.VertexSource;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2007 Lars Brubaker
//                  larsbrubaker@gmail.com
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
// classes ButtonWidget
//
//----------------------------------------------------------------------------
using System;

namespace MatterHackers.Agg.UI
{
	public class ButtonViewText : GuiWidget
	{
		protected double borderWidth;
		protected double borderRadius;

		public double BorderWidth { get { return borderWidth; } set { borderWidth = value; } }

		public double BorderRadius { get { return borderRadius; } set { borderRadius = value; } }

		protected TextWidget buttonText;

		public static BorderDouble DefaultPadding = new BorderDouble(5);

		public ButtonViewText(string label, double textHeight = 16, double borderWidth = 3, double borderRadius = 5)
		{
			BorderRadius = borderRadius;
			this.borderWidth = borderWidth;
			buttonText = new TextWidget(label, textHeight);
			buttonText.VAnchor = VAnchor.Center;
			buttonText.HAnchor = HAnchor.Center;

			AnchorAll();

			AddChild(buttonText);

			Padding = DefaultPadding;

			SetBoundsToEncloseChildren();
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
			buttonText.Text = ((GuiWidget)sender).Text;

			SetBoundsToEncloseChildren();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			Button parentButton = (Button)Parent;

			RectangleDouble Bounds = LocalBounds;

			RoundedRect rectBorder = new RoundedRect(Bounds, BorderRadius);
			if (parentButton.Enabled == true)
			{
				graphics2D.Render(rectBorder, new RGBA_Bytes(0, 0, 0));
			}
			else
			{
				graphics2D.Render(rectBorder, new RGBA_Bytes(128, 128, 128));
			}
			RectangleDouble insideBounds = Bounds;
			insideBounds.Inflate(-BorderWidth);
			RoundedRect rectInside = new RoundedRect(insideBounds, Math.Max(BorderRadius - BorderWidth, 0));
			RGBA_Bytes insideColor = new RGBA_Bytes(1.0, 1.0, 1.0);
			if (parentButton.FirstWidgetUnderMouse)
			{
				if (parentButton.MouseDownOnButton)
				{
					insideColor = DefaultViewFactory.DefaultBlue;
				}
				else
				{
					insideColor = DefaultViewFactory.DefaultBlue.GetAsRGBA_Floats().Blend(RGBA_Floats.White, .75).GetAsRGBA_Bytes();
				}
			}

			graphics2D.Render(rectInside, insideColor);

			base.OnDraw(graphics2D);
		}

		public void redrawButtonIfRequired(object sender, EventArgs e)
		{
			((GuiWidget)sender).Invalidate();
		}
	}
}