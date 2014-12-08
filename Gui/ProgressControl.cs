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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
    public class ProgressControl : FlowLayoutWidget
    {
        GuiWidget bar;
        TextWidget processTextWidget;
        TextWidget progressTextWidget;

        public EventHandler ProgressChanged;

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

        int percentComplete;
        public RGBA_Bytes fillColor;
        public RGBA_Bytes borderColor = RGBA_Bytes.Black;
        public int PercentComplete
        {
            get { return percentComplete; }
            set
            {
                if (value != percentComplete)
                {
                    if (ProgressChanged != null)
                    {
                        ProgressChanged(this, null);
                    }
                    percentComplete = value;
                    Invalidate();
                }
            }
        }

        public ProgressControl(string message, RGBA_Bytes textColor, RGBA_Bytes fillColor)
        {
            this.fillColor = fillColor;

            processTextWidget = new TextWidget(message, textColor: textColor);
            processTextWidget.AutoExpandBoundsToText = true;
            processTextWidget.Margin = new BorderDouble(5, 0);
            AddChild(processTextWidget);

            bar = new GuiWidget(80, 15);
            bar.VAnchor = VAnchor.ParentCenter;
            bar.Draw += new EventHandler(bar_Draw);
            AddChild(bar);
            progressTextWidget = new TextWidget("", textColor: textColor, pointSize: 8);
            progressTextWidget.AutoExpandBoundsToText = true;
            progressTextWidget.VAnchor = VAnchor.ParentCenter;
            progressTextWidget.Margin = new BorderDouble(5, 0);
            AddChild(progressTextWidget);
        }

        void bar_Draw(object sender, EventArgs e)
        {
            DrawEventArgs drawEvent = e as DrawEventArgs;
            GuiWidget widget = sender as GuiWidget;
            if (widget != null && drawEvent != null && drawEvent.graphics2D != null)
            {
                drawEvent.graphics2D.FillRectangle(0, 0, widget.Width * PercentComplete / 100.0, widget.Height, fillColor);
                drawEvent.graphics2D.Rectangle(widget.LocalBounds, borderColor);
            }
        }
    }
}
