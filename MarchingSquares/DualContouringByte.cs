/*
Copyright (c) 2025, Lars Brubaker
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

using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MarchingSquares
{
	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

    public class DualContouringByte
    {
        private ImageBuffer imageToProcess;
        private Func<Color, double> ThresholdFunction;
        private double[] thresholdPerPixel;

        public List<LineSegment> LineSegments { get; } = new List<LineSegment>();

        public DualContouringByte(ImageBuffer sourceImage, Color edgeColor, Func<Color, double> thresholdFunction)
        {
            // Fixed variable shadowing - direct assignment to field
            imageToProcess = new ImageBuffer(sourceImage.Width + 2, sourceImage.Height + 2);
            imageToProcess.SetRecieveBlender(new BlenderBGRAExactCopy());
            var graphics2D = imageToProcess.NewGraphics2D();
            graphics2D.Clear(edgeColor);
            graphics2D.Render(sourceImage, 1, 1);

            this.ThresholdFunction = thresholdFunction;
            thresholdPerPixel = new double[imageToProcess.Width * imageToProcess.Height];
            CalculateThresholds();
        }

        private void CalculateThresholds()
        {
            byte[] buffer = imageToProcess.GetBuffer();
            int strideInBytes = imageToProcess.StrideInBytes();

            for (int y = 0; y < imageToProcess.Height; y++)
            {
                int imageBufferOffset = imageToProcess.GetBufferOffsetY(y);
                int thresholdBufferOffset = y * imageToProcess.Width;

                for (int x = 0; x < imageToProcess.Width; x++)
                {
                    int imageBufferOffsetWithX = imageBufferOffset + x * 4;
                    var color = GetRGBA(buffer, imageBufferOffsetWithX);
                    thresholdPerPixel[thresholdBufferOffset + x] = ThresholdFunction(color);
                }
            }
        }

        private Color GetRGBA(byte[] buffer, int offset)
        {
            return new Color(buffer[offset + 2], buffer[offset + 1], buffer[offset + 0], buffer[offset + 3]);
        }

        private bool IsEdgeCell(int x, int y)
        {
            // Get the four corners of the cell
            double point0 = thresholdPerPixel[(x + 0) + (y + 1) * imageToProcess.Width];
            double point1 = thresholdPerPixel[(x + 1) + (y + 1) * imageToProcess.Width];
            double point2 = thresholdPerPixel[(x + 1) + (y + 0) * imageToProcess.Width];
            double point3 = thresholdPerPixel[(x + 0) + (y + 0) * imageToProcess.Width];

            // Match threshold behavior with MarchingSquares
            int flags = (point0 > 0 ? 1 : 0);
            flags = (flags << 1) | (point1 > 0 ? 1 : 0);
            flags = (flags << 1) | (point2 > 0 ? 1 : 0);
            flags = (flags << 1) | (point3 > 0 ? 1 : 0);

            return flags != 0 && flags != 15;
        }

        private Vector2 CalculateIntersection(int x, int y, int edge)
        {
            // Match MarchingSquares interpolation
            double v0, v1;
            double x0, y0, x1, y1;

            switch (edge)
            {
                case 0: // Bottom edge
                    v0 = thresholdPerPixel[x + y * imageToProcess.Width];
                    v1 = thresholdPerPixel[(x + 1) + y * imageToProcess.Width];
                    x0 = x; y0 = y;
                    x1 = x + 1; y1 = y;
                    break;
                case 1: // Right edge
                    v0 = thresholdPerPixel[(x + 1) + y * imageToProcess.Width];
                    v1 = thresholdPerPixel[(x + 1) + (y + 1) * imageToProcess.Width];
                    x0 = x + 1; y0 = y;
                    x1 = x + 1; y1 = y + 1;
                    break;
                case 2: // Top edge
                    v0 = thresholdPerPixel[x + (y + 1) * imageToProcess.Width];
                    v1 = thresholdPerPixel[(x + 1) + (y + 1) * imageToProcess.Width];
                    x0 = x; y0 = y + 1;
                    x1 = x + 1; y1 = y + 1;
                    break;
                default: // Left edge
                    v0 = thresholdPerPixel[x + y * imageToProcess.Width];
                    v1 = thresholdPerPixel[x + (y + 1) * imageToProcess.Width];
                    x0 = x; y0 = y;
                    x1 = x; y1 = y + 1;
                    break;
            }

            // Linear interpolation
            double t = (0.5 - v0) / (v1 - v0);
            t = Math.Max(0, Math.Min(1, t)); // Clamp to [0,1]

            return new Vector2(
                x0 + (x1 - x0) * t,
                y0 + (y1 - y0) * t
            );
        }

        public void CreateLineSegments()
        {
            LineSegments.Clear();

            for (int y = 0; y < imageToProcess.Height - 1; y++)
            {
                for (int x = 0; x < imageToProcess.Width - 1; x++)
                {
                    if (IsEdgeCell(x, y))
                    {
                        // Get cell corners
                        double point0 = thresholdPerPixel[(x + 0) + (y + 1) * imageToProcess.Width];
                        double point1 = thresholdPerPixel[(x + 1) + (y + 1) * imageToProcess.Width];
                        double point2 = thresholdPerPixel[(x + 1) + (y + 0) * imageToProcess.Width];
                        double point3 = thresholdPerPixel[(x + 0) + (y + 0) * imageToProcess.Width];

                        int flags = (point0 > 0.5 ? 1 : 0);
                        flags = (flags << 1) | (point1 > 0.5 ? 1 : 0);
                        flags = (flags << 1) | (point2 > 0.5 ? 1 : 0);
                        flags = (flags << 1) | (point3 > 0.5 ? 1 : 0);

                        AddSegmentsForCase(x, y, flags);
                    }
                }
            }

            // Account for image expansion
            foreach (var segment in LineSegments)
            {
                segment.start -= Vector2.One;
                segment.end -= Vector2.One;
            }
        }

        private void AddSegmentsForCase(int x, int y, int caseIndex)
        {
            Color color = Color.Green;

            // Handle ambiguous cases
            if (caseIndex == 5 || caseIndex == 10)
            {
                double average = (
                    thresholdPerPixel[(x + 0) + (y + 1) * imageToProcess.Width] +
                    thresholdPerPixel[(x + 1) + (y + 1) * imageToProcess.Width] +
                    thresholdPerPixel[(x + 1) + (y + 0) * imageToProcess.Width] +
                    thresholdPerPixel[(x + 0) + (y + 0) * imageToProcess.Width]
                ) / 4.0;

                if ((caseIndex == 5 && average > 0) ||
                    (caseIndex == 10 && average <= 0))
                {
                    caseIndex = (caseIndex == 5) ? 10 : 5;
                }
            }

            // Complete implementation of all marching squares cases
            switch (caseIndex)
            {
                case 1:
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 3),
                        CalculateIntersection(x, y, 0),
                        color));
                    break;

                case 2:
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 0),
                        CalculateIntersection(x, y, 1),
                        color));
                    break;

                case 3:
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 3),
                        CalculateIntersection(x, y, 1),
                        color));
                    break;

                case 4:
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 1),
                        CalculateIntersection(x, y, 2),
                        color));
                    break;

                case 5:
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 3),
                        CalculateIntersection(x, y, 0),
                        color));
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 1),
                        CalculateIntersection(x, y, 2),
                        color));
                    break;

                case 6:
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 0),
                        CalculateIntersection(x, y, 2),
                        color));
                    break;

                case 7:
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 3),
                        CalculateIntersection(x, y, 2),
                        color));
                    break;

                case 8:
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 2),
                        CalculateIntersection(x, y, 3),
                        color));
                    break;

                case 9:
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 0),
                        CalculateIntersection(x, y, 2),
                        color));
                    break;

                case 10:
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 0),
                        CalculateIntersection(x, y, 1),
                        color));
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 2),
                        CalculateIntersection(x, y, 3),
                        color));
                    break;

                case 11:
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 1),
                        CalculateIntersection(x, y, 3),
                        color));
                    break;

                case 12:
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 1),
                        CalculateIntersection(x, y, 3),
                        color));
                    break;

                case 13:
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 1),
                        CalculateIntersection(x, y, 2),
                        color));
                    break;

                case 14:
                    LineSegments.Add(new LineSegment(
                        CalculateIntersection(x, y, 0),
                        CalculateIntersection(x, y, 3),
                        color));
                    break;
            }
        }

        public Polygons CreateLineLoops(int pixelsToIntPointsScale, int maxLineLoopsToAdd = int.MaxValue)
        {
            var LineLoops = new Polygons();
            bool[] hasBeenAddedList = new bool[LineSegments.Count];
            var sharedPoints = new MarchingSquaresByte.SharedPoints(LineSegments);

            // Use same loop creation logic as MarchingSquares
            for (int segmentToAddIndex = 0; segmentToAddIndex < LineSegments.Count; segmentToAddIndex++)
            {
                if (!hasBeenAddedList[segmentToAddIndex])
                {
                    var loopToAdd = new Polygon();
                    var currentSegmentIndex = segmentToAddIndex;
                    var currentSegment = LineSegments[currentSegmentIndex];
                    var connectionVertex = currentSegment.end;
                    loopToAdd.Add(new IntPoint(
                        (long)(connectionVertex.X * pixelsToIntPointsScale),
                        (long)(connectionVertex.Y * pixelsToIntPointsScale)));

                    hasBeenAddedList[currentSegmentIndex] = true;
                    bool addedToLoop;

                    do
                    {
                        addedToLoop = false;
                        foreach (int segmentToCheckIndex in sharedPoints.GetTouching(currentSegment))
                        {
                            if (!hasBeenAddedList[segmentToCheckIndex])
                            {
                                var segmentToCheck = LineSegments[segmentToCheckIndex];
                                bool foundNextSegment = false;

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

                                if (foundNextSegment)
                                {
                                    hasBeenAddedList[segmentToCheckIndex] = true;
                                    currentSegmentIndex = segmentToCheckIndex;
                                    currentSegment = segmentToCheck;
                                    loopToAdd.Add(new IntPoint(
                                        (long)(connectionVertex.X * pixelsToIntPointsScale),
                                        (long)(connectionVertex.Y * pixelsToIntPointsScale)));
                                    addedToLoop = true;
                                    break;
                                }
                            }
                        }
                    }
                    while (addedToLoop);

                    LineLoops.Add(loopToAdd);
                    if (LineLoops.Count > maxLineLoopsToAdd)
                    {
                        break;
                    }
                }
            }

            return LineLoops;
        }

        public void DrawSegments(Graphics2D graphics2D)
        {
            foreach (LineSegment lineSegment in LineSegments)
            {
                var linesToDraw = new VertexStorage();
                linesToDraw.Clear();
                linesToDraw.MoveTo(lineSegment.start.X, lineSegment.start.Y);
                linesToDraw.LineTo(lineSegment.end.X, lineSegment.end.Y);
                var strokedLineToDraw = new Stroke(linesToDraw, .25);
                graphics2D.Render(strokedLineToDraw, lineSegment.color);
            }
        }
    }
}