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

			this.MinimumSize = new Vector2(boxWidth + 1 * DeviceScale, boxWidth + 1 * DeviceScale);

			// Not double buffered: the circle's colours come from ThemeConfig.Current when it draws, and a
			// cached copy would keep showing the old theme (and the old hover state) after either changed.
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

			bool hovered = this.RadioButton.FirstWidgetUnderMouse;
			SelectionControlStyle.DrawRadio(
				graphics2D,
				center,
				RadioImage.BorderRadius,
				this.RadioButton.Checked,
				hovered,
				pressed: hovered && this.RadioButton.MouseDownOnWidget,
				this.RadioButton.Enabled,
				ThemeConfig.Current);

			base.OnDraw(graphics2D);
		}
	}

	public class RadioButtonViewText : RadioButtonView
	{
		protected TextWidget labelTextWidget;

		public RadioButtonViewText(string label, int fontSize = 12)
			: this(label, Color.Black, fontSize)
		{
		}

		public RadioButtonViewText(string label, Color textColor, int fontSize=12)
		{
			this.AddChild(labelTextWidget = new TextWidget(label, textColor: textColor, pointSize: fontSize));
		}

		public override string Text
		{
			get => labelTextWidget.Text;
			set => labelTextWidget.Text = value;
		}

		public override Color TextColor
		{
			get => labelTextWidget.TextColor;
			set => labelTextWidget.TextColor = value;
		}
	}

	public class RadioButtonView : FlowLayoutWidget
	{
		protected RadioCircleWidget radioCircle;

		protected RadioButton radioButton;

		public RadioButtonView()
		{
			radioCircle = new RadioCircleWidget()
			{
				VAnchor = VAnchor.Center
			};
			this.AddChild(radioCircle);
		}

		public RadioButtonView(GuiWidget view) : this()
		{
			this.AddChild(view);
		}

		public RadioCircleWidget RadioCircle => radioCircle;

		public virtual Color TextColor { get; set; } = Color.Black;

		public override void OnParentChanged(EventArgs e)
		{
			// TODO: This looks to leak if parents change...
			if (Parent is RadioButton radioButton)
			{
				this.radioButton = radioButton;
				radioButton.MouseEnter += redrawButtonIfRequired;
				radioButton.MouseDownCaptured += redrawButtonIfRequired;
				radioButton.MouseUpCaptured += redrawButtonIfRequired;
				radioButton.MouseLeave += redrawButtonIfRequired;
				radioButton.CheckedStateChanged += (s, e2) =>
				{
					// Invalidate double buffered control
					radioCircle.Invalidate();
				};

				radioCircle.RadioButton = radioButton;
				radioButton.TextColor = this.TextColor;
			}
			base.OnParentChanged(e);
		}

		public void redrawButtonIfRequired(object sender, EventArgs e)
		{
			((GuiWidget)sender).Invalidate();
		}
	}

	/// <summary>
	/// The radio circle's metrics, and its original look: a stroked circle with a dot in one colour.
	/// RadioCircleWidget now draws agg-gui's themed circle through SelectionControlStyle; DrawCircle is kept
	/// for callers that draw the old mark themselves.
	/// </summary>
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
			var strokeWidth = Math.Max(1, Math.Round(1 * GuiWidget.DeviceScale));
			if (isActive)
			{
				strokeWidth *= 2;
			}

			graphics2D.Render(
				new Stroke(new Ellipse(center, BorderRadius), strokeWidth),
				color);
		}
	}
}