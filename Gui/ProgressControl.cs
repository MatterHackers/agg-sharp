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

using System;

namespace MatterHackers.Agg.UI
{
	public class ProgressBar : GuiWidget
	{
		public RGBA_Bytes BorderColor = RGBA_Bytes.Black;

		public RGBA_Bytes FillColor { get; set; }

		public EventHandler ProgressChanged;

		private double ratioComplete;

		public ProgressBar(int width, int height)
			: base(width, height)
		{
			AfterDraw += bar_Draw;
		}

		public int PercentComplete
		{
			get { return (int)(ratioComplete * 100 + .5); }
			set
			{
				if (value != (int)(ratioComplete * ratioComplete + .5))
				{
					if (ProgressChanged != null)
					{
						ProgressChanged(this, null);
					}
					ratioComplete = value / 100.0;
					Invalidate();
				}
			}
		}

		public double RatioComplete
		{
			get { return ratioComplete; }
			set
			{
				if (value != ratioComplete)
				{
					if (ProgressChanged != null)
					{
						ProgressChanged(this, null);
					}
					ratioComplete = value;
					Invalidate();
				}
			}
		}

		private void bar_Draw(GuiWidget drawingWidget, DrawEventArgs drawEvent)
		{
			if (drawingWidget != null && drawEvent != null && drawEvent.graphics2D != null)
			{
				drawEvent.graphics2D.FillRectangle(0, 0, drawingWidget.Width * RatioComplete, drawingWidget.Height, FillColor);
				drawEvent.graphics2D.Rectangle(drawingWidget.LocalBounds, BorderColor);
			}
		}
	}

	public class ProgressControl : FlowLayoutWidget
	{
		private TextWidget processTextWidget;
		private ProgressBar progressBar;
		private TextWidget progressTextWidget;

		public event EventHandler ProgressChanged
		{
			//Wraps the PrivateClick event delegate so that we can track which events have been added and clear them if necessary
			add
			{
				progressBar.ProgressChanged += value;
			}

			remove
			{
				progressBar.ProgressChanged -= value;
			}
		}

        public double PointSize
        {
            get { return processTextWidget.PointSize; }
            set 
			{
				processTextWidget.PointSize = value;
				progressTextWidget.PointSize = value;
			}
        }

		public ProgressControl(string message, RGBA_Bytes textColor, RGBA_Bytes fillColor, int barWidgth = 80, int barHeight = 15, int leftMargin = 5)
		{
			processTextWidget = new TextWidget(message, textColor: textColor);
			processTextWidget.AutoExpandBoundsToText = true;
            processTextWidget.Margin = new BorderDouble(leftMargin, 0, 5, 0);
			processTextWidget.VAnchor = VAnchor.ParentCenter;
			AddChild(processTextWidget);

            progressBar = new ProgressBar(barWidgth, barHeight)
            {
                FillColor = fillColor,
            };

			progressBar.VAnchor = VAnchor.ParentCenter;
			AddChild(progressBar);

			progressTextWidget = new TextWidget("", textColor: textColor, pointSize: 8);
			progressTextWidget.AutoExpandBoundsToText = true;
			progressTextWidget.VAnchor = VAnchor.ParentCenter;
			progressTextWidget.Margin = new BorderDouble(5, 0);
			AddChild(progressTextWidget);
		}

		public RGBA_Bytes FillColor 
		{
			get { return progressBar.FillColor; }
			set { progressBar.FillColor = value; }
		}

		public int PercentComplete
		{
			get { return progressBar.PercentComplete; }
			set { progressBar.PercentComplete = value; }
		}

		public double RatioComplete
		{
			get { return progressBar.RatioComplete; }
			set { progressBar.RatioComplete = value; }
		}

			public string ProcessType
		{
			get { return processTextWidget.Text; }
			set
			{
				ProgressMessage = "";
				processTextWidget.Text = value;
			}
		}

		public string ProgressMessage
		{
			get { return progressTextWidget.Text; }
			set
			{
				progressTextWidget.Text = value;
			}
		}
	}
}