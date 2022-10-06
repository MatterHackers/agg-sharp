/*
Copyright (c) 2022, Lars Brubaker
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
using MatterHackers.Agg.Platform;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public record LineInfo(string Text, Color Color);

    public class ConsoleWidget : ScrollableWidget
    {
        public List<LineInfo> allLineInfos = new List<LineInfo>();
        public object locker = new object();
        private static ConsoleWidget _primary;
        private static StyledTypeFace styledTypeFace;
        private static TypeFace typeFace;
        private double _pointSize;

        /// <summary>
        /// The first line to show from the existing visible lines. If -1 then show to the bottom of the list.
        /// </summary>
        private int forceStartLine = -1;

        private int lineHeight;
        private TypeFacePrinter typeFacePrinter;
        private bool writingSingleLine;

        public ConsoleWidget()
        {
            if (typeFace == null)
            {
                var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                typeFace = TypeFace.LoadFrom(StaticData.Instance.ReadAllText(Path.Combine(basePath, "fonts", "LiberationMono.svg")));
            }

            _primary = this;
            PointSize = 10;
            this.BorderColor = Color.Black;
            this.BackgroundOutlineWidth = 1;
        }

        public static ConsoleWidget Primary
        {
            get
            {
                if (_primary == null)
                {
                    throw new Exception("You must create a ConsoleWidget before you can use it.");
                }

                return _primary;
            }
        }

        public int Indent { get; set; }

        public int NumVisibleLines => (int)Math.Ceiling(Height / styledTypeFace.EmSizeInPixels);

        public double PointSize
        {
            get { return _pointSize; }

            set
            {
                // if it changed
                if (_pointSize != value)
                {
                    _pointSize = value;

                    styledTypeFace = new StyledTypeFace(typeFace, PointSize);
                    typeFacePrinter = new TypeFacePrinter("", styledTypeFace);
                    lineHeight = (int)Math.Round(styledTypeFace.AscentInPixels * 1.5);

                    // force a redraw
                    Invalidate();
                }
            }
        }

        public double Position0To1
        {
            get
            {
                if (forceStartLine == -1)
                {
                    return 0;
                }
                else
                {
                    return (allLineInfos.Count - (double)forceStartLine) / allLineInfos.Count;
                }
            }

            set
            {
                forceStartLine = (int)(allLineInfos.Count * (1 - value)) - 1;
                forceStartLine = Math.Max(0, forceStartLine);
                forceStartLine = Math.Min(allLineInfos.Count - 1, forceStartLine);

                // If the start would be less than one screen worth of content, allow
                // the whole screen to have content and scroll with new material.
                if (forceStartLine > allLineInfos.Count - NumVisibleLines)
                {
                    forceStartLine = -1;
                }

                Invalidate();
            }
        }

        public Color TextColor { get; set; } = Color.Black;

        public override void OnDraw(Graphics2D graphics2D)
        {
            RectangleDouble bounds = LocalBounds;

            int numLinesToDraw = NumVisibleLines;

            double y = LocalBounds.Bottom + styledTypeFace.EmSizeInPixels * numLinesToDraw;
            lock (locker)
            {
                int startLineIndex = allLineInfos.Count - numLinesToDraw;
                if (forceStartLine != -1)
                {
                    y = LocalBounds.Top;

                    if (forceStartLine > allLineInfos.Count - numLinesToDraw)
                    {
                        forceStartLine = -1;
                    }
                    else
                    {
                        // make sure we show all the lines we can
                        startLineIndex = Math.Min(forceStartLine, startLineIndex);
                        if (startLineIndex == 0
                            && y > LocalBounds.Top - styledTypeFace.EmSizeInPixels)
                        {
                            y -= styledTypeFace.EmSizeInPixels;
                        }
                    }
                }

                int endLineIndex = allLineInfos.Count;
                for (int lineIndex = startLineIndex; lineIndex < endLineIndex; lineIndex++)
                {
                    if (lineIndex >= 0)
                    {
                        if (allLineInfos[lineIndex] != null)
                        {
                            typeFacePrinter.Text = allLineInfos[lineIndex].Text;
                            typeFacePrinter.Origin = new Vector2(bounds.Left + 2, y);
                            typeFacePrinter.Render(graphics2D, allLineInfos[lineIndex].Color);
                        }
                    }

                    y -= typeFacePrinter.TypeFaceStyle.EmSizeInPixels;
                    if (y < -typeFacePrinter.TypeFaceStyle.EmSizeInPixels)
                    {
                        break;
                    }
                }
            }

            base.OnDraw(graphics2D);
        }

        public override void OnKeyDown(KeyEventArgs keyEvent)
        {
            // make sure children controls get to try to handle this event first
            base.OnKeyDown(keyEvent);

            // check for arrow keys (but only if no modifiers are pressed)
            if (!keyEvent.Handled
                && !keyEvent.Control
                && !keyEvent.Alt
                && !keyEvent.Shift)
            {
                double startingScrollPosition = Position0To1;
                double scrollDelta = NumVisibleLines / (double)allLineInfos.Count;
                double newPos = Position0To1;

                switch (keyEvent.KeyCode)
                {
                    case Keys.PageDown:
                        newPos -= scrollDelta;
                        break;

                    case Keys.PageUp:
                        newPos += scrollDelta;
                        break;

                    case Keys.Home:
                        newPos = 1;
                        break;

                    case Keys.End:
                        newPos = 0;
                        break;
                }

                if (newPos > 1)
                {
                    newPos = 1;
                }
                else if (newPos < 0)
                {
                    newPos = 0;
                }

                Position0To1 = newPos;

                // we only handled the key if it resulted in the area scrolling
                if (startingScrollPosition != Position0To1)
                {
                    keyEvent.Handled = true;
                }
            }
        }

        public override void OnMouseWheel(MouseEventArgs mouseEvent)
        {
            base.OnMouseWheel(mouseEvent);
            var count = allLineInfos.Count;
            double scrollDelta = mouseEvent.WheelDelta / (count * 60.0);

            if (scrollDelta < 0) // Rounding seems to favor scrolling up, compensating scroll down to feel as smooth
            {
                scrollDelta *= 2;
            }
            else if (Position0To1 == 0) // If we scroll up at the bottom get pop out from the "on screen" chunk
            {
                scrollDelta = NumVisibleLines / (double)count;
            }

            double newPos = Position0To1 + scrollDelta;

            if (newPos > 1)
            {
                newPos = 1;
            }
            else if (newPos < 0)
            {
                newPos = 0;
            }

            Position0To1 = newPos;
        }

        /// <summary>
        /// Draw a progress bar to the consol
        /// </summary>
        /// <param name="fullRatio">The amount the bar is full</param>
        public void ShowProgressBar(double fullRatio)
        {
            lock (locker)
            {
                int width = 60;
                var x = (int)(Math.Round(width * fullRatio));
                try
                {
                    var line = new StringBuilder(new String(' ', Indent) + "|");
                    for (int i = 0; i < width; i++)
                    {
                        if (i < x)
                        {
                            line.Append("X");
                        }
                        else
                        {
                            line.Append("_");
                        }
                    }
                    line.Append("|");

                    if (writingSingleLine)
                    {
                        allLineInfos[allLineInfos.Count - 1] = new LineInfo(line.ToString(), TextColor);
                    }
                    else
                    {
                        allLineInfos.Add(new LineInfo(line.ToString(), TextColor));
                        writingSingleLine = true;
                    }
                    Invalidate();
                }
                catch
                {
                }
            }
        }

        public void ShowProgressBar(int current, int total)
        {
            ShowProgressBar((double)current / total);
        }

        public void Write(string text)
        {
            if (writingSingleLine)
            {
                allLineInfos[allLineInfos.Count - 1] = new LineInfo(allLineInfos[allLineInfos.Count - 1] + text, TextColor);
            }
            else
            {
                allLineInfos.Add(new LineInfo(new String(' ', Indent) + text, TextColor));
                writingSingleLine = true;
            }
            Invalidate();
        }

        public void WriteLine(string line)
        {
            writingSingleLine = false;
            // add the indent and the new line
            allLineInfos.Add(new LineInfo(new String(' ', Indent) + line, TextColor));
            Invalidate();
        }

        private void DrawText(Graphics2D graphics2D, LineInfo lineInfo, double x, double y)
        {
            if (lineInfo != null)
            {
                TypeFacePrinter stringPrinter = new TypeFacePrinter(lineInfo.Text, styledTypeFace, new Vector2(x, y));
                stringPrinter.DrawFromHintedCache = true;
                stringPrinter.Render(graphics2D, lineInfo.Color);
            }
        }
    }
}