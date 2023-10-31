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
                    bounds.ExpandToInclude(vertex.position);
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
                    totalLength += (lastVertex - vertex.position).Length;
                    lastVertex = vertex.position;
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
                    double segmentLength = (previousVertex - vertex.position).Length;
                    if (accumulatedLength + segmentLength >= targetLength)
                    {
                        // Interpolate between the two points to get the exact position.
                        double remainingLength = targetLength - accumulatedLength;
                        double segmentRatio = remainingLength / segmentLength;

                        return Vector2.Lerp(previousVertex, vertex.position, segmentRatio);
                    }

                    accumulatedLength += segmentLength;
                    previousVertex = vertex.position;
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

        public static void RenderCurve(this IVertexSource source, Graphics2D graphics2D, Color lineColor, double width = 1, bool showHandles = false, Color handleLineColor = default(Color), Color handleColor = default(Color))
        {
            if(source.Vertices().Count() < 2)
            {
                return;
            }

            // flaten the curve
            var flattenedCurve = new FlattenCurves(source);
            var stroked = new Stroke(flattenedCurve, width);

            // render the curve
            graphics2D.Render(stroked, lineColor);

            if (showHandles)
            {
                if (handleLineColor == default(Color))
                {
                    handleLineColor = lineColor;
                }
                if (handleColor == default(Color))
                {
                    handleColor = lineColor;
                }

                var controlSize = width * 3;

                // iterate the original source looking for curve vertices
                var vertices = source.Vertices().ToArray();
                for (int i = 0; i< vertices.Length; i++)
                {
                    var vertex = vertices[i];
                    var nextVertex = vertices[(i + 1) % vertices.Length];
                    var nextNextVertex = vertices[(i + 2) % vertices.Length];
                    if (vertex.command == ShapePath.FlagsAndCommand.Curve4)
                    {
                        // render the control lines
                        graphics2D.Line(vertex.position, nextVertex.position, handleLineColor);
                        graphics2D.Line(vertex.position, nextNextVertex.position, handleLineColor);

                        // render the control points
                        graphics2D.Render(new Ellipse(nextVertex.position, controlSize), handleColor);
                        graphics2D.Render(new Ellipse(nextNextVertex.position, controlSize), handleColor);

                        graphics2D.Render(new Ellipse(vertex.position, controlSize), lineColor);

                        i += 2;
                    }
                }
            }
        }

        public static double GetXAtY(this IVertexSource source, double y)
        {
            Vector2? previousVertex = null;

            // These will store the x values for the highest y below the given y
            // and the lowest y above the given y, respectively.
            double? belowBoundX = null;
            double? aboveBoundX = null;
            double highestYBelow = double.NegativeInfinity;
            double lowestYAbove = double.PositiveInfinity;

            foreach (var vertex in source.Vertices())
            {
                if (previousVertex.HasValue 
                    && vertex.IsVertex 
                    && vertex.IsLineTo)
                {
                    if ((y >= previousVertex.Value.Y && y <= vertex.position.Y)
                        || (y <= previousVertex.Value.Y && y >= vertex.position.Y))
                    {
                        // The y value lies between the y values of the current segment
                        if (previousVertex.Value.Y == vertex.position.Y)
                        {
                            // If the segment is horizontal, just return any x as all x's will satisfy
                            return previousVertex.Value.X;
                        }

                        // Interpolate to find the x value for the given
                        var deltaFromPrevious = y - previousVertex.Value.Y;
                        var segmentYLength = vertex.position.Y - previousVertex.Value.Y;
                        double ratioOfLength = deltaFromPrevious / segmentYLength;
                        var segmentXLength = vertex.position.X - previousVertex.Value.X;
                        var x = previousVertex.Value.X + ratioOfLength * segmentXLength;

                        return x;
                    }
                    else if (y > vertex.position.Y && vertex.position.Y > highestYBelow)
                    {
                        // Update the below bound
                        highestYBelow = vertex.position.Y;
                        belowBoundX = vertex.position.X;
                    }
                    else if (y < vertex.position.Y && vertex.position.Y < lowestYAbove)
                    {
                        // Update the above bound
                        lowestYAbove = vertex.position.Y;
                        aboveBoundX = vertex.position.X;
                    }
                }

                if (!vertex.IsClose && !vertex.IsStop)
                {
                    previousVertex = vertex.position;
                }
            }

            // If we're out of bounds below the path, return the below bound x value
            if (belowBoundX.HasValue && y < highestYBelow)
            {
                return belowBoundX.Value;
            }

            // If we're out of bounds above the path, return the above bound x value
            if (aboveBoundX.HasValue && y > lowestYAbove)
            {
                return aboveBoundX.Value;
            }

            // If no segment is found containing the y value, throw an exception or handle accordingly
            throw new InvalidOperationException($"No x value found for y = {y} in the given path.");
        }

        public static ulong GetLongHashCode(this IVertexSource source, ulong hash = 14695981039346656037)
        {
            foreach (var vertex in source.Vertices())
            {
                hash = vertex.GetLongHashCode(hash);
            }

            return hash;
        }

        public static IVertexSource Transform(this IVertexSource source, Matrix4X4 matrix)
        {
            RectangleDouble bounds = RectangleDouble.ZeroIntersection;

            var output = new VertexStorage();
            foreach (var vertex in source.Vertices())
            {
                var position = new Vector3(vertex.X, vertex.Y, 0);
                position = position.Transform(matrix);
                output.Add(position.X, position.Y, vertex.command);
            }

            return output;
        }
    }
}