﻿using System;

namespace MatterHackers.Agg.UI
{
	public class RadioButtonViewStates : GuiWidget
	{
		private GuiWidget uncheckedWidget;
		private GuiWidget uncheckedHoverWidget;
		private GuiWidget checkingWidget;
		private GuiWidget checkedWidget;

		private GuiWidget disabledWidget;

		public RadioButtonViewStates(GuiWidget uncheckedWidget, GuiWidget uncheckedHoverWidget, GuiWidget checkingWidget,
			GuiWidget checkedWidget,
			GuiWidget disabledWidget)
		{
			this.uncheckedWidget = uncheckedWidget;
			this.uncheckedHoverWidget = uncheckedHoverWidget;
			this.checkedWidget = checkedWidget;
			this.checkingWidget = checkingWidget;
			this.disabledWidget = disabledWidget;

			AddChild(uncheckedWidget);
			AddChild(uncheckedHoverWidget);
			AddChild(checkingWidget);
			AddChild(checkedWidget);
			AddChild(disabledWidget);

			SetBoundsToEncloseChildren();
		}

		public override void OnParentChanged(EventArgs e)
		{
			RadioButton radioButton = (RadioButton)this.Parent;

			radioButton.MouseEnter += setCorrectVisibility;
			radioButton.MouseDown += setCorrectVisibility;
			radioButton.MouseUp += setCorrectVisibility;
			radioButton.MouseLeave += setCorrectVisibility;
			radioButton.CheckedStateChanged += setCorrectVisibility;
			setCorrectVisibility(radioButton, null);

			base.OnParentChanged(e);
		}

		public void setCorrectVisibility(object sender, EventArgs e)
		{
			RadioButton radioButton = this.Parent as RadioButton;
			if (radioButton != null) // This can be null if the button has been closed.
			{
				SuspendLayout();

				if (Enabled)
				{
					if (radioButton.Checked)
					{
						uncheckedWidget.Visible = false;
						uncheckedHoverWidget.Visible = false;
						checkingWidget.Visible = false;
						disabledWidget.Visible = false;

						checkedWidget.Visible = true;
					}
					else
					{
						if (radioButton.UnderMouseState != UI.UnderMouseState.FirstUnderMouse)
						{
							uncheckedHoverWidget.Visible = false;
							checkedWidget.Visible = false;
							checkingWidget.Visible = false;
							disabledWidget.Visible = false;

							uncheckedWidget.Visible = true;
						}
						else
						{
							if (radioButton.MouseDownOnWidget)
							{
								uncheckedWidget.Visible = false;
								uncheckedHoverWidget.Visible = false;
								checkedWidget.Visible = false;
								disabledWidget.Visible = false;

								checkingWidget.Visible = true;
							}
							else
							{
								uncheckedWidget.Visible = false;
								checkedWidget.Visible = false;
								checkingWidget.Visible = false;
								disabledWidget.Visible = false;

								uncheckedHoverWidget.Visible = true;
							}
						}
					}
				}
				else
				{
					uncheckedWidget.Visible = false;
					uncheckedHoverWidget.Visible = false;
					checkedWidget.Visible = false;
					checkingWidget.Visible = false;

					disabledWidget.Visible = true;
				}

				ResumeLayout();
				Invalidate();
			}
		}
	}
}