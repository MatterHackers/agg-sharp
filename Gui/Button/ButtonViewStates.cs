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
	public class ButtonViewStates : GuiWidget
	{
		private GuiWidget normalWidget;
		private GuiWidget hoverWidget;
		private GuiWidget pressedWidget;
		private GuiWidget disabledWidget;

		public ButtonViewStates(GuiWidget normal, GuiWidget hover, GuiWidget pressed, GuiWidget disabled)
		{
			Selectable = false;

			normalWidget = normal;
			hoverWidget = hover;
			pressedWidget = pressed;
			disabledWidget = disabled;

			this.AddChild(normal);
			this.AddChild(hover);
			this.AddChild(pressed);
			this.AddChild(disabled);

			hoverWidget.Visible = false;
			pressedWidget.Visible = false;
			normalWidget.Visible = true;
			disabledWidget.Visible = false;

			this.SetBoundsToEncloseChildren();
		}

		public override void OnSizeChanged(EventArgs e)
		{
			normalWidget.Size = Size;
			hoverWidget.Size = Size;
			pressedWidget.Size = Size;
			disabledWidget.Size = Size;

			base.OnSizeChanged(e);
		}

		public override void OnParentChanged(EventArgs e)
		{
			Button parentButton = (Button)Parent;

			parentButton.MouseEnter += redrawButtonIfRequired;
			parentButton.MouseDownCaptured += redrawButtonIfRequired;
			parentButton.MouseUpCaptured += redrawButtonIfRequired;
			parentButton.MouseLeave += redrawButtonIfRequired;
			parentButton.EnabledChanged += redrawButtonIfRequired;

			base.OnParentChanged(e);
		}

		public void redrawButtonIfRequired(object sender, EventArgs e)
		{
			Button parentButton = Parent as Button;
			if (parentButton != null)
			{
				if (!parentButton.Enabled)
				{
					hoverWidget.Visible = false;
					pressedWidget.Visible = false;
					normalWidget.Visible = false;
					disabledWidget.Visible = true;
				}
				else
				{
					if (parentButton.FirstWidgetUnderMouse)
					{
						if (parentButton.MouseDownOnWidget)
						{
							hoverWidget.Visible = false;
							pressedWidget.Visible = true;
							normalWidget.Visible = false;
							disabledWidget.Visible = false;
						}
						else
						{
							hoverWidget.Visible = true;
							pressedWidget.Visible = false;
							normalWidget.Visible = false;
							disabledWidget.Visible = false;
						}
					}
					else
					{
						hoverWidget.Visible = false;
						pressedWidget.Visible = false;
						normalWidget.Visible = true;
						disabledWidget.Visible = false;
					}
				}
				((GuiWidget)sender).Invalidate();
			}
		}
	}
}