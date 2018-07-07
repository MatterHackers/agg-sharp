namespace MatterHackers.Agg.UI
{
	public class GroupBox : GuiWidget
	{
		private GuiWidget groupBoxLabel;
		private double lineInset = 8.5;
		private GuiWidget clientArea;

		public Color TextColor
		{
			get
			{
				TextWidget textBox = groupBoxLabel as TextWidget;
				if (textBox != null)
				{
					return textBox.TextColor;
				}
				return Color.White;
			}
			set
			{
				TextWidget textBox = groupBoxLabel as TextWidget;
				if (textBox != null)
				{
					textBox.TextColor = value;
				}
			}
		}

		public GuiWidget ClientArea
		{
			get
			{
				return clientArea;
			}
		}

		public GroupBox()
			: this("")
		{
		}

		public GroupBox(GuiWidget groupBoxLabel)
		{
			BorderColor = Color.Black;
			HAnchor = HAnchor.Fit;
			VAnchor = VAnchor.Fit;

			this.Padding = new BorderDouble(14, 14, 14, 16);
			groupBoxLabel.Margin = new BorderDouble(20, 0, 0, -this.Padding.Top);
			groupBoxLabel.VAnchor = UI.VAnchor.Top;
			groupBoxLabel.HAnchor = UI.HAnchor.Left;
			base.AddChild(groupBoxLabel);

			this.groupBoxLabel = groupBoxLabel;

			clientArea = new GuiWidget()
			{
				HAnchor = HAnchor.MaxFitOrStretch,
				VAnchor = VAnchor.MaxFitOrStretch
			};
			base.AddChild(clientArea);
		}

		public GroupBox(string title)
			: this(new TextWidget(title))
		{
		}

		public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			clientArea.AddChild(childToAdd, indexInChildrenList);
		}

		public override string Text
		{
			get
			{
				return groupBoxLabel.Text;
			}
			set
			{
				groupBoxLabel.Text = value;
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			RectangleDouble localBounds = LocalBounds;
			// bottom
			graphics2D.Line(localBounds.Left + lineInset, localBounds.Bottom + lineInset, localBounds.Left + Width - lineInset, localBounds.Bottom + lineInset, this.BorderColor);
			// left
			graphics2D.Line(localBounds.Left + lineInset, localBounds.Bottom + lineInset, localBounds.Left + lineInset, localBounds.Bottom + Height - lineInset, this.BorderColor);
			// right
			graphics2D.Line(localBounds.Left + Width - lineInset, localBounds.Bottom + lineInset, localBounds.Left + Width - lineInset, localBounds.Bottom + Height - lineInset, this.BorderColor);
			// top
			graphics2D.Line(localBounds.Left + lineInset, localBounds.Bottom + Height - lineInset, groupBoxLabel.BoundsRelativeToParent.Left - 2, localBounds.Bottom + Height - lineInset, this.BorderColor);
			graphics2D.Line(groupBoxLabel.BoundsRelativeToParent.Right + 2, localBounds.Bottom + Height - lineInset, localBounds.Left + Width - lineInset, localBounds.Bottom + Height - lineInset, this.BorderColor);

			base.OnDraw(graphics2D);
		}
	}
}