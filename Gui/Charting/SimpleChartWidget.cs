/*
Copyright (c) 2023, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using Newtonsoft.Json.Linq;
using System;

namespace Gui.Charting
{
    public class SimpleChartWidget : GuiWidget
    {
        private ChartData chartData;
        private ChartOptions options;
        private ThemeConfig theme;

        public SimpleChartWidget(ThemeConfig theme, ChartData chartData, ChartOptions options = null)
        {
            this.theme = theme;
            this.chartData = chartData;
            this.options = options;

            DoubleBuffer = true;
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            switch (chartData.ChartType)
            {
                case ChartType.Bar:
                    DrawBarChart(graphics2D);
                    break;

                case ChartType.Line:
                    throw new NotImplementedException();

                case ChartType.Scatter:
                    throw new NotImplementedException();

                default:
                    throw new NotImplementedException();
            }

            base.OnDraw(graphics2D);
        }

        private void DrawBarChart(Graphics2D graphics2D)
        {
            var maxSize = new Vector2();
            foreach(var dataset in chartData.Datasets)
            {
                maxSize.X = Math.Max(maxSize.X, dataset.Data.Count);
                foreach (var value in dataset.Data)
                {
                    maxSize.Y = Math.Max(maxSize.Y, value);
                }
            }

            foreach (var dataset in chartData.Datasets)
            {
                var backgroundColor = dataset.BackgroundColor;
                if (backgroundColor.Alpha0To255 < 10)
                {
                    backgroundColor = theme.PrimaryAccentColor;
                }
                
                for (int i=0; i<dataset.Data.Count; i++)
                {
                    graphics2D.FillRectangle(i * 5, 0, i * 5 + 3, Height / maxSize.Y * dataset.Data[i], backgroundColor);
                }
            }
        }
    }
}