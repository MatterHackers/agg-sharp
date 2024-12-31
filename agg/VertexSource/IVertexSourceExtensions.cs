//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Image.ThresholdFunctions;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.Agg.VertexSource
{
    public static class IVertexSourceExtensions
    {
        public static RectangleDouble GetBounds(this IVertexSource source)
        {
            RectangleDouble bounds = RectangleDouble.ZeroIntersection;
            foreach (var vertex in source.Vertices())
            {
                if (!vertex.IsClose && !vertex.IsStop)
                {
                    bounds.ExpandToInclude(vertex.Position);
                }
            }

            return bounds;
        }

        public static Vector2 GetPointAtRatio(this IVertexSource source, double ratio)
        {
            if (ratio < 0 || ratio > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(ratio), "Ratio must be between 0 and 1 inclusive.");
            }

            double totalLength = 0;
            Vector2 lastVertex = new Vector2(0, 0);

            // Compute the total length of the path.
            foreach (var vertex in source.Vertices())
            {
                if (!vertex.IsClose && !vertex.IsStop)
                {
                    totalLength += (lastVertex - vertex.Position).Length;
                    lastVertex = vertex.Position;
                }
            }

            double targetLength = totalLength * ratio;
            double accumulatedLength = 0;

            Vector2 previousVertex = new Vector2(0, 0);

            // Walk the path again and stop when the accumulated length matches the target.
            foreach (var vertex in source.Vertices())
            {
                if (!vertex.IsClose && !vertex.IsStop)
                {
                    double segmentLength = (previousVertex - vertex.Position).Length;
                    if (accumulatedLength + segmentLength >= targetLength)
                    {
                        // Interpolate between the two points to get the exact position.
                        double remainingLength = targetLength - accumulatedLength;
                        double segmentRatio = remainingLength / segmentLength;

                        return Vector2.Lerp(previousVertex, vertex.Position, segmentRatio);
                    }

                    accumulatedLength += segmentLength;
                    previousVertex = vertex.Position;
                }
            }

            // If for some reason we get here, return the last vertex.
            return lastVertex;
        }

        public static Vector2 GetWeightedCenter(this IVertexSource vertexSource)
        {
            var polygonBounds = vertexSource.GetBounds();

            int width = 128;
            int height = 128;

            // Set the transform to image space
            var polygonsToImageTransform = Affine.NewIdentity();
            // move it to 0, 0
            polygonsToImageTransform *= Affine.NewTranslation(-polygonBounds.Left, -polygonBounds.Bottom);
            // scale to fit cache
            polygonsToImageTransform *= Affine.NewScaling(width / (double)polygonBounds.Width, height / (double)polygonBounds.Height);
            // and move it in 2 pixels
            polygonsToImageTransform *= Affine.NewTranslation(2, 2);

            // and render the polygon to the image
            var imageBuffer = new ImageBuffer(width + 4, height + 4, 8, new blender_gray(1));
            imageBuffer.NewGraphics2D().Render(new VertexSourceApplyTransform(vertexSource, polygonsToImageTransform), Color.White);

            // center for image
            var centerPosition = imageBuffer.GetWeightedCenter(new MapOnMaxIntensity());
            // translate to vertex source coordinates
            polygonsToImageTransform.inverse_transform(ref centerPosition.X, ref centerPosition.Y);

            return centerPosition;
        }

        public static void RenderPath(this IVertexSource source, 
            Graphics2D graphics2D, 
            Color lineColor, double width = 1, 
            bool showHandles = false, Color handleLineColor = default, 
            Color handleColor = default,
            IEnumerable<int> selectedPoints = null,
            Color selectedPointColor = default)
        {
            if(source.Vertices().Count() < 2)
            {
                return;
            }

            // render the flatened curve
            using (new QuickTimerReport("View3DWidegt.RenderPath"))
            {
                graphics2D.Render(new Stroke(new FlattenCurves(source), width), lineColor);
            }

            if (showHandles)
            {
                if (handleLineColor == default)
                {
                    handleLineColor = lineColor;
                }
                if (handleColor == default)
                {
                    handleColor = lineColor;
                }

                var controlSize = width * 3;

                // iterate the original source looking for curve vertices
                var curveIndex = 0;
                var lastCommand = FlagsAndCommand.Stop;
                var vertices = source.Vertices().ToArray();
                for (int i = 0; i < vertices.Length; i++)
                {
                    var vertexData = vertices[i];
                    if (lastCommand != vertexData.Command)
                    {
                        curveIndex = 0;
                    }

                    if (selectedPoints != null
                        && selectedPoints.Contains(i))
                    {
                        graphics2D.Render(new Stroke(new Ellipse(vertexData.Position, controlSize * 1.5), 2), selectedPointColor);
                    }

                    var prevVertex = vertices[(i - 1 + vertices.Length) % vertices.Length];
                    var nextVertex = vertices[(i + 1) % vertices.Length];
                    switch (vertexData.Command)
                    {
                        case FlagsAndCommand.Curve4:
                            switch (curveIndex)
                            {
                                case 0:
                                    Assert(source.GetCommandHint(i) == CommandHint.C4ControlFromPrev);
                                    // draw the line from the previous vertex to the control point
                                    graphics2D.Line(prevVertex.Position, vertexData.Position, lineColor);
                                    //  draw the control point for the current vertex
                                    graphics2D.Render(new Ellipse(vertexData.Position, controlSize), handleColor);
                                    curveIndex++;
                                    break;

                                case 1:
                                    Assert(source.GetCommandHint(i) == CommandHint.C4ControlToPoint);
                                    // draw the line from the current vertex to the control point
                                    graphics2D.Line(vertexData.Position, nextVertex.Position, lineColor);
                                    // draw the control point for the current vertex
                                    graphics2D.Render(new Ellipse(vertexData.Position, controlSize), handleColor);
                                    curveIndex++;
                                    break;

                                case 2:
                                default:
                                    Assert(source.GetCommandHint(i) == CommandHint.C4Point);
                                    // draw the control point
                                    graphics2D.Render(new Ellipse(vertexData.Position, controlSize), lineColor);
                                    curveIndex = 0;
                                    break;
                            }
                            break;

                        case FlagsAndCommand.Curve3:
                            switch (curveIndex)
                            {
                                case 0:
                                    Assert(source.GetCommandHint(i) == CommandHint.C3ControlFromPrev);
                                    // draw the line from the previous vertex to the control point
                                    graphics2D.Line(prevVertex.Position, vertexData.Position, lineColor);
                                    // draw the line from the control ponit to the next vertex
                                    graphics2D.Line(vertexData.Position, nextVertex.Position, lineColor);
                                    //  draw the control point for the current vertex
                                    graphics2D.Render(new Ellipse(vertexData.Position, controlSize), handleColor);
                                    curveIndex++;
                                    break;

                                case 1:
                                default:
                                    Assert(source.GetCommandHint(i) == CommandHint.C3Point);
                                    // draw the control point
                                    graphics2D.Render(new Ellipse(vertexData.Position, controlSize), lineColor);
                                    curveIndex = 0;
                                    break;
                            }
                            break;

                        default:
                            graphics2D.Render(new Ellipse(vertexData.Position, controlSize), lineColor);
                            break;
                    }

                    lastCommand = vertexData.Command;
                }
            }
        }

        // make this debug only
        private static void Assert(bool condition)
        {
#if DEBUG
            if (!condition)
            {
                throw new NotImplementedException();
            }
#endif
        }

        public static CommandHint GetCommandHint(this IVertexSource vertexSource, int pointIndex)
        {
            var interationIndex = 0;
            var curveIndex = 0;
            var lastCommand = FlagsAndCommand.Stop;
            var commandHint = CommandHint.None;
            foreach (var vertexData in vertexSource.Vertices())
            {
                if (lastCommand != vertexData.Command)
                {
                    curveIndex = 0;
                    lastCommand = vertexData.Command;
                }

                commandHint = CommandHint.None;

                switch (vertexData.Command & FlagsAndCommand.CommandsMask)
                {
                    case FlagsAndCommand.Curve4:
                        switch (curveIndex)
                        {
                            case 0:
                                commandHint = CommandHint.C4ControlFromPrev;
                                curveIndex++;
                                break;

                            case 1:
                                commandHint = CommandHint.C4ControlToPoint;
                                curveIndex++;
                                break;

                            case 2:
                                commandHint = CommandHint.C4Point;
                                curveIndex = 0;
                                break;

                            default:
                                throw new System.Exception("Invalid curve index");
                        }
                        break;

                    case FlagsAndCommand.Curve3:
                        switch (curveIndex)
                        {
                            case 0:
                                commandHint = CommandHint.C3ControlFromPrev;
                                curveIndex++;
                                break;
                            case 1:
                                commandHint = CommandHint.C3Point;
                                curveIndex = 0;
                                break;

                            default:
                                throw new System.Exception("Invalid curve index");
                        }
                        break;
                }

                if (interationIndex == pointIndex)
                {
                    return commandHint;
                }

                interationIndex++;
            }

            return commandHint;
        }

        public static double GetXAtY(this IVertexSource source, double y)
        {
            Vector2? previousVertex = null;

            // These will store the x values for the highest y below the given y
            // and the lowest y above the given y, respectively.
            var highestPoint = new Vector2(double.NegativeInfinity, double.NegativeInfinity);
            var lowestPoint = new Vector2(double.PositiveInfinity, double.PositiveInfinity);

            foreach (var vertex in source.Vertices())
            {
                if (previousVertex.HasValue 
                    && vertex.IsVertex 
                    && vertex.IsLineTo)
                {
                    if ((y >= previousVertex.Value.Y && y <= vertex.Position.Y)
                        || (y <= previousVertex.Value.Y && y >= vertex.Position.Y))
                    {
                        // The y value lies between the y values of the current segment
                        if (previousVertex.Value.Y == vertex.Position.Y)
                        {
                            // If the segment is horizontal, just return any x as all x's will satisfy
                            return previousVertex.Value.X;
                        }

                        // Interpolate to find the x value for the given
                        var deltaFromPrevious = y - previousVertex.Value.Y;
                        var segmentYLength = vertex.Position.Y - previousVertex.Value.Y;
                        double ratioOfLength = deltaFromPrevious / segmentYLength;
                        var segmentXLength = vertex.Position.X - previousVertex.Value.X;
                        var x = previousVertex.Value.X + ratioOfLength * segmentXLength;

                        return x;
                    }
                }

                if (!vertex.IsClose && !vertex.IsStop)
                {
                    if (vertex.Position.Y > highestPoint.Y)
                    {
                        highestPoint = vertex.Position;
                    }
                    if (vertex.Position.Y < lowestPoint.Y)
                    {
                        lowestPoint = vertex.Position;
                    }
                    
                    previousVertex = vertex.Position;
                }
            }

            // If we're out of bounds below the path, return the below bound x value
            if (y < lowestPoint.Y)
            {
                return lowestPoint.X;
            }

            // If we're out of bounds above the path, return the above bound x value
            return highestPoint.X;
        }

        public static IVertexSource Transform(this IVertexSource source, Matrix4X4 matrix)
        {
            RectangleDouble bounds = RectangleDouble.ZeroIntersection;

            var output = new VertexStorage();
            foreach (var vertex in source.Vertices())
            {
                var position = new Vector3(vertex.X, vertex.Y, 0);
                position = position.Transform(matrix);
                output.Add(position.X, position.Y, vertex.Command);
            }

            return output;
        }
    }
}