/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg.Font;
using System;

namespace MatterHackers.Agg.UI
{
	public class WrappedTextWidget : GuiWidget
	{
		private string unwrappedText;
		public TextWidget TextWidget { get; }
		private double pointSize;
		private double wrappedWidth = -1;

		public WrappedTextWidget(string text, double pointSize = 12, Justification justification = Justification.Left,
			Color textColor = new Color(), bool ellipsisIfClipped = true, bool underline = false, Color backgroundColor = new Color(), bool doubleBufferText = true)
		{
			using (this.LayoutLock())
			{
				this.pointSize = pointSize;
				TextWidget = new TextWidget("", 0, 0, pointSize, justification, textColor, ellipsisIfClipped, underline, backgroundColor)
				{
					DoubleBuffer = doubleBufferText,
				};
				TextWidget.AutoExpandBoundsToText = true;
				TextWidget.HAnchor = HAnchor.Left;
				TextWidget.VAnchor = VAnchor.Center | VAnchor.Fit;
				unwrappedText = text;
				HAnchor = HAnchor.Stretch;
				VAnchor = VAnchor.Fit;
				AddChild(TextWidget);
			}

			this.PerformLayout();
		}

		public Color TextColor
		{
			get { return TextWidget.TextColor; }
			set { TextWidget.TextColor = value; }
		}

		public bool DrawFromHintedCache
		{
			get
			{
				return TextWidget.Printer.DrawFromHintedCache;
			}
			set
			{
				TextWidget.Printer.DrawFromHintedCache = value;
			}
		}

		public override string Text
		{
			get => unwrappedText;
			set
			{
				if (unwrappedText != value)
				{
					unwrappedText = value;
					this.AdjustTextWrap();
				}
			}
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			if (wrappedWidth != Width)
			{
				AdjustTextWrap();
			}

			base.OnBoundsChanged(e);
		}

		private void AdjustTextWrap()
		{
			if (TextWidget != null
				&& !string.IsNullOrEmpty(unwrappedText))
			{
				if (Width > 0)
				{
					EnglishTextWrapping wrapper = new EnglishTextWrapping(TextWidget.Printer.TypeFaceStyle.EmSizeInPoints);
					string wrappedMessage = wrapper.InsertCRs(unwrappedText, Width);
					wrappedWidth = Width;
					TextWidget.Text = wrappedMessage;
				}
			}
		}
	}
}