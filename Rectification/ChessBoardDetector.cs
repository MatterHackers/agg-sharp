/*
Copyright (c) 2012, Lars Brubaker
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
//#define SHOW_SUB_PIXEL_LOGIC

using System;
using System.Collections.Generic;

using MatterHackers.VectorMath;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Rectification
{
    public class ChessBoardDetector
    {
        internal class ChessboardConnections
        {
            internal ChessboardConnections(ValidResponseData item, ValidResponseData leftConnection, ValidResponseData rightConnection, ValidResponseData bottomConnection, ValidResponseData topConnection)
            {
                this.item = item;
                this.leftConnection = leftConnection;
                this.rightConnection = rightConnection;
                this.bottomConnection = bottomConnection;
                this.topConnection = topConnection;
            }

            internal ValidResponseData leftConnection;
            internal ValidResponseData rightConnection;
            internal ValidResponseData bottomConnection;
            internal ValidResponseData topConnection;

            internal ValidResponseData item;

            internal void Render(Graphics2D graphics2D)
            {
                if (item != null)
                {
                    Ellipse elipse = new Ellipse(item.position, 10);
                    graphics2D.Render(new Stroke(elipse, 3), RGBA_Bytes.Red);
                }
            }
        }

        internal class ValidResponseData : IKDLeafItem
        {
            internal int originalIndex;
            internal Vector2 position;
            internal int totalResponse;
            internal int orientation;
            public bool has4ChessboardNeighbors = false;
            internal ChessboardConnections chessboardConnections;

            public int Dimensions
            {
                get { return 2; }
            }

            public double GetPositionForDimension(int dimension)
            {
                return position[dimension];
            }

            public void SetPositionForDimension(int dimension, double position)
            {
                this.position[dimension] = position;
            }
        }

        internal class SortOnDistance : IComparer<ValidResponseData>
        {
            public Vector2 positionToSortFrom;

            public SortOnDistance(Vector2 positionToSortFrom)
            {
                this.positionToSortFrom = positionToSortFrom;
            }

            public int Compare(ValidResponseData a, ValidResponseData b)
            {
                if (a == null || b == null)
                {
                    throw new Exception();
                }

                double distanceToA = (a.position - positionToSortFrom).Length;
                double distanceToB = (b.position - positionToSortFrom).Length;

                if (distanceToA > distanceToB)
                {
                    return 1;
                }
                else if (distanceToA < distanceToB)
                {
                    return -1;
                }
                return 0;
            }
        }

        internal class ValidResponseList : List<ValidResponseData>
        {
            List<ValidResponseData> GetClosestResponses(int maxToGet)
            {
                List<ValidResponseData> closestResponses = new List<ValidResponseData>();
                foreach (ValidResponseData validResponse in this)
                {
                }

                return closestResponses;
            }
        }

#if SHOW_SUB_PIXEL_LOGIC
        ImageBuffer gray;
#endif

        const int trimPixels = 5;
        Array2D<ValidResponseData> allResponsesGrid;

        ValidResponseList validResponsesBotomToTopList = new ValidResponseList();

        ChessboardConnections lowestCorrner = null;

        Point2D[] offsetToPixel =
        {
            new Point2D(5, 0), new Point2D(5, 2), new Point2D(4, 4), new Point2D(2, 5),
            new Point2D(0, 5), new Point2D(-2, 5), new Point2D(-4, 4), new Point2D(-5, 2),
            new Point2D(-5, 0), new Point2D(-5, -2), new Point2D(-4, -4), new Point2D(-2, -5),
            new Point2D(0, -5), new Point2D(2, -5), new Point2D(4, -4), new Point2D(5, -2),
        };

        int[] byteOffsetToPixel = new int[16];

        public ChessBoardDetector(int imageWidth, int imageHeight)
        {
            allResponsesGrid = new Array2D<ValidResponseData>(imageWidth, imageHeight);

            for(int i=0; i<offsetToPixel.Length; i++)
            {
                byteOffsetToPixel[i] = offsetToPixel[i].y * imageWidth + offsetToPixel[i].x;
            }
        }

        public void DrawDebug(Agg.Graphics2D graphics2D)
        {
            if (validResponsesBotomToTopList.Count < 1)
            {
                return;
            }

            if (graphics2D.DestImage == null)
            {
                throw new Exception("Your graphics2D must be for a buffered Image.");
            }

            if (lowestCorrner != null)
            {
                lowestCorrner.Render(graphics2D);
            }

            int width = allResponsesGrid.Width;
            int height = allResponsesGrid.Height;
            IImageByte imageBuffer = graphics2D.DestImage;
            ValidResponseData prevValidResponse = validResponsesBotomToTopList[0];
            foreach (ValidResponseData validResponse in validResponsesBotomToTopList)
            {
                if (validResponse.totalResponse > 0)
                {
                    Vector2 start = new Vector2(10, 0);
                    start.Rotate(validResponse.orientation * MathHelper.DegreesToRadians(22.5));

                    graphics2D.Line(validResponse.position + start, validResponse.position - start, RGBA_Bytes.Green);

                    graphics2D.Circle(validResponse.position, 2, RGBA_Bytes.Red);

                    if (validResponse.has4ChessboardNeighbors)
                    {
                        Ellipse elipse = new Ellipse(validResponse.position, 10);
                        graphics2D.Render(new Stroke(elipse, 1), RGBA_Bytes.Red);
                    }

                    //graphics2D.Line(validResponse.position, prevValidResponse.position, RGBA_Bytes.Red);

                    prevValidResponse = validResponse;
                }
            }

#if SHOW_SUB_PIXEL_LOGIC
            graphics2D.Render(gray, 0, 0);
#endif
        }

        void CalculateOrientationAtAllFeatures(ImageBuffer imageBuffer)
        {
            byte[] buffer = imageBuffer.GetBuffer();
            foreach (ValidResponseData validResponse in validResponsesBotomToTopList)
            {
                if (validResponse.totalResponse <= 0)
                {
                    throw new Exception("You should not have added a response below the threashold.");
                }

                int maxAbsSum = 0;
                int byteOffset = imageBuffer.GetBufferOffsetXY((int)validResponse.position.x, (int)validResponse.position.y);
                for (int angleToCheckIndex = 0; angleToCheckIndex < 4; angleToCheckIndex++)
                {
                    int sum = 0;
                    for (int extraAngleToCheckIndex = angleToCheckIndex - 1; extraAngleToCheckIndex <= angleToCheckIndex + 1; extraAngleToCheckIndex++)
                    {
                        int check0 = extraAngleToCheckIndex; if (check0 < 0) check0 += 16; if (check0 > 15) check0 -= 16;
                        int check1 = extraAngleToCheckIndex + 8; if (check1 < 0) check1 += 16; if (check1 > 15) check1 -= 16;
                        int check2 = extraAngleToCheckIndex + 4; if (check2 < 0) check2 += 16; if (check2 > 15) check2 -= 16;
                        int check3 = extraAngleToCheckIndex + 12; if (check3 < 0) check3 += 16; if (check3 > 15) check3 -= 16;
                        sum +=
                            (buffer[byteOffset + byteOffsetToPixel[check0]] + buffer[byteOffset + byteOffsetToPixel[check1]])
                            -
                            (buffer[byteOffset + byteOffsetToPixel[check2]] + buffer[byteOffset + byteOffsetToPixel[check3]]);
                    }
                    int absSum = Math.Abs(sum);
                    if (absSum > maxAbsSum)
                    {
                        maxAbsSum = absSum;
                        if (sum > 0)
                        {
                            validResponse.orientation = angleToCheckIndex;
                        }
                        else
                        {
                            validResponse.orientation = (4 + angleToCheckIndex);
                        }
                    }
                }
            }
        }
    
        void CalculateSumResponseAtAllPixels(ImageBuffer imageBuffer, int totalResponseThreshold)
        {
            validResponsesBotomToTopList.Clear();

            if(imageBuffer.GetBytesBetweenPixelsInclusive() != 1)
            {
                throw new NotImplementedException("We only process gray scale images that are packed");
            }

            int width = imageBuffer.Width;
            int height = imageBuffer.Height;
            byte[] buffer = imageBuffer.GetBuffer();
            for (int y = trimPixels; y < height-trimPixels; y++)
            {
                int byteOffset = imageBuffer.GetBufferOffsetXY(trimPixels, y);
                ValidResponseData[] totalResponseRow = allResponsesGrid.GetRow(y);
                for (int x = trimPixels; x < width - trimPixels; x++)
                {
                    int sumResponse = 0;
                    for (int angleToCheckIndex = 0; angleToCheckIndex < 4; angleToCheckIndex++)
                    {
                        int sum = 
                            (buffer[byteOffset + byteOffsetToPixel[angleToCheckIndex]] + buffer[byteOffset + byteOffsetToPixel[angleToCheckIndex + 8]])
                            -
                            (buffer[byteOffset + byteOffsetToPixel[angleToCheckIndex + 4]] + buffer[byteOffset + byteOffsetToPixel[angleToCheckIndex + 12]]);
                        int absSum = Math.Abs(sum);
                        sumResponse += absSum;
                    }
                    int neighborMeanTotal = 0;
                    int diffResponse = 0;
                    for (int diffCheck = 0; diffCheck < 8; diffCheck++)
                    {
                        int testValue = buffer[byteOffset + byteOffsetToPixel[diffCheck]];
                        int oppositeValue = buffer[byteOffset + byteOffsetToPixel[diffCheck + 8]];
                        diffResponse += Math.Abs(testValue - oppositeValue);
                        neighborMeanTotal += testValue + oppositeValue;
                    }
                    int neighborMean = (neighborMeanTotal + 8) / 16;

                    int centerMeanTotal = buffer[byteOffset - 1] + buffer[byteOffset + 1] + buffer[byteOffset - width] + buffer[byteOffset - width];
                    int centerMean = (centerMeanTotal + 2) / 4;
                    int absMeanResponse = Math.Abs(neighborMean - centerMean);

                    ValidResponseData newResponse = new ValidResponseData();
                    int totalResponse = sumResponse - diffResponse - absMeanResponse;
                    if (totalResponse >= totalResponseThreshold)
                    {
                        newResponse.totalResponse = totalResponse;
                        newResponse.position = new Vector2(x, y);
                        newResponse.originalIndex = validResponsesBotomToTopList.Count;
                        // we are scanning pixels bottom to top so they go in the list bottom to top
                        validResponsesBotomToTopList.Add(newResponse);
                    }

                    totalResponseRow[x] = newResponse;

                    byteOffset++;
                }
            }
        }

        void MergeResponsesThatAreSameFeatures()
        {
            int width = allResponsesGrid.Width;
            int height = allResponsesGrid.Height;
            foreach (ValidResponseData validResponse in validResponsesBotomToTopList)
            {
                if (validResponse.totalResponse <= 0)
                {
                    continue;
                }

                int dist = 5;
                int yStart = Math.Max(0, (int)validResponse.position.y - dist);
                int yEnd = Math.Min(height - 1, (int)validResponse.position.y + dist);
                int numFeaturesFound = 1;
                int accumulatedTotalResponse = validResponse.totalResponse;
                Vector2 accumulatedPosition = validResponse.position * accumulatedTotalResponse;
                int accumulatedOrientation = validResponse.orientation * accumulatedTotalResponse;
                for (int y = yStart; y < yEnd; y++)
                {
                    int xStart = Math.Max(0, (int)validResponse.position.x - dist);
                    int xEnd = Math.Min(width - 1, (int)validResponse.position.x + dist);
                    for (int x = xStart; x < xEnd; x++)
                    {
                        if (x == validResponse.position.x && y == validResponse.position.y)
                        {
                            continue;
                        }

                        ValidResponseData testResponse = allResponsesGrid.GetValue(x, y);
                        if (testResponse != null && testResponse.totalResponse > 0)
                        {
                            numFeaturesFound++;
                            int totalResponsOfTest = testResponse.totalResponse;
                            testResponse.totalResponse = 0;
                            accumulatedTotalResponse += totalResponsOfTest;
                            accumulatedPosition += testResponse.position * totalResponsOfTest;
                            accumulatedOrientation += testResponse.orientation * totalResponsOfTest;
                        }
                    }
                }

                if (numFeaturesFound < 2)
                {
                    validResponse.totalResponse = 0;
                }
                else
                {
                    validResponse.position = accumulatedPosition / accumulatedTotalResponse;
                    validResponse.orientation = (int)((double)accumulatedOrientation / accumulatedTotalResponse + .5);
                }
            }

            int count = validResponsesBotomToTopList.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                if (validResponsesBotomToTopList[i].totalResponse == 0)
                {
                    validResponsesBotomToTopList.RemoveAt(i);
                }
            }
        }

        void FindSubPixelPositions(ImageBuffer imageBuffer)
        {
            // find the subpixel center
            int imageWidth = imageBuffer.Width;
            int imageHeight = imageBuffer.Height;
            byte[] buffer = imageBuffer.GetBuffer();

            foreach (ValidResponseData validResponse in validResponsesBotomToTopList)
            {
                Vector2 position = validResponse.position;
                int centerXInt = (int)(position.x + .5);
                int centerYInt = (int)(position.y + .5);

                int min = int.MaxValue;
                int max = int.MinValue;
                {
                    for (int y = centerYInt - 5; y <= centerYInt + 5; y++)
                    {
                        int byteOffset = imageBuffer.GetBufferOffsetY(y);
                        ValidResponseData[] totalResponseRow = allResponsesGrid.GetRow(y);
                        for (int x = centerXInt - 5; x <= centerXInt + 5; x++)
                        {
                            int intensity = buffer[byteOffset + x];
                            if (intensity < min) min = intensity;
                            if (intensity > max) max = intensity;
                        }
                    }
                }
                double center = (max - min) / 2 + min;
                double maxRange = (max - min) / 4;

                {
                    double weight = 0;
                    Vector2 accumulatedPosition = Vector2.Zero;
                    for (int y = centerYInt - 5; y <= centerYInt + 5; y++)
                    {
                        int byteOffset = imageBuffer.GetBufferOffsetY(y);
                        ValidResponseData[] totalResponseRow = allResponsesGrid.GetRow(y);
                        for (int x = centerXInt - 5; x <= centerXInt + 5; x++)
                        {
                            int value = buffer[byteOffset + x];
                            double absDeltaFromCenter = Math.Abs(value - center);
                            double contribution = 1 - (absDeltaFromCenter / maxRange);
                            contribution = Math.Max(0, Math.Min(1, contribution));
                            double distScalling = Math.Min(1, Math.Max(0, 1 - ((new Vector2(x, y) - position).Length - 3) / 2));
                            contribution *= distScalling;
                            weight += contribution;
                            accumulatedPosition += new Vector2(x, y) * contribution;

#if SHOW_SUB_PIXEL_LOGIC
                                if(i==4) buffer[byteOffset + x] = (byte)(contribution * 255);
#endif
                        }
                    }

                    validResponse.position = accumulatedPosition / weight;
                }
            }
        }

        void FindChessBoardLines()
        {
            List<ValidResponseData> sortedList = new List<ValidResponseData>(validResponsesBotomToTopList);

            // remove all the points that can't be part of a chess board
            for (int i = validResponsesBotomToTopList.Count - 1; i >= 0; i--)
            {
                ValidResponseData checkItem = validResponsesBotomToTopList[i];

                sortedList.Sort(new SortOnDistance(checkItem.position));

                int numThatAreValid = 0;
                if (sortedList.Count > 5)
                {
                    for (int j = 1; j < 5; j++)
                    {
                        ValidResponseData nextNearest = sortedList[j];
                        if (nextNearest == checkItem)
                        {
                            continue;
                        }

                        int delta = Math.Abs(checkItem.orientation - nextNearest.orientation);
                        if (delta > 2 && delta < 5)
                        {
                            numThatAreValid++;
                        }
                    }

                    // find out if it has at least 2 valid connections next to it.
                    if (numThatAreValid < 2)
                    {
                        validResponsesBotomToTopList.RemoveAt(i);
                        sortedList = new List<ValidResponseData>(validResponsesBotomToTopList);
                    }
                }
            }

            // Mark how many neighbors every corner has
            if (validResponsesBotomToTopList.Count > 5)
            {
                foreach (ValidResponseData item in validResponsesBotomToTopList)
                {
                    sortedList.Sort(new SortOnDistance(item.position));

                    int numNeighborsThatCouldBeOnChessboard = 0;
                    for (int i = 1; i < 5; i++)
                    {
                        ValidResponseData nextNearest = sortedList[i];
                        if (nextNearest == item)
                        {
                            continue;
                        }

                        int delta = Math.Abs(item.orientation - nextNearest.orientation);
                        if (delta > 2 && delta < 5)
                        {
                            numNeighborsThatCouldBeOnChessboard++;
                        }
                    }

                    item.has4ChessboardNeighbors = (numNeighborsThatCouldBeOnChessboard == 4);
                    if (item.has4ChessboardNeighbors)
                    {
                        item.chessboardConnections = new ChessboardConnections(sortedList[0], sortedList[1], sortedList[2], sortedList[3], sortedList[4]);
                    }
                }

                lowestCorrner = null;
                // find the lowest corner on the board
                foreach (ValidResponseData item in validResponsesBotomToTopList)
                {
                    if (item.has4ChessboardNeighbors)
                    {
                        sortedList.Sort(new SortOnDistance(item.position));

                        if (lowestCorrner == null)
                        {
                            int countOfNeighborsWith4Neighbors = 0;
                            for (int i = 1; i < 5; i++)
                            {
                                if (sortedList[i].has4ChessboardNeighbors)
                                {
                                    countOfNeighborsWith4Neighbors++;
                                }
                            }

                            if (countOfNeighborsWith4Neighbors == 2)
                            {
                                lowestCorrner = item.chessboardConnections;
                            }
                        }
                    }
                }

                // now that we have the lowest corner find the rest of the chessboard from it
            }
        }

        public void ProcessNewImage(ImageBuffer imageBuffer, int totalResponseThreshold)
        {
#if SHOW_SUB_PIXEL_LOGIC            
            gray = imageBuffer;
#endif
            CalculateSumResponseAtAllPixels(imageBuffer, totalResponseThreshold);
            CalculateOrientationAtAllFeatures(imageBuffer);
            MergeResponsesThatAreSameFeatures();
            FindSubPixelPositions(imageBuffer);

            FindChessBoardLines();
        }
    }
}
