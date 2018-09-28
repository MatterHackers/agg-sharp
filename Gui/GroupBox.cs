/*
Copyright (c) 2018, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;

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

		public override void OnTextChanged(EventArgs e)
		{
			groupBoxLabel.Text = Text;
			base.OnTextChanged(e);
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