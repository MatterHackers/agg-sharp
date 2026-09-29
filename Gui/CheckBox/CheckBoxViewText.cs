using MatterHackers.VectorMath;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026
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

		private double CheckBoxWidth = 10 * GuiWidget.DeviceScale;

		// SelectionControlSize.BoxSize as it was when this was built, in device pixels; null keeps the classic box.
		private readonly double? boxSize = SelectionControlSize.BoxSize * GuiWidget.DeviceScale;
		private Color inactiveColor;
		private Color activeColor;

		public Color CheckColor = DefaultViewFactory.SelectBlue;

		protected TextWidget labelTextWidget;

		public CheckBoxViewText(string label, double textHeight = 12, Color textColor = new Color())
		{
			FlowLayoutWidget leftToRight = new FlowLayoutWidget();
			double slotWidth = boxSize is double size
				? size + (SelectionControlSize.LeadingPad + SelectionControlSize.LabelGap) * DeviceScale
				: CheckBoxWidth * 2;
			GuiWidget boxSpace = new GuiWidget(slotWidth, 1)
			{ 
				VAnchor = VAnchor.Center,
			};
			leftToRight.AddChild(boxSpace);

			labelTextWidget = new TextWidget(label, CheckBoxWidth, 0, textHeight);
			leftToRight.AddChild(labelTextWidget);

			AddChild(leftToRight);
			AnchorAll();

			Padding = DefaultPadding;
			HAnchor = UI.HAnchor.Fit;
			VAnchor = UI.VAnchor.Fit;

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
			parentButton.MouseDownCaptured += redrawButtonIfRequired;
			parentButton.MouseUpCaptured += redrawButtonIfRequired;
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
			if (Parent is CheckBox checkBox)
			{
				// agg-gui's box is 16 with a 2 focus pad in a slot as wide as the label gap; here the slot is
				// CheckBoxWidth * 2 (the label's position, which layouts depend on), so the box is 12 with the
				// same 2 pad, leaving a 6 gap before the label.
				double boxSize = this.boxSize ?? 1.2 * CheckBoxWidth;
				double left = LocalBounds.Left + SelectionControlSize.LeadingPad * DeviceScale;
				double bottom = Math.Round(LocalBounds.Bottom + (Height - boxSize) / 2);
				var box = new RectangleDouble(left, bottom, left + boxSize, bottom + boxSize);

				bool hovered = checkBox.FirstWidgetUnderMouse;
				SelectionControlStyle.DrawCheckBox(graphics2D, box, checkBox.Checked, hovered, hovered && checkBox.MouseDownOnWidget, checkBox.Enabled, ThemeConfig.Current, checkBox.Indeterminate);
			}

			base.OnDraw(graphics2D);
		}

		public void inactive_color(IColorType c)
		{
			inactiveColor = c.ToColor();
		}

		public void active_color(IColorType c)
		{
			activeColor = c.ToColor();
		}

		public Color TextColor
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