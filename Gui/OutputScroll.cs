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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.UI
{
    public class OutputScroll : GuiWidget
    {
        const int TOTOL_POW2 = 64;
        int lineCount = 0;
        ImageBuffer[] lines = new ImageBuffer[TOTOL_POW2];

        public RGBA_Bytes BorderColor = new RGBA_Bytes(204, 204, 204);
        public RGBA_Bytes TextColor = new RGBA_Bytes( 102, 102, 102);
        public int BorderWidth = 5;
        public int BorderRadius = 0;


        public OutputScroll()
        {
        }

        public void WriteLine(Object sender, EventArgs e)
        {
            StringEventArgs lineString = e as StringEventArgs;
            Write(lineString.Data + "\n");
        }

        TypeFacePrinter printer = new TypeFacePrinter();
        public void Write(string lineString)
        {
            string[] splitOnNL = lineString.Split('\n');
            foreach (string line in splitOnNL)
            {
                if (line.Length > 0)
                {
                    printer.Text = line;
                    Vector2 stringSize = printer.GetSize();

                    int arrayIndex = (lineCount % TOTOL_POW2);
                    lines[arrayIndex] = new ImageBuffer((int)Math.Ceiling(stringSize.x), (int)Math.Ceiling(stringSize.y),
                        32, new BlenderBGRA());
                    lines[arrayIndex].NewGraphics2D().DrawString(line, 0, -printer.TypeFaceStyle.DescentInPixels);

                    lineCount++;
                }
            }

            Invalidate();
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            TypeFacePrinter printer = new TypeFacePrinter();

            RectangleDouble Bounds = LocalBounds;
            RoundedRect rectBorder = new RoundedRect(Bounds, this.BorderRadius);

            graphics2D.Render(rectBorder, BorderColor);

            RectangleDouble insideBounds = Bounds;
            insideBounds.Inflate(-this.BorderWidth);
            RoundedRect rectInside = new RoundedRect(insideBounds, Math.Max(this.BorderRadius - this.BorderWidth, 0));

            graphics2D.Render(rectInside, this.BackgroundColor);

            double y = LocalBounds.Bottom + printer.TypeFaceStyle.EmSizeInPixels * (TOTOL_POW2-1) + 5;
            for(int index = lineCount; index < lineCount + TOTOL_POW2; index++)
            {
                int arrayIndex = (index % TOTOL_POW2);
                if (lines[arrayIndex] != null)
                {
                    graphics2D.Render(lines[arrayIndex], new Vector2(this.BorderWidth + 2, y));
                }
                y -= printer.TypeFaceStyle.EmSizeInPixels;
            }

            base.OnDraw(graphics2D);
        }
    }
}
