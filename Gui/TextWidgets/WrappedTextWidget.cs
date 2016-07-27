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
		private String unwrappedText;
		private TextWidget textWidget;
		private double pointSize;
		private double wrappedWidth = -1;

		public WrappedTextWidget(string text, double startingWidth,
			double pointSize = 12, Justification justification = Justification.Left,
			RGBA_Bytes textColor = new RGBA_Bytes(), bool ellipsisIfClipped = true, bool underline = false, RGBA_Bytes backgroundColor = new RGBA_Bytes(), bool doubleBufferText = true)
		{
			this.pointSize = pointSize;
			textWidget = new TextWidget(text, 0, 0, pointSize, justification, textColor, ellipsisIfClipped, underline, backgroundColor)
			{
				DoubleBuffer = doubleBufferText,
			};
			textWidget.AutoExpandBoundsToText = true;
			textWidget.HAnchor = HAnchor.ParentLeft;
			textWidget.VAnchor = VAnchor.ParentCenter;
			unwrappedText = text;
			HAnchor = HAnchor.ParentLeftRight;
			VAnchor = VAnchor.FitToChildren;
			AddChild(textWidget);

			Width = startingWidth;
		}

		public RGBA_Bytes TextColor
		{
			get { return textWidget.TextColor; }
			set { textWidget.TextColor = value; }
		}

		public bool DrawFromHintedCache
		{
			get
			{
				return textWidget.Printer.DrawFromHintedCache;
			}
			set
			{
				textWidget.Printer.DrawFromHintedCache = value;
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
			if (textWidget != null)
			{
				if (Width > 0)
				{
					if(Name == "LicenseAgreementPage")
					{
						int a = 0;
					}
					EnglishTextWrapping wrapper = new EnglishTextWrapping(textWidget.Printer.TypeFaceStyle.EmSizeInPoints);
					string wrappedMessage = wrapper.InsertCRs(unwrappedText, Width);
					wrappedWidth = Width;
					textWidget.Text = wrappedMessage;
				}
			}
		}
	}
}