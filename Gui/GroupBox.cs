namespace MatterHackers.Agg.UI
{
	public class GroupBox : GuiWidget
	{
		private GuiWidget groupBoxLabel;
		private double lineInset = 8.5;
		private RGBA_Bytes borderColor = RGBA_Bytes.Black;
		private GuiWidget clientArea;

		public RGBA_Bytes TextColor
		{
			get
			{
				TextWidget textBox = groupBoxLabel as TextWidget;
				if (textBox != null)
				{
					return textBox.TextColor;
				}
				return RGBA_Bytes.White;
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

		public RGBA_Bytes BorderColor
		{
			get
			{
				return this.borderColor;
			}
			set
			{
				this.borderColor = value;
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
			HAnchor = HAnchor.FitToChildren;
			VAnchor = VAnchor.FitToChildren;

			this.Padding = new BorderDouble(14, 14, 14, 16);
			groupBoxLabel.Margin = new BorderDouble(20, 0, 0, -this.Padding.Top);
			groupBoxLabel.VAnchor = UI.VAnchor.ParentTop;
			groupBoxLabel.HAnchor = UI.HAnchor.ParentLeft;
			base.AddChild(groupBoxLabel);

			this.groupBoxLabel = groupBoxLabel;

			clientArea = new GuiWidget()
			{
				HAnchor = HAnchor.Max_FitToChildren_ParentWidth,
				VAnchor = VAnchor.Max_FitToChildren_ParentHeight
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
			graphics2D.Line(localBounds.Left + lineInset, localBounds.Bottom + lineInset, localBounds.Left + Width - lineInset, localBounds.Bottom + lineInset, this.borderColor);
			// left
			graphics2D.Line(localBounds.Left + lineInset, localBounds.Bottom + lineInset, localBounds.Left + lineInset, localBounds.Bottom + Height - lineInset, this.borderColor);
			// right
			graphics2D.Line(localBounds.Left + Width - lineInset, localBounds.Bottom + lineInset, localBounds.Left + Width - lineInset, localBounds.Bottom + Height - lineInset, this.borderColor);
			// top
			graphics2D.Line(localBounds.Left + lineInset, localBounds.Bottom + Height - lineInset, groupBoxLabel.BoundsRelativeToParent.Left - 2, localBounds.Bottom + Height - lineInset, this.borderColor);
			graphics2D.Line(groupBoxLabel.BoundsRelativeToParent.Right + 2, localBounds.Bottom + Height - lineInset, localBounds.Left + Width - lineInset, localBounds.Bottom + Height - lineInset, this.borderColor);

			base.OnDraw(graphics2D);
		}
	}
}