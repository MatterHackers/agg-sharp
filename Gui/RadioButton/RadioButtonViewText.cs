using System;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class RadioCircleWidget : GuiWidget
	{
		private Vector2 center;

		public RadioCircleWidget()
		{
			var boxWidth = RadioImage.BoxWidth;

			this.MinimumSize = new Vector2(boxWidth + 1, boxWidth + 1);
			this.DoubleBuffer = true;
			this.Margin = new BorderDouble(right: 10);

			center = this.LocalBounds.Center;
		}

		public RadioButton RadioButton { get; set; }

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (this.RadioButton == null)
			{
				return;
			}

			RadioImage.DrawCircle(
				graphics2D,
				center,
				this.RadioButton.TextColor,
				this.RadioButton.Checked,
				isActive: this.RadioButton.MouseDownOnWidget && this.RadioButton.FirstWidgetUnderMouse);

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

			labelTextWidget = new TextWidget(label, pointSize: fontSize);
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

		public Color TextColor
		{
			get => labelTextWidget.TextColor;
			set => labelTextWidget.TextColor = value;
		}
	}

	public static class RadioImage
	{
		public static double BoxWidth => 10 * GuiWidget.DeviceScale;
		public static double BorderRadius => BoxWidth / 2;

		public static void DrawCircle(Graphics2D graphics2D, Vector2 center, Color color, bool isChecked, bool isActive)
		{
			// Radio check
			if (isChecked)
			{
				graphics2D.Circle(center, BoxWidth / 4, color);
			}

			// Radio border
			int strokeWidth = (isActive) ? 2 : 1;
			graphics2D.Render(
				new Stroke(new Ellipse(center, BorderRadius), strokeWidth),
				color);
		}
	}
}