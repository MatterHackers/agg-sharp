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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg;

using ClipperLib;

namespace MatterHackers.MarchingSquares
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public class LineSegment
    {
        public Vector2 start;
        public Vector2 end;
        public RGBA_Bytes color;

        public LineSegment(double x1, double y1, double x2, double y2, RGBA_Bytes color)
            : this(new Vector2(x1, y1), new Vector2(x2, y2), color)
        {
        }

        public LineSegment(Vector2 start, Vector2 end, RGBA_Bytes color)
        {
            this.start = start;
            this.end = end;
            this.color = color;
        }
    }

    public class MarchingSquaresByte
    {
        ImageBuffer imageToMarch;
        int threshold;
        int debugColor;

        List<LineSegment> LineSegments = new List<LineSegment>();

        public MarchingSquaresByte(ImageBuffer imageToMarch, int threshold, int debugColor)
        {
            this.threshold = threshold;
            this.imageToMarch = imageToMarch;
            this.debugColor = debugColor;

            CreateLineSegments();
        }

        public Polygons CreateLineLoops(int pixelsToIntPointsScale)
        {
            Polygons LineLoops = new Polygons();

            bool[] hasBeenAddedList = new bool[LineSegments.Count];

            for(int segmentToAddIndex = 0; segmentToAddIndex < LineSegments.Count; segmentToAddIndex++)
            {
                if (!hasBeenAddedList[segmentToAddIndex])
                {
                    // now find all the connected segments until we get back to this one
                    Polygon loopToAdd = new Polygon();

                    // walk the loop
                    int currentSegmentIndex = segmentToAddIndex;
                    LineSegment currentSegment = LineSegments[currentSegmentIndex];
                    Vector2 connectionVertex = currentSegment.end;
                    loopToAdd.Add(new IntPoint((long)(connectionVertex.x * pixelsToIntPointsScale), (long)(connectionVertex.y * pixelsToIntPointsScale)));
                    hasBeenAddedList[currentSegmentIndex] = true;
                    bool addedToLoop = false;
                    do
                    {
                        bool foundNextSegment = false;
                        addedToLoop = false;
                        for (int segmentToCheckIndex = 0; segmentToCheckIndex < LineSegments.Count; segmentToCheckIndex++)
                        {
                            LineSegment segmentToCheck = LineSegments[segmentToCheckIndex];
                            if (!hasBeenAddedList[segmentToCheckIndex])
                            {
                                if (connectionVertex == segmentToCheck.start)
                                {
                                    connectionVertex = segmentToCheck.end;
                                    foundNextSegment = true;
                                }
                                else if (connectionVertex == segmentToCheck.end)
                                {
                                    connectionVertex = segmentToCheck.start;
                                    foundNextSegment = true;
                                }
                                else
                                {
                                    int i = 0;
                                }

                                if (foundNextSegment)
                                {
                                    hasBeenAddedList[segmentToCheckIndex] = true;
                                    currentSegmentIndex = segmentToCheckIndex;
                                    currentSegment = segmentToCheck;
                                    loopToAdd.Add(new IntPoint((long)(connectionVertex.x * pixelsToIntPointsScale), (long)(connectionVertex.y * pixelsToIntPointsScale)));
                                    addedToLoop = true;
                                    break;
                                }
                            }
                        }
                    } while (addedToLoop);

                    LineLoops.Add(loopToAdd);
                }
            }

            return LineLoops;
        }

        public void CreateLineSegments()
        {
            LineSegments.Clear();
            byte[] buffer = imageToMarch.GetBuffer();
            int strideInBytes = imageToMarch.StrideInBytes();
            for (int y = 0; y < imageToMarch.Height - 1; y++)
            {
                int offset = imageToMarch.GetBufferOffsetY(y);
                for (int x = 0; x < imageToMarch.Width - 1; x++)
                {
                    int offsetWithX = offset + x * 4;
                    int point0 = buffer[offsetWithX+ strideInBytes];
                    int point1 = buffer[offsetWithX + 4 + strideInBytes];
                    int point2 = buffer[offsetWithX + 4];
                    int point3 = buffer[offsetWithX];
                    int flags = (point0 > threshold) ? 1 : 0;
                    flags = (flags << 1) | ((point1 > threshold) ? 1 : 0);
                    flags = (flags << 1) | ((point2 > threshold) ? 1 : 0);
                    flags = (flags << 1) | ((point3 > threshold) ? 1 : 0);

                    bool wasFlipped = false;
                    if (flags == 5)
                    {
                        int average = (point0 + point1 + point2 + point3) / 4;
                        if (average < threshold)
                        {
                            flags = 10;
                            wasFlipped = true;
                        }
                    }
                    else if (flags == 10)
                    {
                        int average = (point0 + point1 + point2 + point3) / 4;
                        if (average < threshold)
                        {
                            flags = 5;
                            wasFlipped = true;
                        }
                    }

                    AddSegmentForFlags(x, y, flags, wasFlipped);
                }

            }
        }

        LineSegment GetInterpolatedSegment(LineSegment segmentA, LineSegment segmentB)
        {
            RGBA_Bytes colorAStart = imageToMarch.GetPixel((int)segmentA.start.x, (int)segmentA.start.y);
            RGBA_Bytes colorAEnd = imageToMarch.GetPixel((int)segmentA.end.x, (int)segmentA.end.y);

            Vector2 directionA = segmentA.end - segmentA.start;
            double offsetA = 1 - (colorAEnd.red / 255.0 + colorAStart.red / 255.0) / 2.0;
            directionA *= offsetA;

            RGBA_Bytes colorBStart = imageToMarch.GetPixel((int)segmentB.start.x, (int)segmentB.start.y);
            RGBA_Bytes colorBEnd = imageToMarch.GetPixel((int)segmentB.end.x, (int)segmentB.end.y);

            Vector2 directionB = segmentB.end - segmentB.start;
            double ratioB = 1 - (colorBEnd.red / 255.0 + colorBStart.red / 255.0) / 2.0;
            directionB *= ratioB;

            double offsetToPixelCenter = .5;
            LineSegment segment = new LineSegment(
                (segmentA.start.x + directionA.x) + offsetToPixelCenter,
                (segmentA.start.y + directionA.y) + offsetToPixelCenter,
                (segmentB.start.x + directionB.x) + offsetToPixelCenter,
                (segmentB.start.y + directionB.y) + offsetToPixelCenter,
                segmentA.color);

            return segment;
        }

        private void AddSegmentForFlags(int x, int y, int flags, bool wasFlipped)
        {
            RGBA_Bytes color = RGBA_Bytes.Red;
            if (flags != debugColor)
                color = RGBA_Bytes.Green;

            switch (flags)
            {
                case 1:
                    LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y + 1, x, y, color), new LineSegment(x + 1, y, x, y, color)));
                    break;

                case 2:
                    LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y + 1, x + 1, y, color), new LineSegment(x, y, x + 1, y, color)));
                    break;

                case 3:
                    LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y + 1, x, y, color), new LineSegment(x + 1, y + 1, x + 1, y, color)));
                    break;

                case 4:
                    LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y, x + 1, y + 1, color), new LineSegment(x, y + 1, x + 1, y + 1, color)));
                    break;

                case 5:
                    if (wasFlipped)
                    {
                        LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y, x, y + 1, color), new LineSegment(x + 1, y + 1, x, y + 1, color)));
                        LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y + 1, x + 1, y, color), new LineSegment(x, y, x + 1, y, color)));
                    }
                    else
                    {
                        LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y + 1, x, y, color), new LineSegment(x, y + 1, x + 1, y + 1, color)));
                        LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y, x + 1, y + 1, color), new LineSegment(x + 1, y, x, y, color)));
                    }
                    break;

                case 6:
                    LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y, x + 1, y, color), new LineSegment(x, y + 1, x + 1, y + 1, color)));
                    break;

                case 7:
                    LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y + 1, x, y, color), new LineSegment(x, y + 1, x + 1, y + 1, color)));
                    break;

                case 8:
                    LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y, x, y + 1, color), new LineSegment(x + 1, y + 1, x, y + 1, color)));
                    break;

                case 9:
                    LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y + 1, x, y + 1, color), new LineSegment(x + 1, y, x, y, color)));
                    break;

                case 10:
                    if (wasFlipped)
                    {
                        LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y, x, y, color), new LineSegment(x, y + 1, x, y, color)));
                        LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y + 1, x + 1, y + 1, color), new LineSegment(x + 1, y, x + 1, y + 1, color)));
                    }
                    else
                    {
                        LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y, x, y + 1, color), new LineSegment(x, y, x + 1, y, color)));
                        LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y + 1, x + 1, y, color), new LineSegment(x + 1, y + 1, x, y + 1, color)));
                    }
                    break;

                case 11:
                    LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y + 1, x + 1, y, color), new LineSegment(x + 1, y + 1, x, y + 1, color)));
                    break;

                case 12:
                    LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y, x, y + 1, color), new LineSegment(x + 1, y, x + 1, y + 1, color)));
                    break;

                case 13:
                    LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y, x + 1, y + 1, color), new LineSegment(x + 1, y, x, y, color)));
                    break;

                case 14:
                    LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y, x, y + 1, color), new LineSegment(x, y, x + 1, y, color)));
                    break;
            }
        }

        public void DrawSegments(Graphics2D graphics2D)
        {
            foreach(LineSegment lineSegment in LineSegments)
            {
                PathStorage m_LinesToDraw = new PathStorage();
                m_LinesToDraw.remove_all();
                m_LinesToDraw.MoveTo(lineSegment.start.x, lineSegment.start.y);
                m_LinesToDraw.LineTo(lineSegment.end.x, lineSegment.end.y);
                Stroke StrockedLineToDraw = new Stroke(m_LinesToDraw, .25);
                graphics2D.Render(StrockedLineToDraw, lineSegment.color);
            }
        }

        public static void AssertDebugNotDefined()
        {
#if DEBUG
            throw new Exception("DEBUG is defined and should not be!");
#endif
        }
    }
}
