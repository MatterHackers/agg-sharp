using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.Agg.UI
{
	public class RadioButtonViewText : GuiWidget
	{
		private RGBA_Bytes inactiveColor;
		private RGBA_Bytes activeColor;

		private TextWidget labelTextWidget;

		private double boxWidth = 10 * TextWidget.GlobalPointSizeScaleRatio;

		public RadioButtonViewText(string label)
		{
			labelTextWidget = new TextWidget(label, 12);
			AddChild(labelTextWidget);

			LocalBounds = GetLocalBounds();

			inactiveColor = new RGBA_Bytes(0.0, 0.0, 0.0);
			activeColor = new RGBA_Bytes(0.4, 0.0, 0.0);
		}

		public override void OnParentChanged(EventArgs e)
		{
			GuiWidget radioButton = Parent;

			radioButton.MouseEnter += redrawButtonIfRequired;
			radioButton.MouseDown += redrawButtonIfRequired;
			radioButton.MouseUp += redrawButtonIfRequired;
			radioButton.MouseLeave += redrawButtonIfRequired;

			base.OnParentChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			DoDrawBeforeChildren(graphics2D);
			base.OnDraw(graphics2D);
		}

		public void redrawButtonIfRequired(object sender, EventArgs e)
		{
			((GuiWidget)sender).Invalidate();
		}

		public void DoDrawBeforeChildren(Graphics2D graphics2D)
		{
			RadioButton radioButton = Parent as RadioButton;
			if (radioButton == null)
			{
				return;
			}
			Vector2 center = new Vector2(boxWidth / 2 + 1, boxWidth / 2 - labelTextWidget.Printer.TypeFaceStyle.DescentInPixels);

			// the check
			if (radioButton.Checked)
			{
				graphics2D.Circle(center, boxWidth / 4, radioButton.TextColor);
			}

			if (radioButton.MouseDownOnButton && radioButton.FirstWidgetUnderMouse)
			{
				// extra frame
				graphics2D.Render(new Stroke(new Ellipse(center, boxWidth / 2, boxWidth / 2), 2), radioButton.TextColor);
			}
			else
			{
				// the frame
				graphics2D.Render(new Stroke(new Ellipse(center, boxWidth / 2, boxWidth / 2)), radioButton.TextColor);
			}
		}

		public override string Text
		{
			get
			{
				return labelTextWidget.Text;
			}

			set
			{
				labelTextWidget.Text = value;
			}
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

		public void inactive_color(IColorType c)
		{
			inactiveColor = c.GetAsRGBA_Bytes();
		}

		public void active_color(IColorType c)
		{
			activeColor = c.GetAsRGBA_Bytes();
		}

		internal RectangleDouble GetLocalBounds()
		{
			labelTextWidget.OriginRelativeParent = new Vector2(boxWidth * 2, -labelTextWidget.Printer.TypeFaceStyle.DescentInPixels);

			RectangleDouble localBounds = new RectangleDouble();
			localBounds.Left = 0;
			localBounds.Bottom = 0;
			localBounds.Right = localBounds.Left + boxWidth * 2 + labelTextWidget.Width;
			localBounds.Top = localBounds.Bottom + labelTextWidget.Height;

			return localBounds;
		}
	}
}