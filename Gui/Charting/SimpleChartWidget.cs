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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace Gui.Charting
{
    public class SimpleChartWidget : GuiWidget
    {
        private ChartData chartData;
        private ChartOptions options;
        private ThemeConfig theme;

        private List<(VertexStorage region, int index)> hoverAreas = new List<(VertexStorage region, int index)>();

        public SimpleChartWidget(ThemeConfig theme, ChartData chartData, ChartOptions options = null)
        {
            this.theme = theme;
            this.chartData = chartData;
            this.options = options;

            DoubleBuffer = true;
        }

        public (double x, double y, string value) HoverValue { get; private set; }

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

            if (!string.IsNullOrEmpty(HoverValue.value))
            {
                graphics2D.DrawString(HoverValue.value, HoverValue.x, HoverValue.y);
            }

            base.OnDraw(graphics2D);
        }

        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            var hoverValue = (0.0, 0.0, "");
            foreach(var area in hoverAreas)
            {
                var regionBounds = area.region.GetBounds();

                if (regionBounds.Contains(mouseEvent.X, mouseEvent.Y))
                {
                    var newHoverText = "";
                    var index = area.index;
                    if (index < chartData.Datasets[0].Data.Count)
                    {
                        if (index < chartData.Datasets[0].HoverMarkdown.Count)
                        {
                            newHoverText = chartData.Datasets[0].HoverMarkdown[index].ToString();
                        }
                        else
                        {
                            newHoverText = chartData.Datasets[0].Data[index].ToString();
                        }
                    }
                    var positionX = regionBounds.Right + 5;
                    positionX = positionX > Width / 2 ? regionBounds.Left - 100 : positionX;
                    hoverValue = (positionX, regionBounds.Top, newHoverText);
                    break;
                }
            }

            if (HoverValue.Item3 != hoverValue.Item3)
            {
                HoverValue = hoverValue;
                Invalidate();
            }

            base.OnMouseMove(mouseEvent);
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

            // draw the left widgets
            var pointSize = 12;

            // print the 0 at the bottom
            var stringPrinter = new TypeFacePrinter($"{0}", pointSize);
            stringPrinter.Render(graphics2D, theme.TextColor);
            var textBounds = stringPrinter.LocalBounds;
            var offset = new Vector2(textBounds.Right, textBounds.YCenter);
            var singleWidth = textBounds.Right;

            // print the top value
            stringPrinter = new TypeFacePrinter($"{maxSize.Y}", pointSize, new Vector2(0, Height * .9 - 6 * DeviceScale));
            stringPrinter.Render(graphics2D, theme.TextColor);
            offset.X = Math.Max(offset.X, stringPrinter.LocalBounds.Right);

            graphics2D.DrawLine(theme.TextColor, new Vector2(offset.X + singleWidth * .5, offset.Y), new Vector2(offset.X + singleWidth, offset.Y));
            graphics2D.DrawLine(theme.TextColor, new Vector2(offset.X + singleWidth * .5, Height * .9), new Vector2(offset.X + singleWidth, Height * .9));
            offset.X += singleWidth * 1.5;

            // draw the graph background
            var bounds = this.LocalBounds;
            bounds.Left += offset.X;
            bounds.Bottom += offset.Y;
            RenderBackground(graphics2D, bounds, theme.TextColor.WithAlpha(20), 5, 1, Color.Transparent);

            var barWidth = bounds.Width / (maxSize.X * 2 + 1 + 2);
            var barOffset = barWidth * 2;

            var hoverAreas = new List<(VertexStorage region, int index)>();

            offset.Y += 1 * DeviceScale;

            // draw the actual graph
            foreach (var dataset in chartData.Datasets)
            {
                var backgroundColor = dataset.BackgroundColor;
                if (backgroundColor.Alpha0To255 < 10)
                {
                    backgroundColor = theme.PrimaryAccentColor;
                }
                
                for (int i=0; i<dataset.Data.Count; i++)
                {
                    var value = dataset.Data[i];
                    var rectangle = new RoundedRect(offset.X + barOffset, offset.Y, offset.X + barOffset + barWidth, offset.Y + ((Height - offset.Y) * .9 / maxSize.Y * value));
                    var region = new VertexStorage(rectangle);
                    hoverAreas.Add((region, i));
                    graphics2D.Render(region, backgroundColor);
                    barOffset += barWidth * 2;
                }
            }

            RenderBackground(graphics2D, bounds, Color.Transparent, 5, 1, theme.TextColor);

            this.hoverAreas = hoverAreas;
        }
    }
}