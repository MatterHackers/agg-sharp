using System;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class RadioCircleWidget : GuiWidget
	{
		private double boxWidth = 10 * GuiWidget.DeviceScale;
		private double borderRadius;
		private Vector2 center;

		public RadioCircleWidget()
		{
			this.MinimumSize = new Vector2(boxWidth + 1, boxWidth + 1);
			this.DoubleBuffer = true;
			this.Margin = new BorderDouble(right: 10);

			center = this.LocalBounds.Center;
			borderRadius = boxWidth / 2;
		}

		public RadioButton RadioButton { get; set; }

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (this.RadioButton == null)
			{
				return;
			}

			// Radio check
			if (this.RadioButton.Checked)
			{
				graphics2D.Circle(center, boxWidth / 4, this.RadioButton.TextColor);
			}

			// Radio border
			int strokeWidth = (this.RadioButton.MouseDownOnWidget && this.RadioButton.FirstWidgetUnderMouse) ? 2 : 1;
			graphics2D.Render(
				new Stroke(new Ellipse(center, borderRadius), strokeWidth), 
				this.RadioButton.TextColor);

			base.OnDraw(graphics2D);
		}
	}
	
	public class RadioButtonViewText : FlowLayoutWidget
	{
		protected TextWidget labelTextWidget;

		protected RadioCircleWidget radioCircle;

		protected RadioButton radioButton;

		public RadioButtonViewText(string label, int fontSize=12)
		{
			radioCircle = new RadioCircleWidget();
			this.AddChild(radioCircle);

			labelTextWidget = new TextWidget(label, fontSize);
			this.AddChild(labelTextWidget);
		}

		public override void OnParentChanged(EventArgs e)
		{
			if (Parent is RadioButton radioButton)
			{
				this.radioButton = radioButton;
				radioButton.MouseEnter += redrawButtonIfRequired;
				radioButton.MouseDown += redrawButtonIfRequired;
				radioButton.MouseUp += redrawButtonIfRequired;
				radioButton.MouseLeave += redrawButtonIfRequired;
				radioButton.CheckedStateChanged += (s, e2) =>
				{
					// Invalidate double buffered control
					radioCircle.Invalidate();
				};

				radioCircle.RadioButton = radioButton;
				radioButton.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			}
			base.OnParentChanged(e);
		}

		public void redrawButtonIfRequired(object sender, EventArgs e)
		{
			((GuiWidget)sender).Invalidate();
		}

		public override string Text
		{
			get => labelTextWidget.Text;
			set => labelTextWidget.Text = value;
		}

		public RGBA_Bytes TextColor
		{
			get => labelTextWidget.TextColor;
			set => labelTextWidget.TextColor = value;
		}
	}
}