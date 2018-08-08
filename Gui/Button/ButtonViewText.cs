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
		public double BorderWidth { get; set; }

		public double BorderRadius { get; set; }

		protected TextWidget buttonText;

		public static BorderDouble DefaultPadding = new BorderDouble(5);

		public ButtonViewText(string label, double textHeight = 16, double borderWidth = 3, double borderRadius = 5)
		{
			this.BorderRadius = borderRadius;
			this.BorderWidth = borderWidth;

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

			parentButton.TextChanged += ParentButton_TextChanged;
			parentButton.MouseEnter += Invalidate_Parent;
			parentButton.MouseDownCaptured += Invalidate_Parent;
			parentButton.MouseUpCaptured += Invalidate_Parent;
			parentButton.MouseLeave += Invalidate_Parent;

			base.OnParentChanged(e);
		}

		private void ParentButton_TextChanged(object sender, EventArgs e)
		{
			buttonText.Text = ((GuiWidget)sender).Text;
			SetBoundsToEncloseChildren();
		}

		private void Invalidate_Parent(object sender, EventArgs e)
		{
			((GuiWidget)sender).Invalidate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			GuiWidget parentButton = Parent;

			RectangleDouble Bounds = LocalBounds;

			RoundedRect rectBorder = new RoundedRect(Bounds, BorderRadius);
			if (parentButton.Enabled == true)
			{
				graphics2D.Render(rectBorder, new Color(0, 0, 0));
			}
			else
			{
				graphics2D.Render(rectBorder, new Color(128, 128, 128));
			}

			RectangleDouble insideBounds = Bounds;
			insideBounds.Inflate(-BorderWidth);

			RoundedRect rectInside = new RoundedRect(insideBounds, Math.Max(BorderRadius - BorderWidth, 0));
			Color insideColor = new Color(1.0, 1.0, 1.0);

			if (parentButton.FirstWidgetUnderMouse)
			{
				if (parentButton.MouseDownOnWidget)
				{
					insideColor = DefaultViewFactory.DefaultBlue;
				}
				else
				{
					insideColor = DefaultViewFactory.DefaultBlue.ToColorF().Blend(ColorF.White, .75).ToColor();
				}
			}

			graphics2D.Render(rectInside, insideColor);

			base.OnDraw(graphics2D);
		}
	}
}